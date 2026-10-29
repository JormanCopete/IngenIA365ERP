using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>El tablero de caja de Recaudo (feature 015, R1, T0401; FR-155): <c>GET /api/receipts/dashboard?date=</c>, cifras sin datos personales. (nuevo)</summary>
public class DashboardEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/dashboard", "Recaudo Tablero");
        g.MapGet("/", async (DateOnly? date, ISender s, CancellationToken ct) => await s.Send(new GetReceiptsDashboardQuery(date), ct))
            .WithName("Receipts_Dashboard_Get").RequirePermission(Reports.ReceiptsReportsEndpoints.PermisoDeVer);
    }
}
