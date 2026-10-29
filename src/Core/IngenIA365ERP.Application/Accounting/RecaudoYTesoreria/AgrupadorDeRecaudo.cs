using System.Globalization;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>
/// Cómo se consumen las entregas de Recaudo y Tesorería de una pasada (feature 015, T0257; contabilidad.md §7): los recibos de caja que llegan por lote
/// (<c>ScheduleKey</c> <c>REC.RCE|CierreDeTurno||PorDia</c>) se juntan en un grupo por <b>fecha contable y punto de recaudo</b> (<c>REC|{fecha}|{punto}</c>); todo lo
/// demás, una unidad sola. No lee ni escribe. Un trabajo nunca mezcla módulos. (nuevo)
/// </summary>
public static class AgrupadorDeRecaudo
{
    /// <summary>El prefijo de las claves de grupo de Recaudo.</summary>
    public const string PrefijoDeGrupo = "REC|";

    public static IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> Unidades(IReadOnlyList<MensajeEntrante> entregas) =>
        AgrupadorDeResumidos.Unidades(entregas, IntegrationDestinations.Accounting);

    public static IReadOnlyList<TrabajoDeConsumo> Planear(IReadOnlyList<MensajeEntrante> entregas)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        var trabajos = new List<TrabajoDeConsumo>();
        var grupos = new Dictionary<string, List<UnidadDeConsumo>>(StringComparer.Ordinal);
        var orden = new List<(string? Clave, UnidadDeConsumo? Sola)>();
        foreach (var (unidad, mensajes) in Unidades(entregas))
        {
            var clave = ClaveDe(mensajes);
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

    /// <summary>La clave del grupo resumido de una unidad de Recaudo, o nula si va sola.</summary>
    public static string? ClaveDe(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad[0].OriginModule != ModulosDeOrigen.Recaudo) return null;
        if (unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var horario = ClavesDeLote.Leer(unidad[0].Entrega.ScheduleKey);
        if (horario is null || horario.TipoDeDocumentoRaiz != "REC.RCE" || horario.Granularidad != ClavesDeLote.GranularidadPorDia) return null;
        var punto = HechoContableDeModulo.Leer(unidad[0].PayloadJson)
            .Select(l => l.Dimensiones.GetValueOrDefault(MatricesDeRecaudoYTesoreria.Punto)).FirstOrDefault(p => p is not null) ?? "*";
        return string.Create(CultureInfo.InvariantCulture, $"{PrefijoDeGrupo}{unidad[0].Envelope.Origin.OperationDate:yyyy-MM-dd}|{punto}");
    }
}
