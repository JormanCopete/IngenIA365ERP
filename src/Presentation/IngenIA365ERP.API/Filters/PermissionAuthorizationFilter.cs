using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// T074 — Filtro de autorización por permiso granular. Lee el o los
/// <see cref="RequirePermissionAttribute"/> que decoran el endpoint y
/// verifica que el JWT del request los contenga en sus claims <c>perm</c>.
///
/// <para>
/// <b>Indistinguibilidad 404</b> (FR-017, SC-005): cuando falta el permiso,
/// la respuesta es <b>idéntica</b> a la de un endpoint inexistente —
/// <c>404</c> con envelope <c>{ code: "Generic.NotFound", message, traceId }</c>.
/// Esto previene enumeración de endpoints y delata mínima información al
/// atacante (no se distingue "existe pero no tienes acceso" de "no existe").
/// </para>
///
/// <para>
/// Si el usuario no está autenticado y el endpoint requiere permiso, también
/// se responde 404 — la <c>RequireAuthorization()</c> de Carter ya debería
/// haber emitido 401, pero por defensa-en-profundidad el filtro lo cubre.
/// </para>
/// </summary>
public sealed class PermissionAuthorizationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requirements = context.HttpContext
            .GetEndpoint()?
            .Metadata
            .GetOrderedMetadata<RequirePermissionAttribute>()
            ?? [];

        if (requirements.Count == 0)
        {
            return await next(context);
        }

        var user = context.HttpContext.User;
        var hasAllPerms = requirements.All(req =>
            user.HasClaim(c => c.Type == "perm" && c.Value == req.PermissionCode));

        if (hasAllPerms)
        {
            return await next(context);
        }

        return NotFoundEnvelope(context.HttpContext);
    }

    /// <summary>
    /// Construye la respuesta 404 con el envelope canónico — debe ser
    /// pixel-idéntica a la que devuelve <see cref="ErrorEnvelopeFilter"/>
    /// para <c>Generic.NotFound</c> y a la respuesta default de ASP.NET
    /// para un endpoint inexistente bajo el mismo <see cref="ErrorEnvelopeFilter"/>.
    /// </summary>
    internal static IResult NotFoundEnvelope(HttpContext http) =>
        Results.Json(
            new
            {
                code = "Generic.NotFound",
                message = "Recurso no encontrado.",
                traceId = http.TraceIdentifier
            },
            statusCode: StatusCodes.Status404NotFound);
}
