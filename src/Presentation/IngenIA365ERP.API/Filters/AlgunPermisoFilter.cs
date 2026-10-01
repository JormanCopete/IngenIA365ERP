using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Una ruta que se abre con <b>cualquiera</b> de varios permisos (feature 012, I4, T746): el enlace de descarga de un artefacto de un documento
/// electrónico lo gobierna la regla del dueño del adjunto —<c>Inventory.Sales.View</c> para uno de venta, <c>Inventory.Purchases.View</c> para
/// uno de compra (decisiones-transversales §2.10, FR-068, T41)—, que el handler decide sobre el documento concreto. La ruta deja pasar a quien
/// tiene alguno y responde el mismo 404 que lo inexistente a quien no tiene ninguno. Se pone con
/// <see cref="PermissionAuthorizationExtensions.RequirePermission(RouteHandlerBuilder, AlgunPermiso)"/>. (nuevo)
/// </summary>
public sealed record AlgunPermiso(params string[] Codigos);

/// <summary>El filtro de <see cref="AlgunPermiso"/>: el mismo camino de <see cref="PermissionAuthorizationFilter.TieneAsync"/> por cada código. (nuevo)</summary>
public sealed class AlgunPermisoFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var requisitos = http.GetEndpoint()?.Metadata.GetOrderedMetadata<AlgunPermiso>() ?? [];
        foreach (var requisito in requisitos)
        {
            var alguno = false;
            foreach (var codigo in requisito.Codigos)
            {
                if (!await PermissionAuthorizationFilter.TieneAsync(http, codigo)) continue;
                alguno = true;
                break;
            }
            if (!alguno) return PermissionAuthorizationFilter.NotFoundEnvelope(http);
        }
        return await next(context);
    }
}
