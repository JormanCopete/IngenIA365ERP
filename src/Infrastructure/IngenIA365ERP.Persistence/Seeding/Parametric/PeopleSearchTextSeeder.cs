using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 022 (T013; plan §8.1): llena <c>COR_People.SearchText</c> de las personas guardadas antes de la feature, por
/// cooperativa y al arrancar. Idempotente: sólo toca filas con el texto vacío (eliminadas incluidas, para que una
/// restaurada ya lo tenga), por lotes de <see cref="Lote"/>, y el texto lo arma <see cref="Person.RecalcularTextoDeBusqueda"/>,
/// la misma regla del guardado: hacerlo en SQL por motor no daría igual tildes y espacios (plan, Complexity Tracking).
///
/// <para>
/// Es la excepción documentada a «una semilla nunca actualiza lo existente» (FR-016): no cambia ningún dato digitado, sólo
/// deriva una columna técnica. Por eso escribe con <c>ExecuteUpdate</c> fila a fila —sin pasar por el guardado, que
/// movería <c>UpdatedAt</c>/<c>UpdatedBy</c> de todas las personas— y una persona cuyo texto queda vacío (sin documento
/// ni nombres) se salta para no recorrerla en cada arranque. Devuelve las filas llenadas.
/// </para>
/// </summary>
public sealed class PeopleSearchTextSeeder : IDataSeeder
{
    public const int Lote = 500;

    public int Order => 94;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var llenadas = await AplicarAsync(db, ct);
        if (llenadas > 0)
            context.Logger.LogInformation("[Personas.TextoDeBusqueda] {Filas} persona(s) con el texto de búsqueda llenado.", llenadas);
        return llenadas;
    }

    public static async Task<int> AplicarAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var llenadas = 0;
        var ultimoId = 0;
        while (true)
        {
            var filas = await db.People.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.Id > ultimoId && (p.SearchText == null || p.SearchText == ""))
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName, p.LegacyCode })
                .Take(Lote)
                .ToListAsync(ct);
            if (filas.Count == 0) return llenadas;

            foreach (var f in filas)
            {
                var texto = Person.TextoDeBusqueda(f.TaxId, f.FirstName, f.OtherNames, f.LastName, f.SecondLastName, f.BusinessName, f.LegacyCode);
                if (texto.Length == 0) continue;
                var id = f.Id;
                llenadas += await db.People.IgnoreQueryFilters().Where(p => p.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.SearchText, texto), ct);
            }
            ultimoId = filas[^1].Id;
        }
    }
}
