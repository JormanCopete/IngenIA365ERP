using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Los dos catálogos oficiales (feature 009, FR-002; <c>puc-comercial.json</c> y
/// <c>puc-solidario.json</c>) en <c>ACC_AccountCatalogs</c> con sus entradas. Idempotente por
/// código de catálogo: un catálogo ya sembrado no se toca mientras el archivo traiga la misma
/// <c>version</c>; si la versión cambia, <see cref="SincronizacionDeCatalogo"/> lo pone al día en
/// su sitio (una resolución nueva sigue siendo otro archivo con otro código,
/// docs/manual/semillas-json.md). Las cuentas de la empresa se copian de aquí al iniciar la
/// contabilidad; después el catálogo sólo sirve para «cuentas nuevas» (FR-006).
/// </summary>
public sealed class AccountCatalogsSeeder : IDataSeeder
{
    public static readonly string[] Recursos = ["puc-comercial.json", "puc-solidario.json"];

    public int Order => 58;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public static IReadOnlyList<CatalogoJson> Catalogos() => Recursos.Select(RecursoJson.Leer<CatalogoJson>).ToList();

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = await db.AccountCatalogs.IgnoreQueryFilters().ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, ct);

        var insertadas = 0;
        var ahora = DateTime.UtcNow;
        foreach (var json in Catalogos())
        {
            if (existentes.TryGetValue(json.Code, out var sembrado))
            {
                var r = await SincronizacionDeCatalogo.SincronizarAsync(db, sembrado, json, SeedContext.ParametricCreatedBy, ahora, context.Logger, ct);
                if (r is not null) insertadas += r.Corregidas + r.Insertadas + r.Retiradas;
                continue;
            }
            var catalogo = json.ComoCatalogo(SeedContext.ParametricCreatedBy);
            db.AccountCatalogs.Add(catalogo);
            insertadas += 1 + catalogo.Entries.Count;
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}

/// <summary>Los rubros NIIF de <c>rubros-niif.json</c> por grupo; idempotente por (grupo, código).</summary>
public sealed class FinancialStatementItemsSeeder : IDataSeeder
{
    public const string Recurso = "rubros-niif.json";

    public int Order => 59;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public static RubrosJson Rubros() => RecursoJson.Leer<RubrosJson>(Recurso);

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = (await db.FinancialStatementItems.IgnoreQueryFilters().Select(i => new { i.NiifGroup, i.Code }).ToListAsync(ct))
            .Select(x => (x.NiifGroup, x.Code)).ToHashSet();

        var insertadas = 0;
        foreach (var rubro in Rubros().Expandir(SeedContext.ParametricCreatedBy))
        {
            if (existentes.Contains((rubro.NiifGroup, rubro.Code))) continue;
            db.FinancialStatementItems.Add(rubro);
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
