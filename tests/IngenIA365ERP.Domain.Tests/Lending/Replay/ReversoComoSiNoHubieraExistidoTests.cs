using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Feature 013, US5, T0515 (propiedad, nombre fijo de DT §2.25; FR-080, FR-081, SC-004, T33): con créditos y secuencias de pagos al azar
/// (semilla fija), reversar un pago deja el crédito EXACTAMENTE como si no hubiera existido: la reconstrucción con el pago y su reverso es
/// la de la entrada sin el pago —saldos por concepto y cuota, cuotas, días de mora, suspensión, transiciones, plan y huella— y los demás
/// pagos quedan redistribuidos igual, con y sin pagos posteriores. Un pago que aplicó algo cambia el crédito (la propiedad no es vacía).
/// </summary>
public class ReversoComoSiNoHubieraExistidoTests
{
    private const int Semilla = 20261012;
    private const int Creditos = 160;

    private static readonly PaymentModality[] Modalidades =
    [
        PaymentModality.FreeAmount, PaymentModality.PastDue, PaymentModality.CycleInstallment, PaymentModality.Advance,
        PaymentModality.ExtraordinaryPrincipal, PaymentModality.FullPayoff, PaymentModality.SpecificInstallment,
    ];

    [Fact]
    public void Reversar_un_pago_es_reconstruir_sin_el()
    {
        var azar = new Random(Semilla);
        var conPosteriores = 0;
        var queCambiaron = 0;
        using var _ = new AssertionScope();
        for (var i = 0; i < Creditos; i++)
        {
            var entrada = CreditosDePrueba.AlAzar(azar, conOperaciones: false);
            var origen = entrada.Operaciones[0].FechaValor;
            var cuotas = MotorDeCartera.GenerarPlan(entrada.Plan!).Cuotas.Count;
            var pagos = Pagos(azar, origen, azar.Next(1, 5), cuotas);
            var hasta = pagos.Max(p => p.FechaValor).AddDays(azar.Next(1, 90));
            var conPagos = entrada with { Operaciones = [.. entrada.Operaciones, .. pagos] };

            var elegido = pagos[azar.Next(pagos.Count)];
            var reverso = new EntradaDeOperacion($"R-{elegido.Id}", LoanOperationKind.PaymentReversal, elegido.FechaValor,
                CreditosDePrueba.Instante(hasta, 20), 99) { Reversa = elegido.Id };
            var reversado = conPagos with { Operaciones = [.. conPagos.Operaciones, reverso] };
            var sinElPago = conPagos with { Operaciones = conPagos.Operaciones.Where(o => o.Id != elegido.Id).ToList() };
            var donde = $"crédito {i}: se reversa {elegido.Id} ({((ContenidoDePago)elegido.Contenido!).Modalidad}) el {elegido.FechaValor:yyyy-MM-dd}";

            EstadoDelCredito conElPago, conReverso, nunca;
            try
            {
                conElPago = MotorDeCartera.Reconstruir(conPagos, hasta);
                conReverso = MotorDeCartera.Reconstruir(reversado, hasta, origen);
                nunca = MotorDeCartera.Reconstruir(sinElPago, hasta, origen);
            }
            catch (ReglaDeCarteraException ex)
            {
                throw new Xunit.Sdk.XunitException($"{donde}: la reconstrucción falló ({ex.Message}).");
            }

            conReverso.Huella.Should().Be(nunca.Huella, donde);
            conReverso.Saldos.Should().Equal(nunca.Saldos, $"{donde}: saldos por concepto y cuota");
            conReverso.Cuotas.Should().Equal(nunca.Cuotas, $"{donde}: cuotas");
            conReverso.AlturaDeMora.Should().Be(nunca.AlturaDeMora, $"{donde}: días de mora");
            conReverso.MotivoDeSuspension.Should().Be(nunca.MotivoDeSuspension, $"{donde}: suspensión");
            conReverso.Transiciones.Should().Equal(nunca.Transiciones, $"{donde}: causación suspendida y reanudada");
            conReverso.Plan!.Cuotas.Select(c => (c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, c.SaldoFinal))
                .Should().Equal(nunca.Plan!.Cuotas.Select(c => (c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, c.SaldoFinal)), $"{donde}: plan");
            conReverso.Libro.Movimientos.Select(m => (m.Dia, m.Clase, m.Concepto, m.Cuota, m.Monto))
                .Should().Equal(nunca.Libro.Movimientos.Select(m => (m.Dia, m.Clase, m.Concepto, m.Cuota, m.Monto)), $"{donde}: libro teórico");
            conReverso.Pagos.Keys.Should().BeEquivalentTo(nunca.Pagos.Keys, donde);
            foreach (var (id, d) in nunca.Pagos)
                conReverso.Pagos[id].Lineas.Should().Equal(d.Lineas, $"{donde}: el pago {id} queda redistribuido igual");

            if (pagos.Any(p => p.FechaValor > elegido.FechaValor || (p.FechaValor == elegido.FechaValor && p.Sequence > elegido.Sequence)))
                conPosteriores++;
            if (conElPago.Pagos[elegido.Id].Aplicado > 0m)
            {
                // A su fecha valor el pago cambia el crédito (a D puede no notarse: una cancelación posterior deja los dos en cero).
                MotorDeCartera.Reconstruir(conPagos, elegido.FechaValor).Huella
                    .Should().NotBe(MotorDeCartera.Reconstruir(sinElPago, elegido.FechaValor).Huella, $"{donde}: el pago aplicó algo y el reverso lo quita");
                queCambiaron++;
            }
        }
        conPosteriores.Should().BeGreaterThan(Creditos / 4, "con pagos posteriores que se redistribuyen");
        queCambiaron.Should().BeGreaterThan(Creditos / 2);
    }

    /// <summary>Pagos al azar con fecha valor después del origen y registrados en orden.</summary>
    private static List<EntradaDeOperacion> Pagos(Random azar, DateOnly origen, int cuantos, int cuotas)
    {
        var pagos = new List<EntradaDeOperacion>();
        var fecha = origen;
        for (var p = 0; p < cuantos; p++)
        {
            fecha = fecha.AddDays(azar.Next(1, 75));
            var modalidad = Modalidades[azar.Next(Modalidades.Length)];
            var valor = azar.Next(10_000, 3_000_000) + azar.Next(0, 100) / 100m;
            pagos.Add(new EntradaDeOperacion($"P{p + 1}", LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha, 16), 10 + p)
            {
                Contenido = new ContenidoDePago(modalidad, valor)
                {
                    Eleccion = azar.Next(0, 3) switch { 0 => PrepaymentChoice.ReduceTerm, 1 => PrepaymentChoice.ReduceInstallment, _ => null },
                    Cuota = modalidad == PaymentModality.SpecificInstallment ? azar.Next(1, cuotas + 1) : null,
                },
            });
        }
        return pagos;
    }
}
