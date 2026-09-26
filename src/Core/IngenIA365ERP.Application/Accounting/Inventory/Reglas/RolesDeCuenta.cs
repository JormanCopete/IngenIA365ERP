using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Inventory;
using CuentaTaxKind = IngenIA365ERP.Domain.Enums.Accounting.TaxKind;
using TarifaTaxKind = IngenIA365ERP.Domain.Enums.Core.TaxKind;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

/// <summary>
/// Una dimensión de la matriz (feature 012, T27; contracts/contabilidad.md §2.1–§2.2). El nombre que viaja
/// (<see cref="RolesDeCuenta.NombreDe"/>) es el del cuerpo de la API (<c>accountingGroupCode</c>…). (nuevo)
/// </summary>
public enum DimensionDeRegla
{
    AccountingGroupCode = 1,
    WarehouseCode = 2,
    PointOfSaleCode = 3,
    PaymentMeansCode = 4,
    TaxRateCode = 5,
    TaxRate = 6,
    ReasonCode = 7,
    Branch = 8,
    CostCenter = 9,
}

/// <summary>Quién es el tercero natural de un rol (§3.2). (nuevo)</summary>
public enum TerceroNatural
{
    /// <summary>Ninguno: el del sobre, sólo si la cuenta lo exige.</summary>
    Ninguno = 0,

    /// <summary><c>thirdPartyPersonPublicId</c> del pago (adquirente, banco o el cliente del crédito).</summary>
    DelPago = 1,

    /// <summary>La persona del sobre (proveedor o cliente).</summary>
    DelSobre = 2,

    /// <summary><c>cashier.personPublicId</c> del arqueo.</summary>
    Cajero = 3,
}

/// <summary>
/// Un rol de cuenta (§2.2): sus dimensiones exigidas y admitidas, su tercero natural, si siempre lleva tercero y los
/// <c>ReasonCode</c> fijos que admite (vacío = la causa de ajuste de Inventario, o ninguno). (nuevo)
/// </summary>
public sealed record RolDeCuenta(
    string Codigo,
    IReadOnlyList<DimensionDeRegla> Exigidas,
    IReadOnlyList<DimensionDeRegla> Opcionales,
    TerceroNatural Tercero,
    bool SiempreLlevaTercero,
    IReadOnlyList<string> MotivosFijos)
{
    public bool EsDeImpuesto => Codigo is RolesDeCuenta.Impuesto or RolesDeCuenta.Retencion;

    /// <summary>Exigidas u opcionales.</summary>
    public bool Admite(DimensionDeRegla dimension) => Exigidas.Contains(dimension) || Opcionales.Contains(dimension);
}

/// <summary>
/// Los diecisiete roles de cuenta de la matriz (decisiones-transversales §2.7; contracts/contabilidad.md §2.2, §2.5 y
/// §3.2). Catálogo fijo en código: son datos, no parámetros. Las dimensiones exigidas se comparan por igualdad exacta y la
/// regla nunca las deja en <c>*</c>; las opcionales coinciden por igualdad o con <c>*</c> y pesan (bodega o punto 16,
/// centro 8, sucursal 4, grupo 2; <see cref="InventoryPostingRule.PesoDe"/>). (nuevo)
///
/// <para>
/// <c>Contrapartida</c> es el único rol cuyas dimensiones dependen de la operación (<see cref="DimensionesDe"/>): exige
/// <c>ReasonCode</c> en <c>AjusteNegativo</c> y <c>Baja</c> (la causa de ajuste) y en <c>AjusteDeCosto</c> (la
/// <see cref="KardexReason"/>), y no lo admite en las demás.
/// </para>
/// </summary>
public static class RolesDeCuenta
{
    public const string Inventario = "Inventario";
    public const string Transito = "Transito";
    public const string Costo = "Costo";
    public const string Ingreso = "Ingreso";
    public const string Descuento = "Descuento";
    public const string Devolucion = "Devolucion";
    public const string Impuesto = "Impuesto";
    public const string Retencion = "Retencion";
    public const string MedioDePago = "MedioDePago";
    public const string MercanciaPorFacturar = "MercanciaPorFacturar";
    public const string CuentaPorPagar = "CuentaPorPagar";
    public const string Contrapartida = "Contrapartida";
    public const string CajaDestino = "CajaDestino";
    public const string Sobrante = "Sobrante";
    public const string Faltante = "Faltante";
    public const string GastoDeArqueo = "GastoDeArqueo";
    public const string Redondeo = "Redondeo";

    private const DimensionDeRegla G = DimensionDeRegla.AccountingGroupCode;
    private const DimensionDeRegla W = DimensionDeRegla.WarehouseCode;
    private const DimensionDeRegla P = DimensionDeRegla.PointOfSaleCode;
    private const DimensionDeRegla M = DimensionDeRegla.PaymentMeansCode;
    private const DimensionDeRegla T = DimensionDeRegla.TaxRateCode;
    private const DimensionDeRegla Tv = DimensionDeRegla.TaxRate;
    private const DimensionDeRegla R = DimensionDeRegla.ReasonCode;
    private const DimensionDeRegla B = DimensionDeRegla.Branch;
    private const DimensionDeRegla C = DimensionDeRegla.CostCenter;

    /// <summary>Las razones del kardex que llevan contrapartida propia en <c>AjusteDeCosto</c> (<c>RoundingResidue</c> va a <c>Redondeo</c>).</summary>
    public static IReadOnlyList<string> RazonesDeAjusteDeCosto { get; } = Enum.GetNames<KardexReason>()
        .Where(n => n is not nameof(KardexReason.Normal) and not nameof(KardexReason.RoundingResidue))
        .ToList();

    /// <summary>Los destinos de caja que son <c>ReasonCode</c> del rol <c>CajaDestino</c> (la caja registradora es otro <c>MedioDePago</c>).</summary>
    public static IReadOnlyList<string> DestinosDeCaja { get; } = [nameof(CashMovementDestination.Safe), nameof(CashMovementDestination.Deposit)];

    public static IReadOnlyList<RolDeCuenta> Todos { get; } =
    [
        new(Inventario, [G], [W, C, B], TerceroNatural.Ninguno, false, []),
        new(Transito, [G], [W, C, B], TerceroNatural.Ninguno, false, []),
        new(Costo, [G], [W, C, B], TerceroNatural.Ninguno, false, []),
        new(Ingreso, [G], [W, C, B], TerceroNatural.Ninguno, false, []),
        new(Descuento, [], [G, W, C, B], TerceroNatural.Ninguno, false, []),
        new(Devolucion, [], [G, W, C, B], TerceroNatural.Ninguno, false, []),
        new(Impuesto, [T, Tv], [C, B], TerceroNatural.DelSobre, true, []),
        new(Retencion, [T, Tv], [C, B], TerceroNatural.DelSobre, true, []),
        new(MedioDePago, [M], [P, B], TerceroNatural.DelPago, true, []),
        new(MercanciaPorFacturar, [], [G, W, C, B], TerceroNatural.DelSobre, true, []),
        new(CuentaPorPagar, [], [B], TerceroNatural.DelSobre, true, []),
        new(Contrapartida, [], [G, W, C, B], TerceroNatural.Ninguno, false, []),
        new(CajaDestino, [R], [P, B], TerceroNatural.Ninguno, false, DestinosDeCaja),
        new(Sobrante, [R], [P, C, B], TerceroNatural.Ninguno, false, [nameof(CashDifferenceTreatment.Surplus)]),
        new(Faltante, [R], [P, C, B], TerceroNatural.Cajero, true, [nameof(CashDifferenceTreatment.ShortageToCashier)]),
        new(GastoDeArqueo, [R], [P, C, B], TerceroNatural.Ninguno, false, [nameof(CashDifferenceTreatment.ShortageToExpense)]),
        new(Redondeo, [], [G, W, C, B], TerceroNatural.Ninguno, false, []),
    ];

    private static readonly Dictionary<string, RolDeCuenta> PorCodigo = Todos.ToDictionary(r => r.Codigo, StringComparer.Ordinal);

    /// <summary>El rol por su código exacto (los códigos son sensibles a mayúsculas, como en la clave de la regla).</summary>
    public static RolDeCuenta? Buscar(string? codigo) =>
        codigo is null ? null : PorCodigo.GetValueOrDefault(codigo.Trim());

    /// <summary>
    /// Las dimensiones exigidas y opcionales del rol en la operación. <c>Contrapartida</c> exige el motivo en
    /// <c>AjusteNegativo</c>, <c>Baja</c> y <c>AjusteDeCosto</c>.
    /// </summary>
    public static (IReadOnlyList<DimensionDeRegla> Exigidas, IReadOnlyList<DimensionDeRegla> Opcionales) DimensionesDe(string operacion, RolDeCuenta rol)
    {
        if (rol.Codigo == Contrapartida && ContrapartidaExigeMotivo(operacion))
            return ([R], rol.Opcionales);
        return (rol.Exigidas, rol.Opcionales);
    }

    /// <summary>¿La contrapartida de esa operación lleva <c>ReasonCode</c>? (causa de ajuste o razón del kardex).</summary>
    public static bool ContrapartidaExigeMotivo(string operacion) =>
        operacion is OperacionesDeInventario.AjusteNegativo or OperacionesDeInventario.Baja or OperacionesDeInventario.AjusteDeCosto;

    /// <summary>
    /// Los <c>ReasonCode</c> admitidos del rol en la operación: los fijos del rol, las razones del kardex en la
    /// contrapartida de <c>AjusteDeCosto</c>, o nulo si el motivo es una causa de ajuste de Inventario (se comprueba
    /// contra <c>IDimensionesDeInventario</c>).
    /// </summary>
    public static IReadOnlyList<string>? MotivosAdmitidos(string operacion, RolDeCuenta rol) =>
        rol.Codigo == Contrapartida
            ? operacion == OperacionesDeInventario.AjusteDeCosto ? RazonesDeAjusteDeCosto : null
            : rol.MotivosFijos;

    /// <summary>§2.2: en todo rol que nombre <c>Inventario</c>, una línea en bodega de tránsito resuelve <c>Transito</c>.</summary>
    public static string RolDeLaBodega(string rol, WarehouseBehavior comportamiento) =>
        rol == Inventario && comportamiento == WarehouseBehavior.Transit ? Transito : rol;

    /// <summary>
    /// §2.5: la clase de impuesto de la cuenta compatible con la de la tarifa: IVA con <c>Iva</c> y <c>ReteIva</c>;
    /// retención en la fuente con <c>ReteFuente</c>; ICA con <c>Ica</c> y <c>ReteIca</c>; <c>Inc</c> y <c>Other</c> con
    /// cualquier cuenta de impuesto. Una cuenta que no es de impuesto nunca es compatible.
    /// </summary>
    public static bool CuentaCompatibleConTarifa(CuentaTaxKind cuenta, TarifaTaxKind tarifa) =>
        cuenta != CuentaTaxKind.None && tarifa switch
        {
            TarifaTaxKind.Iva or TarifaTaxKind.ReteIva => cuenta == CuentaTaxKind.Vat,
            TarifaTaxKind.ReteFuente => cuenta == CuentaTaxKind.Withholding,
            TarifaTaxKind.Ica or TarifaTaxKind.ReteIca => cuenta == CuentaTaxKind.Ica,
            _ => true,
        };

    /// <summary>El nombre de la dimensión en la API y en los errores.</summary>
    public static string NombreDe(DimensionDeRegla dimension) => dimension switch
    {
        DimensionDeRegla.AccountingGroupCode => "accountingGroupCode",
        DimensionDeRegla.WarehouseCode => "warehouseCode",
        DimensionDeRegla.PointOfSaleCode => "pointOfSaleCode",
        DimensionDeRegla.PaymentMeansCode => "paymentMeansCode",
        DimensionDeRegla.TaxRateCode => "taxRateCode",
        DimensionDeRegla.TaxRate => "taxRate",
        DimensionDeRegla.ReasonCode => "reasonCode",
        DimensionDeRegla.Branch => "branch",
        DimensionDeRegla.CostCenter => "costCenter",
        _ => dimension.ToString(),
    };
}
