using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// El consecutivo de un tipo de documento con un prefijo y una vigencia (<c>INV_DocumentSequences</c>; feature 012,
/// T16, FR-038; data-model §5.9). A lo sumo una vigente por tipo a una fecha, compartida por todas las cajas que usan
/// el tipo. Cambiar de prefijo es una fila nueva que cierra la anterior la víspera; volver a un prefijo usado reabre su
/// fila. <see cref="NextValue"/> se fija al crear (para continuar la numeración de SOLIDO) y después sólo lo cambia
/// <c>Numerador</c>, dentro de la transacción de confirmación y con la fila bloqueada al final del cerrojo
/// (<c>SoloElNumeradorNumera</c>). Nunca se llama <c>NextNumber</c> (§2.1).
/// </summary>
public class DocumentSequence : AuditableEntity
{
    public int DocumentTypeId { get; set; }

    public InventoryDocumentType? DocumentType { get; set; }

    /// <summary>Puede ser <c>''</c>.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>El próximo número a asignar, ≥ 1.</summary>
    public long NextValue { get; set; } = 1;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>¿Vigente en <paramref name="fecha"/>?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || fecha <= ValidTo.Value);
}
