using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, T034 (research R04; data-model §0.2, §4.2; FR-001, FR-011, FR-062): la reconstrucción de una cuenta a la fecha D con la información
/// hasta R. Consignación, retiro, reverso (antes y después de R), fecha valor anterior registrada después, la serie de saldos diarios, las sumas del
/// titular y de aportes, la huella reproducible y el rechazo de un libro inconsistente.
/// </summary>
public class ReconstruccionDeCuentaTests
{
    private static readonly DateOnly D1 = new(2026, 11, 2);
    private static readonly DateOnly D2 = new(2026, 11, 5);
    private static readonly DateOnly D3 = new(2026, 11, 10);

    private static DateTime En(DateOnly dia, int hora = 10) => new(dia.Year, dia.Month, dia.Day, hora, 0, 0, DateTimeKind.Utc);

    private static MovimientoDeCuenta Fila(long id, MemberAccountTransactionKind clase, decimal monto, DateOnly valor, DateTime registrado,
        MemberAccountConcept concepto = MemberAccountConcept.Principal, long? reversa = null) =>
        new(id, clase, concepto, monto, valor, registrado, reversa);

    [Fact]
    public void Una_consignacion_y_un_retiro_suman_por_su_signo()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.Withdrawal, -250_000m, D2, En(D2)),
        };

        var estado = MotorDeCaptaciones.Reconstruir(libro, D3, En(D3));

        estado.Saldo(MemberAccountConcept.Principal).Should().Be(750_000m);
        estado.SaldoDeCaptacion.Should().Be(750_000m);
        estado.Filas.Should().Be(2);
        estado.Saldos.Keys.Should().BeEquivalentTo(Enum.GetValues<MemberAccountConcept>(), "los diez conceptos salen siempre, en cero si no hay filas");
        MotorDeCaptaciones.Reconstruir(libro, D1, En(D3)).Saldo(MemberAccountConcept.Principal).Should().Be(1_000_000m, "a D1 el retiro no ha pasado");
    }

    [Fact]
    public void Un_reverso_anula_al_original_desde_que_se_registra_y_no_antes()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 500_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.Deposit, 200_000m, D2, En(D2)),
            // El cheque de la segunda se devolvió: el espejo lleva su fecha valor y se registra el D3.
            Fila(3, MemberAccountTransactionKind.Deposit, -200_000m, D2, En(D3), reversa: 2),
        };

        MotorDeCaptaciones.Reconstruir(libro, D3, En(D3)).Saldo(MemberAccountConcept.Principal).Should().Be(500_000m);
        MotorDeCaptaciones.Reconstruir(libro, D3, En(D2, 18)).Saldo(MemberAccountConcept.Principal).Should().Be(700_000m,
            "lo que se informó antes del reverso sigue siendo reproducible (SC-001)");
        MotorDeCaptaciones.Reconstruir(libro, D2, En(D3)).Saldo(MemberAccountConcept.Principal).Should().Be(500_000m,
            "el espejo vive en la fecha valor de su original: a D2 ya no cuenta, sabiéndolo en D3");
    }

    [Fact]
    public void Una_fecha_valor_anterior_cambia_el_pasado_solo_con_la_informacion_posterior()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 300_000m, D1, En(D1)),
            // Consignada el D3 con fecha valor D2 (FR-062).
            Fila(2, MemberAccountTransactionKind.Deposit, 100_000m, D2, En(D3)),
        };

        MotorDeCaptaciones.Reconstruir(libro, D2, En(D2, 23)).Saldo(MemberAccountConcept.Principal).Should().Be(300_000m);
        MotorDeCaptaciones.Reconstruir(libro, D2, En(D3)).Saldo(MemberAccountConcept.Principal).Should().Be(400_000m);

        var cambios = MotorDeCaptaciones.CambiosPorFechaValor(libro, D1, D3, En(D2, 23), En(D3));
        cambios.Should().HaveCount(D3.DayNumber - D2.DayNumber + 1, "cada día desde la fecha valor hasta hoy cambia");
        cambios.Should().OnlyContain(c => c.Concepto == MemberAccountConcept.Principal && c.Cambio == 100_000m);
        cambios[0].Dia.Should().Be(D2);
    }

    [Fact]
    public void La_informacion_hasta_R_excluye_lo_registrado_despues()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 80_000m, D1, En(D1, 9)),
            Fila(2, MemberAccountTransactionKind.Deposit, 20_000m, D1, En(D1, 15)),
        };

        MotorDeCaptaciones.Reconstruir(libro, D1, En(D1, 12)).Saldo(MemberAccountConcept.Principal).Should().Be(80_000m);
        MotorDeCaptaciones.Reconstruir(libro, D1, En(D1, 15)).Saldo(MemberAccountConcept.Principal).Should().Be(100_000m, "R incluye lo registrado en R");
    }

    [Fact]
    public void La_serie_diaria_va_del_dia_siguiente_al_inicio_hasta_el_fin_incluido()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.Deposit, 500m, D2, En(D2)),
        };

        var serie = MotorDeCaptaciones.SaldosDiarios(libro, D1, D2.AddDays(1), En(D3));

        serie.Select(s => s.Dia).Should().Equal(D1.AddDays(1), D1.AddDays(2), D2, D2.AddDays(1));
        serie.Select(s => s.Saldo(MemberAccountConcept.Principal)).Should().Equal(1_000m, 1_000m, 1_500m, 1_500m);
    }

    [Fact]
    public void Las_sumas_del_titular_de_aportes_y_de_la_memoria_siguen_el_signo_de_cada_familia()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.InterestAccrued, 123.456789m, D2, En(D2), MemberAccountConcept.InterestPayable),
            Fila(3, MemberAccountTransactionKind.ChargeApplied, 5_000m, D2, En(D2), MemberAccountConcept.ChargeReceivable),
            // La retención va en pareja: sale del capital y entra a su memoria.
            Fila(4, MemberAccountTransactionKind.WithholdingApplied, -70m, D2, En(D2)),
            Fila(5, MemberAccountTransactionKind.WithholdingApplied, 70m, D2, En(D2), MemberAccountConcept.Withholding),
        };

        var estado = MotorDeCaptaciones.Reconstruir(libro, D3, En(D3));

        estado.SaldoDeCaptacion.Should().Be(1_000_000m - 70m + 123.456789m - 5_000m);
        estado.Retenido.Should().Be(70m, "lo retenido es memoria: no es saldo del titular (§0.2)");
        estado.SaldoDeAportes.Should().Be(0m);

        var aportes = MotorDeCaptaciones.Reconstruir(new[]
        {
            Fila(1, MemberAccountTransactionKind.OpeningBalance, 400_000m, D1, En(D1), MemberAccountConcept.ContributionMinimum),
            Fila(2, MemberAccountTransactionKind.Deposit, 50_000m, D2, En(D2), MemberAccountConcept.ContributionOrdinary),
            Fila(3, MemberAccountTransactionKind.Deposit, 10_000m, D2, En(D2), MemberAccountConcept.ContributionExtraordinary),
        }, D3, En(D3));
        aportes.SaldoDeAportes.Should().Be(460_000m);
    }

    [Fact]
    public void La_huella_es_reproducible_y_cambia_con_la_informacion()
    {
        var libro = new[] { Fila(1, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D1)), Fila(2, MemberAccountTransactionKind.Deposit, 1m, D2, En(D3)) };

        var a = MotorDeCaptaciones.Reconstruir(libro, D3, En(D3));
        var b = MotorDeCaptaciones.Reconstruir(libro.Reverse(), D3, En(D3));
        var c = MotorDeCaptaciones.Reconstruir(libro, D3, En(D2));

        a.Huella.Should().HaveLength(64).And.Be(b.Huella, "el orden en que llegan las filas no cambia la huella");
        c.Huella.Should().NotBe(a.Huella);
    }

    [Fact]
    public void Verificar_dice_que_concepto_difiere()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.InterestAccrued, 0.5m, D2, En(D2), MemberAccountConcept.InterestPayable),
        };
        var esperado = new Dictionary<MemberAccountConcept, decimal> { [MemberAccountConcept.Principal] = 1_000m, [MemberAccountConcept.InterestPayable] = 0.25m };

        var diferencias = MotorDeCaptaciones.Verificar(libro, D3, esperado);

        diferencias.Should().ContainSingle().Which.Should().Be(new DiferenciaDeCuenta(MemberAccountConcept.InterestPayable, 0.5m, 0.25m));
        diferencias[0].Diferencia.Should().Be(0.25m);
    }

    [Fact]
    public void Un_espejo_de_otro_espejo_se_rechaza()
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.Deposit, -1_000m, D1, En(D2), reversa: 1),
            Fila(3, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D3), reversa: 2),
        };

        FluentActions.Invoking(() => MotorDeCaptaciones.Reconstruir(libro, D3, En(D3)))
            .Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Reversal.OfReversal");
    }

    [Theory]
    [InlineData(-900, "Principal", 1)]
    [InlineData(-1000, "InterestPayable", 1)]
    [InlineData(-1000, "Principal", 99)]
    public void Un_espejo_que_no_anula_exactamente_a_su_original_es_un_libro_inconsistente(int monto, string concepto, long reversa)
    {
        var libro = new[]
        {
            Fila(1, MemberAccountTransactionKind.Deposit, 1_000m, D1, En(D1)),
            Fila(2, MemberAccountTransactionKind.Deposit, monto, D1, En(D2), Enum.Parse<MemberAccountConcept>(concepto), reversa),
        };

        FluentActions.Invoking(() => MotorDeCaptaciones.Reconstruir(libro, D3, En(D3)))
            .Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(ReconstruccionDeCuenta.CodigoLibroInconsistente);
    }

    [Fact]
    public void La_version_de_reglas_es_la_de_la_cartera()
    {
        MotorDeCaptaciones.VersionDeReglas.Should().Be(VersionesDeReglas.Vigente);
    }
}
