namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Motores relacionales soportados por el producto (feature 004 — multi-motor).
/// La seleccion ocurre exclusivamente por configuracion (seccion "Database");
/// ninguna otra capa conoce el proveedor concreto.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql
}

public static class DatabaseProviderParser
{
    /// <summary>
    /// Valores canonicos documentados: "PostgreSQL" y "SqlServer" (case-insensitive).
    /// Se toleran los alias comunes "postgres" y "mssql".
    /// </summary>
    public static bool TryParse(string? value, out DatabaseProvider provider)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "sqlserver":
            case "mssql":
                provider = DatabaseProvider.SqlServer;
                return true;
            case "postgresql":
            case "postgres":
                provider = DatabaseProvider.PostgreSql;
                return true;
            default:
                provider = default;
                return false;
        }
    }

    public const string ValidValues = "PostgreSQL, SqlServer";
}
