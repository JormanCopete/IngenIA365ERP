using System.Data.Common;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Seccion de configuracion "Database" (feature 004 — contracts/configuration.md).
/// Gobierna el motor activo, las cadenas de conexion por proveedor y las
/// politicas de arranque (auto-migracion y seeding). Validada con fail-fast en
/// <see cref="DatabaseOptionsValidator"/> via ValidateOnStart.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"PostgreSQL" | "SqlServer" (case-insensitive).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Cadena de conexion de la BD operativa, por proveedor.</summary>
    public Dictionary<string, string?> ConnectionStrings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cadena de conexion de la BD administrativa central, por proveedor.
    /// Opcional: si falta, se deriva de la operativa con el sufijo "_Admin"
    /// (SQL Server) / "_admin" (PostgreSQL) sobre el nombre de base.
    /// </summary>
    public Dictionary<string, string?> AdminConnectionStrings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool AutoMigrate { get; set; }

    public SeedOptions Seed { get; set; } = new();

    public StartupOptions Startup { get; set; } = new();

    public sealed class SeedOptions
    {
        public bool RunParametricSeed { get; set; } = true;

        /// <summary>
        /// Nullable a proposito: si el operador no lo fija, el default efectivo
        /// depende del ambiente (on en Development/QA, off en Production — FR-017).
        /// </summary>
        public bool? RunTestSeed { get; set; }
    }

    public sealed class StartupOptions
    {
        public int RetryWindowSeconds { get; set; } = 60;
        public int RetryIntervalSeconds { get; set; } = 5;
    }

    // ---- Helpers de resolucion (validos solo tras pasar la validacion) ----

    public DatabaseProvider ResolvedProvider =>
        DatabaseProviderParser.TryParse(Provider, out var p)
            ? p
            : throw new InvalidOperationException(
                $"[Database.InvalidProvider] Proveedor de base de datos '{Provider}' no soportado. " +
                $"Valores válidos: {DatabaseProviderParser.ValidValues}.");

    /// <summary>Clave de diccionario canonica del proveedor activo.</summary>
    public string ProviderKey => ResolvedProvider == DatabaseProvider.PostgreSql ? "PostgreSQL" : "SqlServer";

    public string GetActiveConnectionString() =>
        ConnectionStrings.TryGetValue(ProviderKey, out var cs) && !string.IsNullOrWhiteSpace(cs)
            ? cs!
            : throw new InvalidOperationException(
                $"[Database.ConnectionStringMissing] Falta la cadena de conexión para el proveedor " +
                $"'{ProviderKey}' (Database:ConnectionStrings:{ProviderKey}).");

    public string GetActiveAdminConnectionString()
    {
        if (AdminConnectionStrings.TryGetValue(ProviderKey, out var cs) && !string.IsNullOrWhiteSpace(cs))
            return cs!;

        return DeriveAdminConnectionString(GetActiveConnectionString(), ResolvedProvider);
    }

    /// <summary>
    /// Deriva la cadena admin agregando el sufijo al nombre de la base.
    /// Publica para poder testearla en aislamiento.
    /// </summary>
    public static string DeriveAdminConnectionString(string connectionString, DatabaseProvider provider)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var dbKey = builder.ContainsKey("Database") ? "Database"
                  : builder.ContainsKey("Initial Catalog") ? "Initial Catalog"
                  : null;

        if (dbKey is null)
            throw new InvalidOperationException(
                "[Database.ConnectionStringMissing] No fue posible derivar la cadena admin: la cadena " +
                "operativa no declara 'Database'/'Initial Catalog'. Configure Database:AdminConnectionStrings explícitamente.");

        var suffix = provider == DatabaseProvider.PostgreSql ? "_admin" : "_Admin";
        builder[dbKey] = $"{builder[dbKey]}{suffix}";
        return builder.ConnectionString;
    }
}
