using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// El cumplimiento en la caja y la contingencia (feature 015, US14 y US2, T0244; contracts/api.md §4, §7): el registro de transacciones en efectivo y las señales (lecturas
/// sensibles del oficial), las operaciones retenidas, retener, liberar y devolver por orden de pago, y el recibo de contingencia del talonario con el estado del
/// talonario. Cada escritura lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public class ComplianceEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Compliance.View";
    public const string Liberar = "Receipts.Compliance.Release";
    public const string LiberarListaVinculante = "Receipts.Compliance.ReleaseBindingMatch";
    public const string Registrar = "Receipts.Receipt.Register";
    public const string Contingencia = "Receipts.Receipt.RegisterContingency";
    public const string Reportar = "Receipts.Sarlaft.Report";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var c = ReceiptsGroup.Grupo(app, "/compliance", "Recaudo Cumplimiento");
        c.MapGet("/cash-transactions", async (Guid? person, int? month, ISender s, CancellationToken ct) => await s.Send(new ListCashTransactionsQuery(person, month), ct))
            .WithName("Receipts_Compliance_CashTransactions").RequirePermission(Ver);
        c.MapGet("/signals", async (Guid? person, DateOnly? from, ISender s, CancellationToken ct) => await s.Send(new ListComplianceSignalsQuery(person, from), ct))
            .WithName("Receipts_Compliance_Signals").RequirePermission(Ver);
        c.MapGet("/withheld", async (ISender s, CancellationToken ct) => await s.Send(new ListWithheldOperationsQuery(), ct))
            .WithName("Receipts_Compliance_Withheld").RequirePermission(new AlgunPermiso(Ver, Liberar));
        // Segunda ronda del 2026-10-27 (D-e; FR-230): las operaciones bloqueadas por lista vinculante, para el oficial.
        c.MapGet("/blocked", async (ISender s, CancellationToken ct) => await s.Send(new ListBlockedOperationsQuery(), ct))
            .WithName("Receipts_Compliance_Blocked").RequirePermission(new AlgunPermiso(Ver, LiberarListaVinculante));

        // Feature 015, R1 (T0391; FR-096): el reporte de transacciones a la UIAF.
        var rep = ReceiptsGroup.Grupo(app, "/compliance/cash-transaction-reports", "Recaudo Reporte de transacciones");
        rep.MapGet("/", async (int? year, ISender s, CancellationToken ct) => await s.Send(new ListCashTransactionReportsQuery(year), ct))
            .WithName("Receipts_CashTransactionReports_List").RequirePermission(Reportar);
        rep.MapPost("/", async (PeriodoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new GenerateCashTransactionReportCommand(body.Year, body.Month) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashTransactionReports_Generate").ConClaveDeOperacion().RequirePermission(Reportar);
        rep.MapPost("/{id:guid}/regenerate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegenerateCashTransactionReportCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashTransactionReports_Regenerate").ConClaveDeOperacion().RequirePermission(Reportar);
        rep.MapPost("/{id:guid}/uploaded", async (Guid id, CargadoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkCashTransactionReportUploadedCommand(id, body.Reference ?? string.Empty, body.UploadedAt, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_CashTransactionReports_Uploaded").ConClaveDeOperacion().RequirePermission(Reportar);
        rep.MapGet("/{id:guid}/file", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetCashTransactionReportFileQuery(id), ct))
            .WithName("Receipts_CashTransactionReports_File").RequirePermission(Reportar);

        var o = ReceiptsGroup.Grupo(app, "/cash-operations", "Recaudo Cumplimiento");
        o.MapPost("/{id:guid}/withhold", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new WithholdCashOperationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_Withhold").ConClaveDeOperacion().RequirePermission(Registrar);
        o.MapPost("/{id:guid}/release", async (Guid id, LiberacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReleaseWithheldOperationCommand(id, body.SupportAttachmentPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_Release").ConClaveDeOperacion().RequirePermission(Liberar);
        o.MapPost("/{id:guid}/release-binding-match", async (Guid id, LiberacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReleaseBindingMatchCommand(id, body.SupportAttachmentPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_ReleaseBindingMatch").ConClaveDeOperacion().RequirePermission(LiberarListaVinculante);
        o.MapPost("/{id:guid}/refund-by-order", async (Guid id, DevolucionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RefundWithheldMoneyByOrderCommand(id, body.SupportAttachmentPublicId, body.PersonBankAccountPublicId, body.Form ?? "Transfer")
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_CashOperations_RefundWithheld").ConClaveDeOperacion().RequirePermission(Liberar);

        var t = ReceiptsGroup.Grupo(app, "/contingency-receipts", "Recaudo Contingencia");
        t.MapPost("/", async (ContingenciaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterContingencyReceiptCommand(body.ContingencyBookPublicId, body.ContingencyNumber, body.PayerPersonPublicId, body.ValueDate,
                    body.Parts ?? [], body.ImageAttachmentPublicId, body.MeansClass ?? PaymentMeansClass.Cash, body.DeliveredByPersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_ContingencyReceipts_Register").ConClaveDeOperacion().RequirePermission(Contingencia);
        var b = ReceiptsGroup.Grupo(app, "/contingency-books", "Recaudo Contingencia");
        b.MapGet("/{id:guid}/status", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetContingencyBookStatusQuery(id), ct))
            .WithName("Receipts_ContingencyBooks_Status").RequirePermission(Contingencia);
    }

    public sealed record LiberacionRequest(Guid SupportAttachmentPublicId, string? Reason);

    public sealed record DevolucionRequest(Guid SupportAttachmentPublicId, Guid? PersonBankAccountPublicId, string? Form);

    public sealed record ContingenciaRequest(Guid ContingencyBookPublicId, int ContingencyNumber, Guid PayerPersonPublicId, DateOnly ValueDate,
        IReadOnlyList<ParteDeAbono>? Parts, Guid ImageAttachmentPublicId, PaymentMeansClass? MeansClass, Guid? DeliveredByPersonPublicId);

    public sealed record PeriodoRequest(int Year, int Month);

    public sealed record CargadoRequest(string? Reference, DateTime? UploadedAt, Guid? SupportAttachmentPublicId);
}
