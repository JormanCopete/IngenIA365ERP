namespace IngenIA365ERP.API.Security.CanalServicio;

/// <summary>
/// Feature 021 (research R2; constitución v2.1.0, «Sistemas satélite», regla 2): lo común de las peticiones que llegan del
/// canal digital por la API de servicio <c>/api/canal/v1</c>. Sólo se consideran del canal las autenticadas con el esquema
/// <see cref="Esquema"/>; el prefijo de la ruta solo no basta.
/// </summary>
public static class PeticionDelCanal
{
    /// <summary>El esquema de autenticación del token de servicio del canal (JWT ES256).</summary>
    public const string Esquema = "CanalServicio";

    /// <summary>La política de autorización de las rutas de servicio.</summary>
    public const string Politica = "CanalServicio";

    /// <summary>El prefijo de las rutas de servicio, en minúsculas.</summary>
    public const string Prefijo = "/api/canal/v1";

    /// <summary>Emisor y audiencia del token que firma el canal.</summary>
    public const string EmisorDelCanal = "ingenia365-canal";

    public const string AudienciaDelErp = "ingenia365-erp";

    /// <summary>Emisor y audiencia del token que firma el ERP hacia el canal.</summary>
    public const string EmisorDelErp = "ingenia365-erp";

    public const string AudienciaDelCanal = "ingenia365-canal";

    /// <summary>El <c>sub</c> de las rutas de plataforma, que no son de una cooperativa.</summary>
    public const string SujetoDePlataforma = "platform";

    /// <summary>La vida máxima del token de servicio.</summary>
    public static readonly TimeSpan VidaMaxima = TimeSpan.FromSeconds(60);

    /// <summary>Cabeceras con el contexto del asociado que reenvía el canal (para la auditoría, Principio X).</summary>
    public const string CabeceraIp = "X-Canal-Ip";

    public const string CabeceraAgente = "X-Canal-UserAgent";

    public const string CabeceraCuenta = "X-Canal-Account";

    /// <summary>El nombre del parámetro de ruta con la persona.</summary>
    public const string ParametroPersona = "personPublicId";

    /// <summary>El nombre del parámetro de ruta con la cooperativa.</summary>
    public const string ParametroCooperativa = "tenantPublicId";

    /// <summary>¿La ruta es de la API de servicio del canal?</summary>
    public static bool EsRutaDelCanal(string? ruta) =>
        ruta is not null && ruta.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿La petición viene del canal, autenticada con su esquema?</summary>
    public static bool EsPeticionDelCanal(HttpContext? http) =>
        http?.User?.Identity is { IsAuthenticated: true, AuthenticationType: Esquema }
        && EsRutaDelCanal(http.Request.Path.Value);

    /// <summary>La cooperativa de la ruta <c>/api/canal/v1/tenants/{guid}/…</c>.</summary>
    public static bool IntentarCooperativaDeLaRuta(string? ruta, out Guid cooperativa)
    {
        cooperativa = Guid.Empty;
        if (ruta is null) return false;
        var prefijo = Prefijo + "/tenants/";
        if (!ruta.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) return false;
        var resto = ruta[prefijo.Length..];
        var barra = resto.IndexOf('/');
        return Guid.TryParse(barra < 0 ? resto : resto[..barra], out cooperativa);
    }

    /// <summary>La persona de la ruta, si la ruta la lleva.</summary>
    public static Guid? PersonaDeLaRuta(HttpContext http) =>
        http.Request.RouteValues.TryGetValue(ParametroPersona, out var valor) && Guid.TryParse(valor?.ToString(), out var persona)
            ? persona
            : null;

    /// <summary>Un valor de cabecera recortado, o nulo.</summary>
    public static string? Cabecera(HttpContext http, string nombre, int largoMaximo)
    {
        var valor = http.Request.Headers[nombre].ToString().Trim();
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Length > largoMaximo ? valor[..largoMaximo] : valor;
    }
}
