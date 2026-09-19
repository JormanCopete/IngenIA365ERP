using IngenIA365ERP.Application.Accounting.Setup;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Pone al día un catálogo oficial ya sembrado cuando el archivo trae otra <c>version</c>: las
/// entradas se corrigen en su sitio (nombre, nivel, naturaleza, rubro, padre), las que faltan se
/// insertan —o se reviven si estaban retiradas— y las que sobran se retiran. El catálogo conserva
/// su Id (la configuración contable lo referencia) y pierde la validación del contador, que tiene
/// que volver a mirarlo.
///
/// <para>
/// Nació el 2026-09-18, cuando el PUC solidario dejó de ser una transcripción del equipo y pasó a
/// ser el CUIF oficial: la regla de «un catálogo sembrado no se toca» servía para no pisar lo que
/// el contador ya validó, no para dejar en cada cooperativa un catálogo con códigos que no existen.
/// Una resolución nueva sigue siendo otro archivo con otro código (docs/manual/semillas-json.md).
/// </para>
///
/// <para>
/// Si una empresa ya copió ese catálogo a su plan y el plan sigue intacto —sin cuentas propias ni
/// movimientos—, se vuelve a copiar (el anterior queda retirado, Principio VII). Con cuentas
/// propias o movimientos el plan no se toca y queda dicho en el log: es la misma regla que
/// bloquea cambiar de catálogo desde la configuración.
/// </para>
/// </summary>
public static class SincronizacionDeCatalogo
{
    public sealed record Resultado(int Corregidas, int Insertadas, int Retiradas, bool PlanRefrescado, string? PlanNoRefrescadoPorque);

    public static async Task<Resultado?> SincronizarAsync(
        IApplicationDbContext db, AccountCatalog catalogo, CatalogoJson json, string quien, DateTime ahora, ILogger logger, CancellationToken ct)
    {
        if (catalogo.Source != CatalogSource.Official || string.Equals(catalogo.Version, json.Version, StringComparison.Ordinal)) return null;

        var nuevas = json.Entradas();
        var actuales = await db.AccountCatalogEntries.IgnoreQueryFilters()
            .Where(e => e.CatalogId == catalogo.Id)
            .ToDictionaryAsync(e => e.Code, StringComparer.Ordinal, ct);

        int corregidas = 0, insertadas = 0, retiradas = 0;
        var vigentes = new List<AccountCatalogEntry>(nuevas.Count);
        foreach (var n in nuevas)
        {
            if (actuales.TryGetValue(n.Code, out var e))
            {
                var cambia = e.IsDeleted || e.Name != n.Name || e.Level != n.Level || e.Nature != n.Nature || e.NiifItemCode != n.NiifItemCode || e.ParentCode != n.ParentCode;
                if (cambia)
                {
                    e.Name = n.Name; e.Level = n.Level; e.Nature = n.Nature; e.NiifItemCode = n.NiifItemCode; e.ParentCode = n.ParentCode;
                    e.IsDeleted = false; e.DeletedAt = null; e.DeletedBy = null;
                    e.UpdatedAt = ahora; e.UpdatedBy = quien;
                    corregidas++;
                }
                vigentes.Add(e);
            }
            else
            {
                e = new AccountCatalogEntry
                {
                    CatalogId = catalogo.Id, Code = n.Code, Name = n.Name, Level = n.Level, Nature = n.Nature,
                    NiifItemCode = n.NiifItemCode, ParentCode = n.ParentCode, CreatedAt = ahora, CreatedBy = quien,
                };
                db.AccountCatalogEntries.Add(e);
                vigentes.Add(e);
                insertadas++;
            }
        }
        var codigosNuevos = nuevas.Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var sobrante in actuales.Values.Where(e => !e.IsDeleted && !codigosNuevos.Contains(e.Code)))
        {
            sobrante.IsDeleted = true; sobrante.DeletedAt = ahora; sobrante.DeletedBy = quien;
            retiradas++;
        }

        var versionAnterior = catalogo.Version;
        catalogo.Name = json.Name;
        catalogo.Version = json.Version;
        catalogo.EntryCount = nuevas.Count;
        catalogo.ValidatedAt = null;
        catalogo.ValidatedBy = null;
        catalogo.UpdatedAt = ahora;
        catalogo.UpdatedBy = quien;

        // El plan de la empresa, si lo copió de este catálogo y sigue intacto.
        var (refrescado, porque) = await RefrescarPlanAsync(db, catalogo, vigentes, quien, ahora, ct);
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Catálogo {Codigo}: {VersionAnterior} → {Version}. Entradas corregidas {Corregidas}, insertadas {Insertadas}, retiradas {Retiradas}. Plan de cuentas {Plan}. El contador tiene que validarlo de nuevo.",
            catalogo.Code, versionAnterior, catalogo.Version, corregidas, insertadas, retiradas,
            refrescado ? "vuelto a copiar" : porque is null ? "no iniciado con este catálogo" : "NO refrescado: " + porque);
        return new Resultado(corregidas, insertadas, retiradas, refrescado, porque);
    }

    private static async Task<(bool Refrescado, string? Porque)> RefrescarPlanAsync(
        IApplicationDbContext db, AccountCatalog catalogo, IReadOnlyList<AccountCatalogEntry> entradas, string quien, DateTime ahora, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.FirstOrDefaultAsync(s => !s.IsDeleted && s.CatalogId == catalogo.Id, ct);
        if (setup is null) return (false, null);

        var propias = await db.ChartOfAccounts.CountAsync(a => !a.IsDeleted && a.Origin == AccountOrigin.Company, ct);
        var conMovimiento = await db.ChartOfAccounts.CountAsync(a => !a.IsDeleted && a.FirstMovementAt != null, ct);
        if (propias > 0 || conMovimiento > 0)
            return (false, $"{propias} cuenta(s) propia(s) y {conMovimiento} con movimientos; se conserva el plan copiado de la versión anterior");

        foreach (var cuenta in await db.ChartOfAccounts.Where(a => !a.IsDeleted).ToListAsync(ct))
        {
            cuenta.IsDeleted = true; cuenta.DeletedAt = ahora; cuenta.DeletedBy = quien;
        }
        foreach (var c in CopiaDelCatalogo.Copiar(entradas, new Dictionary<string, ChartOfAccount>(), quien, ahora))
            db.ChartOfAccounts.Add(c);
        setup.ResultAccountId = null;
        setup.UpdatedAt = ahora; setup.UpdatedBy = quien;
        return (true, null);
    }
}
