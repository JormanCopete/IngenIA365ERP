using System.Text;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;
using Microsoft.Extensions.Caching.Memory;

namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>Cómo quedó la autenticación ante el sistema externo: las cabeceras para la operación o por qué no se pudo.</summary>
public sealed record AutenticacionDeConectorResultado(bool Ok, IReadOnlyDictionary<string, string> Cabeceras, int Codigo, string Mensaje,
    RespuestaHttp? Login = null)
{
    public static AutenticacionDeConectorResultado Fallo(int codigo, string mensaje, RespuestaHttp? login = null) =>
        new(false, new Dictionary<string, string>(), codigo, mensaje, login);
}

/// <summary>
/// La autenticación de un conector ante su sistema externo (feature 020, R16; T145). Arma la cabecera según el tipo:
/// <list type="bullet">
/// <item><c>LoginToken</c>: hace el inicio de sesión, lee el token por <c>tokenPath</c> y lo guarda en memoria por cooperativa, conector y
/// versión hasta su vigencia menos 60 s (a lo sumo 24 h). <b>Nunca lo persiste</b>; ante un 401 se invalida y se vuelve a pedir;</item>
/// <item><c>HeaderKey</c>: la llave fija renderizada con <c>{{cred.*}}</c>;</item>
/// <item><c>Basic</c>: usuario y clave en base64 (RFC 7617).</item>
/// </list>
/// Las credenciales salen de <see cref="AlmacenDeCredencialesDeConector"/>; ni ellas ni el token salen de aquí hacia un registro.
/// </summary>
public sealed class SesionDeConector(IEjecutorDeConector ejecutor, AlmacenDeCredencialesDeConector almacen, IMemoryCache cache, ICurrentTenantService cooperativa)
{
    private static readonly TimeSpan VigenciaMaxima = TimeSpan.FromHours(24);
    private static readonly TimeSpan Margen = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VigenciaMinima = TimeSpan.FromSeconds(30);

    public async Task<AutenticacionDeConectorResultado> AutenticarAsync(int conectorId, int version, string urlBase, DefinicionDeConector d, bool forzar,
        CancellationToken ct)
    {
        if (d.Auth.Kind == ConnectorAuthKind.None) return new(true, new Dictionary<string, string>(), 0, "El conector no usa autenticación.");

        var credenciales = await almacen.LeerAsync(conectorId, ct);
        if (credenciales is null) return AutenticacionDeConectorResultado.Fallo(0, "El conector no tiene credenciales: el administrador de la cooperativa debe ponerlas.");
        var valores = credenciales.ToDictionary(c => VariablesDeConector.PrefijoDeCredencial + c.Key, c => (object?)c.Value, StringComparer.Ordinal);
        var cabecera = string.IsNullOrWhiteSpace(d.Auth.Header) ? "Authorization" : d.Auth.Header!;

        switch (d.Auth.Kind)
        {
            case ConnectorAuthKind.HeaderKey:
            {
                var (llave, faltan) = RenderizadorDePlantilla.Renderizar(d.Auth.Value, valores);
                return llave is null
                    ? AutenticacionDeConectorResultado.Fallo(0, $"Faltan credenciales: {string.Join(", ", faltan)}.")
                    : new(true, new Dictionary<string, string> { [cabecera] = (d.Auth.Prefix ?? string.Empty) + llave }, 0, "Llave lista.");
            }
            case ConnectorAuthKind.Basic:
            {
                credenciales.TryGetValue("username", out var usuario);
                credenciales.TryGetValue("password", out var clave);
                var basica = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{clave}"));
                return new(true, new Dictionary<string, string> { ["Authorization"] = "Basic " + basica }, 0, "Credenciales básicas listas.");
            }
            case ConnectorAuthKind.LoginToken:
                return await PorInicioDeSesionAsync(conectorId, version, urlBase, d, valores, cabecera, forzar, ct);
            default:
                return AutenticacionDeConectorResultado.Fallo(0, "Tipo de autenticación desconocido.");
        }
    }

    /// <summary>Olvida el token guardado (un 401 de la operación).</summary>
    public void Invalidar(int conectorId, int version) => cache.Remove(Clave(conectorId, version));

    private async Task<AutenticacionDeConectorResultado> PorInicioDeSesionAsync(int conectorId, int version, string urlBase, DefinicionDeConector d,
        Dictionary<string, object?> valores, string cabecera, bool forzar, CancellationToken ct)
    {
        var clave = Clave(conectorId, version);
        if (!forzar && cache.TryGetValue(clave, out string? guardado) && !string.IsNullOrEmpty(guardado))
            return new(true, new Dictionary<string, string> { [cabecera] = (d.Auth.Prefix ?? string.Empty) + guardado }, 0, "Sesión vigente.");

        var (cuerpo, faltan) = RenderizadorDePlantilla.Renderizar(d.Auth.Body, valores);
        if (cuerpo is null) return AutenticacionDeConectorResultado.Fallo(0, $"Faltan credenciales: {string.Join(", ", faltan)}.");
        var solicitud = new SolicitudHttp((d.Auth.Method ?? "POST").ToUpperInvariant(), Unir(urlBase, d.Auth.Path!),
            new Dictionary<string, string>(), string.IsNullOrEmpty(cuerpo) ? null : cuerpo, d.TimeoutSeconds, $"conector:{conectorId}");
        var respuesta = await ejecutor.EjecutarAsync(solicitud, ct);
        if (!respuesta.EsExito)
        {
            var motivo = respuesta.Codigo == 0 ? respuesta.Error ?? "sin respuesta" : $"respondió {respuesta.Codigo}";
            return AutenticacionDeConectorResultado.Fallo(respuesta.Codigo, $"El inicio de sesión no funcionó: {motivo}.", respuesta);
        }
        var token = DefinicionDeConector.LeerToken(respuesta.Cuerpo, d.Auth.TokenPath);
        if (string.IsNullOrEmpty(token))
            return AutenticacionDeConectorResultado.Fallo(respuesta.Codigo, $"El inicio de sesión respondió {respuesta.Codigo} pero sin token en {d.Auth.TokenPath}.", respuesta);

        var vigencia = d.Auth.ExpiresInSeconds is { } s ? TimeSpan.FromSeconds(s) : VigenciaMaxima;
        if (vigencia > VigenciaMaxima) vigencia = VigenciaMaxima;
        vigencia -= Margen;
        if (vigencia < VigenciaMinima) vigencia = VigenciaMinima;
        cache.Set(clave, token, vigencia);
        return new(true, new Dictionary<string, string> { [cabecera] = (d.Auth.Prefix ?? string.Empty) + token }, respuesta.Codigo, "Sesión iniciada.", respuesta);
    }

    /// <summary>La URL base más la ruta, sin barras dobles.</summary>
    public static Uri Unir(string urlBase, string ruta) => new($"{urlBase.TrimEnd('/')}/{ruta.TrimStart('/')}", UriKind.Absolute);

    private string Clave(int conectorId, int version) => $"conector-token:{cooperativa.TenantId}:{conectorId}:{version}";
}
