using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El grupo base de Cartera (feature 013, T0312; contracts/api.md §1.1, T67): todo archivo de <c>Endpoints/Lending/*.cs</c> arma sus rutas
/// sobre <see cref="Grupo"/>, que es <c>/api/lending/{área}</c> con <c>RequireAuthorization()</c>, <see cref="ErrorEnvelopeFilter"/> y el
/// metadato <c>.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)</c>: el 404 por falta de permiso deja <c>Lending.Access.Denied</c> y el 400
/// del validador <c>Lending.Request.Rejected</c>. El metadato va en el grupo y antes de las rutas, así su filtro envuelve al de permisos de
/// cada ruta (<c>.RequirePermission(...)</c>, que sigue siendo obligatorio en cada una: <c>LosEndpointsProtegidosExigenPermiso</c>). Este
/// archivo no publica rutas. (nuevo)
/// </summary>
public static class LendingGroup
{
    /// <summary>El prefijo de todas las rutas de Cartera.</summary>
    public const string Prefijo = "/api/lending";

    /// <summary>
    /// El grupo de un área de Cartera: <paramref name="area"/> relativa al prefijo (<c>"/sessions"</c>; vacía para rutas en la raíz) y la
    /// etiqueta de OpenAPI.
    /// </summary>
    public static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string area, string etiqueta) =>
        app.MapGroup(Prefijo + area)
            .WithTags(etiqueta)
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Lending)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

    /// <summary>201 con la ubicación del recurso creado; un fallo pasa tal cual al sobre de error.</summary>
    public static object Creado<T>(Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.IsSuccess ? Results.Created(ubicacion(resultado.Value), resultado.Value) : resultado;

    /// <summary>201 en una ubicación fija.</summary>
    public static object Creado<T>(Result<T> resultado, string ubicacion) =>
        resultado.IsSuccess ? Results.Created(ubicacion, resultado.Value) : resultado;
}
