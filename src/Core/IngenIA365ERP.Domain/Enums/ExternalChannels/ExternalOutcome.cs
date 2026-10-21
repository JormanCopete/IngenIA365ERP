namespace IngenIA365ERP.Domain.Enums.ExternalChannels;

/// <summary>
/// Resultado de una llamada a un adaptador externo (feature 013, T71, F-01; data-model parte 3 §9.2;
/// contracts/adaptadores-y-archivos.md §3.1). Viaja en <c>ResultadoExterno&lt;T&gt;</c> y se guarda como int en
/// <c>COR_ExternalChannelSettings.LastVerificationOutcome</c>. Un adaptador nunca lanza por rechazo ni por transporte:
/// lo dice aquí.
/// </summary>
public enum ExternalOutcome
{
    Ok = 1,
    Rejected = 2,

    /// <summary>Pudo haber llegado y todavía no hay respuesta definitiva.</summary>
    InProcess = 3,

    NotFound = 4,

    /// <summary>No salió: el canal no estaba disponible o no hay adaptador para el código sellado.</summary>
    ChannelUnavailable = 5,

    InvalidData = 6,
}
