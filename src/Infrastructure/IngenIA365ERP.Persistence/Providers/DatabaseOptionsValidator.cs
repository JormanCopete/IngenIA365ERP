using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Validacion fail-fast de la seccion "Database" (FR-003). Registrada con
/// ValidateOnStart: si algo falla, el host NO arranca y el error indica la
/// clave exacta a corregir. La cadena del proveedor NO seleccionado nunca es
/// obligatoria (FR-004).
/// </summary>
public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();

        if (!DatabaseProviderParser.TryParse(options.Provider, out _))
        {
            failures.Add(
                $"[Database.InvalidProvider] Proveedor de base de datos '{options.Provider}' no soportado. " +
                $"Valores válidos: {DatabaseProviderParser.ValidValues}.");
        }
        else
        {
            var key = options.ProviderKey;
            if (!options.ConnectionStrings.TryGetValue(key, out var cs) || string.IsNullOrWhiteSpace(cs))
            {
                failures.Add(
                    $"[Database.ConnectionStringMissing] Falta la cadena de conexión para el proveedor " +
                    $"'{key}' (Database:ConnectionStrings:{key}).");
            }
        }

        if (options.Startup.RetryWindowSeconds < 0 || options.Startup.RetryIntervalSeconds < 1)
        {
            failures.Add(
                "[Database.InvalidStartupOptions] Database:Startup:RetryWindowSeconds debe ser ≥ 0 " +
                "y RetryIntervalSeconds ≥ 1.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
