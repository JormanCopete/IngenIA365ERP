using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, US16 (T827–T832): lo que los casos dorados 09, 11, 16 y <c>peps-*</c> no cubren —la restricción D6 del
/// retroactivo a promedio ponderado, <c>SimularImpacto</c> sin retroactivo, el cambio de PEPS a promedio, el valorizado con
/// ajustes sobre una entrada y con historia que deja PEPS en negativo, las entidades de capa y consumo y las claves del
/// catálogo de costeo—.
/// </summary>
public class CosteoAvanzadoTests
{
    private static readonly ParametrosDeCosteo Promedio = new();
    private static readonly ParametrosDeCosteo Peps = new(CostMethod.Fifo);

    private static List<MovimientoRegistrado> HistoriaDeTresVentas() =>
    [
        new(1, 1, new DateOnly(2026, 3, 1), new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1000m), 10000m, 1000m, false),
        new(2, 2, new DateOnly(2026, 3, 10), new MovimientoDeCosto(-2m, ValoracionDelMovimiento.AlCostoVigente), -2000m, 1000m, true),
    ];

    // ------------------------------------------------------------------------------------------ retroactivo --

    [Fact]
    public void Un_retroactivo_con_PEPS_se_rechaza_por_D6()
    {
        var nuevo = new MovimientoRetroactivo(new DateOnly(2026, 3, 5), 3, new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1300m));

        var r = Retroactivo.Insertar(new PedidoRetroactivo(EstadoDeCosto.Vacio, HistoriaDeTresVentas(), [nuevo], Peps));

        r.Admitido.Should().BeFalse();
        r.Rechazo!.Codigo.Should().Be(Retroactivo.CodigoRequierePromedioPonderado);
        r.Rechazo.Codigo.Should().Be("Inventory.Costing.RetroactiveRequiresWeightedAverage");
        r.Ajustes.Should().BeEmpty();
        r.Nuevos.Should().BeEmpty();
    }

    [Fact]
    public void El_primer_movimiento_posterior_es_el_que_nombra_el_rechazo_del_parametro()
    {
        var historia = HistoriaDeTresVentas();

        Retroactivo.PrimerMovimientoPosterior(historia, new DateOnly(2026, 3, 5))!.EntryId.Should().Be(2);
        Retroactivo.PrimerMovimientoPosterior(historia, new DateOnly(2026, 3, 10)).Should().BeNull("a igual fecha el nuevo va después");
    }

    [Fact]
    public void SimularImpacto_sin_movimientos_posteriores_no_es_retroactivo_y_no_afecta_nada()
    {
        var nuevo = new MovimientoRetroactivo(new DateOnly(2026, 3, 11), 3, new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1300m));

        var impacto = MotorDeCosteo.SimularImpacto(new PedidoRetroactivo(EstadoDeCosto.Vacio, HistoriaDeTresVentas(), [nuevo], Promedio));

        impacto.EsRetroactivo.Should().BeFalse();
        impacto.Afectados.Should().BeEmpty();
        impacto.Total.Should().Be(0m);
        impacto.Rechazo.Should().BeNull();
        impacto.Resultado.EstadoFinal.Should().BeEquivalentTo(EstadoDeCosto.Con(18m, 21000m, 1300m));
    }

    [Fact]
    public void SimularImpacto_con_PEPS_devuelve_el_rechazo_D6()
    {
        var nuevo = new MovimientoRetroactivo(new DateOnly(2026, 3, 5), 3, new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1300m));

        var impacto = MotorDeCosteo.SimularImpacto(new PedidoRetroactivo(EstadoDeCosto.Vacio, HistoriaDeTresVentas(), [nuevo], Peps));

        impacto.EsRetroactivo.Should().BeTrue();
        impacto.Rechazo!.Codigo.Should().Be(Retroactivo.CodigoRequierePromedioPonderado);
        impacto.Afectados.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------- cambio de método --

    [Fact]
    public void Volver_de_PEPS_a_promedio_deja_la_linea_MethodChange_en_cero_y_sin_capas()
    {
        var compra = MotorDeCosteo.Aplicar(EstadoDeCosto.Vacio, new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1000m), Peps);
        var compra2 = MotorDeCosteo.Aplicar(compra.Estado, new MovimientoDeCosto(5m, ValoracionDelMovimiento.AlCostoIndicado, 1300m), Peps);

        var r = MotorDeCosteo.CambiarMetodo(compra2.Estado, CostMethod.WeightedAverage, RedondeoDeMontos.Centavo, new DateOnly(2026, 10, 1));

        var linea = r.Lineas.Should().ContainSingle().Subject;
        linea.Kind.Should().Be(KardexEntryKind.CostAdjustment);
        linea.Reason.Should().Be(KardexReason.MethodChange);
        linea.QuantityBase.Should().Be(0m);
        linea.TotalCost.Should().Be(0m);
        r.Estado.Capas.Should().BeEmpty();
        r.Estado.Quantity.Should().Be(15m);
        r.Estado.Value.Should().Be(16500m);
        r.Estado.AverageCost.Should().Be(1100m);
    }

    [Fact]
    public void A_PEPS_la_capa_unica_lleva_la_diferencia_de_redondeo_en_la_linea_MethodChange()
    {
        // 3 por 1.000: promedio 333,333333; la capa vale round(3 × 333,333333) = round(999,999999) = 1.000,00 = el valor del
        // ámbito, así que la línea MethodChange lleva 0. Si no coincidieran, la línea llevaría la diferencia.
        var estado = EstadoDeCosto.Con(3m, 1000m, 400m);
        var r = MotorDeCosteo.CambiarMetodo(estado, CostMethod.Fifo, RedondeoDeMontos.Centavo, new DateOnly(2026, 10, 1));

        var capa = r.Estado.Capas.Should().ContainSingle().Subject;
        capa.UnitCost.Should().Be(333.333333m);
        capa.Valor(RedondeoDeMontos.Centavo).Should().Be(1000m);
        r.Lineas.Single().TotalCost.Should().Be(0m);
        r.Estado.Value.Should().Be(capa.Valor(RedondeoDeMontos.Centavo), "Σ valor de las capas = valor del ámbito");
    }

    [Fact]
    public void A_PEPS_sin_existencia_no_hay_capa()
    {
        var r = MotorDeCosteo.CambiarMetodo(EstadoDeCosto.Con(0m, 0m, 900m), CostMethod.Fifo, RedondeoDeMontos.Centavo, new DateOnly(2026, 10, 1));

        r.Lineas.Should().ContainSingle().Which.Reason.Should().Be(KardexReason.MethodChange);
        r.Estado.Capas.Should().BeEmpty();
        r.CapasNuevas.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------- valorizado --

    [Fact]
    public void El_ajuste_sobre_una_entrada_va_en_PEPS_a_lo_que_queda_de_su_capa()
    {
        // 10 × 1.000 (E1); vende 6; flete de 1.000 sobre E1: PEPS le suma 1.000 × 4 / 10 = 400 a la capa (quedan 4);
        // promedio: 1.000 × min(1, 4 / 10) = 400. Valorizado al 01/10: 4.000 + 400 = 4.400 en los dos.
        var historia = new HistoriaParaValorizar(1, 0, 1, null,
        [
            new MovimientoAValorizar(1, new DateOnly(2026, 9, 1), ClaseAValorizar.Entrada, 10m, 1000m, 10000m, CostMethod.Fifo),
            new MovimientoAValorizar(2, new DateOnly(2026, 9, 2), ClaseAValorizar.Salida, -6m, 1000m, -6000m, CostMethod.Fifo),
            new MovimientoAValorizar(3, new DateOnly(2026, 9, 3), ClaseAValorizar.AjusteSobreEntrada, 0m, 0m, 1000m, CostMethod.Fifo, AffectsEntryId: 1),
            new MovimientoAValorizar(4, new DateOnly(2026, 9, 3), ClaseAValorizar.OtroAjuste, 0m, 0m, -600m, CostMethod.Fifo, AffectsEntryId: 1),
        ]);

        var r = ValorizacionPorDosMetodos.Calcular([historia], [new DateOnly(2026, 10, 1)], RedondeoDeMontos.Centavo);

        var producto = r.Productos.Single();
        producto.Peps.Should().Be(4400m, "registrado en PEPS: el valor del libro, Σ TotalCost");
        producto.PromedioPonderado.Should().Be(4400m);
        producto.Nota.Should().BeNull();
    }

    [Fact]
    public void Una_historia_que_deja_PEPS_en_negativo_no_se_calcula_y_lo_dice()
    {
        var historia = new HistoriaParaValorizar(1, 0, 1, null,
        [
            new MovimientoAValorizar(1, new DateOnly(2026, 9, 1), ClaseAValorizar.Entrada, 5m, 1000m, 5000m, CostMethod.WeightedAverage),
            new MovimientoAValorizar(2, new DateOnly(2026, 9, 2), ClaseAValorizar.Salida, -8m, 1000m, -8000m, CostMethod.WeightedAverage),
            new MovimientoAValorizar(3, new DateOnly(2026, 9, 3), ClaseAValorizar.Entrada, 10m, 1200m, 12000m, CostMethod.WeightedAverage),
        ]);

        var r = ValorizacionPorDosMetodos.Calcular([historia], [new DateOnly(2026, 10, 1)], RedondeoDeMontos.Centavo);

        var grupo = r.Grupos.Single();
        grupo.SinCalcular.Should().ContainSingle().Which.Nota.Should().Contain("negativ");
        grupo.PromedioPonderado.Should().Be(0m);
        grupo.Peps.Should().Be(0m);
    }

    [Fact]
    public void La_fecha_valora_lo_anterior_a_ella_y_el_grupo_sin_productos_sale_en_cero()
    {
        var historia = new HistoriaParaValorizar(1, 0, 7, null,
        [
            new MovimientoAValorizar(1, new DateOnly(2026, 9, 1), ClaseAValorizar.Entrada, 5m, 1000m, 5000m, CostMethod.WeightedAverage),
        ]);

        var r = ValorizacionPorDosMetodos.Calcular([historia], [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)], RedondeoDeMontos.Centavo);

        r.Grupos.Select(g => (g.Fecha, g.PromedioPonderado, g.Peps)).Should().Equal(
            (new DateOnly(2026, 9, 1), 0m, 0m),
            (new DateOnly(2026, 9, 2), 5000m, 5000m));
    }

    // ------------------------------------------------------------------------------------------- entidades --

    [Fact]
    public void La_capa_es_proyeccion_y_el_consumo_es_un_hecho()
    {
        typeof(CostLayer).IsDefined(typeof(SinDiffDeAuditoriaAttribute), inherit: false).Should().BeTrue();
        typeof(IHechoInmutable).IsAssignableFrom(typeof(CostLayer)).Should().BeFalse("la capa es una proyección reconstruible (FR-002)");
        typeof(AuditableEntityLong).IsAssignableFrom(typeof(LayerConsumption)).Should().BeTrue();
        typeof(IHechoInmutable).IsAssignableFrom(typeof(LayerConsumption)).Should().BeTrue();
        typeof(LayerConsumption).GetProperties()
            .Where(p => p.DeclaringType == typeof(LayerConsumption) && p.SetMethod is { IsPublic: true })
            .Should().OnlyContain(p => p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"), "un hecho sólo tiene init");
    }

    [Fact]
    public void La_capa_se_crea_desde_lo_que_propone_el_motor_y_el_consumo_con_su_capa()
    {
        var capa = CostLayer.Desde(5, 0, 77, new DateOnly(2026, 9, 1), 10m, 10m, 1000m);
        capa.Consumir(4m);

        capa.RemainingQuantity.Should().Be(6m);
        var devolver = () => capa.Consumir(-5m);
        devolver.Should().Throw<InvalidOperationException>("el restante nunca pasa del original");
        var demasiado = () => capa.Consumir(7m);
        demasiado.Should().Throw<InvalidOperationException>("el restante nunca es negativo");
    }

    // ------------------------------------------------------------------------------------------ parámetros --

    [Fact]
    public void Las_claves_de_costeo_de_I5_estan_en_el_catalogo_con_su_permiso()
    {
        var metodo = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoMetodo)!;
        metodo.Interpretar("Peps", EntregaDelComercio.I4).Admitido.Should().BeFalse("hasta I5, Parameters.ValueNotAllowed");
        metodo.Interpretar("Peps", EntregaDelComercio.I5).Admitido.Should().BeTrue();
        metodo.PermisoAdicional.Should().Be("Inventory.Costing.Manage");

        var permitidos = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoRetroactivosPermitidos)!;
        permitidos.Tipo.Should().Be(TipoDeParametro.Bool);
        permitidos.DefectoSeguro.Should().Be("false");
        permitidos.DisponibleDesde.Should().Be(EntregaDelComercio.I5);
        permitidos.PermisoAdicional.Should().Be("Inventory.Costing.Manage");

        var dias = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoRetroactivosDiasMaximos)!;
        dias.Tipo.Should().Be(TipoDeParametro.Int);
        dias.DefectoSeguro.Should().Be("0");
        dias.DisponibleDesde.Should().Be(EntregaDelComercio.I5);
        dias.PermisoAdicional.Should().Be("Inventory.Costing.Manage");
        dias.Interpretar("-1", EntregaDelComercio.I5).Admitido.Should().BeFalse("los días no son negativos");
    }

    // ------------------------------------------------------------------ T843: PEPS en la diferencia de precio --

    [Fact]
    public void Con_PEPS_la_diferencia_de_precio_suma_a_la_capa_de_su_recepcion_lo_que_queda_de_ella()
    {
        // C1 (Id 1) 10 × 1.000 ya agotada; C2 (Id 2) 10 × 1.300 con 4 restantes: 5.200 en el ámbito.
        var c2 = new CapaDeCosto(ReferenciaDeKardex.A(2), new DateOnly(2026, 9, 2), 10m, 4m, 1300m);
        var estado = EstadoDeCosto.Con(4m, 5200m, 1300m) with { Capas = [c2] };

        // La factura de C2 cuesta 500 más: 4/10 = 0,4 sigue en la capa (200) y 300 ya se vendió.
        var r = MotorDeCosteo.DiferenciaDePrecio(estado, new PedidoDeDiferenciaDePrecio(ReferenciaDeKardex.A(2), 10m, 500m), RedondeoDeMontos.Centavo, CostMethod.Fifo);

        r.Lineas.Select(l => (l.Reason, l.TotalCost, l.Porcion)).Should().Equal(
            (KardexReason.PriceDifference, 500m, PorcionDelAjuste.EnExistencia), (KardexReason.PriceDifference, -300m, PorcionDelAjuste.Vendida));
        r.Estado.Value.Should().Be(5400m);
        r.Estado.Capas.Should().ContainSingle().Which.UnitCost.Should().Be(1350m, "(5.200 + 200) / 4");
        r.Estado.Capas.Sum(c => c.Valor(RedondeoDeMontos.Centavo)).Should().Be(r.Estado.Value);

        // La de C1, cuya capa ya se agotó: todo a lo vendido y el ámbito no cambia.
        var agotada = MotorDeCosteo.DiferenciaDePrecio(estado, new PedidoDeDiferenciaDePrecio(ReferenciaDeKardex.A(1), 10m, 100m), RedondeoDeMontos.Centavo, CostMethod.Fifo);
        agotada.Valor.Should().Be(0m);
        agotada.Estado.Value.Should().Be(5200m);
    }

    [Fact]
    public void Con_PEPS_el_centavo_que_no_da_el_costo_nuevo_de_la_capa_va_en_una_linea_del_mismo_motivo()
    {
        // 3 restantes de 3 a 1.000: sumar 100 da 3.100 / 3 = 1.033,333333 → 3.100,00 al centavo; con 10 da 1.003,333333 → 3.010,00.
        var capa = new CapaDeCosto(ReferenciaDeKardex.A(7), new DateOnly(2026, 9, 2), 3m, 3m, 1000m);
        var estado = EstadoDeCosto.Con(3m, 3000m, 1000m) with { Capas = [capa] };

        var r = IngenIA365ERP.Domain.Inventory.Costing.Peps.AjusteSobreEntrada(estado, ReferenciaDeKardex.A(7), KardexReason.LandedCost, 0.01m, 0.01m, RedondeoDeMontos.Centavo, new ExplicacionDeCosto());

        r.Estado.Capas.Sum(c => c.Valor(RedondeoDeMontos.Centavo)).Should().Be(r.Estado.Value, "las capas siguen valiendo lo del ámbito");
        r.Lineas.Should().OnlyContain(l => l.Reason == KardexReason.LandedCost);
        r.Valor.Should().Be(r.Estado.Value - 3000m);
    }
}
