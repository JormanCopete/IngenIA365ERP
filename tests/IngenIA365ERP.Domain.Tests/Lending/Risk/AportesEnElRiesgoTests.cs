using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 014, US3 (T084, T092; research R20, E09; FR-094): el fin de D19 en los motores del riesgo. Con la consulta de aportes real, la excepción de
/// aportes en el arrastre y el descuento de aportes en el deterioro individual los decide su parámetro; con el parámetro habilitado, la norma de prueba
/// que los admite y la entidad con indicadores, el crédito cuyo capital no supera los aportes del deudor no se arrastra y los aportes a prorrata se
/// restan de la base. Mismos créditos que el caso dorado r16 (que sigue verde con su bandera <c>rigeD19</c>).
/// </summary>
public class AportesEnElRiesgoTests
{
    private static readonly IngenIA365ERP.Domain.Lending.Risk.Normativa.Anexo1 Norma = CasoDoradoDeRiesgo.LeerNorma("anexo1-prueba");

    private static readonly ContextoDeAportes Habilitados = new(ExcepcionDeArrastre: true, DescuentoEnDeterioro: true, IndicadoresDisponibles: true);

    [Fact]
    public void Con_el_descuento_habilitado_los_aportes_a_prorrata_se_restan_de_la_base()
    {
        Norma.Aportes.Deducibles.Should().BeTrue("la norma de prueba los admite (caso r16)");
        var credito = new CreditoParaDeteriorar(1, "CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, new SaldosParaDeterioro(5_000_000m, 0m, 0m, 0m), false)
        {
            AportesAProrrata = 1_000_000m,
        };

        var con = MotorDeDeterioroIndividual.Calcular(Norma, credito, Habilitados, RedondeoDeDeterioro.Centavo);
        var sin = MotorDeDeterioroIndividual.Calcular(Norma, credito, ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Centavo);

        (con.AportesDescontados, con.Base, con.DeterioroCapital).Should().Be((1_000_000m, 4_000_000m, 400_000m));
        con.Explicacion.Texto.Should().Contain("se descuentan de la base");
        (sin.AportesDescontados, sin.Base, sin.DeterioroCapital).Should().Be((0m, 5_000_000m, 500_000m));
        sin.Explicacion.Texto.Should().Contain("rige D19: el parámetro Riesgo.DescuentoAportesDeterioro no está habilitado");
    }

    [Fact]
    public void Sin_indicadores_vigentes_el_parametro_no_basta()
    {
        var credito = new CreditoParaDeteriorar(1, "CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, new SaldosParaDeterioro(5_000_000m, 0m, 0m, 0m), false)
        {
            AportesAProrrata = 1_000_000m,
        };
        var r = MotorDeDeterioroIndividual.Calcular(Norma, credito, Habilitados with { IndicadoresDisponibles = false }, RedondeoDeDeterioro.Centavo);
        r.Base.Should().Be(5_000_000m);
        r.Explicacion.Texto.Should().Contain("no tiene indicadores vigentes");
    }

    [Fact]
    public void Con_la_excepcion_habilitada_el_credito_cubierto_por_los_aportes_no_se_arrastra()
    {
        CreditoParaCalificar Credito(int id, int dias, decimal capital, decimal aportes) => new(id, 1600, "CONSUMO", dias)
        {
            Capital = capital,
            AportesDelDeudor = aportes,
        };

        var cubierto = MotorDeCalificacion.Calificar(Norma, [Credito(1, 75, 5_000_000m, 2_500_000m), Credito(2, 0, 2_000_000m, 2_500_000m)],
            new OpcionesDeCalificacion(true, Habilitados)).ToDictionary(c => c.LoanId);
        cubierto[2].Categoria.Should().Be(RatingCategory.A, cubierto[2].Explicacion.Texto);
        cubierto[2].Explicacion.Texto.Should().Contain("rige la excepción de aportes");
        cubierto[1].Categoria.Should().Be(RatingCategory.C, "el capital del primero supera los aportes: él mismo no se excepciona de su mora");

        var sinExcepcion = MotorDeCalificacion.Calificar(Norma, [Credito(1, 75, 5_000_000m, 2_500_000m), Credito(2, 0, 2_000_000m, 2_500_000m)],
            new OpcionesDeCalificacion(true, ContextoDeAportes.SinAportes)).ToDictionary(c => c.LoanId);
        sinExcepcion[2].Categoria.Should().Be(RatingCategory.C, "sin la excepción, se arrastra");
        sinExcepcion[2].Causa.Should().Be(RatingCause.Drag);

        var noAlcanza = MotorDeCalificacion.Calificar(Norma, [Credito(1, 75, 5_000_000m, 1_500_000m), Credito(2, 0, 2_000_000m, 1_500_000m)],
            new OpcionesDeCalificacion(true, Habilitados)).ToDictionary(c => c.LoanId);
        noAlcanza[2].Categoria.Should().Be(RatingCategory.C, "el capital supera los aportes");
        noAlcanza[2].Explicacion.Texto.Should().Contain("supera los aportes del deudor");
    }
}
