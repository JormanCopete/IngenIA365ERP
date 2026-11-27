using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
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
        (AutoaprobacionDelSaldoInicial.Clave, "false", "Bool", "Permitir que quien carga el saldo inicial de inventario lo apruebe él mismo (sin un segundo usuario)", "INV"),
        (CuentasPropiasBajoCatalogo.Clave, "false", "Bool", "Permitir cuentas propias de 2 a 6 dígitos donde el catálogo ya define las suyas. Esas cuentas no existen en el catálogo oficial", "ACC"),
        (IngenIA365ERP.Application.Accounting.Documents.BusquedaEnComprobantes.Clave, IngenIA365ERP.Application.Accounting.Documents.BusquedaEnComprobantes.EnElCampo, "String", "Cómo busca F2 en la ventana de comprobantes: EnElCampo (la lista sale debajo mientras se escribe) o VentanaF2 (la cuenta sólo lee el código y F2 abre una ventana para buscar por nombre)", "ACC"),
        (IngenIA365ERP.Application.Common.Impresos.UsuarioEnImpresos.Clave, IngenIA365ERP.Application.Common.Impresos.UsuarioEnImpresos.Nombre, "String", "Cómo sale el usuario en los documentos impresos: Correo, Nombre (primer nombre y apellido), NombreCompleto, Iniciales, Cargo o Sobrenombre (el que le ponga el administrador a cada usuario)", "COR"),
        (IngenIA365ERP.Application.Accounting.Lending.Lotes.ComprobantePorRecibo.Clave, "false", "Bool", "Contabilizar en un solo comprobante lo que un recibo de caja abona a créditos, aportes y ahorro (al instante). Apagado: un comprobante por obligación", "ACC"),
        (IngenIA365ERP.Application.Common.Integration.ReciboEnUnSoloComprobante.Clave, "false", "Bool", "Contabilizar el recibo de caja de Recaudo junto con lo que abona a créditos, aportes y ahorro en un solo comprobante (al instante). Apagado: el recibo y cada módulo con su comprobante", "ACC"),
        // 2026-11-27 (feature 028, T047; FR-027b, D11).
        (IngenIA365ERP.Application.Accounting.Lending.Lotes.DesembolsoConSusRecogidas.Clave, "false", "Bool", "¿El desembolso y sus recogidas salen en un solo comprobante? Sí: el comprobante del desembolso lleva también el recibo de cada crédito que recoge (el puente entra y sale en él). No: el desembolso y cada recogida con su comprobante", "ACC"),
        (TerceroPorDefectoDeInventario.Clave, "", "String", "Número de documento del tercero para movimientos de inventario sin tercero (ajustes, consumo, bajas, ensambles, traslados) en cuentas que lo exigen. Vacío: no se usa", "ACC"),
        (IngenIA365ERP.Application.Core.SystemSettings.ActividadDelEquipoEnLaSesion.Clave, "false", "Bool", "Contar el uso del computador (en cualquier programa) como actividad para el cierre de sesión por inactividad. Funciona en Chrome y Edge con permiso de cada usuario; con la pantalla bloqueada no cuenta", "COR"),
        (IngenIA365ERP.Application.Core.SystemSettings.VentanasConSesion.Clave, "true", "Bool", "Abrir en ventana nueva lleva la sesión a la ventana nueva (sin volver a entrar); las ventanas de una misma sesión cuentan juntas la inactividad y cerrar sesión en una cierra todas. Apagado, la ventana nueva pide entrar", "COR"),
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

