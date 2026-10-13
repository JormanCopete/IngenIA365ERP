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

    /// <summary>El prefijo de las claves de Cartera: <c>CAR.{ShortCode}|{disparador}|{hora}|{granularidad}</c> (feature 013, C06).</summary>
    public const string PrefijoDeCartera = "CAR.";

    /// <summary>El disparador de la franja diaria (<c>Contabilidad.DisparadorDeLote</c>).</summary>
    public const string HoraDiaria = "HoraDiaria";

    /// <summary>El disparador del cierre de turno (<c>Contabilidad.DisparadorDeLote</c>; el lote lo crea el cierre de la sesión de caja, I3). (nuevo, T522)</summary>
    public const string CierreDeTurno = "CierreDeTurno";

    /// <summary>El disparador del cierre del período de inventario (<c>Contabilidad.DisparadorDeLote</c>; el lote lo crea <c>CloseInventoryPeriodCommand</c>). (nuevo, T522)</summary>
    public const string CierreDePeriodo = "CierreDePeriodo";

    /// <summary>
    /// El disparador de Cartera que agrupa lo resumido al terminar la causación del día (<c>Contabilidad.DisparadorDeLote</c> en el ámbito
    /// <c>LendingFactType</c>; feature 013, T0635, C06, C09; contabilidad.md §10.2): el lote lo crea el guardado con que termina la corrida
    /// <c>DailyCycle</c> (<c>BatchTrigger.LendingAccrualRun</c>). La hora va vacía. (nuevo)
    /// </summary>
    public const string FinDeCausacion = "FinDeCausacion";

    /// <summary>
    /// El disparador de Cartera del cierre mensual (feature 013, T0635, C06): el lote lo crea el cierre de Cartera
    /// (<c>BatchTrigger.LendingMonthlyClose</c>) con alcance <c>LendingClose:{aaaa-mm}</c>. La hora va vacía. (nuevo)
    /// </summary>
    public const string CierreDeCartera = "CierreDeCartera";

    /// <summary>
    /// Granularidades de un lote de Cartera (<c>Contabilidad.GranularidadDeLote</c>; feature 013, T0635, C07, C09): un comprobante por
    /// operación (<see cref="GranularidadPorHecho"/>), uno por día y tipo de comprobante con la sucursal en cada línea
    /// (<see cref="GranularidadPorDia"/>, el defecto) o uno por día, tipo y sucursal (<see cref="GranularidadPorDiaYSucursal"/>). (nuevo)
    /// </summary>
    public const string GranularidadPorHecho = "PorHecho";

    public const string GranularidadPorDia = "PorDia";

    public const string GranularidadPorDiaYSucursal = "PorDiaYSucursal";

    /// <summary>Las tres granularidades de Cartera, en el orden del parámetro.</summary>
    public static IReadOnlyList<string> GranularidadesDeCartera { get; } = [GranularidadPorDia, GranularidadPorDiaYSucursal, GranularidadPorHecho];

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
    /// <summary>
    /// El módulo de la clave (feature 013, C06): las de Cartera empiezan por <c>CAR.</c> (<c>CAR.{ShortCode}</c>); las de Inventario
    /// llevan el código del tipo de documento, sin prefijo de módulo.
    /// </summary>
    public string Modulo => TipoDeDocumentoRaiz.StartsWith(ClavesDeLote.PrefijoDeCartera, StringComparison.Ordinal)
        ? ModulosDeOrigen.Cartera
        : ModulosDeOrigen.Inventario;

    /// <summary>¿Es una clave de Inventario (la única que usa el respaldo de <c>InventoryDocumentTypes</c>)?</summary>
    public bool EsDeInventario => Modulo == ModulosDeOrigen.Inventario;

    public Domain.Enums.Inventory.PostingGranularity GranularidadDelLote =>
        Granularidad is "Resumido" or nameof(Domain.Enums.Inventory.PostingGranularity.Summarized)
            ? Domain.Enums.Inventory.PostingGranularity.Summarized
            : Domain.Enums.Inventory.PostingGranularity.PerDocument;
}
