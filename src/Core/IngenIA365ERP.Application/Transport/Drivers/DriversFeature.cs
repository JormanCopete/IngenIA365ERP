using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Drivers;

// Feature 020 (US5; R5; T085–T087; FR-018, FR-019; contracts/api.md §3, «Conductores»): sólo el registro básico. La marca Person.IsDriver
// la escribe RolDeConductor, nunca una casilla de la persona; un vehículo puede tener varios conductores vigentes y a lo sumo un titular.

/// <summary>
/// El rol de conductor de una persona (R5, T085; el modelo de <c>RolDeVendedor</c>): crea, restaura y retira la fila de
/// <c>TRN_Drivers</c> y pone o quita <c>IsDriver</c> en la persona, en la misma unidad de trabajo. <b>No guarda.</b>
/// </summary>
public sealed class RolDeConductor(IApplicationDbContext db, IDateTimeService reloj)
{
    public async Task<Driver?> FilaDeAsync(int personId, CancellationToken ct) =>
        await db.Drivers.IgnoreQueryFilters().Where(d => d.PersonId == personId).OrderBy(d => d.IsDeleted).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct)
        ?? db.Drivers.Local.FirstOrDefault(d => d.PersonId == personId && personId != 0);

    /// <summary>Registra a la persona como conductora; si ya tenía un registro retirado, lo reactiva.</summary>
    public async Task<Result<Driver>> AsignarAsync(Person persona, DateOnly desde, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(persona);
        var fila = persona.Id == 0 ? null : await FilaDeAsync(persona.Id, ct);
        if (fila is { IsDeleted: false, Status: DriverStatus.Active })
            return Result.Failure<Driver>(new ErrorConDatos("Driver.AlreadyRegistered", $"{NombreVisible.De(persona)} ya es conductor.", new { driverPublicId = fila.PublicId }));

        if (fila is null)
        {
            fila = new Driver { PersonId = persona.Id, Person = persona.Id == 0 ? persona : null, Since = desde, Status = DriverStatus.Active };
            db.Drivers.Add(fila);
        }
        else
        {
            fila.IsDeleted = false;
            fila.DeletedAt = null;
            fila.DeletedBy = null;
            fila.Status = DriverStatus.Active;
            fila.Since = desde;
            fila.RetiredOn = null;
        }
        persona.IsDriver = true;
        return Result.Success(fila);
    }

    /// <summary>Retira el registro de conductor: termina sus asignaciones vigentes y quita la marca de la persona.</summary>
    public async Task<Result> RetirarAsync(Driver fila, DateOnly fecha, CancellationToken ct)
    {
        if (fila.Status == DriverStatus.Retired) return Result.Failure("Driver.Retired", "El conductor ya está retirado.");
        var vigentes = await db.DriverAssignments.Where(a => a.DriverId == fila.Id && (a.To == null || a.To > fecha)).ToListAsync(ct);
        foreach (var a in vigentes) a.To = fecha < a.From ? a.From : fecha;
        fila.Status = DriverStatus.Retired;
        fila.RetiredOn = fecha;
        var persona = fila.Person ?? await db.People.FirstOrDefaultAsync(p => p.Id == fila.PersonId, ct);
        if (persona is { IsDeleted: false }) persona.IsDriver = false;
        fila.UpdatedAt = reloj.UtcNow;
        return Result.Success();
    }

    /// <summary>Asigna el conductor a un vehículo desde una fecha (FR-018).</summary>
    public async Task<Result<DriverAssignment>> AsignarVehiculoAsync(Driver conductor, Vehicle vehiculo, DateOnly desde, bool titular, CancellationToken ct)
    {
        if (conductor.Status == DriverStatus.Retired) return Result.Failure<DriverAssignment>("Driver.Retired", "El conductor está retirado.");
        if (vehiculo.EstaRetirado) return Result.Failure<DriverAssignment>(ErroresDeTransporte.Retirado(vehiculo.Plate));

        var delVehiculo = vehiculo.Id == 0
            ? []
            : await db.DriverAssignments.Where(a => a.VehicleId == vehiculo.Id && (a.To == null || a.To >= desde)).ToListAsync(ct);
        delVehiculo.AddRange(db.DriverAssignments.Local.Where(a => (ReferenceEquals(a.Vehicle, vehiculo) || (vehiculo.Id != 0 && a.VehicleId == vehiculo.Id))
                                                               && (a.To == null || a.To >= desde) && !delVehiculo.Contains(a)));
        if (delVehiculo.Any(a => (a.DriverId == conductor.Id && conductor.Id != 0) || ReferenceEquals(a.Driver, conductor)))
            return Result.Failure<DriverAssignment>("Driver.AlreadyAssigned", $"El conductor ya está asignado al vehículo {vehiculo.Plate} en esas fechas.");
        if (titular && delVehiculo.Any(a => a.IsPrimary))
            return Result.Failure<DriverAssignment>("Driver.PrimaryAlreadyExists", $"El vehículo {vehiculo.Plate} ya tiene un conductor titular vigente.");

        var asignacion = new DriverAssignment
        {
            DriverId = conductor.Id, Driver = conductor.Id == 0 ? conductor : null,
            VehicleId = vehiculo.Id, Vehicle = vehiculo.Id == 0 ? vehiculo : null,
            From = desde, IsPrimary = titular, CreatedAt = reloj.UtcNow,
        };
        db.DriverAssignments.Add(asignacion);
        return Result.Success(asignacion);
    }
}

public sealed record DriverCreatedDto(Guid DriverPublicId, Guid PersonPublicId);

/// <summary>Registra como conductor a una persona que ya existe.</summary>
public sealed record RegisterDriverCommand(Guid PersonPublicId, DateOnly? Since = null) : IRequest<Result<DriverCreatedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDriverCommandValidator : AbstractValidator<RegisterDriverCommand>
{
    public RegisterDriverCommandValidator() => RuleFor(x => x.PersonPublicId).NotEqual(Guid.Empty).WithMessage("Indique la persona.");
}

public sealed class RegisterDriverCommandHandler(IApplicationDbContext db, RolDeConductor rol, IDateTimeService reloj)
    : IRequestHandler<RegisterDriverCommand, Result<DriverCreatedDto>>
{
    public async Task<Result<DriverCreatedDto>> Handle(RegisterDriverCommand request, CancellationToken ct)
    {
        var persona = await db.People.FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<DriverCreatedDto>("Driver.PersonNotFound", "La persona no existe.");
        var r = await rol.AsignarAsync(persona, request.Since ?? reloj.HoyLocal, ct);
        if (r.IsFailure) return Result.Failure<DriverCreatedDto>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(new DriverCreatedDto(r.Value.PublicId, persona.PublicId));
    }
}

/// <summary>Registra la persona nueva y su rol de conductor en un paso (contracts/api.md §3, <c>POST /drivers/with-person</c>).</summary>
public sealed record RegisterDriverWithPersonCommand(PersonInput Person, AutorizacionAlCrear? Authorization = null)
    : IRequest<Result<DriverCreatedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDriverWithPersonCommandValidator : AbstractValidator<RegisterDriverWithPersonCommand>
{
    public RegisterDriverWithPersonCommandValidator()
    {
        RuleFor(x => x.Person).NotNull().SetValidator(new PersonInputValidator());
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}

public sealed class RegisterDriverWithPersonCommandHandler(AltaConAutorizacion altas, RolDeConductor rol, IDateTimeService reloj)
    : IRequestHandler<RegisterDriverWithPersonCommand, Result<DriverCreatedDto>>
{
    public async Task<Result<DriverCreatedDto>> Handle(RegisterDriverWithPersonCommand request, CancellationToken ct)
    {
        var alta = await altas.GuardarAsync(request.Person, request.Authorization, persona => rol.AsignarAsync(persona, reloj.HoyLocal, ct), ct);
        return alta.IsSuccess
            ? Result.Success(new DriverCreatedDto(alta.Value.Rol.PublicId, alta.Value.Persona.PublicId))
            : Result.Failure<DriverCreatedDto>(alta.Error);
    }
}

/// <summary>Retira el registro de conductor y termina sus asignaciones vigentes.</summary>
public sealed record RetireDriverCommand(Guid DriverPublicId, DateOnly EffectiveDate) : IRequest<Result>;

public sealed class RetireDriverCommandHandler(IApplicationDbContext db, RolDeConductor rol) : IRequestHandler<RetireDriverCommand, Result>
{
    public async Task<Result> Handle(RetireDriverCommand request, CancellationToken ct)
    {
        var d = await db.Drivers.Include(x => x.Person).FirstOrDefaultAsync(x => x.PublicId == request.DriverPublicId, ct);
        if (d is null) return Result.Failure("Driver.NotFound", "No existe ese conductor.");
        var r = await rol.RetirarAsync(d, request.EffectiveDate, ct);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Asigna un conductor a un vehículo desde una fecha (FR-018).</summary>
public sealed record AssignDriverCommand(Guid DriverPublicId, Guid VehiclePublicId, DateOnly From, bool IsPrimary) : IRequest<Result<Guid>>;

public sealed class AssignDriverCommandValidator : AbstractValidator<AssignDriverCommand>
{
    public AssignDriverCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty).WithMessage("Indique el vehículo.");
        RuleFor(x => x.From).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo.");
    }
}

public sealed class AssignDriverCommandHandler(IApplicationDbContext db, RolDeConductor rol) : IRequestHandler<AssignDriverCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AssignDriverCommand request, CancellationToken ct)
    {
        var d = await db.Drivers.FirstOrDefaultAsync(x => x.PublicId == request.DriverPublicId, ct);
        if (d is null) return Result.Failure<Guid>("Driver.NotFound", "No existe ese conductor.");
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<Guid>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var r = await rol.AsignarVehiculoAsync(d, v, request.From, request.IsPrimary, ct);
        if (r.IsFailure) return Result.Failure<Guid>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(r.Value.PublicId);
    }
}

/// <summary>Termina una asignación con fecha; queda en la historia del vehículo.</summary>
public sealed record EndDriverAssignmentCommand(Guid DriverPublicId, Guid AssignmentPublicId, DateOnly To) : IRequest<Result>;

public sealed class EndDriverAssignmentCommandHandler(IApplicationDbContext db) : IRequestHandler<EndDriverAssignmentCommand, Result>
{
    public async Task<Result> Handle(EndDriverAssignmentCommand request, CancellationToken ct)
    {
        var a = await db.DriverAssignments.Include(x => x.Driver)
            .FirstOrDefaultAsync(x => x.PublicId == request.AssignmentPublicId && x.Driver!.PublicId == request.DriverPublicId, ct);
        if (a is null) return Result.Failure("Driver.AssignmentNotFound", "No existe esa asignación.");
        if (a.To is not null) return Result.Failure("Driver.AssignmentEnded", "La asignación ya terminó.");
        if (request.To < a.From) return Result.Failure("Driver.AssignmentDates", "La asignación no puede terminar antes de empezar.");
        a.To = request.To;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record DriverAssignmentDto(Guid PublicId, Guid VehiclePublicId, string Plate, string InternalNumber, DateOnly From, DateOnly? To, bool IsPrimary, bool IsCurrent);

public sealed record DriverDto(Guid PublicId, Guid PersonPublicId, string IdType, string TaxId, string FullName, DriverStatus Status, DateOnly Since,
    DateOnly? RetiredOn, IReadOnlyList<DriverAssignmentDto> Assignments);

/// <summary>Los conductores con sus asignaciones (las vigentes primero).</summary>
public sealed record ListDriversQuery(string? Q = null, bool IncludeRetired = false) : IRequest<Result<IReadOnlyList<DriverDto>>>;

public sealed class ListDriversQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<ListDriversQuery, Result<IReadOnlyList<DriverDto>>>
{
    public async Task<Result<IReadOnlyList<DriverDto>>> Handle(ListDriversQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var q = db.Drivers.AsNoTracking().Include(d => d.Person).Include(d => d.Assignments).ThenInclude(a => a.Vehicle).AsQueryable();
        if (!request.IncludeRetired) q = q.Where(d => d.Status == DriverStatus.Active);
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var t = request.Q.Trim().ToUpperInvariant();
            q = q.Where(d => d.Person!.TaxId.StartsWith(t) || (d.Person.FirstName + " " + d.Person.LastName).ToUpper().Contains(t));
        }
        var filas = await q.OrderBy(d => d.Person!.LastName).ThenBy(d => d.Person!.FirstName).Take(500).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DriverDto>>(filas.Select(d => new DriverDto(d.PublicId, d.Person!.PublicId, d.Person.IdType, d.Person.TaxId,
                NombreVisible.De(d.Person), d.Status, d.Since, d.RetiredOn,
                d.Assignments.Where(a => !a.IsDeleted).OrderByDescending(a => a.VigenteEn(hoy)).ThenByDescending(a => a.From)
                    .Select(a => new DriverAssignmentDto(a.PublicId, a.Vehicle!.PublicId, a.Vehicle.Plate, a.Vehicle.InternalNumber, a.From, a.To, a.IsPrimary,
                        a.VigenteEn(hoy))).ToList()))
            .ToList());
    }
}

/// <summary>La parte de conductores de la ficha del vehículo: vigentes e historia.</summary>
public sealed class ConductoresEnLaFicha(IApplicationDbContext db, IDateTimeService reloj) : IParteDeLaFichaDelVehiculo
{
    public async Task<VehicleDetailDto> CompletarAsync(VehicleDetailDto ficha, Vehicle vehiculo, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var asignaciones = await db.DriverAssignments.AsNoTracking().Where(a => a.VehicleId == vehiculo.Id)
            .Include(a => a.Driver).ThenInclude(d => d!.Person).ToListAsync(ct);
        return ficha with
        {
            Drivers = asignaciones.OrderByDescending(a => a.VigenteEn(hoy)).ThenByDescending(a => a.From)
                .Select(a => new VehicleDriverDto(a.Driver!.PublicId, a.PublicId, a.Driver.Person!.PublicId, a.Driver.Person.TaxId,
                    NombreVisible.De(a.Driver.Person), a.From, a.To, a.IsPrimary, a.VigenteEn(hoy)))
                .ToList(),
        };
    }
}
