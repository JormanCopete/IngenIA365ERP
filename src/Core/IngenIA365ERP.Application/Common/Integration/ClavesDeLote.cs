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

    /// <summary><c>BatchScopeKey</c> del disparador <c>CierreDeTurno</c>: <c>CashSession:{publicId}</c>.</summary>
    public static string SesionDeCaja(Guid cashSessionPublicId) => $"CashSession:{cashSessionPublicId:D}";

    /// <summary><c>BatchScopeKey</c> del disparador <c>CierreDePeriodo</c>: <c>Period:{aaaa-mm}</c>.</summary>
    public static string Periodo(int anio, int mes) =>
        $"Period:{anio.ToString("D4", CultureInfo.InvariantCulture)}-{mes.ToString("D2", CultureInfo.InvariantCulture)}";
}
