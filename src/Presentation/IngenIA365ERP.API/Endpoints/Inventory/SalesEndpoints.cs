using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing.Promotions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
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
/// <para>
/// I6 (T891, T892; §18.4, §19.4): cotizaciones, pedidos, remisiones y notas débito (<c>/sales/{quotes, orders, shipments, debit-notes}</c>) con
/// las mismas cinco operaciones del ciclo común y los mismos permisos <c>Sales.*</c>; cada prefijo admite sólo su clase
/// (<see cref="RutasDeVenta"/>), <c>POST /quotes/{id}/to-order</c> crea el borrador del pedido, y <c>POST /invoices</c> admite además la
/// factura desde remisiones con <c>originPublicIds</c>. Las promociones (<c>/api/inventory/promotions</c>) viven en este archivo
/// (decisiones-transversales §2.9) con <c>Inventory.Prices.View</c> y <c>.Manage</c>.
/// </para>
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
        CicloComercial(app);
        Promociones(app);
    }

    /// <summary>
    /// I6 (T891; §18.4): cotizaciones, pedidos, remisiones y notas débito sobre el ciclo común; cada prefijo admite sólo su clase y otra
    /// responde <c>Inventory.Document.TypeNotForRoute</c> (<c>data { class, group }</c>). «Convertir en pedido» responde 201 con el borrador.
    /// </summary>
    private static void CicloComercial(IEndpointRouteBuilder app)
    {
        var cotizaciones = MapBorradoresDeVenta(app, "/api/inventory/sales/quotes", "Inventory Sales Quotes", "Inventory_Sales_Quotes", RutasDeVenta.Cotizaciones);
        cotizaciones.MapPost("/{id:guid}/to-order", async (Guid id, ConvertirEnPedidoRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ConvertQuoteToOrderCommand(id, body?.DocumentTypePublicId) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                return Results.Created($"/api/inventory/sales/orders/{result.Value.PublicId}", await DetalleAsync(sender, result.Value, ct));
            })
            .WithName("Inventory_Sales_Quotes_ToOrder")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        MapBorradoresDeVenta(app, "/api/inventory/sales/orders", "Inventory Sales Orders", "Inventory_Sales_Orders", RutasDeVenta.Pedidos);
        MapBorradoresDeVenta(app, "/api/inventory/sales/shipments", "Inventory Sales Shipments", "Inventory_Sales_Shipments", RutasDeVenta.Remisiones);
        MapBorradoresDeVenta(app, "/api/inventory/sales/debit-notes", "Inventory Sales Debit Notes", "Inventory_Sales_DebitNotes", RutasDeVenta.NotasDebito);
    }

    /// <summary>
    /// I6 (T892; §19.4): las promociones. Consultar con <c>Inventory.Prices.View</c>; crear y editar con <c>Inventory.Prices.Manage</c> y
    /// <c>Idempotency-Key</c>. Ya aplicada en un documento confirmado, sólo cambian nombre, fin de vigencia y activo
    /// (<c>Inventory.Promotion.InUse</c>).
    /// </summary>
    private static void Promociones(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/promotions")
            .WithTags("Inventory Promotions")
            .RequireAuthorization();

        group.MapGet("/", async (DateOnly? asOf, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPromotionsQuery(asOf, active), ct))
            .WithName("Inventory_Promotions_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PricingEndpoints.PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPromotionQuery(id), ct))
            .WithName("Inventory_Promotions_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PricingEndpoints.PermisoDeConsulta);

        group.MapPost("/", async (CreatePromotionCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/promotions/{result.Value.PromotionPublicId}", result.Value)
                    : result;
            })
            .WithName("Inventory_Promotions_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PricingEndpoints.PermisoDeListas);

        group.MapPut("/{id:guid}", async (Guid id, UpdatePromotionCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(body with { PromotionPublicId = id, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Promotions_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PricingEndpoints.PermisoDeListas);
    }

    /// <summary>
    /// Las cinco operaciones del ciclo común de un prefijo de ventas de I6 (§18.4): crear y reemplazar el borrador —sólo con las clases de
    /// <paramref name="clases"/>—, confirmar, anular y descartar. Devuelve el grupo para sumarle rutas propias.
    /// </summary>
    private static RouteGroupBuilder MapBorradoresDeVenta(IEndpointRouteBuilder app, string prefijo, string etiqueta, string nombre,
        IReadOnlyList<DocumentClass> clases)
    {
        var group = app.MapGroup(prefijo)
            .WithTags(etiqueta)
            .RequireAuthorization();

        group.MapPost("/", async (SalesDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, body.ComoBorrador(clases)) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                return Results.Created($"{prefijo}/{result.Value.PublicId}", await DetalleAsync(sender, result.Value, ct));
            })
            .WithName($"{nombre}_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        group.MapPut("/{id:guid}", async (Guid id, SalesDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(id, DocumentClassGroup.Sales, body.ComoBorrador(clases)) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsFailure ? (object)result : await DetalleAsync(sender, result.Value, ct);
            })
            .WithName($"{nombre}_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        MapConfirmarAnularDescartar(group, nombre);
        return group;
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
                var result = await sender.Send(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, body.ComoBorrador(RutasDeVenta.Facturas)) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                return Results.Created($"/api/inventory/sales/invoices/{result.Value.PublicId}", await DetalleAsync(sender, result.Value, ct));
            })
            .WithName("Inventory_Sales_Invoices_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        group.MapPut("/{id:guid}", async (Guid id, SalesDraftInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(id, DocumentClassGroup.Sales, body.ComoBorrador(RutasDeVenta.Facturas)) { OperationKey = http.ClaveDeOperacion() }, ct);
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

    /// <summary><c>POST /quotes/{id}/to-order</c> (§18.4): el tipo del pedido; sin él, el primero activo de clase <c>SalesOrder</c>. (nuevo)</summary>
    public sealed record ConvertirEnPedidoRequest(Guid? DocumentTypePublicId);

    /// <summary><c>POST …/confirm</c>: lo que la persona vio a pagar y, si la tiene, la versión leída.</summary>
    public sealed record ConfirmarVentaRequest(decimal? ExpectedAmountDue, byte[]? RowVersion);

    /// <summary>
    /// <c>POST …/void</c>: motivo y fecha. <c>cashSessionPublicId</c> (la sesión de la que sale un reintegro en efectivo, T50) se admite en el
    /// contrato; el documento contrario de un comprobante no electrónico hoy no reintegra por caja.
    /// </summary>
    public sealed record AnularVentaRequest(string? Reason, DateOnly? OperationDate, Guid? CashSessionPublicId);
}
