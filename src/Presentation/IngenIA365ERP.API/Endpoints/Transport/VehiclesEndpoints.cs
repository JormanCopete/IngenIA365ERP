using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Vehículos (feature 020, US2; contracts/api.md §3): ficha, alta (también con propietario nuevo en un paso), edición sin estado ni
/// propietario, activar e inactivar, retiro, cambio de propietario con las deudas del saliente, y equipos. Cada ruta exige su permiso y
/// cuelga de la puerta del módulo (<see cref="TransportGroup"/>). Las plantillas de vehículos y de cambios se suman en la US3.
/// </summary>
public sealed class VehiclesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, "/vehicles", "Transporte Vehículos");

        g.MapGet("/", async (string? q, VehicleStatus? status, Guid? owner, int? page, int? pageSize, ISender s, CancellationToken ct) =>
                await s.Send(new ListVehiclesQuery(q, status, owner, page ?? 1, pageSize ?? 50), ct))
            .WithName("Transport_Vehicles_List").RequirePermission(TransportPermissions.VehiclesView);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetVehicleQuery(id), ct))
            .WithName("Transport_Vehicles_Get").RequirePermission(TransportPermissions.VehiclesView);

        g.MapPost("/", async (AltaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new CreateVehicleCommand(body.Vehicle ?? new VehicleInput(), body.OwnerPersonPublicId ?? Guid.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/transport/vehicles/{v.VehiclePublicId}"))
            .WithName("Transport_Vehicles_Create").ConClaveDeOperacion().RequirePermission(TransportPermissions.VehiclesCreate);

        g.MapPost("/with-person", async (AltaConPersonaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new RegisterVehicleWithPersonCommand(body.Person!, body.Vehicle ?? new VehicleInput(), body.Authorization)
                    { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/transport/vehicles/{v.VehiclePublicId}"))
            .WithName("Transport_Vehicles_CreateWithPerson").ConClaveDeOperacion()
            .RequirePermission("Core.People.Create").RequirePermission(TransportPermissions.VehiclesCreate);

        g.MapPut("/{id:guid}", async (Guid id, VehicleInput body, ISender s, CancellationToken ct) => await s.Send(new UpdateVehicleCommand(id, body), ct))
            .WithName("Transport_Vehicles_Update").RequirePermission(TransportPermissions.VehiclesUpdate);

        g.MapPost("/{id:guid}/status", async (Guid id, EstadoRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new ChangeVehicleStatusCommand(id, body.Status, body.EffectiveDate, body.Reason ?? string.Empty), ct))
            .WithName("Transport_Vehicles_ChangeStatus").RequirePermission(TransportPermissions.VehiclesChangeStatus);

        g.MapPost("/{id:guid}/retire", async (Guid id, RetiroRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new RetireVehicleCommand(id, body.EffectiveDate, body.Reason ?? string.Empty), ct))
            .WithName("Transport_Vehicles_Retire").RequirePermission(TransportPermissions.VehiclesRetire);

        g.MapGet("/{id:guid}/owner-debts", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetOwnerDebtsQuery(id), ct))
            .WithName("Transport_Vehicles_OwnerDebts").RequirePermission(new AlgunPermiso(TransportPermissions.VehiclesChangeOwner, TransportPermissions.VehiclesRetire));

        g.MapPost("/{id:guid}/owner", async (Guid id, PropietarioRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new ChangeVehicleOwnerCommand(id, body.NewOwnerPersonPublicId ?? Guid.Empty, body.EffectiveDate, body.Reason ?? string.Empty), ct))
            .WithName("Transport_Vehicles_ChangeOwner").RequirePermission(TransportPermissions.VehiclesChangeOwner);

        g.MapPut("/{id:guid}/equipment", async (Guid id, IReadOnlyList<EquipmentInput> body, ISender s, CancellationToken ct) =>
                await s.Send(new SetVehicleEquipmentCommand(id, body ?? []), ct))
            .WithName("Transport_Vehicles_Equipment").RequirePermission(TransportPermissions.VehiclesUpdate);

        // US3: las plantillas de vehículos y de cambios de estado y propietario.
        TransportImports.MapPlantillasDeVehiculos(g);
    }

    public sealed record AltaRequest(VehicleInput? Vehicle, Guid? OwnerPersonPublicId);

    public sealed record AltaConPersonaRequest(PersonInput? Person, VehicleInput? Vehicle, AutorizacionAlCrear? Authorization);

    public sealed record EstadoRequest(VehicleStatus Status, DateOnly EffectiveDate, string? Reason);

    public sealed record RetiroRequest(DateOnly EffectiveDate, string? Reason);

    public sealed record PropietarioRequest(Guid? NewOwnerPersonPublicId, DateOnly EffectiveDate, string? Reason);
}
