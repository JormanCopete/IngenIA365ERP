using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, I5, T771 (US13-3; FR-046, FR-075; contracts/api.md §14.9, §14.10; data-model §9.7; mensajes.md §6.10; decisiones-transversales
/// §3 T42e): los costos adicionales (<c>LandedCost</c>) sobre el ciclo común real.
/// <list type="bullet">
/// <item>el borrador devuelve <c>allocations[]</c> y <c>roundingResidue</c> (US13-3: $100.000 por valor sobre $600.000 y $400.000 reparte
/// $60.000 y $40.000);</item>
/// <item>la factura del flete sin servicio → <c>Inventory.LandedCost.InvoiceNotService</c>; más de lo que queda sin repartir →
/// <c>.ExceedsInvoice</c> con <c>data.available</c>; una recepción sin confirmar → <c>Inventory.Purchase.ReceiptNotConfirmed</c>;</item>
/// <item>al confirmar, las filas de <c>INV_LandedCostAllocations</c>, las líneas <c>CostAdjustment</c> <c>LandedCost</c> con
/// <c>AffectsEntryId</c> partidas entre existencia y vendido (D5) y un <c>AjusteDeCostoReconocido</c> por recepción afectada que sigue el
/// destino del mensaje de esa recepción;</item>
/// <item>anular una recepción con costos adicionales vigentes → <c>Inventory.Document.HasDependents</c> (T801); anular los costos
/// adicionales deja las líneas contrarias.</item>
/// </list>
/// </summary>
public class CostosAdicionalesTests
{
    private static async Task<ComprasDePrueba> CrearAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Entrega = EntregaDelComercio.I5;
        return c;
    }

    /// <summary>Una recepción confirmada de <paramref name="cantidad"/> unidades de P1 a <paramref name="precio"/>.</summary>
    private static Task<InventoryDocumentDto> RecibirAsync(ComprasDePrueba c, decimal cantidad, decimal precio, string remision) =>
        c.ConfirmadoAsync(c.Recepcion(remision: remision, lineas: [c.Linea(c.P1, cantidad, precio)]));

    /// <summary>La factura del flete (un servicio) del proveedor B, confirmada.</summary>
    private static async Task<InventoryDocumentDto> FleteAsync(ComprasDePrueba c, decimal valor, string numero = "FL-1")
    {
        var servicio = await c.ServicioAsync();
        return await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento(numero), c.ProveedorB, c.Linea(servicio, 1m, valor)));
    }

    private static SaveInventoryDraftRequest Costos(ComprasDePrueba c, Guid factura, IReadOnlyList<Guid> recepciones,
        LandedCostAllocationMethod metodo = LandedCostAllocationMethod.Value, decimal? monto = null, IReadOnlyList<ManualAllocationRequest>? manuales = null) =>
        new(c.Tipo("CAD"), null, null, null, null, null, null, null, null, null, null, null, null, [],
            SupplierInvoicePublicId: factura, ReceiptPublicIds: recepciones, Amount: monto, Method: metodo, ManualAllocations: manuales);

    private static string Mensaje(Application.Common.Models.Result r) => r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty;

    private static GetPurchaseDocumentQueryHandler Detalle(ComprasDePrueba c) =>
        new(c.C.Db, c.K.Vista(), c.Vinculos(), c.Calculo(), c.Pendientes(), c.Costos());

    // ------------------------------------------------------------------------------------------------ borrador --

    [Fact]
    public async Task El_borrador_reparte_el_flete_por_valor_y_devuelve_las_porciones()
    {
        var c = await CrearAsync();
        var a = await RecibirAsync(c, 600m, 1_000m, "REM-1");
        var b = await RecibirAsync(c, 400m, 1_000m, "REM-2");
        var flete = await FleteAsync(c, 100_000m);

        var r = await c.GuardarAsync(Costos(c, flete.PublicId, [a.PublicId, b.PublicId]));

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        var costos = r.Value.LandedCost!;
        costos.Method.Should().Be(LandedCostAllocationMethod.Value);
        costos.Amount.Should().Be(100_000m, "sin monto se reparte lo que queda de la factura");
        costos.Available.Should().Be(100_000m);
        costos.RoundingResidue.Should().Be(0m);
        costos.Allocations.Select(x => x.Allocated).Should().Equal(60_000m, 40_000m);
        costos.Allocations.Select(x => x.Basis).Should().Equal(600_000m, 400_000m);
        costos.Allocations.Should().OnlyContain(x => x.ToCostOfSales == 0m && x.ToInventory == x.Allocated, "todo sigue en existencia");
        costos.Allocations[0].ReceiptLine.ReceiptPublicId.Should().Be(a.PublicId);
        r.Value.Lines.Should().HaveCount(2, "una línea por línea de recepción, armadas por el servidor");
        r.Value.Totals.Total.Should().Be(100_000m);

        var doc = c.Documento(r.Value.PublicId);
        doc.CounterpartyPersonId.Should().Be(c.ProveedorB.Id, "la contraparte es el proveedor del flete");
        doc.WarehouseId.Should().BeNull();
        (await c.C.Db.LandedCostAllocations.CountAsync(f => f.DocumentId == doc.Id)).Should().Be(2, "la propuesta guarda el método");

        // El detalle devuelve el mismo reparto.
        var detalle = await Detalle(c).Handle(new GetPurchaseDocumentQuery(r.Value.PublicId), default);
        detalle.Value.Document.LandedCost!.Allocations.Select(x => x.Allocated).Should().Equal(60_000m, 40_000m);
    }

    [Fact]
    public async Task El_residuo_del_redondeo_queda_visible_y_la_suma_es_exacta()
    {
        var c = await CrearAsync();
        var r1 = await RecibirAsync(c, 1m, 1_000m, "REM-1");
        var r2 = await RecibirAsync(c, 1m, 1_000m, "REM-2");
        var r3 = await RecibirAsync(c, 1m, 1_000m, "REM-3");
        var flete = await FleteAsync(c, 100m);

        var r = await c.GuardarAsync(Costos(c, flete.PublicId, [r1.PublicId, r2.PublicId, r3.PublicId], LandedCostAllocationMethod.Quantity));

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        var costos = r.Value.LandedCost!;
        costos.Allocations.Sum(x => x.Allocated).Should().Be(100m);
        costos.RoundingResidue.Should().Be(0.01m);
        costos.Allocations.Should().ContainSingle(x => x.RoundingResidue == 0.01m && x.Allocated == 33.34m);
    }

    [Fact]
    public async Task La_factura_del_flete_sin_servicio_no_sirve()
    {
        var c = await CrearAsync();
        var recepcion = await RecibirAsync(c, 10m, 1_000m, "REM-1");
        var mercancia = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento("M-1")));

        var r = await c.GuardarAsync(Costos(c, mercancia.PublicId, [recepcion.PublicId]));

        r.Error.Code.Should().Be("Inventory.LandedCost.InvoiceNotService");
    }

    [Fact]
    public async Task Una_recepcion_sin_confirmar_no_recibe_costos()
    {
        var c = await CrearAsync();
        var borrador = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 10m, 1_000m)]));
        var flete = await FleteAsync(c, 10_000m);

        var r = await c.GuardarAsync(Costos(c, flete.PublicId, [borrador.Value.PublicId]));

        r.Error.Code.Should().Be("Inventory.Purchase.ReceiptNotConfirmed");
    }

    [Fact]
    public async Task No_se_reparte_mas_de_lo_que_queda_de_la_factura()
    {
        var c = await CrearAsync();
        var recepcion = await RecibirAsync(c, 100m, 1_000m, "REM-1");
        var flete = await FleteAsync(c, 100_000m);

        var primero = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId], monto: 70_000m));
        (await c.ConfirmarAsync(primero.Value.PublicId)).IsSuccess.Should().BeTrue();

        // Sin monto propone lo que queda.
        var resto = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId]));
        resto.Value.LandedCost!.Amount.Should().Be(30_000m);
        resto.Value.LandedCost.Available.Should().Be(30_000m);

        // De más: el borrador lo avisa y la confirmación lo rechaza con lo disponible.
        var demas = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId], monto: 40_000m));
        demas.IsSuccess.Should().BeTrue();
        demas.Value.Warnings.Should().Contain(w => w.Code == "Inventory.LandedCost.ExceedsInvoice");
        var confirmado = await c.ConfirmarAsync(demas.Value.PublicId);
        confirmado.Error.Code.Should().Be("Inventory.LandedCost.ExceedsInvoice");
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(confirmado.Error)).Should().Contain("\"available\":30000");
    }

    [Fact]
    public async Task Por_peso_sin_peso_en_el_producto_no_se_confirma()
    {
        var c = await CrearAsync();
        var recepcion = await RecibirAsync(c, 10m, 1_000m, "REM-1");
        var flete = await FleteAsync(c, 5_000m);

        var r = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId], LandedCostAllocationMethod.Weight));

        r.IsSuccess.Should().BeTrue("el borrador se guarda con el aviso");
        r.Value.Warnings.Should().Contain(w => w.Code == "Inventory.LandedCost.BasisMissing");
        r.Value.LandedCost!.Allocations.Should().BeEmpty();
        (await c.ConfirmarAsync(r.Value.PublicId)).Error.Code.Should().Be("Inventory.LandedCost.BasisMissing");
    }

    [Fact]
    public async Task A_mano_lo_digitado_tiene_que_sumar_el_monto()
    {
        var c = await CrearAsync();
        var recepcion = await RecibirAsync(c, 10m, 1_000m, "REM-1");
        var linea = (await c.LineasAsync(recepcion.PublicId)).Single().PublicId;
        var flete = await FleteAsync(c, 5_000m);

        var descuadrado = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId], LandedCostAllocationMethod.Manual,
            manuales: [new ManualAllocationRequest(linea, 4_000m)]));
        descuadrado.Value.Warnings.Should().Contain(w => w.Code == "Inventory.LandedCost.ManualNotBalanced");
        (await c.ConfirmarAsync(descuadrado.Value.PublicId)).Error.Code.Should().Be("Inventory.LandedCost.ManualNotBalanced");

        var cuadrado = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId], LandedCostAllocationMethod.Manual,
            manuales: [new ManualAllocationRequest(linea, 5_000m)]) with { RowVersion = null }, descuadrado.Value.PublicId);
        cuadrado.IsSuccess.Should().BeTrue(Mensaje(cuadrado));
        (await c.ConfirmarAsync(cuadrado.Value.PublicId)).IsSuccess.Should().BeTrue("lo digitado queda guardado en la propuesta");
        c.C.Db.LandedCostAllocations.Single(f => f.DocumentId == c.Documento(cuadrado.Value.PublicId).Id).AllocatedAmount.Should().Be(5_000m);
    }

    // ---------------------------------------------------------------------------------------------- confirmar --

    [Fact]
    public async Task Al_confirmar_el_flete_se_parte_entre_existencia_y_vendido_con_un_ajuste_por_recepcion()
    {
        var c = await CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync(); // 36 unidades a 1.300
        var salida = await c.K.AjusteAsync(c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.P1, 24m)]));
        salida.Confirmacion.IsSuccess.Should().BeTrue();
        var flete = await FleteAsync(c, 1_200m);

        var guardado = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId]));
        var r = await c.ConfirmarAsync(guardado.Value.PublicId);

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        var doc = c.Documento(guardado.Value.PublicId);

        var fila = await c.C.Db.LandedCostAllocations.SingleAsync(f => f.DocumentId == doc.Id);
        fila.AllocatedAmount.Should().Be(1_200m);
        fila.ExistingRatio.Should().Be(0.333333m, "quedan 12 de las 36 recibidas (D5)");
        fila.ExistingAmount.Should().Be(400m);
        fila.SoldAmount.Should().Be(800m);
        fila.AllocationMethod.Should().Be(LandedCostAllocationMethod.Value);

        var entrada = await c.C.Db.KardexEntries.SingleAsync(k => k.DocumentId == c.Documento(recepcion.PublicId).Id && k.Kind == KardexEntryKind.Entry);
        var ajustes = await c.C.Db.KardexEntries.Where(k => k.DocumentId == doc.Id).OrderBy(k => k.Id).ToListAsync();
        ajustes.Should().OnlyContain(k => k.Kind == KardexEntryKind.CostAdjustment && k.Reason == KardexReason.LandedCost
            && k.AffectsEntryId == entrada.Id && k.QuantityBase == 0m);
        ajustes.Select(k => k.TotalCost).Should().Equal(1_200m, -800m);

        var estado = await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1));
        estado.Quantity.Should().Be(12m);
        estado.Value.Should().Be(12m * 1_300m + 400m);

        c.MensajesDe(doc.PublicId).Should().Equal("AjusteDeCostoReconocido");
        var mensaje = await c.C.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == doc.PublicId);
        mensaje.OriginEventKey.Should().Be($"Confirmation:{recepcion.PublicId:N}");
        var ajuste = JsonNode.Parse(mensaje.PayloadJson)!;
        ajuste["reason"]!.ToString().Should().BeOneOf("LandedCost", ((int)KardexReason.LandedCost).ToString());
        ajuste["affectedDocument"]!["publicId"]!.GetValue<Guid>().Should().Be(recepcion.PublicId);
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(400m);
        ajuste["lines"]![0]!["soldAmount"]!.GetValue<decimal>().Should().Be(800m);

        // Sigue el destino del mensaje de su recepción.
        var deLaRecepcion = await c.C.Db.IntegrationMessageDeliveries
            .Where(d => c.C.Db.IntegrationMessages.Any(m => m.Id == d.MessageId && m.OriginPublicId == recepcion.PublicId)).Select(d => d.Mode).FirstAsync();
        (await c.C.Db.IntegrationMessageDeliveries.Where(d => d.MessageId == mensaje.Id).Select(d => d.Mode).FirstAsync()).Should().Be(deLaRecepcion);
    }

    [Fact]
    public async Task Dos_recepciones_dejan_dos_ajustes_uno_por_recepcion()
    {
        var c = await CrearAsync();
        var a = await RecibirAsync(c, 600m, 1_000m, "REM-1");
        var b = await RecibirAsync(c, 400m, 1_000m, "REM-2");
        var flete = await FleteAsync(c, 100_000m);

        var guardado = await c.GuardarAsync(Costos(c, flete.PublicId, [a.PublicId, b.PublicId]));
        (await c.ConfirmarAsync(guardado.Value.PublicId)).IsSuccess.Should().BeTrue();

        var claves = await c.C.Db.IntegrationMessages.Where(m => m.OriginPublicId == guardado.Value.PublicId).OrderBy(m => m.Id)
            .Select(m => m.OriginEventKey).ToListAsync();
        claves.Should().Equal($"Confirmation:{a.PublicId:N}", $"Confirmation:{b.PublicId:N}");
        (await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1))).Value.Should().Be(1_100_000m);
    }

    // ------------------------------------------------------------------------------------------------ anular --

    [Fact]
    public async Task La_recepcion_con_costos_adicionales_no_se_anula_y_anularlos_deja_las_lineas_contrarias()
    {
        var c = await CrearAsync();
        var recepcion = await RecibirAsync(c, 10m, 1_000m, "REM-1");
        var flete = await FleteAsync(c, 2_000m);
        var guardado = await c.GuardarAsync(Costos(c, flete.PublicId, [recepcion.PublicId]));
        (await c.ConfirmarAsync(guardado.Value.PublicId)).IsSuccess.Should().BeTrue();

        // T801: los costos adicionales vigentes son dependientes de la recepción.
        var bloqueada = await c.Anular().Handle(new VoidInventoryDocumentCommand(recepcion.PublicId, DocumentClassGroup.Purchases, "error de digitación"), default);
        bloqueada.Error.Code.Should().Be("Inventory.Document.HasDependents");
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(bloqueada.Error)).Should().Contain(guardado.Value.PublicId.ToString());

        // Anular los costos adicionales: las líneas contrarias, el valor vuelve y un ajuste con el signo contrario.
        var anulado = await c.Anular().Handle(new VoidInventoryDocumentCommand(guardado.Value.PublicId, DocumentClassGroup.Purchases, "flete de otra compra"), default);
        anulado.IsSuccess.Should().BeTrue(Mensaje(anulado));
        var contrario = c.Documento(anulado.Value.VoidingDocumentPublicId);
        var lineas = await c.C.Db.KardexEntries.Where(k => k.DocumentId == contrario.Id).ToListAsync();
        lineas.Should().ContainSingle(k => k.Reason == KardexReason.LandedCost && k.TotalCost == -2_000m);
        (await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1))).Value.Should().Be(10_000m);
        var ajuste = JsonNode.Parse(c.ContenidoDe(contrario.PublicId, "AjusteDeCostoReconocido"))!;
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(-2_000m);

        // Ya sin dependientes, la recepción se anula; y la factura vuelve a tener todo por repartir.
        (await c.Anular().Handle(new VoidInventoryDocumentCommand(recepcion.PublicId, DocumentClassGroup.Purchases, "error de digitación"), default))
            .IsSuccess.Should().BeTrue();
        var otra = await RecibirAsync(c, 5m, 1_000m, "REM-2");
        (await c.GuardarAsync(Costos(c, flete.PublicId, [otra.PublicId]))).Value.LandedCost!.Available.Should().Be(2_000m);
    }

    [Fact]
    public async Task Los_campos_de_costos_adicionales_no_aplican_a_otras_clases()
    {
        var c = await CrearAsync();
        var r = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m)]) with { Method = LandedCostAllocationMethod.Value });
        r.Error.Code.Should().Be("Validation.Invalid");
    }
}
