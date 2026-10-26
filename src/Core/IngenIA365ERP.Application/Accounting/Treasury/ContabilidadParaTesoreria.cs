using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Treasury;

/// <summary>
/// Lo que Contabilidad responde a Tesorería (feature 015, US10, T0137; E-04; R-22; FR-102, FR-103, FR-209; contracts/interfaces.md §3). Es el <b>único</b> que
/// escribe <c>ACC_OwnBankAccountLinks</c>: Tesorería pide el vínculo de su cuenta propia con la cuenta contable bancaria del plan y nunca ve la cuenta; sólo el
/// vínculo resuelto (código y nombre) para mostrarlo. Desde la fase 16 (T0259) la validación previa y el saldo de un rol salen de la matriz de Tesorería; las
/// cuentas por pagar (T2) siguen vacías.
/// El saldo en libros y los movimientos de la cuenta bancaria salen de <c>ACC_JournalEntries</c> contabilizados (no hay saldos guardados, FR-046 de la 009). Las
/// escrituras van a la unidad de trabajo de quien llama, sin guardar: el alta de la cuenta propia y su vínculo nacen en el mismo guardado. (nuevo)
/// </summary>
public sealed class ContabilidadParaTesoreria(IApplicationDbContext db, IDateTimeService reloj, MediatR.ISender sender,
    RecaudoYTesoreria.ResolutorDeLaMatriz? resolutor = null, RecaudoYTesoreria.EvaluacionPreviaDeModulo? evaluacion = null) : IContabilidadParaTesoreria
{
    public async Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct)
    {
        var estado = await sender.Send(new Lending.Consultas.GetAccountingPeriodStatusQuery(fecha), ct);
        return estado.IsSuccess ? estado.Value : new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null);
    }

    public const string PoliticaProvisional = "SinMatriz";

    public const string CodigoYaVinculada = "Accounting.OwnBankAccount.AlreadyLinked";
    public const string CodigoNoBancaria = "Accounting.OwnBankAccount.NotBankAccount";

    /// <summary>La validación previa por la matriz <c>ACC_TreasuryPostingRules</c> (fase 16, T0259); sin evaluador, «contabilizable» con la política provisional.</summary>
    public Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct) =>
        evaluacion is null
            ? Task.FromResult(new EvaluacionContableDto(true, PoliticaProvisional, []))
            : evaluacion.EvaluarAsync(RecaudoYTesoreria.MatricesDeRecaudoYTesoreria.Tesoreria, combinaciones, ct);

    public async Task<IReadOnlyList<CuentaContableBancariaDto>> CuentasBancariasDelPlanAsync(Guid? bankPublicId, CancellationToken ct)
    {
        var consulta = db.ChartOfAccounts.AsNoTracking().Where(c => c.BankId != null && c.IsMovement && c.IsActive);
        if (bankPublicId is { } banco) consulta = consulta.Where(c => c.Bank!.PublicId == banco);
        return await consulta.OrderBy(c => c.Code)
            .Select(c => new CuentaContableBancariaDto(c.PublicId, c.Code, c.Name, c.Bank!.PublicId, c.BankAccountNumber))
            .ToListAsync(ct);
    }

    public async Task<Result> VincularCuentaBancariaAsync(Guid ownBankAccountPublicId, Guid chartAccountPublicId, DateOnly desde, CancellationToken ct)
    {
        var cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == chartAccountPublicId, ct);
        if (cuenta is null || !cuenta.EsBancaria || !cuenta.IsMovement || !cuenta.IsActive)
            return Result.Failure(new Error(CodigoNoBancaria,
                "La cuenta contable elegida no es una cuenta bancaria de movimiento activa del plan: elija la auxiliar del banco (tiene banco y número)."));

        var vigentes = await db.OwnBankAccountLinks.Where(l => l.ValidTo == null).ToListAsync(ct);
        var otra = vigentes.FirstOrDefault(l => l.ChartOfAccountId == cuenta.Id && l.OwnBankAccountPublicId != ownBankAccountPublicId);
        if (otra is not null)
            return Result.Failure(new ErrorConDatos(CodigoYaVinculada,
                string.Create(CultureInfo.InvariantCulture, $"La cuenta contable {cuenta.Code} ya representa a otra cuenta bancaria propia: el vínculo es único en los dos sentidos."),
                new { chartAccount = cuenta.Code }));

        var propia = vigentes.FirstOrDefault(l => l.OwnBankAccountPublicId == ownBankAccountPublicId);
        if (propia is not null)
        {
            if (propia.ChartOfAccountId == cuenta.Id) return Result.Success();
            if (propia.ValidFrom >= desde) propia.IsDeleted = true;
            else propia.ValidTo = desde.AddDays(-1);
        }
        db.OwnBankAccountLinks.Add(new OwnBankAccountLink
        {
            OwnBankAccountPublicId = ownBankAccountPublicId,
            ChartOfAccountId = cuenta.Id,
            ValidFrom = desde,
            CreatedAt = reloj.UtcNow,
        });
        return Result.Success();
    }

    public async Task<VinculoDto?> VinculoAsync(Guid ownBankAccountPublicId, CancellationToken ct) =>
        await (from l in db.OwnBankAccountLinks.AsNoTracking()
               join c in db.ChartOfAccounts.AsNoTracking() on l.ChartOfAccountId equals c.Id
               where l.OwnBankAccountPublicId == ownBankAccountPublicId && l.ValidTo == null
               select new VinculoDto(l.OwnBankAccountPublicId, c.PublicId, c.Code, l.ValidFrom)).FirstOrDefaultAsync(ct);

    public async Task<decimal> SaldoEnLibrosAsync(Guid ownBankAccountPublicId, DateOnly fecha, CancellationToken ct)
    {
        var cuenta = await CuentaVinculadaAsync(ownBankAccountPublicId, fecha, ct);
        if (cuenta is null) return 0m;
        return await db.JournalEntries.AsNoTracking()
            .Where(j => j.AccountId == cuenta && j.IsPosted && j.Date <= fecha)
            .SumAsync(j => (decimal?)(j.Debit - j.Credit), ct) ?? 0m;
    }

    public async Task<IReadOnlyList<MovimientoContableDeCuentaDto>> MovimientosDeCuentaBancariaAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta,
        CancellationToken ct)
    {
        var cuenta = await CuentaVinculadaAsync(ownBankAccountPublicId, hasta, ct);
        if (cuenta is null) return [];
        var filas = await (from j in db.JournalEntries.AsNoTracking()
                           join d in db.AccountingDocuments.AsNoTracking() on j.DocumentId equals d.Id
                           join t in db.VoucherTypes.AsNoTracking() on d.VoucherTypeId equals t.Id
                           where j.AccountId == cuenta && j.IsPosted && j.Date >= desde && j.Date <= hasta
                           orderby j.Date, d.Id, j.LineNumber
                           select new { j.PublicId, t.Code, d.Number, j.Date, j.Debit, j.Credit, j.Description, d.OriginModule }).ToListAsync(ct);
        return filas.Select(f => new MovimientoContableDeCuentaDto(f.PublicId,
            string.Create(CultureInfo.InvariantCulture, $"{f.Code}-{f.Number}"), f.Date, f.Debit, f.Credit, f.Description, f.OriginModule)).ToList();
    }

    /// <summary>El saldo contable de un rol de la matriz de Tesorería (fase 16, T0259).</summary>
    public Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct) =>
        resolutor is null
            ? Task.FromResult<IReadOnlyList<SaldoContableDto>>([])
            : RecaudoYTesoreria.SaldosPorRol.SaldoAsync(db, resolutor, RecaudoYTesoreria.MatricesDeRecaudoYTesoreria.Tesoreria, solicitud, ct);

    /// <summary>
    /// Feature 015, T2 (fase 42, T0455; E-04; FR-125, FR-167): las cuentas por pagar contables que Tesorería puede pagar: lo pendiente por tercero y documento cruce en las
    /// cuentas que la matriz de Tesorería tuvo para el rol <c>CuentaPorPagarContable</c> —«las cuentas que Contabilidad marca por pagar»—, con los pagos de Tesorería ya
    /// restados (debitan la misma cuenta con el mismo cruce). Sin reglas para el rol, ninguna. La fecha es la del primer movimiento del documento (el libro no guarda el
    /// vencimiento) y el filtro <c>DueUntil</c> la compara con ella.
    /// </summary>
    public async Task<IReadOnlyList<CuentaPorPagarDto>> CuentasPorPagarAsync(FiltroDeCuentasPorPagar filtro, CancellationToken ct)
    {
        var cuentas = await db.TreasuryPostingRules.AsNoTracking().Where(r => r.Role == RolCuentaPorPagarContable).Select(r => r.AccountId).Distinct().ToListAsync(ct);
        if (cuentas.Count == 0) return [];
        int? persona = filtro.PersonPublicId is { } pp
            ? await db.People.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;
        if (filtro.PersonPublicId is not null && persona is null) return [];
        var filas = await (from l in db.JournalEntries.AsNoTracking()
                           join d in db.AccountingDocuments.AsNoTracking() on l.DocumentId equals d.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.PersonId equals p.Id
                           where l.IsPosted && cuentas.Contains(l.AccountId) && l.CrossDocumentNumber != null && l.PersonId != null
                                 && (persona == null || l.PersonId == persona)
                           select new { l.CrossDocumentNumber, PersonaPublica = p.PublicId, l.Date, l.Debit, l.Credit, l.Id, d.PublicId }).ToListAsync(ct);
        return filas.GroupBy(f => (f.CrossDocumentNumber!, f.PersonaPublica))
            .Select(g => new CuentaPorPagarDto(g.OrderBy(x => x.Id).First().PublicId, g.Key.PersonaPublica, g.Key.Item1, g.Min(x => x.Date), g.Sum(x => x.Credit - x.Debit)))
            .Where(c => c.Pending > 0m && (filtro.DueUntil == null || c.DueDate <= filtro.DueUntil)).OrderBy(c => c.DueDate).ToList();
    }

    /// <summary>El rol de la matriz de Tesorería de las cuentas por pagar contables (contabilidad.md §5, T2).</summary>
    public const string RolCuentaPorPagarContable = IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.RolesDeTesoreriaT2.CuentaPorPagarContable;

    /// <summary>La cuenta contable vinculada a la cuenta propia en la fecha (el vínculo vigente entonces).</summary>
    private async Task<int?> CuentaVinculadaAsync(Guid propia, DateOnly fecha, CancellationToken ct) =>
        await db.OwnBankAccountLinks.AsNoTracking()
            .Where(l => l.OwnBankAccountPublicId == propia && l.ValidFrom <= fecha && (l.ValidTo == null || l.ValidTo >= fecha))
            .OrderByDescending(l => l.ValidFrom)
            .Select(l => (int?)l.ChartOfAccountId)
            .FirstOrDefaultAsync(ct);

    /// <summary>La completitud de la matriz de Tesorería (fase 19, T0303).</summary>
    public async Task<CompletitudContableDeModuloDto> CompletitudDeLaMatrizAsync(DateOnly fecha, CancellationToken ct) =>
        await Receipts.CompletitudDeModulo.LeerAsync(sender, Posting.ModuloContable.Tesoreria, await EstadoDelPeriodoAsync(fecha, ct), fecha, ct);
}
