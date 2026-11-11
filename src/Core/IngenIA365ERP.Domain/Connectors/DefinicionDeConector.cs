using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Connectors;

namespace IngenIA365ERP.Domain.Connectors;

/// <summary>Cómo se autentica un conector (contracts/api.md §1).</summary>
public sealed record AutenticacionDeConector
{
    public ConnectorAuthKind Kind { get; init; } = ConnectorAuthKind.None;

    /// <summary>LoginToken: el método y la ruta del inicio de sesión.</summary>
    public string? Method { get; init; }

    public string? Path { get; init; }

    /// <summary>LoginToken: la plantilla del cuerpo del inicio de sesión (con <c>{{cred.*}}</c>).</summary>
    public string? Body { get; init; }

    /// <summary>LoginToken: dónde está el token en la respuesta (<c>$.token</c>, <c>$.data.accessToken</c>).</summary>
    public string? TokenPath { get; init; }

    /// <summary>LoginToken: cuánto dura el token si la respuesta no lo dice.</summary>
    public int? ExpiresInSeconds { get; init; }

    /// <summary>LoginToken y HeaderKey: la cabecera donde va el token o la llave (por defecto <c>Authorization</c>).</summary>
    public string? Header { get; init; }

    /// <summary>LoginToken y HeaderKey: lo que va antes del token (<c>Bearer </c>).</summary>
    public string? Prefix { get; init; }

    /// <summary>HeaderKey: la plantilla del valor (<c>{{cred.apiKey}}</c>). Basic: usa <c>cred.username</c> y <c>cred.password</c>.</summary>
    public string? Value { get; init; }
}

/// <summary>La operación que hace el envío.</summary>
public sealed record OperacionDeConector(string Method, string Path, string? Body);

/// <summary>Un campo de credencial que pide el conector; los secretos nunca se vuelven a mostrar.</summary>
public sealed record CampoDeCredencial(string Name, string Label, bool Secret);

/// <summary>
/// La definición de un conector HTTP/JSON (feature 020, R15; FR-035 a FR-040; contracts/api.md §1): autenticación, operación, campos de
/// credencial, mapa de respuestas, intentos y tiempo de espera. Es un dato que define el maestro: se valida al publicar una versión y la
/// versión no cambia. No lleva credenciales.
/// </summary>
public sealed record DefinicionDeConector
{
    public const int IntentosPorDefecto = 12;
    public const int IntentosMaximos = 50;
    public const int EsperaPorDefecto = 30;
    public const int EsperaMaxima = 60;

    public AutenticacionDeConector Auth { get; init; } = new();
    public OperacionDeConector Operation { get; init; } = new("POST", "/", null);
    public IReadOnlyList<CampoDeCredencial> CredentialFields { get; init; } = [];
    public IReadOnlyList<ReglaDeRespuesta> Responses { get; init; } = [];
    public int MaxAttempts { get; init; } = IntentosPorDefecto;
    public int TimeoutSeconds { get; init; } = EsperaPorDefecto;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly string[] Metodos = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static DefinicionDeConector? Leer(string? json, out ProblemaDeConector? problema)
    {
        problema = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            problema = new("Connector.DefinitionInvalid", "Falta la definición del conector.");
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<DefinicionDeConector>(json, Json);
        }
        catch (JsonException ex)
        {
            problema = new("Connector.DefinitionInvalid", $"La definición no es un JSON válido: {ex.Message}");
            return null;
        }
    }

    public string Escribir() => JsonSerializer.Serialize(this, Json);

    public IReadOnlySet<string> NombresDeCredencial => CredentialFields.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Todo lo que impide publicar la definición (FR-035 a FR-038).</summary>
    public IReadOnlyList<ProblemaDeConector> Validar()
    {
        var p = new List<ProblemaDeConector>();
        if (MaxAttempts is < 1 or > IntentosMaximos) p.Add(new("Connector.DefinitionInvalid", $"Los intentos van de 1 a {IntentosMaximos}."));
        if (TimeoutSeconds is < 1 or > EsperaMaxima) p.Add(new("Connector.DefinitionInvalid", $"El tiempo de espera va de 1 a {EsperaMaxima} segundos."));
        foreach (var c in CredentialFields)
            if (string.IsNullOrWhiteSpace(c.Name) || !c.Name.All(char.IsAsciiLetterOrDigit))
                p.Add(new("Connector.DefinitionInvalid", $"«{c.Name}» no es un nombre de campo de credencial: sólo letras y números."));
        if (CredentialFields.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != CredentialFields.Count)
            p.Add(new("Connector.DefinitionInvalid", "Un campo de credencial está repetido."));

        if (!Metodos.Contains(Operation.Method?.ToUpperInvariant())) p.Add(new("Connector.DefinitionInvalid", "El método de la operación es GET, POST, PUT, PATCH o DELETE."));
        if (string.IsNullOrWhiteSpace(Operation.Path) || !Operation.Path.StartsWith('/')) p.Add(new("Connector.DefinitionInvalid", "La ruta de la operación empieza con «/»."));
        var credenciales = NombresDeCredencial;
        p.AddRange(RenderizadorDePlantilla.Validar(Operation.Path, false, credenciales, "la ruta de la operación"));
        p.AddRange(RenderizadorDePlantilla.Validar(Operation.Body, false, credenciales, "el cuerpo de la operación"));

        switch (Auth.Kind)
        {
            case ConnectorAuthKind.LoginToken:
                if (!Metodos.Contains(Auth.Method?.ToUpperInvariant())) p.Add(new("Connector.DefinitionInvalid", "El método del inicio de sesión es GET, POST, PUT o PATCH."));
                if (string.IsNullOrWhiteSpace(Auth.Path) || !Auth.Path.StartsWith('/')) p.Add(new("Connector.DefinitionInvalid", "La ruta del inicio de sesión empieza con «/»."));
                if (string.IsNullOrWhiteSpace(Auth.TokenPath) || !Auth.TokenPath.StartsWith("$.", StringComparison.Ordinal))
                    p.Add(new("Connector.DefinitionInvalid", "Diga dónde está el token en la respuesta: $.campo o $.objeto.campo."));
                if (Auth.ExpiresInSeconds is <= 0) p.Add(new("Connector.DefinitionInvalid", "La vigencia del token es mayor que cero."));
                p.AddRange(RenderizadorDePlantilla.Validar(Auth.Body, true, credenciales, "el inicio de sesión"));
                break;
            case ConnectorAuthKind.HeaderKey:
                if (string.IsNullOrWhiteSpace(Auth.Header)) p.Add(new("Connector.DefinitionInvalid", "Diga la cabecera de la llave."));
                if (string.IsNullOrWhiteSpace(Auth.Value)) p.Add(new("Connector.DefinitionInvalid", "Diga el valor de la llave, normalmente {{cred.apiKey}}."));
                p.AddRange(RenderizadorDePlantilla.Validar(Auth.Value, true, credenciales, "la llave"));
                break;
            case ConnectorAuthKind.Basic:
                if (!credenciales.Contains("username") || !credenciales.Contains("password"))
                    p.Add(new("Connector.DefinitionInvalid", "La autenticación básica pide los campos de credencial «username» y «password»."));
                break;
        }

        if (Responses.Count == 0) p.Add(new("Connector.DefinitionInvalid", "El mapa de respuestas está vacío: diga al menos qué respuesta es «hecho»."));
        if (Responses.Any(r => r.Status is < 0 or > 599)) p.Add(new("Connector.DefinitionInvalid", "Un código de respuesta va de 0 (error de red) a 599."));
        if (!Responses.Any(r => r.Outcome is ConnectorOutcome.Done or ConnectorOutcome.DoneWithWarning))
            p.Add(new("Connector.DefinitionInvalid", "El mapa de respuestas no tiene ninguna respuesta «hecho»."));
        return p;
    }

    /// <summary>Lee el token de la respuesta del inicio de sesión por su ruta (<c>$.a.b</c>).</summary>
    public static string? LeerToken(string? cuerpo, string? ruta)
    {
        if (string.IsNullOrWhiteSpace(cuerpo) || string.IsNullOrWhiteSpace(ruta) || !ruta.StartsWith("$.", StringComparison.Ordinal)) return null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            var actual = doc.RootElement;
            foreach (var parte in ruta[2..].Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(parte, out actual)) return null;
            }
            return actual.ValueKind == JsonValueKind.String ? actual.GetString() : actual.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// La defensa contra SSRF de los conectores (feature 020, R17; FR-038). Al guardar, la URL base es <c>https</c>, sin credenciales en la URL, con
/// un host público; al conectar, el adaptador vuelve a mirar cada IP resuelta con <see cref="EsIpPermitida"/> (cierra el DNS rebinding). Se
/// rechazan loopback, redes privadas (RFC 1918), enlace local, 100.64/10 (CGNAT, la malla de los clústeres), ULA de IPv6, metadatos
/// (169.254.169.254) y las IPv4 escritas como IPv6.
/// </summary>
public static class DireccionSegura
{
    public static ProblemaDeConector? ValidarUrlBase(string? url, bool permitirRedesPrivadas = false)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return new("Connector.InsecureUrl", "La dirección base no es una URL válida.");
        if (uri.Scheme != Uri.UriSchemeHttps) return new("Connector.InsecureUrl", "La dirección base usa https: sin cifrado no se envía nada.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) return new("Connector.InsecureUrl", "La dirección base no lleva usuario ni clave: van en las credenciales.");
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return new("Connector.InsecureUrl", "La dirección base no lleva consulta ni fragmento.");
        if (permitirRedesPrivadas) return null;
        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".svc", StringComparison.OrdinalIgnoreCase) || host.Contains(".svc.cluster", StringComparison.OrdinalIgnoreCase))
            return new("Connector.PrivateAddress", $"«{host}» es un nombre de una red interna.");
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && !EsIpPermitida(ip))
            return new("Connector.PrivateAddress", $"«{host}» es una dirección de una red interna o reservada.");
        return null;
    }

    public static bool EsIpPermitida(IPAddress ip)
    {
        ArgumentNullException.ThrowIfNull(ip);
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None)) return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return !(b[0] == 10
                     || b[0] == 0
                     || b[0] == 127
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                     || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                     || b[0] >= 224);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return false;
            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;
            return true;
        }
        return false;
    }
}
