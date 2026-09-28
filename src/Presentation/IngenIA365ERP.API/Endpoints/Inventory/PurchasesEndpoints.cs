using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Purchasing.Consultas;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Compras de I1 (feature 012, T350; contracts/api.md §14.1–§14.6, §14.8): recepciones, facturas y notas del proveedor,
/// devoluciones y la compra directa, sobre <c>/api/inventory/purchases</c>. Cada clase tiene su ruta con su lista y su detalle
/// (lo que falta facturar, el documento del proveedor, los eventos RADIAN) y el ciclo común de §9.3 (<c>ExpectedGroup =
/// Purchases</c>) por <see cref="CicloDeDocumentoRutas.MapCicloDeDocumento"/>, con <c>Inventory.Purchases.{View, Create, Confirm,
/// Void}</c>; los eventos RADIAN con <c>Inventory.Purchases.RegisterRadianEvent</c>. Toda escritura exige
/// <c>Idempotency-Key</c> (<c>LosComandosDeInventarioLlevanClave</c>); el prellenado desde el XML es una consulta y el archivo no
/// se guarda. <c>/support-documents</c> (I4, T748) publica el documento soporte y su nota de ajuste con el mismo ciclo. I5 (T808; §14.8,
/// §14.9) suma las solicitudes (<c>/requests</c>), las órdenes (<c>/orders</c>, con su PDF, el envío al proveedor y el cierre del saldo),
/// el cruce a tres vías (<c>/matches</c> y <c>/supplier-invoices/{id}/match</c>), los costos adicionales (<c>/landed-costs</c>) y la
/// emisión RADIAN desde el ERP (<c>/supplier-invoices/{id}/radian-events/emit</c>, 202). (nuevo)
/// </summary>
public class PurchasesEndpoints : ICarterModule
{
    public const string Ruta = "/api/inventory/purchases";
    private const string Prefijo = "Inventory.Purchases";
    private const string Ver = Prefijo + ".View";
    private const string Crear = Prefijo + ".Create";
    private const string Confirmar = Prefijo + ".Confirm";
    private const string RegistrarEventoRadian = Prefijo + ".RegisterRadianEvent";
    private const string EmitirEventoRadian = Prefijo + ".EmitRadianEvent";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var compras = app.MapGroup(Ruta).WithTags("Inventory Purchases").RequireAuthorization();

        // ---------------------------------------------------------------------------------- recepciones (§14.2) --
        var recepciones = compras.MapGroup("/receipts");
        recepciones.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPurchaseReceiptsQuery(f.Filtros(), f.Pagina()), ct))
            .WithName("Inventory_Purchases_Receipts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        recepciones.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.PurchaseReceipt), ct))
            .WithName("Inventory_Purchases_Receipts_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        recepciones.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_Receipts", conConsultas: false);

        // ---------------------------------------------------------------------------- facturas (§14.4, §14.8) --
        var facturas = compras.MapGroup("/supplier-invoices");
        facturas.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSupplierInvoicesQuery(f.Filtros(), f.Pagina()), ct))
            .WithName("Inventory_Purchases_SupplierInvoices_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        facturas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupplierInvoice), ct))
            .WithName("Inventory_Purchases_SupplierInvoices_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        facturas.MapPost("/prefill", PrellenarAsync)
            .WithName("Inventory_Purchases_SupplierInvoices_Prefill")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .DisableAntiforgery()
            .RequirePermission(Crear);
        facturas.MapGet("/{id:guid}/radian-events", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRadianEventsQuery(id), ct))
            .WithName("Inventory_Purchases_RadianEvents_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        facturas.MapPost("/{id:guid}/radian-events", async (Guid id, RegistrarEventoRadianRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterExternalRadianEventCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Purchases_RadianEvents_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(RegistrarEventoRadian);
        // I5 (T808; §14.8): el ERP emite el 030 y/o el 032 por el canal de facturación electrónica. 202: quedan Pending y los lleva el
        // procesador.
        facturas.MapPost("/{id:guid}/radian-events/emit", async (Guid id, EmitirEventosRadianRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new EmitRadianEventCommand(id, body.EventCodes ?? []) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Accepted($"{Ruta}/supplier-invoices/{id}/radian-events", result.Value) : result;
            })
            .WithName("Inventory_Purchases_RadianEvents_Emit")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(EmitirEventoRadian);
        // I5 (T797, T808; §14.9): el cruce de la factura, línea por línea (vacío si no se cruzó contra una orden).
        facturas.MapGet("/{id:guid}/match", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetSupplierInvoiceMatchQuery(id), ct))
            .WithName("Inventory_Purchases_SupplierInvoices_Match")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        facturas.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_SupplierInvoices", conConsultas: false);

        // ------------------------------------------------------------------------------------ notas (§14.5) --
        var notas = compras.MapGroup("/supplier-notes");
        notas.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSupplierInvoicesQuery(f.Filtros(), f.Pagina(), DocumentClass.SupplierNote), ct))
            .WithName("Inventory_Purchases_SupplierNotes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        notas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupplierNote), ct))
            .WithName("Inventory_Purchases_SupplierNotes_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        notas.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_SupplierNotes", conConsultas: false);

        // ------------------------------------------------------------------------------- devoluciones (§14.6) --
        var devoluciones = compras.MapGroup("/returns");
        devoluciones.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(DeClase(f, DocumentClass.SupplierReturn), f.Pagina()), ct))
            .WithName("Inventory_Purchases_Returns_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        devoluciones.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupplierReturn), ct))
            .WithName("Inventory_Purchases_Returns_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        devoluciones.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_Returns", conConsultas: false);

        // ------------------------------------------------------------- documento soporte y su nota de ajuste (§14.7, I4) --
        // T748: el mismo ciclo que la factura del proveedor; la nota de ajuste se crea en esta misma ruta con un tipo de clase
        // SupportDocumentAdjustmentNote y supportDocumentPublicId. Su estado ante la DIAN vive en /api/electronic-invoicing/documents.
        var soportes = compras.MapGroup("/support-documents");
        soportes.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, DocumentClass? @class, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSupplierInvoicesQuery(f.Filtros(), f.Pagina(),
                    @class == DocumentClass.SupportDocumentAdjustmentNote ? DocumentClass.SupportDocumentAdjustmentNote : DocumentClass.SupportDocument), ct))
            .WithName("Inventory_Purchases_SupportDocuments_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        soportes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var soporte = await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupportDocument), ct);
                return soporte.IsSuccess ? soporte : await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupportDocumentAdjustmentNote), ct);
            })
            .WithName("Inventory_Purchases_SupportDocuments_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        // La generación semanal (DocumentoSoporte.Generacion = Semanal): un borrador por proveedor no obligado con las recepciones de la
        // semana que no tienen factura ni documento soporte; con PorOperacion no hace nada. (nuevo)
        soportes.MapPost("/weekly", async (GenerarSemanalRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new GenerateWeeklySupportDocumentsCommand(body?.UpTo) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Purchases_SupportDocuments_Weekly")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Crear);
        soportes.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_SupportDocuments", conConsultas: false);

        // -------------------------------------------------------------------------------- I5: solicitudes (§14.9) --
        // T808: el ciclo común con la aprobación por la política del tipo; sin efecto en inventario. El detalle trae neededBy y, por
        // línea, pendingToOrder.
        var solicitudes = compras.MapGroup("/requests");
        solicitudes.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(DeClase(f, DocumentClass.PurchaseRequest), f.Pagina()), ct))
            .WithName("Inventory_Purchases_Requests_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        solicitudes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.PurchaseRequest), ct))
            .WithName("Inventory_Purchases_Requests_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        solicitudes.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_Requests", conConsultas: false);

        // ------------------------------------------------------------------------------------ I5: órdenes (§14.9) --
        // T808: el ciclo común con el límite de monto de Confirm y la política (FR-048); el detalle trae expectedDate, las condiciones,
        // el cierre del saldo y, por línea, pendingToReceive. Rutas propias: el PDF, el envío al proveedor y el cierre del saldo (T792).
        var ordenes = compras.MapGroup("/orders");
        ordenes.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(DeClase(f, DocumentClass.PurchaseOrder), f.Pagina()), ct))
            .WithName("Inventory_Purchases_Orders_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        ordenes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.PurchaseOrder), ct))
            .WithName("Inventory_Purchases_Orders_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        ordenes.MapGet("/{id:guid}/pdf", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var pdf = await sender.Send(new GetPurchaseOrderPdfQuery(id), ct);
                return pdf.IsSuccess ? (object)Results.File(pdf.Value.Pdf, "application/pdf", pdf.Value.FileName) : pdf;
            })
            .WithName("Inventory_Purchases_Orders_Pdf")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        ordenes.MapPost("/{id:guid}/send", async (Guid id, EnviarOrdenRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SendPurchaseOrderCommand(id, body?.Email) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Purchases_Orders_Send")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Confirmar);
        ordenes.MapPost("/{id:guid}/close-balance", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ClosePurchaseOrderBalanceCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Purchases_Orders_CloseBalance")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Confirmar);
        ordenes.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_Orders", conConsultas: false);

        // ---------------------------------------------------------------------------- I5: cruce a tres vías (§14.9) --
        // T808: las líneas del cruce de las facturas visibles en el alcance (precios nulos sin Inventory.Costs.Read).
        compras.MapGet("/matches", async (PurchaseMatchStatus? status, Guid? supplierPersonPublicId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPurchaseMatchesQuery(status, supplierPersonPublicId, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Purchases_Matches_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        // ------------------------------------------------------------------------- I5: costos adicionales (§14.9) --
        // T808: el ciclo común; el borrador arma sus líneas desde las recepciones y devuelve landedCost con el reparto.
        var costos = compras.MapGroup("/landed-costs");
        costos.MapGet("/", async ([AsParameters] FiltrosDeComprasRequest f, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(DeClase(f, DocumentClass.LandedCost), f.Pagina()), ct))
            .WithName("Inventory_Purchases_LandedCosts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        costos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.LandedCost), ct))
            .WithName("Inventory_Purchases_LandedCosts_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        costos.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_LandedCosts", conConsultas: false);

        // ------------------------------------------------------------------------------ compra directa (§14.3) --
        compras.MapPost("/direct", async (CompraDirectaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ConfirmDirectPurchaseCommand(body.Receipt, body.Invoice) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/receipts/{result.Value.Receipt.PublicId}", result.Value) : result;
            })
            .WithName("Inventory_Purchases_Direct")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Confirmar);
    }

    /// <summary>
    /// <c>POST /supplier-invoices/prefill</c> (multipart, campo <c>archivo</c> o <c>file</c>): lee el XML en memoria y lo descarta.
    /// </summary>
    private static async Task<object?> PrellenarAsync(HttpContext http, ISender sender, CancellationToken ct)
    {
        var formulario = await http.Request.ReadFormAsync(ct);
        var archivo = formulario.Files["archivo"] ?? formulario.Files["file"] ?? formulario.Files.FirstOrDefault();
        using var memoria = new MemoryStream();
        if (archivo is not null) await archivo.CopyToAsync(memoria, ct);
        return await sender.Send(new PrefillSupplierInvoiceQuery(memoria.ToArray()), ct);
    }

    /// <summary>Los filtros de una lista de compras por clase (solicitudes, órdenes, costos adicionales, devoluciones).</summary>
    private static FiltrosDeDocumentos DeClase(FiltrosDeComprasRequest f, DocumentClass clase) =>
        new(DocumentClassGroup.Purchases, clase, null, f.Status, f.From, f.To, f.WarehousePublicId, f.SupplierPersonPublicId, f.Number, null);

    /// <summary>El cuerpo de <c>POST /orders/{id}/send</c> (§14.9): a qué correo (nulo = el del proveedor en el maestro). (nuevo)</summary>
    public sealed record EnviarOrdenRequest(string? Email);

    /// <summary>El cuerpo de <c>POST /supplier-invoices/{id}/radian-events/emit</c> (§14.8): qué eventos emitir. (nuevo)</summary>
    public sealed record EmitirEventosRadianRequest(IReadOnlyList<SupplierInvoiceEventCode>? EventCodes);

    /// <summary>El cuerpo de <c>POST /support-documents/weekly</c>: hasta qué día de la semana (nulo = hoy). (nuevo)</summary>
    public sealed record GenerarSemanalRequest(DateOnly? UpTo);

    /// <summary>El cuerpo de <c>POST /direct</c> (§14.3): la recepción (el cuerpo de §14.2) y la factura.</summary>
    public sealed record CompraDirectaRequest(Application.Inventory.Documents.SaveInventoryDraftRequest Receipt, DirectPurchaseInvoiceRequest Invoice);

    /// <summary>Los filtros de las listas de compras (§14.2, §14.4), por query string.</summary>
    public sealed class FiltrosDeComprasRequest
    {
        [FromQuery] public Guid? SupplierPersonPublicId { get; set; }
        [FromQuery] public DocumentStatus? Status { get; set; }
        [FromQuery] public DateOnly? From { get; set; }
        [FromQuery] public DateOnly? To { get; set; }
        [FromQuery] public Guid? WarehousePublicId { get; set; }
        [FromQuery] public string? PaymentForm { get; set; }
        [FromQuery] public bool? RadianPending { get; set; }
        [FromQuery] public string? Number { get; set; }
        [FromQuery] public int? Page { get; set; }
        [FromQuery] public int? PageSize { get; set; }

        public FiltrosDeCompras Filtros() => new(SupplierPersonPublicId, Status, From, To, WarehousePublicId, PaymentForm, RadianPending, Number);

        public PageRequest Pagina() => new(Page ?? 1, PageSize ?? 20);
    }
}
