using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Application.Inventory.Purchasing;
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
/// se guarda. <c>/support-documents</c> (I4), las solicitudes, órdenes, cruce y costos adicionales (I5) y la emisión RADIAN (I5)
/// no se publican aquí. (nuevo)
/// </summary>
public class PurchasesEndpoints : ICarterModule
{
    public const string Ruta = "/api/inventory/purchases";
    private const string Prefijo = "Inventory.Purchases";
    private const string Ver = Prefijo + ".View";
    private const string Crear = Prefijo + ".Create";
    private const string Confirmar = Prefijo + ".Confirm";
    private const string RegistrarEventoRadian = Prefijo + ".RegisterRadianEvent";

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
                await sender.Send(new ListInventoryDocumentsQuery(
                    new FiltrosDeDocumentos(DocumentClassGroup.Purchases, DocumentClass.SupplierReturn, null, f.Status, f.From, f.To, f.WarehousePublicId,
                        f.SupplierPersonPublicId, f.Number, null),
                    f.Pagina()), ct))
            .WithName("Inventory_Purchases_Returns_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        devoluciones.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPurchaseDocumentQuery(id, DocumentClass.SupplierReturn), ct))
            .WithName("Inventory_Purchases_Returns_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);
        devoluciones.MapCicloDeDocumento(DocumentClassGroup.Purchases, Prefijo, "Inventory_Purchases_Returns", conConsultas: false);

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
