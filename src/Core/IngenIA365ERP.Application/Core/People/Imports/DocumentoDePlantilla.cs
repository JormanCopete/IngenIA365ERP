using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Domain.Common.Identidad;

namespace IngenIA365ERP.Application.Core.People.Imports;

/// <summary>
/// El tipo y el número de documento de una fila de plantilla, validados por <see cref="UsuarioDeDocumento"/> (feature 016): de ahí sale
/// el usuario que es la llave de la persona. Lo comparten <c>core.people</c> y <c>core.associates</c>. (nuevo)
/// </summary>
public static class DocumentoDePlantilla
{
    /// <summary>El documento de la fila, o nulo si le faltaba algo o no es válido (el error queda en la fila).</summary>
    public static DocumentoValidado? Leer(FilaDeImportacion fila, string columnaTipo, string columnaNumero)
    {
        ArgumentNullException.ThrowIfNull(fila);
        var tipo = fila.Texto(columnaTipo, 3);
        var numero = fila.Texto(columnaNumero, 20);
        if (numero is null) return null;
        // 2026-11-23: con número y sin tipo la fila no se puede leer y lo tiene que decir; antes devolvía nulo sin error y quien llamaba
        // la saltaba en silencio (la carga de vehículos de COOFLOPAL respondió «aplicado» sin crear ninguno).
        if (tipo is null)
        {
            fila.Requerido(columnaTipo);
            return null;
        }

        var validado = UsuarioDeDocumento.Validar(tipo, numero, soloDeAcceso: false, out var error);
        if (validado is not null) return validado;

        var columna = TiposDeDocumento.PorCodigo(tipo) is null && TiposDeDocumento.PorCodigoDePersona(tipo) is null ? columnaTipo : columnaNumero;
        // «C», «NI», «PT»… son los códigos con que la persona guarda el tipo: se aceptan igual que «CC», «NIT», «PPT».
        if (TiposDeDocumento.PorCodigoDePersona(tipo) is { } tipoDePersona)
            validado = UsuarioDeDocumento.Validar(tipoDePersona.Codigo, numero, soloDeAcceso: false, out error);
        if (validado is null) fila.Error(columna, ImportErrors.CellFormat, error ?? "Documento no válido.");
        return validado;
    }
}
