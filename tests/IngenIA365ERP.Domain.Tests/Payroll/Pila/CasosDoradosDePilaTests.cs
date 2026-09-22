using System.Text;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Pila;

namespace IngenIA365ERP.Domain.Tests.Payroll.Pila;

/// <summary>
/// SC-004 de la feature 010: la planilla del motor coincide al peso con la calculada a mano
/// (research R14) y el archivo sale byte a byte como el <c>.esperado.txt</c> del caso. Cuando
/// falla, el mensaje dice qué línea y qué subsistema, y adjunta la explicación del campo.
/// </summary>
public class CasosDoradosDePilaTests
{
    public static IEnumerable<object[]> Casos() => CasoDoradoPila.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_al_peso_con_la_planilla_manual(string archivo)
    {
        var caso = CasoDoradoPila.Cargar(Path.Combine(CasoDoradoPila.DirectorioDeCasos, archivo));
        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        caso.Derivacion.Should().NotBeNullOrWhiteSpace("todo caso dorado lleva su derivación a mano");

        var input = caso.ConstruirEntrada();
        var r = PilaBuilder.Build(input, caso.TablaFspDeTransicion());
        var e = caso.Esperado;

        var bloqueantes = r.Issues.Where(i => i.Severity == PilaIssueSeverity.Blocking).Select(i => i.Code).Distinct().ToList();
        bloqueantes.Should().BeEquivalentTo(e.Bloqueantes, "las bloqueantes del caso son exactamente las esperadas");
        foreach (var alerta in e.Alertas)
            r.Issues.Should().Contain(i => i.Severity == PilaIssueSeverity.Warning && i.Code == alerta, $"se esperaba la alerta {alerta}");

        r.ContributorCount.Should().Be(e.Cotizantes);
        r.LineCount.Should().Be(e.Lineas);
        r.TotalIbcFamilyCompensation.Should().Be(e.TotalIbcCcf, "campo 20 = Σ campo 45");
        r.TotalContributions.Should().Be(e.TotalAportes);

        foreach (var le in e.LineasEsperadas)
        {
            var l = r.Lines.FirstOrDefault(x => x.LineNumber == le.N);
            l.Should().NotBeNull($"debe existir la línea {le.N}");
            if (l is null) continue;
            var porque = $"línea {le.N} ({le.Documento}): {string.Join(" | ", l.Explanations.Where(x => x.Field is 42 or 47 or 51 or 55 or 63 or 65 or 67 or 69 or 76).Select(x => $"[{x.Field}] {x.Detail}"))}";
            l.Contributor.Document.Should().Be(le.Documento, porque);
            if (le.Novedades is not null) string.Join(",", l.Flags).Should().Be(le.Novedades, porque);
            if (le.Tipo is not null) $"{l.ContributorType}/{l.ContributorSubType}".Should().Be(le.Tipo, porque);
            l.DaysPension.Should().Be(le.Dias, porque);
            l.IbcFamilyCompensation.Should().Be(le.Ibc, porque);
            l.PensionTotal.Should().Be(le.Pension, porque);
            (l.SolidarityFund + l.SubsistenceFund).Should().Be(le.Fsp, porque);
            l.Health.Should().Be(le.Salud, porque);
            l.WorkRisk.Should().Be(le.Arl, porque);
            l.FamilyCompensation.Should().Be(le.Ccf, porque);
            l.Sena.Should().Be(le.Sena, porque);
            l.Icbf.Should().Be(le.Icbf, porque);
            l.Exempt.Should().Be(le.Exonerado, porque);
            if (le.Horas is { } h) l.Hours.Should().Be(h, porque);
        }

        // --- el archivo, byte a byte ---
        if (r.HasBlocking) return;
        var salida = PilaWriter.Write(input.Layout, r);
        salida.HeaderText.Should().HaveLength(input.Layout.Type1.Length);
        salida.LineTexts.Should().OnlyContain(t => t.Length == input.Layout.Type2.Length);
        salida.Content.Should().OnlyContain(b => b < 128, "el archivo es ASCII");

        var esperadoRuta = Path.Combine(CasoDoradoPila.DirectorioDeCasos, Path.GetFileNameWithoutExtension(archivo) + ".esperado.txt");
        // Regenerar el esperado a propósito (tras cotejar los números a mano): PILA_ESCRIBIR_ESPERADO=1 escribe en el
        // directorio de fuentes; nunca lo hace solo, porque un esperado que se reescribe solo no comprueba nada.
        if (Environment.GetEnvironmentVariable("PILA_ESCRIBIR_ESPERADO") == "1")
        {
            var fuente = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Payroll", "Pila", "Casos", Path.GetFileName(esperadoRuta)));
            File.WriteAllBytes(fuente, salida.Content);
            File.WriteAllBytes(esperadoRuta, salida.Content);
        }
        File.Exists(esperadoRuta).Should().BeTrue($"cada caso sin bloqueantes lleva su archivo esperado ({Path.GetFileName(esperadoRuta)})");
        if (!File.Exists(esperadoRuta)) return;
        var esperado = File.ReadAllBytes(esperadoRuta);
        if (!salida.Content.AsSpan().SequenceEqual(esperado))
        {
            var lineasEsperadas = Encoding.ASCII.GetString(esperado).Split("\r\n");
            var lineasSalida = salida.Text.Split("\r\n");
            for (var i = 0; i < Math.Max(lineasEsperadas.Length, lineasSalida.Length); i++)
            {
                var a = i < lineasEsperadas.Length ? lineasEsperadas[i] : "(falta)";
                var b = i < lineasSalida.Length ? lineasSalida[i] : "(falta)";
                if (a == b) continue;
                var pos = 0; while (pos < Math.Min(a.Length, b.Length) && a[pos] == b[pos]) pos++;
                var campo = input.Layout.Type2.Fields.FirstOrDefault(f => f.Start <= pos + 1 && pos + 1 < f.Start + f.Length);
                b.Should().Be(a, $"el renglón {i + 1} difiere en la posición {pos + 1}{(campo is null ? string.Empty : $" (campo {campo.Number} «{campo.Name}»)")}");
                break;
            }
        }
    }
}
