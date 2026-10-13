using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los pagos por pantalla de Cartera (feature 013, US5, T0558; contracts/api.md §8.3 y §10; FR-071–FR-082, T31, CH47). <b>No hay ruta para aplicar
/// pagos</b>: todo dinero que reduce una obligación entra por <c>IContratoDeObligaciones</c>, en proceso y dentro de la unidad de trabajo del
/// recaudador (Recaudo, Nómina, la recogida, el recaudador de prueba). Por pantalla, Cartera sólo
/// <list type="bullet">
/// <item><b>simula</b> un pago (<c>Lending.Payments.Simulate</c>, sin clave: no escribe) con la misma función que distribuye al aplicar (FR-072);</item>
/// <item><b>reversa</b> un pago con su vista previa (<c>Lending.Payments.Reverse</c>; la vista previa sin clave, el reverso con
/// <c>Idempotency-Key</c> y la huella que se vio, <c>expectedPreviewSha256</c>); un pago de Nómina, de Aportes o una recogida sólo lo reversa su
/// recaudador (<c>Lending.Reversal.OwnedByCollector</c>);</item>
/// <item>lee y cambia la <b>preferencia del deudor</b> para abonos a capital (<c>Lending.Loans.Read</c> · <c>Lending.PrepaymentPreferences.Manage</c>,
/// con clave; Ley 2032, D24).</item>
/// </list>
/// Fuera del alcance de oficina o línea, el mismo 404 que si el crédito no existiera. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PaymentsEndpoints : ICarterModule
{
    public const string Simular = "Lending.Payments.Simulate";
    public const string Reversar = "Lending.Payments.Reverse";
    public const string LeerCreditos = "Lending.Loans.Read";
    public const string GestionarPreferencia = "Lending.PrepaymentPreferences.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var pagos = LendingGroup.Grupo(app, "/loans", "Cartera Pagos");

        pagos.MapPost("/{id:guid}/payment-simulations", async (Guid id, SimulacionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateLoanPaymentQuery(id, body.Modality ?? PaymentModality.FreeAmount, body.Amount, body.ValueDate ?? default,
                    body.PrepaymentChoice, body.InstallmentNumber), ct))
            .WithName("Lending_Payments_Simulate")
            .RequirePermission(Simular);

        pagos.MapPost("/{id:guid}/operations/{operationId:guid}/reversal-preview", async (Guid id, Guid operationId, VistaPreviaRequest? body,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewLoanPaymentReversalQuery(id, operationId, body?.Reason), ct))
            .WithName("Lending_Payments_ReversalPreview")
            .RequirePermission(Reversar);

        pagos.MapPost("/{id:guid}/operations/{operationId:guid}/reverse", async (Guid id, Guid operationId, ReversoRequest body, HttpContext http,
                ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ReverseLoanPaymentCommand(id, operationId, body.Reason ?? string.Empty,
                        body.ExpectedPreviewSha256 ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct),
                    r => $"/api/lending/loans/{id}/operations/{r.ReversalOperationPublicId}"))
            .WithName("Lending_Payments_Reverse")
            .ConClaveDeOperacion()
            .RequirePermission(Reversar);

        pagos.MapGet("/{id:guid}/prepayment-preference", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPrepaymentPreferenceQuery(id), ct))
            .WithName("Lending_Payments_PrepaymentPreference")
            .RequirePermission(LeerCreditos);

        pagos.MapPost("/{id:guid}/prepayment-preference", async (Guid id, PreferenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ChangePrepaymentPreferenceCommand(id, body.Choice, body.Reason ?? string.Empty,
                    body.EvidenceAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct), $"/api/lending/loans/{id}/prepayment-preference"))
            .WithName("Lending_Payments_ChangePrepaymentPreference")
            .ConClaveDeOperacion()
            .RequirePermission(GestionarPreferencia);
    }

    /// <summary>La simulación (api.md §10): modalidad, valor (sin él, una cancelación cuesta lo que cuesta y «lo vencido» es lo exigible), fecha valor,
    /// la elección del abono y la cuota específica.</summary>
    public sealed record SimulacionRequest(PaymentModality? Modality, decimal? Amount, DateOnly? ValueDate, PrepaymentChoice? PrepaymentChoice, int? InstallmentNumber);

    /// <summary>El motivo, opcional en la vista previa.</summary>
    public sealed record VistaPreviaRequest(string? Reason);

    /// <summary>El reverso: el motivo y la huella de la vista previa que se vio.</summary>
    public sealed record ReversoRequest(string? Reason, string? ExpectedPreviewSha256);

    /// <summary>La preferencia nueva (nula = revocada), el motivo y la constancia.</summary>
    public sealed record PreferenciaRequest(PrepaymentChoice? Choice, string? Reason, Guid? EvidenceAttachmentPublicId);
}
