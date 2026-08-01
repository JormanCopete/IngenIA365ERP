namespace IngenIA365ERP.Audit.Bootstrap;

/// <summary>
/// Reporte de qué cambió y qué quedó como estaba al aplicar el descriptor.
/// Permite al CLI imprimir un resumen accionable y a los tests aserrar
/// idempotencia (segunda corrida → todos los contadores en 0).
/// </summary>
public sealed class AuditBootstrapResult
{
    public string Database { get; init; } = string.Empty;

    public List<string> CollectionsCreated { get; } = new();
    public List<string> CollectionsAlreadyExisted { get; } = new();

    public List<string> IndexesCreated { get; } = new();
    public List<string> IndexesAlreadyExisted { get; } = new();

    public List<string> RolesCreated { get; } = new();
    public List<string> RolesAlreadyExisted { get; } = new();

    public List<string> UsersCreated { get; } = new();
    public List<string> UsersUpdated { get; } = new();

    public int TotalChanges =>
        CollectionsCreated.Count +
        IndexesCreated.Count +
        RolesCreated.Count +
        UsersCreated.Count +
        UsersUpdated.Count;
}
