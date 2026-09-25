namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// De qué módulo es un evento de auditoría, deducido del espacio de nombres del comando o de la
/// entidad (feature 012, T3, T36; T059). <b>Una sola inferencia</b> para <c>AuditBehavior</c> y
/// <c>AuditableEntityInterceptor</c>: tenían cada uno la suya, con listas distintas, y un mismo cambio
/// podía quedar en un módulo por el comando y en otro por la entidad.
///
/// <para>
/// El orden importa, porque se busca por subcadena: los módulos de negocio primero (así
/// <c>Application.Inventory.Integration</c> es <c>Inventory</c> y <c>Application.Accounting.Inventory</c>
/// es <c>Accounting</c>), después los de plataforma de la 012, y <c>.Core</c> al final para que
/// <c>Core.Taxes</c> y <c>Core.PaymentMeans</c> no caigan en <c>Core</c>. El módulo decide la retención
/// (<c>AuditRetention</c>) y si el evento va encadenado (<see cref="AuditoriaEncadenada"/>).
/// </para>
/// </summary>
public static class ModuloDeAuditoria
{
    public const string Navigation = "Navigation";
    public const string Accounting = "Accounting";
    public const string Lending = "Lending";
    public const string Payroll = "Payroll";
    public const string Inventory = "Inventory";
    public const string Cdt = "CDT";
    public const string Debit = "Debit";
    public const string Treasury = "Treasury";
    public const string Security = "Security";
    public const string ElectronicInvoicing = "ElectronicInvoicing";
    public const string Integration = "Integration";
    public const string Approvals = "Approvals";
    public const string Alerts = "Alerts";
    public const string Parameters = "Parameters";
    public const string Taxes = "Taxes";
    public const string PaymentMeans = "PaymentMeans";
    public const string Audit = "Audit";
    public const string Web = "Web";
    public const string Admin = "Admin";
    public const string Core = "Core";

    /// <summary>Lo que devuelve <see cref="Inferir"/> cuando nada coincide y no se pide otra cosa.</summary>
    public const string General = "General";

    private static readonly (string Fragmento, string Modulo)[] Reglas =
    [
        // Feature 009 (FR-051): el ingreso a una opción del ERP es navegación, no una escritura del módulo.
        (".Audit.RegisterOptionAccess", Navigation),
        (".Accounting", Accounting),
        (".Lending", Lending),
        (".Payroll", Payroll),
        (".Inventory", Inventory),
        (".CDT", Cdt),
        (".Debit", Debit),
        (".Treasury", Treasury),
        (".Security", Security),
        // Feature 012 (T3, T36): plataforma del comercio, antes de .Core.
        (".ElectronicInvoicing", ElectronicInvoicing),
        (".Integration", Integration),
        (".Approvals", Approvals),
        (".Alerts", Alerts),
        (".Parameters", Parameters),
        (".Core.Taxes", Taxes),
        (".Core.PaymentMeans", PaymentMeans),
        (".Core.Payments", PaymentMeans),
        (".Audit", Audit),
        (".Web", Web),
        (".Admin", Admin),
        (".Core", Core),
    ];

    /// <param name="espacioDeNombres">El del comando o la entidad.</param>
    /// <param name="sinModulo">Qué devolver si no coincide nada (la entidad dice <c>Unknown</c>).</param>
    public static string Inferir(string? espacioDeNombres, string sinModulo = General)
    {
        if (string.IsNullOrEmpty(espacioDeNombres)) return sinModulo;
        foreach (var (fragmento, modulo) in Reglas)
        {
            if (Contiene(espacioDeNombres, fragmento)) return modulo;
        }
        return sinModulo;
    }

    /// <summary>
    /// El fragmento tiene que ser un segmento entero o el principio de uno terminado en punto o fin:
    /// <c>.Core</c> no coincide con <c>.CoreBanking</c>, pero <c>.Core.Taxes</c> sí con <c>.Core.Taxes.Import</c>.
    /// </summary>
    private static bool Contiene(string ns, string fragmento)
    {
        var desde = 0;
        while (true)
        {
            var i = ns.IndexOf(fragmento, desde, StringComparison.Ordinal);
            if (i < 0) return false;
            var fin = i + fragmento.Length;
            if (fin == ns.Length || ns[fin] == '.') return true;
            desde = i + 1;
        }
    }
}
