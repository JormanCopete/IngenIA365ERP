using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los créditos (feature 013, US3, T0457; contracts/api.md §8.4, FR-048, C11): por ahora sólo anular un crédito pendiente de giro
/// (<c>Lending.Disbursements.Void</c>, con motivo e <c>Idempotency-Key</c>), que reversa en la misma transacción las recogidas del desembolso
/// por el contrato. US4 suma las consultas del crédito (§8.1). Fuera del alcance, el 404 del crédito. (nuevo)
/// </summary>
public class LoansEndpoints : ICarterModule
{
    public const string AnularCredito = "Lending.Disbursements.Void";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var creditos = LendingGroup.Grupo(app, "/loans", "Cartera Créditos");

        creditos.MapPost("/{id:guid}/void", async (Guid id, AnulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidPendingLoanCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Loans_Void")
            .ConClaveDeOperacion()
            .RequirePermission(AnularCredito);
    }

    /// <summary>El motivo de la anulación.</summary>
    public sealed record AnulacionRequest(string? Reason);
}
