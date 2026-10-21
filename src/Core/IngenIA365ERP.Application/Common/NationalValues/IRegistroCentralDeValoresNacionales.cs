namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// La lectura del registro central de valores nacionales (<c>ADM_NationalValues</c>, <c>ADM_RegulatoryDocuments</c>) por
/// <c>Sequence</c> (feature 013, T37, T39, I-3; data-model parte 3 §3). <b>Sólo la usa el replicador</b>
/// (<see cref="ReplicacionDeValoresNacionales"/>): ningún cálculo lee la base administrativa
/// (<c>SoloElReplicadorLeeElRegistroCentral</c>). La implementa Persistence sobre <c>AdminDbContext</c>, en lectura. (nuevo)
/// </summary>
public interface IRegistroCentralDeValoresNacionales
{
    /// <summary>
    /// Las filas de las dos tablas con <c>Sequence</c> mayor que <paramref name="despuesDe"/>, en orden de <c>Sequence</c>, a
    /// lo sumo <paramref name="maximo"/> en total. Si la central no responde lanza: quien llama lo convierte en motivo.
    /// </summary>
    Task<LecturaDeLaCentral> LeerDesdeAsync(long despuesDe, int maximo, CancellationToken ct);
}

/// <summary>Lo que trajo una lectura de la central: valores y documentos con <c>Sequence</c> mayor que la marca.</summary>
public sealed record LecturaDeLaCentral(IReadOnlyList<ValorCentral> Valores, IReadOnlyList<DocumentoCentral> Documentos)
{
    public static LecturaDeLaCentral Vacia { get; } = new([], []);

    public bool EstaVacia => Valores.Count == 0 && Documentos.Count == 0;
}

/// <summary>Una fila de <c>ADM_NationalValues</c> tal como se copia.</summary>
public sealed record ValorCentral(
    Guid PublicId,
    long Sequence,
    string Code,
    string Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string? Label,
    string Source,
    string? Reason,
    DateTime RegisteredAt,
    Guid? SupersedesPublicId);

/// <summary>Una fila de <c>ADM_RegulatoryDocuments</c> tal como se copia.</summary>
public sealed record DocumentoCentral(
    Guid PublicId,
    long Sequence,
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Content,
    string ContentSha256,
    short SchemaVersion,
    bool Verified,
    string Source,
    string? Reason,
    DateTime RegisteredAt,
    Guid? SupersedesPublicId);
