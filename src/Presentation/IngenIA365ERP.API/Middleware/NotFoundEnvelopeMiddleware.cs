using IngenIA365ERP.API.Filters;
using System.Text.Json;

namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// T074 — Garantiza que toda respuesta 404 bajo <c>/api/*</c> tenga el envelope
/// canónico <c>{ code: "Generic.NotFound", message, traceId }</c>, sea por
/// endpoint inexistente o por permiso faltante
/// (vía <see cref="PermissionAuthorizationFilter"/>). Esto es lo que hace
/// indistinguible "no existe" de "no tienes acceso" — requisito FR-017 / SC-005.
///
/// El middleware corre AL FINAL del pipeline. Si el response ya empezó a
/// escribirse (un endpoint produjo su propio 404 con body), no toca nada.
/// </summary>
public sealed class NotFoundEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotFoundEnvelopeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status404NotFound
            || context.Response.HasStarted
            || context.Response.ContentLength > 0)
        {
            return;
        }

        // Solo aplicamos el envelope a la superficie API; deja swagger/UI/etc en paz.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            return;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new
        {
            code = "Generic.NotFound",
            message = "Recurso no encontrado.",
            traceId = context.TraceIdentifier
        }, JsonOptions);
        await context.Response.WriteAsync(payload);
    }
}

public static class NotFoundEnvelopeMiddlewareExtensions
{
    public static IApplicationBuilder UseNotFoundEnvelope(this IApplicationBuilder app) =>
        app.UseMiddleware<NotFoundEnvelopeMiddleware>();
}
