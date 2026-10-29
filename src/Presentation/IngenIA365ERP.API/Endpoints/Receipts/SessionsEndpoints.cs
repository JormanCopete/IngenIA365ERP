using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La sesión de caja, el arqueo y el cierre del día (feature 015, US2, T0097; contracts/api.md §3): abrir, consultar, lo esperado (lectura sensible, oculto
/// con arqueo ciego), movimientos, reclasificaciones, lo contado, la aprobación de diferencias, la sesión olvidada, los ajustes, el arqueo sorpresivo, el cierre
/// del día, los faltantes y sobrantes, el cuadre diario y los documentos en PDF. Las escrituras llevan <c>Idempotency-Key</c>; las que piden aprobador presente
/// se repiten con <c>X-Approval-Request</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class SessionsEndpoints : ICarterModule
{
    public const string Abrir = "Receipts.Session.Open";
    public const string Ver = "Receipts.Session.View";
    public const string VerEsperado = "Receipts.Session.ViewExpected";
    public const string Mover = "Receipts.Session.Move";
    public const string Reclasificar = "Receipts.Session.Reclassify";
    public const string Cerrar = "Receipts.Session.Close";
    public const string CerrarOlvidada = "Receipts.Session.CloseForgotten";
    public const string Ajustar = "Receipts.Session.Adjust";
    public const string ArquearSorpresa = "Receipts.Session.SurpriseCount";
    public const string AprobarDiferencia = "Receipts.Count.ApproveDifference";
    public const string CerrarDia = "Receipts.Day.Close";
    public const string VerInformes = "Receipts.Reports.View";

    /// <summary>El encabezado con la solicitud aprobada al repetir una operación con aprobador presente.</summary>
    public const string EncabezadoDeAprobacion = "X-Approval-Request";

    public static Guid? SolicitudDeAprobacion(HttpContext http) =>
        Guid.TryParse(http.Request.Headers[EncabezadoDeAprobacion].ToString(), out var id) ? id : null;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Sesiones(ReceiptsGroup.Grupo(app, "/sessions", "Recaudo Sesiones"));
        var otros = ReceiptsGroup.Grupo(app, string.Empty, "Recaudo Sesiones");
        Otros(otros);
    }

    private static void Sesiones(RouteGroupBuilder g)
    {
        g.MapPost("/", async (OpenCashSessionCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/sessions/{v.PublicId}"))
            .WithName("Receipts_Sessions_Open").ConClaveDeOperacion().RequirePermission(Abrir);
        g.MapGet("/", async (CashSessionState? state, Guid? point, DateOnly? date, ISender s, CancellationToken ct) =>
                await s.Send(new ListCashSessionsQuery(state, point, date), ct))
            .WithName("Receipts_Sessions_List").RequirePermission(Ver);
        g.MapGet("/current", async (ISender s, CancellationToken ct) => await s.Send(new GetCurrentSessionQuery(), ct))
            .WithName("Receipts_Sessions_Current").RequirePermission(Abrir);
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetCashSessionQuery(id), ct))
            .WithName("Receipts_Sessions_Get").RequirePermission(Ver);
        g.MapGet("/{id:guid}/expected", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetSessionExpectedQuery(id), ct))
            .WithName("Receipts_Sessions_Expected").RequirePermission(VerEsperado);
        g.MapPost("/{id:guid}/movements", async (Guid id, MovimientoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterCashMovementCommand(id, body.Kind, body.Denominations ?? [], body.Reason ?? string.Empty, body.CounterpartSessionPublicId,
                    body.SecondCustodianUserPublicId, body.BagSeal) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Sessions_Movement").ConClaveDeOperacion().RequirePermission(Mover);
        g.MapPost("/{id:guid}/reclassifications", async (Guid id, ReclasificacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReclassifyMeansCommand(id, body.FromMeansPublicId, body.ToMeansPublicId, body.Amount, body.Reason ?? string.Empty,
                    body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion(), ApprovalRequestPublicId = SolicitudDeAprobacion(http) }, ct))
            .WithName("Receipts_Sessions_Reclassify").ConClaveDeOperacion().RequirePermission(Reclasificar);
        g.MapPost("/{id:guid}/count", async (Guid id, ConteoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterClosingCountCommand(id, body.Means ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Sessions_Count").ConClaveDeOperacion().RequirePermission(Cerrar);
        g.MapPost("/{id:guid}/differences/{lineId:guid}/approve", async (Guid id, Guid lineId, AprobacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveCountDifferenceCommand(id, lineId, body.Reason ?? string.Empty, body.Treatment) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Sessions_ApproveDifference").ConClaveDeOperacion().RequirePermission(AprobarDiferencia);
        g.MapPost("/{id:guid}/close-forgotten", async (Guid id, OlvidadaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new CloseForgottenSessionCommand(id, body.WitnessUserPublicId, body.Means ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Sessions_CloseForgotten").ConClaveDeOperacion().RequirePermission(CerrarOlvidada);
        g.MapPost("/{id:guid}/adjustments", async (Guid id, AjusteRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterSessionAdjustmentCommand(id, body.Kind, body.PaymentMeansPublicId, body.Amount, body.Reason ?? string.Empty,
                    body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion(), ApprovalRequestPublicId = SolicitudDeAprobacion(http) }, ct))
            .WithName("Receipts_Sessions_Adjust").ConClaveDeOperacion().RequirePermission(Ajustar);
        g.MapGet("/{id:guid}/counts", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListSessionCountsQuery(id), ct))
            .WithName("Receipts_Sessions_Counts").RequirePermission(Ver);
        g.MapGet("/{id:guid}/close-report", async (Guid id, ISender s, IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetSessionCloseReportQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.CierreDeSesion(r.Value), "application/pdf", $"cierre-de-sesion-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_Sessions_CloseReport").RequirePermission(Ver);
    }

    private static void Otros(RouteGroupBuilder g)
    {
        g.MapPost("/surprise-counts", async (SorpresivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterSurpriseCountCommand(body.SessionPublicId, body.MinutesAttachmentPublicId, body.Means ?? [], body.WitnessUserPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_SurpriseCounts_Register").ConClaveDeOperacion().RequirePermission(ArquearSorpresa);
        g.MapGet("/surprise-counts/{id:guid}/minutes", async (Guid id, ISender s, IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetSurpriseCountMinutesQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.Acta(r.Value), "application/pdf", $"acta-de-arqueo-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_SurpriseCounts_Minutes").RequirePermission(Ver);
        g.MapPost("/collection-points/{id:guid}/days/{date}/close", async (Guid id, DateOnly date, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new CloseCollectionPointDayCommand(id, date) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Days_Close").ConClaveDeOperacion().RequirePermission(CerrarDia);
        g.MapGet("/collection-points/{id:guid}/days/{date}/report", async (Guid id, DateOnly date, ISender s, IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetDayCloseReportQuery(id, date), ct);
                return r.IsSuccess ? Results.File(pdf.CierreDelDia(r.Value), "application/pdf", $"cierre-del-dia-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_Days_Report").RequirePermission(Ver);
        g.MapGet("/cashier-receivables", async (CashierReceivableState? state, ISender s, CancellationToken ct) =>
                await s.Send(new ListCashierReceivablesQuery(state), ct))
            .WithName("Receipts_CashierReceivables_List").RequirePermission(Ver);
        g.MapPost("/cashier-receivables/{id:guid}/payroll-authorization", async (Guid id, AutorizacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AttachPayrollDeductionAuthorizationCommand(id, body.AttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashierReceivables_Authorize").ConClaveDeOperacion().RequirePermission(AprobarDiferencia);
        g.MapGet("/cash-surpluses", async (CashSurplusState? state, ISender s, CancellationToken ct) => await s.Send(new ListCashSurplusesQuery(state), ct))
            .WithName("Receipts_CashSurpluses_List").RequirePermission(Ver);
        g.MapPost("/cash-surpluses/{id:guid}/to-income", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MoveSurplusToIncomeCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashSurpluses_ToIncome").ConClaveDeOperacion().RequirePermission(AprobarDiferencia);
        g.MapGet("/daily-squares", async (Guid? branch, DateOnly? date, ISender s, CancellationToken ct) => await s.Send(new ListDailySquaresQuery(branch, date), ct))
            .WithName("Receipts_DailySquares_List").RequirePermission(VerInformes);
        g.MapPost("/daily-squares/{id:guid}/explain", async (Guid id, ExplicacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ExplainDailySquareCommand(id, body.Explanation ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_DailySquares_Explain").ConClaveDeOperacion().RequirePermission(CerrarDia);
    }

    public sealed record MovimientoRequest(CashMovementKindR Kind, IReadOnlyList<DenominacionContadaDto>? Denominations, string? Reason,
        Guid? CounterpartSessionPublicId, Guid? SecondCustodianUserPublicId, string? BagSeal);

    public sealed record ReclasificacionRequest(Guid FromMeansPublicId, Guid ToMeansPublicId, decimal Amount, string? Reason, Guid? SupportAttachmentPublicId);

    public sealed record ConteoRequest(IReadOnlyList<ConteoDeMedioDto>? Means);

    public sealed record AprobacionRequest(string? Reason, CountTreatment? Treatment);

    public sealed record OlvidadaRequest(Guid WitnessUserPublicId, IReadOnlyList<ConteoDeMedioDto>? Means);

    public sealed record AjusteRequest(SessionAdjustmentKind Kind, Guid PaymentMeansPublicId, decimal Amount, string? Reason, Guid? SupportAttachmentPublicId);

    public sealed record SorpresivoRequest(Guid SessionPublicId, Guid MinutesAttachmentPublicId, IReadOnlyList<ConteoDeMedioDto>? Means, Guid? WitnessUserPublicId);

    public sealed record AutorizacionRequest(Guid AttachmentPublicId);

    public sealed record ExplicacionRequest(string? Explanation);
}
