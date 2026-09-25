using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Cómo corre una importación (feature 012, T49; contracts/plantillas.md §0.5). <see cref="Review"/> corre las mismas
/// reglas que <see cref="Apply"/> y no guarda nada; <see cref="Apply"/> es todo o nada. Viaja por nombre
/// (<c>"mode": "Review"</c>), como lo muestra el contrato.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModoDeImportacion
{
    Review = 0,
    Apply = 1,
}

/// <summary>Qué le pasa a una fila del archivo (nuevo): la columna <c>resultado</c> de la revisión en Excel dice «Crear», «Actualizar» o «Sin cambio».</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccionDeImportacion
{
    Create = 0,
    Update = 1,
    Unchanged = 2,
}
