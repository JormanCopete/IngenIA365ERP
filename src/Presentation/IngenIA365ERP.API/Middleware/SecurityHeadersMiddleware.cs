using Microsoft.Net.Http.Headers;

namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// T137 — Headers de seguridad HTTP. HSTS solo en prod (en dev HTTP local
/// rompería). El CSP permite conexiones WebSocket para SignalR
/// (<c>connect-src</c>) e inline styles para SyncFusion. Bajo <c>/api</c>
/// declara además <c>Cache-Control: no-store</c> si el endpoint no fijó uno.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        if (!_env.IsDevelopment())
        {
            // HSTS con 1 año + subdominios — solo cuando estamos seguros de servir HTTPS.
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        // CSP: permite WS para SignalR (notifications hub) y blob:/data: para
        // exports de PDF/CSV materializados como object URLs en el cliente.
        // 'unsafe-inline' en styles es necesario para SyncFusion en runtime;
        // en cuanto migremos a CSS extraído o nonces se puede eliminar.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "connect-src 'self' wss: https:; " +
            "img-src 'self' data: blob:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'none'";

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        // X-XSS-Protection deprecado: 0 desactiva el filtro buggy del browser legacy.
        headers["X-XSS-Protection"] = "0";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // No revelar versión del runtime.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        // Cache-Control declarado para todo lo que responde la API. Antes no
        // llevaba ninguno: Cloudflare no cachea JSON por defecto, pero «por
        // defecto» no es «declarado», y un saldo o un listado de empleados
        // servido desde una caché intermedia es un dato de otra persona en otra
        // pantalla. Va en OnStarting y no acá arriba porque un endpoint que fije
        // la suya tiene que ganar —los health checks, por ejemplo, ya ponen
        // «no-store, no-cache»— y eso sólo se sabe cuando la respuesta empieza.
        context.Response.OnStarting(() =>
        {
            if (context.Request.Path.StartsWithSegments("/api")
                && !context.Response.Headers.ContainsKey(HeaderNames.CacheControl))
            {
                context.Response.Headers.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
