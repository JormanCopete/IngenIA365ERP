using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// El punto de extensión de la réplica (feature 013, T39, T42, T93; data-model parte 3 §4.3): lo que otro módulo hace cuando
/// llegan valores nacionales nuevos —Nómina proyecta el SMMLV, la UVT y los festivos; la plataforma avisa un límite legal más
/// estricto; Cartera, desde sus fases, la obligación sobrevenida y la corrección de un valor ya causado—. Corre <b>en la misma
/// transacción</b> de la réplica: escribe en <paramref name="db"/> sin guardar y, si lanza, la réplica no copia nada.
///
/// <para>Recibe el contexto por parámetro, no por constructor, para que la semilla del alta (que arma su propio contexto de la
/// cooperativa) y la tarea programada corran los mismos efectos. Las alertas no las levanta: las devuelve, y el núcleo las
/// levanta después de guardar. Se registra como singleton sin estado. (nuevo)</para>
/// </summary>
public interface IEfectoDeReplicacion
{
    /// <summary>Nombre corto y estable: la clave del efecto en <c>EffectsSummaryJson</c>.</summary>
    string Nombre { get; }

    Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct);
}

/// <summary>Lo que copió una réplica, en orden de <c>Sequence</c>, con el instante y la fecha local de la pasada.</summary>
public sealed record LoteReplicado(
    IReadOnlyList<NationalValue> Valores,
    IReadOnlyList<RegulatoryDocument> Documentos,
    DateTime Ahora,
    DateOnly Hoy);

/// <summary>
/// El resultado de un efecto: conteos para el resumen (<c>adoptadas</c>, <c>insertadas</c>…), notas legibles y las alertas que
/// el núcleo levanta tras guardar.
/// </summary>
public sealed record EfectoAplicado(
    IReadOnlyDictionary<string, int> Conteos,
    IReadOnlyList<string> Notas,
    IReadOnlyList<AlertaALevantar> Alertas)
{
    public static EfectoAplicado Nada { get; } = new(new Dictionary<string, int>(), [], []);
}
