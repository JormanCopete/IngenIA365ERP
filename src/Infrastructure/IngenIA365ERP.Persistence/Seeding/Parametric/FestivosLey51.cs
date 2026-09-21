namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 010 (R6): el calendario de festivos de Colombia según la Ley 51 de 1983, calculado
/// para un año. Es el generador <b>de la semilla</b> de <c>PAY_Holidays</c>, no del cálculo: la
/// lista de festivos es un valor legal, y <c>Domain/Payroll</c> no lleva ninguno (SC-008), así
/// que el contador de días hábiles lee sólo la tabla —editable por la cooperativa, donde un
/// puente decretado tiene dónde registrarse— y este archivo vive en <c>Persistence/Seeding</c>.
///
/// <para>
/// Tres familias (art. 1 y 2 de la Ley 51): seis fijos que no se mueven (1 de enero, 1 de mayo,
/// 20 de julio, 7 de agosto, 8 y 25 de diciembre); siete que se trasladan al lunes siguiente
/// cuando no caen en lunes (Reyes, San José, San Pedro y San Pablo, Asunción, Raza, Todos los
/// Santos, Independencia de Cartagena); y cinco que dependen de la Pascua: Jueves y Viernes
/// Santo en su día, y Ascensión, Corpus Christi y Sagrado Corazón trasladados al lunes. La fecha
/// de Pascua sale del algoritmo de Computus (Meeus/Jones/Butcher, calendario gregoriano), que
/// es determinista: no hace falta descargar ningún calendario.
/// </para>
///
/// <para>
/// Da 18 festivos todos los años. El 1 de noviembre de 2026 es domingo, así que Todos los Santos
/// se celebra el lunes 2; ese es el caso fijo de la spec (28-10 a 10-11-2026 → 11 hábiles L–S).
/// </para>
/// </summary>
public static class FestivosLey51
{
    /// <summary>De dónde sale cada festivo; el seeder lo traduce al <c>HolidayOrigin</c> de la tabla.</summary>
    public enum Origen
    {
        /// <summary>Se celebra en su fecha (Ley 51 de 1983 art. 1 inc. 1).</summary>
        Fijo = 1,

        /// <summary>Se traslada al lunes siguiente cuando no cae en lunes (Ley 51 de 1983 art. 2).</summary>
        TrasladadoAlLunes = 2,

        /// <summary>Depende de la fecha de Pascua (Ley 51 de 1983 art. 1 y 2).</summary>
        Pascua = 3,
    }

    public sealed record Festivo(DateOnly Fecha, string Nombre, Origen Origen);

    private static readonly (int Mes, int Dia, string Nombre)[] Fijos =
    [
        (1, 1, "Año Nuevo"),
        (5, 1, "Día del Trabajo"),
        (7, 20, "Día de la Independencia"),
        (8, 7, "Batalla de Boyacá"),
        (12, 8, "Inmaculada Concepción"),
        (12, 25, "Navidad"),
    ];

    private static readonly (int Mes, int Dia, string Nombre)[] Trasladables =
    [
        (1, 6, "Reyes Magos"),
        (3, 19, "San José"),
        (6, 29, "San Pedro y San Pablo"),
        (8, 15, "Asunción de la Virgen"),
        (10, 12, "Día de la Raza"),
        (11, 1, "Todos los Santos"),
        (11, 11, "Independencia de Cartagena"),
    ];

    /// <summary>Los que se cuentan desde el Domingo de Pascua; los tres últimos caen en jueves o viernes y se corren al lunes.</summary>
    private static readonly (int DiasDesdePascua, string Nombre, bool AlLunes)[] DePascua =
    [
        (-3, "Jueves Santo", false),
        (-2, "Viernes Santo", false),
        (39, "Ascensión del Señor", true),
        (60, "Corpus Christi", true),
        (68, "Sagrado Corazón de Jesús", true),
    ];

    /// <summary>Los 18 festivos del año, en orden de fecha.</summary>
    public static IReadOnlyList<Festivo> DelAño(int año)
    {
        if (año < 1984) throw new ArgumentOutOfRangeException(nameof(año), año, "La Ley 51 de 1983 rige desde 1984.");

        var lista = new List<Festivo>();
        foreach (var (mes, dia, nombre) in Fijos)
            lista.Add(new Festivo(new DateOnly(año, mes, dia), nombre, Origen.Fijo));

        foreach (var (mes, dia, nombre) in Trasladables)
            lista.Add(new Festivo(AlLunesSiguiente(new DateOnly(año, mes, dia)), nombre, Origen.TrasladadoAlLunes));

        var pascua = DomingoDePascua(año);
        foreach (var (dias, nombre, alLunes) in DePascua)
        {
            var fecha = pascua.AddDays(dias);
            lista.Add(new Festivo(alLunes ? AlLunesSiguiente(fecha) : fecha, nombre, Origen.Pascua));
        }

        return lista.OrderBy(f => f.Fecha).ToList();
    }

    /// <summary>La misma fecha si ya es lunes; si no, el lunes que sigue (Ley 51 de 1983 art. 2).</summary>
    public static DateOnly AlLunesSiguiente(DateOnly fecha)
    {
        var corrimiento = ((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7;
        return fecha.AddDays(corrimiento);
    }

    /// <summary>
    /// Domingo de Pascua del calendario gregoriano por el algoritmo de Computus en la forma
    /// anónima de Meeus/Jones/Butcher. Es aritmética pura sobre el año; no hay tabla.
    /// </summary>
    public static DateOnly DomingoDePascua(int año)
    {
        var a = año % 19;
        var b = año / 100;
        var c = año % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(año, mes, dia);
    }
}
