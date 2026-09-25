using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Tests.Taxes;

/// <summary>
/// Feature 012, T097 (FR-013, FR-044; research R21; quickstart §1.1): el motor tributario coincide al peso con los
/// cálculos hechos a mano de <c>Casos/</c>. Cada archivo arma su <see cref="TaxCatalogSnapshot"/> y los
/// <see cref="PerfilTributario"/> de sus escenarios, y se compara renglón a renglón base, valor, tratamiento y
/// explicación; los renglones que no están en lo esperado no deben salir. Molde de <c>Payroll/Calculation</c>.
/// </summary>
public class MotorTributarioCasosDoradosTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoTributario.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_al_peso_con_el_calculo_manual(string archivo)
    {
        var caso = CasoTributario.Cargar(Path.Combine(CasoTributario.DirectorioDeCasos, archivo));
        caso.Escenarios.Should().NotBeEmpty($"{archivo} no tiene escenarios");

        foreach (var escenario in caso.Escenarios)
        {
            var foto = caso.Foto(escenario.Cooperativa);
            var resultado = MotorTributario.Calcular(foto, escenario.Entrada(caso.Fecha, caso));
            var esperado = escenario.Esperado;

            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — {escenario.Nombre}");

            resultado.Rechazos.Select(r => r.Codigo).Should().BeEquivalentTo(esperado.Rechazos,
                $"rechazos: {string.Join(" | ", resultado.Rechazos.Select(r => r.Mensaje))}");
            foreach (var nombrado in esperado.RechazoNombra)
                resultado.Rechazos.Should().Contain(r => r.Mensaje.Contains(nombrado, StringComparison.Ordinal), $"el rechazo debe nombrar {nombrado}");

            foreach (var e in esperado.Renglones)
            {
                var renglon = resultado.Renglones.SingleOrDefault(r => r.Linea == e.Linea && r.TaxRateCode == e.Tarifa);
                renglon.Should().NotBeNull($"falta el renglón {e.Tarifa} de la línea {e.Linea?.ToString() ?? "(documento)"}; omisiones: {string.Join(" | ", resultado.Omisiones)}");
                if (renglon is null) continue;
                var texto = renglon.Explicacion.Texto();
                renglon.Base.Should().Be(e.Base, $"base de {e.Tarifa}. Explicación: {texto}");
                renglon.Amount.Should().Be(e.Valor, $"valor de {e.Tarifa}. Explicación: {texto}");
                renglon.Treatment.Should().Be(e.Tratamiento, $"tratamiento de {e.Tarifa}");
                renglon.Explicacion.Pasos.Should().NotBeEmpty("todo renglón lleva explicación (FR-013)");
                renglon.Explicacion.Resumen.Should().NotBeNullOrWhiteSpace();
                foreach (var fragmento in e.Explicacion)
                    texto.Should().Contain(fragmento, $"la explicación de {e.Tarifa} debe decirlo");
            }

            var sobrantes = resultado.Renglones
                .Where(r => !esperado.Renglones.Any(e => e.Linea == r.Linea && e.Tarifa == r.TaxRateCode))
                .Select(r => $"{r.TaxRateCode} línea {r.Linea?.ToString() ?? "(documento)"} = {r.Amount}: {r.Explicacion.Texto()}")
                .ToList();
            sobrantes.Should().BeEmpty("no debe salir ningún renglón que no esté en lo esperado");
        }
    }

    [Fact]
    public void Estan_los_once_casos_de_T097()
    {
        CasoTributario.Archivos().Select(Path.GetFileName).Should().BeEquivalentTo(
        [
            "01-retefuente-base-igual-al-minimo.json",
            "02-retefuente-base-bajo-el-minimo.json",
            "03-gran-contribuyente-vs-no.json",
            "04-autorretenedor.json",
            "05-declarante-vs-no-declarante.json",
            "06-reteiva-sobre-el-iva.json",
            "07-reteica-municipio-con-caida-a-fila-general.json",
            "08-bolsas-por-unidad.json",
            "09-exento-vs-excluido.json",
            "10-nota-parcial-con-foto-del-original.json",
            "11-empate-de-tarifas.json",
        ]);
    }
}
