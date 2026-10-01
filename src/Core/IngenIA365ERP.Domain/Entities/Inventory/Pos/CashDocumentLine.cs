using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Una línea del documento de diferencia de arqueo (<c>INV_CashDocumentLines</c>; feature 012, I3, T574; FR-099, T50;
/// data-model §15 «Diferencias»): una por medio que difiere, también las aceptadas dentro de la tolerancia (F3). Único
/// <c>(DocumentId, LineNumber)</c>. <see cref="Treatment"/> es el <c>ReasonCode</c> de la matriz (§2.7) y con
/// <c>ShortageToCashier</c> el cajero como persona es el tercero de la cuenta por cobrar. La arma <c>EvaluadorDeArqueo</c>; la
/// fija su documento (se reemplaza sólo en un reconteo, mientras está en borrador).
/// </summary>
public class CashDocumentLine : AuditableEntity
{
    public int DocumentId { get; set; }

    public InventoryDocument? Document { get; set; }

    public short LineNumber { get; set; }

    public int CashCountLineId { get; set; }

    public int PaymentMeansId { get; set; }

    /// <summary>+1 sobrante, −1 faltante.</summary>
    public short Sign { get; set; }

    /// <summary>Mayor que cero.</summary>
    public decimal Amount { get; set; }

    public CashDifferenceTreatment Treatment { get; set; }

    public bool WithinTolerance { get; set; }

    public int CashierUserId { get; set; }

    /// <summary>Obligatorio con <c>ShortageToCashier</c>.</summary>
    public int? CashierPersonId { get; set; }

    public string Reason { get; set; } = string.Empty;
}
