using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.People;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>
/// La persona es propietaria de un vehículo vivo (no retirado) o conductora vigente de uno (feature 020, R5; T051, T089; FR-017).
/// </summary>
public sealed class UsoDePersonaEnTransporte(IApplicationDbContext db) : IUsoDePersonaEnTransporte
{
    public async Task<string?> PlacaQueLoImpideAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var propia = await db.Vehicles.AsNoTracking()
            .Where(v => v.OwnerPersonId == personId && v.Status != VehicleStatus.Retired)
            .Select(v => v.Plate).FirstOrDefaultAsync(ct);
        if (propia is not null) return propia;

        return await db.DriverAssignments.AsNoTracking()
            .Where(a => a.Driver!.PersonId == personId && a.From <= fecha && (a.To == null || a.To >= fecha)
                        && a.Vehicle!.Status != VehicleStatus.Retired)
            .Select(a => a.Vehicle!.Plate).FirstOrDefaultAsync(ct);
    }
}
