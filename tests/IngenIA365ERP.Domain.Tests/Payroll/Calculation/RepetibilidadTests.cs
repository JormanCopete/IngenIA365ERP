using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Tests.Payroll.Calculation;

/// <summary>FR-014: mismo insumo → mismo hash y mismas líneas; un insumo distinto → hash distinto.</summary>
public class RepetibilidadTests
{
    private static CalculationInput Entrada(string archivo) =>
        CasoDorado.Cargar(Path.Combine(CasoDorado.DirectorioDeCasos, archivo)).ConstruirEntrada();

    [Fact]
    public void Calcular_dos_veces_da_el_mismo_hash_y_las_mismas_lineas()
    {
        var archivo = Path.GetFileName(CasoDorado.Archivos().First());
        var motor = new PayrollCalculationEngine();

        var a = motor.Calculate(Entrada(archivo));
        var b = motor.Calculate(Entrada(archivo));

        a.InputsHash.Should().Be(b.InputsHash).And.HaveLength(64);
        Proyectar(a).Should().Be(Proyectar(b));
    }

    [Fact]
    public void Cambiar_una_novedad_cambia_el_hash()
    {
        var archivo = Path.GetFileName(CasoDorado.Archivos().First());
        var original = Entrada(archivo);
        var modificado = new CalculationInput
        {
            Period = original.Period,
            Employee = original.Employee,
            Concepts = original.Concepts,
            Parameters = original.Parameters,
            Policies = original.Policies,
            Novelties = [.. original.Novelties, new NoveltyInput { PublicId = Guid.NewGuid(), ConceptCode = "HEX_DIURNA", Quantity = 2m }],
        };

        InputsHasher.Compute(original).Should().NotBe(InputsHasher.Compute(modificado));
    }

    [Fact]
    public void El_orden_de_conceptos_y_parametros_no_cambia_el_hash()
    {
        var archivo = Path.GetFileName(CasoDorado.Archivos().First());
        var original = Entrada(archivo);
        var barajado = new CalculationInput
        {
            Period = original.Period,
            Employee = original.Employee,
            Novelties = original.Novelties,
            Policies = original.Policies,
            Concepts = original.Concepts.Reverse().ToList(),
            Parameters = original.Parameters.Reverse().ToList(),
        };

        InputsHasher.Compute(barajado).Should().Be(InputsHasher.Compute(original));
    }

    [Fact]
    public void Sin_un_parametro_requerido_el_motor_se_niega_y_lo_nombra()
    {
        var archivo = Path.GetFileName(CasoDorado.Archivos().First());
        var original = Entrada(archivo);
        var sinUvt = new CalculationInput
        {
            Period = original.Period,
            Employee = original.Employee,
            Novelties = original.Novelties,
            Policies = original.Policies,
            Concepts = original.Concepts,
            Parameters = original.Parameters.Where(p => p.Code != LegalParameterCodes.Uvt).ToList(),
        };

        var acto = () => new PayrollCalculationEngine().Calculate(sinUvt);

        acto.Should().Throw<CalculationRefusedException>()
            .Which.MissingCodes.Should().ContainSingle().Which.Should().Be(LegalParameterCodes.Uvt);
    }

    private static string Proyectar(CalculationResult r) =>
        JsonSerializer.Serialize(r.Lines.Select(l => new { l.Code, l.Amount, l.RawAmount, l.Quantity, l.BaseAmount, l.Factor, l.Order }));
}
