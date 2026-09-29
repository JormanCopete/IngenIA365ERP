using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Components;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, I6, T909 (US15-2, US15-3; FR-036 fila «Ensamble», FR-044; contracts/api.md §10): el combo vendido y el ensamble de kits.
/// <list type="bullet">
/// <item>la venta de un combo deja una fila de kardex por componente con el mismo <c>DocumentLineId</c>, el costo de la línea es la suma
/// y <c>CostoDeVentaReconocido</c> va por componente; un componente sin existencia es <c>Inventory.Stock.Insufficient</c> nombrándolo;</item>
/// <item>el ensamble propone las líneas desde <c>INV_ProductComponents</c> × cantidad, rechaza lo que no es kit (<c>.NotAKit</c>) y el kit
/// sin componentes (<c>.ComponentsMissing</c>), hace entrar el kit al costo consumido y emite <c>AjusteInventarioAprobado</c> con
/// operación <c>Ensamble</c>; el monto que se aprueba es el valor al costo de lo consumido.</item>
/// </list>
/// </summary>
public class ComboYEnsambleTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    // ------------------------------------------------------------------------------------------------ combo --

    private static async Task<(VentasDePrueba V, Guid Combo)> ComboAsync(Guid? componenteExtra = null)
    {
        var v = await VentasDePrueba.CrearAsync();
        var combo = (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("CMB", "Combo desayuno", ProductKind.Combo))).PublicId;
        var componentes = new List<ComponentePedido> { new(v.P1, 1m), new(v.P3, 2m) };
        if (componenteExtra is { } extra) componentes.Add(new ComponentePedido(extra, 1m));
        var fijado = await new SetProductComponentsCommandHandler(v.Db, v.Compras.C.Reloj).Handle(new SetProductComponentsCommand(combo, componentes), default);
        fijado.IsSuccess.Should().BeTrue(fijado.IsFailure ? fijado.Error.Message : string.Empty);
        var general = await v.Db.PriceLists.Where(l => l.Code == "GENERAL").Select(l => l.PublicId).SingleAsync();
        await new SetPriceListItemsCommandHandler(v.Db).Handle(new SetPriceListItemsCommand(general,
            [new PriceListItemInput(combo, v.Unidad(combo), 20_000m)], "Precio del combo"), default);
        v.Db.ChangeTracker.Clear();
        return (v, combo);
    }

    private static async Task<(Guid Documento, Result<ConfirmationResultDto> Confirmacion)> VenderAsync(VentasDePrueba v, Guid producto, decimal cantidad)
    {
        var linea = v.Linea(producto, cantidad);
        var borrador = await v.GuardarAsync(v.Venta(lineas: [linea]));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? borrador.Error.Message : string.Empty);
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        (await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, total)], lineas: [linea]), borrador.Value.PublicId)).IsSuccess.Should().BeTrue();
        return (borrador.Value.PublicId, await v.ConfirmarAsync(borrador.Value.PublicId));
    }

    [Fact]
    public async Task La_venta_de_un_combo_saca_cada_componente_con_la_misma_linea_y_el_costo_de_venta_es_la_suma()
    {
        var (v, combo) = await ComboAsync();

        var (documento, r) = await VenderAsync(v, combo, 2m);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var venta = v.Documento(documento);
        var linea = venta.Lines.Single(l => !l.IsDeleted);
        var filas = await v.Db.KardexEntries.Where(k => k.DocumentId == venta.Id).OrderBy(k => k.ProductId).ToListAsync();
        filas.Should().OnlyContain(k => k.DocumentLineId == linea.Id, "las salidas de los componentes son de la línea del combo");
        filas.Select(k => (k.ProductId, k.QuantityBase)).Should().BeEquivalentTo(new[]
        {
            (v.K.ProductoId(v.P1), -2m), (v.K.ProductoId(v.P3), -4m),
        });
        linea.TotalCost.Should().Be(2m * 1_000m + 4m * 6_000m, "el costo de la línea es la suma de sus componentes");
        linea.UnitCost.Should().Be(13_000m);
        (await v.Db.StockBalances.AnyAsync(s => s.ProductId == linea.ProductId)).Should().BeFalse("el combo no tiene existencia propia");

        var costo = System.Text.Json.JsonDocument.Parse(v.ContenidoDe(documento, CostoDeVentaReconocidoV1.Type)).RootElement;
        costo.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("cost").GetDecimal()).Should().Be(26_000m);
    }

    [Fact]
    public async Task Un_componente_sin_existencia_es_Stock_Insufficient_nombrando_al_componente()
    {
        var v = await VentasDePrueba.CrearAsync();
        var vacio = (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("SINEX", "Sin existencia"))).PublicId;
        v.Db.ChangeTracker.Clear();
        var (v2, combo) = (v, (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("CMB2", "Combo", ProductKind.Combo))).PublicId);
        await new SetProductComponentsCommandHandler(v2.Db, v2.Compras.C.Reloj).Handle(new SetProductComponentsCommand(combo, [new(v2.P1, 1m), new(vacio, 1m)]), default);
        var general = await v2.Db.PriceLists.Where(l => l.Code == "GENERAL").Select(l => l.PublicId).SingleAsync();
        await new SetPriceListItemsCommandHandler(v2.Db).Handle(new SetPriceListItemsCommand(general, [new PriceListItemInput(combo, v2.Unidad(combo), 5_000m)], "p"), default);
        v2.Db.ChangeTracker.Clear();

        var (_, r) = await VenderAsync(v2, combo, 1m);

        Codigo(r).Should().Be("Inventory.Stock.Insufficient");
        r.Error.Message.Should().Contain("SINEX");
    }

    [Fact]
    public async Task El_disponible_de_un_combo_es_el_de_su_componente_mas_escaso_y_la_busqueda_de_venta_no_trae_plantillas()
    {
        // I6, T927: 100 del P1 (uno por combo) y 50 del P3 (dos por combo) dan 25 combos.
        var (v, combo) = await ComboAsync();
        var plantilla = (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("CMBT", "Combo plantilla", ProductKind.Template))).PublicId;
        var buscar = new IngenIA365ERP.Application.Inventory.Catalog.Products.SearchProductsQueryHandler(v.Db, v.K.Alcance, v.K.Permisos,
            new ExistenciasEnKardex(v.Db));

        var r = await buscar.Handle(new IngenIA365ERP.Application.Inventory.Catalog.Products.SearchProductsQuery("combo", v.K.Principal.PublicId) { ForSale = true }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Items.Select(i => i.PublicId).Should().Contain(combo).And.NotContain(plantilla);
        r.Value.Items.Single(i => i.PublicId == combo).Available.Should().Be(25m);
    }

    // --------------------------------------------------------------------------------------------- ensamble --

    private static EfectosDeClase EfectosConEnsamble(KardexDePrueba k)
    {
        var registro = k.Registro();
        var reversion = new ReversionDeKardex(k.C.Db, registro);
        var emision = new EmisionDeInventario(k.C.Db);
        var maestros = k.Maestros();
        return new EfectosDeClase(
        [
            new EfectoDeAjustePositivo(registro, reversion, emision, maestros, k.Permisos, k.C.Db),
            new EfectoDeEnsamble(registro, reversion, emision, maestros, k.C.Db),
        ], EntregaDelComercio.I6);
    }

    private static async Task<(KardexDePrueba K, Guid Kit)> KitAsync(bool conComponentes = true)
    {
        var k = await KardexDePrueba.CrearAsync();
        var kit = (await k.C.ProductoAsync(k.C.Alta("KIT", "Kit escolar", ProductKind.Kit))).PublicId;
        if (conComponentes)
            await new SetProductComponentsCommandHandler(k.C.Db, k.C.Reloj).Handle(new SetProductComponentsCommand(kit, [new(k.P1, 2m), new(k.P2, 1m)]), default);
        await k.EntradaAsync(k.P1, 20m, 1_000m);
        await k.EntradaAsync(k.P2, 10m, 3_000m);
        k.C.Db.ChangeTracker.Clear();
        return (k, kit);
    }

    private static Task<Result<InventoryDocumentDto>> GuardarEnsambleAsync(KardexDePrueba k, Guid kit, decimal cantidad) =>
        k.Guardar(EfectosConEnsamble(k)).Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            k.Borrador("ENS", lineas: []) with { Assembly = new AssemblyRequest(kit, cantidad) }), default);

    [Fact]
    public async Task El_ensamble_propone_los_componentes_y_el_kit_entra_al_costo_consumido()
    {
        var (k, kit) = await KitAsync();

        var guardado = await GuardarEnsambleAsync(k, kit, 5m);
        guardado.IsSuccess.Should().BeTrue(guardado.IsFailure ? $"{guardado.Error.Code}: {guardado.Error.Message}" : string.Empty);
        var borrador = await k.C.Db.InventoryDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == guardado.Value.PublicId);
        borrador.Lines.OrderBy(l => l.LineNumber).Select(l => (l.ProductId, l.QuantityBase)).Should().Equal(
            (k.ProductoId(kit), 5m), (k.ProductoId(k.P1), 10m), (k.ProductoId(k.P2), 5m));

        var r = await k.ConfirmarAsync(guardado.Value.PublicId, EfectosConEnsamble(k));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var kitId = k.ProductoId(kit);
        var entrada = await k.C.Db.KardexEntries.SingleAsync(e => e.ProductId == kitId);
        (entrada.QuantityBase, entrada.UnitCost, entrada.TotalCost).Should().Be((5m, 5_000m, 25_000m), "10 × 1.000 + 5 × 3.000 = 25.000 entre 5 kits");
        (await k.C.Db.StockBalances.SingleAsync(s => s.ProductId == kitId)).Physical.Should().Be(5m);

        var mensaje = await k.C.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == guardado.Value.PublicId);
        mensaje.Type.Should().Be(AjusteInventarioAprobadoV1.Type);
        mensaje.PayloadJson.Should().Contain("\"operation\":\"Ensamble\"");
        var lineas = System.Text.Json.JsonDocument.Parse(mensaje.PayloadJson).RootElement.GetProperty("lines").EnumerateArray().ToList();
        lineas.Select(l => (l.GetProperty("movement").GetString(), l.GetProperty("cost").GetDecimal()))
            .Should().BeEquivalentTo(new[] { ("Exit", 25_000m), ("Entry", 25_000m) }, "salen los componentes y entra el kit por lo mismo");

        await k.Motor.Received().EvaluarAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateOnly>(), 25_000m, "Inventory.Adjustments.Confirm", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lo_que_no_es_kit_es_NotAKit_y_un_kit_sin_componentes_es_ComponentsMissing()
    {
        var (k, _) = await KitAsync(conComponentes: false);
        var sinComponentes = await k.C.Db.Products.Where(p => p.Code == "KIT").Select(p => p.PublicId).SingleAsync();

        Codigo(await GuardarEnsambleAsync(k, k.P1, 1m)).Should().Be("Inventory.Assembly.NotAKit");
        Codigo(await GuardarEnsambleAsync(k, sinComponentes, 1m)).Should().Be("Inventory.Assembly.ComponentsMissing");
    }

    [Fact]
    public async Task Un_ensamble_sin_componentes_suficientes_no_mueve_nada()
    {
        var (k, kit) = await KitAsync();
        var guardado = await GuardarEnsambleAsync(k, kit, 11m);

        var r = await k.ConfirmarAsync(guardado.Value.PublicId, EfectosConEnsamble(k));

        Codigo(r).Should().Be("Inventory.Stock.Insufficient", "11 kits piden 22 del P1 y hay 20");
        (await k.C.Db.KardexEntries.CountAsync(e => e.DocumentId == k.C.Db.InventoryDocuments.Single(d => d.PublicId == guardado.Value.PublicId).Id))
            .Should().Be(0);
    }
}
