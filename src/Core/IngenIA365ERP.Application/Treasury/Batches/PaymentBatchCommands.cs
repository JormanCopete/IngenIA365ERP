using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Batches;

// Feature 015, US10 (T0143; FR-110, FR-111, FR-116, FR-203, FR-206, FR-207, FR-213; contracts/api.md §10): armar el lote por cuenta de origen y forma con su
// vista previa (formato vigente del banco o PAGOS-GENERICO, total, cantidad y GMF estimado), enviarlo a aprobación y aprobarlo nivel por nivel por el motor (quien
// prepara no aprueba). Todo con CerrojoDeTesoreria en su orden: configuración → cuenta → lote → órdenes → numeración. Las listas vinculantes se consultan sobre
// todo beneficiario antes de aprobar. (nuevo)

/// <summary>Lo común de los comandos de lote: la cuenta de origen válida para pagar. (nuevo)</summary>
internal static class CuentaDeOrigen
{
    public static async Task<Result<OwnBankAccount>> ValidarAsync(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, OwnBankAccount cuenta,
        DateOnly fecha, CancellationToken ct)
    {
        if (!cuenta.Activa(fecha)) return Result.Failure<OwnBankAccount>(ErroresDeTesoreria.CuentaInactiva(cuenta.Code));
        if (!cuenta.EsDelFondoDeLiquidez && !cuenta.Purposes.HasFlag(BankAccountPurpose.Payments))
            return Result.Failure<OwnBankAccount>(ErroresDeTesoreria.SinPropositoDePagos(cuenta.Code));
        if (await contabilidad.VinculoAsync(cuenta.PublicId, ct) is null) return Result.Failure<OwnBankAccount>(ErroresDeTesoreria.CuentaSinVinculo());
        _ = db;
        return Result.Success(cuenta);
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches</c> (<c>Treasury.Batches.Prepare</c>): arma un lote en borrador desde una cuenta de origen de pagos vinculada, con la forma
/// (archivo, portal del banco o cheques) y las órdenes elegidas; las que no entran vuelven con su motivo. Desde una cuenta del fondo de liquidez hace falta el uso
/// aprobado (<c>Treasury.Batch.SourceIsLiquidityFund</c>). (nuevo)
/// </summary>
public sealed record CreatePaymentBatchCommand(
    Guid BankAccountPublicId,
    PaymentBatchForm Form,
    DateOnly PayDate,
    IReadOnlyList<Guid> OrderPublicIds,
    Guid? BankFileFormatPublicId = null,
    Guid? LiquidityFundUsePublicId = null,
    string? Note = null) : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePaymentBatchCommandValidator : AbstractValidator<CreatePaymentBatchCommand>
{
    public CreatePaymentBatchCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta de origen.");
        RuleFor(x => x.Form).IsInEnum();
        RuleFor(x => x.PayDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de pago.");
        RuleFor(x => x.OrderPublicIds).NotNull().NotEmpty().WithMessage("Elija al menos una orden.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CreatePaymentBatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    IContabilidadParaTesoreria contabilidad,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    GuardaDelFondoDeLiquidez fondo,
    ParametrosDeTesoreria parametros,
    IDateTimeService reloj) : IRequestHandler<CreatePaymentBatchCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(CreatePaymentBatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var hoy = reloj.HoyLocal;
        var valida = await CuentaDeOrigen.ValidarAsync(db, contabilidad, cuenta, hoy, ct);
        if (valida.IsFailure) return Result.Failure<PaymentBatchDto>(valida.Error);
        LiquidityFundUse? uso = null;
        if (cuenta.EsDelFondoDeLiquidez)
        {
            if (request.LiquidityFundUsePublicId is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.OrigenEsFondoDeLiquidez());
            var exigido = await fondo.ExigirAsync(cuenta, 0m, request.LiquidityFundUsePublicId, ct);
            if (exigido.IsFailure) return Result.Failure<PaymentBatchDto>(exigido.Error);
            uso = exigido.Value;
        }
        var formato = await LotesDePago.FormatoAsync(db, cuenta, request.Form, request.BankFileFormatPublicId, request.PayDate, ct);
        if (formato.IsFailure) return Result.Failure<PaymentBatchDto>(formato.Error);

        var pedidas = request.OrderPublicIds.Distinct().ToList();
        var ids = await db.PaymentOrders.AsNoTracking().Where(o => pedidas.Contains(o.PublicId)).Select(o => o.Id).ToListAsync(ct);
        if (ids.Count != pedidas.Count) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ids.Select(i => (long)i).Order().ToList(), ct: ct);
        var ordenes = await db.PaymentOrders.Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).ToListAsync(ct);

        var lote = new PaymentBatch
        {
            BankAccountId = cuenta.Id, Form = request.Form, BankFileFormatId = formato.Value?.Id, PreparedByUserId = usuario, PreparedAt = reloj.UtcNow,
            PayDate = request.PayDate, LiquidityFundUseId = uso?.Id, Note = request.Note?.Trim(),
        };
        db.PaymentBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        var pendientes = await LotesDePago.AgregarAsync(db, validador, await parametros.GmfAsync(request.PayDate, ct), lote, cuenta, ordenes, ct);
        await LotesDePago.RecalcularAsync(db, lote, ct);
        if (lote.OrderCount == 0) return Result.Failure<PaymentBatchDto>(new ErrorConDatos("Treasury.Batch.Empty",
            "Ninguna de las órdenes elegidas puede entrar al lote: revise sus motivos.", new { pending = pendientes }));
        await numerador.NumerarAsync(lote, LotesDePago.Codigo, LotesDePago.Codigo, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await LotesDePago.DtoAsync(db, lote, pendientes, ct));
    }
}

/// <summary>
/// <c>PUT /api/treasury/payment-batches/{id}/items</c> (<c>Treasury.Batches.Prepare</c>): agrega o saca órdenes de un lote en borrador; la que sale vuelve a la
/// bandeja (lista). (nuevo)
/// </summary>
public sealed record UpdatePaymentBatchItemsCommand(Guid BatchPublicId, IReadOnlyList<Guid> AddOrderPublicIds, IReadOnlyList<Guid> RemoveOrderPublicIds)
    : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdatePaymentBatchItemsCommandValidator : AbstractValidator<UpdatePaymentBatchItemsCommand>
{
    public UpdatePaymentBatchItemsCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.AddOrderPublicIds).NotNull();
        RuleFor(x => x.RemoveOrderPublicIds).NotNull();
    }
}

public sealed class UpdatePaymentBatchItemsCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    ICerrojoDeTesoreria cerrojo,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    IMotorDeAprobaciones motor,
    IDateTimeService reloj) : IRequestHandler<UpdatePaymentBatchItemsCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(UpdatePaymentBatchItemsCommand request, CancellationToken ct)
    {
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        var pedidas = request.AddOrderPublicIds.Concat(request.RemoveOrderPublicIds).Distinct().ToList();
        var ids = await db.PaymentOrders.AsNoTracking().Where(o => pedidas.Contains(o.PublicId)).Select(o => o.Id).ToListAsync(ct);
        if (ids.Count != pedidas.Count) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ids.Select(i => (long)i).Order().ToList(), ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Status != PaymentBatchState.Draft) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoEnBorrador(lote.Status.ToString()));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == lote.BankAccountId, ct);

        var aSacar = await db.PaymentOrders.Where(o => request.RemoveOrderPublicIds.Contains(o.PublicId)).ToListAsync(ct);
        foreach (var orden in aSacar)
        {
            var item = await db.PaymentBatchItems.FirstOrDefaultAsync(i => i.PaymentBatchId == lote.Id && i.PaymentOrderId == orden.Id && i.IsLive, ct);
            if (item is null) continue;
            await LotesDePago.SacarAsync(db, motor, item, "Sacada del lote al prepararlo", reloj.UtcNow, ct);
            orden.Status = PaymentOrderState.Ready;
        }
        var aAgregar = await db.PaymentOrders.Where(o => request.AddOrderPublicIds.Contains(o.PublicId)).OrderBy(o => o.Id).ToListAsync(ct);
        var pendientes = aAgregar.Count == 0
            ? []
            : await LotesDePago.AgregarAsync(db, validador, await parametros.GmfAsync(lote.PayDate, ct), lote, cuenta, aAgregar, ct);
        await LotesDePago.RecalcularAsync(db, lote, ct);
        if (lote.Status == PaymentBatchState.Voided) lote.Status = lote.OrderCount > 0 ? PaymentBatchState.Draft : PaymentBatchState.Voided;
        await db.SaveChangesAsync(ct);
        return Result.Success(await LotesDePago.DtoAsync(db, lote, pendientes, ct));
    }
}

/// <summary>Una línea de la vista previa del lote. (nuevo)</summary>
public sealed record PaymentBatchPreviewLineDto(int LineNumber, string OrderNumber, string BeneficiaryTaxId, string BeneficiaryName, string? BankCode,
    string? AccountNumberMasked, decimal Amount, string? Text);

/// <summary>La vista previa del lote: el formato que se usará, las líneas, el total, la cantidad y el GMF estimado (FR-110). (nuevo)</summary>
public sealed record PaymentBatchPreviewDto(Guid BatchPublicId, string Number, PaymentBatchForm Form, string? FormatCode, string? FileName, int LineCount,
    decimal Total, decimal GmfEstimate, IReadOnlyList<PaymentBatchPreviewLineDto> Lines, IReadOnlyList<string> Warnings);

/// <summary>
/// <c>GET /api/treasury/payment-batches/{id}/preview</c> (<c>Treasury.Batches.Prepare</c>): las líneas como saldrían con el formato sellado del lote (o, en el portal
/// del banco y en cheques, la relación a digitar), con el total, la cantidad y el GMF estimado. No escribe nada. (nuevo)
/// </summary>
public sealed record PreviewPaymentBatchQuery(Guid BatchPublicId) : IRequest<Result<PaymentBatchPreviewDto>>;

public sealed class PreviewPaymentBatchQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<PreviewPaymentBatchQuery, Result<PaymentBatchPreviewDto>>
{
    public async Task<Result<PaymentBatchPreviewDto>> Handle(PreviewPaymentBatchQuery request, CancellationToken ct)
    {
        var lote = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (lote is null) return Result.Failure<PaymentBatchPreviewDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        var lineas = await LineasDelArchivo.CargarAsync(db, lote, ct);
        var avisos = new List<string>();
        string? texto = null, archivo = null, codigo = null;
        IReadOnlyList<string> detalle = [];
        if (lote.Form == PaymentBatchForm.File && lote.BankFileFormatId is { } f)
        {
            var formato = await db.BankFileFormats.AsNoTracking().Include(x => x.Bank).Include(x => x.Fields).FirstAsync(x => x.Id == f, ct);
            codigo = formato.Code;
            if (formato.Notes?.Contains("Pendiente de validar", StringComparison.OrdinalIgnoreCase) == true)
                avisos.Add($"El formato {formato.Code} está pendiente de validar con el banco.");
            var contexto = await LineasDelArchivo.ContextoAsync(db, lote, formato, reloj.UtcNow, ct);
            try
            {
                var salida = FlatFileWriter.Escribir(formato, contexto, lineas.Select(l => l.Valores).ToList());
                detalle = salida.DetailTexts;
                archivo = salida.FileName;
                texto = salida.Text;
            }
            catch (BankFileWriteException ex)
            {
                avisos.Add($"La línea {ex.LineNumber} no cabe en el campo «{ex.Field}» del formato: {ex.Message}");
            }
        }
        _ = texto;
        return Result.Success(new PaymentBatchPreviewDto(lote.PublicId, lote.Number, lote.Form, codigo, archivo, lote.OrderCount, lote.Total, lote.GmfEstimate,
            lineas.Select((l, i) => new PaymentBatchPreviewLineDto(i + 1, l.Orden.Number, l.Orden.BeneficiaryTaxId, l.Orden.BeneficiaryName, l.Orden.DestinationBankCode,
                l.Orden.DestinationAccountNumber is null ? null : CuentasBancariasPropias.Enmascarar(l.Orden.DestinationAccountNumber), l.Item.Amount,
                i < detalle.Count ? detalle[i] : null)).ToList(), avisos));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/submit</c> (<c>Treasury.Batches.Prepare</c>): envía el lote a aprobación por monto (sujeto
/// <c>PaymentBatchApproval</c>, permiso con monto <c>Treasury.Batches.Approve</c>; sin política, un nivel). Desde una cuenta del fondo exige el uso aprobado por el
/// total (<c>Treasury.LiquidityFund.UseRequired</c>, FR-207). (nuevo)
/// </summary>
public sealed record SubmitPaymentBatchCommand(Guid BatchPublicId) : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class SubmitPaymentBatchCommandValidator : AbstractValidator<SubmitPaymentBatchCommand>
{
    public SubmitPaymentBatchCommandValidator() => RuleFor(x => x.BatchPublicId).NotEmpty();
}

public sealed class SubmitPaymentBatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    ICerrojoDeTesoreria cerrojo,
    AprobacionDiferidaEnTesoreria aprobacion,
    GuardaDelFondoDeLiquidez fondo,
    IDateTimeService reloj) : IRequestHandler<SubmitPaymentBatchCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(SubmitPaymentBatchCommand request, CancellationToken ct)
    {
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Status != PaymentBatchState.Draft) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoEnBorrador(lote.Status.ToString()));
        if (lote.OrderCount == 0) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteVacio());
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == lote.BankAccountId, ct);
        if (cuenta.EsDelFondoDeLiquidez)
        {
            var uso = lote.LiquidityFundUseId is { } u
                ? await db.LiquidityFundUses.AsNoTracking().Where(x => x.Id == u).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
                : null;
            var exigido = await fondo.ExigirAsync(cuenta, lote.Total, uso ?? Guid.Empty, ct);
            if (exigido.IsFailure) return Result.Failure<PaymentBatchDto>(exigido.Error);
        }
        await LotesDePago.RecalcularAsync(db, lote, ct);
        var pedida = await aprobacion.PedirAsync(new PedidoDeAprobacionDiferida(ApprovalSubjects.PaymentBatchApproval, lote.PublicId,
            string.Create(CultureInfo.InvariantCulture, $"Lote de pago {lote.Number} desde {cuenta.Code}"), lote.Total, reloj.HoyLocal, lote.InputsHash,
            LotesDePago.PermisoDeAprobar, [lote.PreparedByUserId]), ct);
        if (pedida.IsFailure) return Result.Failure<PaymentBatchDto>(pedida.Error);
        lote.SubmittedAt = reloj.UtcNow;
        if (pedida.Value is { } solicitud)
        {
            lote.ApprovalRequestPublicId = solicitud.PublicId;
            lote.Status = PaymentBatchState.PendingApproval;
        }
        else
        {
            lote.Status = PaymentBatchState.Approved;
            lote.ApprovedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await LotesDePago.DtoAsync(db, lote, null, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/approve</c> (<c>Treasury.Batches.Approve</c>, con monto): decide el nivel actual por el motor de aprobaciones —quien
/// preparó no aprueba (<c>Treasury.Batch.SelfApproval</c>)—, después de consultar las listas vinculantes sobre todo beneficiario
/// (<c>Treasury.Compliance.BindingListMatch</c>, FR-203). El último nivel deja el lote aprobado. Aprobar no mueve dinero. (nuevo)
/// </summary>
public sealed record ApprovePaymentBatchCommand(Guid BatchPublicId, string? Reason = null) : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ApprovePaymentBatchCommandValidator : AbstractValidator<ApprovePaymentBatchCommand>
{
    public ApprovePaymentBatchCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ApprovePaymentBatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    ICerrojoDeTesoreria cerrojo,
    IMotorDeAprobaciones motor,
    GuardasDeAprobacionDelLote guardas) : IRequestHandler<ApprovePaymentBatchCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(ApprovePaymentBatchCommand request, CancellationToken ct)
    {
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var ordenes = await db.PaymentBatchItems.AsNoTracking().Where(i => i.PaymentBatchId == visto.Id && i.IsLive).Select(i => (long)i.PaymentOrderId).ToListAsync(ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ordenes.Order().ToList(), ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Status != PaymentBatchState.PendingApproval || lote.ApprovalRequestPublicId is not { } solicitud)
            return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoEnAprobacion(lote.Status.ToString()));
        var previas = await guardas.AntesDeAprobarAsync(lote, ct);
        if (previas.IsFailure) return Result.Failure<PaymentBatchDto>(previas.Error);
        var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, ApprovalDecisionKind.Approve, request.Reason, lote.InputsHash), ct);
        // Corrida e2e del 2026-10-26: el motor de aprobaciones (012) rechaza la autoaprobación por usuario antes que la guarda de la persona; el contrato de
        // Tesorería dice Treasury.Batch.SelfApproval.
        if (decidida.IsFailure && decidida.Error.Code == ErroresDeAprobaciones.CodigoAutoaprobacion)
            return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteAutoaprobado());
        if (decidida.IsFailure) return Result.Failure<PaymentBatchDto>(decidida.Error);
        var actual = await db.PaymentBatches.AsNoTracking().FirstAsync(b => b.Id == lote.Id, ct);
        return Result.Success(await LotesDePago.DtoAsync(db, actual, null, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/reject</c> (<c>Treasury.Batches.Approve</c>): devuelve el lote con motivo; vuelve a borrador con sus órdenes y se
/// puede corregir y enviar otra vez. (nuevo)
/// </summary>
public sealed record RejectPaymentBatchCommand(Guid BatchPublicId, string Reason) : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectPaymentBatchCommandValidator : AbstractValidator<RejectPaymentBatchCommand>
{
    public RejectPaymentBatchCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del rechazo.").MaximumLength(500);
    }
}

public sealed class RejectPaymentBatchCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, ICerrojoDeTesoreria cerrojo, IMotorDeAprobaciones motor)
    : IRequestHandler<RejectPaymentBatchCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(RejectPaymentBatchCommand request, CancellationToken ct)
    {
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Status != PaymentBatchState.PendingApproval || lote.ApprovalRequestPublicId is not { } solicitud)
            return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoEnAprobacion(lote.Status.ToString()));
        var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, ApprovalDecisionKind.Reject, request.Reason.Trim(), lote.InputsHash), ct);
        if (decidida.IsFailure) return Result.Failure<PaymentBatchDto>(decidida.Error);
        var actual = await db.PaymentBatches.AsNoTracking().FirstAsync(b => b.Id == lote.Id, ct);
        return Result.Success(await LotesDePago.DtoAsync(db, actual, null, ct));
    }
}

/// <summary><c>GET /api/treasury/payment-batches?state=&amp;bankAccount=</c> (<c>Treasury.Batches.Prepare</c> o <c>.Approve</c>): los lotes al alcance. (nuevo)</summary>
public sealed record ListPaymentBatchesQuery(PaymentBatchState? State = null, Guid? BankAccountPublicId = null) : IRequest<Result<IReadOnlyList<PaymentBatchDto>>>;

public sealed class ListPaymentBatchesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListPaymentBatchesQuery, Result<IReadOnlyList<PaymentBatchDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentBatchDto>>> Handle(ListPaymentBatchesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.PaymentBatches.AsNoTracking();
        if (request.State is { } e) consulta = consulta.Where(b => b.Status == e);
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(b => b.BankAccountId == (id ?? -1));
        }
        var lotes = (await consulta.OrderByDescending(b => b.Id).Take(200).ToListAsync(ct)).Where(b => mio.IncluyeCuenta(b.BankAccountId)).ToList();
        var lista = new List<PaymentBatchDto>(lotes.Count);
        foreach (var l in lotes) lista.Add(await LotesDePago.DtoAsync(db, l, null, ct));
        return Result.Success<IReadOnlyList<PaymentBatchDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/payment-batches/{id}</c>. (nuevo)</summary>
public sealed record GetPaymentBatchQuery(Guid BatchPublicId) : IRequest<Result<PaymentBatchDto>>;

public sealed class GetPaymentBatchQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<GetPaymentBatchQuery, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(GetPaymentBatchQuery request, CancellationToken ct) =>
        await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct) is { } l
            ? Result.Success(await LotesDePago.DtoAsync(db, l, null, ct))
            : Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
}

/// <summary>
/// Lo que se verifica antes de aprobar un lote, en un solo sitio (feature 015, US10 y US26, T0143, T0166, T0167): las listas vinculantes sobre todo beneficiario
/// (FR-203). La revisión expresa de las cuentas cambiadas (FR-210) la suma US26. (nuevo)
/// </summary>
public sealed class GuardasDeAprobacionDelLote(IApplicationDbContext db, IListasDeTesoreria listas, IAlertas alertas)
{
    public async Task<Result> AntesDeAprobarAsync(PaymentBatch lote, CancellationToken ct)
    {
        var items = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join o in db.PaymentOrders.AsNoTracking() on i.PaymentOrderId equals o.Id
                           where i.PaymentBatchId == lote.Id && i.IsLive
                           select new { o.PublicId, o.Number, o.BeneficiaryPersonId, o.BeneficiaryName, i.Flags, i.ExplicitlyReviewedByUserId }).ToListAsync(ct);
        // US26 (T0166; FR-210): una orden a una cuenta destino cambiada hace pocos días no se aprueba sin su revisión expresa por otra persona.
        var sinRevisar = items.Where(i => i.Flags.HasFlag(PaymentBatchItemFlags.RecentAccountChange) && i.ExplicitlyReviewedByUserId is null).Select(i => i.Number).ToList();
        if (sinRevisar.Count > 0) return Result.Failure(ErroresDeTesoreria.RevisionRequerida(sinRevisar));
        // US26 (T0167; FR-203): las listas vinculantes sobre todo beneficiario antes de aprobar; una coincidencia alerta al oficial de inmediato.
        var resultado = await listas.EvaluarAsync(items.Select(i => i.BeneficiaryPersonId).Distinct().ToList(), ct);
        if (!resultado.HayVinculante) return Result.Success();
        var bloqueadas = items.Where(i => resultado.Vinculantes.Contains(i.BeneficiaryPersonId)).ToList();
        foreach (var b in bloqueadas)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaListaVinculante, "Beneficiario en lista vinculante",
                $"El beneficiario de la orden {b.Number} ({b.BeneficiaryName}) del lote {lote.Number} coincidió en una lista vinculante al aprobar: no se aprueba ni se paga.",
                "PaymentOrder", b.PublicId, DedupKey: $"Tesoreria.ListaVinculante:{b.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        return Result.Failure(ErroresDeTesoreria.ListaVinculanteEnLote(bloqueadas.Select(i => i.Number).ToList()));
    }
}
