namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>
/// Un prefijo que un módulo usa en la numeración propia de sus notas electrónicas: quién lo usa, para el mensaje de
/// <c>ElectronicInvoicing.Resolution.PrefixInUse</c>. (nuevo)
/// </summary>
public sealed record PrefijoDeModulo(string Prefijo, string UsadoPor);

/// <summary>
/// El puerto con que la plataforma pregunta a los módulos fuente qué prefijos numeran sus notas electrónicas (feature 012, I4, T710;
/// data-model §27 duda 9). Las notas y las resoluciones comparten la unicidad <c>(Environment, Prefix, Consecutive)</c> de
/// <c>COR_ElectronicDocuments</c>, así que un prefijo de notas no puede coincidir con el de una resolución: lo consulta
/// <c>RegisterNumberingResolutionCommand</c> sin leer tablas <c>INV_</c> (T40). Inventario lo implementa con <c>PrefijosDeInventario</c>
/// sobre <c>INV_DocumentSequences</c>; la validación inversa la hacen los comandos de secuencias del módulo. (nuevo)
/// </summary>
public interface IPrefijosDeModulos
{
    /// <summary>¿Qué nota usa <paramref name="prefijo"/> (ya normalizado, no vacío)? Nulo si ninguna.</summary>
    Task<PrefijoDeModulo?> UsoDeAsync(string prefijo, CancellationToken ct = default);
}
