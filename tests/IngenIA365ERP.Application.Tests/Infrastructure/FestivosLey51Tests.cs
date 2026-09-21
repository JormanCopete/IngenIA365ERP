using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 010 (T018, R6): el generador de festivos de la Ley 51 de 1983 contra tres calendarios
/// esperados (<c>Festivos/2026.json</c>, <c>2027.json</c>, <c>2028.json</c>) calculados a mano y
/// contrastados con los publicados. Los valores viven en el JSON, nunca en la prueba, para que
/// quien los revise no tenga que leer C#. El caso fijo de la spec: el 1 de noviembre de 2026 es
/// domingo, Todos los Santos se celebra el lunes 2, y el 1 no es festivo.
/// </summary>
public class FestivosLey51Tests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<int> Años => [2026, 2027, 2028];

    private static CalendarioEsperado Esperado(int año)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Festivos", $"{año}.json");
        return JsonSerializer.Deserialize<CalendarioEsperado>(File.ReadAllText(ruta), Opciones)
            ?? throw new InvalidOperationException($"El calendario {ruta} está vacío.");
    }

    [Theory]
    [MemberData(nameof(Años))]
    public void El_calendario_del_anio_coincide_con_el_esperado(int año)
    {
        var esperado = Esperado(año);
        esperado.Anio.Should().Be(año);
        esperado.Festivos.Should().HaveCount(18, "la Ley 51 de 1983 deja 18 festivos todos los años");

        var generado = FestivosLey51.DelAño(año);

        FestivosLey51.DomingoDePascua(año).Should().Be(esperado.Pascua);
        generado.Select(f => (f.Fecha, f.Nombre, f.Origen))
            .Should().Equal(esperado.Festivos.Select(f => (f.Fecha, f.Nombre, f.Origen)),
                "el generador tiene que dar exactamente el calendario revisado, en orden de fecha");
    }

    [Fact]
    public void En_2026_todos_los_santos_cae_domingo_y_se_celebra_el_lunes_2_de_noviembre()
    {
        var festivos = FestivosLey51.DelAño(2026);

        festivos.Should().HaveCount(18);
        festivos.Select(f => f.Fecha).Should().Contain(new DateOnly(2026, 11, 2));
        festivos.Select(f => f.Fecha).Should().NotContain(new DateOnly(2026, 11, 1), "el domingo no es festivo: el festivo se trasladó al lunes");
        new DateOnly(2026, 11, 2).DayOfWeek.Should().Be(DayOfWeek.Monday);
        festivos.Single(f => f.Fecha == new DateOnly(2026, 11, 2)).Origen.Should().Be(FestivosLey51.Origen.TrasladadoAlLunes);
    }

    [Theory]
    [MemberData(nameof(Años))]
    public void Los_trasladables_y_los_de_pascua_movidos_caen_siempre_en_lunes(int año)
    {
        var festivos = FestivosLey51.DelAño(año);

        festivos.Where(f => f.Origen == FestivosLey51.Origen.TrasladadoAlLunes)
            .Should().OnlyContain(f => f.Fecha.DayOfWeek == DayOfWeek.Monday);
        festivos.Where(f => f.Origen == FestivosLey51.Origen.Pascua && f.Nombre is not ("Jueves Santo" or "Viernes Santo"))
            .Should().OnlyContain(f => f.Fecha.DayOfWeek == DayOfWeek.Monday);
        festivos.Single(f => f.Nombre == "Jueves Santo").Fecha.DayOfWeek.Should().Be(DayOfWeek.Thursday);
        festivos.Single(f => f.Nombre == "Viernes Santo").Fecha.DayOfWeek.Should().Be(DayOfWeek.Friday);
        festivos.Select(f => f.Fecha).Should().OnlyHaveUniqueItems("dos festivos el mismo día se insertarían dos veces por Date");
    }

    [Fact]
    public void Un_lunes_no_se_traslada_y_cualquier_otro_dia_va_al_lunes_siguiente()
    {
        FestivosLey51.AlLunesSiguiente(new DateOnly(2026, 6, 29)).Should().Be(new DateOnly(2026, 6, 29), "ya es lunes");
        FestivosLey51.AlLunesSiguiente(new DateOnly(2026, 1, 6)).Should().Be(new DateOnly(2026, 1, 12), "martes → lunes siguiente");
        FestivosLey51.AlLunesSiguiente(new DateOnly(2026, 11, 1)).Should().Be(new DateOnly(2026, 11, 2), "domingo → lunes siguiente");
    }

    [Fact]
    public void Antes_de_la_ley_no_hay_calendario()
    {
        var acto = () => FestivosLey51.DelAño(1983);
        acto.Should().Throw<ArgumentOutOfRangeException>();
    }

    public sealed class CalendarioEsperado
    {
        public int Anio { get; set; }
        public DateOnly Pascua { get; set; }
        public List<FestivoEsperado> Festivos { get; set; } = [];
    }

    public sealed class FestivoEsperado
    {
        public DateOnly Fecha { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public FestivosLey51.Origen Origen { get; set; }
    }
}
