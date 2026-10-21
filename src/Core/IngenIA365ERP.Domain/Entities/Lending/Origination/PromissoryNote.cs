using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_PromissoryNotes</c> (feature 013; data-model §2.9; FR-044, FR-045, T73): pagaré físico o desmaterializado, con canal de
/// custodia sellado e id externo (F-19). Al menos uno de <see cref="ApplicationId"/> y <see cref="LoanId"/>. Desembolsar exige
/// <c>Signed</c> (físico) o <c>InCustody</c> (desmaterializado). No es hecho.
/// </summary>
public class PromissoryNote : AuditableEntity
{
    public PromissoryNoteKind Kind { get; set; }

    public int? ApplicationId { get; set; }

    public int? LoanId { get; set; }

    /// <summary>Número del pagaré o del depósito.</summary>
    public string? Number { get; set; }

    public DateOnly IssuedOn { get; set; }

    public PromissoryNoteStatus Status { get; set; } = PromissoryNoteStatus.Created;

    /// <summary>Canal del depósito (desmaterializado).</summary>
    public string? CustodianChannelCode { get; set; }

    /// <summary>Id del pagaré en el depósito (desmaterializado); único cuando no es nulo.</summary>
    public string? ExternalNoteId { get; set; }

    public bool IsSimulated { get; set; }

    public DateTime? SignedAt { get; set; }

    public DateTime? InCustodyAt { get; set; }

    public DateTime? EndorsedAt { get; set; }

    public string? EndorseeName { get; set; }

    /// <summary>Custodia física.</summary>
    public string? PhysicalLocation { get; set; }

    /// <summary>Carta de instrucciones (adjunto, sin FK).</summary>
    public Guid? InstructionsAttachmentPublicId { get; set; }

    /// <summary>Copia del pagaré (adjunto, sin FK).</summary>
    public Guid? DocumentAttachmentPublicId { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>Devolución o anulación tras la firmeza.</summary>
    public DateOnly? ReleasedOn { get; set; }

    /// <summary>Última sincronización de la tarea <c>cartera.pagares</c>.</summary>
    public DateTime? LastSyncedAt { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0432) --

    /// <summary>
    /// Firmado o en custodia (FR-044; data-model §2.9): el físico firmado o en custodia, el desmaterializado en custodia del depósito. Si un
    /// simulado tiene efecto legal lo decide aparte <c>GuardiaDeCanalesExternos</c> (T73).
    /// </summary>
    public bool Perfeccionado => !IsDeleted && Kind switch
    {
        PromissoryNoteKind.Physical => Status is PromissoryNoteStatus.Signed or PromissoryNoteStatus.InCustody,
        PromissoryNoteKind.Dematerialized => Status == PromissoryNoteStatus.InCustody,
        _ => false,
    };

    /// <summary>Pasa el pagaré de la solicitud al crédito que nació (nulo → valor, una vez).</summary>
    public void AsignarCredito(int loanId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(loanId, 1);
        if (LoanId is { } actual && actual != loanId)
            throw new InvalidOperationException($"El pagaré {PublicId} ya respalda otro crédito.");
        LoanId = loanId;
    }
}
