using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Channel.Concepts;
using IngenIA365ERP.Application.Channel.ContactUpdates;
using IngenIA365ERP.Application.Channel.Destinations;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Payments;
using IngenIA365ERP.Application.Channel.Settings;
using IngenIA365ERP.Application.Channel.Support;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Channel;

/// <summary>
/// Feature 021 (T049; contracts/erp-administracion.md): la administración del canal digital por los usuarios de la cooperativa. Cada
/// ruta reenvía a su comando o consulta (Principio III) y exige su permiso; las escrituras llevan clave de operación.
/// </summary>
public class ChannelAdminModule : ICarterModule
{
    public const string VerConfiguracion = "Channel.Settings.View";
    public const string AdministrarConfiguracion = "Channel.Settings.Manage";
    public const string AdministrarDestinos = "Channel.Destinations.Manage";
    public const string VerHabilitaciones = "Channel.Enablements.View";
    public const string AdministrarHabilitaciones = "Channel.Enablements.Manage";
    public const string ReversarPagos = "Channel.Payments.Reverse";
    public const string VerAtencion = "Channel.Support.View";
    public const string AdministrarAtencion = "Channel.Support.Manage";
    public const string VerIndicadores = "Channel.Metrics.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/channel")
            .WithTags("Canal digital")
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Channel)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        // Configuración.
        g.MapGet("/settings", async (ISender s, CancellationToken ct) => await s.Send(new GetChannelSettingsQuery(), ct))
            .WithName("Channel_Settings_Get").RequirePermission(new AlgunPermiso(VerConfiguracion, AdministrarConfiguracion));
        g.MapPut("/settings", async (UpdateChannelSettingsCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Settings_Update").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);
        g.MapPost("/settings/enable", async (HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new EnableChannelCommand { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Settings_Enable").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);
        g.MapPost("/settings/disable", async (HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DisableChannelCommand { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Settings_Disable").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);

        // Textos legales.
        g.MapGet("/legal-texts", async (LegalTextKind? kind, ISender s, CancellationToken ct) => await s.Send(new ListChannelLegalTextsQuery(kind), ct))
            .WithName("Channel_LegalTexts_List").RequirePermission(new AlgunPermiso(VerConfiguracion, AdministrarConfiguracion));
        g.MapPost("/legal-texts", async (PublishChannelLegalTextCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_LegalTexts_Publish").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);

        // Destinos de pago y credenciales de la pasarela.
        g.MapGet("/destinations", async (ISender s, CancellationToken ct) => await s.Send(new ListChannelDestinationsQuery(), ct))
            .WithName("Channel_Destinations_List").RequirePermission(new AlgunPermiso(VerConfiguracion, AdministrarDestinos));
        g.MapPost("/destinations", async (SaveChannelDestinationCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { PublicId = null, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Destinations_Create").ConClaveDeOperacion().RequirePermission(AdministrarDestinos);
        g.MapPut("/destinations/{id:guid}", async (Guid id, SaveChannelDestinationCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { PublicId = id, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Destinations_Update").ConClaveDeOperacion().RequirePermission(AdministrarDestinos);
        g.MapPut("/destinations/{id:guid}/credentials", async (Guid id, CredencialesRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetChannelGatewayCredentialsCommand(id, body.BaseUrl ?? string.Empty, body.Login ?? string.Empty, body.SecretKey ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Destinations_Credentials").ConClaveDeOperacion().RequirePermission(AdministrarDestinos);

        // Conceptos publicados.
        g.MapGet("/concepts", async (ISender s, CancellationToken ct) => await s.Send(new ListChannelConceptsQuery(), ct))
            .WithName("Channel_Concepts_List").RequirePermission(new AlgunPermiso(VerConfiguracion, AdministrarConfiguracion));
        g.MapPost("/concepts", async (SaveChannelConceptCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { PublicId = null, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Concepts_Create").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);
        g.MapPut("/concepts/{id:guid}", async (Guid id, SaveChannelConceptCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { PublicId = id, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Concepts_Update").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);
        g.MapDelete("/concepts/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UnpublishChannelConceptCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Concepts_Unpublish").ConClaveDeOperacion().RequirePermission(AdministrarConfiguracion);

        // Habilitaciones.
        var h = g.MapGroup("/enablements");
        h.MapGet("/", async (EnablementStatus? status, string? q, int? page, int? pageSize, ISender s, CancellationToken ct) =>
                await s.Send(new ListChannelEnablementsQuery(status, q, page ?? 1, pageSize ?? 50), ct))
            .WithName("Channel_Enablements_List").RequirePermission(new AlgunPermiso(VerHabilitaciones, AdministrarHabilitaciones));
        h.MapPost("/", async (PersonaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new EnableChannelPersonCommand(body.PersonPublicId ?? Guid.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Enablements_Enable").ConClaveDeOperacion().RequirePermission(AdministrarHabilitaciones);
        h.MapPost("/{personPublicId:guid}/disable", async (Guid personPublicId, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DisableChannelPersonCommand(personPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Enablements_Disable").ConClaveDeOperacion().RequirePermission(AdministrarHabilitaciones);
        h.MapPost("/{personPublicId:guid}/enable", async (Guid personPublicId, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new EnableChannelPersonCommand(personPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_Enablements_ReEnable").ConClaveDeOperacion().RequirePermission(AdministrarHabilitaciones);
        // GET /api/channel/enablements/template.xlsx y POST /api/channel/enablements/import?mode=review|apply.
        h.MapPlantillaDePlataforma(PlantillaDeHabilitaciones.Clave, "Channel_Enablements",
            (modo, archivo, motivo, clave) => new ImportChannelEnablementsCommand(modo, archivo, motivo) { OperationKey = clave });

        // Novedades de contacto (FR-024): la cooperativa las ve y decide si actualiza la persona en Personas.
        var verHabilitaciones = new AlgunPermiso(VerHabilitaciones, AdministrarHabilitaciones);
        var n = g.MapGroup("/contact-updates");
        n.MapGet("/", async (bool? pending, int? page, int? pageSize, ISender s, CancellationToken ct) =>
                await s.Send(new ListContactUpdatesQuery(pending, page ?? 1, pageSize ?? 50), ct))
            .WithName("Channel_ContactUpdates_List").RequirePermission(verHabilitaciones);
        n.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetContactUpdateQuery(id), ct))
            .WithName("Channel_ContactUpdates_Get").RequirePermission(verHabilitaciones);
        n.MapPost("/{id:guid}/acknowledge", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AcknowledgeContactUpdateCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Channel_ContactUpdates_Acknowledge").ConClaveDeOperacion().RequirePermission(verHabilitaciones);

        // Reverso de un pago del canal (FR-052): antes del corte por la pasarela, después como devolución por Tesorería. Idempotente por la referencia.
        g.MapPost("/payments/{reference}/reverse", async (string reference, ReversoRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new ReverseChannelPaymentCommand(reference, body.Reason ?? string.Empty, body.PersonBankAccountPublicId), ct))
            .WithName("Channel_Payments_Reverse").RequirePermission(ReversarPagos);

        // Atención (FR-058) e indicadores (FR-060): lo del canal, acotado por el canal a esta cooperativa (FR-059), junto a lo del ERP.
        g.MapGet("/support", async (string? documentType, string? document, string? reference, ISender s, CancellationToken ct) =>
                await s.Send(new SearchChannelSupportQuery(documentType, document, reference), ct))
            .WithName("Channel_Support_Search").RequirePermission(new AlgunPermiso(VerAtencion, AdministrarAtencion));
        g.MapPost("/support/accounts/{id:guid}/unlock", async (Guid id, ISender s, CancellationToken ct) =>
                await s.Send(new UnlockChannelAccountCommand(id), ct))
            .WithName("Channel_Support_Unlock").RequirePermission(AdministrarAtencion);
        g.MapPost("/support/accounts/{id:guid}/revoke-sessions", async (Guid id, ISender s, CancellationToken ct) =>
                await s.Send(new RevokeChannelSessionsCommand(id), ct))
            .WithName("Channel_Support_RevokeSessions").RequirePermission(AdministrarAtencion);
        g.MapGet("/metrics", async (DateOnly from, DateOnly to, ISender s, CancellationToken ct) => await s.Send(new GetChannelMetricsQuery(from, to), ct))
            .WithName("Channel_Metrics").RequirePermission(VerIndicadores);
    }

    /// <summary>El secreto viaja en el cuerpo y nunca se registra.</summary>
    public sealed record CredencialesRequest(string? BaseUrl, string? Login, string? SecretKey);

    public sealed record PersonaRequest(Guid? PersonPublicId);

    public sealed record MotivoRequest(string? Reason);

    /// <summary>El motivo del reverso y, para la devolución por Tesorería, la cuenta bancaria del asociado (opcional: Tesorería la pide al girar).</summary>
    public sealed record ReversoRequest(string? Reason, Guid? PersonBankAccountPublicId);
}
