using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Drivers;
using IngenIA365ERP.Application.Transport.Imports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Conductores (feature 020, US5; R5; contracts/api.md §3, «Conductores»): registro básico, retiro, asignaciones con fechas y la plantilla
/// <c>transport.drivers</c>.
/// </summary>
public sealed class DriversEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, "/drivers", "Transporte Conductores");

        g.MapGet("/", async (string? q, bool? includeRetired, ISender s, CancellationToken ct) => await s.Send(new ListDriversQuery(q, includeRetired ?? false), ct))
            .WithName("Transport_Drivers_List").RequirePermission(TransportPermissions.DriversView);

        g.MapPost("/", async (AltaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new RegisterDriverCommand(b.PersonPublicId ?? Guid.Empty, b.Since) { OperationKey = http.ClaveDeOperacion() }, ct),
                    d => $"/api/transport/drivers/{d.DriverPublicId}"))
            .WithName("Transport_Drivers_Register").ConClaveDeOperacion().RequirePermission(TransportPermissions.DriversManage);

        g.MapPost("/with-person", async (ConPersonaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new RegisterDriverWithPersonCommand(b.Person!, b.Authorization) { OperationKey = http.ClaveDeOperacion() }, ct),
                    d => $"/api/transport/drivers/{d.DriverPublicId}"))
            .WithName("Transport_Drivers_RegisterWithPerson").ConClaveDeOperacion()
            .RequirePermission("Core.People.Create").RequirePermission(TransportPermissions.DriversManage);

        g.MapPost("/{id:guid}/retire", async (Guid id, FechaRequest b, ISender s, CancellationToken ct) =>
                await s.Send(new RetireDriverCommand(id, b.Date ?? DateOnly.FromDateTime(DateTime.Today)), ct))
            .WithName("Transport_Drivers_Retire").RequirePermission(TransportPermissions.DriversManage);

        g.MapPost("/{id:guid}/assignments", async (Guid id, AsignacionRequest b, ISender s, CancellationToken ct) =>
                await s.Send(new AssignDriverCommand(id, b.VehiclePublicId ?? Guid.Empty, b.From, b.IsPrimary), ct))
            .WithName("Transport_Drivers_Assign").RequirePermission(TransportPermissions.DriversManage);

        g.MapPost("/{id:guid}/assignments/{aId:guid}/end", async (Guid id, Guid aId, TerminarRequest b, ISender s, CancellationToken ct) =>
                await s.Send(new EndDriverAssignmentCommand(id, aId, b.To), ct))
            .WithName("Transport_Drivers_EndAssignment").RequirePermission(TransportPermissions.DriversManage);

        g.MapPlantillaDePlataformaConExistentes(PlantillaDeConductores.Clave, "Transport_Drivers",
            (modo, archivo, motivo, existentes, clave) => new ImportDriversCommand(modo, archivo, motivo, existentes) { OperationKey = clave });
    }

    public sealed record AltaRequest(Guid? PersonPublicId, DateOnly? Since);

    public sealed record ConPersonaRequest(PersonInput? Person, AutorizacionAlCrear? Authorization);

    public sealed record FechaRequest(DateOnly? Date);

    public sealed record AsignacionRequest(Guid? VehiclePublicId, DateOnly From, bool IsPrimary);

    public sealed record TerminarRequest(DateOnly To);
}
