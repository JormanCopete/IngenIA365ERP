using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// El único lector de los valores nacionales escalares de la cooperativa (feature 013, T40, R5; data-model parte 3 §4.1):
/// lee <c>COR_NationalValues</c> con la regla bitemporal única (<c>VigenciaBitemporal</c>, R = <c>KnownAt</c>) y nadie más
/// lo hace (<c>LosValoresNacionalesSeLeenEnUnSoloSitio</c>). Si falta el valor de una fecha devuelve
/// <c>NationalValues.Missing</c> y alerta <c>Cartera.ValorNacionalFaltante</c>: lo que lo necesita se bloquea. (nuevo)
/// </summary>
public interface IValoresNacionales
{
    /// <summary>
    /// El valor de <paramref name="codigo"/> y <paramref name="clave"/> que rige el <paramref name="fecha"/> con lo que se
    /// sabía hasta <paramref name="informacionHasta"/> (nulo = todo lo replicado).
    /// </summary>
    Task<Result<ValorNacionalVigente>> LeerAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default);

    /// <summary>
    /// El último índice publicado a la fecha (IBR, IPC). Si es más viejo que lo que admite <c>CodigosDeValorNacional</c> se
    /// devuelve igual, marcado <see cref="ValorNacionalVigente.Desactualizado"/>, y se alerta <c>Cartera.IndiceDesactualizado</c>.
    /// </summary>
    Task<Result<ValorNacionalVigente>> LeerUltimoPublicadoAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default);

    /// <summary>
    /// La serie de un rango en <b>una</b> consulta, día por día (la causación la carga en bloque): para cada fecha el valor que
    /// rige o nulo si falta. No alerta: quien la recorre decide qué hacer con un hueco.
    /// </summary>
    Task<SerieDeValorNacional> LeerSerieAsync(string codigo, string clave, DateOnly desde, DateOnly hasta, DateTime? informacionHasta = null, CancellationToken ct = default);

    /// <summary>
    /// La copia de una fila central por su <c>SourcePublicId</c>, con la réplica que la trajo (feature 013, US4, T0500: la corrida
    /// <c>NationalValueCorrection</c> sabe así qué valor la provocó). Nulo si no está replicada. Sin alertas.
    /// </summary>
    Task<ValorNacionalVigente?> LeerCopiaAsync(Guid sourcePublicId, CancellationToken ct = default) => Task.FromResult<ValorNacionalVigente?>(null);
}

/// <summary>Un valor nacional vigente leído de la copia de la cooperativa.</summary>
/// <param name="PublicId">El de la copia en esta cooperativa (lo que se revisa y se sella).</param>
/// <param name="SourcePublicId">El de la fila central.</param>
/// <param name="Desactualizado">Sólo en <see cref="IValoresNacionales.LeerUltimoPublicadoAsync"/>: más viejo de lo admitido.</param>
public sealed record ValorNacionalVigente(
    Guid PublicId,
    Guid SourcePublicId,
    string Codigo,
    string Clave,
    decimal? Valor,
    string? Unidad,
    string? Etiqueta,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    string Fuente,
    long Sequence,
    DateTime KnownAt,
    EstadoDeRevision Estado,
    bool Desactualizado = false)
{
    /// <summary>La réplica que trajo la copia (feature 013, US4, T0500); nula si no se pidió.</summary>
    public int? ReplicationId { get; init; }
}

/// <summary>La serie de un código y clave en un rango: el valor de cada día, o nulo si falta.</summary>
public sealed class SerieDeValorNacional(string codigo, string clave, IReadOnlyDictionary<DateOnly, ValorNacionalVigente?> dias)
{
    public string Codigo { get; } = codigo;
    public string Clave { get; } = clave;
    public IReadOnlyDictionary<DateOnly, ValorNacionalVigente?> Dias { get; } = dias;

    public ValorNacionalVigente? En(DateOnly fecha) => Dias.TryGetValue(fecha, out var v) ? v : null;

    /// <summary>Las fechas del rango sin valor.</summary>
    public IReadOnlyList<DateOnly> Faltantes => Dias.Where(d => d.Value is null).Select(d => d.Key).OrderBy(d => d).ToList();
}
