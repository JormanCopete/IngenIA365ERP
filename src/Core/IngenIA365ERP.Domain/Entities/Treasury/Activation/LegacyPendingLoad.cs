using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Activation;

/// <summary>
/// <c>TES_LegacyPendingLoads</c> (feature 015, T2, fase 40, T0441; data-model §13.2, §24; FR-161): lo pendiente del módulo heredado de Tesorería que se carga antes de su
/// retiro —los cheques girados sin cobrar (que pasan a cheques en tránsito de T0) y las facturas por pagar (que pasan a órdenes por pagar de T2)—, cotejado con las
/// cifras de referencia. Se crea en la base de T2 para poder cargar después; la plantilla <c>tesoreria.pendientes-heredados</c> y el retiro son de la fase 46. Guarda la
/// referencia del heredado como texto (no hay FK hacia <c>TRS_</c>). (nuevo)
/// </summary>
public class LegacyPendingLoad : AuditableEntity
{
    public LegacyPendingKind Kind { get; set; }

    /// <summary>El identificador de la fila heredada (el número del cheque o de la factura), para cotejar.</summary>
    public string LegacyReference { get; set; } = string.Empty;

    /// <summary>La cuenta propia del cheque (los cheques sin cobrar).</summary>
    public int? BankAccountId { get; set; }

    public long? CheckNumber { get; set; }

    public int? BeneficiaryPersonId { get; set; }

    public string? BeneficiaryName { get; set; }

    public decimal Amount { get; set; }

    /// <summary>La fecha de emisión del cheque o la de vencimiento de la factura.</summary>
    public DateOnly Date { get; set; }

    public string? InvoiceNumber { get; set; }

    public LegacyPendingLoadState Status { get; set; } = LegacyPendingLoadState.Loaded;

    /// <summary>Lo que nació en Tesorería al migrarla (el cheque girado o la orden de pago), por su <c>PublicId</c>.</summary>
    public Guid? MigratedToPublicId { get; set; }

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    public string? Note { get; set; }
}
