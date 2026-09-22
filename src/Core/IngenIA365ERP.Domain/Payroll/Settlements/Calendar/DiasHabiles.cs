namespace IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

/// <summary>Un día del rango que no cuenta como hábil y por qué («domingo», «sábado», «festivo: Todos los Santos»).</summary>
public sealed record DiaSaltado(DateTime Fecha, string Motivo);

/// <summary>Lo que salió de contar un rango: hábiles, calendario y cada día saltado con su motivo.</summary>
public sealed record ConteoDeDiasHabiles(int Habiles, int Calendario, IReadOnlyList<DiaSaltado> Saltados);

/// <summary>
/// Cuenta los días hábiles de un disfrute de vacaciones (CST art. 186: quince días
/// hábiles consecutivos; FR-015). Es una función pura: recibe las fechas, la semana
/// laboral vigente de la empresa y la lista de festivos —que viene de
/// <c>PAY_Holidays</c>, sembrada con la Ley 51 de 1983 y editable— y devuelve el conteo
/// con cada día saltado explicado, para que la pantalla lo muestre antes de guardar.
/// Aquí no hay ningún festivo escrito: el calendario es dato de la cooperativa
/// (research R6).
/// </summary>
public static class DiasHabiles
{
    public static ConteoDeDiasHabiles Contar(DateTime desde, DateTime hasta, SemanaLaboral semana, IEnumerable<DateTime> festivos)
    {
        desde = desde.Date;
        hasta = hasta.Date;
        if (hasta < desde) return new ConteoDeDiasHabiles(0, 0, []);

        var festivosSet = festivos.Select(f => f.Date).ToHashSet();
        var saltados = new List<DiaSaltado>();
        var habiles = 0;
        var calendario = 0;

        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
        {
            calendario++;
            if (dia.DayOfWeek == DayOfWeek.Sunday)
            {
                saltados.Add(new DiaSaltado(dia, "domingo"));
                continue;
            }
            if (dia.DayOfWeek == DayOfWeek.Saturday && semana == SemanaLaboral.LunesAViernes)
            {
                saltados.Add(new DiaSaltado(dia, "sábado (semana laboral de lunes a viernes)"));
                continue;
            }
            if (festivosSet.Contains(dia))
            {
                saltados.Add(new DiaSaltado(dia, "festivo"));
                continue;
            }
            habiles++;
        }

        return new ConteoDeDiasHabiles(habiles, calendario, saltados);
    }

    /// <summary>Igual que <see cref="Contar(DateTime, DateTime, SemanaLaboral, IEnumerable{DateTime})"/> pero con el nombre de cada festivo en el motivo.</summary>
    public static ConteoDeDiasHabiles Contar(DateTime desde, DateTime hasta, SemanaLaboral semana, IReadOnlyDictionary<DateTime, string> festivos)
    {
        var conteo = Contar(desde, hasta, semana, festivos.Keys);
        var conNombre = conteo.Saltados
            .Select(s => s.Motivo == "festivo" && festivos.TryGetValue(s.Fecha.Date, out var nombre) && !string.IsNullOrWhiteSpace(nombre)
                ? new DiaSaltado(s.Fecha, $"festivo: {nombre}")
                : s)
            .ToList();
        return conteo with { Saltados = conNombre };
    }

    /// <summary>
    /// Fecha en que terminan <paramref name="habiles"/> días hábiles contados desde
    /// <paramref name="desde"/> inclusive: para proponer la fecha de regreso cuando la
    /// solicitud llega en días y no en fechas. Con cero días devuelve el día anterior.
    /// </summary>
    public static DateTime FinDeHabiles(DateTime desde, int habiles, SemanaLaboral semana, IEnumerable<DateTime> festivos)
    {
        desde = desde.Date;
        if (habiles <= 0) return desde.AddDays(-1);
        var festivosSet = festivos.Select(f => f.Date).ToHashSet();
        var contados = 0;
        var dia = desde.AddDays(-1);
        while (contados < habiles)
        {
            dia = dia.AddDays(1);
            if (dia.DayOfWeek == DayOfWeek.Sunday) continue;
            if (dia.DayOfWeek == DayOfWeek.Saturday && semana == SemanaLaboral.LunesAViernes) continue;
            if (festivosSet.Contains(dia)) continue;
            contados++;
        }
        return dia;
    }
}
