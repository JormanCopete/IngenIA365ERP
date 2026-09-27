namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los valores técnicos de <c>Integration:Retries</c> que usa <c>RegisterDeliveryResultCommand</c> (feature 012, T10, T498;
/// contracts/mensajes.md §11). Los defectos son los del contrato; la API los copia de <c>IntegrationOptions.Retries</c>
/// (T526/T527), que es donde se configuran y se validan al arrancar. (nuevo)
/// </summary>
public sealed class ReintentosDeIntegracion
{
    public int BaseDelaySeconds { get; set; } = 15;

    public int MaxDelayMinutes { get; set; } = 15;

    public int AlertAfterAttempts { get; set; } = 3;

    public int AlertAfterMinutes { get; set; } = 15;

    /// <summary>La dispersión máxima, como fracción de la espera: evita que las réplicas reintenten todas a la vez.</summary>
    public const double Dispersion = 0.1;

    /// <summary>
    /// La espera antes del intento siguiente al intento <paramref name="intento"/> (1, 2, …):
    /// <c>min(base·2^(n−1), tope)</c> más una dispersión de hasta el 10 % (<paramref name="aleatorio"/> en [0, 1)).
    /// </summary>
    public TimeSpan Espera(int intento, double aleatorio)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intento, 1);
        var tope = TimeSpan.FromMinutes(MaxDelayMinutes);
        var exponente = Math.Min(intento - 1, 30);
        var cruda = TimeSpan.FromSeconds(BaseDelaySeconds * Math.Pow(2, exponente));
        var espera = cruda > tope ? tope : cruda;
        return espera + TimeSpan.FromMilliseconds(espera.TotalMilliseconds * Dispersion * Math.Clamp(aleatorio, 0, 1));
    }
}
