namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// «Ver en {módulo}» (feature 009, T083): la ruta de la pantalla del módulo para el documento que
/// originó un comprobante. La API manda <c>origin.link</c> cuando lo conoce; esto es el respaldo
/// del cliente para un servidor que aún no lo calcule, y se amplía módulo a módulo (T128).
/// </summary>
public static class EnlacesDeOrigen
{
    public static string? Ruta(string? sourceType, Guid? sourcePublicId) => sourcePublicId is null ? null : sourceType switch
    {
        "PayrollRun" => $"/nomina/liquidacion?corrida={sourcePublicId}",
        _ => null,
    };
}
