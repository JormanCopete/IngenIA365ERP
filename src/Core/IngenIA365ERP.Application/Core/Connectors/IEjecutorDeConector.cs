namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>
/// Una petición a un sistema externo, ya armada (feature 020, R16, T140): método, URL completa, cabeceras —incluida la de autenticación— y
/// cuerpo. <see cref="ClaveDeCircuito"/> agrupa las fallas seguidas (por cooperativa y conector; la cooperativa la pone el adaptador).
/// </summary>
public sealed record SolicitudHttp(
    string Metodo,
    Uri Url,
    IReadOnlyDictionary<string, string> Cabeceras,
    string? Cuerpo,
    int SegundosDeEspera,
    string ClaveDeCircuito);

/// <summary>
/// Lo que respondió el sistema externo. <see cref="Codigo"/> es 0 si falló la red, venció el tiempo o el circuito está abierto (el mapa de
/// respuestas lo trata como <c>status: 0</c>). <see cref="DireccionBloqueada"/> dice que la dirección resolvió a una red interna (R17): no se
/// reintenta, se revisa. <see cref="Cuerpo"/> llega completo hasta 64 KB y vive sólo en memoria: el del inicio de sesión trae el token.
/// </summary>
public sealed record RespuestaHttp(int Codigo, string? Cuerpo, int DuracionMs, string? Error = null, bool DireccionBloqueada = false)
{
    /// <summary>Lo que se guarda de una respuesta: los primeros 2 KB (FR-049).</summary>
    public const int LargoDelExtracto = 2048;

    public string? Extracto() =>
        Cuerpo is null ? Error : Cuerpo.Length <= LargoDelExtracto ? Cuerpo : Cuerpo[..LargoDelExtracto];

    public bool EsExito => Codigo is >= 200 and < 300;
}

/// <summary>
/// El puerto de la única salida HTTP de los conectores (feature 020, R16, R17; T140). Lo implementa sólo el adaptador de
/// <c>ExternalChannels</c> (<c>SoloElAdaptadorDeConectoresHablaHttp</c>): sin redirecciones, sin reintentos propios, con la guarda de
/// direcciones en cada conexión y el circuito por cooperativa y conector. <b>Nunca lanza</b> por la red: devuelve código 0.
/// </summary>
public interface IEjecutorDeConector
{
    Task<RespuestaHttp> EjecutarAsync(SolicitudHttp solicitud, CancellationToken ct);
}

/// <summary>
/// El defecto mientras no se compone el adaptador (el migrador, las pruebas sin API): no sale a la red y responde código 0. La API lo
/// reemplaza con el adaptador de <c>ExternalChannels</c>.
/// </summary>
public sealed class SinSalidaHttpDeConector : IEjecutorDeConector
{
    public Task<RespuestaHttp> EjecutarAsync(SolicitudHttp solicitud, CancellationToken ct) =>
        Task.FromResult(new RespuestaHttp(0, null, 0, "Este proceso no tiene salida a sistemas externos."));
}
