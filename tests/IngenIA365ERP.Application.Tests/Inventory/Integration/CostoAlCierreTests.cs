using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Inventory;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// 2026-11-18 (pedido del dueño, <c>Contabilidad.CostoDeVenta = AlCierreDelPeriodo</c>): el costo que las ventas dejan para el cierre del mes se
/// suma en un solo <see cref="CostoDelPeriodoReconocidoV1"/> y Contabilidad lo desglosa con las mismas reglas que si hubiera viajado con cada
/// venta: cambia cuándo pasa, no el asiento.
/// </summary>
public class CostoAlCierreTests
{
    private static readonly DateOnly Fin = new(2026, 11, 30);
    private static readonly Guid Norte = EscenarioContable.Norte;
    private static readonly Guid Principal = EscenarioContable.Principal;

    private static CostoDeVentaReconocidoV1 Costo(decimal costo, string grupo = "ABARROTES", string bodega = "B01", decimal cantidad = 1m) => new()
    {
        PointOfSaleCode = "P1",
        Lines = [new CostLineV1 { AccountingGroupCode = grupo, WarehouseCode = bodega, Movement = KardexEntryKind.Exit, QuantityBase = cantidad, Cost = costo, DocumentLines = [1] }],
    };

    private static AjusteDeCostoReconocidoV1 Ajuste(decimal vendido, KardexReason motivo) => new()
    {
        Reason = motivo, EffectiveDate = new DateOnly(2026, 11, 10), AffectedDocument = new DocumentRefV1 { PublicId = Guid.NewGuid(), Number = "POS-1" },
        Lines = [new CostDifferenceLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", InventoryAmount = 0m, SoldAmount = vendido }],
    };

    private static CostoAlCierre.ParteDiferida Fila(object contenido, Guid? sucursal = null, Guid? centro = null) =>
        new(CatalogoDeMensajesV1.Buscar(contenido.GetType())!.Type, 1, sucursal ?? Principal, centro, contenido);

    [Fact]
    public void Las_ventas_del_mismo_grupo_bodega_y_sucursal_se_suman_en_una_linea()
    {
        var partes = CostoAlCierre.Sumar([Fila(Costo(1000m)), Fila(Costo(250m, cantidad: 2m)), Fila(Costo(400m, grupo: "ASEO"))], Fin, negar: false);

        partes.Should().ContainSingle();
        var costo = (CostoDeVentaReconocidoV1)partes[0].Content!;
        costo.Lines.Should().HaveCount(2);
        costo.Lines.Single(l => l.AccountingGroupCode == "ABARROTES").Should().Match<CostLineV1>(l => l.Cost == 1250m && l.QuantityBase == 3m && l.DocumentLines.Count == 0);
        costo.Lines.Single(l => l.AccountingGroupCode == "ASEO").Cost.Should().Be(400m);
    }

    [Fact]
    public void Otra_sucursal_u_otro_centro_es_otra_parte()
    {
        var centro = Guid.NewGuid();
        var partes = CostoAlCierre.Sumar([Fila(Costo(1000m)), Fila(Costo(1000m), Norte), Fila(Costo(1000m), centro: centro)], Fin, negar: false);

        partes.Should().HaveCount(3);
        partes.Select(p => (p.BranchPublicId, p.CostCenterPublicId)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void La_venta_y_su_anulacion_se_cancelan_y_no_dejan_parte()
    {
        var venta = Costo(1000m);
        var partes = CostoAlCierre.Sumar([Fila(venta), Fila(CostoAlCierre.Negar(venta))], Fin, negar: false);

        partes.Should().BeEmpty();
    }

    [Fact]
    public void Los_ajustes_se_suman_por_motivo_y_quedan_fechados_al_fin_del_mes()
    {
        var partes = CostoAlCierre.Sumar(
            [Fila(Ajuste(20m, KardexReason.NegativeRegularization)), Fila(Ajuste(5m, KardexReason.NegativeRegularization)), Fila(Ajuste(-7m, KardexReason.Retroactive))],
            Fin, negar: false);

        partes.Should().HaveCount(2);
        var regularizacion = partes.Select(p => (AjusteDeCostoReconocidoV1)p.Content!).Single(a => a.Reason == KardexReason.NegativeRegularization);
        regularizacion.EffectiveDate.Should().Be(Fin);
        regularizacion.Lines.Should().ContainSingle().Which.SoldAmount.Should().Be(25m);
    }

    [Fact]
    public void La_reversion_lleva_los_importes_con_el_signo_contrario()
    {
        var partes = CostoAlCierre.Sumar([Fila(Costo(1000m)), Fila(Ajuste(20m, KardexReason.NegativeRegularization))], Fin, negar: true);

        partes.Select(p => p.Content).OfType<CostoDeVentaReconocidoV1>().Single().Lines.Single().Cost.Should().Be(-1000m);
        partes.Select(p => p.Content).OfType<AjusteDeCostoReconocidoV1>().Single().Lines.Single().SoldAmount.Should().Be(-20m);
    }

    [Fact]
    public void Solo_el_costo_propio_de_un_documento_con_parte_comercial_se_puede_diferir()
    {
        CostoAlCierre.EsCostoPropio(Costo(1m)).Should().BeTrue();
        CostoAlCierre.EsCostoPropio(new DevolucionRegistradaV1 { Operation = "DevolucionDeCliente" }).Should().BeTrue();
        CostoAlCierre.EsCostoPropio(new DevolucionRegistradaV1 { Operation = "DevolucionAProveedor" }).Should().BeFalse();

        CostoAlCierre.TieneParteComercial([Costo(1m)]).Should().BeFalse("una remisión sólo lleva el costo: se queda con su documento");
        CostoAlCierre.TieneParteComercial([new VentaFacturadaV2(), Costo(1m)]).Should().BeTrue();
    }

    [Fact]
    public void El_contenido_guardado_se_lee_igual()
    {
        var venta = Costo(1234.5m);
        var json = JsonSerializer.Serialize(venta, OpcionesDeMensajes.Opciones);

        var leido = CostoAlCierre.Leer(CostoDeVentaReconocidoV1.Type, 1, json);

        leido.Should().BeEquivalentTo(venta);
    }

    // ------------------------------------------------------------------------------ lo que ve Contabilidad --

    [Fact]
    public void Contabilidad_desglosa_el_costo_del_periodo_como_las_ventas_que_lo_forman()
    {
        var a = Costo(1000m);
        var b = Costo(250m, grupo: "ASEO");
        var ajuste = Ajuste(20m, KardexReason.NegativeRegularization);

        var porVenta = new[] { (object)a, b, ajuste }
            .SelectMany(c => ConstructorDeLineasDeInventario.Desglosar([Mensaje(c)]).Partidas).ToList();
        var periodo = ConstructorDeLineasDeInventario.Desglosar([Periodo(CostoAlCierre.Sumar([Fila(a), Fila(b), Fila(ajuste)], Fin, negar: false))]);

        periodo.Fallos.Should().BeEmpty();
        Saldos(periodo.Partidas).Should().BeEquivalentTo(Saldos(porVenta));
    }

    [Fact]
    public void La_reversion_de_la_reapertura_deja_el_asiento_contrario()
    {
        var filas = new[] { Fila(Costo(1000m)), Fila(Ajuste(20m, KardexReason.Retroactive)) };
        var cierre = ConstructorDeLineasDeInventario.Desglosar([Periodo(CostoAlCierre.Sumar(filas, Fin, negar: false))]);
        var reversion = ConstructorDeLineasDeInventario.Desglosar([Periodo(CostoAlCierre.Sumar(filas, Fin, negar: true), reversion: true)]);

        Saldos(reversion.Partidas).Should().BeEquivalentTo(Saldos(cierre.Partidas).ToDictionary(s => s.Key, s => -s.Value));
    }

    [Fact]
    public void El_mensaje_leido_de_la_base_se_desglosa_igual_que_el_armado_en_memoria()
    {
        var partes = CostoAlCierre.Sumar([Fila(Costo(1000m)), Fila(Costo(300m), Norte)], Fin, negar: false);
        var enMemoria = Periodo(partes);
        var json = JsonSerializer.Serialize((CostoDelPeriodoReconocidoV1)enMemoria.Contenido!, OpcionesDeMensajes.Opciones);
        var leido = JsonSerializer.Deserialize<CostoDelPeriodoReconocidoV1>(json, OpcionesDeMensajes.Opciones)!;

        var desdeLaBase = ConstructorDeLineasDeInventario.Desglosar([new MensajeDeUnidad(enMemoria.Sobre, leido)]);

        Saldos(desdeLaBase.Partidas).Should().BeEquivalentTo(Saldos(ConstructorDeLineasDeInventario.Desglosar([enMemoria]).Partidas));
    }

    [Fact]
    public void El_tipo_de_comprobante_sale_de_la_operacion_del_costo_de_venta()
    {
        var periodo = Periodo(CostoAlCierre.Sumar([Fila(Costo(1000m))], Fin, negar: false));

        IngenIA365ERP.Application.Accounting.Inventory.Reglas.TiposDeComprobanteDeInventario.OperacionDe([(periodo.Sobre, periodo.Contenido)])!
            .Should().Be(new IngenIA365ERP.Application.Accounting.Inventory.Reglas.OperacionDeLaUnidad("CostoDeVenta", null));
    }

    private static MensajeDeUnidad Mensaje(object contenido)
    {
        var tipo = CatalogoDeMensajesV1.Buscar(contenido.GetType())!.Type;
        var sobre = EscenarioContable.Sobre(tipo, clase: "PosEquivalentDocument", tipoDoc: "POS", numero: "POS-1", fecha: Fin);
        return new MensajeDeUnidad(sobre with { Payload = contenido }, contenido);
    }

    private static MensajeDeUnidad Periodo(IReadOnlyList<DeferredCostPartV1> partes, bool reversion = false)
    {
        var contenido = new CostoDelPeriodoReconocidoV1 { Year = 2026, Month = 11, ClosingVersion = 1, IsReversal = reversion, Parts = partes };
        var sobre = EscenarioContable.Sobre(CostoDelPeriodoReconocidoV1.Type, fecha: Fin) with
        {
            Origin = new MessageOriginV1 { Kind = Domain.Enums.Integration.MessageOriginKind.Operation, PublicId = Guid.NewGuid(), Number = "2026-11", OperationDate = Fin },
        };
        return new MensajeDeUnidad(sobre with { Payload = contenido }, contenido);
    }

    /// <summary>Lo que cada rol recibe, por lo que busca en la matriz (grupo, bodega, sucursal, centro, motivo): el neto de las partidas.</summary>
    private static Dictionary<string, decimal> Saldos(IEnumerable<ConstructorDeLineasDeInventario.Partida> partidas) =>
        partidas.GroupBy(p => $"{p.Operacion}|{p.Rol}|{p.Valores.AccountingGroupCode}|{p.Valores.WarehouseCode}|{p.Valores.BranchPublicId}|{p.Valores.CostCenterPublicId}|{p.Valores.ReasonCode}")
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Neto))
            .Where(kv => kv.Value != 0m)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
}
