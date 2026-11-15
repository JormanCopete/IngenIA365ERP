using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Billing;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// La facturación de servicios (feature 020, US6; R10; contracts/api.md §3): vista previa, factura individual, corrida en lote por tandas
/// (<c>POST /billing/runs</c> la abre y <c>POST /billing/runs/{id}/next</c> la avanza) y los cargos del período.
/// </summary>
public sealed class BillingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, "/billing", "Transporte Facturación");

        g.MapGet("/preview", async (string period, ISender s, CancellationToken ct) => await s.Send(new PreviewServiceBillingQuery(period), ct))
            .WithName("Transport_Billing_Preview").RequirePermission(TransportPermissions.BillingView);

        g.MapPost("/single", async (IndividualRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RunSingleServiceBillingCommand(b.VehiclePublicId ?? Guid.Empty, b.Period ?? "", b.PaymentMode) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Transport_Billing_Single").ConClaveDeOperacion().RequirePermission(TransportPermissions.BillingRun);

        g.MapPost("/runs", async (CorridaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new StartServiceBillingRunCommand(b.Period ?? "", b.PaymentMode) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Transport_Billing_StartRun").ConClaveDeOperacion().RequirePermission(TransportPermissions.BillingRun);

        g.MapPost("/runs/{id:guid}/next", async (Guid id, int? batchSize, ISender s, CancellationToken ct) =>
                await s.Send(new ProcessServiceBillingRunCommand(id, batchSize ?? 20), ct))
            .WithName("Transport_Billing_NextBatch").RequirePermission(TransportPermissions.BillingRun);

        g.MapGet("/runs", async (string? period, ISender s, CancellationToken ct) => await s.Send(new GetBillingRunsQuery(period), ct))
            .WithName("Transport_Billing_Runs").RequirePermission(TransportPermissions.BillingView);

        g.MapGet("/runs/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetBillingRunQuery(id), ct))
            .WithName("Transport_Billing_Run").RequirePermission(TransportPermissions.BillingView);

        var c = TransportGroup.Grupo(app, "/charges", "Transporte Facturación");
        c.MapGet("/", async (Guid? vehicle, string? period, ChargeStatus? status, ISender s, CancellationToken ct) =>
                await s.Send(new ListServiceChargesQuery(vehicle, period, status), ct))
            .WithName("Transport_Charges_List").RequirePermission(TransportPermissions.BillingView);
    }

    public sealed record IndividualRequest(Guid? VehiclePublicId, string? Period, BillingPaymentMode PaymentMode);

    public sealed record CorridaRequest(string? Period, BillingPaymentMode PaymentMode);
}
