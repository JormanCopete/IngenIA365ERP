using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 012 (decisiones-transversales T17, T18; data-model §0): un documento que se edita mientras es borrador
/// y queda <b>fijo al confirmarse</b> (Principio XI). Lo que estaba mal en un confirmado no se corrige
/// reescribiéndolo: se anula con su documento contrario (<c>Voiding</c>) o se corrige con su nota.
///
/// <para>
/// Una vez <see cref="DocumentStatus.Confirmed"/> (o <see cref="DocumentStatus.Voided"/>) sólo pueden cambiar:
/// </para>
/// <list type="bullet">
/// <item><c>Status</c>, y sólo de <c>Confirmed</c> a <c>Voided</c> (lo pone la confirmación de su anulación);</item>
/// <item><c>VoidedByDocumentId</c>: el documento que lo anuló;</item>
/// <item><c>FiscalNumberReleased</c>: la liberación del número del rechazado en el caso b de FR-066;</item>
/// <item>las columnas de auditoría (<c>UpdatedAt</c>, <c>UpdatedBy</c>, <c>RowVersion</c>).</item>
/// </list>
/// <para>
/// Sus líneas quedan igual de fijas. No se da de baja (ni lógica ni física). Es sólo un marcador con lo que el guardián
/// necesita leer; quien lo hace cumplir es <c>ApplicationDbContext.SaveChangesAsync</c> (T137) y la prueba
/// <c>LosHechosInmutablesNoSeModifican</c>. Lo implementa <c>InventoryDocument</c>.
/// </para>
/// </summary>
public interface IInmutableTrasConfirmar
{
    /// <summary>Las propiedades que un documento confirmado todavía puede cambiar (además de <c>Status</c> a <c>Voided</c>).</summary>
    static readonly IReadOnlySet<string> PropiedadesMutablesTrasConfirmar = new HashSet<string>(StringComparer.Ordinal)
    {
        "Status",
        "VoidedByDocumentId",
        "FiscalNumberReleased",
        nameof(AuditableEntity.UpdatedAt),
        nameof(AuditableEntity.UpdatedBy),
        nameof(BaseEntity.RowVersion),
    };

    /// <summary>El estado del documento: el guardián mira el <b>original</b> (el que se leyó de la base).</summary>
    DocumentStatus Status { get; }

    /// <summary>¿Este estado ya fija el documento?</summary>
    static bool EstaFijo(DocumentStatus estado) => estado is DocumentStatus.Confirmed or DocumentStatus.Voided;
}
