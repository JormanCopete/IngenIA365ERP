using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

// Feature 020 (US2; R3, R4; T042–T047; contracts/api.md §3): los comandos del vehículo. El estado y el propietario cambian sólo por sus
// comandos, con historia; la edición no los lleva en su contrato (FR-011). Un choque de dos usuarios en el índice único se traduce al
// mismo error que habrían visto un segundo después, nombrando al vehículo que ganó.

/// <summary>Las reglas de la ficha que repite la pantalla (Principio VIII).</summary>
public sealed class VehicleInputValidator : AbstractValidator<VehicleInput>
{
    public VehicleInputValidator()
    {
        RuleFor(x => x.Plate).Must(Placa.EsValida).WithMessage("La placa tiene de 5 a 10 letras y números.");
        RuleFor(x => x.InternalNumber).NotEmpty().WithMessage("Indique el número interno.").MaximumLength(10)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("El número interno es alfanumérico, sin espacios.");
        RuleFor(x => x.RadioCode).MaximumLength(20).Matches("^[A-Za-z0-9-]*$").WithMessage("El código radio-móvil es alfanumérico, sin espacios.");
        RuleFor(x => x.Line).MaximumLength(60);
        RuleFor(x => x.Color).MaximumLength(40);
        RuleFor(x => x.ModelYear).InclusiveBetween((short)1950, (short)(DateTime.Today.Year + 1)).When(x => x.ModelYear is not null)
            .WithMessage("El modelo es un año entre 1950 y el próximo.");
        RuleFor(x => x.EngineDisplacementCc).InclusiveBetween(1, 100_000).When(x => x.EngineDisplacementCc is not null);
        RuleFor(x => x.PassengerCapacity).InclusiveBetween((short)1, (short)200).When(x => x.PassengerCapacity is not null);
        RuleFor(x => x.EngineNumber).MaximumLength(40);
        RuleFor(x => x.ChassisNumber).MaximumLength(40);
        RuleFor(x => x.Vin).MaximumLength(40);
        RuleFor(x => x.TransitLicenseNumber).MaximumLength(30);
        RuleFor(x => x.AffiliationContractNumber).MaximumLength(30);
        RuleFor(x => x.OperationCardNumber).MaximumLength(30);
        RuleFor(x => x.AffiliationContractTo).GreaterThanOrEqualTo(x => x.AffiliationContractFrom)
            .When(x => x.AffiliationContractFrom is not null && x.AffiliationContractTo is not null)
            .WithMessage("La vigencia del contrato termina después de empezar.");
        RuleFor(x => x.MinDailyRolling).GreaterThanOrEqualTo(0).When(x => x.MinDailyRolling is not null);
        RuleFor(x => x.MinMonthlyRolling).GreaterThanOrEqualTo(0).When(x => x.MinMonthlyRolling is not null);
    }
}

/// <summary>Registra un vehículo de un propietario que ya existe (FR-005, FR-013).</summary>
public sealed record CreateVehicleCommand(VehicleInput Vehicle, Guid OwnerPersonPublicId) : IRequest<Result<VehicleCreatedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Vehicle).NotNull().SetValidator(new VehicleInputValidator());
        RuleFor(x => x.OwnerPersonPublicId).NotEqual(Guid.Empty).WithMessage("Indique el propietario.");
    }
}

public sealed class CreateVehicleCommandHandler(IApplicationDbContext db, VehicleRegistrar registrar)
    : IRequestHandler<CreateVehicleCommand, Result<VehicleCreatedDto>>
{
    public async Task<Result<VehicleCreatedDto>> Handle(CreateVehicleCommand request, CancellationToken ct)
    {
        var propietario = await db.People.FirstOrDefaultAsync(p => p.PublicId == request.OwnerPersonPublicId, ct);
        if (propietario is null) return Result.Failure<VehicleCreatedDto>(ErroresDeTransporte.PropietarioNoEncontrado(request.OwnerPersonPublicId));

        var alta = await registrar.RegistrarAsync(request.Vehicle, propietario, ChangeSource.Screen, ct);
        if (alta.IsFailure) return Result.Failure<VehicleCreatedDto>(alta.Error);
        var choque = await ChoqueDeVehiculo.GuardarAsync(db, request.Vehicle, ct);
        return choque ?? Result.Success(new VehicleCreatedDto(alta.Value.PublicId, propietario.PublicId));
    }
}

/// <summary>
/// Registra el vehículo y su propietario nuevo en un paso (contracts/api.md §3, <c>POST /vehicles/with-person</c>): la persona sale por el
/// alta única de personas (<c>AltaConAutorizacion</c>, el mismo camino que <c>PersonaDialog</c>), un comando y un guardado.
/// </summary>
public sealed record RegisterVehicleWithPersonCommand(PersonInput Person, VehicleInput Vehicle, AutorizacionAlCrear? Authorization = null)
    : IRequest<Result<VehicleCreatedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterVehicleWithPersonCommandValidator : AbstractValidator<RegisterVehicleWithPersonCommand>
{
    public RegisterVehicleWithPersonCommandValidator()
    {
        RuleFor(x => x.Person).NotNull().WithMessage("Faltan los datos de la persona.").SetValidator(new PersonInputValidator());
        RuleFor(x => x.Vehicle).NotNull().SetValidator(new VehicleInputValidator());
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}

public sealed class RegisterVehicleWithPersonCommandHandler(AltaConAutorizacion altas, VehicleRegistrar registrar)
    : IRequestHandler<RegisterVehicleWithPersonCommand, Result<VehicleCreatedDto>>
{
    public async Task<Result<VehicleCreatedDto>> Handle(RegisterVehicleWithPersonCommand request, CancellationToken ct)
    {
        var alta = await altas.GuardarAsync(request.Person, request.Authorization,
            persona => registrar.RegistrarAsync(request.Vehicle, persona, ChangeSource.Screen, ct), ct);
        return alta.IsSuccess
            ? Result.Success(new VehicleCreatedDto(alta.Value.Rol.PublicId, alta.Value.Persona.PublicId))
            : Result.Failure<VehicleCreatedDto>(alta.Error);
    }
}

/// <summary>Corrige la ficha de un vehículo vivo (FR-011): sin estado ni propietario.</summary>
public sealed record UpdateVehicleCommand(Guid VehiclePublicId, VehicleInput Vehicle) : IRequest<Result>;

public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Vehicle).NotNull().SetValidator(new VehicleInputValidator());
    }
}

public sealed class UpdateVehicleCommandHandler(IApplicationDbContext db, VehicleRegistrar registrar) : IRequestHandler<UpdateVehicleCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var r = await registrar.ActualizarAsync(v, request.Vehicle, ct);
        if (r.IsFailure) return r;
        var choque = await ChoqueDeVehiculo.GuardarAsync(db, request.Vehicle, ct);
        return choque is null ? Result.Success() : Result.Failure(choque.Error);
    }
}

/// <summary>Activa o inactiva un vehículo con fecha y motivo (FR-009).</summary>
public sealed record ChangeVehicleStatusCommand(Guid VehiclePublicId, VehicleStatus Status, DateOnly EffectiveDate, string Reason) : IRequest<Result>, IConMotivo;

public sealed class ChangeVehicleStatusCommandValidator : ValidadorConMotivo<ChangeVehicleStatusCommand>
{
    public ChangeVehicleStatusCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Status).Must(s => s is VehicleStatus.Active or VehicleStatus.Inactive)
            .WithMessage("Se activa o se inactiva; retirar es otra operación.");
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha efectiva.");
    }
}

public sealed class ChangeVehicleStatusCommandHandler(IApplicationDbContext db, CambiosDeVehiculo cambios) : IRequestHandler<ChangeVehicleStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeVehicleStatusCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var r = await cambios.CambiarEstadoAsync(v, request.Status, request.EffectiveDate, request.Reason, ChangeSource.Screen, ct);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Retira (desvincula) un vehículo con fecha y motivo (FR-009, FR-010): libera placa, número interno y radio-móvil.</summary>
public sealed record RetireVehicleCommand(Guid VehiclePublicId, DateOnly EffectiveDate, string Reason) : IRequest<Result>, IConMotivo;

public sealed class RetireVehicleCommandValidator : ValidadorConMotivo<RetireVehicleCommand>
{
    public RetireVehicleCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha del retiro.");
    }
}

public sealed class RetireVehicleCommandHandler(IApplicationDbContext db, CambiosDeVehiculo cambios) : IRequestHandler<RetireVehicleCommand, Result>
{
    public async Task<Result> Handle(RetireVehicleCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var r = await cambios.RetirarAsync(v, request.EffectiveDate, request.Reason, ChangeSource.Screen, ct);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Cambia el propietario con fecha efectiva y motivo (FR-014). Las deudas del saliente se informan antes; no bloquean.</summary>
public sealed record ChangeVehicleOwnerCommand(Guid VehiclePublicId, Guid NewOwnerPersonPublicId, DateOnly EffectiveDate, string Reason)
    : IRequest<Result>, IConMotivo;

public sealed class ChangeVehicleOwnerCommandValidator : ValidadorConMotivo<ChangeVehicleOwnerCommand>
{
    public ChangeVehicleOwnerCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.NewOwnerPersonPublicId).NotEqual(Guid.Empty).WithMessage("Indique el nuevo propietario.");
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha efectiva.");
    }
}

public sealed class ChangeVehicleOwnerCommandHandler(IApplicationDbContext db, CambiosDeVehiculo cambios) : IRequestHandler<ChangeVehicleOwnerCommand, Result>
{
    public async Task<Result> Handle(ChangeVehicleOwnerCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var nuevo = await db.People.FirstOrDefaultAsync(p => p.PublicId == request.NewOwnerPersonPublicId, ct);
        if (nuevo is null) return Result.Failure(ErroresDeTransporte.PropietarioNoEncontrado(request.NewOwnerPersonPublicId));
        var r = await cambios.CambiarPropietarioAsync(v, nuevo, request.EffectiveDate, request.Reason, ChangeSource.Screen, ct);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Reemplaza la lista de equipos del vehículo (FR-008).</summary>
public sealed record SetVehicleEquipmentCommand(Guid VehiclePublicId, IReadOnlyList<EquipmentInput> Equipment) : IRequest<Result>;

public sealed class SetVehicleEquipmentCommandValidator : AbstractValidator<SetVehicleEquipmentCommand>
{
    public SetVehicleEquipmentCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Equipment).NotNull();
        RuleForEach(x => x.Equipment).ChildRules(e =>
        {
            e.RuleFor(x => x.EquipmentTypePublicId).NotEqual(Guid.Empty).WithMessage("Indique el tipo de equipo.");
            e.RuleFor(x => x.Code).MaximumLength(40);
            e.RuleFor(x => x.Status).IsInEnum();
        });
        RuleFor(x => x.Equipment).Must(l => l is null || l.Where(e => e.Status == EquipmentStatus.Active)
                .GroupBy(e => e.EquipmentTypePublicId).All(g => g.Count() == 1))
            .WithMessage("Un vehículo tiene a lo sumo un equipo activo de cada tipo.");
    }
}

public sealed class SetVehicleEquipmentCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<SetVehicleEquipmentCommand, Result>
{
    public async Task<Result> Handle(SetVehicleEquipmentCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        if (v.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(v.Plate));

        var tipos = request.Equipment.Select(e => e.EquipmentTypePublicId).Distinct().ToList();
        var catalogo = await db.TransportCatalogItems.AsNoTracking().Where(c => tipos.Contains(c.PublicId))
            .Select(c => new { c.PublicId, c.Id, c.Kind }).ToListAsync(ct);
        foreach (var t in tipos)
        {
            var item = catalogo.FirstOrDefault(c => c.PublicId == t);
            if (item is null) return Result.Failure(ErroresDeTransporte.CatalogoNoEncontrado("Tipo de equipo"));
            if (item.Kind != TransportCatalogKind.EquipmentType) return Result.Failure(ErroresDeTransporte.CatalogoDeOtroTipo("Tipo de equipo", TransportCatalogKind.EquipmentType));
        }

        var ahora = reloj.UtcNow;
        foreach (var viejo in v.Equipment.Where(e => !e.IsDeleted).ToList())
        {
            viejo.IsDeleted = true;
            viejo.DeletedAt = ahora;
        }
        foreach (var e in request.Equipment)
        {
            db.VehicleEquipment.Add(new VehicleEquipment
            {
                VehicleId = v.Id,
                EquipmentTypeId = catalogo.First(c => c.PublicId == e.EquipmentTypePublicId).Id,
                Code = string.IsNullOrWhiteSpace(e.Code) ? null : e.Code.Trim(),
                Status = e.Status,
                CreatedAt = ahora,
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Guarda y traduce el choque de dos usuarios en los índices únicos de vehículos al mismo error que habrían visto un segundo después.
/// </summary>
internal static class ChoqueDeVehiculo
{
    public static async Task<Result<VehicleCreatedDto>?> GuardarAsync(IApplicationDbContext db, VehicleInput datos, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (EsChoque(ex))
        {
            db.DescartarCambios();
            var placa = Placa.Normalizar(datos.Plate);
            var interno = datos.InternalNumber.Trim().ToUpperInvariant();
            var radio = string.IsNullOrWhiteSpace(datos.RadioCode) ? null : datos.RadioCode.Trim().ToUpperInvariant();
            var vivos = db.Vehicles.AsNoTracking().Where(x => x.Status != VehicleStatus.Retired);
            if (await vivos.Where(x => x.Plate == placa).Select(x => new { x.PublicId, x.InternalNumber }).FirstOrDefaultAsync(ct) is { } p)
                return Result.Failure<VehicleCreatedDto>(ErroresDeTransporte.PlacaDuplicada(placa, p.PublicId, p.InternalNumber));
            if (await vivos.Where(x => x.InternalNumber == interno).Select(x => new { x.PublicId, x.Plate }).FirstOrDefaultAsync(ct) is { } i)
                return Result.Failure<VehicleCreatedDto>(ErroresDeTransporte.NumeroInternoDuplicado(interno, i.PublicId, i.Plate));
            if (radio is not null && await vivos.Where(x => x.RadioCode == radio).Select(x => new { x.PublicId, x.Plate }).FirstOrDefaultAsync(ct) is { } r)
                return Result.Failure<VehicleCreatedDto>(ErroresDeTransporte.RadioMovilDuplicado(radio, r.PublicId, r.Plate));
            throw;
        }
    }

    private static bool EsChoque(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e.Message.Contains("UK_TRN_Vehicles_", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
