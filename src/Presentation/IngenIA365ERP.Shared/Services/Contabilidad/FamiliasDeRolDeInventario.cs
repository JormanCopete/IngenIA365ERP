namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Las pestañas de la matriz contable de Inventario (feature 012, US7, T533; contracts/contabilidad.md §2.8): los roles de
/// cuenta del catálogo fijo (<c>RolesDeCuenta</c>, en Application) repartidos por familia —inventario y costo, ventas, compras,
/// impuestos y retenciones, medios de pago, caja, traslados y puentes—. Un rol va en una sola familia; que ninguno quede sin
/// pestaña lo fija <c>LaMatrizMuestraTodosLosRoles</c>. (nuevo)
/// </summary>
public static class FamiliasDeRolDeInventario
{
    public sealed record Familia(string Clave, string Nombre, IReadOnlyList<string> Roles);

    public static IReadOnlyList<Familia> Todas { get; } =
    [
        new("inventario", "Inventario y costo", ["Inventario", "Costo", "Redondeo"]),
        new("ventas", "Ventas", ["Ingreso", "Descuento", "Devolucion"]),
        new("compras", "Compras", ["MercanciaPorFacturar", "CuentaPorPagar"]),
        new("impuestos", "Impuestos y retenciones", ["Impuesto", "Retencion"]),
        new("medios", "Medios de pago", ["MedioDePago"]),
        new("caja", "Caja", ["CajaDestino", "Sobrante", "Faltante", "GastoDeArqueo"]),
        new("traslados", "Traslados y puentes", ["Transito", "Contrapartida"]),
    ];

    /// <summary>La familia de un rol, o nula si el rol no es del catálogo.</summary>
    public static Familia? FamiliaDe(string? rol) => Todas.FirstOrDefault(f => f.Roles.Contains(rol ?? "", StringComparer.Ordinal));

    /// <summary>El nombre de un rol para la pantalla (los códigos no llevan tilde).</summary>
    public static string NombreDelRol(string rol) => rol switch
    {
        "Transito" => "Tránsito",
        "Devolucion" => "Devolución",
        "Retencion" => "Retención",
        "MedioDePago" => "Medio de pago",
        "MercanciaPorFacturar" => "Mercancía por facturar",
        "CuentaPorPagar" => "Cuenta por pagar",
        "CajaDestino" => "Caja de destino",
        "GastoDeArqueo" => "Gasto de arqueo",
        _ => rol,
    };

    /// <summary>El nombre de una dimensión de la API (<c>accountingGroupCode</c>…) para la pantalla.</summary>
    public static string NombreDeLaDimension(string dimension) => dimension switch
    {
        "accountingGroupCode" => "Grupo contable",
        "warehouseCode" => "Bodega",
        "pointOfSaleCode" => "Punto de venta",
        "paymentMeansCode" => "Medio de pago",
        "taxRateCode" => "Tarifa",
        "taxRate" => "Porcentaje de la tarifa",
        "reasonCode" => "Causa o motivo",
        "branch" => "Sucursal",
        "costCenter" => "Centro de costo",
        _ => dimension,
    };
}
