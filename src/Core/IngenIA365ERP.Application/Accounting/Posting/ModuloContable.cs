using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Posting;

/// <summary>
/// Los módulos que contabilizan (feature 009) y su bandera en <see cref="AccountingModules"/>:
/// el código viaja en <c>AccountingDocument.OriginModule</c> y en <c>VoucherType.ModuleCode</c>;
/// la bandera es lo que la cuenta declara en «aplica a».
/// </summary>
public static class ModuloContable
{
    public const string Contabilidad = "CNT";
    public const string Nomina = "NOM";
    public const string Cartera = "CAR";
    public const string Inventario = "INV";
    public const string Tesoreria = "TES";
    public const string Cdt = "CDT";
    public const string Activos = "ACT";

    public static readonly IReadOnlyList<string> Codigos = [Contabilidad, Nomina, Cartera, Inventario, Tesoreria, Cdt, Activos];

    public static AccountingModules? Bandera(string module) => module switch
    {
        Contabilidad => AccountingModules.Accounting,
        Nomina => AccountingModules.Payroll,
        Cartera => AccountingModules.Lending,
        Inventario => AccountingModules.Inventory,
        Tesoreria => AccountingModules.Treasury,
        Cdt => AccountingModules.Savings,
        Activos => AccountingModules.Assets,
        _ => null,
    };

    public static bool Habilitada(AccountingModules habilitados, string module) =>
        Bandera(module) is { } bandera && habilitados.HasFlag(bandera);

    public static bool EsValido(string? module) => module is not null && Codigos.Contains(module);

    /// <summary>De la lista de códigos de la pantalla a la máscara de la cuenta; los desconocidos se ignoran.</summary>
    public static AccountingModules Desde(IEnumerable<string>? modules)
    {
        var mascara = AccountingModules.None;
        foreach (var m in modules ?? [])
            if (Bandera(m.Trim().ToUpperInvariant()) is { } b) mascara |= b;
        return mascara;
    }

    /// <summary>De la máscara a los códigos, en el orden de <see cref="Codigos"/>.</summary>
    public static IReadOnlyList<string> Lista(AccountingModules modules) =>
        Codigos.Where(c => Bandera(c) is { } b && modules.HasFlag(b)).ToList();

    public static string Nombre(string module) => module switch
    {
        Contabilidad => "Contabilidad",
        Nomina => "Nómina",
        Cartera => "Cartera",
        Inventario => "Inventario",
        Tesoreria => "Tesorería",
        Cdt => "CDT y ahorros",
        Activos => "Activos",
        _ => module,
    };
}
