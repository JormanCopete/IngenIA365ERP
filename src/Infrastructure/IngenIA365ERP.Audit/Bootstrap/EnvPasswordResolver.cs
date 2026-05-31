namespace IngenIA365ERP.Audit.Bootstrap;

/// <summary>
/// Resuelve el <c>passwordSource</c> del descriptor a un secreto en claro.
/// Hoy solo soporta el prefijo <c>env:</c>. Aislado en una clase para que
/// los tests sustituyan la resolución sin tocar variables de entorno reales.
/// </summary>
public interface IPasswordResolver
{
    string Resolve(string passwordSource);
}

public sealed class EnvPasswordResolver : IPasswordResolver
{
    private const string EnvPrefix = "env:";

    public string Resolve(string passwordSource)
    {
        if (string.IsNullOrWhiteSpace(passwordSource))
            throw new ArgumentException("passwordSource is empty", nameof(passwordSource));

        if (!passwordSource.StartsWith(EnvPrefix, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"passwordSource '{passwordSource}' no es soportado. Formatos válidos: 'env:VAR_NAME'.");
        }

        var varName = passwordSource[EnvPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(varName))
            throw new ArgumentException($"passwordSource '{passwordSource}' no especifica el nombre de la variable.");

        var value = Environment.GetEnvironmentVariable(varName);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"La variable de entorno '{varName}' (requerida por passwordSource '{passwordSource}') no está definida o está vacía.");
        }

        return value;
    }
}
