using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US4 (T0468; nombre fijo de DT §2.25; SC-001, US4-5, US4-6, T24, R32): la liquidación es una función de (entrada, D, R).
/// Con créditos al azar (semilla fija): misma entrada, misma huella y mismos saldos a D pasada, de hoy y futura, repetida y con las
/// operaciones en cualquier orden; con R anterior a una operación que se registra después —aunque su fecha valor sea anterior a D—,
/// idéntica al centavo; una D futura sin usura certificada se proyecta y dice hasta cuándo vale (D11).
/// </summary>
public class ReproducibilidadDeLiquidacionTests
{
    private const int Semilla = 20261011;
    private const int Creditos = 150;

    [Fact]
    public void Misma_entrada_misma_huella_y_mismos_saldos_a_cualquier_fecha()
    {
        var azar = new Random(Semilla);
        using var _ = new AssertionScope();
        for (var i = 0; i < Creditos; i++)
        {
            var entrada = CreditosDePrueba.AlAzar(azar);
            var origen = entrada.Operaciones[0].FechaValor;
            foreach (var d in new[] { origen.AddDays(azar.Next(1, 60)), origen.AddDays(azar.Next(60, 400)), origen.AddDays(azar.Next(400, 1_400)) })
            {
                var uno = MotorDeCartera.Reconstruir(entrada, d);
                var otro = MotorDeCartera.Reconstruir(entrada, d);
                var desordenada = MotorDeCartera.Reconstruir(entrada with { Operaciones = [.. entrada.Operaciones.OrderByDescending(o => o.Id)] }, d);
                var donde = $"crédito {i} a {d:yyyy-MM-dd}";
                otro.Huella.Should().Be(uno.Huella, donde);
                desordenada.Huella.Should().Be(uno.Huella, $"{donde}: el orden de llegada no es dato");
                otro.Saldos.Should().Equal(uno.Saldos, donde);
                otro.ValorParaCancelar.Should().Be(uno.ValorParaCancelar, donde);
                uno.ValorParaCancelar.Should().Be(uno.Saldos.Where(s => s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)).Sum(s => s.Valor), donde);
            }
        }
    }

    [Fact]
    public void Con_R_anterior_a_una_operacion_posterior_la_liquidacion_no_cambia()
    {
        var azar = new Random(Semilla + 1);
        using var _ = new AssertionScope();
        for (var i = 0; i < Creditos; i++)
        {
            var entrada = CreditosDePrueba.AlAzar(azar, conOperaciones: false);
            var origen = entrada.Operaciones[0].FechaValor;
            var d = origen.AddDays(azar.Next(20, 300));
            var r = CreditosDePrueba.Instante(d, 20);
            var antes = MotorDeCartera.Reconstruir(entrada with { InformacionHasta = r }, d, origen);

            // Hoy se registra un tramo (o un reajuste) con fecha valor ANTERIOR a D, después de R (US4-6). Un tramo sólo cabe si
            // quedan cuotas por vencer (si no, Lending.Schedule.NothingToRegenerate).
            var fechaValor = origen.AddDays(azar.Next(1, d.DayNumber - origen.DayNumber));
            var ultimoVencimiento = Domain.Lending.Schedules.GeneradorDePlan.Generar(entrada.Plan!).Cuotas[^1].Vencimiento;
            var posterior = azar.Next(0, 2) == 0 && fechaValor < ultimoVencimiento
                ? new EntradaDeOperacion("T9", LoanOperationKind.Disbursement, fechaValor, r.AddHours(5), 9) { Monto = 1_000_000m }
                : new EntradaDeOperacion("R9", LoanOperationKind.RateReset, fechaValor, r.AddHours(5), 9) { TasaEfectivaAnual = 0.15m };
            var conPosterior = entrada with { Operaciones = [.. entrada.Operaciones, posterior] };

            var repetida = MotorDeCartera.Reconstruir(conPosterior with { InformacionHasta = r }, d, origen);
            var donde = $"crédito {i} a {d:yyyy-MM-dd}";
            repetida.Huella.Should().Be(antes.Huella, $"{donde}: con R de antes, idéntica (SC-001)");
            repetida.Saldos.Should().Equal(antes.Saldos, donde);
            repetida.Libro.Movimientos.Should().Equal(antes.Libro.Movimientos, $"{donde}: el libro teórico con R de antes tampoco cambia");

            // Con R de después la operación cuenta. Un tramo siempre cambia la liquidación; un reajuste puede no cambiarla (después del
            // último vencimiento sólo hay mora, y si la usura la topaba antes y después, la tasa del día es la misma).
            var conociendola = MotorDeCartera.Reconstruir(conPosterior with { InformacionHasta = r.AddDays(1) }, d, origen);
            if (posterior.Clase == LoanOperationKind.Disbursement)
            {
                conociendola.Huella.Should().NotBe(antes.Huella, $"{donde}: con R de después el tramo cuenta");
                conociendola.Libro.Diferencias(antes.Libro.Movimientos).Should().NotBeEmpty($"{donde}: la diferencia es lo que se concilia");
            }
        }
    }

    [Fact]
    public void Una_fecha_futura_sin_usura_certificada_se_proyecta_y_dice_hasta_cuando_vale()
    {
        var entrada = CreditosDePrueba.Simple();
        var ultimaUsura = entrada.Usura.Max(u => u.ValidTo!.Value);
        var futura = ultimaUsura.AddDays(45);
        var estado = MotorDeCartera.Reconstruir(entrada, futura);
        estado.ValidaHasta.Should().Be(ultimaUsura);
        estado.PrimerDiaSinUsura.Should().Be(ultimaUsura.AddDays(1));
        estado.Explicacion.Lineas.Should().Contain(l => l.Contains("Sin usura certificada"));
        MotorDeCartera.Reconstruir(entrada, futura).Huella.Should().Be(estado.Huella);
        MotorDeCartera.Reconstruir(entrada, ultimaUsura).ValidaHasta.Should().BeNull("hasta el último día certificado no hay proyección");
    }

    [Fact]
    public void Antes_del_origen_o_sin_operaciones_conocidas_no_hay_credito()
    {
        var entrada = CreditosDePrueba.Simple();
        var origen = entrada.Operaciones[0].FechaValor;
        var antes = MotorDeCartera.Reconstruir(entrada, origen.AddDays(-1));
        antes.Origen.Should().BeNull();
        antes.Saldos.Should().BeEmpty();
        var sinConocer = MotorDeCartera.Reconstruir(entrada with { InformacionHasta = CreditosDePrueba.Instante(origen, 10) }, origen.AddDays(10));
        sinConocer.Origen.Should().BeNull("el desembolso se registró después de R");
        var reversado = entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("X1", LoanOperationKind.DisbursementReversal, origen, CreditosDePrueba.Instante(origen.AddDays(2)), 2) { Reversa = "D1" }],
        };
        MotorDeCartera.Reconstruir(reversado, origen.AddDays(30)).Origen.Should().BeNull("como si el desembolso no hubiera existido");
        MotorDeCartera.Reconstruir(reversado with { InformacionHasta = CreditosDePrueba.Instante(origen.AddDays(1)) }, origen.AddDays(30)).Saldo(LoanConcept.Principal)
            .Should().Be(3_000_000m, "con R anterior al reverso, el desembolso cuenta");
    }

    [Fact]
    public void Una_operacion_que_la_reconstruccion_no_interpreta_se_rechaza_y_una_version_desconocida_tambien()
    {
        var entrada = CreditosDePrueba.Simple();
        // US5 ya interpreta los pagos; una condonación (K3) todavía no.
        var pago = entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("W1", LoanOperationKind.Waiver, new DateOnly(2027, 2, 15), CreditosDePrueba.Instante(new DateOnly(2027, 2, 15)), 2)],
        };
        ((Action)(() => MotorDeCartera.Reconstruir(pago, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada, "las condonaciones las interpreta K3");

        // El punto de extensión: una historia registra su intérprete y la reconstrucción lo usa.
        var interpretes = new Dictionary<LoanOperationKind, IInterpreteDeOperacion> { [LoanOperationKind.Waiver] = new PagoDePrueba() };
        MotorDeCartera.Reconstruir(pago, new DateOnly(2027, 3, 1), interpretes: interpretes).Libro.Movimientos
            .Should().Contain(m => m.Clase == LoanTransactionKind.PaymentAllocation);

        var otraVersion = entrada with { Condiciones = entrada.Condiciones with { RulesVersion = "LND-2031.4" } };
        ((Action)(() => MotorDeCartera.Reconstruir(otraVersion, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(VersionesDeReglas.CodigoVersionDesconocida);
        VersionesDeReglas.Para(VersionesDeReglas.Vigente).Should().BeSameAs(VersionesDeReglas.K1);
    }

    /// <summary>Un intérprete de prueba: deja una fila, como hace US5 con <c>AplicadorDePagos</c>.</summary>
    private sealed class PagoDePrueba : IInterpreteDeOperacion
    {
        public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion) =>
            contexto.AgregarFila(new MovimientoDeLibro(operacion.FechaValor, LoanTransactionKind.PaymentAllocation, LoanConcept.CurrentInterest, 1, -1m));
    }
}
