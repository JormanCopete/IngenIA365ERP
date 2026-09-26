using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T211; decisiones-transversales §2.14, Order 79; FR-037, FR-039; data-model §5.10): las causas de baja y
/// ajuste —merma o faltante, daño, vencimiento, hurto, diferencia de conteo, destrucción y reclamación al transportador—.
/// Las que usa el sistema llevan <c>IsRequiredBySystem</c>: «diferencia de conteo» (el ajuste de un conteo aprobado, US11)
/// y «reclamación al transportador» (la baja desde tránsito de una diferencia de traslado, US10). Idempotente por código.
/// Espera a <c>InventarioComercialNucleo</c>.
/// </summary>
public sealed class AdjustmentCausesSeeder : IDataSeeder
{
    public int Order => 79;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public const string CodigoDiferenciaDeConteo = "DIFCONTEO";
    public const string CodigoReclamacionAlTransportador = "RECLTRANSP";

    /// <summary>Código, nombre, sentidos admitidos, baja desde tránsito, exige soporte y si el sistema la usa.</summary>
    public static readonly IReadOnlyList<AdjustmentCause> Sembradas =
    [
        Causa("MERMA", "Merma o faltante", positivo: false, negativo: true, transito: false),
        Causa("DANO", "Daño", positivo: false, negativo: true, transito: true),
        Causa("VENCIM", "Vencimiento", positivo: false, negativo: true, transito: false),
        Causa("HURTO", "Hurto", positivo: false, negativo: true, transito: true, soporte: true),
        Causa(CodigoDiferenciaDeConteo, "Diferencia de conteo", positivo: true, negativo: true, transito: false, sistema: true),
        Causa("DESTRUC", "Destrucción", positivo: false, negativo: true, transito: false, soporte: true),
        Causa(CodigoReclamacionAlTransportador, "Reclamación al transportador", positivo: false, negativo: true, transito: true, sistema: true),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await SemillasDeInventario.TieneLasTablasAsync(db, context.Logger, nameof(AdjustmentCausesSeeder), ct)) return 0;
        return await AplicarAsync(db, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = (await db.AdjustmentCauses.IgnoreQueryFilters().Select(c => c.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var insertadas = 0;
        foreach (var s in Sembradas)
        {
            if (existentes.Contains(s.Code)) continue;
            db.AdjustmentCauses.Add(new AdjustmentCause
            {
                Code = s.Code, Name = s.Name, AllowsPositive = s.AllowsPositive, AllowsNegative = s.AllowsNegative,
                AllowsTransitWriteOff = s.AllowsTransitWriteOff, RequiresAttachment = s.RequiresAttachment,
                IsRequiredBySystem = s.IsRequiredBySystem, IsSeeded = true, IsActive = true, CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    private static AdjustmentCause Causa(string codigo, string nombre, bool positivo, bool negativo, bool transito, bool soporte = false, bool sistema = false) =>
        new()
        {
            Code = codigo, Name = nombre, AllowsPositive = positivo, AllowsNegative = negativo, AllowsTransitWriteOff = transito,
            RequiresAttachment = soporte, IsRequiredBySystem = sistema,
        };
}
