using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

/// <summary>Una resolución de una partida. (nuevo)</summary>
public sealed record UnidentifiedItemResolutionDto(Guid PublicId, UnidentifiedItemResolutionKind Kind, decimal Amount, string? DocumentNumber, string? MiscConceptCode,
    DateTime At, string? Note);

/// <summary>Una partida por identificar con su antigüedad y sus resoluciones. (nuevo)</summary>
public sealed record UnidentifiedItemDto(
    Guid PublicId,
    string Number,
    Guid? BankStatementLinePublicId,
    Guid OwnBankAccountPublicId,
    string? BankName,
    DateOnly MovementDate,
    decimal Amount,
    decimal Balance,
    string? Reference,
    string? DepositorName,
    string? DepositorTaxId,
    UnidentifiedItemState Status,
    UnidentifiedItemOrigin OriginKind,
    int AgeDays,
    short AgingStep,
    IReadOnlyList<UnidentifiedItemResolutionDto> Resolutions);

/// <summary>
/// Lo común de las partidas por identificar (feature 015, US6, T0195; FR-070 a FR-073; data-model §5.2): reconocerlas desde un abono —con su número (<c>PI</c>), su
/// identificación y su hecho <c>PartidaPorIdentificarReconocida</c>—, resolverlas en partes y su DTO. Escribe en la unidad de trabajo de quien llama; guarda sólo lo que
/// necesita su Id. (nuevo)
/// </summary>
public sealed class PartidasPorIdentificar(IApplicationDbContext db, NumeradorDeRecaudo numerador, EmisionDeRecaudo emision, AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj)
{
    /// <summary>La clase de numeración de las partidas (<c>RCD:PI:PI</c>).</summary>
    public const string ClaseDeNumeracion = "PI";

    /// <summary>Reconoce una partida por <paramref name="valor"/> del abono: su identificación (<paramref name="secuencia"/> o la siguiente) y su hecho.</summary>
    public async Task<UnidentifiedItem> ReconocerAsync(LineaDeExtractoDto linea, int oficina, decimal valor, int usuario, string? nota, CancellationToken ct,
        int? secuencia = null)
    {
        var banco = linea.BankPublicId is { } b ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) : null;
        var partida = new UnidentifiedItem
        {
            Number = await numerador.AsignarAsync(ClaseDeNumeracion, ClaseDeNumeracion, ct), BankStatementLinePublicId = linea.PublicId,
            OwnBankAccountPublicId = linea.OwnBankAccountPublicId, BankId = banco, BranchId = oficina, MovementDate = linea.MovementDate, Amount = valor, Balance = valor,
            Reference = linea.Reference, DepositorName = linea.DepositorName, DepositorTaxId = linea.DepositorTaxId, OpenedOn = linea.MovementDate,
            OriginKind = UnidentifiedItemOrigin.Statement,
        };
        db.UnidentifiedItems.Add(partida);
        await db.SaveChangesAsync(ct);
        db.BankItemIdentifications.Add(new BankItemIdentification
        {
            BankStatementLinePublicId = linea.PublicId, Sequence = secuencia ?? await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct),
            OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate, Amount = valor, Outcome = BankItemOutcome.UnidentifiedItem,
            UnidentifiedItemId = partida.Id, IdentifiedByUserId = usuario, IdentifiedAt = reloj.UtcNow, Note = nota?.Trim(),
        });
        await emision.PartidaPorIdentificarReconocidaAsync(partida, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.Recognized", "UnidentifiedItem", partida.PublicId, null,
            new { number = partida.Number, line = linea.PublicId, amount = valor, depositor = linea.DepositorTaxId }, ct);
        return partida;
    }

    /// <summary>
    /// Feature 015, R2 (fase 38; FR-089): reconoce como partida un pago de un archivo de convenio o de pasarela que no se identificó por su referencia (o que el contrato
    /// rechazó): el dinero llegó a la cuenta recaudadora del convenio. Sin línea del extracto: el archivo y su línea quedan en la nota. Con su hecho. Sin guardar la línea.
    /// </summary>
    public async Task<UnidentifiedItem> ReconocerDeArchivoAsync(Guid cuentaPropia, int oficina, DateOnly fecha, decimal valor, string? referencia, string? consignante,
        string? documento, string archivo, int linea, string motivo, CancellationToken ct)
    {
        var partida = new UnidentifiedItem
        {
            Number = await numerador.AsignarAsync(ClaseDeNumeracion, ClaseDeNumeracion, ct), OwnBankAccountPublicId = cuentaPropia, BranchId = oficina, MovementDate = fecha,
            Amount = valor, Balance = valor, Reference = referencia, DepositorName = consignante, DepositorTaxId = documento, OpenedOn = fecha,
            OriginKind = UnidentifiedItemOrigin.AgreementFile,
        };
        db.UnidentifiedItems.Add(partida);
        await db.SaveChangesAsync(ct);
        await emision.PartidaPorIdentificarReconocidaAsync(partida, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.Recognized", "UnidentifiedItem", partida.PublicId, null,
            new { number = partida.Number, file = archivo, line = linea, amount = valor, reason = motivo, depositor = documento }, ct);
        _ = reloj;
        return partida;
    }

    /// <summary>Baja el saldo de la partida por una resolución y deja su estado. Sin guardar.</summary>
    public static UnidentifiedItemResolution Resolver(IApplicationDbContext db, UnidentifiedItem partida, UnidentifiedItemResolutionKind clase, decimal valor, Guid documento,
        string? numero, int? concepto, Guid? aprobacion, int usuario, DateTime ahora, string? nota)
    {
        partida.Balance -= valor;
        partida.Status = partida.Balance > 0m
            ? UnidentifiedItemState.PartiallyIdentified
            : clase switch
            {
                UnidentifiedItemResolutionKind.RefundedByOrder => UnidentifiedItemState.ReturnedToDepositor,
                UnidentifiedItemResolutionKind.ClassifiedToConcept or UnidentifiedItemResolutionKind.MovedToIncome => UnidentifiedItemState.Classified,
                _ => UnidentifiedItemState.Identified,
            };
        var r = new UnidentifiedItemResolution
        {
            UnidentifiedItemId = partida.Id, Kind = clase, Amount = valor, DocumentPublicId = documento, DocumentNumber = numero, MiscConceptId = concepto,
            ApprovalRequestPublicId = aprobacion, ByUserId = usuario, At = ahora, Note = nota?.Trim(),
        };
        db.UnidentifiedItemResolutions.Add(r);
        return r;
    }

    public static async Task<UnidentifiedItemDto> DtoAsync(IApplicationDbContext db, UnidentifiedItem p, DateOnly hoy, CancellationToken ct)
    {
        var banco = p.BankId is { } b ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == b).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var resoluciones = await (from r in db.UnidentifiedItemResolutions.AsNoTracking()
                                  join c in db.MiscConcepts.AsNoTracking().IgnoreQueryFilters() on r.MiscConceptId equals c.Id into cs
                                  from c in cs.DefaultIfEmpty()
                                  where r.UnidentifiedItemId == p.Id
                                  orderby r.Id
                                  select new UnidentifiedItemResolutionDto(r.PublicId, r.Kind, r.Amount, r.DocumentNumber, c == null ? null : c.Code, r.At, r.Note)).ToListAsync(ct);
        return new UnidentifiedItemDto(p.PublicId, p.Number, p.BankStatementLinePublicId, p.OwnBankAccountPublicId, banco, p.MovementDate, p.Amount, p.Balance, p.Reference,
            p.DepositorName, p.DepositorTaxId, p.Status, p.OriginKind, hoy.DayNumber - p.OpenedOn.DayNumber, p.AgingStep, resoluciones);
    }
}
