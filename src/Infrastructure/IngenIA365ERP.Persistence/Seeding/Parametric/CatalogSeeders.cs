using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Seeders parametricos de catalogos por tenant (feature 004 — T038/T039,
/// data-model §4). Nota de adaptacion al modelo real: el producto no tiene
/// entidades Currency/DocumentType dedicadas — monedas y tipos de documento
/// viven en <see cref="ListParameter"/> (legacy sys_parlistas) y los
/// parametros en <see cref="SystemSetting"/>. Idempotencia por clave natural;
/// NUNCA se actualizan registros existentes (FR-016).
/// </summary>
public abstract class ListParameterSeederBase : IDataSeeder
{
    public abstract int Order { get; }
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>(ListType, LegacyCode, Description)</summary>
    protected abstract IEnumerable<(string ListType, string LegacyCode, string Description)> Items { get; }

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var inserted = 0;
        foreach (var (listType, code, description) in Items)
        {
            var exists = await db.ListParameters
                .AnyAsync(p => p.ListType == listType && p.LegacyCode == code, ct);
            if (exists) continue;

            db.ListParameters.Add(new ListParameter
            {
                ListType = listType,
                LegacyCode = code,
                Description = description,
                CreatedBy = SeedContext.ParametricCreatedBy
            });
            inserted++;
        }
        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}

/// <summary>Monedas de referencia (ListType MONE).</summary>
public sealed class CurrenciesSeeder : ListParameterSeederBase
{
    public override int Order => 40;
    protected override IEnumerable<(string, string, string)> Items =>
    [
        ("MONE", "COP", "Peso colombiano"),
        ("MONE", "USD", "Dólar estadounidense"),
        ("MONE", "EUR", "Euro"),
    ];
}

/// <summary>
/// Tipos de documento de identidad (ListType TDOC). Los LegacyCode coinciden
/// con la convencion de <c>Person.IdType</c> (una letra).
/// </summary>
public sealed class DocumentTypesSeeder : ListParameterSeederBase
{
    public override int Order => 50;
    protected override IEnumerable<(string, string, string)> Items =>
    [
        ("TDOC", "C", "Cédula de ciudadanía"),
        ("TDOC", "N", "NIT"),
        ("TDOC", "E", "Cédula de extranjería"),
        ("TDOC", "T", "Tarjeta de identidad"),
        ("TDOC", "P", "Pasaporte"),
        ("TDOC", "R", "Registro civil"),
    ];
}

/// <summary>
/// Parametros operativos por tenant (SystemSetting key/value). Cubre tambien
/// el rol del "TenantParametersSeeder" del inventario (fusionado aqui: mismo
/// almacen, misma clave natural).
/// </summary>
public sealed class SystemParametersSeeder : IDataSeeder
{
    public int Order => 10;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly (string Key, string Value, string Type, string Description, string Module)[] Defaults =
    [
        ("Core.Country", "CO", "String", "País de operación (ISO 3166-1)", "COR"),
        ("Core.DefaultCurrency", "COP", "String", "Moneda funcional por defecto", "COR"),
        ("Core.Locale", "es-CO", "String", "Cultura por defecto de la cooperativa", "COR"),
        ("Accounting.DecimalPlaces", "2", "Int", "Decimales para importes contables", "ACC"),
        ("Accounting.FiscalYearStartMonth", "1", "Int", "Mes de inicio del año fiscal", "ACC"),
        // Nomina (feature 005, research D-09): decisiones de la cooperativa, no valores legales.
        ("Payroll.Rounding", "Peso", "String", "Redondeo de la liquidación de nómina: Peso o Centavo", "PAY"),
        ("Payroll.VariationThresholdPercent", "10", "Decimal", "Umbral (%) que resalta variaciones en el comparativo de nómina", "PAY"),
        ("Payroll.AllowSameUserApproval", "false", "Bool", "Permitir que quien registra novedades apruebe la liquidación (con doble confirmación)", "PAY"),
        ("Payroll.ApplyEmployerExemption", "false", "Bool", "El empleador goza de la exoneración de salud, SENA e ICBF (art. 114-1 E.T.) para empleados bajo el tope; una cooperativa del régimen especial normalmente no", "PAY"),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var inserted = 0;
        foreach (var (key, value, type, description, module) in Defaults)
        {
            if (await db.SystemSettings.AnyAsync(s => s.SettingKey == key, ct)) continue;
            db.SystemSettings.Add(new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                ValueType = type,
                Description = description,
                ModulePrefix = module,
                CreatedBy = SeedContext.ParametricCreatedBy
            });
            inserted++;
        }
        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}

/// <summary>
/// Plan de cuentas base — clases nivel 1 del PUC del sector solidario
/// colombiano. Solo inserta las clases faltantes; el detalle (grupos/cuentas)
/// lo carga cada cooperativa o un catalogo ampliado posterior.
/// </summary>
public sealed class ChartOfAccountsSeeder : IDataSeeder
{
    public int Order => 60;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly (string Code, string Name, string Nature)[] PucLevel1 =
    [
        ("1", "Activo", "D"),
        ("2", "Pasivo", "C"),
        ("3", "Patrimonio", "C"),
        ("4", "Ingresos", "C"),
        ("5", "Gastos", "D"),
        ("6", "Costos de ventas y de prestación de servicios", "D"),
        ("7", "Costos de producción", "D"),
        ("8", "Cuentas de orden deudoras", "D"),
        ("9", "Cuentas de orden acreedoras", "C"),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var inserted = 0;
        foreach (var (code, name, nature) in PucLevel1)
        {
            if (await db.ChartOfAccounts.AnyAsync(a => a.AccountCode == code, ct)) continue;
            db.ChartOfAccounts.Add(new ChartOfAccount
            {
                AccountCode = code,
                Name = name,
                Nature = nature,
                Level = 1,
                CreatedBy = SeedContext.ParametricCreatedBy
            });
            inserted++;
        }
        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}
