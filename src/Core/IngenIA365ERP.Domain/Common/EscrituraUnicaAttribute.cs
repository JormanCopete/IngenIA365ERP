namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 012, I4 (T691; data-model §18 y §26, duda 5): propiedad de un <see cref="IHechoInmutable"/> que nace nula y
/// se llena <b>una sola vez</b> (nulo → valor) después de insertar el hecho. Es la forma de que los artefactos de una
/// versión del documento electrónico (canónico, XML firmado, AttachedDocument, representación gráfica) lleguen después
/// del commit sin volver mutable la versión.
///
/// <para>
/// Quien lo hace cumplir es <c>GuardaDeInmutabilidad</c> (la que llama <c>ApplicationDbContext.SaveChangesAsync</c>):
/// admite un <c>Modified</c> sobre un hecho sólo si <b>toda</b> propiedad modificada lleva este atributo y su valor
/// original es nulo. Reemplazar o vaciar un valor ya escrito, cambiar cualquier otra columna o borrar el hecho se sigue
/// rechazando. (nuevo)
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class EscrituraUnicaAttribute : Attribute;
