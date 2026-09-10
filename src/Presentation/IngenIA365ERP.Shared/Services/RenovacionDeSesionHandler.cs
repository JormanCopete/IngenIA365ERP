using System.Net;
using System.Net.Http.Headers;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Primer handler de la cadena del cliente <c>api</c>: renueva el access token
/// antes de que venza y, si un 401 se cuela igual, renueva y reintenta la
/// petición una vez.
///
/// <para>
/// Va <b>antes</b> de <see cref="AuthBearerHandler"/> y deja la cabecera puesta,
/// así aquél no la toca. Se aparta en dos casos: la petición trae ya su propia
/// <c>Authorization</c> —son los desafíos de login, MFA y selección de cooperativa,
/// que viajan con un token temporal que no se renueva ni se reintenta— y la que
/// lleva la marca <see cref="RenovadorDeSesion.SinSesion"/>, que es el propio canje
/// del refresh.
/// </para>
///
/// <para>
/// El reintento necesita reenviar el cuerpo, y un cuerpo que ya se leyó no vuelve.
/// Por eso se lee a memoria antes de enviar, con un tope: por encima de
/// <see cref="MaximoReintentable"/> (importaciones y adjuntos grandes) la petición
/// sale sin red de reintento, y la renovación proactiva de arriba es la que la
/// protege.
/// </para>
/// </summary>
public sealed class RenovacionDeSesionHandler(RenovadorDeSesion sesion) : DelegatingHandler
{
    public const long MaximoReintentable = 8 * 1024 * 1024;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(RenovadorDeSesion.SinSesion, out var sinSesion) && sinSesion)
            return await base.SendAsync(request, cancellationToken);

        if (request.Headers.Authorization is not null)
            return await base.SendAsync(request, cancellationToken);

        var token = await sesion.TokenVigenteAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
            return await base.SendAsync(request, cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        sesion.RegistrarActividad();

        var cuerpo = await CapturarCuerpoAsync(request, cancellationToken);
        var respuesta = await base.SendAsync(request, cancellationToken);

        if (respuesta.StatusCode != HttpStatusCode.Unauthorized || !cuerpo.Reintentable)
            return respuesta;

        // El servidor no aceptó el token que teníamos por vigente: reloj desfasado,
        // o revocación. Se canjea una vez; si el servidor dice que la sesión ya no
        // vale, RenovadorDeSesion la termina y avisa, y esta 401 vuelve tal cual.
        if (!await sesion.RenovarAsync(RenovadorDeSesion.MotivoDeRenovacion.Rechazado, token, cancellationToken)) return respuesta;

        var nuevo = sesion.AccessToken;
        if (string.IsNullOrEmpty(nuevo) || nuevo == token) return respuesta;

        respuesta.Dispose();
        using var reintento = Clonar(request, nuevo, cuerpo);
        return await base.SendAsync(reintento, cancellationToken);
    }

    private static async Task<CuerpoCapturado> CapturarCuerpoAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null) return CuerpoCapturado.Vacio;

        var largo = request.Content.Headers.ContentLength;
        if (largo is > MaximoReintentable) return CuerpoCapturado.NoReintentable;

        await request.Content.LoadIntoBufferAsync(MaximoReintentable, ct);
        var bytes = await request.Content.ReadAsByteArrayAsync(ct);
        return bytes.LongLength > MaximoReintentable
            ? CuerpoCapturado.NoReintentable
            : new CuerpoCapturado(true, bytes, request.Content.Headers);
    }

    private static HttpRequestMessage Clonar(HttpRequestMessage original, string token, CuerpoCapturado cuerpo)
    {
        var clon = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };

        foreach (var cabecera in original.Headers)
            clon.Headers.TryAddWithoutValidation(cabecera.Key, cabecera.Value);
        clon.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        foreach (var opcion in original.Options)
            clon.Options.Set(new HttpRequestOptionsKey<object?>(opcion.Key), opcion.Value);

        if (cuerpo.Bytes is { } bytes)
        {
            var contenido = new ByteArrayContent(bytes);
            if (cuerpo.Cabeceras is not null)
                foreach (var cabecera in cuerpo.Cabeceras)
                    contenido.Headers.TryAddWithoutValidation(cabecera.Key, cabecera.Value);
            clon.Content = contenido;
        }

        return clon;
    }

    private sealed record CuerpoCapturado(bool Reintentable, byte[]? Bytes, HttpContentHeaders? Cabeceras)
    {
        public static readonly CuerpoCapturado Vacio = new(true, null, null);
        public static readonly CuerpoCapturado NoReintentable = new(false, null, null);
    }
}
