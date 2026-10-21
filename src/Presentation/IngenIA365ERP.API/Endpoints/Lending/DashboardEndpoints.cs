using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El tablero de Cartera (feature 013, US15, K5, T0984; FR-135; contracts/api.md §17): <c>GET /api/lending/dashboard?branch=&amp;line=&amp;asOf=</c>
/// con <c>Lending.Dashboard.View</c>, dentro del grupo <c>/api/lending</c> (<see cref="LendingGroup.Grupo"/>: el intento denegado se audita y el
/// error lleva el sobre). Sólo lee: sin <c>Idempotency-Key</c>. Las cifras siempre son del alcance de quien consulta (<see cref="GetLendingDashboardQuery"/>)
/// y no traen datos del deudor. (nuevo)
/// </summary>
public class DashboardEndpoints : ICarterModule
{
    public const string Ver = "Lending.Dashboard.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var grupo = LendingGroup.Grupo(app, string.Empty, "Cartera Tablero");

        grupo.MapGet("/dashboard", async (Guid? branch, Guid? line, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingDashboardQuery(branch, line, asOf), ct))
            .WithName("Lending_Dashboard_Get")
            .RequirePermission(Ver);
    }
}
