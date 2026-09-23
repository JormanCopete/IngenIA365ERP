namespace IngenIA365ERP.Domain.Enums.Core;

/// <summary>
/// Dónde está un adjunto en su ciclo (feature 011, data-model.md). El borrado no es un estado: es la
/// baja lógica de siempre (<c>IsDeleted</c>), que además retira el objeto a la papelera de 90 días.
/// </summary>
public enum EstadoDeAdjunto
{
    /// <summary>Hay una autorización de subida emitida; el archivo puede estar viajando al almacén.</summary>
    Uploading = 1,

    /// <summary>Llegó, su tamaño y su contenido coinciden con lo declarado, y se puede bajar.</summary>
    Available = 2,

    /// <summary>
    /// Llegó pero no es lo que dijo ser. Nunca fue aceptado: su objeto se retiró a la papelera y la
    /// fila queda visible con el motivo (FR-001, FR-014).
    /// </summary>
    Rejected = 3,

    /// <summary>La autorización venció y el archivo no llegó. Se reintenta o se borra; no se borra solo.</summary>
    Incomplete = 4,
}
