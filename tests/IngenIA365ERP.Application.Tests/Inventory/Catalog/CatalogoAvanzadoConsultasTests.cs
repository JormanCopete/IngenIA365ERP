using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Components;
using IngenIA365ERP.Application.Inventory.Catalog.Lots;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Catalog.Variants;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, I6, T934 (US15; FR-023, FR-026, FR-009; contracts/api.md §3.5, §5, §6.1): lo que las rutas del catálogo avanzado leen.
/// <list type="bullet">
/// <item><c>GET /api/inventory/lots</c>: los lotes con existencia del producto, en orden FEFO (vence primero, sin vencimiento al final), con el
/// sugerido, el estado (vigente, próximo a vencer según <c>Informes.DiasProximoAVencer</c>, vencido) y los vencidos sólo con
/// <c>includeExpired</c>; una bodega fuera del alcance es 404 y sin bodega suma las del alcance;</item>
/// <item><c>GET /api/inventory/serials</c>: las series del producto con su bodega y lote, <c>inStock</c> filtra, y la de una bodega fuera del
/// alcance no se ve;</item>
/// <item><c>ProductDto</c> con <c>parent</c>, <c>variantValues[]</c> y <c>components[]</c>; <c>ProductStockDto.byLocation[].lot</c>;</item>
/// <item>la vista <c>kardex</c> con la columna «Lote/serie» y el filtro <c>lot</c>, con el saldo del lote.</item>
/// </list>
/// Hoy es el 25 de septiembre de 2026. (nuevo)
/// </summary>
public class CatalogoAvanzadoConsultasTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static async Task<Guid> ConLoteAsync(KardexDePrueba k, string codigo = "LCH") =>
        (await k.C.ProductoAsync(k.C.Alta(codigo, "Leche entera") with { TracksLot = true, TracksExpiry = true })).PublicId;

    private static async Task<Guid> ConSerieAsync(KardexDePrueba k, string codigo = "CEL") =>
        (await k.C.ProductoAsync(k.C.Alta(codigo, "Celular") with { TracksSerial = true })).PublicId;

    private static SaveInventoryDraftLine ConLote(KardexDePrueba k, Guid producto, decimal cantidad, string lote, DateOnly? vence) =>
        k.Linea(producto, cantidad, 1_000m) with { LotCode = lote, ExpiryDate = vence };

    private static async Task EntradaAsync(KardexDePrueba k, Domain.Entities.Inventory.Warehousing.Warehouse? bodega, params SaveInventoryDraftLine[] lineas)
    {
        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", bodega, lineas: lineas));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    private static ListLotsQueryHandler Lotes(KardexDePrueba k) => new(k.C.Db, k.Alcance, k.Lector(), k.C.Reloj);

    private static ListSerialsQueryHandler Series(KardexDePrueba k) => new(k.C.Db, k.Alcance);

    private static async Task<(KardexDePrueba K, Guid Leche)> TresLotesAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Entrega = EntregaDelComercio.I6;
        var leche = await ConLoteAsync(k);
        await EntradaAsync(k, null,
            ConLote(k, leche, 5m, "LARGO", Hoy.AddDays(60)),
            ConLote(k, leche, 3m, "CORTO", Hoy.AddDays(10)),
            ConLote(k, leche, 2m, "VIEJO", Hoy.AddDays(-3)));
        return (k, leche);
    }

    // ------------------------------------------------------------------------------------------------ lotes --

    [Fact]
    public async Task Los_lotes_salen_en_orden_FEFO_con_el_sugerido_y_su_estado_y_sin_los_vencidos()
    {
        var (k, leche) = await TresLotesAsync();

        var r = await Lotes(k).Handle(new ListLotsQuery(leche, k.Principal.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Select(l => (l.Code, l.Quantity, l.State, l.Suggested)).Should().Equal(
            ("CORTO", 3m, EstadosDeLote.ProximoAVencer, true),
            ("LARGO", 5m, EstadosDeLote.Vigente, false));
    }

    [Fact]
    public async Task Con_includeExpired_vienen_tambien_los_vencidos_marcados_y_nunca_sugeridos()
    {
        var (k, leche) = await TresLotesAsync();

        var r = await Lotes(k).Handle(new ListLotsQuery(leche, k.Principal.PublicId, IncludeExpired: true), default);

        r.Value.Select(l => (l.Code, l.State, l.Suggested)).Should().Equal(
            ("VIEJO", EstadosDeLote.Vencido, false),
            ("CORTO", EstadosDeLote.ProximoAVencer, true),
            ("LARGO", EstadosDeLote.Vigente, false));
    }

    [Fact]
    public async Task Sin_bodega_suma_las_del_alcance_y_una_bodega_fuera_del_alcance_es_404()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);
        await EntradaAsync(k, null, ConLote(k, leche, 4m, "L1", Hoy.AddDays(40)));
        await EntradaAsync(k, k.Segunda, ConLote(k, leche, 6m, "L1", Hoy.AddDays(40)));

        (await Lotes(k).Handle(new ListLotsQuery(leche), default)).Value.Single().Quantity.Should().Be(10m);

        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Principal.Id }, k.Principal.Id, false, new HashSet<int>(), null));
        (await Lotes(k).Handle(new ListLotsQuery(leche), default)).Value.Single().Quantity.Should().Be(4m, "sólo la bodega del alcance");
        (await Lotes(k).Handle(new ListLotsQuery(leche, k.Segunda.PublicId), default)).Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Un_producto_inexistente_es_404()
    {
        var k = await KardexDePrueba.CrearAsync();

        (await Lotes(k).Handle(new ListLotsQuery(Guid.NewGuid()), default)).Error.Code.Should().Be("Inventory.Product.NotFound");
        (await Series(k).Handle(new ListSerialsQuery(Guid.NewGuid()), default)).Error.Code.Should().Be("Inventory.Product.NotFound");
    }

    // ------------------------------------------------------------------------------------------------ series --

    [Fact]
    public async Task Las_series_dicen_donde_estan_y_inStock_filtra()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, null, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-1" }, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-2" });
        var (_, sale) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(celular, 1m) with { SerialNumber = "SN-2" }]));
        sale.IsSuccess.Should().BeTrue(sale.IsFailure ? sale.Error.Message : string.Empty);

        var todas = await Series(k).Handle(new ListSerialsQuery(celular), default);
        todas.Value.Select(s => (s.SerialNumber, s.InStock, s.Warehouse?.Code)).Should().Equal(("SN-1", true, "PRIN"), ("SN-2", false, null));

        var enExistencia = await Series(k).Handle(new ListSerialsQuery(celular, k.Principal.PublicId, InStock: true), default);
        enExistencia.Value.Select(s => s.SerialNumber).Should().Equal("SN-1");
        (await Series(k).Handle(new ListSerialsQuery(celular, InStock: false), default)).Value.Select(s => s.SerialNumber).Should().Equal("SN-2");
    }

    [Fact]
    public async Task La_serie_de_una_bodega_fuera_del_alcance_no_se_ve()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, k.Segunda, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-B2" });
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Principal.Id }, k.Principal.Id, false, new HashSet<int>(), null));

        (await Series(k).Handle(new ListSerialsQuery(celular), default)).Value.Should().BeEmpty();
        (await Series(k).Handle(new ListSerialsQuery(celular, k.Segunda.PublicId), default)).Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    // ------------------------------------------------------------------------------------ ProductDto ampliado --

    [Fact]
    public async Task El_producto_trae_su_plantilla_sus_valores_de_variante_y_sus_componentes()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var talla = (await new SaveVariantAttributeCommandHandler(c.Db, c.Reloj)
            .Handle(new SaveVariantAttributeCommand(null, "TALLA", "Talla", [new("S", "Pequeña", 1)]), default)).Value;
        var plantilla = await c.ProductoAsync(c.Alta("CAM", "Camisa", ProductKind.Template));
        var generadas = await new GenerateProductVariantsCommandHandler(c.Db)
            .Handle(new GenerateProductVariantsCommand(plantilla.PublicId, [new(talla.PublicId, talla.Values.Select(v => v.PublicId).ToList())]), default);
        generadas.IsSuccess.Should().BeTrue(generadas.IsFailure ? generadas.Error.Message : string.Empty);
        var a = await c.ProductoAsync(c.Alta("A", "Café"));
        var combo = await c.ProductoAsync(c.Alta("CMB", "Desayuno", ProductKind.Combo));
        (await new SetProductComponentsCommandHandler(c.Db, c.Reloj).Handle(new SetProductComponentsCommand(combo.PublicId, [new(a.PublicId, 2m)]), default))
            .IsSuccess.Should().BeTrue();
        c.Olvidar();
        var leer = new GetProductQueryHandler(c.Db, c.Reloj);

        var variante = (await leer.Handle(new GetProductQuery(generadas.Value.Created.Single().PublicId), default)).Value;
        variante.Parent.Should().NotBeNull();
        variante.Parent!.Code.Should().Be("CAM");
        variante.VariantValues.Select(v => (v.AttributeCode, v.ValueCode)).Should().Equal(("TALLA", "S"));
        variante.Components.Should().BeEmpty();

        var elCombo = (await leer.Handle(new GetProductQuery(combo.PublicId), default)).Value;
        elCombo.Parent.Should().BeNull();
        elCombo.Components.Select(x => (x.Component.Code, x.Quantity)).Should().Equal(("A", 2m));
    }

    // ---------------------------------------------------------------------------------------- existencias --

    [Fact]
    public async Task La_existencia_por_ubicacion_dice_el_lote()
    {
        var (k, leche) = await TresLotesAsync();

        var r = await new GetProductStockQueryHandler(k.C.Db, k.Alcance, k.Permisos, new PosicionDeReposicion(k.C.Db),
            new ValorDeExistencias(k.C.Db, k.Lector(), k.C.Reloj)).Handle(new GetProductStockQuery(leche), default);

        r.Value.ByLocation.Select(l => (l.Lot, l.Quantity)).Should().BeEquivalentTo(new[] { ("CORTO", 3m), ("LARGO", 5m), ("VIEJO", 2m) });
    }

    // ------------------------------------------------------------------------------------------ kardex --

    [Fact]
    public async Task El_kardex_muestra_el_lote_y_filtra_por_el_con_su_saldo()
    {
        var (k, leche) = await TresLotesAsync();
        var (_, salida) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(leche, 1m) with { LotCode = "CORTO" }]));
        salida.IsSuccess.Should().BeTrue(salida.IsFailure ? salida.Error.Message : string.Empty);
        var filtros = new FiltrosDeInformeDeInventario { Product = leche, From = Hoy, To = Hoy };
        var informe = new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj);

        var todo = await informe.Handle(new KardexReportQuery(filtros), default);
        todo.Value.Filas.Skip(1).Select(f => f.Valores[6]).Should().Equal("LARGO", "CORTO", "VIEJO", "CORTO");

        var corto = await informe.Handle(new KardexReportQuery(filtros, Lot: "corto"), default);
        corto.IsSuccess.Should().BeTrue(corto.IsFailure ? corto.Error.Message : string.Empty);
        corto.Value.Filas.Skip(1).Select(f => f.Valores[6]).Should().Equal("CORTO", "CORTO");
        corto.Value.Filas[^1].Valores[9].Should().Be(2m, "entraron 3 y salió 1 del lote");

        (await informe.Handle(new KardexReportQuery(filtros, Lot: "NOEXISTE"), default)).Error.Code.Should().Be("Inventory.Lot.NotFound");
    }

    [Fact]
    public async Task El_kardex_de_un_producto_con_serie_muestra_la_serie()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, null, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-7" });

        var r = await new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj)
            .Handle(new KardexReportQuery(new FiltrosDeInformeDeInventario { Product = celular, From = Hoy, To = Hoy }), default);

        r.Value.Filas[^1].Valores[6].Should().Be("SN-7");
    }

    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";
}
