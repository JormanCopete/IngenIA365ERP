namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }

    /// <summary>
    /// El instante actual en la hora local de la plataforma (feature 012, T20). Los instantes se
    /// guardan en UTC; esto es para decidir fechas y horas «de calendario» (lotes programados, la
    /// hora que va a la DIAN con −05:00).
    ///
    /// <para>
    /// La implementación por defecto usa −05:00 fijo sobre <see cref="UtcNow"/>, para que los
    /// relojes falsos de las pruebas sigan compilando sin cambios; la API la reemplaza con la zona
    /// de <c>Plataforma:ZonaHoraria</c> (por defecto <c>America/Bogota</c>). Un sustituto de
    /// NSubstitute NO ejecuta esta implementación: devuelve el valor por defecto salvo que la
    /// prueba lo configure.
    /// </para>
    /// </summary>
    DateTimeOffset AhoraLocal =>
        new DateTimeOffset(DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc)).ToOffset(DesfaseColombia);

    /// <summary>
    /// La <b>fecha de operación</b>: el día local. Entre las 19:00 y la medianoche de Colombia
    /// <see cref="TodayUtc"/> ya dice mañana, y una venta de las 8 de la noche quedaría fechada al
    /// día siguiente. La usan Inventario, POS y los lotes; los demás módulos siguen con
    /// <see cref="TodayUtc"/> hasta adoptarla.
    /// </summary>
    DateOnly HoyLocal => DateOnly.FromDateTime(AhoraLocal.DateTime);

    /// <summary>Colombia no tiene horario de verano: −05:00 todo el año.</summary>
    static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);
}
