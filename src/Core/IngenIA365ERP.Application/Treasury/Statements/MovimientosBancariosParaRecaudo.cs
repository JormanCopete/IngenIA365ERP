using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Statements;

/// <summary>
/// El puerto que Tesorería ofrece a Recaudo sobre el extracto (feature 015, US6, T0193; R-05, R-06; FR-065, FR-197; contracts/interfaces.md §2): el extracto es uno, de
/// Tesorería, y Recaudo identifica sus abonos por aquí. Lee los abonos pendientes de identificar con el alcance de oficinas que pide Recaudo (no el de Tesorería, que una
/// persona de Recaudo no tiene) y escribe sólo lo que Recaudo decide de la línea —cuánto quedó identificado y la decisión sobre un posible duplicado—, en la unidad de trabajo
/// de quien llama y sin guardar. Valida y audita sus escrituras (<c>LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras</c>). (nuevo)
/// </summary>
public sealed class MovimientosBancariosParaRecaudo(IApplicationDbContext db, AuditoriaDeTesoreria auditoria) : IMovimientosBancariosParaRecaudo
{
    public async Task<PaginaDto<LineaDeExtractoDto>> AbonosPorIdentificarAsync(FiltroDeAbonos filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var consulta = from l in db.TreasuryBankStatementLines.AsNoTracking()
                       join c in db.OwnBankAccounts.AsNoTracking() on l.BankAccountId equals c.Id
                       join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on c.BranchId equals b.Id
                       where l.Direction == StatementLineDirection.Credit && l.RecaudoStatus != StatementLineRecaudoStatus.NotApplicable
                       select new { Linea = l, Cuenta = c, Oficina = b.PublicId };
        if (filtro.SoloPendientes)
            consulta = consulta.Where(x => x.Linea.RecaudoStatus == StatementLineRecaudoStatus.Pending || x.Linea.RecaudoStatus == StatementLineRecaudoStatus.PartiallyIdentified);
        if (filtro.OwnBankAccountPublicId is { } cuenta) consulta = consulta.Where(x => x.Cuenta.PublicId == cuenta);
        if (filtro.Desde is { } desde) consulta = consulta.Where(x => x.Linea.MovementDate >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(x => x.Linea.MovementDate <= hasta);
        if (!string.IsNullOrWhiteSpace(filtro.Referencia))
        {
            var r = filtro.Referencia.Trim();
            consulta = consulta.Where(x => (x.Linea.Reference != null && x.Linea.Reference.Contains(r)) || (x.Linea.DepositorTaxId != null && x.Linea.DepositorTaxId == r));
        }
        if (filtro.OficinaPublicIds is { } oficinas) consulta = consulta.Where(x => oficinas.Contains(x.Oficina));
        var total = await consulta.CountAsync(ct);
        var pagina = Math.Max(1, filtro.Page);
        var tamano = Math.Clamp(filtro.PageSize, 1, 500);
        var filas = await consulta.OrderBy(x => x.Linea.MovementDate).ThenBy(x => x.Linea.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        var lista = new List<LineaDeExtractoDto>(filas.Count);
        foreach (var f in filas) lista.Add(await DtoAsync(f.Linea, ct));
        return new PaginaDto<LineaDeExtractoDto>(lista, total, pagina, tamano);
    }

    public async Task<LineaDeExtractoDto?> LineaAsync(Guid statementLinePublicId, CancellationToken ct)
    {
        var linea = await db.TreasuryBankStatementLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == statementLinePublicId, ct);
        return linea is null || linea.Direction != StatementLineDirection.Credit ? null : await DtoAsync(linea, ct);
    }

    public async Task MarcarIdentificacionAsync(Guid statementLinePublicId, decimal identificado, CancellationToken ct)
    {
        var linea = await LineaSeguidaAsync(statementLinePublicId, ct);
        ValidarIdentificado(linea, identificado);
        var antes = new { status = linea.RecaudoStatus.ToString(), identified = linea.RecaudoIdentifiedAmount };
        linea.RecaudoIdentifiedAmount = identificado;
        linea.RecaudoStatus = identificado <= 0m
            ? StatementLineRecaudoStatus.Pending
            : identificado >= linea.Amount ? StatementLineRecaudoStatus.Identified : StatementLineRecaudoStatus.PartiallyIdentified;
        await auditoria.AgregarAsync("Treasury.StatementLine.IdentifiedByReceipts", "BankStatementLine", linea.PublicId, antes,
            new { status = linea.RecaudoStatus.ToString(), identified = identificado }, ct);
    }

    public async Task DecidirDuplicadoAsync(Guid statementLinePublicId, bool esDuplicado, CancellationToken ct)
    {
        var linea = await LineaSeguidaAsync(statementLinePublicId, ct);
        ValidarDuplicado(linea);
        linea.DuplicateDecision = esDuplicado ? StatementLineDuplicateDecision.Duplicate : StatementLineDuplicateDecision.Distinct;
        if (esDuplicado) linea.RecaudoStatus = StatementLineRecaudoStatus.NotApplicable;
        await auditoria.AgregarAsync("Treasury.StatementLine.DuplicateDecided", "BankStatementLine", linea.PublicId, null,
            new { decision = linea.DuplicateDecision.ToString() }, ct);
    }

    public async Task<IReadOnlyList<UltimaCargaDeExtractoDto>> UltimasCargasAsync(CancellationToken ct)
    {
        var cuentas = await (from c in db.OwnBankAccounts.AsNoTracking()
                             join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on c.BranchId equals b.Id
                             where c.Status == TreasuryCatalogStatus.Active
                             select new { c.Id, c.PublicId, c.Code, c.Purposes, Oficina = (Guid?)b.PublicId }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var ultimas = await db.TreasuryBankStatements.AsNoTracking().Where(s => ids.Contains(s.BankAccountId))
            .GroupBy(s => s.BankAccountId).Select(g => new { Cuenta = g.Key, Hasta = g.Max(s => s.To), Cargada = g.Max(s => s.LoadedAt) }).ToListAsync(ct);
        return cuentas.Where(c => c.Purposes.HasFlag(BankAccountPurpose.Collection))
            .Select(c =>
            {
                var u = ultimas.FirstOrDefault(x => x.Cuenta == c.Id);
                return new UltimaCargaDeExtractoDto(c.PublicId, c.Code, c.Oficina, u?.Hasta, u?.Cargada);
            }).ToList();
    }

    /// <summary>El validador del puerto (desviación III): lo identificado no es negativo ni pasa del abono.</summary>
    public static void ValidarIdentificado(BankStatementLine linea, decimal identificado)
    {
        if (linea.Direction != StatementLineDirection.Credit) throw new ArgumentException("Sólo un abono se identifica en Recaudo.", nameof(linea));
        if (identificado < 0m || identificado > linea.Amount)
            throw new ArgumentOutOfRangeException(nameof(identificado), $"Lo identificado ({identificado}) tiene que estar entre 0 y el valor del abono ({linea.Amount}).");
    }

    /// <summary>El validador del puerto (desviación III): sólo se decide un posible duplicado pendiente.</summary>
    public static void ValidarDuplicado(BankStatementLine linea)
    {
        if (linea.DuplicateDecision != StatementLineDuplicateDecision.Pending)
            throw new InvalidOperationException("La línea no es un posible duplicado pendiente de decidir.");
    }

    private async Task<BankStatementLine> LineaSeguidaAsync(Guid publicId, CancellationToken ct) =>
        db.TreasuryBankStatementLines.Local.FirstOrDefault(l => l.PublicId == publicId)
        ?? await db.TreasuryBankStatementLines.FirstOrDefaultAsync(l => l.PublicId == publicId, ct)
        ?? throw new ArgumentException("La línea del extracto no existe.", nameof(publicId));

    private async Task<LineaDeExtractoDto> DtoAsync(BankStatementLine l, CancellationToken ct)
    {
        var c = await (from cuenta in db.OwnBankAccounts.AsNoTracking()
                       join banco in db.Banks.AsNoTracking().IgnoreQueryFilters() on cuenta.BankId equals banco.Id
                       join oficina in db.Branches.AsNoTracking().IgnoreQueryFilters() on cuenta.BranchId equals oficina.Id
                       where cuenta.Id == l.BankAccountId
                       select new { cuenta.PublicId, cuenta.Code, Oficina = oficina.PublicId, Banco = banco.PublicId, banco.TransferCode }).FirstAsync(ct);
        var numero = await db.TreasuryBankStatements.AsNoTracking().Where(s => s.Id == l.BankStatementId).Select(s => s.Number).FirstOrDefaultAsync(ct) ?? string.Empty;
        var duplicaDe = l.PossibleDuplicateOfLineId is { } d
            ? await db.TreasuryBankStatementLines.AsNoTracking().Where(x => x.Id == d).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new LineaDeExtractoDto(l.PublicId, c.PublicId, l.MovementDate, l.ValueDate, l.Amount, l.RecaudoIdentifiedAmount, l.Reference, l.DepositorTaxId,
            l.DepositorName, l.Description, l.DuplicateDecision == StatementLineDuplicateDecision.Pending, c.Code, c.Oficina, c.Banco, c.TransferCode,
            l.RecaudoStatus.ToString(), l.DuplicateDecision?.ToString(), numero, duplicaDe);
    }
}
