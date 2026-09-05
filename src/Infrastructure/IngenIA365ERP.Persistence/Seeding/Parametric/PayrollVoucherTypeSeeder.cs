using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 005 (FR-023): la aprobación de la nómina genera un comprobante contable de
/// tipo <c>NM</c>. Si la cooperativa no lo tiene, la aprobación se negaría con
/// <c>Payroll.VoucherTypeMissing</c>; esta semilla lo crea una vez.
/// </summary>
public sealed class PayrollVoucherTypeSeeder : IDataSeeder
{
    public const string Code = "NM";

    public int Order => 72;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;

        var existe = await db.VoucherTypes.IgnoreQueryFilters().AnyAsync(v => v.Code == Code, ct);
        if (existe) return 0;

        db.VoucherTypes.Add(new VoucherType
        {
            Code = Code,
            Name = "Nómina",
            ShortName = "NOMINA",
            ModuleCode = "NOM",
            UpdatesAccounting = true,
            ControlSequential = true,
            NextSequenceNumber = 0,
            CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }
}
