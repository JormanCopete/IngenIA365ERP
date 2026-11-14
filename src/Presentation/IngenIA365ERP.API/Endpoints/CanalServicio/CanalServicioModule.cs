using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Security.CanalServicio;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.ContactUpdates;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CanalServicio;

/// <summary>
/// Feature 021 (contracts/erp-api-de-servicio.md): la API de servicio que consume el canal digital del asociado. Sólo acepta el
/// token de servicio del canal (<see cref="PeticionDelCanal.Politica"/>); cada ruta reenvía a una consulta o un comando MediatR
/// (Principio III). Las rutas de cooperativa van en <see cref="Cooperativa"/>, que exige el canal encendido.
/// </summary>
public class CanalServicioModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var plataforma = Grupo(app, PeticionDelCanal.Prefijo);
        plataforma.MapGet("/tenants", async (ISender s, CancellationToken ct) => await s.Send(new ListChannelTenantsQuery(), ct))
            .WithName("Canal_Tenants_List");

        var cooperativa = Cooperativa(app);
        // R8: los cambios de habilitación desde el cursor, y la conciliación de la política que el canal pide antes de arrastrar.
        cooperativa.MapGet("/enablements", async (long? since, int? limit, ISender s, CancellationToken ct) =>
                await s.Send(new ListEnablementChangesQuery(since ?? 0, limit ?? 500), ct))
            .WithName("Canal_Enablements_Changes");
        cooperativa.MapPost("/enablements/reconcile", async (ISender s, CancellationToken ct) =>
                await s.Send(new ReconcileChannelEnablementsCommand(), ct))
            .WithName("Canal_Enablements_Reconcile");

        cooperativa.MapGet("/legal-texts", async (ISender s, CancellationToken ct) => await s.Send(new GetChannelLegalTextsForServiceQuery(), ct))
            .WithName("Canal_LegalTexts");

        // US2 (T059, T060): contacto de la entidad validadora para enviar el código, y novedades de contacto del asociado.
        cooperativa.MapGet("/people/{personPublicId:guid}/activation-contact", async (Guid personPublicId, ISender s, CancellationToken ct) =>
                await s.Send(new GetActivationContactQuery(personPublicId), ct))
            .WithName("Canal_People_ActivationContact");
        cooperativa.MapPost("/people/{personPublicId:guid}/contact-updates", async (Guid personPublicId, NovedadDeContactoRequest body, ISender s,
                CancellationToken ct) =>
                await s.Send(new RegisterContactUpdateCommand(personPublicId, body.Field, body.NewValue, body.ReportedAt), ct))
            .WithName("Canal_People_ContactUpdates_Register");

        // US3 (T080): los conceptos publicados de la persona y la simulación de un pago. La habilitación se verifica en vivo en cada consulta.
        cooperativa.MapGet("/people/{personPublicId:guid}/payables", async (Guid personPublicId, DateOnly? date, ISender s, CancellationToken ct) =>
                await s.Send(new GetChannelPayablesQuery(personPublicId, date), ct))
            .WithName("Canal_People_Payables");
        cooperativa.MapPost("/people/{personPublicId:guid}/payables/simulate", async (Guid personPublicId, SimulacionRequest body, ISender s,
                CancellationToken ct) =>
                await s.Send(new SimulateChannelPaymentQuery(personPublicId, body.ConceptKey, body.Modality, body.Amount, body.PrepaymentChoice), ct))
            .WithName("Canal_People_Payables_Simulate");

        // US5 (T100–T103): el aviso de un pago aprobado y el comprobante. El aviso responde aunque el canal esté apagado: el pago ya se
        // cobró (FR-001). La persona va en la ruta para que el actor sea el asociado que pagó (research R3); la clave de operación sale
        // de la referencia, así el reintento del canal es la misma operación (FR-044).
        AunApagado(cooperativa.MapPost("/people/{personPublicId:guid}/payment-notices", async (Guid personPublicId, AvisoDePagoRequest body,
                ISender s, CancellationToken ct) =>
                await s.Send(new RegisterChannelPaymentCommand(personPublicId, body.Reference, body.DestinationPublicId, body.Total, body.Currency,
                    body.ApprovedAt, body.Gateway, body.Items, body.Payer)
                {
                    OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(body.Reference ?? string.Empty),
                }, ct))
            .WithName("Canal_People_PaymentNotices_Register"));
        AunApagado(cooperativa.MapGet("/people/{personPublicId:guid}/receipts/{receiptPublicId:guid}/document", async (Guid personPublicId,
                Guid receiptPublicId, ISender s, Application.Receipts.Sessions.IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetChannelReceiptDocumentQuery(personPublicId, receiptPublicId), ct);
                return r.IsSuccess ? Results.File(pdf.Recibo(r.Value), "application/pdf", $"comprobante-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Canal_People_Receipts_Document"));
        // US13 (T142; C6): crédito en línea. La solicitud queda en borrador con canal de origen; la clave la manda el canal (Idempotency-Key del asociado).
        cooperativa.MapGet("/credit-lines/published", async (ISender s, CancellationToken ct) => await s.Send(new ListPublishedCreditLinesQuery(), ct))
            .WithName("Canal_CreditLines_Published");
        cooperativa.MapPost("/people/{personPublicId:guid}/credit-simulations", async (Guid personPublicId, SimulacionDeCreditoRequest body, ISender s,
                CancellationToken ct) => await s.Send(new SimulateChannelCreditQuery(personPublicId, body.CreditLinePublicId, body.Amount, body.Installments), ct))
            .WithName("Canal_People_CreditSimulations");
        cooperativa.MapPost("/people/{personPublicId:guid}/credit-applications", async (Guid personPublicId, SolicitudDeCreditoRequest body, ISender s,
                CancellationToken ct) => await s.Send(new CreateChannelCreditApplicationCommand(personPublicId, body.CreditLinePublicId, body.Amount, body.Installments,
                    body.Purpose, body.MonthlyIncome, body.MonthlyExpenses) { OperationKey = body.RequestKey }, ct))
            .WithName("Canal_People_CreditApplications_Create");
        cooperativa.MapGet("/people/{personPublicId:guid}/credit-applications", async (Guid personPublicId, ISender s, CancellationToken ct) =>
                await s.Send(new ListChannelCreditApplicationsQuery(personPublicId), ct))
            .WithName("Canal_People_CreditApplications_List");
        // US10 (T133; FR-012, FR-056): las cuotas que vencen en N días, para el aviso de cuota próxima.
        cooperativa.MapGet("/due-installments", async (int? inDays, ISender s, CancellationToken ct) => await s.Send(new ListDueInstallmentsQuery(inDays ?? 0), ct))
            .WithName("Canal_DueInstallments");
        // US9 (T124; FR-055): los extractos y certificados publicados y su PDF, emitido en el momento por Cartera.
        cooperativa.MapGet("/people/{personPublicId:guid}/documents", async (Guid personPublicId, ISender s, CancellationToken ct) =>
                await s.Send(new ListChannelDocumentsQuery(personPublicId), ct))
            .WithName("Canal_People_Documents_List");
        cooperativa.MapPost("/people/{personPublicId:guid}/documents", async (Guid personPublicId, DocumentoRequest body, ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(new IssueChannelDocumentCommand(personPublicId, body.Kind ?? string.Empty, body.ProductPublicId, body.Year, body.From, body.To), ct);
                return r.IsSuccess ? Results.File(r.Value.Content, "application/pdf", r.Value.FileName) : (object)r;
            })
            .WithName("Canal_People_Documents_Issue");
    }

    /// <summary>El grupo base: token de servicio, sobre de error y sin documentación pública.</summary>
    internal static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string prefijo) =>
        app.MapGroup(prefijo)
            .WithTags("Canal digital (servicio)")
            .ExcludeFromDescription()
            .RequireAuthorization(PeticionDelCanal.Politica)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

    /// <summary>
    /// El grupo de una cooperativa (<c>/api/canal/v1/tenants/{tenantPublicId}</c>): además exige el canal encendido. Las rutas que
    /// deben seguir funcionando con el canal apagado (los avisos de intentos ya creados, FR-001) se registran con
    /// <see cref="AunApagado"/>.
    /// </summary>
    internal static RouteGroupBuilder Cooperativa(IEndpointRouteBuilder app) =>
        Grupo(app, $"{PeticionDelCanal.Prefijo}/tenants/{{{PeticionDelCanal.ParametroCooperativa}:guid}}")
            .AddEndpointFilter<CanalEncendidoFilter>();

    /// <summary>Marca una ruta de cooperativa que responde aunque el canal esté apagado.</summary>
    internal static RouteHandlerBuilder AunApagado(RouteHandlerBuilder ruta) => ruta.WithMetadata(new AunConCanalApagado());

    internal sealed class AunConCanalApagado;

    /// <summary>El cuerpo de la novedad de contacto; la persona va en la ruta.</summary>
    /// <summary>El cuerpo de la simulación; la persona va en la ruta.</summary>
    public sealed record SimulacionRequest(string ConceptKey, string Modality, decimal Amount, string? PrepaymentChoice);

    /// <summary>El cuerpo del aviso de pago (contracts/erp-api-de-servicio.md); la persona va en la ruta.</summary>
    public sealed record AvisoDePagoRequest(string Reference, Guid DestinationPublicId, decimal Total, string Currency, DateTimeOffset ApprovedAt,
        AprobacionDeLaPasarelaDto Gateway, IReadOnlyList<ItemDelAvisoDto> Items, PagadorDelAvisoDto? Payer);

    public sealed record SimulacionDeCreditoRequest(Guid CreditLinePublicId, decimal Amount, int Installments);

    public sealed record SolicitudDeCreditoRequest(Guid RequestKey, Guid CreditLinePublicId, decimal Amount, int Installments, string? Purpose, decimal? MonthlyIncome,
        decimal? MonthlyExpenses);

    public sealed record DocumentoRequest(string? Kind, Guid? ProductPublicId, short? Year, DateOnly? From, DateOnly? To);

    public sealed record NovedadDeContactoRequest(Domain.Enums.Channel.ContactField Field, string NewValue, DateTime ReportedAt);
}

/// <summary>Responde 409 <c>Channel.Disabled</c> si la cooperativa apagó el canal (FR-001).</summary>
internal sealed class CanalEncendidoFilter(IApplicationDbContext db, IDateTimeService reloj) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<CanalServicioModule.AunConCanalApagado>() is not null)
            return await next(context);

        if (!await ConfiguracionDelCanal.EncendidoAsync(db, reloj.UtcNow, http.RequestAborted))
        {
            return Results.Json(new
            {
                code = ConfiguracionDelCanal.Errores.Apagado,
                errorCode = ConfiguracionDelCanal.Errores.Apagado,
                message = "La cooperativa no tiene encendido el canal digital.",
                traceId = http.TraceIdentifier,
            }, statusCode: StatusCodes.Status409Conflict);
        }
        return await next(context);
    }
}
