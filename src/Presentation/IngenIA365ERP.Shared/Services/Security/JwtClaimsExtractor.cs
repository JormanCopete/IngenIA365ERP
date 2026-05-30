using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Decodifica un JWT en cliente para extraer sus claims, sin verificar la
/// firma — la verificación de integridad la hace el backend en cada request.
/// El propósito aquí es UI: <c>PermissionGate</c> necesita saber qué
/// <c>perm</c> tiene el usuario para mostrar/ocultar componentes.
///
/// Hacer base64-decode manual evita arrastrar <c>System.IdentityModel.Tokens.Jwt</c>
/// al bundle WASM. Mismo riesgo que cualquier UI guard del lado cliente —
/// no es una barrera de seguridad, solo de ergonomía.
/// </summary>
public static class JwtClaimsExtractor
{
    public static IReadOnlyList<Claim> Extract(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return Array.Empty<Claim>();

        var parts = jwt.Split('.');
        if (parts.Length < 2) return Array.Empty<Claim>();

        var payload = parts[1];
        var json = DecodeBase64Url(payload);
        if (json is null) return Array.Empty<Claim>();

        using var doc = JsonDocument.Parse(json);
        var claims = new List<Claim>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    claims.Add(new Claim(prop.Name, item.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(prop.Name, prop.Value.ToString()));
            }
        }
        return claims;
    }

    private static string? DecodeBase64Url(string base64Url)
    {
        try
        {
            var padded = base64Url
                .Replace('-', '+')
                .Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            // Token mal formado — devolvemos null para que el caller lo trate como "no claims".
            return null;
        }
    }
}
