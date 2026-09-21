using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 010 (R7, data-model §2.5): el catálogo de motivos de retiro que el programa trae, con
/// la marca legal de si generan indemnización (CST art. 64). Los nueve códigos son
/// <c>IsSeeded</c>: la cooperativa agrega los suyos (sin indemnización) pero no cambia la marca ni
/// borra los sembrados. Idempotente por <c>Code</c>: en una fila sembrada que sigue intacta
/// corrige <c>Name</c> y <c>LegalBasis</c> (textos), <b>nunca</b> <c>GeneratesSeverancePay</c> ni
/// <c>RequiresContractEndDate</c> (marcas con efecto en la liquidación) ni <c>IsActive</c>; un
/// motivo propio de la cooperativa no se toca aunque comparta código.
/// </summary>
public sealed class TerminationReasonsSeeder : IDataSeeder
{
    public int Order => 72;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    // Los códigos caben en los 10 caracteres de CodigoDeCatalogo (la columna es nvarchar(10)); el
    // data-model los tenía más largos y se acortaron al implementar (2026-09-21).
    public static IReadOnlyList<TerminationReason> Catalogo() =>
    [
        Motivo("RENUNCIA", "Renuncia voluntaria", indemniza: false, exigeFin: false, "CST art. 61 lit. h"),
        Motivo("DESP_SINJC", "Despido sin justa causa", indemniza: true, exigeFin: true, "CST art. 64 (Ley 789 de 2002 art. 28)"),
        Motivo("DESP_JC", "Despido con justa causa", indemniza: false, exigeFin: false, "CST art. 62"),
        Motivo("VENC_TERM", "Vencimiento del término fijo con preaviso", indemniza: false, exigeFin: false, "CST art. 46"),
        Motivo("MUTUO_ACDO", "Mutuo acuerdo", indemniza: false, exigeFin: false, "CST art. 61 lit. b"),
        Motivo("FIN_OBRA", "Terminación de la obra o labor contratada", indemniza: false, exigeFin: false, "CST art. 61 lit. d"),
        Motivo("PER_PRUEBA", "Terminación en período de prueba", indemniza: false, exigeFin: false, "CST art. 78"),
        Motivo("MUERTE", "Muerte del trabajador", indemniza: false, exigeFin: false, "CST art. 61 lit. a"),
        Motivo("PENSION", "Reconocimiento de la pensión", indemniza: false, exigeFin: false, "CST art. 62 lit. a num. 14"),
    ];

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = await db.TerminationReasons.IgnoreQueryFilters().ToListAsync(ct);
        var porCodigo = existentes.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var insertados = 0;
        foreach (var motivo in Catalogo())
        {
            if (porCodigo.TryGetValue(motivo.Code, out var existente))
            {
                // Sólo textos, y sólo en una fila del programa que nadie editó: las marcas son de la ley
                // y la cooperativa no las cambia; un motivo propio con el mismo código es suyo.
                if (!existente.IsSeeded || !Intacta(existente)) continue;
                if (existente.Name == motivo.Name && existente.LegalBasis == motivo.LegalBasis) continue;
                existente.Name = motivo.Name;
                existente.LegalBasis = motivo.LegalBasis;
                existente.UpdatedBy = SeedContext.ParametricCreatedBy;
                existente.UpdatedAt = DateTime.UtcNow;
                continue;
            }
            db.TerminationReasons.Add(motivo);
            insertados++;
        }
        await db.SaveChangesAsync(ct);
        return insertados;
    }

    private static bool Intacta(TerminationReason r) =>
        r.CreatedBy == SeedContext.ParametricCreatedBy && (r.UpdatedBy is null || r.UpdatedBy == SeedContext.ParametricCreatedBy);

    private static TerminationReason Motivo(string code, string name, bool indemniza, bool exigeFin, string legalBasis) => new()
    {
        Code = code,
        Name = name,
        GeneratesSeverancePay = indemniza,
        RequiresContractEndDate = exigeFin,
        LegalBasis = legalBasis,
        IsSeeded = true,
        IsActive = true,
        CreatedBy = SeedContext.ParametricCreatedBy,
    };
}
