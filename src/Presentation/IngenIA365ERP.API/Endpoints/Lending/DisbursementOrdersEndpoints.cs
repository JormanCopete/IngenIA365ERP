using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las órdenes de giro (feature 013, US3, T0457; contracts/api.md §9.2, FR-045, FR-048, C12): la lista con la cuenta enmascarada y el detalle
/// con la cuenta completa (<c>Lending.Loans.Read</c>), pedir la anulación a Tesorería (<c>Lending.DisbursementOrders.Void</c>; la orden sigue
/// <c>Issued</c> hasta que Tesorería responda) y el giro nuevo de un crédito pendiente de giro (<c>Lending.Disbursements.Execute</c>, con monto
/// operativo). Las escrituras llevan <c>Idempotency-Key</c>. El resultado del giro no tiene ruta: llega de Tesorería por la bandeja. Fuera del
/// alcance, el 404 de la orden. (nuevo)
/// </summary>
public class DisbursementOrdersEndpoints : ICarterModule
{
    public const string AnularOrden = "Lending.DisbursementOrders.Void";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var ordenes = LendingGroup.Grupo(app, "/disbursement-orders", "Cartera Órdenes de giro");

        ordenes.MapGet("/", async (DisbursementOrderStatus? status, Guid? loanPublicId, DateOnly? from, DateOnly? to, int? withoutResultDays, int? page,
                int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDisbursementOrdersQuery(status, loanPublicId, from, to, withoutResultDays, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_DisbursementOrders_List")
            .RequirePermission(DisbursementsEndpoints.LeerCreditos);

        ordenes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetDisbursementOrderQuery(id), ct))
            .WithName("Lending_DisbursementOrders_Get")
            .RequirePermission(DisbursementsEndpoints.LeerCreditos);

        ordenes.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidDisbursementOrderCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_DisbursementOrders_Void")
            .ConClaveDeOperacion()
            .RequirePermission(AnularOrden);

        ordenes.MapPost("/{id:guid}/reissue", async (Guid id, GiroNuevoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ReissueDisbursementOrderCommand(id, body.Form, body.Beneficiary, body.Reason ?? string.Empty,
                    body.PaidByFile)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), o => $"/api/lending/disbursement-orders/{o.PublicId}"))
            .WithName("Lending_DisbursementOrders_Reissue")
            .ConClaveDeOperacion()
            .RequirePermission(DisbursementsEndpoints.Desembolsar);
    }

    /// <summary>El motivo de una anulación.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El giro nuevo (§9.2): la forma, el beneficiario con su cuenta (nulo = el deudor) y el motivo.</summary>
    public sealed record GiroNuevoRequest(DisbursementForm Form, BeneficiaryInput? Beneficiary, string? Reason, bool PaidByFile = false);
}
