using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Tipos de documento cruce sembrados (feature 009, FR-013; <c>cross-document-types.json</c>):
/// factura de venta y de compra, cuenta de cobro, notas, contrato, pagaré y «otro». La empresa
/// puede ampliarlos; los sembrados no se eliminan. Idempotente por <c>Code</c>.
/// </summary>
public sealed class CrossDocumentTypesSeeder : IDataSeeder
{
    public const string Recurso = "cross-document-types.json";

    public int Order => 61;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public sealed record Semilla(string Code, string Name);

    public static IReadOnlyList<Semilla> Semillas() => RecursoJson.Leer<List<Semilla>>(Recurso);

    public static IReadOnlyList<CrossDocumentType> Catalogo() => Semillas().Select(s => new CrossDocumentType
    {
        Code = s.Code.Trim().ToUpperInvariant(),
        Name = s.Name,
        IsActive = true,
        IsSeeded = true,
        CreatedBy = SeedContext.ParametricCreatedBy,
    }).ToList();

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = (await db.CrossDocumentTypes.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var insertadas = 0;
        foreach (var tipo in Catalogo())
        {
            if (existentes.Contains(tipo.Code)) continue;
            db.CrossDocumentTypes.Add(tipo);
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
