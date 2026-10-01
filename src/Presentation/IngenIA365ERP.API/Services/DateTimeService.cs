using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Services;

/// <summary>Sección <c>Plataforma</c> de appsettings (feature 012, T20).</summary>
public sealed class PlataformaOptions
{
    public const string SectionName = "Plataforma";

    /// <summary>Zona IANA de la hora local de la plataforma. Una sola para todas las cooperativas.</summary>
    public string ZonaHoraria { get; set; } = "America/Bogota";
}

/// <summary>
/// El reloj de la API. <see cref="AhoraLocal"/> y <see cref="HoyLocal"/> (feature 012, T20) usan la
/// zona de <c>Plataforma:ZonaHoraria</c>; si la imagen no la trae (un contenedor sin
/// <c>tzdata</c>), se usa −05:00 fijo —Colombia no tiene horario de verano, así que da lo mismo— y se
/// avisa <b>una sola vez</b> en el log, al construir el servicio.
/// </summary>
public class DateTimeService : IDateTimeService
{
    private static readonly TimeSpan DesfaseFijo = TimeSpan.FromHours(-5);

    private readonly TimeZoneInfo? _zona;

    public DateTimeService(IOptions<PlataformaOptions> opciones, ILogger<DateTimeService> logger)
    {
        var nombre = opciones.Value.ZonaHoraria;
        if (string.IsNullOrWhiteSpace(nombre)) nombre = "America/Bogota";

        if (TimeZoneInfo.TryFindSystemTimeZoneById(nombre, out var zona))
        {
            _zona = zona;
        }
        else
        {
            logger.LogWarning(
                "[Plataforma.ZonaHorariaNoDisponible] La zona horaria «{Zona}» no está en esta imagen; " +
                "la hora local se calcula con −05:00 fijo.", nombre);
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    public DateTimeOffset AhoraLocal => ALocal(DateTime.UtcNow);

    public DateOnly HoyLocal => DateOnly.FromDateTime(AhoraLocal.DateTime);

    /// <summary>Un instante UTC en la hora local de la plataforma. Público para probarlo sin esperar al reloj.</summary>
    public DateTimeOffset ALocal(DateTime utc)
    {
        var instante = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return _zona is null ? instante.ToOffset(DesfaseFijo) : TimeZoneInfo.ConvertTime(instante, _zona);
    }
}
