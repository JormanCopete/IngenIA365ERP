using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// Una línea de la naturaleza de la carga con la versión vigente al corte (feature 014, US9): la de captación (con su clase y lo que admite) o la de
/// aportes. Sin versión al corte, la línea no recibe cuentas migradas. (nuevo)
/// </summary>
public sealed record LineaAlCorte(int Id, string Code, LineNature Nature, DepositLineVersion? DeCaptacion, ContributionLineVersion? DeAportes)
{
    public bool TieneVersion => DeCaptacion is not null || DeAportes is not null;

    public DepositClass? Clase => DeCaptacion?.DepositClass;

    public string Prefijo => Code + "-";
}

/// <summary>
/// Las líneas de una naturaleza con su versión vigente al corte, en bloque (dos lecturas): las usan la importación de las partes, la validación entre
/// partes, el cuadre y la activación. Sólo lee. (nuevo)
/// </summary>
public static class LineasDeLaCarga
{
    public static async Task<IReadOnlyDictionary<string, LineaAlCorte>> LeerAsync(IApplicationDbContext db, LineNature naturaleza, DateOnly corte,
        IReadOnlyCollection<string>? codigos, CancellationToken ct)
    {
        var lineas = await db.CreditLines.AsNoTracking()
            .Where(l => l.Nature == naturaleza && !l.IsDeleted && (codigos == null || codigos.Contains(l.Code)))
            .Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var ids = lineas.Select(l => l.Id).ToList();
        var resultado = new Dictionary<string, LineaAlCorte>(StringComparer.OrdinalIgnoreCase);
        if (naturaleza == LineNature.Deposit)
        {
            var versiones = (await db.DepositLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct))
                .ToLookup(v => v.CreditLineId);
            foreach (var l in lineas)
                resultado[l.Code] = new LineaAlCorte(l.Id, l.Code, naturaleza, VigenciaBitemporal.VigenteDeCartera(versiones[l.Id], corte, DateTime.MaxValue), null);
        }
        else
        {
            var versiones = (await db.ContributionLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct))
                .ToLookup(v => v.CreditLineId);
            foreach (var l in lineas)
                resultado[l.Code] = new LineaAlCorte(l.Id, l.Code, naturaleza, null, VigenciaBitemporal.VigenteDeCartera(versiones[l.Id], corte, DateTime.MaxValue));
        }
        return resultado;
    }
}
