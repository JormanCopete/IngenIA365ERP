using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Exceptions;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// La cabecera genérica de las 34 clases de documento (<c>INV_Documents</c>; feature 012, T17; data-model §5.1). El
/// comportamiento de cada clase vive en <c>ClasesDeDocumento</c> y en su estrategia de <c>Inventory/Documents/Efectos</c>;
/// el tipo (<see cref="DocumentTypeId"/>) sólo elige la clase y la parametriza.
///
/// <para>
/// Ciclo (FR-005, FR-010, T33): <c>Draft → PendingApproval → Confirmed → Voided</c>, <c>PendingApproval → Draft</c> al
/// rechazar o retirar, y <c>Draft → Discarded</c> con motivo. Las transiciones sólo ocurren por los métodos de abajo;
/// cualquier otra lanza <see cref="InvalidDocumentTransitionException"/>. Confirmado, el documento queda fijo
/// (<see cref="IInmutableTrasConfirmar"/>): lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>.
/// </para>
/// <para>
/// <see cref="Prefix"/> y <see cref="Number"/> los asigna sólo <c>Numerador</c> (o <c>NumeradorFiscal</c>, I4); lo vigila
/// <c>SoloElNumeradorNumera</c>. Las FK hacia bodegas, ubicaciones, productos, unidades, canales y causas son
/// columnas <c>int</c> sin navegación: las declara la configuración de esas entidades (US1). Las columnas de I3
/// (punto, caja, sesión, suspensión) y de I5 (<c>AllocationMethod</c>, <c>BalanceClosed*</c>, <c>ExpectedDate</c>) las
/// agrega su entrega.
/// </para>
/// </summary>
public class InventoryDocument : AuditableEntity, IInmutableTrasConfirmar
{
    public const int MaxLineas = 4000;
    public const string MonedaPorDefecto = "COP";

    public DocumentClass Class { get; set; }

    public int DocumentTypeId { get; set; }

    public InventoryDocumentType? DocumentType { get; set; }

    /// <summary><c>''</c> en borrador; lo fija el numerador.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Nulo en borrador, en aprobación y en descartado.</summary>
    public long? Number { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public DateOnly OperationDate { get; set; }

    public DateTime? ConfirmedAt { get; private set; }

    public int CreatedByUserId { get; set; }

    public int? ConfirmedByUserId { get; private set; }

    public DateTime? DiscardedAt { get; private set; }

    public int? DiscardedByUserId { get; private set; }

    public string? DiscardReason { get; private set; }

    public int? WarehouseId { get; set; }

    public int? DestinationWarehouseId { get; set; }

    public int? TransitWarehouseId { get; set; }

    public int BranchId { get; set; }

    public int? CostCenterId { get; set; }

    public int? CounterpartyPersonId { get; set; }

    public int? SalespersonId { get; set; }

    public int? SalesChannelId { get; set; }

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public string Currency { get; set; } = MonedaPorDefecto;

    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Sellado al confirmar (data-model §5.3); nulo en las clases sin mensajes a Contabilidad.</summary>
    public PostingMode? PostingMode { get; set; }

    public string? Reason { get; set; }

    /// <summary>En la anulación: el documento anulado.</summary>
    public int? VoidsDocumentId { get; set; }

    /// <summary>En el anulado: su anulación.</summary>
    public int? VoidedByDocumentId { get; private set; }

    public bool FiscalNumberReleased { get; set; }

    /// <summary>Compras: municipio de la operación para ReteICA (T24).</summary>
    public string? OperationMunicipalityDaneCode { get; set; }

    // ---- totales (T26) ----
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal WithholdingTotal { get; set; }
    public decimal Total { get; set; }
    public decimal AmountDue { get; set; }
    public decimal CostTotal { get; set; }

    // ---- conteo físico (§8) ----
    public CountKind? CountKind { get; set; }
    public CountScope? CountScope { get; set; }
    public string? CountScopeJson { get; set; }
    public bool IsBlindCount { get; set; }
    public DateTime? CountSnapshotAt { get; set; }
    public long? CountSnapshotKardexEntryId { get; set; }
    public byte? CountRound { get; set; }

    // ---- nacen en I1 y se usan después (§14) ----
    public DateOnly? ValidUntil { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool ReturnsGoods { get; set; }
    public bool IsFullReversal { get; set; }
    public string? CorrectionConceptCode { get; set; }

    public ICollection<InventoryDocumentLine> Lines { get; set; } = new List<InventoryDocumentLine>();

    // ---------------------------------------------------------------------------- transiciones --

    /// <summary>¿Existe la transición <paramref name="de"/> → <paramref name="a"/> en el ciclo del documento (data-model §5.1)?</summary>
    public static bool EsTransicionValida(DocumentStatus de, DocumentStatus a) => (de, a) switch
    {
        (DocumentStatus.Draft, DocumentStatus.PendingApproval) => true,
        (DocumentStatus.Draft, DocumentStatus.Confirmed) => true,
        (DocumentStatus.Draft, DocumentStatus.Discarded) => true,
        (DocumentStatus.PendingApproval, DocumentStatus.Confirmed) => true,
        (DocumentStatus.PendingApproval, DocumentStatus.Draft) => true,
        (DocumentStatus.Confirmed, DocumentStatus.Voided) => true,
        _ => false,
    };

    /// <summary>Confirmar con niveles de aprobación: queda congelado y sin número.</summary>
    public void EnviarAAprobacion() => Pasar(DocumentStatus.PendingApproval);

    /// <summary>Rechazo de un nivel o retiro de la solicitud: vuelve a borrador.</summary>
    public void DevolverABorrador() => Pasar(DocumentStatus.Draft);

    /// <summary>
    /// Confirmar (sin niveles, o por la última aprobación). El número lo asigna antes el numerador en la misma
    /// transacción; <paramref name="confirmadoPor"/> es quien confirmó o dio la última aprobación.
    /// </summary>
    public void Confirmar(int confirmadoPor, DateTime confirmadoEnUtc)
    {
        Pasar(DocumentStatus.Confirmed);
        ConfirmedByUserId = confirmadoPor;
        ConfirmedAt = confirmadoEnUtc;
    }

    /// <summary>Descartar el borrador: queda registrado quién, cuándo y por qué (FR-005). No consume número.</summary>
    public void Descartar(int descartadoPor, DateTime descartadoEnUtc, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Descartar un borrador exige el motivo.", nameof(motivo));
        Pasar(DocumentStatus.Discarded);
        DiscardedByUserId = descartadoPor;
        DiscardedAt = descartadoEnUtc;
        DiscardReason = motivo.Trim();
    }

    /// <summary>Lo pone sólo la confirmación de su anulación (FR-006): referencia el documento contrario.</summary>
    public void MarcarAnulado(int anuladoPorDocumentoId)
    {
        Pasar(DocumentStatus.Voided);
        VoidedByDocumentId = anuladoPorDocumentoId;
    }

    private void Pasar(DocumentStatus a)
    {
        if (!EsTransicionValida(Status, a))
            throw new InvalidDocumentTransitionException(Status, a);
        Status = a;
    }
}
