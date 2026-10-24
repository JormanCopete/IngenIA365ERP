using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>La activación de Recaudo (feature 015, T0183b; api.md §14): existe desde el bloque 2 con la activación mínima; la fase 19 le suma el cuadre. (nuevo)</summary>
public class ReceiptsActivationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/activation", "Recaudo Activación");
        g.MapGet("/requirements", async (ISender s, CancellationToken ct) => await s.Send(new Application.Receipts.Activation.GetReceiptsActivationRequirementsQuery(), ct))
            .WithName("Receipts_Activation_Requirements").RequirePermission(new AlgunPermiso("Receipts.Activation.View", "Receipts.Activation.Activate"));
        g.MapPost("/", async (ActivacionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new Application.Receipts.Activation.ActivateReceiptsCommand(body?.CutoffDate) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Activation_Activate").ConClaveDeOperacion().RequirePermission("Receipts.Activation.Activate");
    }

    public sealed record ActivacionRequest(DateOnly? CutoffDate);
}
