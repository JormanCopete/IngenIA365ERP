using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.API.Services;
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
/// se responde 404 — la <c>RequireAuthorization()</c> de Carter ya emitió 401
/// antes de llegar aquí, pero por defensa-en-profundidad el filtro lo cubre.
/// </para>
///
/// <para>
/// <b>Atajo del administrador maestro</b>: el emisor de identidad central
/// (<c>CentralJwtIssuer</c>) no emite claims <c>perm</c> — sólo los emite el
/// emisor heredado, al que la interfaz ya no llega. Sin este atajo, el maestro
/// veía 404 en TODO endpoint con permiso: podía registrar una cooperativa
/// (esa ruta se guarda con <see cref="RequireMasterAdminAttribute"/>) pero no
/// volver a listarla. Se reutiliza esa misma guardia, que es la que el resto
/// del proyecto ya usa para lo global-SaaS, en vez de inventar otra: exige
/// <c>purpose=full</c>, así que un token a medio autenticar —MFA pendiente,
/// por ejemplo— NO pasa por aquí.
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

        // Antes de mirar los `perm`: el maestro global no los lleva en el token.
        // Esto NO relaja el aislamiento entre cooperativas — el tenant sigue
        // saliendo de TenantResolutionMiddleware, no de este filtro.
        if (RequireMasterAdminAttribute.Check(context.HttpContext).IsAllowed)
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

        // El token central no lleva claims `perm`, asi que para todo el mundo
        // salvo el maestro la comprobacion de arriba siempre da falsa. Aqui se
        // resuelven de verdad, contra la cooperativa activa.
        if (await TienePermisosResueltosAsync(context.HttpContext, requirements))
        {
            return await next(context);
        }

        return NotFoundEnvelope(context.HttpContext);
    }

    /// <summary>
    /// Resolución por petición. Devuelve false ante cualquier problema —nunca
    /// propaga— porque una excepción aquí subiría como 500 y delataría que el
    /// endpoint existe, que es justo lo que la indistinguibilidad 404 evita.
    /// Fallar cerrado deja al usuario sin acceso; fallar abierto se lo da a
    /// quien no debe.
    /// </summary>
    private static async Task<bool> TienePermisosResueltosAsync(
        HttpContext http, IReadOnlyList<RequirePermissionAttribute> requisitos)
    {
        var servicios = http.RequestServices;
        if (servicios is null) return false;

        try
        {
            var servicio = servicios.GetService<PermisosDeLaPeticion>();
            if (servicio is null) return false;

            var concedidos = await servicio.ResolverAsync(http, http.RequestAborted);
            if (concedidos.Count == 0) return false;

            var conjunto = new HashSet<string>(concedidos, StringComparer.OrdinalIgnoreCase);
            return requisitos.All(r => conjunto.Contains(r.PermissionCode));
        }
        catch (Exception ex)
        {
            // El propio registro va en try: si el contenedor no puede darnos un
            // logger, eso no puede convertirse en la excepcion que tumbe la
            // autorizacion. Denegar y seguir.
            try
            {
                servicios.GetService<ILoggerFactory>()?
                    .CreateLogger<PermissionAuthorizationFilter>()
                    .LogError(ex, "Fallo al resolver permisos de la peticion. Se responde 404.");
            }
            catch
            {
                // Sin registro posible. El 404 de abajo sigue siendo lo correcto.
            }

            return false;
        }
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
