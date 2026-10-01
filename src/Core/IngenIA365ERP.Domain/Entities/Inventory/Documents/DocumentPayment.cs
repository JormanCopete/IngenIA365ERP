using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un pago de un documento de venta o de una nota (<c>INV_DocumentPayments</c>; feature 012, I3, T577; FR-056, FR-097, FR-101,
/// T25, T26, T32, T50; data-model §16): N pagos por documento, en reemplazo de las columnas fijas del sistema anterior. En un
/// borrador de oficina se editan; el POS los manda al cobrar y entran en la transacción que confirma. Confirmado, el pago es
/// inmutable: un medio mal registrado se corrige con un movimiento <c>ReclassificationBetweenMeans</c> (FR-005). Guarda
/// <b>copias</b> de lo que importa del medio (código, nombre, clase, red, adquirente y su tercero, banco, código DIAN, comisión
/// esperada), de modo que cambiar el medio después no reescribe la historia. De la tarjeta sólo guarda <see cref="Last4"/> y la
/// autorización, <b>nunca</b> el número (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>). Todo pago cuyo medio se arquea lleva su
/// sesión de caja, también en oficina (T50). Único <c>(DocumentId, LineNumber)</c> entre vivos.
/// </summary>
public class DocumentPayment : AuditableEntity
{
    public int DocumentId { get; set; }

    public InventoryDocument? Document { get; set; }

    public short LineNumber { get; set; }

    /// <summary>El mismo medio puede repetirse con referencias distintas.</summary>
    public int PaymentMeansId { get; set; }

    public PaymentMeans? PaymentMeans { get; set; }

    /// <summary>Ventas <c>Received</c>; notas y anulaciones <c>Refunded</c>.</summary>
    public PaymentDirection Direction { get; set; } = PaymentDirection.Received;

    /// <summary>Lo aplicado; mayor que cero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Sólo en medios que admiten vueltas.</summary>
    public decimal? AmountTendered { get; set; }

    /// <summary>Entregado − aplicado.</summary>
    public decimal? ChangeGiven { get; set; }

    /// <summary>Obligatoria según el medio; se rechaza si parece un número de tarjeta.</summary>
    public string? Reference { get; set; }

    /// <summary>Mayúsculas, sin espacios ni guiones; la usa <c>INV_VoucherRedemptions</c>.</summary>
    public string? NormalizedReference { get; set; }

    /// <summary>Aprobación de la red.</summary>
    public string? AuthorizationCode { get; set; }

    /// <summary><c>COR_CardTerminals</c> del adquirente del medio; se propone el de la caja.</summary>
    public int? CardTerminalId { get; set; }

    /// <summary>Opcional al cobrar; el arqueo lo exige si el medio lo pide.</summary>
    public string? TerminalBatchNumber { get; set; }

    /// <summary>Los últimos cuatro dígitos (<c>char(4)</c>); nunca el número completo.</summary>
    public string? Last4 { get; set; }

    /// <summary>Obligatoria si el medio se arquea; la devolución en efectivo sale de la sesión donde se devuelve.</summary>
    public int? CashSessionId { get; set; }

    public string MeansCode { get; set; } = string.Empty;

    public string MeansName { get; set; } = string.Empty;

    public PaymentMeansClass MeansClass { get; set; }

    public string? CardNetworkCode { get; set; }

    public string? CardAcquirerCode { get; set; }

    /// <summary>Copia del tercero de la cuenta por cobrar a la red.</summary>
    public int? CardAcquirerPersonId { get; set; }

    public int? BankId { get; set; }

    public string DianPaymentMeansCode { get; set; } = string.Empty;

    /// <summary><see cref="Amount"/> × tasa + fijo del medio al confirmar (informativo, FR-101).</summary>
    public decimal? ExpectedCommissionAmount { get; set; }

    /// <summary>En una nota: el pago de la venta que se reintegra (por defecto el mismo medio).</summary>
    public int? RefundsPaymentId { get; set; }

    /// <summary>Sólo clases crédito, dentro de los máximos del medio.</summary>
    public short? CreditTermDays { get; set; }

    public short? InstallmentCount { get; set; }

    public short? InstallmentPeriodDays { get; set; }

    public DateOnly? FirstDueDate { get; set; }

    public DateOnly? FinalDueDate { get; set; }

    /// <summary>Copia del medio.</summary>
    public string? SuggestedCreditLineCode { get; set; }

    /// <summary>Mientras IC esté pendiente, siempre verdadero en crédito.</summary>
    public bool PendingValidation { get; set; }

    public CreditOrigin? CreditOrigin { get; set; }

    /// <summary>Copia sellada de <c>Cartera.CuentaPorCobrarRegistradaPor</c> (<c>Contabilidad</c> forzado mientras IC está pendiente).</summary>
    public string? AccountsReceivableRecordedBy { get; set; }

    /// <summary><c>COR_ApprovalRequests</c>: la aprobación del crédito provisional (sujeto <c>ProvisionalCredit</c>).</summary>
    public int? ApprovalRequestId { get; set; }

    /// <summary>¿Un pago de clase crédito? (la venta es a crédito y lleva vencimiento).</summary>
    public bool EsCredito => ClasesDeMedio.EsCredito(MeansClass);

    /// <summary>Copia del medio lo que el pago guarda de él al confirmar (la comisión esperada, sobre el valor aplicado).</summary>
    public void CopiarDelMedio(PaymentMeans medio)
    {
        ArgumentNullException.ThrowIfNull(medio);
        PaymentMeansId = medio.Id;
        MeansCode = medio.Code;
        MeansName = medio.Name;
        MeansClass = medio.Class;
        CardNetworkCode = medio.CardNetwork?.Code;
        CardAcquirerCode = medio.CardAcquirer?.Code;
        CardAcquirerPersonId = medio.CardAcquirer?.PersonId;
        BankId = medio.BankId;
        DianPaymentMeansCode = medio.DianPaymentMeansCode;
        SuggestedCreditLineCode = ClasesDeMedio.EsCredito(medio.Class) ? medio.SuggestedCreditLineCode : null;
        ExpectedCommissionAmount = medio.ComisionEsperada(Amount);
    }
}
