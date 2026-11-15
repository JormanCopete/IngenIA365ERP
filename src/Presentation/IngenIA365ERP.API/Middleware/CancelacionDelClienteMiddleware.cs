namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// Una petición que el cliente abandona —el buscador que cancela su consulta en cada tecla, la pestaña que se cierra— no es un
/// error del servidor. Hasta el 2026-11-12 la <see cref="OperationCanceledException"/> de EF que eso provoca subía hasta el
/// manejador de excepciones: el registro decía <c>ERR Unhandled exception in SearchProductsQuery</c> y <c>responded 500</c>
/// (46 veces al día en producción, sólo en la búsqueda de productos) y escondía los errores de verdad.
///
/// <para>
/// Va justo después de <c>UseSerilogRequestLogging</c>, así el registro de la petición la ve terminar con 499 (Information) y
/// no con 500. Sólo atrapa la cancelación cuando <see cref="HttpContext.RequestAborted"/> está cancelado: una cancelación del
/// propio servidor (un tiempo de espera interno) con el cliente todavía esperando sigue siendo un error y sale como siempre.
/// No escribe cuerpo: nadie lo va a leer.
/// </para>
/// </summary>
public sealed class CancelacionDelClienteMiddleware(RequestDelegate next, ILogger<CancelacionDelClienteMiddleware> logger)
{
    /// <summary>El estado que usa nginx para «el cliente cerró la petición»; no hay uno estándar.</summary>
    public const int EstadoCanceladaPorElCliente = 499;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("{Metodo} {Ruta} cancelada por el cliente.", context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted) context.Response.StatusCode = EstadoCanceladaPorElCliente;
        }
    }

    /// <summary>¿Es esta falla la cancelación de una petición que el cliente abandonó?</summary>
    public static bool EsDelCliente(Exception? fallo, HttpContext context) =>
        fallo is OperationCanceledException && context.RequestAborted.IsCancellationRequested;
}

public static class CancelacionDelClienteMiddlewareExtensions
{
    public static IApplicationBuilder UseCancelacionDelCliente(this IApplicationBuilder app) =>
        app.UseMiddleware<CancelacionDelClienteMiddleware>();
}
