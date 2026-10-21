using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementFiles</c> (feature 013; data-model §7.6; FR-047, FR-149, T78): archivo plano de transferencias de desembolso con
/// el motor común (<c>BankFileScope.LoanDisbursement</c>, <c>FlatFileWriter</c>). Molde de <c>PAY_BankDisbursementFiles</c>. Hecho de
/// sólo inserción (<see cref="IHechoInmutable"/>, C42) con la anulación en <see cref="EscrituraUnicaAttribute"/>; el estado (generado
/// o anulado) se deriva. Anularlo libera <c>CurrentDisbursementFileId</c> de sus órdenes.
/// </summary>
public class DisbursementFile : AuditableEntity, IHechoInmutable
{
    /// <summary>Numerador <c>DisbursementFile</c>; único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Banco del formato.</summary>
    public int BankId { get; init; }

    /// <summary>Formato vigente del ámbito <c>LoanDisbursement</c>.</summary>
    public int FormatId { get; init; }

    public string FormatCode { get; init; } = string.Empty;

    public DateOnly PaymentDate { get; init; }

    public int DailySequence { get; init; }

    public string? Reference { get; init; }

    /// <summary>Cuenta bancaria del plan que informa <c>IContabilidadParaCartera.CuentasBancariasAsync</c>; dato, sin FK.</summary>
    public Guid SourceAccountPublicId { get; init; }

    public string SourceAccountNumber { get; init; } = string.Empty;

    public int LineCount { get; init; }

    public decimal TotalAmount { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>Adjunto <c>LendingDisbursementFile</c>, no borrable, subido antes de guardar la fila (sin FK).</summary>
    public Guid FileAttachmentPublicId { get; init; }

    public DateTime GeneratedAt { get; init; }

    public int GeneratedByUserId { get; init; }

    [EscrituraUnica]
    public DateTime? VoidedAt { get; set; }

    [EscrituraUnica]
    public int? VoidedByUserId { get; set; }

    [EscrituraUnica]
    public string? VoidReason { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0433) --

    /// <summary>El estado derivado (data-model §7.6, sin columna): anulado si tiene la anulación escrita.</summary>
    public bool Anulado => VoidedAt is not null;

    /// <summary>Anula el archivo una sola vez (escritura única); liberar sus órdenes lo hace quien anula.</summary>
    public void Anular(int usuarioId, DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Anulado) throw new InvalidOperationException($"El archivo {Number} ya está anulado.");
        VoidedAt = instante;
        VoidedByUserId = usuarioId;
        VoidReason = motivo.Trim();
    }
}
