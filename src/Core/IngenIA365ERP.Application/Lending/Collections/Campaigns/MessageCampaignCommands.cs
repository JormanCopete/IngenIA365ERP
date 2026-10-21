using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Campaigns;

// Feature 013, K4 (US12, T0949; api.md §14.9; data-model §26.3; FR-122, US12-4): las campañas de mensajes al deudor. Programar evalúa la ventana legal
// por destinatario y deja cada mensaje con su evento —el excluido, con su constancia y su motivo—. Sólo deudores: nunca referencias. (nuevo)

/// <summary>Los criterios de una campaña (api.md §14.9 <c>filters</c>; data-model §26.3 <c>SelectionCriteriaJson</c>). (nuevo)</summary>
public sealed record CampaignFilters(
    string? StageCode = null,
    Guid? OfficePublicId = null,
    Guid? CreditLinePublicId = null,
    int? MinDaysPastDue = null,
    Guid? PaymasterPersonPublicId = null);

/// <summary>Los conteos de una campaña por el estado de sus mensajes (api.md §14.9). (nuevo)</summary>
public sealed record CampaignCountsDto(int Queued, int Deferred, int Excluded, int Sent, int Failed, int Cancelled);

/// <summary>Una campaña (api.md §14.9). (nuevo)</summary>
public sealed record MessageCampaignDto(
    Guid PublicId,
    string Code,
    string Name,
    string TemplateCode,
    MessageChannel Channel,
    DebtorMessagePurpose Purpose,
    CampaignFilters Filters,
    DateTimeOffset ScheduledFor,
    MessageCampaignStatus Status,
    int TargetCount,
    CampaignCountsDto Counts,
    string? CancelReason);

/// <summary>Un destinatario de la vista previa: el crédito, el deudor y qué diría la ventana. (nuevo)</summary>
public sealed record CampaignRecipientDto(CreditoRefDto Loan, PersonaRefDto Debtor, string Decision, DateTimeOffset? SendsAt);

/// <summary>Un excluido de la vista previa, con su motivo (<c>ChannelNotAuthorized</c>, <c>Deceased</c>, <c>Insolvency</c>…). (nuevo)</summary>
public sealed record CampaignExclusionDto(CreditoRefDto Loan, PersonaRefDto Debtor, string Reason);

/// <summary>La vista previa de una campaña (api.md §14.9): los destinatarios y los excluidos por motivo. No escribe. (nuevo)</summary>
public sealed record CampaignPreviewDto(IReadOnlyList<CampaignRecipientDto> Recipients, IReadOnlyList<CampaignExclusionDto> Excluded);

/// <summary>Lo común de las campañas: los criterios, la selección con alcance, la vista y los conteos. (nuevo)</summary>
public static class CampanasDeCobranza
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El origen de los mensajes de una campaña (<c>LND_DebtorMessages.OriginKind</c>).</summary>
    public const string Origen = "Campaign";

    /// <summary>El código del motivo con que la cancelación de la campaña cancela sus mensajes en cola.</summary>
    public const string MotivoCampanaCancelada = "CampaignCancelled";

    public static string Serializar(CampaignFilters f) => JsonSerializer.Serialize(f, Json);

    public static CampaignFilters Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CampaignFilters();
        try
        {
            return JsonSerializer.Deserialize<CampaignFilters>(json, Json) ?? new CampaignFilters();
        }
        catch (JsonException)
        {
            return new CampaignFilters();
        }
    }

    /// <summary>
    /// Los créditos vigentes del alcance que casan con los criterios: la etapa vigente, la oficina, la línea, los días de mora de la última posición y
    /// la pagaduría vigente. Cada uno va a su deudor (nunca a una referencia).
    /// </summary>
    public static async Task<IReadOnlyList<(Loan Credito, Domain.Entities.Lending.Projections.LoanDailyPosition? Posicion)>> SeleccionAsync(
        IApplicationDbContext db, AlcanceDeCartera alcance, CampaignFilters f, CancellationToken ct)
    {
        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.Status == LoanStatus.Active)
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (f.OfficePublicId is { } oficina)
        {
            var id = await db.Branches.AsNoTracking().Where(b => b.PublicId == oficina).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(l => l.BranchId == id);
        }
        if (f.CreditLinePublicId is { } linea)
        {
            var id = await db.CreditLines.AsNoTracking().Where(l => l.PublicId == linea).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(l => l.CreditLineId == id);
        }
        var creditos = await consulta.OrderBy(l => l.Number).ToListAsync(ct);
        var ids = creditos.Select(c => c.Id).ToList();
        if (!string.IsNullOrWhiteSpace(f.StageCode))
        {
            var etapa = f.StageCode.Trim().ToUpperInvariant();
            var etapas = await CobranzaDeCartera.EtapaVigenteAsync(db, ids, ct);
            creditos = creditos.Where(c => etapas.TryGetValue(c.Id, out var e) && e.StageCode == etapa).ToList();
        }
        if (f.PaymasterPersonPublicId is { } pagaduria)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == pagaduria).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            var conPagaduria = (await db.LoanPayrollLinks.AsNoTracking().Where(v => v.PaymasterPersonId == id && v.ValidTo == null && !v.IsDeleted)
                .Select(v => v.LoanId).ToListAsync(ct)).ToHashSet();
            creditos = creditos.Where(c => conPagaduria.Contains(c.Id)).ToList();
        }
        ids = creditos.Select(c => c.Id).ToList();
        var posiciones = (await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId)).ToListAsync(ct))
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First());
        var salida = creditos.Select(c => (c, posiciones.GetValueOrDefault(c.Id))).ToList();
        if (f.MinDaysPastDue is { } minimo) salida = salida.Where(x => (x.Item2?.DaysPastDue ?? 0) >= minimo).ToList();
        return salida;
    }

    /// <summary>Las variables de cobranza de un mensaje desde la última posición diaria del crédito (lo vencido, los días, las cuotas vencidas).</summary>
    public static Dictionary<string, string?> Variables(Domain.Entities.Lending.Projections.LoanDailyPosition? p)
    {
        var vencido = p is null ? 0m : p.PrincipalPastDue + p.DefaultInterest + p.DefaultInterestInSuspense + p.CollectionFee + p.OtherCharge;
        return new Dictionary<string, string?>
        {
            ["ValorVencido"] = ErroresDeCobranza.Pesos(vencido),
            ["DiasDeMora"] = (p?.DaysPastDue ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["CuotasVencidas"] = (p?.InstallmentsPastDue ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ValorParaPonerseAlDia"] = ErroresDeCobranza.Pesos(vencido),
        };
    }

    /// <summary>La ventana decía «excluido» por este motivo, en los nombres de api.md §14.9.</summary>
    public static string Motivo(ContactWindowReason motivo) => motivo switch
    {
        ContactWindowReason.DebtorDeceased => "Deceased",
        ContactWindowReason.WeeklyChannelLocked => "WeeklyChannelUsed",
        _ => motivo.ToString(),
    };

    public static async Task<IReadOnlyList<MessageCampaignDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<MessageCampaign> campanas, TimeSpan desfase,
        CancellationToken ct)
    {
        var ids = campanas.Select(c => c.Id).ToList();
        var plantillas = campanas.Select(c => c.TemplateId).Distinct().ToList();
        var codigos = await db.NotificationTemplates.AsNoTracking().IgnoreQueryFilters().Where(t => plantillas.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var ultimos = await (from m in db.DebtorMessages.AsNoTracking()
                             join e in db.DebtorMessageEvents.AsNoTracking() on m.Id equals e.DebtorMessageId
                             where m.CampaignId != null && ids.Contains(m.CampaignId.Value)
                                   && !db.DebtorMessageEvents.Any(x => x.DebtorMessageId == e.DebtorMessageId && x.Sequence > e.Sequence)
                             select new { CampaignId = m.CampaignId!.Value, e.Kind }).ToListAsync(ct);
        return campanas.Select(c =>
        {
            var de = ultimos.Where(u => u.CampaignId == c.Id).ToList();
            int Cuenta(params DebtorMessageEventKind[] k) => de.Count(u => k.Contains(u.Kind));
            return new MessageCampaignDto(c.PublicId, c.Code, c.Name, codigos.GetValueOrDefault(c.TemplateId) ?? string.Empty, c.Channel, c.Purpose,
                Leer(c.SelectionCriteriaJson), new DateTimeOffset(CobranzaDeCartera.ALocal(c.ScheduledFor, desfase), desfase), c.Status, c.TargetCount,
                new CampaignCountsDto(Cuenta(DebtorMessageEventKind.Queued), Cuenta(DebtorMessageEventKind.Deferred), Cuenta(DebtorMessageEventKind.Excluded),
                    Cuenta(DebtorMessageEventKind.Sent, DebtorMessageEventKind.Delivered, DebtorMessageEventKind.Simulated), Cuenta(DebtorMessageEventKind.Failed),
                    Cuenta(DebtorMessageEventKind.Cancelled)), c.CancelReason);
        }).ToList();
    }

    /// <summary>
    /// Pone al día el estado de las campañas programadas por lo que pasó con sus mensajes (lo llama el ciclo diario, paso 7): la programada cuyo primer
    /// mensaje ya salió pasa a <c>Running</c>; la que corre y ya no tiene mensajes en cola, a <c>Completed</c>. Sin guardar.
    /// </summary>
    public static async Task<int> AvanzarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var vivas = await db.MessageCampaigns.Where(c => !c.IsDeleted && (c.Status == MessageCampaignStatus.Scheduled || c.Status == MessageCampaignStatus.Running))
            .ToListAsync(ct);
        if (vivas.Count == 0) return 0;
        var ids = vivas.Select(c => c.Id).ToList();
        var ultimos = await (from m in db.DebtorMessages.AsNoTracking()
                             join e in db.DebtorMessageEvents.AsNoTracking() on m.Id equals e.DebtorMessageId
                             where m.CampaignId != null && ids.Contains(m.CampaignId.Value)
                                   && !db.DebtorMessageEvents.Any(x => x.DebtorMessageId == e.DebtorMessageId && x.Sequence > e.Sequence)
                             select new { CampaignId = m.CampaignId!.Value, e.Kind }).ToListAsync(ct);
        var cambiadas = 0;
        foreach (var c in vivas)
        {
            var de = ultimos.Where(u => u.CampaignId == c.Id).ToList();
            var enCola = de.Count(u => u.Kind is DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred);
            if (c.Status == MessageCampaignStatus.Scheduled && de.Count > enCola)
            {
                c.Iniciar();
                cambiadas++;
            }
            if (c.Status == MessageCampaignStatus.Running && enCola == 0)
            {
                c.Completar();
                cambiadas++;
            }
        }
        return cambiadas;
    }
}

// ---------------------------------------------------------------------------------------------------------------- crear --

/// <summary>
/// Crea una campaña en borrador (feature 013, K4, T0949; api.md §14.9, <c>POST /api/lending/campaigns</c>, <c>Lending.Campaigns.Manage</c>, con clave): código
/// de la cooperativa (único entre las vivas, <c>Lending.Campaign.CodeDuplicate</c>; data-model §26.3 lo trae y api.md no: T0956 lo suma al cuerpo), nombre,
/// la plantilla vigente del canal y el propósito —cobranza o informativo— (<c>Lending.MessageTemplate.NotFound</c>), los criterios —que tienen que poder
/// casar con algo: <c>Lending.Campaign.FiltersInvalid</c>, K4 bloque D— y el instante. (nuevo)
/// </summary>
public sealed record CreateMessageCampaignCommand(
    string Code,
    string Name,
    string TemplateCode,
    MessageChannel Channel,
    DebtorMessagePurpose Purpose,
    CampaignFilters Filters,
    DateTimeOffset ScheduledFor) : IRequest<Result<MessageCampaignDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateMessageCampaignCommandValidator : AbstractValidator<CreateMessageCampaignCommand>
{
    public CreateMessageCampaignCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(MessageCampaign.LargoDelCodigo).Must(c => CodigoDeCatalogo.Normalizar(c) is not null)
            .WithMessage($"El código de la campaña es alfanumérico, sin espacios y de hasta {MessageCampaign.LargoDelCodigo} caracteres.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TemplateCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Purpose).Must(p => p is DebtorMessagePurpose.Collection or DebtorMessagePurpose.Informative)
            .WithMessage("Una campaña es de cobranza o informativa.");
        RuleFor(x => x.Filters).NotNull();
        RuleFor(x => x.Filters.MinDaysPastDue).GreaterThanOrEqualTo(0).When(x => x.Filters?.MinDaysPastDue is not null);
    }
}

public sealed class CreateMessageCampaignCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<CreateMessageCampaignCommand, Result<MessageCampaignDto>>
{
    public async Task<Result<MessageCampaignDto>> Handle(CreateMessageCampaignCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.MessageCampaigns.AnyAsync(c => c.Code == codigo && !c.IsDeleted, ct)) return Falla(ErroresDeCobranza.CampanaDuplicada(codigo));
        if (await CriteriosInvalidosAsync(request.Filters, DateOnly.FromDateTime(CobranzaDeCartera.ALocal(request.ScheduledFor.UtcDateTime,
                reloj.AhoraLocal.Offset)), ct) is { } invalidos)
            return Falla(invalidos);
        var plantilla = CodigoDeCatalogo.Normalizar(request.TemplateCode) ?? request.TemplateCode.Trim().ToUpperInvariant();
        var canal = request.Channel.ToString();
        var proposito = request.Purpose.ToString();
        var hoy = reloj.HoyLocal;
        var versiones = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.Module == ColaDeMensajesAlDeudor.Modulo && t.Code == plantilla && t.Channel == canal && t.Purpose == proposito && t.IsActive && !t.IsDeleted)
            .ToListAsync(ct);
        var vigente = versiones.Where(t => t.VigenteEn(hoy)).OrderByDescending(t => t.Version).FirstOrDefault();
        if (vigente is null) return Falla(ErroresDeMensajesAlDeudor.PlantillaInexistente(plantilla, request.Channel));

        var campana = MessageCampaign.Nueva(codigo, request.Name, request.Purpose, request.Channel, vigente.Id, CampanasDeCobranza.Serializar(request.Filters),
            request.ScheduledFor.UtcDateTime, usuario);
        db.MessageCampaigns.Add(campana);
        await db.SaveChangesAsync(ct);
        return Result.Success((await CampanasDeCobranza.DtosAsync(db, [campana], reloj.AhoraLocal.Offset, ct))[0]);
    }

    /// <summary>
    /// Los criterios que no pueden casar con nada (K4, bloque D; <c>Lending.Campaign.FiltersInvalid</c>): una etapa que no existe en la versión vigente
    /// el día programado, una oficina, una línea o una pagaduría que no existen, o una mora mínima por encima del tope de la etapa elegida.
    /// </summary>
    private async Task<Error?> CriteriosInvalidosAsync(CampaignFilters f, DateOnly dia, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(f.StageCode))
        {
            var codigo = f.StageCode.Trim().ToUpperInvariant();
            var etapa = (await CobranzaDeCartera.EtapasVigentesAsync(db, dia, ct)).FirstOrDefault(e => e.Code == codigo);
            if (etapa is null)
                return ErroresDeCobranza.CriteriosInvalidos($"La etapa {codigo} no existe en las etapas de cobranza vigentes el {ErroresDeCobranza.Fecha(dia)}.");
            if (f.MinDaysPastDue is { } minimo && etapa.MaxDaysPastDue is { } tope && minimo > tope)
                return ErroresDeCobranza.CriteriosInvalidos($"La etapa {codigo} llega hasta {tope} días de mora y la campaña pide al menos {minimo}: "
                    + "ningún crédito cumpliría los dos criterios. Quite uno o ajústelo.");
        }
        if (f.OfficePublicId is { } oficina && !await db.Branches.AsNoTracking().AnyAsync(b => b.PublicId == oficina, ct))
            return ErroresDeCobranza.CriteriosInvalidos("La oficina elegida no existe. Elíjala de la lista.");
        if (f.CreditLinePublicId is { } linea && !await db.CreditLines.AsNoTracking().AnyAsync(l => l.PublicId == linea, ct))
            return ErroresDeCobranza.CriteriosInvalidos("La línea de crédito elegida no existe. Elíjala de la lista.");
        if (f.PaymasterPersonPublicId is { } pagaduria && !await db.People.AsNoTracking().AnyAsync(p => p.PublicId == pagaduria && !p.IsDeleted, ct))
            return ErroresDeCobranza.CriteriosInvalidos("La pagaduría elegida no existe en el maestro de personas.");
        return null;
    }

    private static Result<MessageCampaignDto> Falla(Error e) => Result.Failure<MessageCampaignDto>(e);
}

// ------------------------------------------------------------------------------------------------------------ vista previa --

/// <summary>
/// La vista previa de una campaña (feature 013, K4, T0949; api.md §14.9, <c>POST /api/lending/campaigns/{id}/preview</c>, <c>Lending.Campaigns.Manage</c>, sin
/// clave): los créditos del alcance que casan con los criterios y, para su deudor, lo que diría la ventana legal en el instante programado: destinatario
/// (sale o se difiere) o excluido con el motivo (<c>ChannelNotAuthorized</c>, <c>Deceased</c>, <c>Insolvency</c>, <c>CollectionSuspended</c>…). No escribe
/// nada. (nuevo)
/// </summary>
public sealed record PreviewMessageCampaignQuery(Guid CampaignPublicId) : IRequest<Result<CampaignPreviewDto>>;

public sealed class PreviewMessageCampaignQueryValidator : AbstractValidator<PreviewMessageCampaignQuery>
{
    public PreviewMessageCampaignQueryValidator() => RuleFor(x => x.CampaignPublicId).NotEmpty();
}

public sealed class PreviewMessageCampaignQueryHandler(IApplicationDbContext db, ColaDeMensajesAlDeudor cola, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<PreviewMessageCampaignQuery, Result<CampaignPreviewDto>>
{
    public async Task<Result<CampaignPreviewDto>> Handle(PreviewMessageCampaignQuery request, CancellationToken ct)
    {
        var campana = await db.MessageCampaigns.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.CampaignPublicId && !c.IsDeleted, ct);
        if (campana is null) return Result.Failure<CampaignPreviewDto>(ErroresDeCobranza.CampanaInexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var seleccion = await CampanasDeCobranza.SeleccionAsync(db, alcance, CampanasDeCobranza.Leer(campana.SelectionCriteriaJson), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, seleccion.Select(s => s.Credito.DebtorPersonId), ct);
        var destinatarios = new List<CampaignRecipientDto>();
        var excluidos = new List<CampaignExclusionDto>();
        foreach (var (c, _) in seleccion)
        {
            var ventana = await cola.EvaluarAsync(new ConsultaDeVentana(c.DebtorPersonId, c.Id, campana.Channel, campana.Purpose, campana.ScheduledFor, false), ct);
            if (ventana.IsFailure) return Result.Failure<CampaignPreviewDto>(ventana.Error);
            var credito = new CreditoRefDto(c.PublicId, c.Number);
            if (ventana.Value.Decision is Excluido x)
                excluidos.Add(new CampaignExclusionDto(credito, personas[c.DebtorPersonId], CampanasDeCobranza.Motivo(x.Motivo)));
            else
                destinatarios.Add(new CampaignRecipientDto(credito, personas[c.DebtorPersonId], ventana.Value.Decision is Permitido ? "Permitido" : "Diferido",
                    ventana.Value.PermitidoDesde));
        }
        return Result.Success(new CampaignPreviewDto(destinatarios, excluidos));
    }
}

// ------------------------------------------------------------------------------------------------------------- programar --

/// <summary>
/// Programa una campaña en borrador (feature 013, K4, T0949; api.md §14.9, <c>POST /api/lending/campaigns/{id}/schedule</c>, <c>Lending.Campaigns.Manage</c>,
/// con clave; US12-4): un mensaje al deudor de cada crédito seleccionado, con la campaña, en el instante programado, por <see cref="ColaDeMensajesAlDeudor"/>
/// —que evalúa la ventana por destinatario: nace <c>Queued</c>, <c>Deferred</c> con el instante nuevo, o <c>Excluded</c> con su motivo (sin autorización de
/// WhatsApp, <c>ChannelNotAuthorized</c>), que es la constancia—. La campaña pasa a <c>Scheduled</c> con los conteos (<c>Lending.Campaign.InvalidStatus</c> si
/// no está en borrador). Un deudor con dos créditos recibe un mensaje por crédito. (nuevo)
/// </summary>
public sealed record ScheduleMessageCampaignCommand(Guid CampaignPublicId, string? Reason = null) : IRequest<Result<MessageCampaignDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ScheduleMessageCampaignCommandValidator : AbstractValidator<ScheduleMessageCampaignCommand>
{
    public ScheduleMessageCampaignCommandValidator() => RuleFor(x => x.CampaignPublicId).NotEmpty();
}

public sealed class ScheduleMessageCampaignCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ScheduleMessageCampaignCommand, Result<MessageCampaignDto>>
{
    public async Task<Result<MessageCampaignDto>> Handle(ScheduleMessageCampaignCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var campana = await db.MessageCampaigns.FirstOrDefaultAsync(c => c.PublicId == request.CampaignPublicId && !c.IsDeleted, ct);
        if (campana is null) return Falla(ErroresDeCobranza.CampanaInexistente());
        if (campana.Status != MessageCampaignStatus.Draft)
            return Falla(ErroresDeGobierno.De(new ProblemaDeCartera(MessageCampaign.CodigoEstadoInvalido,
                $"La campaña está en «{campana.Status}» y no puede pasar a «{MessageCampaignStatus.Scheduled}».",
                new Dictionary<string, object?> { ["status"] = campana.Status.ToString(), ["requested"] = nameof(MessageCampaignStatus.Scheduled) })));
        var plantilla = await db.NotificationTemplates.AsNoTracking().Where(t => t.Id == campana.TemplateId).Select(t => t.Code).FirstAsync(ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var seleccion = await CampanasDeCobranza.SeleccionAsync(db, alcance, CampanasDeCobranza.Leer(campana.SelectionCriteriaJson), ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            int encolados = 0, excluidos = 0;
            foreach (var (c, posicion) in seleccion)
            {
                var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(c.DebtorPersonId, c.Id, campana.Purpose, campana.Channel, plantilla,
                    CampanasDeCobranza.Origen, $"{campana.PublicId:N}:{c.PublicId:N}", ActorKind.Person, usuario)
                {
                    OriginPublicId = campana.PublicId,
                    ProgramadoPara = campana.ScheduledFor,
                    CampaignId = campana.Id,
                    Valores = CampanasDeCobranza.Variables(posicion),
                }, ct);
                if (r.IsFailure) return Falla(r.Error);
                if (r.Value.Evento.Kind == DebtorMessageEventKind.Excluded) excluidos++;
                else encolados++;
            }
            campana.Programar(usuario, encolados, excluidos);
            await db.SaveChangesAsync(ct);
            return Result.Success((await CampanasDeCobranza.DtosAsync(db, [campana], reloj.AhoraLocal.Offset, ct))[0]);
        }, ct);
    }

    private static Result<MessageCampaignDto> Falla(Error e) => Result.Failure<MessageCampaignDto>(e);
}

// ---------------------------------------------------------------------------------------------------------------- cancelar --

/// <summary>
/// Cancela una campaña (feature 013, K4, T0949; api.md §14.9, <c>POST /api/lending/campaigns/{id}/cancel</c>, <c>Lending.Campaigns.Manage</c>, con clave y
/// motivo): en borrador, programada o corriendo; sus mensajes que siguen en cola (<c>Queued</c> o <c>Deferred</c>) pasan a <c>Cancelled</c> con el motivo;
/// lo que ya salió no se toca (<c>Lending.Campaign.InvalidStatus</c> si ya terminó). (nuevo)
/// </summary>
public sealed record CancelMessageCampaignCommand(Guid CampaignPublicId, string Reason) : IRequest<Result<MessageCampaignDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelMessageCampaignCommandValidator : ValidadorConMotivo<CancelMessageCampaignCommand>
{
    public CancelMessageCampaignCommandValidator() => RuleFor(x => x.CampaignPublicId).NotEmpty();
}

public sealed class CancelMessageCampaignCommandHandler(IApplicationDbContext db, ColaDeMensajesAlDeudor cola, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<CancelMessageCampaignCommand, Result<MessageCampaignDto>>
{
    public async Task<Result<MessageCampaignDto>> Handle(CancelMessageCampaignCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var campana = await db.MessageCampaigns.FirstOrDefaultAsync(c => c.PublicId == request.CampaignPublicId && !c.IsDeleted, ct);
        if (campana is null) return Falla(ErroresDeCobranza.CampanaInexistente());
        try
        {
            campana.Cancelar(usuario, request.Reason, reloj.UtcNow);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        var mensajes = await db.DebtorMessages.Where(m => m.CampaignId == campana.Id).ToListAsync(ct);
        foreach (var m in mensajes)
        {
            var ultimo = await cola.UltimoEventoAsync(m, ct);
            if (ultimo?.Kind is not (DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred)) continue;
            await cola.RegistrarAsync(m, new Domain.Lending.Collections.EventoDeMensaje(DebtorMessageEventKind.Cancelled, reloj.UtcNow, ActorKind.Person, usuario)
            {
                CodigoDeMotivo = CampanasDeCobranza.MotivoCampanaCancelada,
                TextoDelMotivo = request.Reason.Trim(),
            }, ct);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await CampanasDeCobranza.DtosAsync(db, [campana], reloj.AhoraLocal.Offset, ct))[0]);
    }

    private static Result<MessageCampaignDto> Falla(Error e) => Result.Failure<MessageCampaignDto>(e);
}

// ------------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// Las campañas (feature 013, K4, T0949; api.md §14.9, <c>GET /api/lending/campaigns?status=</c>, <c>Lending.Campaigns.Manage</c>): con sus conteos por el
/// estado vigente de sus mensajes (en cola, diferidos, excluidos, enviados, fallidos, cancelados); la más reciente primero. (nuevo)
/// </summary>
public sealed record ListMessageCampaignsQuery(MessageCampaignStatus? Status = null) : IRequest<Result<IReadOnlyList<MessageCampaignDto>>>;

public sealed class ListMessageCampaignsQueryValidator : AbstractValidator<ListMessageCampaignsQuery>
{
    public ListMessageCampaignsQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListMessageCampaignsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListMessageCampaignsQuery, Result<IReadOnlyList<MessageCampaignDto>>>
{
    public async Task<Result<IReadOnlyList<MessageCampaignDto>>> Handle(ListMessageCampaignsQuery request, CancellationToken ct)
    {
        var consulta = db.MessageCampaigns.AsNoTracking().Where(c => !c.IsDeleted);
        if (request.Status is { } estado) consulta = consulta.Where(c => c.Status == estado);
        var campanas = await consulta.OrderByDescending(c => c.Id).ToListAsync(ct);
        return Result.Success(await CampanasDeCobranza.DtosAsync(db, campanas, reloj.AhoraLocal.Offset, ct));
    }
}
