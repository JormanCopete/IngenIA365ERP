using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Imports;
using IngenIA365ERP.Application.Transport.Services;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Servicios por vehículo, suscripciones y políticas de facturación (feature 020, US6; contracts/api.md §3, «Servicios, suscripciones y
/// facturación»).
/// </summary>
public sealed class ServicesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var s = TransportGroup.Grupo(app, "/services", "Transporte Servicios");
        s.MapGet("/", async (bool? includeInactive, ISender x, CancellationToken ct) => await x.Send(new ListTransportServicesQuery(includeInactive ?? false), ct))
            .WithName("Transport_Services_List").RequirePermission(new AlgunPermiso(TransportPermissions.ServicesView, TransportPermissions.ServiceSetupManage));
        s.MapPost("/", async (TransportServiceInput b, ISender x, CancellationToken ct) =>
                TransportGroup.Creado(await x.Send(new CreateTransportServiceCommand(b), ct), d => $"/api/transport/services/{d.PublicId}"))
            .WithName("Transport_Services_Create").RequirePermission(TransportPermissions.ServiceSetupManage);
        s.MapPut("/{id:guid}", async (Guid id, TransportServiceInput b, ISender x, CancellationToken ct) => await x.Send(new UpdateTransportServiceCommand(id, b), ct))
            .WithName("Transport_Services_Update").RequirePermission(TransportPermissions.ServiceSetupManage);

        var p = TransportGroup.Grupo(app, "/billing-policies", "Transporte Servicios");
        p.MapGet("/", async (ISender x, CancellationToken ct) => await x.Send(new ListBillingPoliciesQuery(), ct))
            .WithName("Transport_BillingPolicies_List").RequirePermission(new AlgunPermiso(TransportPermissions.ServiceSetupManage, TransportPermissions.BillingView));
        p.MapPost("/", async (PoliticaRequest b, ISender x, CancellationToken ct) =>
                await x.Send(new SetBillingPolicyCommand(b.ValidFrom, b.Proration, b.SalesDocumentTypeCode ?? "", b.WarehouseCode, b.SalesPointCode, b.DueDay), ct))
            .WithName("Transport_BillingPolicies_Set").RequirePermission(TransportPermissions.ServiceSetupManage);

        var v = TransportGroup.Grupo(app, "/vehicles/{vehicleId:guid}/subscriptions", "Transporte Servicios");
        v.MapPost("/", async (Guid vehicleId, SuscripcionRequest b, ISender x, CancellationToken ct) =>
                TransportGroup.Creado(await x.Send(new SubscribeVehicleCommand(vehicleId, b.ServicePublicId ?? Guid.Empty, b.From, b.To, b.Price, b.Reason), ct),
                    id => $"/api/transport/vehicles/{vehicleId}/subscriptions/{id}"))
            .WithName("Transport_Subscriptions_Create").RequirePermission(TransportPermissions.ServicesManage);
        v.MapPost("/{sId:guid}/end", async (Guid vehicleId, Guid sId, TerminarRequest b, ISender x, CancellationToken ct) =>
                await x.Send(new EndSubscriptionCommand(vehicleId, sId, b.To, b.Reason), ct))
            .WithName("Transport_Subscriptions_End").RequirePermission(TransportPermissions.ServicesManage);
        v.MapPost("/{sId:guid}/prices", async (Guid vehicleId, Guid sId, PrecioRequest b, ISender x, CancellationToken ct) =>
                await x.Send(new SetSubscriptionPriceCommand(vehicleId, sId, b.Price, b.From), ct))
            .WithName("Transport_Subscriptions_Price").RequirePermission(TransportPermissions.ServicesManage);

        var plantilla = TransportGroup.Grupo(app, "/subscriptions", "Transporte Servicios");
        plantilla.MapPlantillaDePlataformaConExistentes(PlantillaDeSuscripciones.Clave, "Transport_Subscriptions",
            (modo, archivo, motivo, existentes, clave) => new ImportSubscriptionsCommand(modo, archivo, motivo, existentes) { OperationKey = clave });
    }

    public sealed record PoliticaRequest(DateOnly ValidFrom, ProrationPolicy Proration, string? SalesDocumentTypeCode, string? WarehouseCode, string? SalesPointCode, short DueDay);

    public sealed record SuscripcionRequest(Guid? ServicePublicId, DateOnly From, DateOnly? To, decimal? Price, string? Reason);

    public sealed record TerminarRequest(DateOnly To, string? Reason);

    public sealed record PrecioRequest(decimal Price, DateOnly From);
}
