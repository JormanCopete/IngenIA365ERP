using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Anonymous endpoints — tenant comes in body (login) or token (refresh).
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

        // Strategy 1: X-Tenant-Id header
        string? tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        // Strategy 2: Subdomain (tenant.ingenia365.app)
        if (string.IsNullOrEmpty(tenantId))
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length >= 3 && parts[0] != "www" && parts[0] != "api")
                tenantId = parts[0];
        }

        // Strategy 3: Query param (development only)
        if (string.IsNullOrEmpty(tenantId) && context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            tenantId = context.Request.Query["tenant"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not specified. Use X-Tenant-Id header." });
            return;
        }

        // Resolve tenant from DB
        var tenantDbContext = context.RequestServices.GetRequiredService<TenantDbContext>();
        var tenant = await tenantDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Identifier == tenantId && t.IsActive);

        if (tenant == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Tenant '{tenantId}' not found or inactive." });
            return;
        }

        // Store in HttpContext for downstream services
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
