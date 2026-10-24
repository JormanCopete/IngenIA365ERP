using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CashDesk;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La caja (feature 015, US3, T0119; contracts/api.md §4): lo que una persona puede pagar, la operación de caja en preparación —ítems simulados, medios e
/// imputación, declaración de origen de fondos— y su confirmación todo o nada; y los recibos con su documento. Las escrituras llevan <c>Idempotency-Key</c>; la
/// confirmación que pide aprobador presente (fecha valor anterior) se repite con <c>X-Approval-Request</c>. Cada ruta sólo reenvía al <see cref="ISender"/>.
/// (nuevo)
/// </summary>
public class CashDeskEndpoints : ICarterModule
{
    public const string Registrar = "Receipts.Receipt.Register";
    public const string Ver = "Receipts.Receipt.View";
    public const string Imprimir = "Receipts.Receipt.Print";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var pagables = ReceiptsGroup.Grupo(app, "/payables", "Recaudo Caja");
        pagables.MapGet("/", async (Guid? person, string? document, bool? includeCoDebtor, ISender s, CancellationToken ct) =>
                await s.Send(new GetPayablesQuery(person, document, includeCoDebtor ?? false), ct))
            .WithName("Receipts_Payables_Get").RequirePermission(Registrar);

        var g = ReceiptsGroup.Grupo(app, "/cash-operations", "Recaudo Caja");
        g.MapPost("/", async (CreateCashOperationCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/cash-operations/{v.PublicId}"))
            .WithName("Receipts_CashOperations_Create").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetCashOperationQuery(id), ct))
            .WithName("Receipts_CashOperations_Get").RequirePermission(Registrar);
        g.MapPut("/{id:guid}/items", async (Guid id, ItemsRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateCashOperationItemsCommand(id, body.Items ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_Items").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapPut("/{id:guid}/means", async (Guid id, MediosRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateCashOperationMeansCommand(id, body.Means ?? [], body.FixedImputation) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_Means").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapPost("/{id:guid}/declaration", async (Guid id, DeclaracionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DeclareSourceOfFundsCommand(id, body.Origin ?? string.Empty, body.CashAmount, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_CashOperations_Declaration").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapPost("/{id:guid}/confirm", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConfirmCashOperationCommand(id)
                {
                    OperationKey = http.ClaveDeOperacion(),
                    ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct))
            .WithName("Receipts_CashOperations_Confirm").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapPost("/{id:guid}/abandon", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AbandonCashOperationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashOperations_Abandon").ConClaveDeOperacion().RequirePermission(Registrar);

        var recibos = ReceiptsGroup.Grupo(app, "/receipts", "Recaudo Recibos");
        recibos.MapGet("/", async (DateOnly? from, DateOnly? to, Guid? person, string? number, Guid? session, ISender s, CancellationToken ct) =>
                await s.Send(new ListReceiptsQuery(from, to, person, number, session), ct))
            .WithName("Receipts_Receipts_List").RequirePermission(Ver);
        recibos.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetReceiptQuery(id), ct))
            .WithName("Receipts_Receipts_Get").RequirePermission(Ver);
        recibos.MapGet("/{id:guid}/document", async (Guid id, string? format, bool? copy, ISender s,
                Application.Receipts.Sessions.IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var formato = string.Equals(format, "letter", StringComparison.OrdinalIgnoreCase) ? FormatoDeRecibo.Letter : FormatoDeRecibo.Thermal;
                var r = await s.Send(new GetReceiptDocumentQuery(id, formato, copy ?? false), ct);
                return r.IsSuccess ? Results.File(pdf.Recibo(r.Value), "application/pdf", $"recibo-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_Receipts_Document").RequirePermission(Imprimir);
    }

    public sealed record ItemsRequest(IReadOnlyList<ItemPedidoDto>? Items);

    public sealed record MediosRequest(IReadOnlyList<MedioDeCajaDto>? Means, IReadOnlyList<PorcionDeCajaDto>? FixedImputation);

    public sealed record DeclaracionRequest(string? Origin, decimal CashAmount, Guid SupportAttachmentPublicId);
}
