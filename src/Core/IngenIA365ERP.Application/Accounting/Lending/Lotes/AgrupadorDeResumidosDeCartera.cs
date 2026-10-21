using System.Globalization;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Accounting.Lending.Lotes;

/// <summary>
/// Cómo se consumen las entregas de Cartera de una pasada de un lote (feature 013, T0648; contracts/contabilidad.md §10.3 y §10.4; C07, C09).
/// No lee ni escribe: recibe lo que el despachador ya leyó. Agrupa los hechos en <b>unidades</b> (origen + evento, T11) y, si la entrega sella
/// una <c>ScheduleKey</c> de Cartera resumida, junta las unidades en un grupo por <b>fecha contable y tipo de comprobante</b>
/// (<c>PorDia</c>: <c>{fecha}|{tipo}</c>) o además por sucursal (<c>PorDiaYSucursal</c>: <c>{fecha}|{tipo}|{sucursal}</c>). «Tipo» es el tipo de
/// comprobante del hecho principal (C09), el de su fila de <see cref="OperacionesDeCartera"/>: si la contadora mapea dos hechos del grupo a
/// tipos distintos, el resumido arma un comprobante por tipo. Con <c>PorHecho</c> (C07), los informativos y lo que no es resumido, una unidad
/// sola. Un trabajo nunca mezcla módulos. (nuevo)
/// </summary>
public static class AgrupadorDeResumidosDeCartera
{
    /// <summary>Las unidades de una lista de entregas, en orden de emisión (las de <see cref="AgrupadorDeResumidos.Unidades"/>).</summary>
    public static IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> Unidades(
        IReadOnlyList<MensajeEntrante> entregas, string destino) => AgrupadorDeResumidos.Unidades(entregas, destino);

    /// <summary>Los trabajos de una pasada: los grupos resumidos (con su clave) y las unidades solas, en el orden de su primer hecho.</summary>
    public static IReadOnlyList<TrabajoDeConsumo> Planear(IReadOnlyList<MensajeEntrante> entregas, string destino)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        var trabajos = new List<TrabajoDeConsumo>();
        var grupos = new Dictionary<string, List<UnidadDeConsumo>>(StringComparer.Ordinal);
        var orden = new List<(string? Clave, UnidadDeConsumo? Sola)>();
        foreach (var (unidad, mensajes) in Unidades(entregas, destino))
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

    /// <summary>La granularidad sellada en la <c>ScheduleKey</c> de Cartera de la unidad, o nula si no es una clave de Cartera.</summary>
    public static string? Granularidad(IReadOnlyList<MensajeEntrante> unidad) =>
        unidad.Count > 0 && ClavesDeLote.Leer(unidad[0].Entrega.ScheduleKey) is { Modulo: ModulosDeOrigen.Cartera } horario ? horario.Granularidad : null;

    /// <summary>
    /// La clave del grupo resumido de una unidad (§10.4), o nula si va sola: <c>{fecha contable}|{tipo de comprobante}</c> con <c>PorDia</c>;
    /// <c>+|{sucursal}</c> con <c>PorDiaYSucursal</c>. La fecha es la contable del hecho, nunca la del lote.
    /// </summary>
    public static string? ClaveDe(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var granularidad = Granularidad(unidad);
        if (granularidad is not (ClavesDeLote.GranularidadPorDia or ClavesDeLote.GranularidadPorDiaYSucursal)) return null;
        var principal = ConstructorDeLineasDeCartera.Principal(unidad.Select(HechoDeCartera.De).ToList())!;
        var tipo = OperacionesDeCartera.Buscar(principal.Tipo)?.Comprobante ?? principal.Tipo;
        var fecha = principal.Sobre.Origin.OperationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return granularidad == ClavesDeLote.GranularidadPorDiaYSucursal
            ? $"{fecha}|{tipo}|{principal.Sobre.BranchPublicId:D}"
            : $"{fecha}|{tipo}";
    }
}
