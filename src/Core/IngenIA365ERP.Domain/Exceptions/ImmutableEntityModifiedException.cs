namespace IngenIA365ERP.Domain.Exceptions;

/// <summary>
/// Se intentó modificar o borrar un hecho inmutable, o cambiar una columna fija de un documento confirmado (feature 012,
/// T17, T18; Principio XI). La lanza <c>ApplicationDbContext.SaveChangesAsync</c> antes de escribir y nombra la entidad
/// y la propiedad, para que el defecto se encuentre sin adivinar. Es un error de programación, no de la persona: lo
/// que estaba mal se corrige con otro hecho (anulación, nota), no reescribiendo éste. (nuevo)
/// </summary>
public sealed class ImmutableEntityModifiedException : InvalidOperationException
{
    public string EntityType { get; }
    public string? Property { get; }

    public ImmutableEntityModifiedException(string entityType, string? property, string detail)
        : base(property is null
            ? $"{entityType} es inmutable: {detail}"
            : $"{entityType}.{property} no se puede cambiar: {detail}")
    {
        EntityType = entityType;
        Property = property;
    }
}
