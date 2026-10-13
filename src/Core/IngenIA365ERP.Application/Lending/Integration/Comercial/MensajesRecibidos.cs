using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>Una evaluación de un mensaje recibido (api.md §16.2). (nuevo)</summary>
public sealed record InboundEvaluationDto(int Number, string Outcome, string? Code, string? Reason, DateTime At, bool Retryable, string? CreditLineCode,
    string ChecksJson, string? PrevalidationOutcome, Guid? ResponseMessagePublicId);

/// <summary>La respuesta que Cartera envió a Comercial por un mensaje (api.md §16.2). (nuevo)</summary>
public sealed record InboundResponseDto(string Type, Guid MessagePublicId);

/// <summary>Un recibo de un mensaje que llegó a Cartera (api.md §16.2). (nuevo)</summary>
public sealed record LendingInboundMessageDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    string OriginModule,
    string? OriginNumber,
    DateOnly OperationDate,
    DateTime ReceivedAt,
    DateTime LastProcessedAt,
    string Status,
    RefDto? Loan,
    string ValidationStatus,
    string? ValidationReason,
    DateTime? NotRetryableAt,
    string? NotRetryableReason,
    IReadOnlyList<InboundEvaluationDto> Evaluations,
    InboundResponseDto? Response);

/// <summary>El recibo con el contenido del mensaje (api.md §16.2, <c>GET /{messagePublicId}</c>). (nuevo)</summary>
public sealed record LendingInboundMessageDetailDto(LendingInboundMessageDto Receipt, string? PayloadJson);

/// <summary>Lo que respondió marcar un recibo no reprocesable (api.md §16.2). (nuevo)</summary>
public sealed record InboundMessageMarkedDto(string Status, DateTime NotRetryableAt, InboundResponseDto Response);

/// <summary>
/// <c>GET /api/lending/inbound-messages</c> (feature 013, US6, T0595; api.md §16.2, <c>Lending.Messages.Read</c>): los recibos de lo que llegó a Cartera
/// (ventas, ajustes, resultados del giro) con sus evaluaciones y la respuesta enviada. Alcance por la oficina y la línea del crédito
/// (<c>LasConsultasDeCarteraRespetanElAlcance</c>); un recibo sin crédito —rechazado o no incorporado— no es de ninguna oficina y lo ve quien tiene
/// el permiso. (nuevo)
/// </summary>
public sealed record ListLendingInboundMessagesQuery(
    InboundMessageStatus? Status = null,
    string? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<LendingInboundMessageDto>>>;

public sealed class ListLendingInboundMessagesQueryValidator : AbstractValidator<ListLendingInboundMessagesQuery>
{
    public ListLendingInboundMessagesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListLendingInboundMessagesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLendingInboundMessagesQuery, Result<PagedResult<LendingInboundMessageDto>>>
{
    public async Task<Result<PagedResult<LendingInboundMessageDto>>> Handle(ListLendingInboundMessagesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var enAlcance = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.LendingInboundMessages.AsNoTracking().Where(r => !r.IsDeleted && (r.LoanId == null || enAlcance.Contains(r.LoanId.Value)));
        if (request.Status is { } estado) consulta = consulta.Where(r => r.Status == estado);
        if (!string.IsNullOrWhiteSpace(request.Type)) consulta = consulta.Where(r => r.MessageType == request.Type);
        if (request.From is { } desde) consulta = consulta.Where(r => r.OperationDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(r => r.OperationDate <= hasta);

        var total = await consulta.CountAsync(ct);
        var pagina = await consulta.OrderByDescending(r => r.LastProcessedAt).ThenByDescending(r => r.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var dtos = await MensajesRecibidos.DtosAsync(db, pagina, ct);
        return Result.Success(new PagedResult<LendingInboundMessageDto>(dtos, request.Page, request.PageSize, total));
    }
}

/// <summary>
/// <c>GET /api/lending/inbound-messages/{messagePublicId}</c> (feature 013, US6, T0595; <c>Lending.Messages.Read</c>): el recibo con cada evaluación y el
/// contenido del mensaje (leído por <see cref="IMensajesEntrantes"/>). Inexistente o fuera del alcance: <c>Lending.InboundMessage.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetLendingInboundMessageQuery(Guid MessagePublicId) : IRequest<Result<LendingInboundMessageDetailDto>>;

public sealed class GetLendingInboundMessageQueryValidator : AbstractValidator<GetLendingInboundMessageQuery>
{
    public GetLendingInboundMessageQueryValidator() => RuleFor(x => x.MessagePublicId).NotEmpty();
}

public sealed class GetLendingInboundMessageQueryHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<GetLendingInboundMessageQuery, Result<LendingInboundMessageDetailDto>>
{
    public async Task<Result<LendingInboundMessageDetailDto>> Handle(GetLendingInboundMessageQuery request, CancellationToken ct)
    {
        var recibo = await MensajesRecibidos.EnAlcanceAsync(db, alcance, auditoria, request.MessagePublicId, ct);
        if (recibo.IsFailure) return Result.Failure<LendingInboundMessageDetailDto>(recibo.Error);
        var dto = (await MensajesRecibidos.DtosAsync(db, [recibo.Value], ct))[0];
        var leido = await entrantes.LeerAsync([request.MessagePublicId], IntegrationDestinations.Lending, ct);
        return Result.Success(new LendingInboundMessageDetailDto(dto, leido.IsSuccess ? leido.Value[0].PayloadJson : null));
    }
}

/// <summary>
/// <c>POST /api/lending/inbound-messages/{messagePublicId}/mark-not-retryable</c> (feature 013, US6, T0595; api.md §16.2, <c>Lending.Messages.Reprocess</c>,
/// con clave y motivo; C95, C102): una persona declara que una venta no convertible ya no se va a reprocesar. En la misma transacción llena
/// <c>NotRetryable*</c> y emite <c>ObligacionRechazada</c> <c>NoConvertible</c> con <c>retryable = false</c> y la clave de la <b>última evaluación</b>
/// (C102: sólo lo declara una persona). Un recibo que no está <c>Rejected</c> o ya está marcado: <c>Lending.InboundMessage.NotMarkable</c>. (nuevo)
/// </summary>
public sealed record MarkLendingInboundMessageNotRetryableCommand(Guid MessagePublicId, string Reason)
    : IRequest<Result<InboundMessageMarkedDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkLendingInboundMessageNotRetryableCommandValidator : ValidadorConMotivo<MarkLendingInboundMessageNotRetryableCommand>
{
    public MarkLendingInboundMessageNotRetryableCommandValidator() => RuleFor(x => x.MessagePublicId).NotEmpty();
}

public sealed class MarkLendingInboundMessageNotRetryableCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    EmisionDeCartera emision,
    VentasAnterioresAlCorte anteriores,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<MarkLendingInboundMessageNotRetryableCommand, Result<InboundMessageMarkedDto>>
{
    public async Task<Result<InboundMessageMarkedDto>> Handle(MarkLendingInboundMessageNotRetryableCommand request, CancellationToken ct)
    {
        var enAlcance = await MensajesRecibidos.EnAlcanceAsync(db, alcance, auditoria, request.MessagePublicId, ct);
        if (enAlcance.IsFailure) return Result.Failure<InboundMessageMarkedDto>(enAlcance.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var recibo = await db.LendingInboundMessages.FirstAsync(r => r.MessagePublicId == request.MessagePublicId, ct);
            if (recibo.Status != InboundMessageStatus.Rejected || recibo.NotRetryableAt is not null || recibo.LastEvaluationId is null)
                return Result.Failure<InboundMessageMarkedDto>(ErroresDeVentasACredito.NoMarcable());
            var ultima = await db.LendingInboundMessageEvaluations.AsNoTracking().FirstAsync(e => e.Id == recibo.LastEvaluationId, ct);
            var leido = await entrantes.LeerAsync([request.MessagePublicId], IntegrationDestinations.Lending, ct);
            if (leido.IsFailure) return Result.Failure<InboundMessageMarkedDto>(leido.Error);
            var mensaje = leido.Value[0];

            var actor = await actorActual.ObtenerAsync(ct);
            var motivo = request.Reason.Trim();
            var ahora = reloj.UtcNow;
            var codigo = ultima.FailureCode ?? ErroresDeVentasACredito.CodigoNoMarcable;
            var razon = $"{ultima.FailureReason} El mensaje se marcó no reprocesable: {motivo}";
            var respuesta = await emision.ObligacionRechazadaAsync(new ObligacionRechazadaAEmitir(recibo, mensaje, recibo.CreditPaymentPublicId ?? Guid.Empty,
                ultima.PublicId, EmisionDeCartera.MotivoNoConvertible, codigo, razon, await anteriores.SucursalAsync(mensaje, ct)), ct);
            recibo.NotRetryableAt = ahora;
            recibo.NotRetryableByUserId = actor.UserId;
            recibo.NotRetryableReason = RecibosDeCartera.Recortar(motivo, 500);
            recibo.ResponseMessagePublicId = respuesta.PublicId;
            await db.SaveChangesAsync(ct);
            return Result.Success(new InboundMessageMarkedDto(nameof(InboundMessageStatus.Rejected), ahora,
                new InboundResponseDto(ObligacionRechazadaV1.Type, respuesta.PublicId)));
        }, ct);
    }
}

/// <summary>Lo que comparten las consultas y los comandos de los mensajes recibidos (alcance y forma). (nuevo)</summary>
public static class MensajesRecibidos
{
    public const string CodigoInexistente = ErroresDeVentasACredito.CodigoReciboInexistente;

    /// <summary>El recibo dentro del alcance de quien pregunta (por el crédito; sin crédito, de nadie): si no, el 404 del recibo y el evento de alcance.</summary>
    public static async Task<Result<LendingInboundMessage>> EnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid messagePublicId, CancellationToken ct)
    {
        var recibo = await db.LendingInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == messagePublicId && !r.IsDeleted, ct);
        if (recibo is null) return Result.Failure<LendingInboundMessage>(ErroresDeVentasACredito.ReciboInexistente());
        if (recibo.LoanId is not { } loanId) return Result.Success(recibo);
        var credito = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => new { l.BranchId, l.CreditLineId }).FirstAsync(ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeVentasACredito.ReciboInexistente(), auditoria,
            nameof(LendingInboundMessage), messagePublicId, ct);
        return dentro.IsFailure ? Result.Failure<LendingInboundMessage>(dentro.Error) : Result.Success(recibo);
    }

    /// <summary>Los DTO de unos recibos, con sus evaluaciones, su crédito y la respuesta enviada, en el orden dado.</summary>
    public static async Task<IReadOnlyList<LendingInboundMessageDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<LendingInboundMessage> recibos,
        CancellationToken ct)
    {
        var ids = recibos.Select(r => r.Id).ToList();
        var evaluaciones = await db.LendingInboundMessageEvaluations.AsNoTracking().Where(e => ids.Contains(e.InboundMessageId)).ToListAsync(ct);
        var creditos = recibos.Where(r => r.LoanId is not null).Select(r => r.LoanId!.Value).Distinct().ToList();
        var refs = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => new RefDto(l.PublicId, l.Number), ct);
        var respuestas = recibos.Where(r => r.ResponseMessagePublicId is not null).Select(r => r.ResponseMessagePublicId!.Value).ToList();
        var tipos = await db.IntegrationMessages.AsNoTracking().Where(m => respuestas.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, m => m.Type, ct);
        return recibos.Select(r =>
        {
            var suyas = evaluaciones.Where(e => e.InboundMessageId == r.Id).OrderBy(e => e.EvaluationNumber).ToList();
            var validacion = ConsultasDeCartera.EstadoDe(r, suyas.LastOrDefault());
            return new LendingInboundMessageDto(r.MessagePublicId, r.MessageType, r.MessageVersion, r.OriginModule, r.OriginNumber, r.OperationDate,
                r.FirstReceivedAt, r.LastProcessedAt, r.Status.ToString(), r.LoanId is { } l ? refs.GetValueOrDefault(l) : null, validacion.Status,
                validacion.Reason, r.NotRetryableAt, r.NotRetryableReason,
                suyas.Select(e => new InboundEvaluationDto(e.EvaluationNumber, e.Outcome.ToString(), e.FailureCode, e.FailureReason, e.EvaluatedAt, e.Retryable,
                    e.CreditLineCode, e.ChecksJson, e.PrevalidationOutcome, e.ResponseMessagePublicId)).ToList(),
                r.ResponseMessagePublicId is { } m ? new InboundResponseDto(tipos.GetValueOrDefault(m) ?? string.Empty, m) : null);
        }).ToList();
    }
}
