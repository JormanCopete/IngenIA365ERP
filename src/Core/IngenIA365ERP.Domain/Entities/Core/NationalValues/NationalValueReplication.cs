using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.NationalValues;

/// <summary>
/// <c>COR_NationalValueReplications</c> (feature 013, T39; data-model parte 3 §4.3): bitácora de cada réplica que
/// <b>copió algo</b>. La tarea corre en cada pasada (cada 60 s) y una fila por pasada vacía serían 1.440 filas diarias por
/// cooperativa, así que una pasada sin nada nuevo no deja fila. La marca de agua es <c>MAX(SourceSequence)</c> de las
/// copias, no esta tabla.
/// </summary>
public class NationalValueReplication : AuditableEntity, IHechoInmutable
{
    /// <summary>Primera <c>Sequence</c> central copiada.</summary>
    public long FromSequence { get; init; }

    /// <summary>Última <c>Sequence</c> central copiada.</summary>
    public long ToSequence { get; init; }

    /// <summary>Filas copiadas a <c>COR_NationalValues</c>.</summary>
    public int ValueCount { get; init; }

    /// <summary>Filas copiadas a <c>COR_RegulatoryDocuments</c>.</summary>
    public int DocumentCount { get; init; }

    public NationalValueReplicationOrigin Origin { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime FinishedAt { get; init; }

    /// <summary>Resumen de los efectos de <c>IEfectoDeReplicacion</c> en la misma transacción (hasta 2.000).</summary>
    public string? EffectsSummaryJson { get; init; }

    /// <summary>El proceso de la cooperativa o quien ordenó la corrida.</summary>
    public string ActorName { get; init; } = string.Empty;

    /// <summary>Las copias de valores que trajo (se agregan al crearla; nunca después).</summary>
    public List<NationalValue> Values { get; init; } = [];

    /// <summary>Las copias de documentos que trajo.</summary>
    public List<RegulatoryDocument> Documents { get; init; } = [];
}
