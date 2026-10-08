using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// El envío manual de un mensaje al deudor (feature 013, T0308; T74, T75; contracts/api.md §14.2, <c>POST /api/lending/debtor-messages</c>,
/// <c>Lending.DebtorMessages.Send</c>, que expone T0309; adaptadores-y-archivos.md §4.5–§4.6). Sin <see cref="ScheduledFor"/> es
/// <b>inmediato</b>: si la ley no lo permite ahora se rechaza (<c>Lending.DebtorMessage.OutsideLegalWindow</c> con <c>data.nextAllowedAt</c>,
/// <c>.WeeklyChannelUsed</c>, <c>.ChannelNotAuthorized</c>, <c>Lending.Authorization.WithoutEvidence</c>); programado, nace <c>Queued</c>,
/// <c>Deferred</c> con el instante nuevo o <c>Excluded</c> con el motivo. Sólo al deudor o a un codeudor obligado
/// (<c>.RecipientNotObligated</c>). El crédito fuera del alcance de quien lo pide no existe (<c>Lending.Loan.NotFound</c>). Lo encola
/// <see cref="ColaDeMensajesAlDeudor"/> en la transacción de la operación; la clave es la <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed record SendDebtorMessageCommand(
    Guid? LoanPublicId,
    Guid RecipientPersonPublicId,
    MessageChannel Channel,
    DebtorMessagePurpose Purpose,
    string TemplateCode,
    DateTimeOffset? ScheduledFor = null,
    IReadOnlyDictionary<string, string?>? Variables = null) : IRequest<Result<DebtorMessageDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendDebtorMessageCommandValidator : AbstractValidator<SendDebtorMessageCommand>
{
    /// <summary>Los propósitos del envío manual (api.md §14.2); el aviso de gasto de cobranza es de K4.</summary>
    public static readonly DebtorMessagePurpose[] Propositos =
        [DebtorMessagePurpose.Collection, DebtorMessagePurpose.PriorNotice, DebtorMessagePurpose.Informative, DebtorMessagePurpose.DocumentDelivery];

    public SendDebtorMessageCommandValidator()
    {
        RuleFor(x => x.RecipientPersonPublicId).NotEmpty().WithMessage("Indique a quién va el mensaje.");
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Purpose).Must(p => Propositos.Contains(p))
            .WithMessage("El propósito de un envío manual es cobranza, aviso previo, informativo o envío de documento.");
        RuleFor(x => x.TemplateCode).NotEmpty().MaximumLength(20).WithMessage("Indique la plantilla del mensaje.");
        RuleFor(x => x.LoanPublicId).NotEmpty().When(x => x.Purpose != DebtorMessagePurpose.DocumentDelivery)
            .WithMessage("Indique el crédito del mensaje.");
    }
}

public sealed class SendDebtorMessageCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<SendDebtorMessageCommand, Result<DebtorMessageDto>>
{
    public async Task<Result<DebtorMessageDto>> Handle(SendDebtorMessageCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.Kind == ActorKind.Person && actor.UserId is null) return Result.Failure<DebtorMessageDto>(ErroresDeGobierno.SinUsuario());

        int? loanId = null;
        if (request.LoanPublicId is { } loanPublicId)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.PublicId == loanPublicId)
                .Select(l => new { l.Id, l.BranchId, l.CreditLineId }).FirstOrDefaultAsync(ct);
            if (credito is null) return Result.Failure<DebtorMessageDto>(ErroresDeMensajesAlDeudor.CreditoInexistente());
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeMensajesAlDeudor.CreditoInexistente(), auditoria,
                "Loan", loanPublicId, ct);
            if (dentro.IsFailure) return Result.Failure<DebtorMessageDto>(dentro.Error);
            loanId = credito.Id;
        }

        var personaId = await db.People.AsNoTracking().Where(p => p.PublicId == request.RecipientPersonPublicId).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (personaId is null) return Result.Failure<DebtorMessageDto>(ErroresDeMensajesAlDeudor.DestinatarioNoObligado());

        var clave = request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey;
        var encolado = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(personaId.Value, loanId, request.Purpose, request.Channel,
            request.TemplateCode, "Manual", clave.ToString("N"), actor.Kind, actor.UserId)
        {
            OriginPublicId = clave,
            ProgramadoPara = request.ScheduledFor?.UtcDateTime,
            Inmediato = request.ScheduledFor is null,
            Valores = request.Variables,
        }, ct);
        if (encolado.IsFailure) return Result.Failure<DebtorMessageDto>(encolado.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(await ProyeccionDeMensajesAlDeudor.UnoAsync(db, encolado.Value.Mensaje.Id, ct));
    }
}

/// <summary>
/// Cancela un mensaje al deudor antes de que salga (feature 013, T0308; api.md §14.2, <c>POST /api/lending/debtor-messages/{id}/cancel</c>,
/// <c>Lending.DebtorMessages.Cancel</c>): sólo en <c>Queued</c> o <c>Deferred</c> (<c>Lending.DebtorMessage.AlreadySent</c> si ya salió o
/// terminó), con motivo, firmado por quien cancela. Fuera del alcance, el mismo 404 del mensaje inexistente. (nuevo)
/// </summary>
public sealed record CancelDebtorMessageCommand(Guid DebtorMessagePublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelDebtorMessageCommandValidator : ValidadorConMotivo<CancelDebtorMessageCommand>
{
    public CancelDebtorMessageCommandValidator() => RuleFor(x => x.DebtorMessagePublicId).NotEmpty();
}

public sealed class CancelDebtorMessageCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<CancelDebtorMessageCommand, Result>
{
    /// <summary>El código de motivo de la cancelación de una persona.</summary>
    public const string MotivoCanceladoPorPersona = "CancelledByUser";

    public async Task<Result> Handle(CancelDebtorMessageCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.Kind == ActorKind.Person && actor.UserId is null) return Result.Failure(ErroresDeGobierno.SinUsuario());

        var mensaje = await db.DebtorMessages.FirstOrDefaultAsync(m => m.PublicId == request.DebtorMessagePublicId, ct);
        if (mensaje is null) return Result.Failure(ErroresDeMensajesAlDeudor.MensajeInexistente());
        var credito = mensaje.LoanId is { } loanId
            ? await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => new { l.BranchId, l.CreditLineId }).FirstOrDefaultAsync(ct)
            : null;
        var dentro = credito is null
            ? await AlcanceTotalAsync(mensaje, ct)
            : await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeMensajesAlDeudor.MensajeInexistente(), auditoria,
                "DebtorMessage", mensaje.PublicId, ct);
        if (dentro.IsFailure) return dentro;

        var ultimo = await cola.UltimoEventoAsync(mensaje, ct);
        if (ultimo?.Kind is not (DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred))
            return Result.Failure(ErroresDeMensajesAlDeudor.YaSalio(ultimo?.Kind ?? DebtorMessageEventKind.Cancelled));

        await cola.RegistrarAsync(mensaje, new EventoDeMensaje(DebtorMessageEventKind.Cancelled, reloj.UtcNow, actor.Kind, actor.UserId)
        {
            CodigoDeMotivo = MotivoCanceladoPorPersona,
            TextoDelMotivo = request.Reason.Trim(),
        }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Un mensaje sin crédito (un documento general) sólo lo toca quien tiene todas las oficinas (falla cerrado).</summary>
    private async Task<Result> AlcanceTotalAsync(DebtorMessage mensaje, CancellationToken ct)
    {
        var delUsuario = await alcance.ObtenerAsync(ct);
        if (delUsuario.TodasLasOficinas) return Result.Success();
        await auditoria.EmitirFueraDeAlcanceAsync("DebtorMessage", mensaje.PublicId, ct);
        return Result.Failure(ErroresDeMensajesAlDeudor.MensajeInexistente());
    }
}
