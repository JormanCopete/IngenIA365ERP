using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

/// <summary>
/// Una operación de la matriz (§2.2): el mensaje que la trae, los roles del débito y del crédito (lado del importe
/// positivo; un negativo lo invierte) y los roles exigidos, los que todo documento de la operación mueve y que la
/// completitud pide por cada combinación en uso (FR-082). Los demás roles sólo piden regla cuando el mensaje trae
/// importe para ellos (§13.3). (nuevo)
/// </summary>
public sealed record OperacionDeInventario(
    string Codigo,
    string Mensaje,
    IReadOnlyList<string> RolesDebito,
    IReadOnlyList<string> RolesCredito,
    IReadOnlyList<string> RolesExigidos)
{
    /// <summary>Todos los roles de la operación, sin repetir, en el orden débito → crédito.</summary>
    public IReadOnlyList<string> Roles { get; } = RolesDebito.Concat(RolesCredito).Distinct(StringComparer.Ordinal).ToList();

    public bool TieneRol(string rol) => Roles.Contains(rol, StringComparer.Ordinal);
}

/// <summary>
/// Las veinte operaciones de la matriz (decisiones-transversales §2.7; contracts/contabilidad.md §2.2 y §3.3). Catálogo
/// fijo en código. Es distinto de <c>Application.Inventory.Common.OperacionesDeInventario</c>, que es el lado de Inventario
/// (sucursal principal y modo de paso): éste es el de Contabilidad y Contabilidad no depende de aquél (T31). (nuevo)
///
/// <para>
/// En todo rol que nombre <c>Inventario</c>, una línea en bodega de tránsito resuelve <c>Transito</c>
/// (<see cref="RolesDeCuenta.RolDeLaBodega"/>): por eso las operaciones que mueven bodegas admiten los dos.
/// </para>
/// </summary>
public static class OperacionesDeInventario
{
    public const string Venta = "Venta";
    public const string CostoDeVenta = "CostoDeVenta";
    public const string Compra = "Compra";
    public const string FacturaProveedor = "FacturaProveedor";
    public const string DevolucionAProveedor = "DevolucionAProveedor";
    public const string DevolucionDeCliente = "DevolucionDeCliente";
    public const string NotaCredito = "NotaCredito";
    public const string NotaDebito = "NotaDebito";
    public const string AjustePositivo = "AjustePositivo";
    public const string AjusteNegativo = "AjusteNegativo";
    public const string ConsumoInterno = "ConsumoInterno";
    public const string RetiroGravado = "RetiroGravado";
    public const string Baja = "Baja";
    public const string Ensamble = "Ensamble";
    public const string DespachoTraslado = "DespachoTraslado";
    public const string RecepcionTraslado = "RecepcionTraslado";
    public const string AjusteDeCosto = "AjusteDeCosto";
    public const string Reclasificacion = "Reclasificacion";
    public const string MovimientoDeCaja = "MovimientoDeCaja";
    public const string DiferenciaDeArqueo = "DiferenciaDeArqueo";

    public static IReadOnlyList<OperacionDeInventario> Todas { get; } =
    [
        new(Venta, VentaFacturadaV1.Type,
            [R.MedioDePago, R.Descuento, R.Retencion], [R.Ingreso, R.Impuesto], [R.Ingreso, R.MedioDePago]),
        new(CostoDeVenta, CostoDeVentaReconocidoV1.Type,
            [R.Costo], [R.Inventario, R.Transito], [R.Costo, R.Inventario]),
        new(Compra, CompraRecibidaV1.Type,
            [R.Inventario, R.Transito], [R.MercanciaPorFacturar], [R.Inventario, R.MercanciaPorFacturar]),
        new(FacturaProveedor, FacturaProveedorRegistradaV1.Type,
            [R.MercanciaPorFacturar, R.Contrapartida, R.Impuesto], [R.Retencion, R.CuentaPorPagar], [R.CuentaPorPagar]),
        new(DevolucionAProveedor, DevolucionRegistradaV1.Type,
            [R.MercanciaPorFacturar], [R.Inventario, R.Transito], [R.MercanciaPorFacturar, R.Inventario]),
        new(DevolucionDeCliente, DevolucionRegistradaV1.Type,
            [R.Inventario, R.Transito], [R.Costo], [R.Inventario, R.Costo]),
        new(NotaCredito, NotaCreditoEmitidaV1.Type,
            [R.Devolucion, R.Impuesto], [R.Descuento, R.Retencion, R.MedioDePago], [R.Devolucion]),
        new(NotaDebito, NotaDebitoEmitidaV1.Type,
            [R.MedioDePago, R.Descuento, R.Retencion], [R.Ingreso, R.Impuesto], [R.Ingreso, R.MedioDePago]),
        new(AjustePositivo, AjusteInventarioAprobadoV1.Type,
            [R.Inventario, R.Transito], [R.Contrapartida], [R.Inventario, R.Contrapartida]),
        new(AjusteNegativo, AjusteInventarioAprobadoV1.Type,
            [R.Contrapartida], [R.Inventario, R.Transito], [R.Contrapartida, R.Inventario]),
        new(ConsumoInterno, AjusteInventarioAprobadoV1.Type,
            [R.Contrapartida], [R.Inventario, R.Transito], [R.Contrapartida, R.Inventario]),
        new(RetiroGravado, AjusteInventarioAprobadoV1.Type,
            [R.Contrapartida], [R.Inventario, R.Transito, R.Impuesto], [R.Contrapartida, R.Inventario]),
        new(Baja, AjusteInventarioAprobadoV1.Type,
            [R.Contrapartida], [R.Inventario, R.Transito], [R.Contrapartida, R.Inventario]),
        new(Ensamble, AjusteInventarioAprobadoV1.Type,
            [R.Inventario], [R.Inventario], [R.Inventario]),
        new(DespachoTraslado, TrasladoDespachadoV1.Type,
            [R.Transito, R.Inventario], [R.Inventario, R.Transito], [R.Transito, R.Inventario]),
        new(RecepcionTraslado, TrasladoRecibidoV1.Type,
            [R.Inventario, R.Transito], [R.Transito, R.Inventario], [R.Inventario, R.Transito]),
        new(AjusteDeCosto, AjusteDeCostoReconocidoV1.Type,
            [R.Inventario, R.Transito, R.Costo], [R.Contrapartida, R.Redondeo], [R.Inventario]),
        new(Reclasificacion, GrupoContableReclasificadoV1.Type,
            [R.Inventario, R.Transito], [R.Inventario, R.Transito], [R.Inventario]),
        new(MovimientoDeCaja, MovimientoDeCajaRegistradoV1.Type,
            [R.CajaDestino, R.MedioDePago], [R.MedioDePago, R.CajaDestino], [R.MedioDePago]),
        new(DiferenciaDeArqueo, DiferenciaDeArqueoAprobadaV1.Type,
            [R.MedioDePago, R.Faltante, R.GastoDeArqueo], [R.Sobrante, R.MedioDePago], [R.MedioDePago]),
    ];

    private static readonly Dictionary<string, OperacionDeInventario> PorCodigo = Todas.ToDictionary(o => o.Codigo, StringComparer.Ordinal);

    /// <summary>La operación por su código exacto.</summary>
    public static OperacionDeInventario? Buscar(string? codigo) =>
        codigo is null ? null : PorCodigo.GetValueOrDefault(codigo.Trim());

    /// <summary>Las operaciones que trae un tipo de mensaje (varias en <c>AjusteInventarioAprobado</c> y <c>DevolucionRegistrada</c>).</summary>
    public static IReadOnlyList<OperacionDeInventario> DelMensaje(string tipoDeMensaje) =>
        Todas.Where(o => string.Equals(o.Mensaje, tipoDeMensaje, StringComparison.Ordinal)).ToList();
}
