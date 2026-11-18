using System.Globalization;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Lotes;

/// <summary>
/// 2026-11-17 (pedido del dueño): <b>un comprobante de Cartera por recibo</b>. Un recibo de caja que abona a varios créditos, a aportes y a
/// ahorro deja en la bandeja una unidad por obligación (un <c>PagoAplicado</c>, un <c>AporteRecibido</c>, una <c>ConsignacionEnCuentaAplicada</c>
/// por cada una), y cada unidad daba su propio comprobante (RC, AO, MA). Con <see cref="Clave"/> en «true» (parámetro del sistema, de fábrica
/// «false»), las unidades en línea que traen el <b>mismo recibo</b> (recaudador, número, fecha y oficina del <c>CollectorRefV1.Receipt</c>, misma
/// fecha contable) se contabilizan juntas en <b>un solo comprobante</b>, al instante, con el tipo del pago a crédito (RC) si lo hay.
///
/// <para>
/// Cartera no cambia: sigue emitiendo una unidad por operación con su recibo en el contenido. Lo decide Contabilidad al consumir, con lo que
/// lee del mensaje (<c>CarteraNoConoceContabilidad</c>). El comprobante del recibo lo arma el mismo handler de los resumidos
/// (<see cref="PostLendingSummaryGroupCommand"/>) sin sumar líneas: cada crédito y cada cuenta conservan las suyas. Lo que falla se rechaza solo
/// y no tumba a los demás, que se contabilizan; los reversos, los hechos sin recibo y los de un recibo con una sola unidad van como siempre.
/// Lo que la tanda del despachador parte (un recibo muy grande) sale en dos comprobantes, cuadrados igual.
/// </para>
/// </summary>
public static class ComprobantePorRecibo
{
    /// <summary>La clave en <c>COR_SystemSettings</c> (módulo <c>ACC</c>; la siembra <c>SystemParametersSeeder</c> en «false»).</summary>
    public const string Clave = "Accounting.LendingVoucherPerReceipt";

    /// <summary>El prefijo de la clave de grupo de un recibo; las de los resumidos empiezan por la fecha.</summary>
    public const string PrefijoDeGrupo = "Recibo|";

    /// <summary>Los hechos principales que juntan su unidad con las demás del mismo recibo.</summary>
    public static IReadOnlyList<string> HechosDeRecibo { get; } = [PagoAplicadoV1.Type, AporteRecibidoV1.Type, ConsignacionEnCuentaAplicadaV1.Type];

    public static bool EsClaveDeRecibo(string? clave) => clave is not null && clave.StartsWith(PrefijoDeGrupo, StringComparison.Ordinal);

    /// <summary>Lo guardado como sí o no; cualquier otra cosa es «no» (como funcionaba antes del parámetro).</summary>
    public static bool Encendido(string? valor) =>
        string.Equals(valor?.Trim(), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(valor?.Trim(), "sí", StringComparison.OrdinalIgnoreCase)
        || string.Equals(valor?.Trim(), "si", StringComparison.OrdinalIgnoreCase) || valor?.Trim() == "1";

    /// <summary>El recaudo del contenido de un hecho que se junta por recibo, o nulo.</summary>
    public static CollectorRefV1? Recaudo(object? contenido) => contenido switch
    {
        PagoAplicadoV1 p => p.Collector,
        AporteRecibidoV1 a => a.Collector,
        ConsignacionEnCuentaAplicadaV1 c => c.Collector,
        _ => null,
    };

    /// <summary>
    /// La clave del recibo de una unidad (<c>Recibo|{recaudador}|{número}|{fecha del recibo}|{oficina}|{fecha contable}</c>), o nula si va sola:
    /// informativa, sin hecho principal de recibo, sin número de recibo.
    /// </summary>
    public static string? ClaveDe(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var principal = ConstructorDeLineasDeCartera.Principal(unidad.Select(HechoDeCartera.De).ToList());
        if (principal is null || !HechosDeRecibo.Contains(principal.Tipo, StringComparer.Ordinal)) return null;
        if (Recaudo(principal.Contenido) is not { Receipt.Number: { Length: > 0 } numero } recaudo) return null;
        return string.Create(CultureInfo.InvariantCulture,
            $"{PrefijoDeGrupo}{recaudo.CollectorCode}|{numero.Trim()}|{recaudo.Receipt.Date:yyyy-MM-dd}|{recaudo.Receipt.OfficePublicId:N}|{principal.Sobre.Origin.OperationDate:yyyy-MM-dd}");
    }

    /// <summary>
    /// Los trabajos de una tanda en línea: las unidades del mismo recibo, juntas (con su clave) si son dos o más; las demás, solas. En el orden de su
    /// primera unidad. No lee ni escribe.
    /// </summary>
    public static IReadOnlyList<TrabajoDeConsumo> Planear(IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> unidades)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        var grupos = new Dictionary<string, List<UnidadDeConsumo>>(StringComparer.Ordinal);
        var orden = new List<(string? Clave, UnidadDeConsumo Unidad)>();
        foreach (var (unidad, mensajes) in unidades)
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
                orden.Add((clave, unidad));
            }
            grupo.Add(unidad);
        }
        return orden
            .Select(o => o.Clave is null || grupos[o.Clave].Count == 1
                ? TrabajoDeConsumo.DeUnaUnidad(o.Clave is null ? o.Unidad : grupos[o.Clave][0])
                : new TrabajoDeConsumo(o.Clave, grupos[o.Clave]))
            .ToList();
    }
}

/// <summary>
/// Cómo consume Contabilidad lo en línea de una tanda (2026-11-17): con <see cref="ComprobantePorRecibo.Clave"/> apagado, una unidad por trabajo
/// (como siempre, sin leer nada); encendido, lee las unidades de Cartera y junta las del mismo recibo. Sólo lee.
/// </summary>
public sealed class PlaneadorEnLineaDeCartera(IApplicationDbContext db, IMensajesEntrantes entrantes)
{
    public async Task<IReadOnlyList<TrabajoDeConsumo>> PlanearAsync(IReadOnlyList<UnidadDeConsumo> unidades, string destino, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        var solas = unidades.Select(TrabajoDeConsumo.DeUnaUnidad).ToList();
        var deCartera = unidades.Where(u => u.OriginModule == ModulosDeOrigen.Cartera).ToList();
        if (deCartera.Count < 2) return solas;

        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == ComprobantePorRecibo.Clave)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        if (!ComprobantePorRecibo.Encendido(valor)) return solas;

        var leidos = await entrantes.LeerAsync(deCartera.SelectMany(u => u.MessagePublicIds).ToList(), destino, ct);
        if (leidos.IsFailure) return solas;
        var porId = leidos.Value.ToDictionary(m => m.MessageId);
        var conMensajes = new List<(UnidadDeConsumo, IReadOnlyList<MensajeEntrante>)>();
        foreach (var u in deCartera)
        {
            // Una unidad que no se pudo leer entera va sola: su consumo dirá qué le pasa.
            if (!u.MessagePublicIds.All(porId.ContainsKey)) return solas;
            conMensajes.Add((u, u.MessagePublicIds.Select(id => porId[id]).ToList()));
        }
        var deCarteraPlaneada = ComprobantePorRecibo.Planear(conMensajes);

        // El orden de la tanda: cada trabajo de Cartera en el lugar de su primera unidad; los demás módulos, solos donde estaban.
        var trabajoDe = deCarteraPlaneada.ToDictionary(t => t.Unidades[0]);
        var enGrupo = deCarteraPlaneada.Where(t => t.Unidades.Count > 1).SelectMany(t => t.Unidades.Skip(1)).ToHashSet();
        var salida = new List<TrabajoDeConsumo>();
        foreach (var u in unidades)
        {
            if (enGrupo.Contains(u)) continue;
            salida.Add(trabajoDe.TryGetValue(u, out var t) ? t : TrabajoDeConsumo.DeUnaUnidad(u));
        }
        return salida;
    }
}
