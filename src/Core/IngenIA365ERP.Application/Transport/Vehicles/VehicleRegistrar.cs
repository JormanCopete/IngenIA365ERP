using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>
/// El alta y la edición unitarias de la ficha de un vehículo (feature 020, R3, T041; FR-005 a FR-008, FR-011). Lo usan la pantalla, la
/// API y la plantilla <c>transport.vehicles</c>: no hay un camino masivo con reglas propias (FR-027; lo vigila
/// <c>LasPlantillasDeTransporteUsanElAltaUnitaria</c>). Normaliza la placa, valida la unicidad entre vivos nombrando al vehículo que tiene
/// el dato, el tipo de cada catálogo y las fechas, y escribe el evento <c>Registered</c>. <b>No guarda</b> y no toca estado ni propietario
/// en la edición.
/// </summary>
public sealed class VehicleRegistrar(IApplicationDbContext db, IDateTimeService reloj, IActorActual? actor = null)
{
    /// <summary>Un vehículo nuevo de un propietario ya guardado.</summary>
    public async Task<Result<Vehicle>> RegistrarAsync(VehicleInput datos, Person propietario, ChangeSource origen, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(propietario);
        var ingreso = datos.JoinedOn ?? reloj.HoyLocal;
        var vehiculo = propietario.Id > 0 ? Vehicle.Registrar(propietario.Id, ingreso) : Vehicle.Registrar(propietario, ingreso);
        var aplicado = await AplicarAsync(vehiculo, datos, ct);
        if (aplicado.IsFailure) return Result.Failure<Vehicle>(aplicado.Error);

        db.Vehicles.Add(vehiculo);
        var nombre = actor is null ? null : (await actor.ObtenerAsync(ct)).Name;
        db.VehicleEvents.Add(new VehicleEvent
        {
            Vehicle = vehiculo,
            Kind = VehicleEventKind.Registered,
            EffectiveDate = ingreso,
            ToValue = VehicleStatus.Active.ToString(),
            ToAffiliation = await AfiliacionVigente.DeAsync(db, propietario, ingreso, ct),
            Source = origen,
            ChangedByName = Recortar(nombre, 150),
            CreatedAt = reloj.UtcNow,
        });
        return Result.Success(vehiculo);
    }

    /// <summary>Corrige la ficha de un vehículo vivo (FR-011): no cambia estado ni propietario.</summary>
    public async Task<Result> ActualizarAsync(Vehicle vehiculo, VehicleInput datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);
        if (vehiculo.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(vehiculo.Plate));
        return await AplicarAsync(vehiculo, datos, ct);
    }

    /// <summary>La ficha de un vehículo como <see cref="VehicleInput"/>: el punto de partida de la plantilla en «actualizar».</summary>
    public async Task<VehicleInput> ComoEntradaAsync(Vehicle v, CancellationToken ct)
    {
        var ids = new[] { v.BrandId, v.VehicleClassId, v.BodyTypeId, v.FuelId, v.TransitAuthorityId, v.ServiceClassId, v.ModalityId, v.RollingGroupId }
            .Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        var catalogos = await db.TransportCatalogItems.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        Guid? P(int? id) => id is { } x && catalogos.TryGetValue(x, out var p) ? p : null;
        var sucursal = v.BranchId is { } b ? await db.Branches.AsNoTracking().Where(x => x.Id == b).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        return new VehicleInput
        {
            Plate = v.Plate, InternalNumber = v.InternalNumber, RadioCode = v.RadioCode,
            BrandPublicId = P(v.BrandId), Line = v.Line, ModelYear = v.ModelYear, VehicleClassPublicId = P(v.VehicleClassId),
            BodyTypePublicId = P(v.BodyTypeId), Color = v.Color, EngineDisplacementCc = v.EngineDisplacementCc, FuelPublicId = P(v.FuelId),
            PassengerCapacity = v.PassengerCapacity, EngineNumber = v.EngineNumber, ChassisNumber = v.ChassisNumber, Vin = v.Vin,
            TransitLicenseNumber = v.TransitLicenseNumber, TransitAuthorityPublicId = P(v.TransitAuthorityId), RegistrationDate = v.RegistrationDate,
            ServiceClassPublicId = P(v.ServiceClassId), ModalityPublicId = P(v.ModalityId), JoinedOn = v.JoinedOn,
            AffiliationContractNumber = v.AffiliationContractNumber, AffiliationContractFrom = v.AffiliationContractFrom,
            AffiliationContractTo = v.AffiliationContractTo, OperationCardNumber = v.OperationCardNumber, RollingGroupPublicId = P(v.RollingGroupId),
            MinDailyRolling = v.MinDailyRolling, MinMonthlyRolling = v.MinMonthlyRolling, BranchPublicId = sucursal,
        };
    }

    private async Task<Result> AplicarAsync(Vehicle v, VehicleInput d, CancellationToken ct)
    {
        if (!Placa.EsValida(d.Plate)) return Result.Failure(ErroresDeTransporte.PlacaInvalida(d.Plate));
        var placa = Placa.Normalizar(d.Plate);
        var interno = (d.InternalNumber ?? string.Empty).Trim().ToUpperInvariant();
        var radio = string.IsNullOrWhiteSpace(d.RadioCode) ? null : d.RadioCode.Trim().ToUpperInvariant();

        var unico = await UnicidadAsync(v, placa, interno, radio, ct);
        if (unico.IsFailure) return unico;

        var catalogos = await CatalogosAsync(d, ct);
        if (catalogos.IsFailure) return catalogos;
        var c = catalogos.Value;

        int? sucursal = null;
        if (d.BranchPublicId is { } s)
        {
            sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == s).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (sucursal is null) return Result.Failure("Branch.NotFound", $"No existe la oficina {s:D}.");
        }

        v.Plate = placa;
        v.InternalNumber = interno;
        v.RadioCode = radio;
        v.BrandId = c[TransportCatalogKind.Brand];
        v.Line = Limpio(d.Line);
        v.ModelYear = d.ModelYear;
        v.VehicleClassId = c[TransportCatalogKind.VehicleClass];
        v.BodyTypeId = c[TransportCatalogKind.BodyType];
        v.Color = Limpio(d.Color);
        v.EngineDisplacementCc = d.EngineDisplacementCc;
        v.FuelId = c[TransportCatalogKind.Fuel];
        v.PassengerCapacity = d.PassengerCapacity;
        v.EngineNumber = Limpio(d.EngineNumber)?.ToUpperInvariant();
        v.ChassisNumber = Limpio(d.ChassisNumber)?.ToUpperInvariant();
        v.Vin = Limpio(d.Vin)?.ToUpperInvariant();
        v.TransitLicenseNumber = Limpio(d.TransitLicenseNumber);
        v.TransitAuthorityId = c[TransportCatalogKind.TransitAuthority];
        v.RegistrationDate = d.RegistrationDate;
        v.ServiceClassId = c[TransportCatalogKind.ServiceClass];
        v.ModalityId = c[TransportCatalogKind.Modality];
        if (d.JoinedOn is { } ingreso) v.JoinedOn = ingreso;
        v.AffiliationContractNumber = Limpio(d.AffiliationContractNumber);
        v.AffiliationContractFrom = d.AffiliationContractFrom;
        v.AffiliationContractTo = d.AffiliationContractTo;
        v.OperationCardNumber = Limpio(d.OperationCardNumber);
        v.RollingGroupId = c[TransportCatalogKind.RollingGroup];
        v.MinDailyRolling = d.MinDailyRolling;
        v.MinMonthlyRolling = d.MinMonthlyRolling;
        v.BranchId = sucursal;
        return Result.Success();
    }

    /// <summary>Placa, número interno y radio-móvil únicos entre vivos, sin contar al propio vehículo.</summary>
    private async Task<Result> UnicidadAsync(Vehicle propio, string placa, string interno, string? radio, CancellationToken ct)
    {
        var otros = db.Vehicles.AsNoTracking().Where(x => x.Status != VehicleStatus.Retired && (propio.Id == 0 || x.Id != propio.Id));

        var conPlaca = await otros.Where(x => x.Plate == placa).Select(x => new { x.PublicId, x.InternalNumber }).FirstOrDefaultAsync(ct)
                       ?? db.Vehicles.Local.Where(x => !ReferenceEquals(x, propio) && x.Status != VehicleStatus.Retired && x.Plate == placa)
                           .Select(x => new { x.PublicId, x.InternalNumber }).FirstOrDefault();
        if (conPlaca is not null) return Result.Failure(ErroresDeTransporte.PlacaDuplicada(placa, conPlaca.PublicId, conPlaca.InternalNumber));

        var conInterno = await otros.Where(x => x.InternalNumber == interno).Select(x => new { x.PublicId, x.Plate }).FirstOrDefaultAsync(ct)
                         ?? db.Vehicles.Local.Where(x => !ReferenceEquals(x, propio) && x.Status != VehicleStatus.Retired && x.InternalNumber == interno)
                             .Select(x => new { x.PublicId, x.Plate }).FirstOrDefault();
        if (conInterno is not null) return Result.Failure(ErroresDeTransporte.NumeroInternoDuplicado(interno, conInterno.PublicId, conInterno.Plate));

        if (radio is not null)
        {
            var conRadio = await otros.Where(x => x.RadioCode == radio).Select(x => new { x.PublicId, x.Plate }).FirstOrDefaultAsync(ct)
                           ?? db.Vehicles.Local.Where(x => !ReferenceEquals(x, propio) && x.Status != VehicleStatus.Retired && x.RadioCode == radio)
                               .Select(x => new { x.PublicId, x.Plate }).FirstOrDefault();
            if (conRadio is not null) return Result.Failure(ErroresDeTransporte.RadioMovilDuplicado(radio, conRadio.PublicId, conRadio.Plate));
        }

        return Result.Success();
    }

    private async Task<Result<Dictionary<TransportCatalogKind, int?>>> CatalogosAsync(VehicleInput d, CancellationToken ct)
    {
        var pedidos = new (TransportCatalogKind Kind, Guid? Id, string Campo)[]
        {
            (TransportCatalogKind.Brand, d.BrandPublicId, "Marca"),
            (TransportCatalogKind.VehicleClass, d.VehicleClassPublicId, "Clase de vehículo"),
            (TransportCatalogKind.BodyType, d.BodyTypePublicId, "Carrocería"),
            (TransportCatalogKind.Fuel, d.FuelPublicId, "Combustible"),
            (TransportCatalogKind.TransitAuthority, d.TransitAuthorityPublicId, "Organismo de tránsito"),
            (TransportCatalogKind.ServiceClass, d.ServiceClassPublicId, "Clase de servicio"),
            (TransportCatalogKind.Modality, d.ModalityPublicId, "Modalidad"),
            (TransportCatalogKind.RollingGroup, d.RollingGroupPublicId, "Grupo de rodamiento"),
        };
        var ids = pedidos.Where(p => p.Id is not null).Select(p => p.Id!.Value).Distinct().ToList();
        var encontrados = ids.Count == 0
            ? []
            : await db.TransportCatalogItems.AsNoTracking().Where(c => ids.Contains(c.PublicId))
                .Select(c => new { c.PublicId, c.Id, c.Kind, c.IsActive }).ToListAsync(ct);

        var resultado = new Dictionary<TransportCatalogKind, int?>();
        foreach (var (kind, id, campo) in pedidos)
        {
            if (id is not { } publico) { resultado[kind] = null; continue; }
            var item = encontrados.FirstOrDefault(e => e.PublicId == publico);
            if (item is null) return Result.Failure<Dictionary<TransportCatalogKind, int?>>(ErroresDeTransporte.CatalogoNoEncontrado(campo));
            if (item.Kind != kind) return Result.Failure<Dictionary<TransportCatalogKind, int?>>(ErroresDeTransporte.CatalogoDeOtroTipo(campo, kind));
            resultado[kind] = item.Id;
        }
        return Result.Success(resultado);
    }

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? Recortar(string? texto, int largo) => texto is null ? null : texto.Length <= largo ? texto : texto[..largo];
}
