using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentAccounts</c> (feature 014, US3, T087; data-model §1.5.1; contracts/documentos.md §1.1): qué cuentas de captación o de aportes
/// cubre un documento emitido, con la última operación y la última fila del libro consideradas y el saldo mostrado. Gemela de
/// <c>LND_IssuedDocumentLoans</c>: la anulación por reverso de una operación de cuenta la usa para saber qué documentos tocar. El título del CDAT suma su
/// versión de condiciones con K9 (data-model §0.9). Hecho de sólo inserción (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class IssuedDocumentAccount : AuditableEntity, IHechoInmutable
{
    public int IssuedDocumentId { get; init; }

    public int MemberAccountId { get; init; }

    public long? LastOperationId { get; init; }

    public long? LastTransactionId { get; init; }

    public decimal? BalanceAtCutoff { get; init; }

    /// <summary>Feature 014, US13 (T194; data-model §1.5.1): título de CDAT: la versión de condiciones que titula.</summary>
    public int? TermsVersionId { get; init; }

    /// <summary>Título: el título anterior al que reemplaza una prórroga o renovación (FR-068, K9).</summary>
    public int? PreviousIssuedDocumentId { get; init; }
}
