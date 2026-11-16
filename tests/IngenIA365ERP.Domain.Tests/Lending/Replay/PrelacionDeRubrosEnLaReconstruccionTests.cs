using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// 2026-11-16 (pedido del dueño; <c>Pagos.PrelacionDeRubros</c>): el orden de rubros de la empresa en la reconstrucción real. Un crédito de 3.000.000 a 12
/// cuotas desembolsado el 15-01-2027 tiene la cuota 1 (15-02) y la 2 (15-03) vencidas el 25-03. Un pago con el orden sellado se reconstruye siempre con él
/// (simular es aplicar); uno sin orden —anterior al parámetro— se reconstruye con la prelación de la línea, como antes; y el anticipo que cubre una cuota al
/// vencer usa el último orden sellado por un pago ya aplicado.
/// </summary>
public class PrelacionDeRubrosEnLaReconstruccionTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);
    private static readonly DateOnly Pago = new(2027, 3, 25);
    private static readonly OrdenDeRubros CapitalPrimero = OrdenDeRubros.Leer("Capital,Interes,Seguro")!;

    private static EntradaDeOperacion Valor(string id, DateOnly fecha, decimal valor, OrdenDeRubros? rubros, PaymentModality modalidad = PaymentModality.FreeAmount,
        long secuencia = 2) =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha), secuencia)
        {
            Contenido = new ContenidoDePago(modalidad, valor) { Rubros = rubros },
        };

    private static EntradaDeReconstruccion Con(params EntradaDeOperacion[] pagos)
    {
        var e = CreditosDePrueba.Simple(desembolso: Desembolso);
        return e with { Operaciones = [.. e.Operaciones, .. pagos] };
    }

    [Fact]
    public void Un_pago_sin_orden_sellado_se_reconstruye_con_la_prelacion_de_la_linea_como_antes()
    {
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var solicitud = new SolicitudDeDistribucion(PaymentModality.FreeAmount, 150_000m, Pago);

        var viejo = MotorDeCartera.Reconstruir(Con(Valor("P1", Pago, 150_000m, rubros: null)), Pago).Pagos["P1"];

        viejo.Lineas.Should().Equal(AplicadorDePagos.Distribuir(antes, solicitud, Prelacion.PorDefecto, RedondeoDeCartera.AlCentavo).Lineas);
        viejo.Lineas.First().Concepto.Should().NotBe(LoanConcept.Principal, "la línea cubre los intereses de la cuota antes que su capital");
    }

    [Fact]
    public void Un_pago_con_el_orden_sellado_se_reparte_con_el_y_simular_es_aplicar()
    {
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var simulada = AplicadorDePagos.Distribuir(antes, new SolicitudDeDistribucion(PaymentModality.FreeAmount, 150_000m, Pago),
            Prelacion.PorDefecto.ConRubros(CapitalPrimero), RedondeoDeCartera.AlCentavo);

        var estado = MotorDeCartera.Reconstruir(Con(Valor("P1", Pago, 150_000m, CapitalPrimero)), new DateOnly(2027, 6, 30));
        var aplicada = estado.Pagos["P1"];

        aplicada.Lineas.Should().Equal(simulada.Lineas);
        aplicada.Lineas.First().Should().Match<LineaDeDistribucion>(l => l.Cuota == 1 && l.Concepto == LoanConcept.Principal, "el capital de la cuota 1 primero");
        HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, aplicada)
            .Should().Be(HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, simulada), "simular = aplicar (FR-072)");
        MotorDeCartera.Verificar(Con(Valor("P1", Pago, 150_000m, CapitalPrimero)), new DateOnly(2027, 6, 30),
            MotorDeCartera.Reconstruir(Con(Valor("P1", Pago, 150_000m, CapitalPrimero)), new DateOnly(2027, 6, 30), Desembolso).Libro.Movimientos)
            .Should().BeEmpty("reconstruir otra vez da lo mismo");
    }

    [Fact]
    public void El_anticipo_al_vencer_usa_el_ultimo_orden_sellado_y_sin_ninguno_la_prelacion_de_la_linea()
    {
        // Un pago a vencidas que deja al día las cuotas 1 y 2 y, el 01-04, un anticipo general de 50.000 que el 15-04 cubre (parcial) la cuota 3.
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var exigible = antes.Saldos.Where(s => s.Cuota is 1 or 2).Sum(s => s.Valor);
        EntradaDeOperacion[] Pagos(OrdenDeRubros? rubros) =>
        [
            Valor("P1", Pago, exigible, rubros, PaymentModality.PastDue),
            Valor("P2", new DateOnly(2027, 4, 1), 50_000m, rubros, PaymentModality.Advance, secuencia: 3),
        ];
        var vence3 = new DateOnly(2027, 4, 15);

        var conOrden = MotorDeCartera.Reconstruir(Con(Pagos(CapitalPrimero)), vence3).AnticiposAplicados.Single(a => a.Cuota == 3);
        var sinOrden = MotorDeCartera.Reconstruir(Con(Pagos(null)), vence3).AnticiposAplicados.Single(a => a.Cuota == 3);

        conOrden.Lineas.Should().ContainSingle().Which.Concepto.Should().Be(LoanConcept.Principal, "con el capital primero, los 50.000 van al capital de la 3");
        sinOrden.Lineas.Select(l => l.Concepto).Should().Equal(LoanConcept.CurrentInterest, LoanConcept.Principal);
        conOrden.Usado.Sum(u => u.Monto).Should().Be(50_000m);
    }
}
