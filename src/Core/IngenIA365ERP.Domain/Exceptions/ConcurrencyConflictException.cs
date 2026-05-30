namespace IngenIA365ERP.Domain.Exceptions;

/// <summary>
/// Se lanza cuando un <c>SaveChanges</c> detecta que la fila objetivo cambió
/// desde la lectura (token <c>RowVersion</c> obsoleto). Equivalente de aplicación
/// a <c>Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException</c>, pero sin
/// dependencia de EF y con datos para construir un mensaje accionable en español.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public string EntityType { get; }
    public string? EntityPublicId { get; }
    public string? LastEditor { get; }
    public DateTime? LastEditedAt { get; }

    public ConcurrencyConflictException(
        string entityType,
        string? entityPublicId = null,
        string? lastEditor = null,
        DateTime? lastEditedAt = null,
        Exception? innerException = null)
        : base(BuildMessage(entityType, entityPublicId, lastEditor), innerException)
    {
        EntityType = entityType;
        EntityPublicId = entityPublicId;
        LastEditor = lastEditor;
        LastEditedAt = lastEditedAt;
    }

    private static string BuildMessage(string entityType, string? entityPublicId, string? lastEditor)
    {
        var who = string.IsNullOrWhiteSpace(lastEditor) ? "otro usuario" : lastEditor!;
        var which = string.IsNullOrWhiteSpace(entityPublicId)
            ? entityType
            : $"{entityType} {entityPublicId}";
        return $"El registro {which} fue modificado por {who} mientras editabas. Refresca y vuelve a intentar.";
    }
}
