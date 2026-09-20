using IngenIA365ERP.API.Reports;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Marca un endpoint de informe como protegido por un segundo permiso que sólo se exige cuando
/// la query string pide un archivo (<c>format=xlsx|pdf|docx</c>). Se aplica con
/// <see cref="PermissionAuthorizationExtensions.RequirePermissionWhenExporting"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionWhenExportingAttribute : Attribute
{
    public string PermissionCode { get; }

    public RequirePermissionWhenExportingAttribute(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("El código de permiso es obligatorio.", nameof(permissionCode));
        PermissionCode = permissionCode;
    }
}

/// <summary>
/// Feature 009 E2 (contracts/api.md §8): una sola ruta por vista sirve la pantalla (<c>json</c>,
/// <c>Reports.View</c>) y la descarga (<c>Reports.Export</c>). El permiso de ver lo pone
/// <see cref="PermissionAuthorizationFilter"/> como en toda ruta; el de exportar depende de un
/// valor de la query string, que un atributo fijo no puede mirar, así que lo decide este filtro
/// leyendo <c>format</c> y preguntando por <see cref="PermissionAuthorizationFilter.TieneAsync"/>
/// —el mismo camino, atajo del maestro incluido—. Sin el permiso responde el mismo 404
/// <c>Generic.NotFound</c> que cualquier otra falta de permiso (FR-017): quien puede ver pero no
/// exportar no se entera de que la exportación existe.
/// </summary>
public sealed class PermisoDeExportacionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var requisitos = http.GetEndpoint()?.Metadata.GetOrderedMetadata<RequirePermissionWhenExportingAttribute>() ?? [];
        if (requisitos.Count == 0) return await next(context);

        var formato = http.Request.Query["format"].ToString();
        if (!EntregaDeInformes.EsExportacion(formato)) return await next(context);

        foreach (var requisito in requisitos)
        {
            if (!await PermissionAuthorizationFilter.TieneAsync(http, requisito.PermissionCode))
                return PermissionAuthorizationFilter.NotFoundEnvelope(http);
        }

        return await next(context);
    }
}
