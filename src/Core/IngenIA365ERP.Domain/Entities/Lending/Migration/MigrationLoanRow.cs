using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoans</c> (feature 013, K1; data-model §29.3, parte <c>loans</c>): fila de preparación de un crédito de origen.
/// Hecho (<see cref="IHechoInmutable"/>, C46 sin diff de auditoría): una tanda reemplazada conserva sus filas. Al activar nace
/// el crédito (origen <c>Migration</c>) con condiciones, plan, controles, vínculo de pagaduría y preferencia. Las columnas siguen a la
/// plantilla (plantillas.md §5.3.1).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationLoanRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>Número del crédito de origen; llave natural de la parte.</summary>
    public string SourceLoanNumber { get; init; } = string.Empty;

    public int PersonId { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>La línea resuelta desde <see cref="CreditLineCode"/>; nula mientras no se resuelva.</summary>
    public int? CreditLineId { get; init; }

    public Guid OfficePublicId { get; init; }

    /// <summary>Factura de la venta de origen, si el crédito nació en Comercial (FR-055).</summary>
    public string? InvoiceNumber { get; init; }

    public DateOnly DisbursementDate { get; init; }

    public decimal OriginalAmount { get; init; }

    /// <summary>
    /// JSON de las condiciones: tasa o índice y margen, mora, sistema, periodicidad, plazo, convención, equivalencia, redondeo,
    /// prelación, seguros y gracia (las columnas de condiciones de la plantilla, plantillas.md §5.3.1). El pagaré viaja en la
    /// parte <c>collaterals</c>.
    /// </summary>
    public string TermsJson { get; init; } = "{}";

    /// <summary>Preferencia de imputación del prepago.</summary>
    public PrepaymentChoice? PrepaymentChoice { get; init; }

    public RepaymentChannel RepaymentChannel { get; init; }

    public int? PaymasterPersonId { get; init; }

    public string? EmployeeIdAtPaymaster { get; init; }

    public DateOnly? LibranzaDate { get; init; }

    /// <summary>Etapa de cobranza del sistema de origen (<c>etapaDeCobranza</c>); se registra al desplegar K4 (C64).</summary>
    public string? CollectionStageCode { get; init; }

    /// <summary>Asesor de la colocación (<c>asesor</c>, FR-133); una persona del maestro.</summary>
    public int? AdvisorPersonId { get; init; }

    /// <summary>El crédito está en cobro jurídico (<c>enJuridico</c>).</summary>
    public bool InLegalCollection { get; init; }

    /// <summary>Abogado a cargo del cobro jurídico (<c>abogado</c>); una persona del maestro.</summary>
    public int? LawyerPersonId { get; init; }

    /// <summary>Notas libres de la fila (<c>notas</c>, hasta 400).</summary>
    public string? Notes { get; init; }
}
