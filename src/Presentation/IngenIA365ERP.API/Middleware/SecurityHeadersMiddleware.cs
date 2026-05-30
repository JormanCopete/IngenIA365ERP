namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// T137 — Headers de seguridad HTTP. HSTS solo en prod (en dev HTTP local
/// rompería). El CSP permite conexiones WebSocket para SignalR
/// (<c>connect-src</c>) e inline styles para SyncFusion.
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
