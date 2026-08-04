namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Estado de la inicializacion de base de datos (feature 004). El health check
/// "ready" reporta no-listo hasta que el <see cref="DatabaseInitializerHostedService"/>
/// termina (FR-022, D-09).
/// </summary>
public sealed class DatabaseReadiness
{
    public bool IsReady { get; private set; }
    public string Status { get; private set; } = "Inicialización de base de datos pendiente";

    public void MarkReady() => (IsReady, Status) = (true, "Base de datos migrada y sembrada");
    public void MarkFailed(string reason) => (IsReady, Status) = (false, reason);
}
