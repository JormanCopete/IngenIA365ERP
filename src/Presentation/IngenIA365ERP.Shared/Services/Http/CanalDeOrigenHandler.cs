namespace IngenIA365ERP.Shared.Services.Http;

/// <summary>
/// Dice a la API por qué canal entra cada petición del cliente <c>api</c>, con la cabecera
/// <c>X-Canal</c> (feature 012, T36, T043): <c>web</c> desde Web y Web.Client, <c>app</c> desde la
/// aplicación MAUI. La auditoría lo registra (<c>IOrigenDeLaPeticion</c>); no concede nada.
///
/// <para>
/// Va <b>después</b> de <c>RenovacionDeSesionHandler</c> en la cadena y <b>no toca</b>
/// <c>Authorization</c>: la cabecera de sesión la pone sólo aquél (<c>ElTokenDeSesionLoPoneElHandler</c>).
/// Un valor que una pantalla haya puesto a mano se reemplaza: el canal lo decide el anfitrión.
/// </para>
/// </summary>
public sealed class CanalDeOrigenHandler : DelegatingHandler
{
    public const string Cabecera = "X-Canal";
    public const string Web = "web";
    public const string App = "app";

    private readonly string _canal;

    /// <param name="canal"><see cref="Web"/> o <see cref="App"/>.</param>
    public CanalDeOrigenHandler(string canal)
    {
        if (canal is not (Web or App))
        {
            throw new ArgumentException($"El canal de un anfitrión es «{Web}» o «{App}»; llegó «{canal}».", nameof(canal));
        }

        _canal = canal;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(Cabecera);
        request.Headers.TryAddWithoutValidation(Cabecera, _canal);
        return base.SendAsync(request, cancellationToken);
    }
}
