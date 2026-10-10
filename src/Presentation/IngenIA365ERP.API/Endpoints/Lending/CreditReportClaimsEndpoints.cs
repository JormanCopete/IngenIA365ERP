using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los reclamos y suplantaciones sobre el reporte a centrales (feature 013, T0313, T69, FR-006, C88; contracts/api.md §14.4). En K1 sólo la
/// lectura de lo migrado, con <c>Lending.CreditReportClaims.Read</c> y el alcance por la oficina del crédito; registrar, prorrogar, responder y
/// desistir son de K4 y no tienen ruta todavía. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CreditReportClaimsEndpoints : ICarterModule
{
    public const string Leer = "Lending.CreditReportClaims.Read";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var reclamos = LendingGroup.Grupo(app, "/credit-report-claims", "Cartera Reclamos");

        reclamos.MapGet("/", async ([AsParameters] ListCreditReportClaimsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_CreditReportClaims_List")
            .RequirePermission(Leer);
    }
}
