using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.ElectronicInvoicing;

/// <summary>El plazo de transmisión de un documento de contingencia, con de dónde salió. (nuevo)</summary>
/// <param name="DesdeCuando">Desde qué instante se cuenta.</param>
/// <param name="Plazo">Hasta cuándo hay para transmitir (<c>TransmissionDeadline</c>).</param>
/// <param name="HorasAplicadas">Las horas del parámetro vigente (<c>DeadlineHoursApplied</c>).</param>
/// <param name="FuenteLegal">La norma del parámetro (<c>LegalSource</c>).</param>
public sealed record PlazoCalculado(DateTimeOffset DesdeCuando, DateTimeOffset Plazo, int HorasAplicadas, string FuenteLegal);

/// <summary>
/// El plazo para transmitir lo expedido en contingencia (feature 012, I4, T697; contracts/dian.md §7.3; FR-067,
/// SC-015), puro. Ninguna hora es del programa: las horas y la norma son las de <c>Dian.PlazoContingenciaHoras</c>
/// vigente a la fecha del documento, y la alerta sale <c>Dian.AlertaHorasAntesDelPlazo</c> horas antes; las dos llegan
/// por parámetro. Desde cuándo se cuenta depende del tipo:
/// <list type="bullet">
/// <item>factura, documento equivalente POS y sus notas: desde el fin del evento (<c>EndedAt</c>, «desde que se supera
/// el inconveniente»);</item>
/// <item>documento soporte y su nota de ajuste: desde las 00:00 del día siguiente al fin del evento, en hora local
/// (Res. 167, arts. 11-12).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class PlazoDeContingencia
{
    /// <summary>¿Este tipo cuenta desde el día siguiente al fin del evento?</summary>
    public static bool CuentaDesdeElDiaSiguiente(ElectronicDocumentKind tipo) =>
        tipo is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote;

    /// <summary>Calcula el plazo de un documento del evento.</summary>
    /// <param name="tipo">El tipo del documento.</param>
    /// <param name="finDelEvento">El fin del evento de contingencia.</param>
    /// <param name="horas">Las horas del parámetro vigente (positivas).</param>
    /// <param name="fuenteLegal">La norma del parámetro (obligatoria).</param>
    /// <param name="desfaseLocal">
    /// El desfase de la hora local de la cooperativa (el de <c>IDateTimeService</c>); si no llega, el de
    /// <paramref name="finDelEvento"/>. Sólo importa para saber qué día es «el día siguiente».
    /// </param>
    public static PlazoCalculado Calcular(
        ElectronicDocumentKind tipo, DateTimeOffset finDelEvento, int horas, string fuenteLegal, TimeSpan? desfaseLocal = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(horas);
        ArgumentException.ThrowIfNullOrWhiteSpace(fuenteLegal);

        var fin = finDelEvento.ToOffset(desfaseLocal ?? finDelEvento.Offset);
        var desde = CuentaDesdeElDiaSiguiente(tipo)
            ? new DateTimeOffset(fin.Date.AddDays(1), fin.Offset)
            : fin;

        return new PlazoCalculado(desde, desde.AddHours(horas), horas, fuenteLegal.Trim());
    }

    /// <summary>El instante de la alerta <c>Dian.PlazoDeContingencia</c>: el plazo menos las horas del parámetro.</summary>
    public static DateTimeOffset InstanteDeLaAlerta(DateTimeOffset plazo, int horasAntes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horasAntes);
        return plazo.AddHours(-horasAntes);
    }

    /// <summary>Como <see cref="InstanteDeLaAlerta(DateTimeOffset, int)"/>, pero nunca antes de que empiece el conteo.</summary>
    public static DateTimeOffset InstanteDeLaAlerta(PlazoCalculado plazo, int horasAntes)
    {
        ArgumentNullException.ThrowIfNull(plazo);
        var alerta = InstanteDeLaAlerta(plazo.Plazo, horasAntes);
        return alerta < plazo.DesdeCuando ? plazo.DesdeCuando : alerta;
    }
}
