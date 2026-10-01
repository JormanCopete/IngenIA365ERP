using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Catalogos;

/// <summary>
/// El código que la persona escribe en un catálogo (EPS, banco, centro de costo…):
/// alfanumérico, sin espacios, en mayúsculas. Es la nomenclatura de cada cooperativa,
/// no un identificador del sistema —para eso están <c>Id</c> y <c>PublicId</c>—, pero
/// tiene que ser único dentro de su tabla para que sirva como referencia.
/// Hasta el 2026-09-12 los catálogos de nómina lo exigían numérico y los de Core ni lo
/// mostraban.
/// </summary>
public static class CodigoDeCatalogo
{
    public const int LargoCorto = 10;
    public const int LargoLargo = 20;

    /// <summary>Letras, dígitos, punto, guion y guion bajo. Sin espacios ni tildes: es una clave, no un nombre.</summary>
    public const string Patron = "^[A-Za-z0-9._-]+$";

    public const string MensajeDePatron = "El código sólo admite letras, dígitos, punto, guion y guion bajo, sin espacios.";

    /// <summary>Recorta y pasa a mayúsculas; vacío se guarda como nulo.</summary>
    public static string? Normalizar(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();

    /// <summary>Error uniforme para «ese código ya lo tiene otro registro».</summary>
    public static Error Duplicado(string catalogo, string codigo, string nombreDelExistente) =>
        new("Catalogo.CodigoDuplicado",
            $"Ya existe {catalogo} con el código {codigo}: «{nombreDelExistente}». Editá ese registro o usá otro código.");

    /// <summary>
    /// El mismo error con <c>data: { existingPublicId, existingName }</c> (feature 012, contracts/api.md §3, §3.10), para
    /// que la pantalla ofrezca abrir el existente.
    /// </summary>
    public static Error Duplicado(string catalogo, string codigo, string nombreDelExistente, Guid publicIdDelExistente) =>
        new ErrorConDatos("Catalogo.CodigoDuplicado",
            $"Ya existe {catalogo} con el código {codigo}: «{nombreDelExistente}». Editá ese registro o usá otro código.",
            new { existingPublicId = publicIdDelExistente, existingName = nombreDelExistente });
}
