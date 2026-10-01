using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.Payments;

/// <summary>
/// Un medio de pago de la cooperativa (<c>COR_PaymentMeans</c>; feature 012, I3, T573; FR-096, T25; data-model §16). Catálogo
/// configurable sin programar: la <see cref="Class"/> es lo único que el código conoce (vueltas, crédito, forma de pago DIAN,
/// arqueo por defecto; <c>ClasesDeMedio</c>) y todo lo demás es dato. <see cref="Code"/> no cambia (dimensión
/// <c>PaymentMeansCode</c> de la matriz, T27); la clase, la red, el adquirente y el método de arqueo no cambian una vez tiene
/// pagos (<c>Core.PaymentMeans.InUse</c>). Modificarlo exige motivo (<c>IConMotivo</c>) y el historial es el diff de auditoría;
/// la tolerancia y la comisión se copian al arqueo y al pago, así que un cambio rige para lo que se cierre o cobre después.
/// <c>COR_PaymentMethods</c> no se toca ni se usa.
/// </summary>
public class PaymentMeans : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public short DisplayOrder { get; set; }

    /// <summary>Tecla rápida del panel de cobro; única entre activos (regla del comando).</summary>
    public string? QuickKey { get; set; }

    public PaymentMeansClass Class { get; set; }

    /// <summary>Obligatorio en tarjetas.</summary>
    public int? CardNetworkId { get; set; }

    public CardNetwork? CardNetwork { get; set; }

    /// <summary>Obligatorio en tarjetas: el medio admite los datáfonos de su adquirente.</summary>
    public int? CardAcquirerId { get; set; }

    public CardAcquirer? CardAcquirer { get; set; }

    /// <summary><c>COR_Banks</c>: consignación y transferencia.</summary>
    public int? BankId { get; set; }

    /// <summary>La cuenta de la cooperativa como <b>dato</b>, no como cuenta contable.</summary>
    public string? DestinationAccountNumber { get; set; }

    /// <summary>1 ahorros, 2 corriente (el código de <c>PAY_Employees.PayrollBankAccountType</c>).</summary>
    public byte? DestinationAccountType { get; set; }

    public bool RequiresReference { get; set; }

    /// <summary>Obligatorio si <see cref="RequiresReference"/>.</summary>
    public PaymentReferenceKind? ReferenceKind { get; set; }

    public byte? ReferenceMinLength { get; set; }

    public byte? ReferenceMaxLength { get; set; }

    /// <summary>Sólo si la clase es efectivo.</summary>
    public bool AllowsChange { get; set; }

    public bool AllowsPartial { get; set; } = true;

    /// <summary>Un número no se usa dos veces (bonos): <c>INV_VoucherRedemptions</c>.</summary>
    public bool UniqueReference { get; set; }

    public CashCountMethod CountMethod { get; set; }

    /// <summary>El arqueo exige el lote de cada datáfono.</summary>
    public bool RequiresTerminalBatchAtClose { get; set; }

    /// <summary>Tolerancia de arqueo del medio (FR-012); se copia a cada línea de arqueo al cerrar.</summary>
    public decimal ToleranceAmount { get; set; }

    /// <summary>Fracción informativa (FR-101); se copia al pago como <c>ExpectedCommissionAmount</c>.</summary>
    public decimal? ExpectedCommissionRate { get; set; }

    public decimal? ExpectedCommissionFixed { get; set; }

    /// <summary>Código de medio de pago DIAN (10, 48, 49, 42, 47, 20, 71…), validado por la contadora (A8).</summary>
    public string DianPaymentMeansCode { get; set; } = string.Empty;

    /// <summary>La opción «todos» de cada conjunto de disponibilidad (<c>DisponibilidadDeMedio</c>).</summary>
    public bool OfferedAtAllPointsOfSale { get; set; }

    public bool OfferedInAllChannels { get; set; }

    public bool OfferedForAllDocumentTypes { get; set; }

    /// <summary>Sólo créditos: plazo propuesto y máximo.</summary>
    public short? DefaultTermDays { get; set; }

    public short? MaxTermDays { get; set; }

    public short? DefaultInstallments { get; set; }

    public short? MaxInstallments { get; set; }

    /// <summary>Periodicidad de las cuotas.</summary>
    public short? InstallmentPeriodDays { get; set; }

    /// <summary>Código de línea de Cartera sugerida, como texto: sin llave a <c>LND_*</c> (T32).</summary>
    public string? SuggestedCreditLineCode { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? Notes { get; set; }

    /// <summary>La comisión esperada de un pago de <paramref name="valor"/> (informativa, FR-101); nula sin tasa ni fijo.</summary>
    public decimal? ComisionEsperada(decimal valor) =>
        ExpectedCommissionRate is null && ExpectedCommissionFixed is null
            ? null
            : Math.Round(valor * (ExpectedCommissionRate ?? 0m) + (ExpectedCommissionFixed ?? 0m), 2, MidpointRounding.AwayFromZero);
}
