using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Cuántas cargas masivas (importaciones por plantilla, actualizaciones y resultados en bloque) atiende a la vez cada réplica.
/// Una carga de 12.000 filas asigna entre 1 y 5 GB en diez segundos (medido el 2026-11-06, <c>MemoriaDeImportacionTests</c>), y esa
/// memoria la pagan los usuarios del mismo pod: el 2026-11-06 una carga de personas tumbó las dos réplicas de producción.
///
/// <para>
/// Convive con <c>LimiteDeAdjuntos</c> y con <c>AspNetCoreRateLimit</c>, que limita peticiones por IP y minuto: éste
/// limita operaciones <b>simultáneas</b> por réplica, sin importar de quién.
/// </para>
/// </summary>
public static class LimiteDeCargas
{
    /// <summary>El nombre de la política; la lleva toda ruta que recibe un archivo o un lote para cargar (<c>TodaCargaMasivaTieneLimite</c>).</summary>
    public const string Politica = "cargas";

    public const string Codigo = "Import.Busy";

    public const string Mensaje = "Hay otras cargas en curso; intente de nuevo en unos segundos.";

    /// <summary>
    /// Registra el limitador con los valores de <c>Cargas:Concurrencia</c> (por defecto 2 cargas en curso y 4 en cola por réplica).
    /// Lo que excede la cola recibe 429 con el sobre de siempre y el código <see cref="Codigo"/>.
    /// </summary>
    public static IServiceCollection AddLimiteDeCargas(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("Cargas:Concurrencia");
        var permisos = seccion.GetValue("Permisos", 2);
        var cola = seccion.GetValue("Cola", 4);
        if (permisos < 1 || cola < 0)
            throw new InvalidOperationException(
                $"Cargas:Concurrencia: Permisos debe ser al menos 1 y Cola no puede ser negativa (vinieron {permisos} y {cola}).");

        return services.AddRateLimiter(o => o.AddConcurrencyLimiter(Politica, c =>
        {
            c.PermitLimit = permisos;
            c.QueueLimit = cola;
            c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }));
    }

    /// <summary>La política que rechazó la petición es ésta (el rechazo de <c>AddRateLimiter</c> es uno solo para todas).</summary>
    public static bool EsSuya(HttpContext http) =>
        http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == Politica;

    /// <summary>El 429 con el sobre de la API. Público para probarlo sin tener que saturar el limitador.</summary>
    public static async ValueTask ResponderOcupadoAsync(HttpContext http, CancellationToken ct)
    {
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = "15";
        await http.Response.WriteAsJsonAsync(new { code = Codigo, message = Mensaje, traceId = http.TraceIdentifier }, ct);
    }
}
