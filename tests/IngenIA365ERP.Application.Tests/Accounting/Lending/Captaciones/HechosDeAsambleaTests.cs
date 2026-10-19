using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using static IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones.EscenarioDeCaptaciones;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US12 (T203, T204; contracts/contabilidad.md §3, filas 22 a 24; research §22): lo que el constructor arma con los hechos de la asamblea —la
/// revalorización contra el fondo de revalorización, los excedentes pagados del fondo de excedentes a los retornos por pagar (sin mover cuentas del
/// asociado) y el reverso entero de una decisión con un espejo por cuenta, cada uno de vuelta a su fondo con la sucursal de su cuenta—. (nuevo)
/// </summary>
public class HechosDeAsambleaTests
{
    private static readonly EscenarioDeCaptaciones S = new();

    private static DepositFactLineV1 Linea(DepositFactDimensionsV1 d, string cuenta, params DepositConceptAmountV1[] valores) =>
        new() { Account = Cuenta(cuenta), HolderPersonPublicId = S.Titular, Dimensions = d, Amounts = valores };

    [Fact]
    public void La_revalorizacion_va_de_su_fondo_a_los_aportes_revalorizados_una_linea_por_cuenta()
    {
        var a = S.Aportes();
        var hecho = new RevalorizacionDeAportesAplicadaV1
        {
            Lines = [Linea(a, "APORTES-1", Valor("ContributionRevaluation", 3_000m, claseDeAporte: "Revalued")),
                Linea(a, "APORTES-2", Valor("ContributionRevaluation", 6_000m, claseDeAporte: "Revalued"))],
        };

        var d = ConstructorDeLineasDeCartera.Desglosar(S.Unidad("AssemblyDistribution", "ASM-000001", hecho));

        d.Fallos.Should().BeEmpty();
        d.Partidas.Where(p => p.Rol == R.FondoDeRevalorizacion).Sum(p => p.Importe).Should().Be(9_000m);
        d.Partidas.Where(p => p.Rol == R.AportesSociales).Sum(p => p.Importe).Should().Be(9_000m);
        d.Partidas.Where(p => p.Rol == R.AportesSociales).Should().OnlyContain(p => p.Valores.ContributionClass == "Revalued" && p.Cuenta != null);
    }

    [Fact]
    public void Los_excedentes_pagados_van_del_fondo_de_excedentes_a_los_retornos_por_pagar()
    {
        var a = S.Aportes();
        var hecho = new ExcedentesDistribuidosV1
        {
            Payments = [new SurplusPaymentV1 { Account = Cuenta("APORTES-1"), HolderPersonPublicId = S.Titular, Dimensions = a, Amount = 10_000m }],
        };

        var d = ConstructorDeLineasDeCartera.Desglosar(S.Unidad("AssemblyDistribution", "ASM-000002", hecho));

        d.Fallos.Should().BeEmpty();
        d.Partidas.Should().HaveCount(2);
        d.Partidas.Single(p => p.Rol == R.FondoDeExcedentes).Importe.Should().Be(10_000m);
        var porPagar = d.Partidas.Single(p => p.Rol == R.DevolucionPorPagar);
        porPagar.Importe.Should().Be(10_000m);
        porPagar.Valores.ReasonCode.Should().Be("SurplusPayment");
        porPagar.Tercero.Should().Be(S.Titular);
    }

    [Fact]
    public void El_reverso_entero_devuelve_cada_cuenta_a_su_fondo()
    {
        var a = S.Aportes();
        var reverso = new OperacionDeCuentaReversadaV1
        {
            Account = Cuenta("APORTES-1"), HolderPersonPublicId = S.Titular, Dimensions = a, Amounts = [Valor("ContributionRevaluation", -3_000m)],
            Original = new OriginalFactRefV1 { Type = RevalorizacionDeAportesAplicadaV1.Type },
            Mirrors =
            [
                new DepositFactMirrorV1 { Type = RevalorizacionDeAportesAplicadaV1.Type, Account = Cuenta("APORTES-1"), HolderPersonPublicId = S.Titular,
                    Dimensions = a, Amounts = [Valor("ContributionRevaluation", -3_000m, claseDeAporte: "Revalued")] },
                new DepositFactMirrorV1 { Type = RevalorizacionDeAportesAplicadaV1.Type, Account = Cuenta("APORTES-2"), HolderPersonPublicId = S.Titular,
                    Dimensions = a, Amounts = [Valor("ContributionRevaluation", -6_000m, claseDeAporte: "Revalued")] },
            ],
        };

        var d = ConstructorDeLineasDeCartera.Desglosar(S.Unidad("AssemblyDistributionReversal", "ASM-000001-R", reverso));

        d.Fallos.Should().BeEmpty();
        var fondo = d.Partidas.Where(p => p.Rol == R.FondoDeRevalorizacion).ToList();
        fondo.Should().HaveCount(2, "una contrapartida por cuenta, con la sucursal de su cuenta");
        fondo.Sum(p => p.Importe).Should().Be(-9_000m);
        d.Partidas.Where(p => p.Rol == R.AportesSociales).Sum(p => p.Importe).Should().Be(-9_000m);
    }
}
