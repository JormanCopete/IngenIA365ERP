using FluentAssertions;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Delinquency;

namespace IngenIA365ERP.Domain.Tests.Lending.Delinquency;

/// <summary>
/// Feature 013, US4 (T0467; T30, R38; FR-069, FR-114): <see cref="AlturaDeMora"/>. El capital de una cuota vence el día siguiente a su
/// fecha exigible; la altura cuenta desde la cuota vencida más antigua cuyo saldo vencido supere la tolerancia, sin las cubiertas por
/// anticipo ni, dentro del umbral de la pagaduría, las descontadas pendientes de giro. Los días se contaron a mano en el calendario.
/// </summary>
public class AlturaDeMoraTests
{
    private static DateOnly F(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void El_capital_vence_el_dia_siguiente_a_la_fecha_exigible()
    {
        var cuotas = new[] { new CuotaParaMora(1, F("2026-10-13"), 478_575.09m) };
        AlturaDeMora.EstaVencida(F("2026-10-13"), F("2026-10-13")).Should().BeFalse("el día exigible se paga sin mora (art. 829)");
        AlturaDeMora.EstaVencida(F("2026-10-13"), F("2026-10-14")).Should().BeTrue();
        AlturaDeMora.Calcular(F("2026-10-13"), cuotas, 0m).Should().Be(ResultadoDeAlturaDeMora.AlDia);
        AlturaDeMora.Calcular(F("2026-10-14"), cuotas, 0m).Should().Be(new ResultadoDeAlturaDeMora(1, 1, 1));
        AlturaDeMora.Calcular(F("2026-11-12"), cuotas, 0m).Dias.Should().Be(30);
    }

    [Fact]
    public void Un_vencido_que_no_supera_la_tolerancia_no_cuenta_dias()
    {
        // Caso dorado 24: un residuo de 300 pesos con tolerancia de 1.000 no cuenta; la cuota 2 sí desde su exigible.
        var cuotas = new[]
        {
            new CuotaParaMora(1, F("2027-03-15"), 300m),
            new CuotaParaMora(2, F("2027-04-15"), 508_650m),
            new CuotaParaMora(3, F("2027-05-15"), 500_000m),
        };
        AlturaDeMora.Calcular(F("2027-04-10"), cuotas, 1_000m).Should().Be(ResultadoDeAlturaDeMora.AlDia);
        AlturaDeMora.Calcular(F("2027-04-20"), cuotas, 1_000m).Should().Be(new ResultadoDeAlturaDeMora(5, 2, 1));
        AlturaDeMora.Calcular(F("2027-04-20"), cuotas, 0m).Should().Be(new ResultadoDeAlturaDeMora(36, 1, 2), "sin tolerancia cuenta el residuo");
        AlturaDeMora.Calcular(F("2027-04-20"), [new CuotaParaMora(1, F("2027-03-15"), 1_000m)], 1_000m).Dias
            .Should().Be(0, "igual a la tolerancia no la supera");
    }

    [Fact]
    public void Las_cuotas_cubiertas_por_anticipo_no_cuentan()
    {
        var cuotas = new[]
        {
            new CuotaParaMora(1, F("2027-02-15"), 250_000m) { CubiertaPorAnticipo = true },
            new CuotaParaMora(2, F("2027-03-15"), 250_000m),
        };
        AlturaDeMora.Calcular(F("2027-03-01"), cuotas, 0m).Should().Be(ResultadoDeAlturaDeMora.AlDia);
        AlturaDeMora.Calcular(F("2027-03-20"), cuotas, 0m).Should().Be(new ResultadoDeAlturaDeMora(5, 2, 1));
    }

    [Fact]
    public void La_descontada_pendiente_de_giro_no_cuenta_dentro_del_umbral_de_la_pagaduria()
    {
        // La entrada existe desde K1; el umbral de la pagaduría lo trae K2 (FR-114, SC-016).
        var cuotas = new[] { new CuotaParaMora(1, F("2027-02-15"), 250_000m) { DescontadaPendienteDeGiro = true } };
        AlturaDeMora.Calcular(F("2027-02-25"), cuotas, 0m, umbralDePagaduria: 10).Dias.Should().Be(0);
        AlturaDeMora.Calcular(F("2027-02-26"), cuotas, 0m, umbralDePagaduria: 10).Should().Be(new ResultadoDeAlturaDeMora(11, 1, 1));
        AlturaDeMora.Calcular(F("2027-02-25"), cuotas, 0m).Dias.Should().Be(10, "sin umbral de pagaduría cuenta como cualquier vencida");
    }

    /// <summary>
    /// K2 (T0826): con los giros sellados por resultado, la mora de la pagaduría se mide desde el vencimiento de la ventana; lo descontado se
    /// resta del saldo vencido mientras no supere su umbral (un parcial deja contar lo que falta); sin umbral, nunca se imputa (C71).
    /// </summary>
    [Fact]
    public void Lo_descontado_con_ventana_y_umbral_sellados_se_resta_mientras_la_pagaduria_no_supere_el_umbral()
    {
        var giro = new GiroPendiente(250_000m, F("2027-02-18"), 10);
        var completa = new[] { new CuotaParaMora(1, F("2027-02-15"), 250_000m) { Giros = [giro] } };
        AlturaDeMora.Calcular(F("2027-02-28"), completa, 0m).Should().Be(ResultadoDeAlturaDeMora.AlDia, "la pagaduría lleva 10 días: el umbral");
        AlturaDeMora.Calcular(F("2027-03-01"), completa, 0m).Should().Be(new ResultadoDeAlturaDeMora(14, 1, 1), "superado, cuenta desde el vencimiento");

        var parcial = new[] { new CuotaParaMora(1, F("2027-02-15"), 250_000m) { Giros = [giro with { Monto = 249_000m }] } };
        AlturaDeMora.Calcular(F("2027-02-20"), parcial, 1_000m).Should().Be(ResultadoDeAlturaDeMora.AlDia, "lo que falta no supera la tolerancia");
        AlturaDeMora.Calcular(F("2027-02-20"), parcial, 999.99m).Dias.Should().Be(5, "lo que falta cuenta");

        var sinUmbral = new[] { new CuotaParaMora(1, F("2027-02-15"), 250_000m) { Giros = [giro with { UmbralDias = null }] } };
        AlturaDeMora.Calcular(F("2028-01-01"), sinUmbral, 0m).Should().Be(ResultadoDeAlturaDeMora.AlDia, "sin umbral la mora de la pagaduría no se imputa");

        giro.MoraDeLaPagaduria(F("2027-02-10")).Should().Be(0, "antes del vencimiento de la ventana no hay mora de la pagaduría");
        AlturaDeMora.CubiertoPorGiro(F("2027-02-28"), [giro, giro with { Monto = 5m, UmbralDias = 2 }]).Should().Be(250_000m);
    }

    [Fact]
    public void La_fachada_da_lo_mismo_y_una_tolerancia_negativa_es_un_error()
    {
        var cuotas = new[] { new CuotaParaMora(3, F("2027-01-31"), 10m) };
        MotorDeCartera.AlturaDeMora(F("2027-02-02"), cuotas, 0m).Should().Be(AlturaDeMora.Calcular(F("2027-02-02"), cuotas, 0m));
        var negativa = () => AlturaDeMora.Calcular(F("2027-02-02"), cuotas, -1m);
        negativa.Should().Throw<ArgumentOutOfRangeException>();
    }
}
