namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Las vistas que muestra <c>/ventas/informes</c> (feature 012, T643; contracts/api.md §27): las de ventas y caja, los indicios de
/// deterioro y, desde el 2026-11-23, el detalle de ventas por medio de pago. El registro de vistas de la API (<c>GET /api/reports/inventory</c>) las publica junto a las de inventario; la
/// pantalla de Ventas se queda con éstas y en su orden. (nuevo)
/// </summary>
public static class VistasDeVentas
{
    public static readonly IReadOnlyList<string> Claves =
    [
        "sales-by-session", "sales-by-register", "sales-by-payment-means", "sales-payments-detail", "cash-session", "day-close", "card-payments", "cash-movements",
        "cash-differences", "voucher-redemptions", "discount-approvals", "impairment",
    ];

    /// <summary>Los agrupamientos que admiten las vistas «sales-by-*» (el de cliente trae datos personales).</summary>
    public static readonly IReadOnlyList<(string Valor, string Texto)> Agrupamientos = [("day", "Por día"), ("customer", "Por cliente")];

    public static bool EsDeVentas(string clave) => Claves.Contains(clave, StringComparer.Ordinal);
}
