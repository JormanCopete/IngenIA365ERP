using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 010 (R4, data-model §2.1): las políticas por empresa de <c>PAY_CompanyPolicies</c> con
/// su valor por defecto de <see cref="CompanyPolicyKeys"/>. Inserta una clave <b>sólo si no tiene
/// ninguna vigencia</b> (de ninguna fecha): una política que la cooperativa ya decidió, o que la
/// migración de datos copió de <c>COR_SystemSettings</c>, no se pisa. Las dos claves heredadas
/// (<c>Exonerada114_1</c> desde <c>Payroll.ApplyEmployerExemption</c>, <c>AllowSameUserApproval</c>
/// desde su homónima) toman el valor que haya en <c>COR_SystemSettings</c> y, si no hay, «false».
/// <c>ArranqueNominaFecha</c> no tiene defecto fijo: es la fecha de inicio del primer período de
/// pago de la cooperativa, y si todavía no hay períodos no se siembra (la pantalla de políticas la
/// pide). La vigencia abre en <see cref="VigenciaAbierta"/>: rige «desde siempre».
/// </summary>
public sealed class CompanyPoliciesSeeder : IDataSeeder
{
    public int Order => 75;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Desde cuándo rige un defecto: una fecha anterior a cualquier nómina de la plataforma.</summary>
    public static readonly DateOnly VigenciaAbierta = new(1900, 1, 1);

    private const string SettingExoneracion = "Payroll.ApplyEmployerExemption";
    private const string SettingMismoUsuario = "Payroll.AllowSameUserApproval";

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = (await db.CompanyPolicies.IgnoreQueryFilters().Select(p => p.Key).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var sistema = (await db.SystemSettings.AsNoTracking()
                .Where(s => s.ModulePrefix == "PAY" && (s.SettingKey == SettingExoneracion || s.SettingKey == SettingMismoUsuario))
                .Select(s => new { s.SettingKey, s.SettingValue })
                .ToListAsync(ct))
            .ToDictionary(s => s.SettingKey, s => s.SettingValue, StringComparer.OrdinalIgnoreCase);
        var primerPeriodo = await db.PayPeriods.AsNoTracking().OrderBy(p => p.StartDate).Select(p => (DateTime?)p.StartDate).FirstOrDefaultAsync(ct);

        var insertadas = 0;
        foreach (var def in CompanyPolicyKeys.Todas)
        {
            if (existentes.Contains(def.Clave)) continue;
            var valor = ValorInicial(def, sistema, primerPeriodo);
            if (valor is null) continue;
            db.CompanyPolicies.Add(new CompanyPolicy
            {
                Key = def.Clave,
                Value = valor,
                ValidFrom = VigenciaAbierta,
                Notes = NotaDe(def.Clave, sistema),
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    /// <summary>El valor con que nace cada clave; nulo = no se siembra (sin defecto y sin dato del que derivarlo).</summary>
    public static string? ValorInicial(CompanyPolicyKeys.Definicion def, IReadOnlyDictionary<string, string> sistema, DateTime? primerPeriodo)
    {
        switch (def.Clave)
        {
            case CompanyPolicyKeys.Exonerada114_1:
                return Booleano(sistema.GetValueOrDefault(SettingExoneracion)) ?? def.Defecto;
            case CompanyPolicyKeys.AllowSameUserApproval:
                return Booleano(sistema.GetValueOrDefault(SettingMismoUsuario)) ?? def.Defecto;
            case CompanyPolicyKeys.ArranqueNominaFecha:
                return primerPeriodo is { } p ? p.ToString(CompanyPolicyKeys.FormatoFecha) : null;
            default:
                return def.Defecto;
        }
    }

    private static string? Booleano(string? texto) =>
        texto is not null && bool.TryParse(texto, out var b) ? (b ? CompanyPolicyKeys.Verdadero : CompanyPolicyKeys.Falso) : null;

    private static string NotaDe(string clave, IReadOnlyDictionary<string, string> sistema) => clave switch
    {
        CompanyPolicyKeys.Exonerada114_1 when sistema.ContainsKey(SettingExoneracion) => "Copiada de Parámetros del sistema (Payroll.ApplyEmployerExemption) por la semilla de la feature 010.",
        CompanyPolicyKeys.AllowSameUserApproval when sistema.ContainsKey(SettingMismoUsuario) => "Copiada de Parámetros del sistema (Payroll.AllowSameUserApproval) por la semilla de la feature 010.",
        CompanyPolicyKeys.ArranqueNominaFecha => "Fecha de inicio del primer período de pago de la cooperativa; la semilla la propone y la cooperativa la confirma.",
        _ => "Valor por defecto de la semilla; la cooperativa lo decide en Nómina › Políticas.",
    };
}
