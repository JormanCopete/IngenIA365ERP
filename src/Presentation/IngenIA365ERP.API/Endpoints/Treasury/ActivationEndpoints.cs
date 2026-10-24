using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Activation;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>La activación de Tesorería (feature 015, T0183b; api.md §14): existe desde el bloque 2 con la activación mínima; la fase 19 le suma el cuadre. (nuevo)</summary>
public class TreasuryActivationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/activation", "Tesorería Activación");
        g.MapGet("/requirements", async (ISender s, CancellationToken ct) => await s.Send(new GetTreasuryActivationRequirementsQuery(), ct))
            .WithName("Treasury_Activation_Requirements").RequirePermission(new AlgunPermiso("Treasury.Activation.View", "Treasury.Activation.Activate"));
        g.MapPost("/", async (ActivacionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ActivateTreasuryCommand(body?.CutoffDate) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Activation_Activate").ConClaveDeOperacion().RequirePermission("Treasury.Activation.Activate");
    }

    public sealed record ActivacionRequest(DateOnly? CutoffDate);
}
