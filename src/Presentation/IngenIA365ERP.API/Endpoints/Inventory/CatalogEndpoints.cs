using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Catalog.AccountingGroups;
using IngenIA365ERP.Application.Inventory.Catalog.AdjustmentCauses;
using IngenIA365ERP.Application.Inventory.Catalog.Brands;
using IngenIA365ERP.Application.Inventory.Catalog.Categories;
using IngenIA365ERP.Application.Inventory.Catalog.Components;
using IngenIA365ERP.Application.Inventory.Catalog.Lots;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Catalog.SalesChannels;
using IngenIA365ERP.Application.Inventory.Catalog.UnitsOfMeasure;
using IngenIA365ERP.Application.Inventory.Catalog.Variants;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// El catálogo de Inventario (feature 012, T230; contracts/api.md §3): unidades, categorías, marcas, grupos contables,
/// productos con sus subrecursos (unidades alternas, códigos de barras, impuestos) y su búsqueda, causas de ajuste y canales
/// de venta. Leer con <c>Inventory.Catalog.View</c>; escribir con <c>Inventory.Catalog.Manage</c> e <c>Idempotency-Key</c>
/// (<c>ConClaveDeOperacion</c>). Los catálogos se inactivan y reactivan con motivo; sólo un producto sin historia admite
/// <c>DELETE</c>. Las plantillas 2 a 6 (<c>template.xlsx</c>, <c>import</c>) van por <see cref="RutasDePlantilla.MapPlantilla"/>
/// junto a su catálogo: descarga con <c>Catalog.View</c>, con datos además <c>Catalog.Export</c>, importar con
/// <c>Catalog.Import</c>. La lista de plantillas es de <c>TemplatesEndpoints</c>. El cambio de grupo contable de un producto
/// (<c>/products/{id}/accounting-group</c>, US3 T292): historial con <c>Catalog.View</c> y cambio con
/// <c>Inventory.Catalog.ReclassifyAccountingGroup</c>, motivo e <c>Idempotency-Key</c>. El catálogo avanzado de I6 (US15, T934; rutas nuevas,
/// contracts/api.md §17.1): atributos de variante (<c>/variant-attributes</c>), variantes de una plantilla (<c>/products/{id}/variants</c>),
/// componentes de un combo o kit (<c>/products/{id}/components</c>) con <c>Catalog.View</c>/<c>Catalog.Manage</c>, y los lotes y las series de
/// un producto (<c>/lots</c>, <c>/serials</c>) con <c>Inventory.Stock.View</c> y el alcance por bodega (fuera de él, 404). Cada ruta sólo reenvía
/// al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CatalogEndpoints : ICarterModule
{
    public const string Ver = "Inventory.Catalog.View";
    public const string Administrar = "Inventory.Catalog.Manage";
    public const string Importar = "Inventory.Catalog.Import";
    public const string Exportar = "Inventory.Catalog.Export";
    public const string Reclasificar = "Inventory.Catalog.ReclassifyAccountingGroup";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Unidades(app);
        Categorias(app);
        Marcas(app);
        GruposContables(app);
        Productos(app);
        CausasDeAjuste(app);
        CanalesDeVenta(app);
        AtributosDeVariante(app);
        LotesYSeries(app);
    }

    private static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string ruta, string etiqueta) =>
        app.MapGroup($"/api/inventory/{ruta}").WithTags(etiqueta).RequireAuthorization();

    // ---------------------------------------------------------------------------------------------- unidades --

    private static void Unidades(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "units", "Inventory Units");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListUnitsOfMeasureQuery(includeInactive ?? false), ct))
            .WithName("Inventory_Units_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (UnidadRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateUnitOfMeasureCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Symbol,
                    body.AllowedDecimals, body.DianUnitCode) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/units/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Units_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, UnidadRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateUnitOfMeasureCommand(id, body.Name ?? string.Empty, body.Symbol, body.AllowedDecimals, body.DianUnitCode)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Units_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_Units", (id, activa, motivo, clave) => new SetUnitOfMeasureActiveCommand(id, activa, motivo) { OperationKey = clave });

        g.MapPlantilla(Ver, Importar, PlantillaDeUnidades.Clave, "Inventory_Units",
            importar: (modo, archivo, motivo, clave) => new ImportUnitsOfMeasureCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetUnitsOfMeasureTemplateDataQuery(),
            permisoDeExportacion: Exportar);
    }

    // -------------------------------------------------------------------------------------------- categorías --

    private static void Categorias(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "product-categories", "Inventory Categories");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListProductCategoriesQuery(includeInactive ?? false), ct))
            .WithName("Inventory_Categories_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CategoriaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateProductCategoryCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.ParentPublicId)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/product-categories/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Categories_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, CategoriaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateProductCategoryCommand(id, body.Name ?? string.Empty, body.ParentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Categories_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_Categories", (id, activa, motivo, clave) => new SetProductCategoryActiveCommand(id, activa, motivo) { OperationKey = clave });

        g.MapPlantilla(Ver, Importar, PlantillaDeCategorias.Clave, "Inventory_Categories",
            importar: (modo, archivo, motivo, clave) => new ImportProductCategoriesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetProductCategoriesTemplateDataQuery(),
            permisoDeExportacion: Exportar);
    }

    // ------------------------------------------------------------------------------------------------ marcas --

    private static void Marcas(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "brands", "Inventory Brands");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBrandsQuery(includeInactive ?? false), ct))
            .WithName("Inventory_Brands_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CodigoYNombreRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateBrandCommand(body.Code ?? string.Empty, body.Name ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/brands/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Brands_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, CodigoYNombreRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBrandCommand(id, body.Name ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Brands_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_Brands", (id, activa, motivo, clave) => new SetBrandActiveCommand(id, activa, motivo) { OperationKey = clave });

        g.MapPlantilla(Ver, Importar, PlantillaDeMarcas.Clave, "Inventory_Brands",
            importar: (modo, archivo, motivo, clave) => new ImportBrandsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetBrandsTemplateDataQuery(),
            permisoDeExportacion: Exportar);
    }

    // -------------------------------------------------------------------------------------- grupos contables --

    private static void GruposContables(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "accounting-groups", "Inventory Accounting Groups");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccountingGroupsQuery(includeInactive ?? false), ct))
            .WithName("Inventory_AccountingGroups_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (GrupoContableRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateAccountingGroupCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Description)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/accounting-groups/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_AccountingGroups_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, GrupoContableRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateAccountingGroupCommand(id, body.Name ?? string.Empty, body.Description) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_AccountingGroups_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_AccountingGroups", (id, activa, motivo, clave) => new SetAccountingGroupActiveCommand(id, activa, motivo) { OperationKey = clave });

        g.MapPlantilla(Ver, Importar, PlantillaDeGruposContables.Clave, "Inventory_AccountingGroups",
            importar: (modo, archivo, motivo, clave) => new ImportAccountingGroupsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetAccountingGroupsTemplateDataQuery(),
            permisoDeExportacion: Exportar);
    }

    // --------------------------------------------------------------------------------------------- productos --

    private static void Productos(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "products", "Inventory Products");

        g.MapGet("/", async (string? search, Guid? categoryPublicId, Guid? brandPublicId, Guid? accountingGroupPublicId,
                    ProductKind? kind, ProductStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListProductsQuery(search, categoryPublicId, brandPublicId, accountingGroupPublicId, kind, status,
                    new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Products_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        // La búsqueda va antes que /{id:guid}: «search» no es un Guid, pero el orden lo deja explícito.
        g.MapGet("/search", async (string? q, Guid? warehousePublicId, string? kinds, bool? includeInactive, int? take, bool? forSale, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new SearchProductsQuery(q ?? string.Empty, warehousePublicId, Clases(kinds), includeInactive ?? false, take)
                    { ForSale = forSale ?? false }, ct))
            .WithName("Inventory_Products_Search").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetProductQuery(id), ct))
            .WithName("Inventory_Products_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CrearProductoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateProductCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.ShortName, body.Description, body.Kind, body.CategoryPublicId,
                    body.BrandPublicId, body.BaseUnitPublicId, body.AccountingGroupPublicId, body.VatSaleTreatment, body.WithholdingConceptPublicId,
                    body.Reference, body.Weight, body.Volume, body.TracksLot ?? false, body.TracksSerial ?? false, body.TracksExpiry ?? false,
                    body.Units?.Select(u => new UnidadPedida(u.UnitPublicId, u.Factor, u.Usage)).ToList(),
                    body.Barcodes?.Select(b => new CodigoPedido(b.Barcode ?? string.Empty, b.UnitPublicId)).ToList(),
                    body.Taxes?.Select(t => new ImpuestoPedido(t.TaxDefinitionPublicId, t.TaxRatePublicId, t.TaxableUnitsPerBaseUnit)).ToList(),
                    body.IsPurchasable ?? true, body.IsSellable ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/products/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Products_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, EditarProductoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateProductCommand(
                    id, body.Name ?? string.Empty, body.ShortName, body.Description, body.CategoryPublicId, body.BrandPublicId, body.BaseUnitPublicId,
                    body.AccountingGroupPublicId, body.VatSaleTreatment, body.WithholdingConceptPublicId, body.Reference, body.Weight, body.Volume,
                    body.TracksLot ?? false, body.TracksSerial ?? false, body.TracksExpiry ?? false, body.IsPurchasable ?? true, body.IsSellable ?? true,
                    body.Kind)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Products_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/status", async (Guid id, EstadoDeProductoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetProductStatusCommand(id, body.Status, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Products_Status").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteProductCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Products_Delete").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // §3.6.4 Cambio de grupo contable (US3, T292).
        g.MapGet("/{id:guid}/accounting-group", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetProductAccountingGroupHistoryQuery(id), ct))
            .WithName("Inventory_Products_AccountingGroup_History").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/accounting-group", async (Guid id, CambioDeGrupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ChangeProductAccountingGroupCommand(id, body.AccountingGroupPublicId, body.EffectiveDate, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/products/{id}/accounting-group", r.Value) : r;
            })
            .WithName("Inventory_Products_AccountingGroup_Change").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Reclasificar);

        // §3.6.1 Unidades alternas.
        g.MapGet("/{id:guid}/units", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListProductUnitsQuery(id), ct))
            .WithName("Inventory_Products_Units_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/units", async (Guid id, UnidadAlternaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new AddProductUnitCommand(id, body.UnitPublicId, body.Factor, body.Usage, body.IsDefaultPurchase, body.IsDefaultSale)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/products/{id}/units/{r.Value.ProductUnitPublicId}", r.Value) : r;
            })
            .WithName("Inventory_Products_Units_Add").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}/units/{productUnitId:guid}", async (Guid id, Guid productUnitId, UnidadAlternaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateProductUnitCommand(id, productUnitId, body.Factor, body.Usage, body.IsDefaultPurchase, body.IsDefaultSale)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Products_Units_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapDelete("/{id:guid}/units/{productUnitId:guid}", async (Guid id, Guid productUnitId, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RemoveProductUnitCommand(id, productUnitId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Products_Units_Remove").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // §3.6.2 Códigos de barras.
        g.MapGet("/{id:guid}/barcodes", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListProductBarcodesQuery(id), ct))
            .WithName("Inventory_Products_Barcodes_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/barcodes", async (Guid id, CodigoDeBarrasRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new AddProductBarcodeCommand(id, body.Barcode ?? string.Empty, body.ProductUnitPublicId)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/products/{id}/barcodes/{r.Value.BarcodePublicId}", r.Value) : r;
            })
            .WithName("Inventory_Products_Barcodes_Add").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapDelete("/{id:guid}/barcodes/{barcodeId:guid}", async (Guid id, Guid barcodeId, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RemoveProductBarcodeCommand(id, barcodeId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Products_Barcodes_Remove").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // §3.6.3 Impuestos.
        g.MapGet("/{id:guid}/taxes", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetProductTaxesQuery(id), ct))
            .WithName("Inventory_Products_Taxes_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPut("/{id:guid}/taxes", async (Guid id, ImpuestosDeProductoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetProductTaxesCommand(id, body.VatSaleTreatment, body.WithholdingConceptPublicId,
                    (body.Taxes ?? []).Select(t => new ImpuestoPedido(t.TaxDefinitionPublicId, t.TaxRatePublicId, t.TaxableUnitsPerBaseUnit)).ToList())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Products_Taxes_Set").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // I6, US15 (T934): variantes de una plantilla y componentes de un combo o kit.
        g.MapGet("/{id:guid}/variants", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListProductVariantsQuery(id), ct))
            .WithName("Inventory_Products_Variants_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/variants", async (Guid id, GenerarVariantesRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new GenerateProductVariantsCommand(id,
                    (body.Attributes ?? []).Select(a => new AtributoElegido(a.AttributePublicId, a.ValuePublicIds ?? [])).ToList(),
                    body.Adjustments?.Select(a => new VarianteAjustada(a.VariantKey ?? string.Empty, a.Code, a.Name, a.Barcode)).ToList())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/products/{id}/variants", r.Value) : r;
            })
            .WithName("Inventory_Products_Variants_Generate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapGet("/{id:guid}/components", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetProductComponentsQuery(id), ct))
            .WithName("Inventory_Products_Components_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPut("/{id:guid}/components", async (Guid id, ComponentesRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetProductComponentsCommand(id,
                    (body.Components ?? []).Select(c => new ComponentePedido(c.ComponentProductPublicId, c.Quantity)).ToList())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Products_Components_Set").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPlantilla(Ver, Importar, PlantillaDeProductos.Clave, "Inventory_Products",
            importar: (modo, archivo, motivo, clave) => new ImportProductsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetProductsTemplateDataQuery(),
            permisoDeExportacion: Exportar);
    }

    /// <summary><c>kinds=Inventoriable,Service</c> (por nombre o número); vacío = todas.</summary>
    private static IReadOnlyList<ProductKind>? Clases(string? kinds) =>
        string.IsNullOrWhiteSpace(kinds)
            ? null
            : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => Enum.TryParse<ProductKind>(k, ignoreCase: true, out var v) && Enum.IsDefined(v) ? v : (ProductKind?)null)
                .OfType<ProductKind>().Distinct().ToList();

    // ------------------------------------------------------------------------------------ causas y canales --

    private static void CausasDeAjuste(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "adjustment-causes", "Inventory Adjustment Causes");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAdjustmentCausesQuery(includeInactive ?? false), ct))
            .WithName("Inventory_AdjustmentCauses_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CausaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateAdjustmentCauseCommand(body.Code ?? string.Empty, body.Name ?? string.Empty,
                    body.AllowsPositive ?? false, body.AllowsNegative ?? true, body.AllowsTransitWriteOff ?? false, body.RequiresAttachment ?? false)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/adjustment-causes/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_AdjustmentCauses_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, CausaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateAdjustmentCauseCommand(id, body.Name ?? string.Empty,
                    body.AllowsPositive ?? false, body.AllowsNegative ?? true, body.AllowsTransitWriteOff ?? false, body.RequiresAttachment ?? false)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_AdjustmentCauses_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_AdjustmentCauses", (id, activa, motivo, clave) => new SetAdjustmentCauseActiveCommand(id, activa, motivo) { OperationKey = clave });
    }

    private static void CanalesDeVenta(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "sales-channels", "Inventory Sales Channels");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSalesChannelsQuery(includeInactive ?? false), ct))
            .WithName("Inventory_SalesChannels_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CodigoYNombreRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateSalesChannelCommand(body.Code ?? string.Empty, body.Name ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/sales-channels/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_SalesChannels_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, CodigoYNombreRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateSalesChannelCommand(id, body.Name ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_SalesChannels_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        Estado(g, "Inventory_SalesChannels", (id, activa, motivo, clave) => new SetSalesChannelActiveCommand(id, activa, motivo) { OperationKey = clave });
    }

    // ------------------------------------------------------------------------------- catálogo avanzado (I6) --

    /// <summary>Los atributos de variante —talla, color— con sus valores (T934; <c>SaveVariantAttributeCommand</c> crea y cambia).</summary>
    private static void AtributosDeVariante(IEndpointRouteBuilder app)
    {
        var g = Grupo(app, "variant-attributes", "Inventory Variant Attributes");

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListVariantAttributesQuery(includeInactive ?? false), ct))
            .WithName("Inventory_VariantAttributes_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (AtributoDeVarianteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(Atributo(null, body, http), ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/variant-attributes/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_VariantAttributes_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, AtributoDeVarianteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(Atributo(id, body, http), ct))
            .WithName("Inventory_VariantAttributes_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);
    }

    private static SaveVariantAttributeCommand Atributo(Guid? id, AtributoDeVarianteRequest body, HttpContext http) =>
        new(id, body.Code ?? string.Empty, body.Name ?? string.Empty,
            (body.Values ?? []).Select(v => new ValorDeAtributoPedido(v.Code ?? string.Empty, v.Name ?? string.Empty, v.SortOrder ?? 0)).ToList(),
            body.IsActive ?? true)
        {
            OperationKey = http.ClaveDeOperacion(),
        };

    /// <summary>Los lotes con existencia (FEFO, con el sugerido) y las series de un producto, con el alcance por bodega (T934).</summary>
    private static void LotesYSeries(IEndpointRouteBuilder app)
    {
        const string verExistencias = StockEndpoints.VerExistencias;

        Grupo(app, "lots", "Inventory Lots")
            .MapGet("/", async (Guid productPublicId, Guid? warehousePublicId, bool? includeExpired, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLotsQuery(productPublicId, warehousePublicId, includeExpired ?? false), ct))
            .WithName("Inventory_Lots_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(verExistencias);

        Grupo(app, "serials", "Inventory Serials")
            .MapGet("/", async (Guid productPublicId, Guid? warehousePublicId, bool? inStock, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSerialsQuery(productPublicId, warehousePublicId, inStock), ct))
            .WithName("Inventory_Serials_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(verExistencias);
    }

    /// <summary><c>POST /{id}/deactivate</c> y <c>/reactivate</c> con <c>{ reason }</c> (§3), con la clave de la operación.</summary>
    private static void Estado(RouteGroupBuilder g, string nombre,
        Func<Guid, bool, string, Guid, IRequest<IngenIA365ERP.Application.Common.Models.Result>> comando)
    {
        g.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(comando(id, false, body.Reason ?? string.Empty, http.ClaveDeOperacion()), ct))
            .WithName($"{nombre}_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/reactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(comando(id, true, body.Reason ?? string.Empty, http.ClaveDeOperacion()), ct))
            .WithName($"{nombre}_Reactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);
    }

    // ------------------------------------------------------------------------------------------------ cuerpos --

    public sealed record UnidadRequest(string? Code, string? Name, string? Symbol, int AllowedDecimals, string? DianUnitCode);

    public sealed record CategoriaRequest(string? Code, string? Name, Guid? ParentPublicId);

    public sealed record CodigoYNombreRequest(string? Code, string? Name);

    public sealed record GrupoContableRequest(string? Code, string? Name, string? Description);

    public sealed record CausaRequest(string? Code, string? Name, bool? AllowsPositive, bool? AllowsNegative, bool? AllowsTransitWriteOff, bool? RequiresAttachment);

    public sealed record MotivoRequest(string? Reason);

    public sealed record UnidadDelAltaRequest(Guid UnitPublicId, decimal Factor, ProductUnitUsage Usage);

    public sealed record CodigoDelAltaRequest(string? Barcode, Guid? UnitPublicId);

    public sealed record ImpuestoRequest(Guid TaxDefinitionPublicId, Guid? TaxRatePublicId, decimal? TaxableUnitsPerBaseUnit);

    /// <summary><c>CreateProductRequest</c> (§3.5).</summary>
    public sealed record CrearProductoRequest(
        string? Code, string? Name, string? ShortName, string? Description, ProductKind Kind, Guid CategoryPublicId, Guid? BrandPublicId,
        Guid BaseUnitPublicId, Guid? AccountingGroupPublicId, VatSaleTreatment VatSaleTreatment, Guid? WithholdingConceptPublicId,
        string? Reference, decimal? Weight, decimal? Volume, bool? TracksLot, bool? TracksSerial, bool? TracksExpiry,
        IReadOnlyList<UnidadDelAltaRequest>? Units, IReadOnlyList<CodigoDelAltaRequest>? Barcodes, IReadOnlyList<ImpuestoRequest>? Taxes,
        bool? IsPurchasable, bool? IsSellable);

    /// <summary>
    /// <c>UpdateProductRequest</c> (§3.5): sin código, unidades, códigos ni impuestos. <c>kind?</c> desde I6 (T918): nulo no cambia la clase;
    /// con movimientos o dependientes, <c>Inventory.Product.KindLocked</c>.
    /// </summary>
    public sealed record EditarProductoRequest(
        string? Name, string? ShortName, string? Description, Guid CategoryPublicId, Guid? BrandPublicId, Guid BaseUnitPublicId,
        Guid? AccountingGroupPublicId, VatSaleTreatment VatSaleTreatment, Guid? WithholdingConceptPublicId, string? Reference,
        decimal? Weight, decimal? Volume, bool? TracksLot, bool? TracksSerial, bool? TracksExpiry, bool? IsPurchasable, bool? IsSellable,
        ProductKind? Kind = null);

    /// <summary>Un valor de un atributo de variante: código (10), nombre y orden en la matriz (I6, T934).</summary>
    public sealed record ValorDeAtributoRequest(string? Code, string? Name, int? SortOrder);

    /// <summary>El cuerpo de <c>POST/PUT /variant-attributes</c> (I6, T934): los valores que no vienen se retiran.</summary>
    public sealed record AtributoDeVarianteRequest(string? Code, string? Name, IReadOnlyList<ValorDeAtributoRequest>? Values, bool? IsActive);

    /// <summary>Un atributo elegido para generar y los valores que se combinan.</summary>
    public sealed record AtributoElegidoRequest(Guid AttributePublicId, IReadOnlyList<Guid>? ValuePublicIds);

    /// <summary>Lo que se cambia de una variante propuesta antes de crearla, por su <c>variantKey</c>.</summary>
    public sealed record VarianteAjustadaRequest(string? VariantKey, string? Code, string? Name, string? Barcode);

    /// <summary>El cuerpo de <c>POST /products/{id}/variants</c> (I6, T934).</summary>
    public sealed record GenerarVariantesRequest(IReadOnlyList<AtributoElegidoRequest>? Attributes, IReadOnlyList<VarianteAjustadaRequest>? Adjustments);

    /// <summary>Un componente de un combo o kit y su cantidad en la unidad base del componente.</summary>
    public sealed record ComponenteRequest(Guid ComponentProductPublicId, decimal Quantity);

    /// <summary>El cuerpo de <c>PUT /products/{id}/components</c> (I6, T934): la lista nueva completa.</summary>
    public sealed record ComponentesRequest(IReadOnlyList<ComponenteRequest>? Components);

    public sealed record EstadoDeProductoRequest(ProductStatus Status, string? Reason);

    /// <summary>El cuerpo del cambio de grupo contable (§3.6.4): la fecha efectiva vacía es hoy.</summary>
    public sealed record CambioDeGrupoRequest(Guid AccountingGroupPublicId, DateOnly? EffectiveDate, string? Reason);

    public sealed record UnidadAlternaRequest(Guid UnitPublicId, decimal Factor, ProductUnitUsage Usage, bool? IsDefaultPurchase, bool? IsDefaultSale);

    public sealed record CodigoDeBarrasRequest(string? Barcode, Guid? ProductUnitPublicId);

    public sealed record ImpuestosDeProductoRequest(VatSaleTreatment VatSaleTreatment, Guid? WithholdingConceptPublicId, IReadOnlyList<ImpuestoRequest>? Taxes);
}
