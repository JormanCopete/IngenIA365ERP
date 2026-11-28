using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Sales.Payments;

/// <summary>
/// Lo único que el código sabe de un medio de pago: su clase (FR-096, T25). Todo lo demás —referencia, vueltas, pago parcial,
/// tolerancia, disponibilidad— es dato del medio. De la clase salen el arqueo por defecto, si admite vueltas, si es tarjeta y si
/// vuelve la venta «a crédito» (forma de pago DIAN). (nuevo)
/// </summary>
public static class ClasesDeMedio
{
    /// <summary>Crédito a asociado o comercial a cliente: la venta queda a crédito y el pago no se arquea.</summary>
    public static bool EsCredito(PaymentMeansClass clase) => clase is PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit;

    /// <summary>Tarjeta de crédito o débito: exige red y adquirente, y se arquea por lote del datáfono.</summary>
    public static bool EsTarjeta(PaymentMeansClass clase) => clase is PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard;

    /// <summary>Sólo el efectivo admite vueltas.</summary>
    public static bool PuedeDarVueltas(PaymentMeansClass clase) => clase == PaymentMeansClass.Cash;

    /// <summary>
    /// 2026-11-27 (feature 028, E4, T086): el cruce interno sin dinero (<see cref="PaymentMeansClass.InternalOffset"/>) es del sistema: su medio lo deja la
    /// semilla, no se crea a mano y nunca se ofrece en la caja ni en el punto de venta. (nuevo)
    /// </summary>
    public static bool EsInterna(PaymentMeansClass clase) => clase == PaymentMeansClass.InternalOffset;

    /// <summary>
    /// Cómo se arquea por defecto (FR-099): efectivo y cheques por conteo físico; tarjetas por total del lote; consignaciones,
    /// transferencias, bonos y otros por referencias; créditos sin arqueo.
    /// </summary>
    public static CashCountMethod ArqueoPorDefecto(PaymentMeansClass clase) => clase switch
    {
        PaymentMeansClass.Cash or PaymentMeansClass.Check => CashCountMethod.PhysicalCount,
        PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard => CashCountMethod.VoucherTotal,
        PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit or PaymentMeansClass.InternalOffset => CashCountMethod.None,
        _ => CashCountMethod.ByReference,
    };
}
