using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Domain.Platform;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// El grupo base de Transporte (feature 020, R1; contracts/api.md §3): todo archivo de <c>Endpoints/Transport/*.cs</c> arma sus rutas
/// sobre <see cref="Grupo"/>, que es <c>/api/transport/{área}</c> con sesión, sobre de error, auditoría del intento denegado y la
/// <b>puerta del módulo</b> (<see cref="FiltroDeModuloHabilitado"/>): sin Transporte encendido en la cooperativa, 403
/// <c>Module.NotEnabled</c> para todos, el maestro incluido. Cada ruta declara su permiso. Lo vigila
/// <c>LosModulosHabilitablesTienenSuFiltro</c>.
/// </summary>
public static class TransportGroup
{
    public const string Prefijo = "/api/transport";

    /// <summary>El grupo de un área de Transporte: <paramref name="area"/> relativa al prefijo (<c>"/vehicles"</c>).</summary>
    public static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string area, string etiqueta) =>
        app.MapGroup(Prefijo + area)
            .WithTags(etiqueta)
            .RequireAuthorization()
            .RequireModulo(ModulosHabilitables.Transporte)
            .AuditarIntentoDenegado(ModuloDeAuditoria.Transport)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

    /// <summary>201 con la ubicación del recurso creado; un fallo pasa tal cual al sobre de error.</summary>
    public static object Creado<T>(Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.IsSuccess ? Results.Created(ubicacion(resultado.Value), resultado.Value) : resultado;
}

/// <summary>
/// Una ruta mínima para comprobar la puerta del módulo (feature 020, US1, T027): responde si la cooperativa tiene Transporte encendido.
/// </summary>
public sealed class TransportModuleStatusEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, string.Empty, "Transporte");
        g.MapGet("/module-status", () => Results.Ok(new { module = ModulosHabilitables.Transporte, enabled = true }))
            .WithName("Transport_ModuleStatus")
            .RequirePermission(TransportPermissions.VehiclesView);
    }
}
