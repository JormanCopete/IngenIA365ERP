using System.Security.Claims;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// Resuelve el tenant activo de la solicitud a partir del JWT central
/// (claim <c>active_tenant_id</c>, T044 — Feature 002).
///
/// <para>
/// Cambio mayor respecto a Fase 0: ya NO se aceptan los headers
/// <c>X-Tenant-Id</c>, el subdominio ni el query <c>?tenant=</c>. El JWT
/// emitido por <c>CentralJwtIssuer</c> es la única fuente de verdad — si
/// el claim no está presente el request está en el estado intermedio
/// post-login pre-select-tenant y solo puede consumir los endpoints de
/// sesión / auth / invitaciones.
/// </para>
///
/// <para>
/// Rutas exentas (NO requieren claim):
/// <list type="bullet">
///   <item><c>/api/auth/*</c> — login, refresh, logout, mfa/verify, me</item>
///   <item><c>/api/sessions/*</c> — select-tenant, switch-tenant, set-default</item>
///   <item><c>/api/invitations/*</c> — preview, accept (público pre-auth)</item>
///   <item><c>/api/saas/*</c> — superficie master admin (no scoped a tenant)</item>
///   <item><c>/api/admin/*</c> — legado master admin (carve-out Fase 0)</item>
///   <item><c>/api/profile/mfa/enroll</c> y <c>/confirm</c> — el enrollment
///     forzado (FR-003b) llega con challenge token <c>purpose=mfa-enroll</c>
///     que por definición no porta tenant; ambos endpoints solo tocan
///     <c>ADM_*</c> y quedan custodiados por <c>RequirePurpose</c></item>
///   <item><c>/api/health</c>, <c>/swagger</c>, <c>/_framework</c>, <c>/_vs</c>, <c>/hubs/*</c></item>
/// </list>
/// </para>
///
/// <para>
/// Respaldo master (FR-039 / FR-040a): el master global no tiene membresías y
/// por lo tanto nunca porta <c>active_tenant_id</c>. Para rutas
/// <c>/api/tenants/{tenantPublicId}/...</c> con JWT master autenticado, el
/// tenant se resuelve desde el segmento de la ruta; la autorización sigue a
/// cargo de <c>RequireTenantAdmin</c>/<c>RequireMasterAdmin</c> en el endpoint.
/// </para>
/// </summary>
public class TenantResolutionMiddleware
{
    private const string ActiveTenantIdClaim = "active_tenant_id";

    private static readonly string[] ExemptPrefixes =
    [
        "/api/auth/",
        "/api/sessions/",
        "/api/invitations/",
        "/api/saas/",
        "/api/admin",
        "/api/profile/mfa/enroll",
        "/api/profile/mfa/confirm",
        "/api/health",
        "/health",     // /health/live y /health/ready (T031 — sin tenant)
        "/swagger",
        "/_framework",
        "/_vs",
        "/hubs/",
    ];

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        // T044: única fuente de verdad — claim active_tenant_id del JWT validado.
        // Sin autenticación o sin claim → respuesta tipada Session.TenantNotSelected.
        // Excepción única (FR-039/FR-040a): master global sin claim opera
        // /api/tenants/{id}/... resolviendo el tenant desde la ruta.
        var claimValue = context.User?.FindFirst(ActiveTenantIdClaim)?.Value;
        if ((string.IsNullOrWhiteSpace(claimValue) ||
             !Guid.TryParse(claimValue, out var activeTenantId)) &&
            !(IsMasterAdmin(context.User) && TryGetTenantIdFromPath(path, out activeTenantId)))
        {
            await WriteTenantNotSelectedAsync(context);
            return;
        }

        // Resolución: CentralJwtIssuer (T037) emite el claim con el PublicId (Guid)
        // del tenant — el Id interno (int) nunca sale de la BD. IsDeleted está
        // cubierto por HasQueryFilter en TenantConfiguration.
        var adminDb = context.RequestServices.GetRequiredService<AdminDbContext>();
        var tenant = await adminDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == activeTenantId && t.IsActive);

        if (tenant is null)
        {
            await WriteTenantNotFoundAsync(context, activeTenantId);
            return;
        }

        context.Items["TenantInfo"] = tenant;
        context.Items["TenantId"] = tenant.Id;
        context.Items["TenantPublicId"] = tenant.PublicId;
        context.Items["TenantSchema"] = tenant.SchemaName;

        await _next(context);
    }

    private static bool IsMasterAdmin(ClaimsPrincipal? user) =>
        bool.TryParse(user?.FindFirst("is_global_master_admin")?.Value, out var isMaster) && isMaster;

    private static bool TryGetTenantIdFromPath(string path, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        const string prefix = "/api/tenants/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var rest = path[prefix.Length..];
        var slash = rest.IndexOf('/');
        var segment = slash < 0 ? rest : rest[..slash];
        return Guid.TryParse(segment, out tenantId);
    }

    private static bool IsExempt(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static async Task WriteTenantNotSelectedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            errorCode = "Session.TenantNotSelected",
            message = "Se requiere seleccionar una empresa antes de consumir este recurso.",
            traceId = context.TraceIdentifier,
        });
    }

    private static async Task WriteTenantNotFoundAsync(HttpContext context, Guid tenantId)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            errorCode = "Tenant.NotFound",
            message = $"La empresa indicada (Id={tenantId}) no existe o está inactiva.",
            traceId = context.TraceIdentifier,
        });
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
