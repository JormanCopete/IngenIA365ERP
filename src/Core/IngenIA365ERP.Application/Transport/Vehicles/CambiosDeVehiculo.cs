using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>
/// Los cambios de estado y de propietario de un vehículo con su historia (feature 020, R3, T044, T045, T063; FR-009, FR-010, FR-014). Los usan
/// los comandos de la pantalla y la plantilla <c>transport.changes</c>, con las mismas reglas: fecha efectiva no anterior al último cambio
/// del mismo tipo, motivo obligatorio, retirado terminal, evento en la historia (que sella la clase de vinculación de cada lado en el
/// cambio de propietario). <b>No guarda.</b> Las deudas del propietario saliente se informan antes, en la pantalla; nunca bloquean.
/// </summary>
public sealed class CambiosDeVehiculo(IApplicationDbContext db, IDateTimeService reloj, IActorActual? actor = null)
{
    public async Task<Result> CambiarEstadoAsync(Vehicle v, VehicleStatus nuevo, DateOnly fecha, string motivo, ChangeSource origen, CancellationToken ct)
    {
        if (v.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(v.Plate));
        if (nuevo == VehicleStatus.Retired) return await RetirarAsync(v, fecha, motivo, origen, ct);
        if (v.Status == nuevo) return Result.Failure(ErroresDeTransporte.MismoEstado(nuevo));
        var hacia = await NoAntesDelUltimoAsync(v, VehicleEventKind.StatusChanged, fecha, v.StatusSince, ct);
        if (hacia.IsFailure) return hacia;

        var anterior = v.Status;
        v.CambiarEstado(nuevo, fecha);
        await AnotarAsync(v, VehicleEventKind.StatusChanged, fecha, anterior.ToString(), nuevo.ToString(), null, null, motivo, origen, ct);
        return Result.Success();
    }

    public async Task<Result> RetirarAsync(Vehicle v, DateOnly fecha, string motivo, ChangeSource origen, CancellationToken ct)
    {
        if (v.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(v.Plate));
        var hacia = await NoAntesDelUltimoAsync(v, VehicleEventKind.StatusChanged, fecha, v.StatusSince, ct);
        if (hacia.IsFailure) return hacia;

        var anterior = v.Status;
        v.Retirar(fecha, motivo);
        await AnotarAsync(v, VehicleEventKind.StatusChanged, fecha, anterior.ToString(), VehicleStatus.Retired.ToString(), null, null, motivo, origen, ct);
        return Result.Success();
    }

    public async Task<Result> CambiarPropietarioAsync(Vehicle v, Person nuevo, DateOnly fecha, string motivo, ChangeSource origen, CancellationToken ct)
    {
        if (v.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(v.Plate));
        if (nuevo.Id == v.OwnerPersonId) return Result.Failure(ErroresDeTransporte.MismoPropietario);
        var hacia = await NoAntesDelUltimoAsync(v, VehicleEventKind.OwnerChanged, fecha, v.OwnerSince, ct);
        if (hacia.IsFailure) return hacia;

        var saliente = await db.People.AsNoTracking().FirstAsync(p => p.Id == v.OwnerPersonId, ct);
        var claseSaliente = await AfiliacionVigente.DeAsync(db, saliente, fecha, ct);
        var claseEntrante = await AfiliacionVigente.DeAsync(db, nuevo, fecha, ct);
        v.CambiarPropietario(nuevo.Id, fecha);
        await AnotarAsync(v, VehicleEventKind.OwnerChanged, fecha, saliente.PublicId.ToString(), nuevo.PublicId.ToString(), claseSaliente, claseEntrante,
            motivo, origen, ct);
        return Result.Success();
    }

    /// <summary>Una fecha efectiva no anterior al último cambio del mismo tipo (ni a la fecha desde la que rige el valor actual).</summary>
    private async Task<Result> NoAntesDelUltimoAsync(Vehicle v, VehicleEventKind tipo, DateOnly fecha, DateOnly vigenteDesde, CancellationToken ct)
    {
        var ultimo = v.Id == 0
            ? (DateOnly?)null
            : await db.VehicleEvents.AsNoTracking().Where(e => e.VehicleId == v.Id && e.Kind == tipo)
                .OrderByDescending(e => e.EffectiveDate).Select(e => (DateOnly?)e.EffectiveDate).FirstOrDefaultAsync(ct);
        var referencia = ultimo is { } u && u > vigenteDesde ? u : vigenteDesde;
        return fecha < referencia ? Result.Failure(ErroresDeTransporte.FechaAnteriorAlUltimoCambio(fecha, referencia)) : Result.Success();
    }

    private async Task AnotarAsync(Vehicle v, VehicleEventKind tipo, DateOnly fecha, string desde, string hacia,
        AffiliationClass? claseDesde, AffiliationClass? claseHacia, string motivo, ChangeSource origen, CancellationToken ct)
    {
        var nombre = actor is null ? null : (await actor.ObtenerAsync(ct)).Name;
        db.VehicleEvents.Add(new VehicleEvent
        {
            VehicleId = v.Id,
            Vehicle = v.Id == 0 ? v : null,
            Kind = tipo,
            EffectiveDate = fecha,
            FromValue = desde,
            ToValue = hacia,
            FromAffiliation = claseDesde,
            ToAffiliation = claseHacia,
            Reason = motivo.Trim(),
            Source = origen,
            ChangedByName = nombre is { Length: > 150 } ? nombre[..150] : nombre,
            CreatedAt = reloj.UtcNow,
        });
    }
}
