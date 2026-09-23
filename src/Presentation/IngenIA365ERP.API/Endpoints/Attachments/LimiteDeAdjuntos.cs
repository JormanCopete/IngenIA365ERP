using System.Threading.RateLimiting;
using IngenIA365ERP.Application.Attachments.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace IngenIA365ERP.API.Endpoints.Attachments;

/// <summary>
/// Feature 011 (research R13): cuántas operaciones de adjuntos atiende a la vez cada réplica. Con las
/// transferencias fuera del servidor lo que queda en estas rutas es firmar, leer los primeros bytes y
/// servir el formato anterior —que sigue armando el archivo en memoria—; el límite es sobre todo para
/// eso. Hasta el 2026-09-23 no había ninguno: veinte descargas simultáneas de 25 MB eran ~1,5 GB en un
/// pod de 1 GiB.
///
/// <para>
/// Convive con <c>AspNetCoreRateLimit</c>, que limita peticiones por IP y minuto y sigue igual: éste
/// limita operaciones <b>simultáneas</b> por réplica, sin importar de quién.
/// </para>
/// </summary>
public static class LimiteDeAdjuntos
{
    /// <summary>El nombre de la política; lo llevan el grupo <c>/api/attachments</c> y las rutas de descarga de módulo.</summary>
    public const string Politica = "adjuntos";

    public const string Mensaje = "Hay muchas transferencias en curso; intente de nuevo en unos segundos.";

    /// <summary>
    /// Registra el limitador con los valores de <c>AttachmentStorage:Concurrencia</c> (por defecto 32
    /// operaciones en curso y 64 en cola por réplica). Lo que excede la cola recibe 429 con el sobre de
    /// siempre y el código <see cref="AttachmentErrorCodes.Busy"/>.
    /// </summary>
    public static IServiceCollection AddLimiteDeAdjuntos(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection($"{LimitesDeAdjuntos.SectionName}:Concurrencia");
        var permisos = seccion.GetValue("Permisos", 32);
        var cola = seccion.GetValue("Cola", 64);
        if (permisos < 1 || cola < 0)
            throw new InvalidOperationException(
                $"{LimitesDeAdjuntos.SectionName}:Concurrencia: Permisos debe ser al menos 1 y Cola no puede ser negativa (vinieron {permisos} y {cola}).");

        return services.AddRateLimiter(o =>
        {
            o.AddConcurrencyLimiter(Politica, c =>
            {
                c.PermitLimit = permisos;
                c.QueueLimit = cola;
                c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (contexto, ct) => ResponderOcupadoAsync(contexto.HttpContext, ct);
        });
    }

    /// <summary>El 429 con el sobre de la API. Público para probarlo sin tener que saturar el limitador.</summary>
    public static async ValueTask ResponderOcupadoAsync(HttpContext http, CancellationToken ct)
    {
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = "5";
        await http.Response.WriteAsJsonAsync(
            new { code = AttachmentErrorCodes.Busy, message = Mensaje, traceId = http.TraceIdentifier }, ct);
    }
}
