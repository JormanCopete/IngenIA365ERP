using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Catalogs;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Los catálogos de Transporte (feature 020, US2; R6; contracts/api.md §3): <c>/api/transport/catalogs/{kind}</c> con el nombre del catálogo
/// (<c>marca</c>, <c>clase</c>, <c>carroceria</c>, <c>combustible</c>, <c>modalidad</c>, <c>clase-de-servicio</c>, <c>organismo-de-transito</c>,
/// <c>grupo-de-rodamiento</c>, <c>tipo-de-equipo</c>). Ver los tiene cualquier lectura de Transporte; administrarlos, <c>Catalogs.Manage</c>.
/// </summary>
public sealed class TransportCatalogsEndpoints : ICarterModule
{
    private static readonly AlgunPermiso Lectura = new(
        TransportPermissions.CatalogsView, TransportPermissions.VehiclesView, TransportPermissions.DocumentsView,
        TransportPermissions.DriversView, TransportPermissions.ServicesView, TransportPermissions.CatalogsManage);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, "/catalogs", "Transporte Catálogos");

        g.MapGet("/{kind}", async (string kind, bool? includeInactive, ISender s, CancellationToken ct) =>
                CatalogosDeTransporte.DelNombre(kind) is { } tipo
                    ? await s.Send(new ListTransportCatalogQuery(tipo, includeInactive ?? false), ct)
                    : (object)Result.Failure("Transport.CatalogUnknown", $"No existe el catálogo «{kind}»."))
            .WithName("Transport_Catalogs_List").RequirePermission(Lectura);

        g.MapPost("/{kind}", async (string kind, ValorRequest body, ISender s, CancellationToken ct) =>
                CatalogosDeTransporte.DelNombre(kind) is { } tipo
                    ? TransportGroup.Creado(await s.Send(new CreateTransportCatalogItemCommand(tipo, body.Code ?? string.Empty, body.Name ?? string.Empty), ct),
                        v => $"/api/transport/catalogs/{kind}/{v.PublicId}")
                    : Result.Failure("Transport.CatalogUnknown", $"No existe el catálogo «{kind}»."))
            .WithName("Transport_Catalogs_Create").RequirePermission(TransportPermissions.CatalogsManage);

        g.MapPut("/{kind}/{id:guid}", async (string kind, Guid id, CambioRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateTransportCatalogItemCommand(id, body.Name ?? string.Empty, body.IsActive ?? true), ct))
            .WithName("Transport_Catalogs_Update").RequirePermission(TransportPermissions.CatalogsManage);
    }

    public sealed record ValorRequest(string? Code, string? Name);

    public sealed record CambioRequest(string? Name, bool? IsActive);
}
