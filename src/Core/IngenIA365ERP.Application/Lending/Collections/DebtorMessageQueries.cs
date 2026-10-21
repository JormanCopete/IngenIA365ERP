using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>El crédito de un mensaje, por su número (api.md §1.7). (nuevo)</summary>
public sealed record CreditoDelMensajeDto(Guid LoanPublicId, string Number);

/// <summary>La plantilla sellada en un mensaje: código y versión. (nuevo)</summary>
public sealed record PlantillaDelMensajeDto(string Code, int Version);

/// <summary>El último paso de un mensaje (su estado): clase, instante, detalle, el instante siguiente si quedó diferido y los motivos. (nuevo)</summary>
public sealed record UltimoEventoDelMensajeDto(DebtorMessageEventKind Kind, DateTime At, string? Detail, DateTime? NextAttemptAt, IReadOnlyList<string> Reasons);

/// <summary>Un mensaje al deudor en el registro de envíos (api.md §14.2, <c>DebtorMessageDto</c>). (nuevo)</summary>
public sealed record DebtorMessageDto(
    Guid PublicId,
    CreditoDelMensajeDto? Loan,
    PersonaRefDto Recipient,
    ContactRole RecipientRole,
    MessageChannel Channel,
    DebtorMessagePurpose Purpose,
    PlantillaDelMensajeDto? Template,
    DateTime RequestedAt,
    DateTime? ScheduledFor,
    DebtorMessageEventKind Status,
    UltimoEventoDelMensajeDto LastEvent,
    bool Simulated);

/// <summary>Un paso de la historia de un mensaje (sólo inserción). (nuevo)</summary>
public sealed record DebtorMessageEventDto(
    int Sequence,
    DebtorMessageEventKind Kind,
    DateTime OccurredAt,
    DateTime? NextAttemptAt,
    IReadOnlyList<string> Reasons,
    string? ReasonCode,
    string? ReasonText,
    string? DestinationAddress,
    int? Attempt,
    string? ProviderReference,
    ExternalOutcome? Outcome,
    ActorKind ActorKind);

/// <summary>
/// Un mensaje con su cuerpo renderizado, el adaptador sellado, el destino copiado al enviar (C57), la autorización usada y su historia
/// (api.md §14.2, <c>GET /{id}</c>). (nuevo)
/// </summary>
public sealed record DebtorMessageDetailDto(
    DebtorMessageDto Message,
    string? Subject,
    string Body,
    string ChannelCode,
    string? DestinationAddress,
    Guid? AuthorizationPublicId,
    IReadOnlyList<DebtorMessageEventDto> Events);

/// <summary>
/// El registro de envíos (feature 013, T0308; api.md §14.2, <c>GET /api/lending/debtor-messages</c>, <c>Lending.DebtorMessages.Read</c>):
/// filtra por crédito, persona, propósito, canal, estado (el último evento) y fechas del pedido (hora de Colombia), y respeta el alcance de
/// Cartera por la oficina y la línea del crédito —lo de fuera no aparece ni cuenta; un mensaje sin crédito sólo lo ve el alcance total—.
/// El más reciente primero. (nuevo)
/// </summary>
public sealed record ListDebtorMessagesQuery(
    Guid? LoanPublicId = null,
    Guid? PersonPublicId = null,
    DebtorMessagePurpose? Purpose = null,
    MessageChannel? Channel = null,
    DebtorMessageEventKind? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<DebtorMessageDto>>>;

public sealed class ListDebtorMessagesQueryValidator : AbstractValidator<ListDebtorMessagesQuery>
{
    public ListDebtorMessagesQueryValidator()
    {
        RuleFor(x => x.Purpose).IsInEnum().When(x => x.Purpose is not null);
        RuleFor(x => x.Channel).IsInEnum().When(x => x.Channel is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListDebtorMessagesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListDebtorMessagesQuery, Result<PagedResult<DebtorMessageDto>>>
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public async Task<Result<PagedResult<DebtorMessageDto>>> Handle(ListDebtorMessagesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = ProyeccionDeMensajesAlDeudor.ConAlcance(db, alcance);

        if (request.LoanPublicId is { } loan) consulta = consulta.Where(x => x.LoanPublicId == loan);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.Mensaje.PersonId == id);
        }
        if (request.Purpose is { } proposito) consulta = consulta.Where(x => x.Mensaje.Purpose == proposito);
        if (request.Channel is { } canal) consulta = consulta.Where(x => x.Mensaje.Channel == canal);
        if (request.From is { } desde)
        {
            var desdeUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
            consulta = consulta.Where(x => x.Mensaje.RequestedAt >= desdeUtc);
        }
        if (request.To is { } hasta)
        {
            var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
            consulta = consulta.Where(x => x.Mensaje.RequestedAt < hastaUtc);
        }
        if (request.Status is { } estado)
            consulta = consulta.Where(x => db.DebtorMessageEvents
                .Where(e => e.DebtorMessageId == x.Mensaje.Id).OrderByDescending(e => e.Sequence).Select(e => e.Kind).FirstOrDefault() == estado);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var ids = await consulta.OrderByDescending(x => x.Mensaje.RequestedAt).ThenByDescending(x => x.Mensaje.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(x => x.Mensaje.Id).ToListAsync(ct);
        var items = await ProyeccionDeMensajesAlDeudor.VariosAsync(db, ids, ct);
        return Result.Success(new PagedResult<DebtorMessageDto>(ids.Select(i => items[i]).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Un mensaje con su historia (feature 013, T0308; api.md §14.2, <c>GET /api/lending/debtor-messages/{id}</c>,
/// <c>Lending.DebtorMessages.Read</c>): el cuerpo renderizado, el destino copiado al enviar, la autorización usada y los eventos. Fuera del
/// alcance, el mismo 404 que si no existiera (<c>Lending.DebtorMessage.NotFound</c>). (nuevo)
/// </summary>
public sealed record GetDebtorMessageQuery(Guid PublicId) : IRequest<Result<DebtorMessageDetailDto>>;

public sealed class GetDebtorMessageQueryValidator : AbstractValidator<GetDebtorMessageQuery>
{
    public GetDebtorMessageQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetDebtorMessageQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetDebtorMessageQuery, Result<DebtorMessageDetailDto>>
{
    public async Task<Result<DebtorMessageDetailDto>> Handle(GetDebtorMessageQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var mensaje = await ProyeccionDeMensajesAlDeudor.ConAlcance(db, alcance)
            .Where(x => x.Mensaje.PublicId == request.PublicId).Select(x => x.Mensaje).FirstOrDefaultAsync(ct);
        if (mensaje is null) return Result.Failure<DebtorMessageDetailDto>(ErroresDeMensajesAlDeudor.MensajeInexistente());

        var resumen = await ProyeccionDeMensajesAlDeudor.UnoAsync(db, mensaje.Id, ct);
        var eventos = await db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == mensaje.Id).OrderBy(e => e.Sequence).ToListAsync(ct);
        var destino = eventos.LastOrDefault(e => e.Kind is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Simulated)?.DestinationAddress;
        return Result.Success(new DebtorMessageDetailDto(resumen, mensaje.Subject, mensaje.RenderedBody, mensaje.ChannelCode, destino,
            mensaje.AuthorizationConsentPublicId, eventos.Select(ProyeccionDeMensajesAlDeudor.Evento).ToList()));
    }
}

/// <summary>
/// La decisión de la ventana legal de contacto para una persona, un canal y un propósito (feature 013, T0308;
/// adaptadores-y-archivos.md §4.6, <c>GET /api/lending/debtor-messages/contact-window</c>, <c>Lending.DebtorMessages.Read</c>): para la agenda
/// y antes de contactar. Sólo lee. <see cref="At"/> nulo = ahora. (nuevo)
/// </summary>
public sealed record GetContactWindowQuery(
    Guid PersonPublicId,
    MessageChannel Channel,
    DebtorMessagePurpose Purpose,
    DateTimeOffset? At = null,
    Guid? LoanPublicId = null) : IRequest<Result<ContactWindowDto>>;

/// <summary>
/// La respuesta de <see cref="GetContactWindowQuery"/>: <c>Permitido</c>, <c>Diferido</c> o <c>Excluido</c>, el instante en que se puede
/// contactar (o el siguiente), los motivos y el canal que ya fijó la semana. (nuevo)
/// </summary>
public sealed record ContactWindowDto(string Decision, DateTimeOffset? AllowedAt, DateTimeOffset? NextAllowedAt, IReadOnlyList<string> Reasons,
    string? ChannelUsed);

public sealed class GetContactWindowQueryValidator : AbstractValidator<GetContactWindowQuery>
{
    public GetContactWindowQueryValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
    }
}

public sealed class GetContactWindowQueryHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj) : IRequestHandler<GetContactWindowQuery, Result<ContactWindowDto>>
{
    public async Task<Result<ContactWindowDto>> Handle(GetContactWindowQuery request, CancellationToken ct)
    {
        var personaId = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (personaId is null) return Result.Failure<ContactWindowDto>(ErroresDeGobierno.CodigoPersonaInexistente, "La persona no existe en esta cooperativa.");

        int? loanId = null;
        if (request.LoanPublicId is { } loanPublicId)
        {
            var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
            var credito = await db.Loans.AsNoTracking().Where(l => l.PublicId == loanPublicId)
                .Select(l => new { l.Id, l.BranchId, l.CreditLineId }).FirstOrDefaultAsync(ct);
            if (credito is null || !alcance.IncluyeOficina(credito.BranchId) || !alcance.IncluyeLinea(credito.CreditLineId))
                return Result.Failure<ContactWindowDto>(ErroresDeMensajesAlDeudor.CreditoInexistente());
            loanId = credito.Id;
        }

        var pedido = request.At?.UtcDateTime ?? reloj.UtcNow;
        var ventana = await cola.EvaluarAsync(new ConsultaDeVentana(personaId.Value, loanId, request.Channel, request.Purpose,
            DateTime.SpecifyKind(pedido, DateTimeKind.Utc), request.At is null), ct);
        if (ventana.IsFailure) return Result.Failure<ContactWindowDto>(ventana.Error);

        var v = ventana.Value;
        return Result.Success(v.Decision switch
        {
            Permitido => new ContactWindowDto("Permitido", v.PermitidoDesde, null, [], v.CanalDeLaSemana?.ToString()),
            Diferido d => new ContactWindowDto("Diferido", null, v.PermitidoDesde, d.Motivos.Select(m => m.ToString()).ToList(), v.CanalDeLaSemana?.ToString()),
            Excluido x => new ContactWindowDto("Excluido", null, null, [x.Motivo.ToString()], v.CanalDeLaSemana?.ToString()),
            _ => throw new InvalidOperationException("Decisión de la ventana legal desconocida."),
        });
    }
}

/// <summary>Las proyecciones compartidas por las consultas y los comandos de los mensajes al deudor. (nuevo)</summary>
internal static class ProyeccionDeMensajesAlDeudor
{
    internal sealed class MensajeConCredito
    {
        public DebtorMessage Mensaje { get; init; } = null!;
        public int? Oficina { get; init; }
        public int? Linea { get; init; }
        public Guid? LoanPublicId { get; init; }
    }

    /// <summary>Los mensajes con la oficina y la línea de su crédito, filtrados por el alcance (sin crédito, sólo el alcance total).</summary>
    public static IQueryable<MensajeConCredito> ConAlcance(IApplicationDbContext db, AlcanceDeCartera alcance)
    {
        var consulta = from m in db.DebtorMessages.AsNoTracking()
                       join l in db.Loans.AsNoTracking() on m.LoanId equals (int?)l.Id into creditos
                       from l in creditos.DefaultIfEmpty()
                       select new MensajeConCredito
                       {
                           Mensaje = m,
                           Oficina = l == null ? (int?)null : l.BranchId,
                           Linea = l == null ? (int?)null : l.CreditLineId,
                           LoanPublicId = l == null ? (Guid?)null : l.PublicId,
                       };
        if (!alcance.TodasLasOficinas)
        {
            var oficinas = alcance.Oficinas.ToArray();
            consulta = consulta.Where(x => x.Oficina != null && oficinas.Contains(x.Oficina.Value));
        }
        if (!alcance.TodasLasLineas)
        {
            var lineas = alcance.Lineas.ToArray();
            consulta = consulta.Where(x => x.Linea != null && lineas.Contains(x.Linea.Value));
        }
        return consulta;
    }

    public static async Task<DebtorMessageDto> UnoAsync(IApplicationDbContext db, int id, CancellationToken ct) =>
        (await VariosAsync(db, [id], ct))[id];

    public static async Task<IReadOnlyDictionary<int, DebtorMessageDto>> VariosAsync(IApplicationDbContext db, IReadOnlyCollection<int> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, DebtorMessageDto>();
        var mensajes = await db.DebtorMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var eventos = await db.DebtorMessageEvents.AsNoTracking().Where(e => ids.Contains(e.DebtorMessageId)).ToListAsync(ct);
        var ultimos = eventos.GroupBy(e => e.DebtorMessageId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Sequence).First());
        var loanIds = mensajes.Where(m => m.LoanId is not null).Select(m => m.LoanId!.Value).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => loanIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new CreditoDelMensajeDto(l.PublicId, l.Number), ct);
        var plantillaIds = mensajes.Where(m => m.TemplateId is not null).Select(m => m.TemplateId!.Value).Distinct().ToList();
        var plantillas = await db.NotificationTemplates.AsNoTracking().Where(t => plantillaIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, mensajes.Select(m => m.PersonId), ct);

        return mensajes.ToDictionary(m => m.Id, m =>
        {
            var ultimo = ultimos[m.Id];
            return new DebtorMessageDto(
                m.PublicId,
                m.LoanId is { } l ? creditos.GetValueOrDefault(l) : null,
                personas[m.PersonId],
                m.RecipientRole,
                m.Channel,
                m.Purpose,
                m.TemplateId is { } t && m.TemplateVersion is { } v && plantillas.TryGetValue(t, out var code) ? new PlantillaDelMensajeDto(code, v) : null,
                m.RequestedAt,
                m.ScheduledFor,
                ultimo.Kind,
                new UltimoEventoDelMensajeDto(ultimo.Kind, ultimo.OccurredAt, ultimo.ReasonText, ultimo.DueAt,
                    ultimo.Motivos().Select(x => x.ToString()).ToList()),
                ultimo.Kind == DebtorMessageEventKind.Simulated);
        });
    }

    public static DebtorMessageEventDto Evento(DebtorMessageEvent e) =>
        new(e.Sequence, e.Kind, e.OccurredAt, e.DueAt, e.Motivos().Select(m => m.ToString()).ToList(), e.ReasonCode, e.ReasonText,
            e.DestinationAddress, e.AttemptNumber, e.ProviderReference, e.ExternalOutcome, e.ActorKind);
}
