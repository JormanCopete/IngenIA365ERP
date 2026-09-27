using System.Globalization;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Las dos claves que sella una entrega por lotes (feature 012, T9; contracts/mensajes.md §11; data-model §19). Se
/// construyen aquí para que nadie las arme a mano con otro formato.
/// </summary>
public static class ClavesDeLote
{
    /// <summary>
    /// <c>ScheduleKey</c>: <c>{DocumentTypeCode raíz}|{DisparadorDeLote}|{HoraDeLote}|{Granularidad}</c>, sellados al
    /// confirmar. Todas las entregas de una clave comparten horario y granularidad. La hora es <c>HH:mm</c> o vacía
    /// si el disparador no la usa.
    /// </summary>
    public static string Horario(string tipoDeDocumentoRaiz, string disparador, TimeOnly? hora, string granularidad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeDocumentoRaiz);
        ArgumentException.ThrowIfNullOrWhiteSpace(disparador);
        ArgumentException.ThrowIfNullOrWhiteSpace(granularidad);
        var textoDeHora = hora?.ToString("HH':'mm", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{tipoDeDocumentoRaiz}|{disparador}|{textoDeHora}|{granularidad}";
    }

    /// <summary>
    /// Lee una <c>ScheduleKey</c> (feature 012, T502): tipo raíz, disparador, hora (nula si vacía) y granularidad. Nulo si
    /// no tiene las cuatro partes. (nuevo)
    /// </summary>
    public static HorarioDeLote? Leer(string? scheduleKey)
    {
        if (string.IsNullOrWhiteSpace(scheduleKey)) return null;
        var partes = scheduleKey.Split('|');
        if (partes.Length != 4 || string.IsNullOrWhiteSpace(partes[0]) || string.IsNullOrWhiteSpace(partes[1])) return null;
        TimeOnly? hora = TimeOnly.TryParseExact(partes[2], "HH':'mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : null;
        return new HorarioDeLote(partes[0], partes[1], hora, partes[3]);
    }

    /// <summary>El disparador de la franja diaria (<c>Contabilidad.DisparadorDeLote</c>).</summary>
    public const string HoraDiaria = "HoraDiaria";

    /// <summary>El disparador del cierre de turno (<c>Contabilidad.DisparadorDeLote</c>; el lote lo crea el cierre de la sesión de caja, I3). (nuevo, T522)</summary>
    public const string CierreDeTurno = "CierreDeTurno";

    /// <summary>El disparador del cierre del período de inventario (<c>Contabilidad.DisparadorDeLote</c>; el lote lo crea <c>CloseInventoryPeriodCommand</c>). (nuevo, T522)</summary>
    public const string CierreDePeriodo = "CierreDePeriodo";

    /// <summary><c>BatchScopeKey</c> del disparador <c>CierreDeTurno</c>: <c>CashSession:{publicId}</c>.</summary>
    public static string SesionDeCaja(Guid cashSessionPublicId) => $"CashSession:{cashSessionPublicId:D}";

    /// <summary><c>BatchScopeKey</c> del disparador <c>CierreDePeriodo</c>: <c>Period:{aaaa-mm}</c>.</summary>
    public static string Periodo(int anio, int mes) =>
        $"Period:{anio.ToString("D4", CultureInfo.InvariantCulture)}-{mes.ToString("D2", CultureInfo.InvariantCulture)}";
}

/// <summary>
/// Una <c>ScheduleKey</c> leída (feature 012, T502). La granularidad es el texto sellado (<c>PorDocumento</c> o
/// <c>Resumido</c>; también se admite el nombre del enum). (nuevo)
/// </summary>
public sealed record HorarioDeLote(string TipoDeDocumentoRaiz, string Disparador, TimeOnly? Hora, string Granularidad)
{
    public Domain.Enums.Inventory.PostingGranularity GranularidadDelLote =>
        Granularidad is "Resumido" or nameof(Domain.Enums.Inventory.PostingGranularity.Summarized)
            ? Domain.Enums.Inventory.PostingGranularity.Summarized
            : Domain.Enums.Inventory.PostingGranularity.PerDocument;
}
