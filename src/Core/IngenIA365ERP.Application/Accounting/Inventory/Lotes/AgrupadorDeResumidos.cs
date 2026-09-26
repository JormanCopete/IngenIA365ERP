using System.Globalization;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Accounting.Inventory.Lotes;

/// <summary>
/// Cómo se consumen las entregas de una pasada de un lote (feature 012, T513; contracts/contabilidad.md §5.2 y §5.3; FR-077).
/// No lee ni escribe: recibe lo que el despachador ya leyó. Agrupa los mensajes en <b>unidades</b> (origen + evento, T11) y, si
/// la entrega es de una <c>ScheduleKey</c> resumida, junta las unidades en un grupo por <b>fecha del comprobante, tipo de
/// documento, operación (que decide el tipo de comprobante con el tipo de documento), sucursal y centro de costo</b>. Una
/// anulación, una nota o un ajuste de costo tienen su propio tipo de documento y su propia fecha: nunca caen en el grupo del
/// original. Los informativos y lo que va por documento salen como una unidad sola. (nuevo)
/// </summary>
public static class AgrupadorDeResumidos
{
    /// <summary>Las unidades de una lista de entregas, en orden de emisión (el de la lista).</summary>
    public static IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> Unidades(
        IReadOnlyList<MensajeEntrante> entregas, string destino)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        return entregas
            .GroupBy(m => (m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey))
            .Select(g =>
            {
                var mensajes = g.ToList();
                var unidad = new UnidadDeConsumo(destino, g.Key.PublicId, g.Key.OriginEventKey,
                    mensajes.Select(m => m.MessageId).ToList(),
                    mensajes.Select(m => m.Entrega.BatchPublicId).FirstOrDefault(b => b is not null),
                    mensajes.Max(m => m.Entrega.Attempts));
                return (unidad, (IReadOnlyList<MensajeEntrante>)mensajes);
            })
            .ToList();
    }

    /// <summary>
    /// Los trabajos de una pasada: los grupos resumidos (con su clave) y las unidades por documento, en el orden en que aparece
    /// su primer mensaje.
    /// </summary>
    public static IReadOnlyList<TrabajoDeConsumo> Planear(IReadOnlyList<MensajeEntrante> entregas, string destino)
    {
        var trabajos = new List<TrabajoDeConsumo>();
        var grupos = new Dictionary<string, List<UnidadDeConsumo>>(StringComparer.Ordinal);
        var orden = new List<(string? Clave, UnidadDeConsumo? Sola)>();
        foreach (var (unidad, mensajes) in Unidades(entregas, destino))
        {
            var clave = EsResumida(mensajes) ? ClaveDe(mensajes) : null;
            if (clave is null)
            {
                orden.Add((null, unidad));
                continue;
            }
            if (!grupos.TryGetValue(clave, out var grupo))
            {
                grupo = [];
                grupos[clave] = grupo;
                orden.Add((clave, null));
            }
            grupo.Add(unidad);
        }
        foreach (var (clave, sola) in orden)
            trabajos.Add(clave is null ? TrabajoDeConsumo.DeUnaUnidad(sola!) : new TrabajoDeConsumo(clave, grupos[clave]));
        return trabajos;
    }

    /// <summary>¿Va en un comprobante resumido? Una unidad de negocio cuya <c>ScheduleKey</c> sella la granularidad resumida.</summary>
    public static bool EsResumida(IReadOnlyList<MensajeEntrante> unidad) =>
        unidad.Count > 0
        && unidad.All(m => m.Envelope.Kind != IntegrationMessageKind.Informational)
        && ClavesDeLote.Leer(unidad[0].Entrega.ScheduleKey) is { GranularidadDelLote: PostingGranularity.Summarized };

    /// <summary>
    /// La clave del grupo resumido de una unidad: <c>{fecha}|{tipo de documento}|{operación}|{sucursal}|{centro}</c>. La fecha es la
    /// del comprobante (la de operación; en un ajuste de costo, la efectiva): nunca la del lote.
    /// </summary>
    public static string ClaveDe(IReadOnlyList<MensajeEntrante> unidad)
    {
        var mensajes = unidad.Select(MensajeDeUnidad.De).ToList();
        var principal = ConstructorDeLineasDeInventario.Principal(mensajes)!;
        var operacion = TiposDeComprobanteDeInventario.OperacionDe(mensajes.Select(m => (m.Sobre, m.Contenido)).ToList());
        var sobre = principal.Sobre;
        return string.Join('|',
            ConstructorDeLineasDeInventario.FechaDelComprobante(principal).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            sobre.Origin.DocumentTypeCode ?? string.Empty,
            operacion?.Operation ?? principal.Tipo,
            sobre.BranchPublicId.ToString("D"),
            sobre.CostCenterPublicId?.ToString("D") ?? string.Empty);
    }
}
