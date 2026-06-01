using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

namespace IngenIA365ERP.API.Logging;

/// <summary>
/// T123 — Enricher Serilog que añade <c>central_user_id</c> y
/// <c>active_tenant_id</c> a cada log emitido bajo un HttpContext autenticado
/// con el JWT central. Si no hay HttpContext o no hay claim, omite la
/// propiedad sin emitir basura.
///
/// <para>
/// Convive con el <c>LoggingBehavior</c> de MediatR (que arma el scope con
/// tenant/usuario para el lifecycle del request) — este enricher cubre los
/// logs fuera del MediatR pipeline (middlewares, errores tempranos, etc.).
/// </para>
/// </summary>
public sealed class CentralIdentityLogEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CentralIdentityLogEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(sub))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("central_user_id", sub));
        }

        var activeTenant = user.FindFirst("active_tenant_id")?.Value;
        if (!string.IsNullOrEmpty(activeTenant))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("active_tenant_id", activeTenant));
        }

        var purpose = user.FindFirst("purpose")?.Value;
        if (!string.IsNullOrEmpty(purpose))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("jwt_purpose", purpose));
        }
    }
}
