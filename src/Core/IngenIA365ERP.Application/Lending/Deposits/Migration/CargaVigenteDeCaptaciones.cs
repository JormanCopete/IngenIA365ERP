using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// El lector de las cargas de aportes y captaciones <c>Applied</c> antes de activar (feature 014, US9, T155; contrato-de-cuentas §3.7 y §5,
/// <c>MigratedNotActivated</c>): de unas personas, las que son titulares de una cuenta de una carga aplicada cuya naturaleza todavía no está activada. Después
/// de activar la naturaleza nadie lee su preparación. Sólo lee las tandas vivas; en bloque (una consulta por parte). (nuevo)
/// </summary>
public static class CargaVigenteDeCaptaciones
{
    public static async Task<IReadOnlySet<int>> PersonasAsync(IApplicationDbContext db, IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (personas.Count == 0) return new HashSet<int>();
        var fechas = await Activation.ActivacionesDeCaptacion.LeerAsync(db, ct);
        var naturalezas = new[] { LineNature.Deposit, LineNature.Contribution }.Where(n => !fechas.Activada(n)).ToList();
        if (naturalezas.Count == 0) return new HashSet<int>();
        var cargas = await db.MigrationLoads.AsNoTracking()
            .Where(l => naturalezas.Contains(l.Nature) && l.Status == MigrationLoadStatus.Applied && !l.IsDeleted).Select(l => l.Id).ToListAsync(ct);
        if (cargas.Count == 0) return new HashSet<int>();
        var ids = personas.Distinct().ToList();
        var tandas = db.MigrationLoadParts.AsNoTracking()
            .Where(p => cargas.Contains(p.MigrationLoadId) && p.Status == MigrationPartStatus.Imported && !p.IsDeleted);
        var deAhorro = tandas.Where(p => p.PartKey == P.MemberAccounts).Select(p => p.Id);
        var deAportes = tandas.Where(p => p.PartKey == P.ContributionAccounts).Select(p => p.Id);
        var titulares = await db.MigrationAccountRows.AsNoTracking().Where(r => deAhorro.Contains(r.MigrationLoadPartId) && ids.Contains(r.PersonId))
            .Select(r => r.PersonId).ToListAsync(ct);
        var asociados = await db.MigrationContributionAccountRows.AsNoTracking().Where(r => deAportes.Contains(r.MigrationLoadPartId) && ids.Contains(r.PersonId))
            .Select(r => r.PersonId).ToListAsync(ct);
        return titulares.Concat(asociados).ToHashSet();
    }
}
