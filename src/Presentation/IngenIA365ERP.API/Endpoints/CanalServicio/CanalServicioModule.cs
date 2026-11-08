using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Security.CanalServicio;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CanalServicio;

/// <summary>
/// Feature 021 (contracts/erp-api-de-servicio.md): la API de servicio que consume el canal digital del asociado. Sólo acepta el
/// token de servicio del canal (<see cref="PeticionDelCanal.Politica"/>); cada ruta reenvía a una consulta o un comando MediatR
/// (Principio III). Las rutas de cooperativa van en <see cref="Cooperativa"/>, que exige el canal encendido.
/// </summary>
public class CanalServicioModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var plataforma = Grupo(app, PeticionDelCanal.Prefijo);
        plataforma.MapGet("/tenants", async (ISender s, CancellationToken ct) => await s.Send(new ListChannelTenantsQuery(), ct))
            .WithName("Canal_Tenants_List");

        var cooperativa = Cooperativa(app);
        // R8: los cambios de habilitación desde el cursor, y la conciliación de la política que el canal pide antes de arrastrar.
        cooperativa.MapGet("/enablements", async (long? since, int? limit, ISender s, CancellationToken ct) =>
                await s.Send(new ListEnablementChangesQuery(since ?? 0, limit ?? 500), ct))
            .WithName("Canal_Enablements_Changes");
        cooperativa.MapPost("/enablements/reconcile", async (ISender s, CancellationToken ct) =>
                await s.Send(new ReconcileChannelEnablementsCommand(), ct))
            .WithName("Canal_Enablements_Reconcile");
    }

    /// <summary>El grupo base: token de servicio, sobre de error y sin documentación pública.</summary>
    internal static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string prefijo) =>
        app.MapGroup(prefijo)
            .WithTags("Canal digital (servicio)")
            .ExcludeFromDescription()
            .RequireAuthorization(PeticionDelCanal.Politica)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

    /// <summary>
    /// El grupo de una cooperativa (<c>/api/canal/v1/tenants/{tenantPublicId}</c>): además exige el canal encendido. Las rutas que
    /// deben seguir funcionando con el canal apagado (los avisos de intentos ya creados, FR-001) se registran con
    /// <see cref="AunApagado"/>.
    /// </summary>
    internal static RouteGroupBuilder Cooperativa(IEndpointRouteBuilder app) =>
        Grupo(app, $"{PeticionDelCanal.Prefijo}/tenants/{{{PeticionDelCanal.ParametroCooperativa}:guid}}")
            .AddEndpointFilter<CanalEncendidoFilter>();

    /// <summary>Marca una ruta de cooperativa que responde aunque el canal esté apagado.</summary>
    internal static RouteHandlerBuilder AunApagado(RouteHandlerBuilder ruta) => ruta.WithMetadata(new AunConCanalApagado());

    internal sealed class AunConCanalApagado;
}

/// <summary>Responde 409 <c>Channel.Disabled</c> si la cooperativa apagó el canal (FR-001).</summary>
internal sealed class CanalEncendidoFilter(IApplicationDbContext db, IDateTimeService reloj) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<CanalServicioModule.AunConCanalApagado>() is not null)
            return await next(context);

        if (!await ConfiguracionDelCanal.EncendidoAsync(db, reloj.UtcNow, http.RequestAborted))
        {
            return Results.Json(new
            {
                code = ConfiguracionDelCanal.Errores.Apagado,
                errorCode = ConfiguracionDelCanal.Errores.Apagado,
                message = "La cooperativa no tiene encendido el canal digital.",
                traceId = http.TraceIdentifier,
            }, statusCode: StatusCodes.Status409Conflict);
        }
        return await next(context);
    }
}
