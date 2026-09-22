using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Azúcar sintáctico para encadenar permisos al construir endpoints Carter.
/// Uso:
///
/// <code>
/// group.MapGet("/users", ListUsersAsync)
///     .RequireAuthorization()
///     .RequirePermission("Security.Users.View");
///
/// // Múltiples permisos (todos exigidos = AND):
/// group.MapPost("/users/{publicId:guid}/disable", DisableUserAsync)
///     .RequireAuthorization()
///     .RequirePermission("Security.Users.Disable");
/// </code>
///
/// El filtro <see cref="PermissionAuthorizationFilter"/> se registra una sola
/// vez por endpoint via <see cref="RouteHandlerBuilder.AddEndpointFilter{T}"/>;
/// cada llamada a <c>RequirePermission</c> agrega un attribute más al metadata
/// y el filtro chequea TODOS al ejecutar (AND semántico).
/// </summary>
public static class PermissionAuthorizationExtensions
{
    /// <summary>
    /// Marca el endpoint como protegido por el permiso indicado. Sin permiso
    /// el filtro responde 404 indistinguible (FR-017).
    /// </summary>
    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder, string permissionCode)
    {
        builder.WithMetadata(new RequirePermissionAttribute(permissionCode));
        builder.AddEndpointFilter<PermissionAuthorizationFilter>();
        return builder;
    }

    /// <summary>
    /// Exige un segundo permiso sólo cuando la petición pide un archivo (<c>?format=xlsx|pdf|docx</c>).
    /// Feature 009 E2: los informes contables viven en una sola ruta por vista y el contrato separa
    /// <c>Reports.View</c> (pantalla) de <c>Reports.Export</c> (descarga). Se encadena después de
    /// <see cref="RequirePermission(RouteHandlerBuilder, string)"/>, que sigue cubriendo el de ver.
    /// </summary>
    public static RouteHandlerBuilder RequirePermissionWhenExporting(
        this RouteHandlerBuilder builder, string permissionCode)
    {
        builder.WithMetadata(new RequirePermissionWhenExportingAttribute(permissionCode));
        builder.AddEndpointFilter<PermisoDeExportacionFilter>();
        return builder;
    }

    /// <summary>Versión para <see cref="RouteGroupBuilder"/> — aplica a todos los endpoints del grupo.</summary>
    public static RouteGroupBuilder RequirePermission(
        this RouteGroupBuilder builder, string permissionCode)
    {
        builder.WithMetadata(new RequirePermissionAttribute(permissionCode));
        builder.AddEndpointFilter<PermissionAuthorizationFilter>();
        return builder;
    }
}
