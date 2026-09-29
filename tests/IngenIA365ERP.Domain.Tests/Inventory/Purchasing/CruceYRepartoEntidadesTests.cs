using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, US13, T777–T778 (data-model §9.6 y §9.7): las filas del cruce a tres vías y del reparto de costos adicionales
/// nacen del resultado de sus motores puros y cuidan sus invariantes —una línea que excede nace <c>Held</c> y sólo desde ahí se
/// aprueba o se rechaza; una que no excede no tiene estado; <c>ExistingAmount + SoldAmount = AllocatedAmount</c> y
/// Σ <c>AllocatedAmount</c> = <c>Subtotal</c>—. También las reglas sueltas de la tolerancia (<see cref="CruceDeCompra"/>).
/// </summary>
public class CruceYRepartoEntidadesTests
{
    private static readonly ToleranciasDelCruce UnPorCiento = new(0m, 0m, 0.01m, 0m, ReglaDeTolerancia.AmbasCondiciones);

    private static (LineaAlCruce Linea, ResultadoDelCruce Resultado) Cruce(decimal facturado, decimal precio)
    {
        var linea = new LineaAlCruce(100m, 98m, facturado, 1000m, 1000m, precio);
        return (linea, CruceDeCompra.Cruzar(linea, UnPorCiento, RedondeoDeMontos.Centavo));
    }

    [Fact]
    public void La_linea_que_excede_nace_retenida_con_sus_razones_y_su_tolerancia()
    {
        var (linea, resultado) = Cruce(100m, 1020m);

        var fila = PurchaseMatchLine.Registrar(7, 70, 700, linea, resultado);

        fila.InvoiceDocumentId.Should().Be(7);
        fila.InvoiceLineId.Should().Be(70);
        fila.OrderLineId.Should().Be(700);
        fila.OrderedQuantity.Should().Be(100m);
        fila.ReceivedNotInvoicedQuantity.Should().Be(98m);
        fila.InvoicedQuantity.Should().Be(100m);
        fila.OrderedUnitPrice.Should().Be(1000m);
        fila.ReceivedUnitCost.Should().Be(1000m);
        fila.InvoicedUnitPrice.Should().Be(1020m);
        fila.QuantityDifference.Should().Be(2m);
        fila.PriceDifferenceAmount.Should().Be(2000m);
        fila.PriceDifferenceRate.Should().Be(0.02m);
        fila.ExceedsTolerance.Should().BeTrue();
        fila.Reasons.Should().Be("Quantity,Price");
        fila.Status.Should().Be(PurchaseMatchStatus.Held);
        fila.ToleranceJson.Should().Contain("\"Compras.ToleranciaPrecioPorcentaje\":0.01");
        fila.EstaRetenida.Should().BeTrue();
    }

    [Fact]
    public void La_linea_dentro_de_tolerancia_no_tiene_estado_ni_se_aprueba()
    {
        var (linea, resultado) = Cruce(98m, 1005m);

        var fila = PurchaseMatchLine.Registrar(7, 70, 700, linea, resultado);

        fila.ExceedsTolerance.Should().BeFalse();
        fila.Status.Should().BeNull();
        fila.Reasons.Should().BeNull();
        fila.Invoking(f => f.Aprobar()).Should().Throw<InvalidOperationException>();
        fila.Invoking(f => f.Rechazar()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retenida_se_aprueba_o_se_rechaza_una_sola_vez()
    {
        var (linea, resultado) = Cruce(98m, 1020m);
        var aprobada = PurchaseMatchLine.Registrar(7, 70, 700, linea, resultado);
        var solicitud = Guid.NewGuid();

        aprobada.AsignarSolicitud(solicitud);
        aprobada.ApprovalRequestPublicId.Should().Be(solicitud);
        aprobada.Aprobar();
        aprobada.Status.Should().Be(PurchaseMatchStatus.Approved);
        aprobada.Invoking(f => f.Rechazar()).Should().Throw<InvalidOperationException>();
        aprobada.Invoking(f => f.AsignarSolicitud(Guid.NewGuid())).Should().Throw<InvalidOperationException>();

        var rechazada = PurchaseMatchLine.Registrar(7, 71, 701, linea, resultado);
        rechazada.Rechazar();
        rechazada.Status.Should().Be(PurchaseMatchStatus.Rejected);
        rechazada.Invoking(f => f.Aprobar()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Facturar_mas_de_lo_recibido_no_se_aprueba_por_excepcion()
    {
        var (linea, resultado) = Cruce(100m, 1000m);
        var fila = PurchaseMatchLine.Registrar(7, 70, 700, linea, resultado);

        resultado.Aprobable.Should().BeFalse();
        fila.Invoking(f => f.Aprobar()).Should().Throw<InvalidOperationException>()
            .WithMessage($"*{CruceDeCompra.CodigoCantidadNoAprobable}*");
        fila.Rechazar();
        fila.Status.Should().Be(PurchaseMatchStatus.Rejected);
    }

    [Fact]
    public void El_reparto_se_guarda_con_su_metodo_y_cuida_la_suma()
    {
        var reparto = Prorrateo.Repartir(new PedidoDeProrrateo(100000m, LandedCostAllocationMethod.Value,
            [new LineaAProrratear(11, 10, 60m, 600000m, null, null, null, 30m), new LineaAProrratear(12, 20, 40m, 400000m, null, null, null, 40m)],
            RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor));

        var filas = reparto.Lineas.Select(l => LandedCostAllocation.Desde(5, 50, LandedCostAllocationMethod.Value, l)).ToList();

        filas.Select(f => (f.ReceiptLineId, f.ProductId, f.AllocatedAmount, f.ExistingAmount, f.SoldAmount)).Should().Equal(
            (11, 10, 60000m, 30000m, 30000m), (12, 20, 40000m, 40000m, 0m));
        filas.Should().OnlyContain(f => f.DocumentId == 5 && f.ReceiptDocumentId == 50 && f.AllocationMethod == LandedCostAllocationMethod.Value);
        filas[0].ExistingRatio.Should().Be(0.5m);

        var cuadra = () => LandedCostAllocation.VerificarSuma(filas, 100000m);
        cuadra.Should().NotThrow();
        var noCuadra = () => LandedCostAllocation.VerificarSuma(filas, 100000.01m);
        noCuadra.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_reparto_cuyas_porciones_no_suman_lo_asignado_no_se_guarda()
    {
        var malo = new RepartoDeLinea(11, 10, 1m, 100m, 0m, 0.5m, 50m, 40m);

        var desde = () => LandedCostAllocation.Desde(5, 50, LandedCostAllocationMethod.Manual, malo);

        desde.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("AmbasCondiciones", ReglaDeTolerancia.AmbasCondiciones)]
    [InlineData("CualquieraDeLas", ReglaDeTolerancia.CualquieraDeLas)]
    [InlineData("cualquieradelas", ReglaDeTolerancia.CualquieraDeLas)]
    public void La_regla_se_lee_del_valor_del_parametro(string valor, ReglaDeTolerancia esperada) =>
        CruceDeCompra.ReglaDesde(valor).Should().Be(esperada);

    [Fact]
    public void Una_regla_desconocida_es_un_error_visible()
    {
        var leer = () => CruceDeCompra.ReglaDesde("Alguna");
        leer.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1000, 0, 0, ReglaDeTolerancia.AmbasCondiciones, 0)]
    [InlineData(1000, 0.01, 0, ReglaDeTolerancia.AmbasCondiciones, 10)]
    [InlineData(1000, 0, 15, ReglaDeTolerancia.AmbasCondiciones, 15)]
    [InlineData(1000, 0.01, 15, ReglaDeTolerancia.AmbasCondiciones, 10)]
    [InlineData(1000, 0.01, 15, ReglaDeTolerancia.CualquieraDeLas, 15)]
    [InlineData(1000, 0.02, 15, ReglaDeTolerancia.CualquieraDeLas, 20)]
    public void La_tolerancia_permitida_combina_porcentaje_y_valor_segun_la_regla(
        decimal referencia, decimal porcentaje, decimal valor, ReglaDeTolerancia regla, decimal esperada) =>
        CruceDeCompra.ToleranciaPermitida(referencia, porcentaje, valor, regla).Should().Be(esperada);

    [Fact]
    public void Una_tolerancia_negativa_es_un_error_de_programa()
    {
        var negativa = () => CruceDeCompra.ToleranciaPermitida(1000m, -0.01m, 0m, ReglaDeTolerancia.AmbasCondiciones);
        negativa.Should().Throw<ArgumentOutOfRangeException>();
    }
}
