using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_LendingDisbursementFiles</c> (feature 015, US10, T0267; E-01 c; FR-108, FR-164; data-model §10.8): el archivo de desembolsos que <b>Cartera</b> generó y
/// que llegó por <c>ArchivoDeDesembolsosGenerado</c> v1. Tesorería no lo genera ni lo paga: lo guarda con sus órdenes para registrar resultados y conciliar
/// (D16). El anulado (<c>ArchivoDeDesembolsosAnulado</c> v1) lo deja <see cref="LendingDisbursementFileState.Voided"/> y suelta sus órdenes, que esperan otro
/// archivo de Cartera. Sin FK a tablas <c>LND_</c>: el archivo va por su <see cref="FilePublicId"/>. (nuevo)
/// </summary>
public class LendingDisbursementFile : AuditableEntity
{
    public Guid FileMessagePublicId { get; set; }

    public Guid FilePublicId { get; set; }

    public string FileNumber { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public Guid AttachmentPublicId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileSha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? FormatCode { get; set; }

    /// <summary>La cuenta propia de Tesorería que casa con la cuenta de origen del archivo (banco y número), si casó.</summary>
    public int? BankAccountId { get; set; }

    public string? SourceBankCode { get; set; }

    public string? SourceAccountNumber { get; set; }

    public decimal Total { get; set; }

    public int LineCount { get; set; }

    public LendingDisbursementFileState Status { get; set; } = LendingDisbursementFileState.Generated;

    public Guid? VoidMessagePublicId { get; set; }

    public string? VoidReason { get; set; }

    public DateTime? VoidedAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string GeneratedByName { get; set; } = string.Empty;

    public List<LendingDisbursementFileOrder> Orders { get; set; } = [];
}

/// <summary><c>TES_LendingDisbursementFileOrders</c>: una orden que cubre el archivo de Cartera, con la orden de Tesorería que casa (si ya llegó). (nuevo)</summary>
public class LendingDisbursementFileOrder : AuditableEntity
{
    public int LendingDisbursementFileId { get; set; }

    public LendingDisbursementFile? LendingDisbursementFile { get; set; }

    public Guid OriginOrderPublicId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal NetAmount { get; set; }

    public Guid PayeePersonPublicId { get; set; }

    public int? PaymentOrderId { get; set; }
}
