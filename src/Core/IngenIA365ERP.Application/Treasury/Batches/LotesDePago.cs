using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using IngenIA365ERP.Domain.Treasury.Lotes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Batches;

// Feature 015, US10 (T0143; FR-110, FR-111, FR-116, FR-206, FR-207, FR-213; data-model §10.3, §10.4, §19.10): lo común de los lotes de pago —el DTO, la huella
// que confirma el aprobador, el armado con ArmadoDeLote y la validación de cada orden, el GMF estimado y el recálculo cuando una orden sale— y lo que hace el lote
// cuando su aprobación se completa o se devuelve. (nuevo)

public sealed record PaymentBatchItemDto(
    Guid PublicId,
    Guid OrderPublicId,
    string OrderNumber,
    string BeneficiaryName,
    string BeneficiaryTaxId,
    PaymentForm Form,
    decimal Amount,
    decimal GmfEstimate,
    PaymentBatchItemFlags Flags,
    bool RequiresReview,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    PaymentBatchItemState Status);

public sealed record PendingOrderDto(Guid OrderPublicId, string OrderNumber, string Reason);

public sealed record PaymentFileSummaryDto(
    Guid PublicId,
    int Version,
    string FileName,
    string Sha256,
    int LineCount,
    decimal Total,
    DateTime GeneratedAt,
    DateTime? DownloadedAt,
    DateTime? SentAt,
    DateTime? VoidedAt,
    DateTime? RejectedByBankAt);

public sealed record PaymentBatchDto(
    Guid PublicId,
    string Number,
    Guid BankAccountPublicId,
    string BankAccountCode,
    PaymentBatchForm Form,
    string? FormatCode,
    DateOnly PayDate,
    int OrderCount,
    decimal Total,
    decimal GmfEstimate,
    PaymentBatchState Status,
    Guid? ApprovalRequestPublicId,
    string PreparedByName,
    DateTime PreparedAt,
    DateTime? SentAt,
    string? UploadedAtBankBy,
    string? AuthorizedAtBankBy,
    string? Note,
    IReadOnlyList<PaymentBatchItemDto> Items,
    IReadOnlyList<PendingOrderDto> Pending,
    IReadOnlyList<PaymentFileSummaryDto> Files,
    bool SignaturesRequired = true,
    string? SignaturesNotice = null);

/// <summary>Lo común de los lotes de pago. (nuevo)</summary>
public static class LotesDePago
{
    public const string Codigo = "LP";

    public const string PermisoDeAprobar = "Treasury.Batches.Approve";

    /// <summary>Lo que el aprobador confirma: la cuenta, la forma, el formato, la fecha y cada orden con su valor y marcas.</summary>
    public static string Huella(PaymentBatch lote, IEnumerable<PaymentBatchItem> items) => HuellaDeOperacion.Calcular("Tesoreria.LoteDePago", new
    {
        lote.PublicId, lote.BankAccountId, lote.Form, lote.BankFileFormatId, lote.PayDate,
        Items = items.Where(i => i.IsLive).OrderBy(i => i.PaymentOrderId).Select(i => new { i.PaymentOrderId, i.Amount, i.Flags }),
    });

    public static async Task<PaymentBatchDto> DtoAsync(IApplicationDbContext db, PaymentBatch lote, IReadOnlyList<PendingOrderDto>? pendientes, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == lote.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        var formato = lote.BankFileFormatId is { } f
            ? await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == f).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        var preparo = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == lote.PreparedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        var items = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join o in db.PaymentOrders.AsNoTracking() on i.PaymentOrderId equals o.Id
                           join u in db.Users.AsNoTracking().IgnoreQueryFilters() on i.ExplicitlyReviewedByUserId equals u.Id into revisores
                           from r in revisores.DefaultIfEmpty()
                           where i.PaymentBatchId == lote.Id && i.Status != PaymentBatchItemState.Removed
                           orderby i.Id
                           select new PaymentBatchItemDto(i.PublicId, o.PublicId, o.Number, o.BeneficiaryName, o.BeneficiaryTaxId, o.Form, i.Amount, i.GmfEstimate, i.Flags,
                               (i.Flags & PaymentBatchItemFlags.RecentAccountChange) != 0 && i.ExplicitlyReviewedByUserId == null, r != null ? r.Username : null,
                               i.ReviewedAt, i.Status)).ToListAsync(ct);
        var archivos = await db.PaymentFiles.AsNoTracking().Where(a => a.PaymentBatchId == lote.Id).OrderBy(a => a.Version)
            .Select(a => new PaymentFileSummaryDto(a.PublicId, a.Version, a.FileName, a.Sha256, a.LineCount, a.Total, a.GeneratedAt, a.DownloadedAt, a.SentAt,
                a.VoidedAt, a.RejectedByBankAt)).ToListAsync(ct);
        // Segunda ronda del 2026-10-27 (D-l; recomendación aceptada): sin firmantes vigentes en la cuenta el lote no exige combinación de firmas, y se informa.
        var conFirmantes = await FirmasDelLote.TieneFirmantesAsync(db, lote.BankAccountId, lote.PayDate, ct);
        return new PaymentBatchDto(lote.PublicId, lote.Number, cuenta.PublicId, cuenta.Code, lote.Form, formato, lote.PayDate, lote.OrderCount, lote.Total,
            lote.GmfEstimate, lote.Status, lote.ApprovalRequestPublicId, preparo ?? string.Empty, lote.PreparedAt, lote.SentAt, lote.UploadedAtBankBy,
            lote.AuthorizedAtBankBy, lote.Note, items, pendientes ?? [], archivos, conFirmantes,
            conFirmantes ? null : "La cuenta de origen no tiene firmantes registrados: el lote no exige combinación de firmas.");
    }

    /// <summary>El lote visible por su <c>PublicId</c> (alcance por la cuenta de origen) o nulo.</summary>
    public static async Task<PaymentBatch?> VisibleAsync(IApplicationDbContext db, IngenIA365ERP.Application.Common.Scope.IAlcanceDeTesoreria alcance, Guid publicId, bool seguir,
        CancellationToken ct)
    {
        var consulta = seguir ? db.PaymentBatches : db.PaymentBatches.AsNoTracking();
        var lote = await consulta.FirstOrDefaultAsync(b => b.PublicId == publicId, ct);
        return lote is not null && (await alcance.ObtenerAsync(ct)).IncluyeCuenta(lote.BankAccountId) ? lote : null;
    }

    /// <summary>El formato del lote: el pedido (de pagos, vigente y del banco de la cuenta o genérico) o el vigente del banco, o el genérico (FR-110, FR-213).</summary>
    public static async Task<Result<BankFileFormat?>> FormatoAsync(IApplicationDbContext db, OwnBankAccount cuenta, PaymentBatchForm forma, Guid? pedido,
        DateOnly fecha, CancellationToken ct)
    {
        if (forma != PaymentBatchForm.File) return Result.Success<BankFileFormat?>(null);
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BankId).Select(b => b.Name).FirstAsync(ct);
        if (pedido is { } id)
        {
            var f = await db.BankFileFormats.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == id, ct);
            if (f is null || f.Scope != BankFileScope.TreasuryPayments || !f.VigenteEn(fecha)) return Result.Failure<BankFileFormat?>(ErroresDeTesoreria.SinFormato(banco));
            if (f.BankId is { } delFormato && delFormato != cuenta.BankId) return Result.Failure<BankFileFormat?>(ErroresDeTesoreria.FormatoDistinto(f.Code, banco));
            return Result.Success<BankFileFormat?>(f);
        }
        var candidatos = await db.BankFileFormats.AsNoTracking()
            .Where(f => f.Scope == BankFileScope.TreasuryPayments && f.IsActive && f.ValidFrom <= fecha && (f.ValidTo == null || f.ValidTo >= fecha))
            .ToListAsync(ct);
        var elegido = candidatos.Where(f => f.BankId == cuenta.BankId).OrderByDescending(f => f.ValidFrom).FirstOrDefault()
                      ?? candidatos.Where(f => f.BankId == null).OrderByDescending(f => f.ValidFrom).FirstOrDefault();
        return elegido is null ? Result.Failure<BankFileFormat?>(ErroresDeTesoreria.SinFormato(banco)) : Result.Success<BankFileFormat?>(elegido);
    }

    /// <summary>
    /// Arma las órdenes pedidas en el lote: las valida de nuevo (<see cref="ValidadorDeOrdenes"/>), las pasa por <see cref="ArmadoDeLote"/> y agrega un ítem vivo por
    /// cada una que entra, con su GMF estimado; la orden queda <see cref="PaymentOrderState.InBatch"/>. Devuelve las que no entran con su motivo. Sin guardar.
    /// </summary>
    public static async Task<IReadOnlyList<PendingOrderDto>> AgregarAsync(IApplicationDbContext db, ValidadorDeOrdenes validador, ParametrosDeGmf gmf,
        PaymentBatch lote, OwnBankAccount cuenta, IReadOnlyList<PaymentOrder> ordenes, CancellationToken ct)
    {
        var ids = ordenes.Select(o => o.Id).ToList();
        var enOtro = await db.PaymentBatchItems.AsNoTracking().Where(i => ids.Contains(i.PaymentOrderId) && i.IsLive && i.PaymentBatchId != lote.Id)
            .Select(i => i.PaymentOrderId).ToListAsync(ct);
        var candidatas = new List<OrdenCandidata>();
        foreach (var o in ordenes)
        {
            VerificacionDeDestino destino = VerificacionDeDestino.Limpia;
            if (ValidadorDeOrdenes.Revalidable(o.Status) && !o.PaidByLendingFile && !enOtro.Contains(o.Id))
                destino = (await validador.ValidarAsync(o, ct)).Destino;
            candidatas.Add(new OrdenCandidata(o.Id, o.Status, o.Form, o.NetAmount, o.PaidByLendingFile, enOtro.Contains(o.Id), o.VoidRequested,
                ValidadorDeOrdenes.Motivos(o), destino));
        }
        var armado = ArmadoDeLote.Armar(candidatas, lote.Form);
        foreach (var incluida in armado.Incluidas)
        {
            var orden = ordenes.First(o => o.Id == incluida.OrdenId);
            var evaluacion = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, orden.BeneficiaryIsDebtor,
                cuenta.IsGmfExempt, orden.Origin == PaymentOrderOrigin.DepositsPayout), gmf);
            db.PaymentBatchItems.Add(new PaymentBatchItem
            {
                PaymentBatchId = lote.Id, PaymentOrderId = orden.Id, Amount = incluida.Valor, GmfEstimate = evaluacion.Gravado ? evaluacion.Gmf : 0m,
                Flags = incluida.Marcas,
            });
            orden.Status = PaymentOrderState.InBatch;
            orden.GmfEstimate = evaluacion.Gravado ? evaluacion.Gmf : 0m;
        }
        return armado.Pendientes.Select(p => new PendingOrderDto(ordenes.First(o => o.Id == p.OrdenId).PublicId, ordenes.First(o => o.Id == p.OrdenId).Number, p.Motivo))
            .ToList();
    }

    /// <summary>Recalcula totales, cantidad, GMF y huella del lote con sus ítems vivos (los agregados en esta unidad de trabajo incluidos).</summary>
    public static async Task RecalcularAsync(IApplicationDbContext db, PaymentBatch lote, CancellationToken ct)
    {
        var deLaBase = await db.PaymentBatchItems.Where(i => i.PaymentBatchId == lote.Id).ToListAsync(ct);
        var todos = deLaBase.Concat(db.PaymentBatchItems.Local.Where(i => i.PaymentBatchId == lote.Id && !deLaBase.Contains(i))).Where(i => i.IsLive).ToList();
        lote.OrderCount = todos.Count;
        lote.Total = Math.Round(todos.Sum(i => i.Amount), 2, MidpointRounding.AwayFromZero);
        lote.GmfEstimate = todos.Sum(i => i.GmfEstimate);
        lote.InputsHash = Huella(lote, todos);
    }

    /// <summary>
    /// Saca una orden de su lote (anulación del origen, FR-115; o la persona que arma el lote): el ítem queda <see cref="PaymentBatchItemState.Removed"/> y deja de
    /// estar vivo; el lote se recalcula. Un lote en aprobación cambió lo que se aprueba: su solicitud se invalida y vuelve a borrador. Un archivo generado y no
    /// enviado incluía la orden: se anula y el lote vuelve a aprobado (hay que generar otro). Sin guardar.
    /// </summary>
    public static async Task SacarAsync(IApplicationDbContext db, IMotorDeAprobaciones motor, PaymentBatchItem item, string motivo, DateTime ahora, CancellationToken ct)
    {
        item.Status = PaymentBatchItemState.Removed;
        item.IsLive = false;
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == item.PaymentBatchId, ct);
        await RecalcularAsync(db, lote, ct);
        switch (lote.Status)
        {
            case PaymentBatchState.PendingApproval:
                await motor.InvalidarAsync(ApprovalSourceTypes.TreasuryDocument, lote.PublicId, ApprovalSubjects.PaymentBatchApproval, ct);
                lote.Status = PaymentBatchState.Draft;
                lote.ApprovalRequestPublicId = null;
                break;
            case PaymentBatchState.FileGenerated:
                foreach (var archivo in await db.PaymentFiles.Where(a => a.PaymentBatchId == lote.Id && a.VoidedAt == null && a.RejectedByBankAt == null).ToListAsync(ct))
                {
                    archivo.VoidedAt = ahora;
                    archivo.VoidReason = motivo;
                }
                lote.Status = PaymentBatchState.Approved;
                break;
        }
        if (lote.OrderCount == 0 && lote.Status is PaymentBatchState.Draft or PaymentBatchState.Approved) lote.Status = PaymentBatchState.Voided;
    }
}

/// <summary>
/// Lo que hace el lote cuando su aprobación se completa o se devuelve (<see cref="IAprobableDeTesoreria"/>, sujeto <c>PaymentBatchApproval</c>): aprobado, sus órdenes
/// pasan a <see cref="PaymentOrderState.Approved"/> y el uso del fondo de liquidez que lo autoriza queda usado; devuelto, el lote vuelve a borrador con sus órdenes en
/// él. Aprobar no mueve dinero. (nuevo)
/// </summary>
public sealed class AprobableLoteDePago(IApplicationDbContext db, IDateTimeService reloj) : IAprobableDeTesoreria
{
    public string Sujeto => ApprovalSubjects.PaymentBatchApproval;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var lote = await db.PaymentBatches.FirstOrDefaultAsync(b => b.PublicId == solicitud.SourcePublicId, ct);
        if (lote is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        if (lote.Status != PaymentBatchState.PendingApproval) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.LoteNoEnAprobacion(lote.Status.ToString()));
        // T2 (fase 42; FR-208; pedido del dueño): las aprobaciones del lote hacen una combinación válida de los firmantes vigentes de la cuenta para su monto.
        var firmas = await FirmasDelLote.EvaluarAsync(db, lote, solicitud, reloj.HoyLocal, ct);
        if (!firmas.Cumple) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreriaT2Lotes.CombinacionDeFirmas(firmas.Motivo!));
        lote.Status = PaymentBatchState.Approved;
        lote.ApprovedAt = reloj.UtcNow;
        var ordenes = await (from i in db.PaymentBatchItems where i.PaymentBatchId == lote.Id && i.IsLive select i.PaymentOrderId).ToListAsync(ct);
        foreach (var o in await db.PaymentOrders.Where(o => ordenes.Contains(o.Id)).ToListAsync(ct)) o.Status = PaymentOrderState.Approved;
        if (lote.LiquidityFundUseId is { } uso && await db.LiquidityFundUses.FirstOrDefaultAsync(u => u.Id == uso, ct) is { } fila)
            GuardaDelFondoDeLiquidez.MarcarUsado(fila, lote.PublicId);
        return Result.Success(new EstadoDeFuenteDto(lote.PublicId, ApprovalSourceTypes.TreasuryDocument, lote.Status.ToString(), solicitud.SourceLabel));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        var lote = await db.PaymentBatches.FirstOrDefaultAsync(b => b.PublicId == solicitud.SourcePublicId, ct);
        if (lote is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        lote.Status = PaymentBatchState.Draft;
        lote.ApprovalRequestPublicId = null;
        lote.Note = motivo.Length <= 500 ? motivo : motivo[..500];
        return Result.Success(new EstadoDeFuenteDto(lote.PublicId, ApprovalSourceTypes.TreasuryDocument, lote.Status.ToString(), solicitud.SourceLabel));
    }
}
