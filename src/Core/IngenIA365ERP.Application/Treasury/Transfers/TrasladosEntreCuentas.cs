using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Transfers;

// Feature 015, T1 (fase 33, T0411; FR-104, FR-207, FR-012; contracts/api.md §13): los traslados entre cuentas propias. Se piden con aprobación diferida por monto (sujeto
// TreasuryTransfer), una persona los marca enviados al ejecutarlos en el portal del banco —salen de la de origen y quedan en tránsito— y llegan a la de destino por el cruce
// del extracto o con el soporte del banco. Desde una cuenta del fondo de liquidez exigen un uso aprobado. Quien pide no aprueba.

/// <summary>Un traslado entre cuentas propias, para la pantalla. (nuevo)</summary>
public sealed record TransferDto(
    Guid PublicId,
    string Number,
    Guid FromBankAccountPublicId,
    string FromBankAccountCode,
    Guid ToBankAccountPublicId,
    string ToBankAccountCode,
    decimal Amount,
    string Reason,
    TransferState Status,
    DateOnly RequestedOn,
    Guid? ApprovalRequestPublicId,
    DateOnly? SentOn,
    string? BankReference,
    DateOnly? ConfirmedOn,
    Guid? ConfirmedByStatementLinePublicId,
    string? DecisionReason,
    int DaysInTransit);

/// <summary>Lo común de los traslados: el número, la huella que el aprobador confirma, la visibilidad, el DTO y la confirmación por una línea del extracto. (nuevo)</summary>
public static class TrasladosDeTesoreria
{
    /// <summary>La clase de numeración de los traslados (<c>TES:TL</c>).</summary>
    public const string Codigo = "TL";

    public const string PermisoDeAprobar = "Treasury.Transfers.Approve";

    public static string Huella(Transfer t) => HuellaDeOperacion.Calcular("Tesoreria.Traslado",
        new { t.PublicId, t.FromBankAccountId, t.ToBankAccountId, t.Amount, t.Reason, t.SupportAttachmentPublicId });

    /// <summary>El traslado si alguna de sus dos cuentas está al alcance; si no, nulo (la ruta responde <c>Generic.NotFound</c>).</summary>
    public static async Task<Transfer?> VisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.TreasuryTransfers : db.TreasuryTransfers.AsNoTracking();
        var t = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (t is null) return null;
        var mio = await alcance.ObtenerAsync(ct);
        return mio.IncluyeCuenta(t.FromBankAccountId) || mio.IncluyeCuenta(t.ToBankAccountId) ? t : null;
    }

    public static async Task<TransferDto> DtoAsync(IApplicationDbContext db, Transfer t, DateOnly hoy, CancellationToken ct)
    {
        var cuentas = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == t.FromBankAccountId || c.Id == t.ToBankAccountId)
            .Select(c => new { c.Id, c.PublicId, c.Code }).ToListAsync(ct);
        var de = cuentas.First(c => c.Id == t.FromBankAccountId);
        var a = cuentas.First(c => c.Id == t.ToBankAccountId);
        return new TransferDto(t.PublicId, t.Number, de.PublicId, de.Code, a.PublicId, a.Code, t.Amount, t.Reason, t.Status, t.RequestedOn, t.ApprovalRequestPublicId,
            t.SentOn, t.BankReference, t.ConfirmedOn, t.ConfirmedByStatementLinePublicId, t.DecisionReason,
            t.Status == TransferState.Sent && t.SentOn is { } salio ? hoy.DayNumber - salio.DayNumber : 0);
    }

    /// <summary>Si la línea del extracto confirma el traslado: un abono de la cuenta de destino por el mismo valor, no antes del envío.</summary>
    public static bool LineaLoConfirma(Transfer t, BankStatementLine linea) =>
        linea.BankAccountId == t.ToBankAccountId && linea.Direction == StatementLineDirection.Credit && linea.Amount == t.Amount
        && (t.SentOn is null || linea.MovementDate >= t.SentOn.Value);

    /// <summary>
    /// Confirma un traslado enviado (ya bloqueado por quien llama) con la línea del extracto o con el soporte, y emite <c>TrasladoConfirmado</c>. La usan la confirmación por
    /// pantalla y el cruce de la conciliación de la cuenta de destino. Sin guardar.
    /// </summary>
    public static async Task ConfirmarAsync(IApplicationDbContext db, EmisionDeTesoreria emision, Transfer t, int usuario, DateTime ahora, DateOnly fecha, Guid? linea,
        Guid? soporte, CancellationToken ct)
    {
        t.Status = TransferState.Confirmed;
        t.ConfirmedByUserId = usuario;
        t.ConfirmedAt = ahora;
        t.ConfirmedOn = fecha;
        t.ConfirmedByStatementLinePublicId = linea;
        t.ConfirmationSupportAttachmentPublicId = soporte;
        var origen = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == t.FromBankAccountId, ct);
        var destino = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == t.ToBankAccountId, ct);
        await emision.TrasladoConfirmadoAsync(origen, destino, t, ct);
    }

    /// <summary>Las dos cuentas de un traslado, en el orden de su <c>Id</c> (el del cerrojo).</summary>
    public static IReadOnlyCollection<long> CuentasEnOrden(Transfer t) => new long[] { t.FromBankAccountId, t.ToBankAccountId }.Distinct().Order().ToList();
}

/// <summary>Lo que hace el traslado cuando su aprobación se completa o se devuelve (<see cref="IAprobableDeTesoreria"/>). Aprobar no mueve dinero. (nuevo)</summary>
public sealed class AprobableTraslado(IApplicationDbContext db, IDateTimeService reloj) : IAprobableDeTesoreria
{
    public string Sujeto => ApprovalSubjects.TreasuryTransfer;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        await DecidirAsync(solicitud, TransferState.Approved, null, ct);

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        await DecidirAsync(solicitud, TransferState.Rejected, motivo, ct);

    private async Task<Result<EstadoDeFuenteDto>> DecidirAsync(ApprovalRequest solicitud, TransferState estado, string? motivo, CancellationToken ct)
    {
        var t = await db.TreasuryTransfers.FirstOrDefaultAsync(x => x.PublicId == solicitud.SourcePublicId, ct);
        if (t is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.NoEncontrado("El traslado"));
        if (t.Status != TransferState.PendingApproval)
            return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.TrasladoEstado(t.Number, t.Status.ToString(), "pendiente de aprobación"));
        t.Status = estado;
        t.DecidedAt = reloj.UtcNow;
        t.DecisionReason = motivo;
        return Result.Success(new EstadoDeFuenteDto(t.PublicId, ApprovalSourceTypes.TreasuryDocument, estado.ToString(), solicitud.SourceLabel));
    }
}

// ---------------------------------------------------------------------------------------------------------------- pedir --

/// <summary>
/// <c>POST /api/treasury/transfers</c> (<c>Treasury.Transfers.Request</c>, con clave): pide un traslado entre dos cuentas propias activas y distintas, con su motivo, y pide la
/// aprobación diferida por monto (sujeto <c>TreasuryTransfer</c>). Si la cuenta de origen es del fondo de liquidez, avisa desde ya si no hay un uso aprobado que alcance
/// (<c>Treasury.LiquidityFund.UseRequired</c>); el uso se consume al enviarlo. Antes de todo, la validación previa contable de los dos hechos. (nuevo)
/// </summary>
public sealed record RequestTransferCommand(Guid FromBankAccountPublicId, Guid ToBankAccountPublicId, decimal Amount, string Reason, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestTransferCommandValidator : AbstractValidator<RequestTransferCommand>
{
    public RequestTransferCommandValidator()
    {
        RuleFor(x => x.FromBankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta de origen.");
        RuleFor(x => x.ToBankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta de destino.");
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("El valor del traslado tiene que ser positivo.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del traslado.").MaximumLength(500);
    }
}

public sealed class RequestTransferCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    AprobacionDiferidaEnTesoreria aprobacion,
    GuardaDelFondoDeLiquidez fondo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RequestTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(RequestTransferCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (request.FromBankAccountPublicId == request.ToBankAccountPublicId) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoMismaCuenta());
        var origen = await BankAccounts.CuentasPropias.VisibleAsync(db, alcance, request.FromBankAccountPublicId, seguir: false, ct);
        var destino = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.ToBankAccountPublicId, ct);
        if (origen is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("La cuenta de origen"));
        if (destino is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("La cuenta de destino"));
        var hoy = reloj.HoyLocal;
        if (!origen.Activa(hoy)) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoCuentaInactiva(origen.Code));
        if (!destino.Activa(hoy)) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoCuentaInactiva(destino.Code));

        // Validación previa contable (FR-221): los dos hechos del traslado, cada uno con su cuenta.
        var previa = await emision.ValidacionPreviaAsync([TrasladoEntreCuentasEnviadoV1.Type], origen, hoy, ct);
        if (previa.IsFailure) return Result.Failure<TransferDto>(previa.Error);
        previa = await emision.ValidacionPreviaAsync([TrasladoConfirmadoV1.Type], destino, hoy, ct);
        if (previa.IsFailure) return Result.Failure<TransferDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, new long[] { origen.Id, destino.Id }.Order().ToList(), exclusivo: false, ct);
        var uso = await fondo.ExigirAsync(origen, request.Amount, null, ct);
        if (uso.IsFailure) return Result.Failure<TransferDto>(uso.Error);

        var traslado = new Transfer
        {
            FromBankAccountId = origen.Id, ToBankAccountId = destino.Id, Amount = request.Amount, Reason = request.Reason.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId, RequestedByUserId = usuario, RequestedAt = reloj.UtcNow, RequestedOn = hoy,
        };
        var pedida = await aprobacion.PedirAsync(new PedidoDeAprobacionDiferida(ApprovalSubjects.TreasuryTransfer, traslado.PublicId,
            string.Create(CultureInfo.InvariantCulture, $"Traslado de {origen.Code} a {destino.Code}"), request.Amount, hoy, TrasladosDeTesoreria.Huella(traslado),
            TrasladosDeTesoreria.PermisoDeAprobar, [usuario]), ct);
        if (pedida.IsFailure) return Result.Failure<TransferDto>(pedida.Error);
        traslado.ApprovalRequestPublicId = pedida.Value?.PublicId;
        if (pedida.Value is null) traslado.Status = TransferState.Approved;
        traslado.Number = await numerador.AsignarAsync(TrasladosDeTesoreria.Codigo, TrasladosDeTesoreria.Codigo, ct);
        db.TreasuryTransfers.Add(traslado);
        await auditoria.AgregarAsync("Treasury.Transfer.Requested", "Transfer", traslado.PublicId, null,
            new { number = traslado.Number, from = origen.Code, to = destino.Code, amount = traslado.Amount, reason = traslado.Reason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, traslado, hoy, ct));
    }
}

// ------------------------------------------------------------------------------------------------------- aprobar y devolver --

/// <summary>
/// <c>POST /api/treasury/transfers/{id}/approve</c> (<c>Treasury.Transfers.Approve</c>, con monto): decide el nivel actual por el motor de aprobaciones; quien pidió no aprueba
/// (<c>Treasury.Transfer.SelfApproval</c>). Aprobar no mueve dinero. (nuevo)
/// </summary>
public sealed record ApproveTransferCommand(Guid TransferPublicId, string? Reason = null) : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveTransferCommandValidator : AbstractValidator<ApproveTransferCommand>
{
    public ApproveTransferCommandValidator()
    {
        RuleFor(x => x.TransferPublicId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ApproveTransferCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor, IDateTimeService reloj)
    : IRequestHandler<ApproveTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(ApproveTransferCommand request, CancellationToken ct) =>
        await DecisionDelTraslado.DecidirAsync(db, alcance, motor, reloj, request.TransferPublicId, ApprovalDecisionKind.Approve, request.Reason, ct);
}

/// <summary><c>POST /api/treasury/transfers/{id}/reject</c> (<c>Treasury.Transfers.Approve</c>): devuelve el traslado con su motivo. (nuevo)</summary>
public sealed record RejectTransferCommand(Guid TransferPublicId, string Reason) : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectTransferCommandValidator : AbstractValidator<RejectTransferCommand>
{
    public RejectTransferCommandValidator()
    {
        RuleFor(x => x.TransferPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del rechazo.").MaximumLength(500);
    }
}

public sealed class RejectTransferCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor, IDateTimeService reloj)
    : IRequestHandler<RejectTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(RejectTransferCommand request, CancellationToken ct) =>
        await DecisionDelTraslado.DecidirAsync(db, alcance, motor, reloj, request.TransferPublicId, ApprovalDecisionKind.Reject, request.Reason, ct);
}

internal static class DecisionDelTraslado
{
    public static async Task<Result<TransferDto>> DecidirAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor, IDateTimeService reloj,
        Guid traslado, ApprovalDecisionKind decision, string? motivo, CancellationToken ct)
    {
        var fila = await TrasladosDeTesoreria.VisibleAsync(db, alcance, traslado, seguir: false, ct);
        if (fila is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El traslado"));
        if (fila.Status != TransferState.PendingApproval || fila.ApprovalRequestPublicId is not { } solicitud)
            return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(fila.Number, fila.Status.ToString(), "pendiente de aprobación"));
        var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, decision, motivo, TrasladosDeTesoreria.Huella(fila)), ct);
        if (decidida.IsFailure) return Result.Failure<TransferDto>(decidida.Error);
        var actual = await db.TreasuryTransfers.AsNoTracking().FirstAsync(t => t.Id == fila.Id, ct);
        return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, actual, reloj.HoyLocal, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------------- enviar --

/// <summary>
/// <c>POST /api/treasury/transfers/{id}/sent</c> (<c>Treasury.Transfers.Request</c>, con clave; nuevo): una persona marca enviado el traslado aprobado al ejecutarlo en el portal
/// del banco, con la fecha y la referencia; desde una cuenta del fondo de liquidez consume el uso aprobado. Sale de la cuenta de origen y queda en tránsito con
/// <c>TrasladoEntreCuentasEnviado</c>. (nuevo)
/// </summary>
public sealed record MarkTransferSentCommand(Guid TransferPublicId, DateOnly? SentOn = null, string? BankReference = null, Guid? LiquidityFundUsePublicId = null)
    : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkTransferSentCommandValidator : AbstractValidator<MarkTransferSentCommand>
{
    public MarkTransferSentCommandValidator()
    {
        RuleFor(x => x.TransferPublicId).NotEmpty();
        RuleFor(x => x.BankReference).MaximumLength(60);
    }
}

public sealed class MarkTransferSentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    GuardaDelFondoDeLiquidez fondo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<MarkTransferSentCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(MarkTransferSentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leido = await TrasladosDeTesoreria.VisibleAsync(db, alcance, request.TransferPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El traslado"));
        var hoy = reloj.HoyLocal;
        if (leido.Status == TransferState.Sent) return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, leido, hoy, ct));
        if (leido.Status != TransferState.Approved)
            return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(leido.Number, leido.Status.ToString(), "aprobado"));
        var origen = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == leido.FromBankAccountId, ct);
        var destino = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == leido.ToBankAccountId, ct);
        var fecha = request.SentOn ?? hoy;
        var previa = await emision.ValidacionPreviaAsync([TrasladoEntreCuentasEnviadoV1.Type], origen, fecha, ct);
        if (previa.IsFailure) return Result.Failure<TransferDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, TrasladosDeTesoreria.CuentasEnOrden(leido), ct: ct);
        var traslado = await db.TreasuryTransfers.FirstAsync(t => t.Id == leido.Id, ct);
        if (traslado.Status != TransferState.Approved)
            return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(traslado.Number, traslado.Status.ToString(), "aprobado"));
        var uso = await fondo.ExigirAsync(origen, traslado.Amount, request.LiquidityFundUsePublicId, ct);
        if (uso.IsFailure) return Result.Failure<TransferDto>(uso.Error);
        if (uso.Value is { } u)
        {
            GuardaDelFondoDeLiquidez.MarcarUsado(u, traslado.PublicId);
            traslado.LiquidityFundUseId = u.Id;
        }
        traslado.Status = TransferState.Sent;
        traslado.SentByUserId = usuario;
        traslado.SentAt = reloj.UtcNow;
        traslado.SentOn = fecha;
        traslado.BankReference = string.IsNullOrWhiteSpace(request.BankReference) ? null : request.BankReference.Trim();
        await emision.TrasladoEnviadoAsync(origen, destino, traslado, uso.Value?.PublicId, ct);
        await auditoria.AgregarAsync("Treasury.Transfer.Sent", "Transfer", traslado.PublicId, null,
            new { number = traslado.Number, on = fecha, reference = traslado.BankReference, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, traslado, hoy, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------- confirmar --

/// <summary>
/// <c>POST /api/treasury/transfers/{id}/confirm</c> (<c>Treasury.Transfers.Request</c>, con clave; nuevo): confirma un traslado enviado con la constancia del banco o con la línea
/// del extracto de la cuenta de destino que lo abona por el mismo valor, y emite <c>TrasladoConfirmado</c>. El cruce de la conciliación de la cuenta de destino lo confirma
/// solo. (nuevo)
/// </summary>
public sealed record ConfirmTransferCommand(Guid TransferPublicId, Guid? SupportAttachmentPublicId = null, Guid? StatementLinePublicId = null, DateOnly? ConfirmedOn = null)
    : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmTransferCommandValidator : AbstractValidator<ConfirmTransferCommand>
{
    public ConfirmTransferCommandValidator() => RuleFor(x => x.TransferPublicId).NotEmpty();
}

public sealed class ConfirmTransferCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(ConfirmTransferCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leido = await TrasladosDeTesoreria.VisibleAsync(db, alcance, request.TransferPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El traslado"));
        var hoy = reloj.HoyLocal;
        if (leido.Status == TransferState.Confirmed) return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, leido, hoy, ct));
        if (leido.Status != TransferState.Sent) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(leido.Number, leido.Status.ToString(), "enviado"));
        if (request.SupportAttachmentPublicId is null && request.StatementLinePublicId is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoSinSoporte());
        var fecha = request.ConfirmedOn ?? hoy;
        if (request.StatementLinePublicId is { } lineaId)
        {
            var linea = await db.TreasuryBankStatementLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == lineaId, ct);
            if (linea is null || !TrasladosDeTesoreria.LineaLoConfirma(leido, linea)) return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoLineaInvalida());
            fecha = request.ConfirmedOn ?? linea.MovementDate;
        }
        var destino = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == leido.ToBankAccountId, ct);
        var previa = await emision.ValidacionPreviaAsync([TrasladoConfirmadoV1.Type], destino, fecha, ct);
        if (previa.IsFailure) return Result.Failure<TransferDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, TrasladosDeTesoreria.CuentasEnOrden(leido), ct: ct);
        var traslado = await db.TreasuryTransfers.FirstAsync(t => t.Id == leido.Id, ct);
        if (traslado.Status != TransferState.Sent)
            return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(traslado.Number, traslado.Status.ToString(), "enviado"));
        await TrasladosDeTesoreria.ConfirmarAsync(db, emision, traslado, usuario, reloj.UtcNow, fecha, request.StatementLinePublicId, request.SupportAttachmentPublicId, ct);
        await auditoria.AgregarAsync("Treasury.Transfer.Confirmed", "Transfer", traslado.PublicId, null,
            new { number = traslado.Number, on = fecha, line = request.StatementLinePublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, traslado, hoy, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------------- anular --

/// <summary>
/// <c>POST /api/treasury/transfers/{id}/void</c> (<c>Treasury.Transfers.Request</c>, con clave; nuevo): anula con motivo un traslado que todavía no se envió (pendiente o
/// aprobado). Uno enviado no se anula: si el banco lo devolvió, entra por el extracto y se registra otro traslado de vuelta. (nuevo)
/// </summary>
public sealed record VoidTransferCommand(Guid TransferPublicId, string Reason) : IRequest<Result<TransferDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidTransferCommandValidator : AbstractValidator<VoidTransferCommand>
{
    public VoidTransferCommandValidator()
    {
        RuleFor(x => x.TransferPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo de la anulación.").MaximumLength(500);
    }
}

public sealed class VoidTransferCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IActorActual actorActual, AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<VoidTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(VoidTransferCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var traslado = await TrasladosDeTesoreria.VisibleAsync(db, alcance, request.TransferPublicId, seguir: true, ct);
        if (traslado is null) return Result.Failure<TransferDto>(ErroresDeTesoreria.NoEncontrado("El traslado"));
        if (traslado.Status is not (TransferState.PendingApproval or TransferState.Approved))
            return Result.Failure<TransferDto>(ErroresDeTesoreria.TrasladoEstado(traslado.Number, traslado.Status.ToString(), "pendiente o aprobado sin enviar"));
        traslado.Status = TransferState.Voided;
        traslado.VoidReason = request.Reason.Trim();
        await auditoria.AgregarAsync("Treasury.Transfer.Voided", "Transfer", traslado.PublicId, null, new { number = traslado.Number, reason = traslado.VoidReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await TrasladosDeTesoreria.DtoAsync(db, traslado, reloj.HoyLocal, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------------ listar --

/// <summary><c>GET /api/treasury/transfers?state=</c> (<c>Treasury.Transfers.Request</c> o <c>.Approve</c>): los traslados de las cuentas al alcance, con los días en tránsito. (nuevo)</summary>
public sealed record ListTransfersQuery(TransferState? State = null) : IRequest<Result<IReadOnlyList<TransferDto>>>;

public sealed class ListTransfersQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<ListTransfersQuery, Result<IReadOnlyList<TransferDto>>>
{
    public async Task<Result<IReadOnlyList<TransferDto>>> Handle(ListTransfersQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TreasuryTransfers.AsNoTracking();
        if (request.State is { } s) consulta = consulta.Where(t => t.Status == s);
        var filas = (await consulta.OrderByDescending(t => t.Id).Take(300).ToListAsync(ct))
            .Where(t => mio.IncluyeCuenta(t.FromBankAccountId) || mio.IncluyeCuenta(t.ToBankAccountId)).ToList();
        var lista = new List<TransferDto>(filas.Count);
        foreach (var t in filas) lista.Add(await TrasladosDeTesoreria.DtoAsync(db, t, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<TransferDto>>(lista);
    }
}
