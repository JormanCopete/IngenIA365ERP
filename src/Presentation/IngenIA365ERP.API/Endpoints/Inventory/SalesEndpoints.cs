using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Las ventas de oficina (feature 012; contracts/api.md §18.1–§18.3, §20.3; T632): la consulta de documentos de venta
/// (<c>/api/inventory/sales/documents</c>, su detalle y la primera entrega), las facturas y comprobantes no electrónicos
/// (<c>/sales/invoices</c>) y las notas de venta (<c>/sales/credit-notes</c>), con la familia <c>Inventory.Sales.*</c>. Toda escritura con
/// <c>Idempotency-Key</c>. Qué clase admite cada ruta lo decide el borrador (<c>BorradorDeVenta</c>, <c>SaveCreditNoteDraftCommand</c>):
/// otra clase es <c>Inventory.Document.TypeNotForRoute</c>. Los 422 por permisos que dependen del cuerpo
/// (<c>Payments.RefundMeansNotAllowed</c>, <c>Payments.MeansNotAvailable</c>) salen del handler, no del filtro: el recurso ya es visible
/// (§1.2). La reimpresión, que sirve para cualquier grupo, está en <see cref="DocumentsEndpoints"/>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class SalesEndpoints : ICarterModule
{
    public const string Ver = "Inventory.Sales.View";
    public const string Crear = "Inventory.Sales.Create";
    public const string Confirmar = "Inventory.Sales.Confirm";
    public const string Anular = "Inventory.Sales.Void";
    public const string VenderACredito = "Inventory.Sales.SellOnCredit";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Documentos(app);
        Facturas(app);
        Notas(app);
        Credito(app);
    }

    /// <summary>
    /// I3 (T653, T657; §23.1, §23.2): evaluar una venta a crédito —una consulta, sin <c>Idempotency-Key</c>— y la pestaña «Crédito» de la
    /// venta (<c>GET /sales/documents/{id}/credit</c>). La venta nunca muestra saldos de Cartera.
    /// </summary>
    private static void Credito(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/inventory/sales/credit-evaluations", async (EvaluateSaleCreditQuery body, ISender sender, CancellationToken ct) =>
                await sender.Send(body, ct))
            .WithTags("Inventory Sales Credit")
            .WithName("Inventory_Sales_Credit_Evaluate")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VenderACredito);

        app.MapGet("/api/inventory/sales/documents/{id:guid}/credit", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetSalesDocumentCreditQuery(id), ct))
            .WithTags("Inventory Sales Documents")
            .WithName("Inventory_Sales_Documents_Credit")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
    }

    private static void Documentos(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/sales/documents")
            .WithTags("Inventory Sales Documents")
            .RequireAuthorization();

        group.MapGet("/", async (
                DocumentClass? @class, Guid? documentType, DocumentStatus? status, DateOnly? from, DateOnly? to, Guid? pointOfSale, Guid? cashRegister,
                Guid? cashSession, Guid? person, Guid? salesperson, long? number, string? electronicStatus, bool? pendingDelivery,
                bool? pendingValidation, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSalesDocumentsQuery(@class, documentType, status, from, to, pointOfSale, cashRegister, cashSession, person,
                    salesperson, number, electronicStatus, pendingDelivery, pendingValidation, page ?? 1, pageSize ?? 20), ct))
            .WithName("Inventory_Sales_Documents_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetSalesDocumentQuery(id), ct))
            .WithName("Inventory_Sales_Documents_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        // §20.3: la primera entrega, sin «COPIA». La tirilla vuelve como JSON; la carta de un comprobante no electrónico, como PDF.
        group.MapPost("/{id:guid}/deliver", async (Guid id, EntregaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                ComoArchivoOJson(http, await sender.Send(new DeliverSalesDocumentCommand(id, body.Format ?? CashRegisterPrintFormat.Ticket80, body.SendEmail ?? false, body.Email)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct)))
            .WithName("Inventory_Sales_Documents_Deliver")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Ver);

        // §18.3.1 (I4, T748): el comprador pide factura cuando el documento equivalente POS ya se expidió. Una sola acción: la nota de ajuste
        // de anulación total y la factura con los mismos pagos, en una transacción.
        group.MapPost("/{id:guid}/invoice-instead", async (Guid id, FacturaEnLugarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReplacePosDocumentWithInvoiceCommand(id, body.BuyerPersonPublicId, body.Reason ?? string.Empty, body.ExpectedAmountDue)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Sales_Documents_InvoiceInstead")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Confirmar);
    }

    private static void Facturas(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/sales/invoices")
            .WithTags("Inventory Sales Invoices")
            .RequireAuthorization();

        group.MapPost("/", async (SalesDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, body.ComoBorrador()) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                return Results.Created($"/api/inventory/sales/invoices/{result.Value.PublicId}", await DetalleAsync(sender, result.Value, ct));
            })
            .WithName("Inventory_Sales_Invoices_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        group.MapPut("/{id:guid}", async (Guid id, SalesDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(id, DocumentClassGroup.Sales, body.ComoBorrador()) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsFailure ? (object)result : await DetalleAsync(sender, result.Value, ct);
            })
            .WithName("Inventory_Sales_Invoices_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        MapConfirmarAnularDescartar(group, "Inventory_Sales_Invoices");
    }

    private static void Notas(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/sales/credit-notes")
            .WithTags("Inventory Sales Credit Notes")
            .RequireAuthorization();

        group.MapPost("/", async (CreditNoteDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveCreditNoteDraftCommand(null, body) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                return Results.Created($"/api/inventory/sales/credit-notes/{result.Value.PublicId}", await DetalleAsync(sender, result.Value, ct));
            })
            .WithName("Inventory_Sales_CreditNotes_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        group.MapPut("/{id:guid}", async (Guid id, CreditNoteDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveCreditNoteDraftCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsFailure ? (object)result : await DetalleAsync(sender, result.Value, ct);
            })
            .WithName("Inventory_Sales_CreditNotes_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        MapConfirmarAnularDescartar(group, "Inventory_Sales_CreditNotes");
    }

    /// <summary>
    /// Confirmar (<c>{ expectedAmountDue }</c> → <c>Inventory.Document.TotalChanged</c> si no coincide), anular (documento contrario; un
    /// fiscal electrónico responde <c>FiscalUseCorrection</c>) y descartar el borrador: el ciclo común del grupo <c>Sales</c> (§18.2, §18.3).
    /// </summary>
    private static void MapConfirmarAnularDescartar(RouteGroupBuilder group, string nombre)
    {
        group.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmarVentaRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmInventoryDocumentCommand(id, DocumentClassGroup.Sales)
                {
                    RowVersion = body?.RowVersion,
                    ExpectedAmountDue = body?.ExpectedAmountDue,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName($"{nombre}_Confirm")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Confirmar);

        group.MapPost("/{id:guid}/void", async (Guid id, AnularVentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, DocumentClassGroup.Sales, body.Reason ?? string.Empty, body.OperationDate)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{http.Request.Path.Value}", result.Value) : result;
            })
            .WithName($"{nombre}_Void")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Anular);

        group.MapPost("/{id:guid}/discard", async (Guid id, CicloDeDocumentoRutas.MotivoRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, DocumentClassGroup.Sales, body?.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName($"{nombre}_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);
    }

    /// <summary>
    /// El <c>SalesDocumentDto</c> del borrador recién guardado (§18.2: <c>POST</c> y <c>PUT</c> responden con él); si la consulta no lo
    /// devuelve, lo que respondió el guardado.
    /// </summary>
    private static async Task<object> DetalleAsync(ISender sender, InventoryDocumentDto guardado, CancellationToken ct)
    {
        var detalle = await sender.Send(new GetSalesDocumentQuery(guardado.PublicId), ct);
        return detalle.IsSuccess ? (object)detalle.Value : guardado;
    }

    /// <summary>La entrega o la reimpresión: la carta en PDF como archivo; la tirilla (y cualquier error) por el sobre de siempre.</summary>
    internal static object ComoArchivoOJson(HttpContext http, Result<EntregaDto> entrega) =>
        entrega is { IsSuccess: true, Value.Pdf: { } pdf }
            ? Results.File(pdf, "application/pdf", entrega.Value.FileName ?? $"venta-{entrega.Value.DocumentPublicId:N}.pdf")
            : entrega;

    /// <summary><c>POST …/deliver</c> y <c>/reprint</c>: formato (tirilla o carta), si se manda por correo y a cuál.</summary>
    public sealed record EntregaRequest(CashRegisterPrintFormat? Format, bool? SendEmail, string? Email, string? Reason);

    /// <summary><c>POST …/invoice-instead</c> (§18.3.1): a nombre de quién va la factura, el motivo y lo que la persona vio a pagar. (nuevo)</summary>
    public sealed record FacturaEnLugarRequest(Guid BuyerPersonPublicId, string? Reason, decimal ExpectedAmountDue);

    /// <summary><c>POST …/confirm</c>: lo que la persona vio a pagar y, si la tiene, la versión leída.</summary>
    public sealed record ConfirmarVentaRequest(decimal? ExpectedAmountDue, byte[]? RowVersion);

    /// <summary>
    /// <c>POST …/void</c>: motivo y fecha. <c>cashSessionPublicId</c> (la sesión de la que sale un reintegro en efectivo, T50) se admite en el
    /// contrato; el documento contrario de un comprobante no electrónico hoy no reintegra por caja.
    /// </summary>
    public sealed record AnularVentaRequest(string? Reason, DateOnly? OperationDate, Guid? CashSessionPublicId);
}
