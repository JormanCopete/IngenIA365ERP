using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Transfers;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, I6, T908 (FR-026; US15-4, US15-5; data-model §1.11, §13; SC-006): lotes, vencimientos y series por el ciclo común.
/// <list type="bullet">
/// <item>toda entrada de un producto con lote exige el lote (y su vencimiento si lo controla); el lote nace con la primera entrada y un
/// mismo código con otra fecha es <c>Inventory.Lot.ExpiryMismatch</c>;</item>
/// <item>la salida sin lote toma el que vence primero y se reparte entre lotes; vender un lote vencido con <c>Bloquear</c> es
/// <c>Inventory.Lot.Expired</c> y con <c>Advertir</c> confirma con aviso; la baja sí saca el vencido;</item>
/// <item>serie: una línea por unidad, no entra la que ya está (<c>.AlreadyInStock</c>) ni sale la que no está en la bodega (<c>.NotInStock</c>);</item>
/// <item>el traslado lleva lote y serie al tránsito y al destino, y la existencia por lote y la proyección de la serie cuadran con el
/// kardex en <see cref="VerificacionDeIntegridad"/>.</item>
/// </list>
/// Hoy es el 25 de septiembre de 2026.
/// </summary>
public class LotesYSeriesTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    private static async Task<Guid> ConLoteAsync(KardexDePrueba k, string codigo = "LCH", bool vencimiento = true) =>
        (await k.C.ProductoAsync(k.C.Alta(codigo, "Leche entera") with { TracksLot = true, TracksExpiry = vencimiento })).PublicId;

    private static async Task<Guid> ConSerieAsync(KardexDePrueba k, string codigo = "CEL") =>
        (await k.C.ProductoAsync(k.C.Alta(codigo, "Celular") with { TracksSerial = true })).PublicId;

    private static SaveInventoryDraftLine ConLote(KardexDePrueba k, Guid producto, decimal cantidad, string lote, DateOnly? vence, decimal? costo = 1_000m) =>
        k.Linea(producto, cantidad, costo) with { LotCode = lote, ExpiryDate = vence };

    private static async Task EntradaAsync(KardexDePrueba k, params SaveInventoryDraftLine[] lineas)
    {
        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", lineas: lineas));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    // ------------------------------------------------------------------------------------------ entradas --

    [Fact]
    public async Task Una_entrada_de_un_producto_con_lote_sin_lote_es_Lot_Required_y_el_borrador_lo_avisa()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);

        var guardado = await k.GuardarAsync(k.Borrador("AJP", lineas: [k.Linea(leche, 5m, 1_000m)]));
        guardado.IsSuccess.Should().BeTrue("el aviso no detiene el guardado");
        guardado.Value.Warnings.Select(w => w.Code).Should().Contain("Inventory.Lot.Required");

        Codigo(await k.ConfirmarAsync(guardado.Value.PublicId)).Should().Be("Inventory.Lot.Required");
    }

    [Fact]
    public async Task El_lote_nace_con_la_primera_entrada_y_el_mismo_codigo_con_otra_fecha_es_ExpiryMismatch()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);

        await EntradaAsync(k, ConLote(k, leche, 5m, "l-001", Hoy.AddDays(30)));

        var lote = await k.C.Db.Lots.SingleAsync();
        (lote.Code, lote.ExpiryDate).Should().Be(("L-001", Hoy.AddDays(30)), "el código se guarda en mayúsculas y con su vencimiento");
        (await k.C.Db.KardexEntries.SingleAsync()).LotId.Should().Be(lote.Id);
        (await k.C.Db.StockDetails.SingleAsync(s => s.LotId == lote.Id)).Quantity.Should().Be(5m);

        await EntradaAsync(k, ConLote(k, leche, 2m, "L-001", Hoy.AddDays(30)));
        (await k.C.Db.Lots.CountAsync()).Should().Be(1, "la segunda entrada reutiliza el lote");

        var otraFecha = await k.GuardarAsync(k.Borrador("AJP", lineas: [ConLote(k, leche, 1m, "L-001", Hoy.AddDays(60))]));
        Codigo(otraFecha).Should().Be("Inventory.Lot.ExpiryMismatch");
    }

    [Fact]
    public async Task Un_producto_con_vencimiento_exige_la_fecha_del_lote_nuevo()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);

        Codigo(await k.GuardarAsync(k.Borrador("AJP", lineas: [ConLote(k, leche, 1m, "L-9", null)]))).Should().Be("Inventory.Lot.ExpiryRequired");
    }

    // ------------------------------------------------------------------------------------------- salidas --

    [Fact]
    public async Task La_salida_sin_lote_toma_el_que_vence_primero_y_se_reparte_entre_lotes()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);
        await EntradaAsync(k, ConLote(k, leche, 5m, "LARGO", Hoy.AddDays(60)), ConLote(k, leche, 3m, "CORTO", Hoy.AddDays(10)));

        var (documento, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(leche, 5m)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var id = (await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == documento)).Id;
        var lotes = await k.C.Db.Lots.ToDictionaryAsync(l => l.Id, l => l.Code);
        var salidas = await k.C.Db.KardexEntries.Where(e => e.DocumentId == id).OrderBy(e => e.Id).ToListAsync();
        salidas.Select(e => (lotes[e.LotId!.Value], e.QuantityBase)).Should().Equal(("CORTO", -3m), ("LARGO", -2m));
        (await k.C.Db.StockDetails.Where(s => s.LotId != null).SumAsync(s => s.Quantity)).Should().Be(3m);
        (await k.C.Db.InventoryDocumentLines.SingleAsync(l => l.DocumentId == id)).TotalCost.Should().Be(5_000m, "el costo de la línea suma sus lotes");
    }

    [Fact]
    public async Task La_baja_saca_un_lote_vencido_sin_politica()
    {
        var k = await KardexDePrueba.CrearAsync();
        var leche = await ConLoteAsync(k);
        await EntradaAsync(k, ConLote(k, leche, 4m, "VIEJO", Hoy.AddDays(-5)));

        var (_, r) = await k.AjusteAsync(k.Borrador("BAJ", causa: k.Causa("DANO"), lineas: [k.Linea(leche, 4m)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await k.C.Db.StockDetails.Where(s => s.LotId != null).SumAsync(s => s.Quantity)).Should().Be(0m);
    }

    private static async Task<(VentasDePrueba V, Guid Leche)> VentaConLoteVencidoAsync(string? politica = null)
    {
        var v = await VentasDePrueba.CrearAsync();
        if (politica is not null)
        {
            v.K.Entrega = EntregaDelComercio.I6;
            v.Parametro(ParametrosDeInventario.VentasLoteVencido, politica);
        }
        var leche = await ConLoteAsync(v.K);
        var general = await v.Db.PriceLists.Where(l => l.Code == "GENERAL").Select(l => l.PublicId).SingleAsync();
        await new SetPriceListItemsCommandHandler(v.Db).Handle(new SetPriceListItemsCommand(general,
            [new PriceListItemInput(leche, v.Unidad(leche), 3_000m)], "Precio"), default);
        await EntradaAsync(v.K, ConLote(v.K, leche, 5m, "VENCIDO", Hoy.AddDays(-2)));
        return (v, leche);
    }

    private static async Task<Result<ConfirmationResultDto>> VenderAsync(VentasDePrueba v, SalesLineInput linea)
    {
        var borrador = await v.GuardarAsync(v.Venta(lineas: [linea]));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? borrador.Error.Message : string.Empty);
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        (await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, total)], lineas: [linea]), borrador.Value.PublicId)).IsSuccess.Should().BeTrue();
        return await v.ConfirmarAsync(borrador.Value.PublicId);
    }

    [Fact]
    public async Task Vender_con_solo_un_lote_vencido_y_Bloquear_es_Lot_Expired()
    {
        var (v, leche) = await VentaConLoteVencidoAsync();

        var sugerido = await VenderAsync(v, v.Linea(leche, 1m));
        Codigo(sugerido).Should().Be("Inventory.Lot.Expired", "el único lote que alcanza está vencido");

        var elegido = await VenderAsync(v, v.Linea(leche, 1m) with { LotCode = "VENCIDO" });
        Codigo(elegido).Should().Be("Inventory.Lot.Expired");
    }

    [Fact]
    public async Task Vender_un_lote_vencido_con_Advertir_confirma_con_aviso()
    {
        var (v, leche) = await VentaConLoteVencidoAsync("Advertir");

        var r = await VenderAsync(v, v.Linea(leche, 1m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Warnings.Select(w => w.Code).Should().Contain("Inventory.Lot.Expired");
    }

    [Fact]
    public async Task La_tarea_diaria_alerta_una_vez_cada_lote_con_existencia_proximo_a_vencer()
    {
        // I6, T932 (FR-022, FR-026; §2.13): DedupKey = ProximoAVencer:{lote}:{bodega}.
        var k = await KardexDePrueba.CrearAsync();
        k.Entrega = EntregaDelComercio.I6;
        var leche = await ConLoteAsync(k);
        await EntradaAsync(k, ConLote(k, leche, 2m, "PRONTO", Hoy.AddDays(10)), ConLote(k, leche, 2m, "LEJOS", Hoy.AddDays(90)));
        var alertas = new AlertasDePrueba();

        var r = await new IngenIA365ERP.Application.Inventory.Catalog.Lots.RaiseExpiringLotAlertsCommandHandler(k.C.Db, k.Lector(), alertas, k.C.Reloj)
            .Handle(new IngenIA365ERP.Application.Inventory.Catalog.Lots.RaiseExpiringLotAlertsCommand(), default);

        r.Value.Should().Be(1);
        var lote = await k.C.Db.Lots.SingleAsync(l => l.Code == "PRONTO");
        var alerta = alertas.Levantadas.Should().ContainSingle().Which;
        alerta.TypeCode.Should().Be("Inventario.ProximoAVencer");
        alerta.DedupKey.Should().Be($"ProximoAVencer:{lote.PublicId}:{k.Principal.PublicId}");
        new IngenIA365ERP.Application.Inventory.Catalog.Lots.TareaDeLotesProximosAVencer(EntregaDelComercio.I5).DebeCorrer(DateTimeOffset.Now, null)
            .Should().BeFalse("antes de I6 no hay lotes");
    }

    // --------------------------------------------------------------------------------------------- series --

    [Fact]
    public async Task Una_serie_es_una_unidad_y_no_entra_si_ya_esta_en_existencia()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);

        await EntradaAsync(k, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "sn-1" });
        var serie = await k.C.Db.Serials.SingleAsync();
        (serie.SerialNumber, serie.InStockWarehouseId).Should().Be(("SN-1", k.Principal.Id));

        Codigo(await k.GuardarAsync(k.Borrador("AJP", lineas: [k.Linea(celular, 2m, 500_000m) with { SerialNumber = "SN-2" }])))
            .Should().Be("Inventory.Serial.QuantityNotOne");
        Codigo(await k.GuardarAsync(k.Borrador("AJP", lineas: [k.Linea(celular, 1m, 500_000m)]))).Should().Be("(éxito)", "el borrador se guarda con el aviso");

        var (_, repetida) = await k.AjusteAsync(k.Borrador("AJP", lineas: [k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-1" }]));
        Codigo(repetida).Should().Be("Inventory.Serial.AlreadyInStock");
    }

    [Fact]
    public async Task Sacar_una_serie_que_no_esta_en_la_bodega_es_NotInStock()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-1" });

        var (_, enOtraBodega) = await k.AjusteAsync(k.Borrador("AJN", k.Segunda, k.Causa(), lineas: [k.Linea(celular, 1m) with { SerialNumber = "SN-1" }]));
        Codigo(enOtraBodega).Should().Be("Inventory.Serial.NotInStock");

        var (_, sale) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(celular, 1m) with { SerialNumber = "SN-1" }]));
        sale.IsSuccess.Should().BeTrue(sale.IsFailure ? sale.Error.Message : string.Empty);
        (await k.C.Db.Serials.SingleAsync()).InStockWarehouseId.Should().BeNull("salió de existencia");

        var (_, otraVez) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(celular, 1m) with { SerialNumber = "SN-1" }]));
        Codigo(otraVez).Should().Be("Inventory.Serial.NotInStock");
    }

    // ------------------------------------------------------------------------------ traslado e integridad --

    [Fact]
    public async Task El_traslado_lleva_lote_y_serie_al_transito_y_al_destino_y_todo_cuadra_con_el_kardex()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var k = t.K;
        var leche = await ConLoteAsync(k);
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, ConLote(k, leche, 6m, "L-A", Hoy.AddDays(20)), k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-9" });

        var (despacho, r) = await t.TrasladoAsync(t.PRIN, t.PV2, k.Linea(leche, 4m), k.Linea(celular, 1m) with { SerialNumber = "SN-9" });
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);

        var lote = await k.C.Db.Lots.SingleAsync();
        var transito = t.TR01.Id;
        (await k.C.Db.StockDetails.SingleAsync(s => s.WarehouseId == transito && s.LotId == lote.Id)).Quantity.Should().Be(4m);
        (await k.C.Db.Serials.SingleAsync()).InStockWarehouseId.Should().Be(transito, "la serie viaja por el tránsito");

        var recibido = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 4m, linea: 1), t.Recibida(despacho, 1m, linea: 2));
        recibido.IsSuccess.Should().BeTrue(recibido.IsFailure ? $"{recibido.Error.Code}: {recibido.Error.Message}" : string.Empty);
        (await k.C.Db.StockDetails.SingleAsync(s => s.WarehouseId == t.PV2.Id && s.LotId == lote.Id)).Quantity.Should().Be(4m);
        (await k.C.Db.Serials.SingleAsync()).InStockWarehouseId.Should().Be(t.PV2.Id);

        var verificacion = await new VerificacionDeIntegridad(k.C.Db).VerificarAsync(AlcanceDeVerificacion.Todo, default);
        verificacion.Incidentes.Should().BeEmpty("la existencia por lote y la proyección de la serie salen del kardex");
    }

    [Fact]
    public async Task Una_serie_mal_proyectada_es_un_incidente_de_la_verificacion()
    {
        var k = await KardexDePrueba.CrearAsync();
        var celular = await ConSerieAsync(k);
        await EntradaAsync(k, k.Linea(celular, 1m, 500_000m) with { SerialNumber = "SN-1" });
        var serie = await k.C.Db.Serials.SingleAsync();
        serie.InStockWarehouseId = null;
        await k.C.Db.SaveChangesAsync();

        var verificacion = await new VerificacionDeIntegridad(k.C.Db).VerificarAsync(AlcanceDeVerificacion.Todo, default);

        verificacion.Incidentes.Should().ContainSingle(i => i.Kind == TiposDeIncidente.Serial && i.Field == "InStockWarehouseId");
    }
}
