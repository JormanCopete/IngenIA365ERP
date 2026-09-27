using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Los datos propios de un movimiento de caja (<c>INV_CashMovementDetails</c>; feature 012, I3, T574; FR-100, T50; data-model
/// §15). El movimiento es un documento de clase <c>CashMovement</c> (1:1): su tipo trae consecutivo y política de aprobación; sólo
/// lo confirmado cuenta en el esperado (<c>CalculadoraDeEsperado</c>). La reclasificación corrige el medio de un pago confirmado
/// sin editarlo (<see cref="ReclassifiedPaymentId"/>, FR-005). Lo fija su documento: confirmado, no cambia.
/// </summary>
public class CashMovementDetail : AuditableEntity
{
    public int DocumentId { get; set; }

    public InventoryDocument? Document { get; set; }

    /// <summary>La sesión de origen.</summary>
    public int CashSessionId { get; set; }

    public CashMovementKind Kind { get; set; }

    /// <summary>El medio que sale de la sesión; en <c>BaseIncome</c>, el que entra (el efectivo).</summary>
    public int SourcePaymentMeansId { get; set; }

    /// <summary>Sólo <c>ReclassificationBetweenMeans</c>: el medio correcto.</summary>
    public int? TargetPaymentMeansId { get; set; }

    /// <summary>La otra punta: a dónde va en los retiros; de dónde viene en <c>BaseIncome</c>; nula en la reclasificación.</summary>
    public CashMovementDestination? Destination { get; set; }

    /// <summary><c>WithdrawalToRegister</c>: la caja que recibe.</summary>
    public int? DestinationCashRegisterId { get; set; }

    /// <summary>La sesión abierta de esa caja, resuelta al confirmar.</summary>
    public int? DestinationCashSessionId { get; set; }

    /// <summary><c>WithdrawalForDeposit</c>: el banco al que se lleva (dato; la consignación es de Tesorería).</summary>
    public int? DepositBankId { get; set; }

    /// <summary>Mayor que cero.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>[{ denominationPublicId, quantity, amount }]</c> informativo.</summary>
    public string? DenominationsJson { get; set; }

    /// <summary>El pago registrado con el medio equivocado (<c>INV_DocumentPayments</c>).</summary>
    public int? ReclassifiedPaymentId { get; set; }

    public string? TargetReference { get; set; }

    public string? TargetAuthorizationCode { get; set; }

    public int? TargetCardTerminalId { get; set; }
}
