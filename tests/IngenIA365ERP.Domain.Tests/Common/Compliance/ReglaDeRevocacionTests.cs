using FluentAssertions;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Tests.Common.Compliance;

/// <summary>
/// Feature 013, T0020 (T69, S10; contracts/interfaces.md §10.3; FR-006): la revocación o supresión por finalidad.
/// <c>ContactChannel</c> procede siempre; las finalidades de crédito (y el tratamiento general) no proceden mientras
/// haya un deber legal: una obligación viva como deudor o codeudor, la persona «migrada, sin activar» o, sólo para
/// <c>CreditBureauReport</c>, una obligación cerrada dentro de la permanencia del dato negativo, que se calcula con los
/// límites legales recibidos (<c>PERMANENCIA_NEGATIVO_VECES_MORA</c>, <c>PERMANENCIA_NEGATIVO_MESES_MAXIMOS</c>).
/// La constancia lista el porqué.
/// </summary>
public class ReglaDeRevocacionTests
{
    private static readonly LimitesDePermanencia Permanencia = new(VecesMora: 2m, MesesMaximos: 48);
    private static readonly DateOnly Hoy = new(2026, 10, 5);

    private static ObligacionParaRevocacion Viva(string numero) => new(numero, IsLive: true, ClosedOn: null, DiasDeMoraDelDatoNegativo: 0);

    private static ObligacionParaRevocacion Cerrada(string numero, string cerradaEl, int diasDeMora) =>
        new(numero, IsLive: false, ClosedOn: DateOnly.Parse(cerradaEl), DiasDeMoraDelDatoNegativo: diasDeMora);

    [Fact]
    public void ContactChannel_procede_siempre()
    {
        var d = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.ContactChannel, Hoy, migradaSinActivar: true,
            [Viva("CR-1"), Cerrada("CR-2", "2026-09-30", 120)], Permanencia);

        d.Procede.Should().BeTrue();
        d.Deberes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DataAuthorizationPurpose.CreditProcessing)]
    [InlineData(DataAuthorizationPurpose.CreditBureauQuery)]
    [InlineData(DataAuthorizationPurpose.CreditBureauReport)]
    [InlineData(DataAuthorizationPurpose.GeneralTreatment)]
    public void Con_una_obligacion_viva_no_procede(DataAuthorizationPurpose finalidad)
    {
        var d = ReglaDeRevocacion.Evaluar(finalidad, Hoy, migradaSinActivar: false, [Viva("CR-77")], Permanencia);

        d.Procede.Should().BeFalse();
        d.Deberes.Should().ContainSingle(x => x.Motivo == MotivoDeDeber.ObligacionViva && x.NumeroDeObligacion == "CR-77");
        d.Constancia.Should().Contain("CR-77");
    }

    [Theory]
    [InlineData(DataAuthorizationPurpose.CreditProcessing)]
    [InlineData(DataAuthorizationPurpose.CreditBureauQuery)]
    [InlineData(DataAuthorizationPurpose.CreditBureauReport)]
    public void Migrada_sin_activar_no_procede(DataAuthorizationPurpose finalidad)
    {
        var d = ReglaDeRevocacion.Evaluar(finalidad, Hoy, migradaSinActivar: true, [], Permanencia);

        d.Procede.Should().BeFalse();
        d.Deberes.Should().ContainSingle(x => x.Motivo == MotivoDeDeber.MigradaSinActivar);
    }

    [Fact]
    public void El_reporte_no_procede_con_una_cerrada_dentro_de_la_permanencia_del_dato_negativo()
    {
        // Cerrada el 31-03-2026 con 90 días de mora: el dato negativo permanece el doble, 180 días, hasta el 27-09-2026.
        var cerrada = Cerrada("CR-9", "2026-03-31", 90);

        var dentro = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditBureauReport, new DateOnly(2026, 9, 27), false, [cerrada], Permanencia);
        dentro.Procede.Should().BeFalse();
        dentro.Deberes.Should().ContainSingle(x => x.Motivo == MotivoDeDeber.PermanenciaDelDatoNegativo
            && x.NumeroDeObligacion == "CR-9" && x.Hasta == new DateOnly(2026, 9, 27));

        var fuera = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditBureauReport, new DateOnly(2026, 9, 28), false, [cerrada], Permanencia);
        fuera.Procede.Should().BeTrue();
    }

    [Fact]
    public void La_permanencia_no_pasa_del_maximo_de_meses()
    {
        // 1.000 días de mora × 2 = 2.000 días, más que 48 meses: rige el tope, hasta el 31-01-2024 + 48 meses.
        var cerrada = Cerrada("CR-5", "2024-01-31", 1000);

        var d = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditBureauReport, new DateOnly(2027, 1, 1), false, [cerrada], Permanencia);
        d.Deberes.Single().Hasta.Should().Be(new DateOnly(2028, 1, 31));
    }

    [Fact]
    public void Una_cerrada_sin_mora_no_deja_dato_negativo()
    {
        var d = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditBureauReport, Hoy, false, [Cerrada("CR-3", "2026-09-30", 0)], Permanencia);
        d.Procede.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataAuthorizationPurpose.CreditProcessing)]
    [InlineData(DataAuthorizationPurpose.CreditBureauQuery)]
    public void La_permanencia_del_dato_negativo_solo_impide_revocar_el_reporte(DataAuthorizationPurpose finalidad)
    {
        var d = ReglaDeRevocacion.Evaluar(finalidad, Hoy, false, [Cerrada("CR-4", "2026-09-30", 120)], Permanencia);
        d.Procede.Should().BeTrue();
    }

    [Fact]
    public void La_constancia_lista_cada_porque()
    {
        var d = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditBureauReport, Hoy, migradaSinActivar: true,
            [Viva("CR-1"), Cerrada("CR-2", "2026-09-30", 30), Cerrada("CR-3", "2020-01-31", 10)], Permanencia);

        d.Procede.Should().BeFalse();
        d.Deberes.Select(x => x.Motivo).Should().BeEquivalentTo(
            [MotivoDeDeber.ObligacionViva, MotivoDeDeber.MigradaSinActivar, MotivoDeDeber.PermanenciaDelDatoNegativo]);
        d.Constancia.Should().Contain("CR-1").And.Contain("CR-2").And.NotContain("CR-3");
    }

    [Fact]
    public void Sin_deber_procede_y_la_constancia_lo_dice()
    {
        var d = ReglaDeRevocacion.Evaluar(DataAuthorizationPurpose.CreditProcessing, Hoy, false, [], Permanencia);

        d.Procede.Should().BeTrue();
        d.Constancia.Should().NotBeNullOrWhiteSpace();
    }
}
