using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Http;

/// <summary>
/// Guarda en memoria, unos minutos, las respuestas de los catálogos que casi no cambian (bancos, sucursales,
/// centros de costo, ciudades…): hasta el 2026-11-24 cada pantalla los volvía a pedir —bancos y sucursales desde
/// 14 pantallas, de a 500; ciudades de a 2.000— y abrir una pantalla esperaba esas llamadas aunque los datos
/// fueran los mismos de la anterior. (nuevo)
///
/// <para>
/// Va <b>último</b> en la cadena del cliente <c>api</c>, para ver la petición con la cabecera <c>Authorization</c>
/// ya puesta: la clave lleva la persona (<c>sub</c>) y la cooperativa activa (<c>active_tenant_id</c>) del token,
/// así un cambio de cooperativa o de usuario no ve lo del anterior. <b>Sólo se registra en el navegador
/// (Web.Client) y en la app (MAUI)</b>, donde la memoria es de una persona; nunca en el servidor Web, cuyo
/// pre-renderizado comparte el contenedor entre usuarios. Lo guardado vive en <see cref="CacheDeCatalogos"/>
/// (singleton): el handler lo recrea la fábrica de HttpClient cada tanto.
/// </para>
/// <para>
/// Cualquier escritura (POST, PUT, PATCH, DELETE) bajo el mismo prefijo descarta lo guardado de ese catálogo, y
/// lo guardado vence solo a los <see cref="CacheDeCatalogos.Vigencia"/>: lo que cambie otra persona u otra
/// pestaña se ve, a más tardar, a los cinco minutos. Sólo se guardan respuestas 200. Dos pantallas que piden lo
/// mismo a la vez comparten una sola llamada.
/// </para>
/// </summary>
public sealed class CatalogosEnCacheHandler(CacheDeCatalogos cache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var prefijo = CacheDeCatalogos.PrefijoDe(request.RequestUri);
        if (prefijo is null) return await base.SendAsync(request, cancellationToken);

        var quien = CacheDeCatalogos.QuienPide(request);
        if (request.Method != HttpMethod.Get)
        {
            var escrita = await base.SendAsync(request, cancellationToken);
            if (quien is not null) cache.Olvidar(quien, prefijo);
            return escrita;
        }
        if (quien is null) return await base.SendAsync(request, cancellationToken);

        var clave = $"{quien}|{prefijo}|{request.RequestUri!.PathAndQuery}";
        if (cache.Buscar(clave) is { } guardada) return guardada.Crear(request);

        // La llamada compartida no lleva el token de cancelación de quien la abrió: si esa pantalla se cierra, la
        // otra que espera lo mismo no tiene por qué perder su respuesta.
        var propio = new Lazy<Task<CacheDeCatalogos.Pedido>>(() => PedirAsync(request, clave));
        var (vuelo, esPropio) = cache.Unirse(clave, propio);
        CacheDeCatalogos.Pedido pedido;
        try
        {
            pedido = await vuelo.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (esPropio) cache.TerminarVuelo(clave, vuelo);
        }
        if (pedido.Guardada is not null) return pedido.Guardada.Crear(request);
        // No fue 200: quien abrió la llamada recibe la respuesta tal cual; quien sólo esperaba pide la suya (un
        // mensaje HTTP no se puede enviar dos veces).
        return esPropio ? pedido.Cruda! : await base.SendAsync(request, cancellationToken);
    }

    private async Task<CacheDeCatalogos.Pedido> PedirAsync(HttpRequestMessage request, string clave)
    {
        var respuesta = await base.SendAsync(request, CancellationToken.None);
        if (respuesta.StatusCode != HttpStatusCode.OK) return new(null, respuesta);
        using (respuesta)
        {
            var leida = await CacheDeCatalogos.Respuesta.LeerAsync(respuesta);
            cache.Guardar(clave, leida);
            return new(leida, null);
        }
    }
}

/// <summary>Lo que guarda <see cref="CatalogosEnCacheHandler"/>; singleton en el navegador y en la app. (nuevo)</summary>
public sealed class CacheDeCatalogos
{
    /// <summary>Cuánto vive una respuesta guardada.</summary>
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Los catálogos que se guardan: listas de datos maestros que cambian poco. Prefijos completos de ruta
    /// (<c>/api/core/banks</c> cubre <c>/api/core/banks?…</c> y <c>/api/core/banks/{id}</c>, no <c>/api/core/banksx</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> Prefijos =
    [
        "/api/core/banks",
        "/api/core/branches",
        "/api/core/cost-centers",
        "/api/core/cities",
        "/api/core/departments",
        "/api/core/countries",
        "/api/core/committees",
        "/api/core/positions",
        "/api/core/companies",
        "/api/admin/branches",
        "/api/payroll/work-risk",
        "/api/payroll/pension-providers",
        "/api/payroll/health-insurance",
        "/api/payroll/severance-providers",
        "/api/payroll/family-compensation-funds",
    ];

    private readonly ConcurrentDictionary<string, (Respuesta Respuesta, DateTimeOffset VenceEn)> _guardado = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<Pedido>>> _enVuelo = new();
    private readonly Func<DateTimeOffset> _ahora;

    public CacheDeCatalogos() : this(() => DateTimeOffset.UtcNow) { }

    /// <summary>Para las pruebas: el reloj.</summary>
    public CacheDeCatalogos(Func<DateTimeOffset> ahora) => _ahora = ahora;

    /// <summary>Descarta todo lo guardado.</summary>
    public void OlvidarTodo() => _guardado.Clear();

    internal Respuesta? Buscar(string clave) =>
        _guardado.TryGetValue(clave, out var e) && e.VenceEn > _ahora() ? e.Respuesta : null;

    internal void Guardar(string clave, Respuesta respuesta) => _guardado[clave] = (respuesta, _ahora() + Vigencia);

    internal void Olvidar(string quien, string prefijo)
    {
        var comienzo = $"{quien}|{prefijo}|";
        foreach (var clave in _guardado.Keys)
        {
            if (clave.StartsWith(comienzo, StringComparison.Ordinal)) _guardado.TryRemove(clave, out _);
        }
    }

    internal (Lazy<Task<Pedido>> Vuelo, bool EsPropio) Unirse(string clave, Lazy<Task<Pedido>> propio)
    {
        var vuelo = _enVuelo.GetOrAdd(clave, propio);
        return (vuelo, ReferenceEquals(vuelo, propio));
    }

    internal void TerminarVuelo(string clave, Lazy<Task<Pedido>> vuelo) =>
        _enVuelo.TryRemove(new KeyValuePair<string, Lazy<Task<Pedido>>>(clave, vuelo));

    internal static string? PrefijoDe(Uri? uri)
    {
        if (uri is null) return null;
        var ruta = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString.Split('?', 2)[0];
        foreach (var prefijo in Prefijos)
        {
            if (ruta.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)
                && (ruta.Length == prefijo.Length || ruta[prefijo.Length] == '/'))
                return prefijo;
        }
        return null;
    }

    /// <summary>
    /// La persona y la cooperativa del token de la petición (<c>sub</c> y <c>active_tenant_id</c>), sin validarlo:
    /// sólo separa lo guardado; quien valida es la API. Sin token o sin cooperativa no se guarda nada.
    /// </summary>
    internal static string? QuienPide(HttpRequestMessage request)
    {
        var token = request.Headers.Authorization?.Parameter;
        if (string.IsNullOrEmpty(token)) return null;
        var partes = token.Split('.');
        if (partes.Length < 2) return null;
        try
        {
            var cuerpo = partes[1].Replace('-', '+').Replace('_', '/');
            cuerpo = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cuerpo)));
            var raiz = json.RootElement;
            var sub = raiz.TryGetProperty("sub", out var s) ? s.ToString() : null;
            var cooperativa = raiz.TryGetProperty("active_tenant_id", out var t) ? t.ToString() : null;
            return string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(cooperativa) ? null : $"{sub}|{cooperativa}";
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    internal sealed record Pedido(Respuesta? Guardada, HttpResponseMessage? Cruda);

    /// <summary>Una respuesta guardada: cuerpo y cabeceras de contenido; cada uso arma un mensaje nuevo.</summary>
    internal sealed record Respuesta(byte[] Cuerpo, IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> Cabeceras)
    {
        public static async Task<Respuesta> LeerAsync(HttpResponseMessage respuesta) =>
            new(await respuesta.Content.ReadAsByteArrayAsync(), respuesta.Content.Headers.ToList());

        public HttpResponseMessage Crear(HttpRequestMessage request)
        {
            var contenido = new ByteArrayContent(Cuerpo);
            foreach (var (nombre, valores) in Cabeceras)
            {
                if (!string.Equals(nombre, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    contenido.Headers.TryAddWithoutValidation(nombre, valores);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = contenido, RequestMessage = request };
        }
    }
}
