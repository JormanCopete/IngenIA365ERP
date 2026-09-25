using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Exceptions;

/// <summary>
/// Se intentó una transición que el ciclo del documento de inventario no tiene (feature 012, data-model §5.1). Es un
/// error de programación: los comandos verifican el estado antes y responden su código (<c>Inventory.Document.NotDraft</c>,
/// <c>.NotConfirmed</c>…). (nuevo)
/// </summary>
public sealed class InvalidDocumentTransitionException : InvalidOperationException
{
    public DocumentStatus From { get; }
    public DocumentStatus To { get; }

    public InvalidDocumentTransitionException(DocumentStatus from, DocumentStatus to)
        : base($"Transición de documento inválida: {from} → {to}.")
    {
        From = from;
        To = to;
    }
}
