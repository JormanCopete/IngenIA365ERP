using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Endpoints anónimos — tenant viene en body (login) o token (refresh).
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/api/admin") ||
            path.StartsWith("/api/health") ||
            path.StartsWith("/api/auth/login") ||
            path.StartsWith("/api/auth/refresh") ||
            path.StartsWith("/swagger") ||
            path.StartsWith("/_framework") ||
            path.StartsWith("/_vs"))
        {
            await _next(context);
            return;
        }

        // T023 — Para requests autenticados, el tenant_id del JWT es la
        // única fuente de verdad. Si el cliente envió X-Tenant-Id en el
        // header y NO coincide con el claim, devolvemos 403: es un intento
        // de saltar al schema de otra cooperativa (FR-002, SC-005).
        string? tenantIdFromClaim = null;
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            tenantIdFromClaim = context.User.FindFirst("tenant_id")?.Value;
            var headerTenant = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenant)
                && !string.IsNullOrEmpty(tenantIdFromClaim)
                && !string.Equals(headerTenant, tenantIdFromClaim, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "Tenant.Forbidden",
                    message = "El claim tenant_id no coincide con el header X-Tenant-Id. La operación fue rechazada.",
                    traceId = context.TraceIdentifier
                });
                return;
            }
        }

        // Estrategia 1: claim (más fuerte) — Estrategia 2: header — Estrategia 3: subdominio — Estrategia 4: query (dev).
        string? tenantId = tenantIdFromClaim
            ?? context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length >= 3 && parts[0] != "www" && parts[0] != "api")
                tenantId = parts[0];
        }

        if (string.IsNullOrEmpty(tenantId)
            && context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            tenantId = context.Request.Query["tenant"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "Tenant.Missing",
                message = "No se pudo identificar la cooperativa. Envía el header X-Tenant-Id o usa el subdominio correcto.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        var tenantDbContext = context.RequestServices.GetRequiredService<TenantDbContext>();
        var tenant = await tenantDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Identifier == tenantId && t.IsActive);

        if (tenant == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "Tenant.NotFound",
                message = $"Cooperativa '{tenantId}' no encontrada o inactiva.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        context.Items["TenantInfo"] = tenant;
        context.Items["TenantId"] = tenant.Id;
        context.Items["TenantSchema"] = tenant.Schema;

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
