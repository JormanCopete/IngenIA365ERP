using System.Data.Common;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Enmascara credenciales de una cadena de conexion antes de citarla en logs
/// o mensajes de error (FR-006, D-12). Host y base de datos quedan visibles
/// (necesarios para diagnostico); usuario y password se ocultan.
/// TODO log que cite una cadena DEBE pasar por aqui.
/// </summary>
public static class ConnectionStringMasker
{
    private static readonly string[] SecretKeys =
    [
        "password", "pwd", "user id", "userid", "uid", "username", "user"
    ];

    public static string Mask(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "(vacía)";

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (var key in SecretKeys)
            {
                if (builder.ContainsKey(key))
                    builder[key] = "***";
            }
            return builder.ConnectionString;
        }
        catch
        {
            // Cadena no parseable: no arriesgamos filtrar nada.
            return "(cadena no parseable — enmascarada por completo)";
        }
    }
}
