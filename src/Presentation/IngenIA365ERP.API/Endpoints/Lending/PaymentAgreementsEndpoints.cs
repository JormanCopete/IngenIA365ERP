using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Agreements;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los acuerdos de pago sobre lo vencido (feature 013, K4, US12, T0956; contracts/api.md §14.7, FR-120): la lista (<c>Lending.Collections.Read</c>),
/// proponer y cancelar (<c>Lending.PaymentAgreements.Create</c>) y aprobar o rechazar con otra persona distinta de quien lo propuso
/// (<c>Lending.PaymentAgreements.Approve</c>). Toda escritura lleva <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PaymentAgreementsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Crear = "Lending.PaymentAgreements.Create";
    public const string Aprobar = "Lending.PaymentAgreements.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var acuerdos = LendingGroup.Grupo(app, "/payment-agreements", "Cartera Acuerdos de pago");

        acuerdos.MapGet("/", async ([AsParameters] ListPaymentAgreementsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_PaymentAgreements_List")
            .RequirePermission(Leer);

        acuerdos.MapPost("/", async (AcuerdoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreatePaymentAgreementCommand(body.LoanPublicId, body.Installments ?? [], body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), a => $"/api/lending/payment-agreements/{a.PublicId}"))
            .WithName("Lending_PaymentAgreements_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        acuerdos.MapPost("/{id:guid}/approve", async (Guid id, MotivoRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApprovePaymentAgreementCommand(id, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_PaymentAgreements_Approve")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        acuerdos.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RejectPaymentAgreementCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_PaymentAgreements_Reject")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        acuerdos.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelPaymentAgreementCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_PaymentAgreements_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);
    }

    /// <summary>El cuerpo de un acuerdo: cuotas sobre lo vencido y el motivo (§14.7).</summary>
    public sealed record AcuerdoRequest(Guid LoanPublicId, IReadOnlyList<AgreementInstallmentInput>? Installments, string? Reason);

    /// <summary>Un motivo, opcional al aprobar.</summary>
    public sealed record MotivoRequest(string? Reason);
}
