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
/// 2026-11-27 (feature 028, T047; FR-027b, D11; research R11): <b>el desembolso y sus recogidas en un solo comprobante</b>. El desembolso emite su
/// <c>CreditoDesembolsado</c> (DS) y, en la misma transacción, el contrato emite un <c>PagoAplicado</c> (RC) por cada crédito recogido, con el número del
/// desembolso como recibo. Cada uno es su propia unidad y daba su propio comprobante; el puente de la recogida propia se saldaba entre ellos. Con
/// <see cref="Clave"/> en «true» (parámetro de Contabilidad, visible como «¿El desembolso y sus recogidas salen en un solo comprobante?», de fábrica
/// «false»), la unidad del desembolso y las de los recibos de sus recogidas que llegan en la misma tanda en línea y en la misma fecha contable se
/// contabilizan juntas, en <b>un</b> comprobante del tipo del desembolso: el puente entra y sale en él.
///
/// <para>
/// Casan por la operación: los recibos que se juntan son los de las operaciones que el desembolso nombra en sus <c>PayoffDeductions</c> (su
/// <c>OperationPublicId</c> es el origen del <c>PagoAplicado</c>, cuyo recibo lleva el número del desembolso). Lo arma el mismo handler que el recibo por
/// recibo (<see cref="PostLendingSummaryGroupCommand"/>, la clave empieza por <see cref="ComprobantePorRecibo.PrefijoDeGrupo"/>), sin sumar líneas y
/// sin lote; si el desembolso no se puede contabilizar, sus recogidas salen juntas en su RC y el desembolso queda para reproceso, solo. Una tanda que
/// los parta (el despachador limita su tamaño) los deja en dos comprobantes, cuadrados igual; un giro nuevo, cuyos recibos ya salieron, va solo.
/// Cartera no cambia lo que emite.
/// </para>
/// </summary>
public static class DesembolsoConSusRecogidas
{
    /// <summary>La clave en <c>COR_SystemSettings</c> (módulo <c>ACC</c>; la siembra <c>SystemParametersSeeder</c> en «false»).</summary>
    public const string Clave = "Accounting.LendingDisbursementWithPayoffsInOneVoucher";

    /// <summary>El prefijo de la clave de grupo: un recibo de Cartera (el número del desembolso es el recibo de sus recogidas).</summary>
    public const string PrefijoDeGrupo = ComprobantePorRecibo.PrefijoDeGrupo + "Desembolso|";

    public static bool EsClaveDeGrupo(string? clave) => clave is not null && clave.StartsWith(PrefijoDeGrupo, StringComparison.Ordinal);

    /// <summary>Si la cooperativa lo tiene encendido (sí, true o 1; cualquier otra cosa es «no»). Sólo lee.</summary>
    public static async Task<bool> EncendidoAsync(IApplicationDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return ComprobantePorRecibo.Encendido(valor);
    }

    /// <summary>La clave de grupo, las operaciones de las recogidas y la fecha contable de una unidad de desembolso con recogidas; nulo si no lo es.</summary>
    public static (string Clave, IReadOnlyList<Guid> Recogidas, DateOnly Fecha)? Desembolso(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var principal = ConstructorDeLineasDeCartera.Principal(unidad.Select(HechoDeCartera.De).ToList());
        if (principal?.Contenido is not CreditoDesembolsadoV1 { InKind: null } desembolso || desembolso.PayoffDeductions.Count == 0) return null;
        return (string.Create(CultureInfo.InvariantCulture, $"{PrefijoDeGrupo}{principal.Sobre.Origin.PublicId:N}"),
            desembolso.PayoffDeductions.Select(r => r.OperationPublicId).Where(g => g != Guid.Empty).ToList(), principal.Sobre.Origin.OperationDate);
    }

    /// <summary>La operación y la fecha contable de una unidad que es el recibo de una recogida propia (un <c>PagoAplicado</c> de <c>OwnLoanPayoff</c>); o nulo.</summary>
    public static (Guid Operacion, DateOnly Fecha)? Recogida(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var principal = ConstructorDeLineasDeCartera.Principal(unidad.Select(HechoDeCartera.De).ToList());
        return principal?.Contenido is PagoAplicadoV1 pago
               && string.Equals(pago.Collector.CollectorClass, ConstructorDeLineasDeCartera.ClaseDeLaRecogidaPropia, StringComparison.Ordinal)
            ? (principal.Sobre.Origin.PublicId, principal.Sobre.Origin.OperationDate)
            : null;
    }

    /// <summary>
    /// Los grupos de una tanda: cada desembolso con los recibos de sus recogidas que están en ella (misma fecha contable), en el orden de la tanda; sólo los
    /// que juntan al menos un recibo. Lo demás no se toca. Puro.
    /// </summary>
    public static IReadOnlyList<TrabajoDeConsumo> Planear(IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> unidades)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        var claveDe = new Dictionary<UnidadDeConsumo, string>();
        var deLaOperacion = new Dictionary<Guid, (string Clave, DateOnly Fecha)>();
        var repetidas = new HashSet<Guid>();
        foreach (var (unidad, mensajes) in unidades)
        {
            if (Desembolso(mensajes) is not { } d) continue;
            claveDe[unidad] = d.Clave;
            foreach (var operacion in d.Recogidas)
                if (!deLaOperacion.TryAdd(operacion, (d.Clave, d.Fecha))) repetidas.Add(operacion); // dos desembolsos la nombran: no se adivina
        }
        if (claveDe.Count == 0) return [];

        foreach (var (unidad, mensajes) in unidades)
            if (!claveDe.ContainsKey(unidad) && Recogida(mensajes) is { } r && !repetidas.Contains(r.Operacion)
                && deLaOperacion.TryGetValue(r.Operacion, out var g) && g.Fecha == r.Fecha)
                claveDe[unidad] = g.Clave;

        return unidades.Where(x => claveDe.ContainsKey(x.Unidad))
            .GroupBy(x => claveDe[x.Unidad], StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new TrabajoDeConsumo(g.Key, g.Select(x => x.Unidad).ToList()))
            .ToList();
    }
}

/// <summary>
/// Cómo consume Contabilidad lo en línea de una tanda (2026-11-17): con <see cref="ComprobantePorRecibo.Clave"/> apagado, una unidad por trabajo
/// (como siempre, sin leer nada); encendido, lee las unidades de Cartera y junta las del mismo recibo. 2026-11-27 (feature 028, T047): con
/// <see cref="DesembolsoConSusRecogidas.Clave"/> encendido, junta además cada desembolso con los recibos de sus recogidas. Sólo lee.
/// </summary>
public sealed class PlaneadorEnLineaDeCartera(IApplicationDbContext db, IMensajesEntrantes entrantes)
{
    public async Task<IReadOnlyList<TrabajoDeConsumo>> PlanearAsync(IReadOnlyList<UnidadDeConsumo> unidades, string destino, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        var solas = unidades.Select(TrabajoDeConsumo.DeUnaUnidad).ToList();
        var deCartera = unidades.Where(u => u.OriginModule == ModulosDeOrigen.Cartera).ToList();
        if (deCartera.Count == 0) return solas;

        // 2026-11-26: el recibo con su caja (Recaudo y Cartera juntos) y, aparte, sólo lo de Cartera por recibo.
        var conCaja = await ReciboEnUnSoloComprobante.EncendidoAsync(db, ct);
        var deRecaudo = conCaja ? unidades.Where(u => u.OriginModule == ModulosDeOrigen.Recaudo).ToList() : [];
        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == ComprobantePorRecibo.Clave)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        var porRecibo = ComprobantePorRecibo.Encendido(valor);
        // 2026-11-27 (feature 028, T047; FR-027b, D11): el desembolso con los recibos de sus recogidas, en un solo comprobante si la empresa lo pide.
        var conRecogidas = await DesembolsoConSusRecogidas.EncendidoAsync(db, ct);
        if (!(conCaja && deRecaudo.Count > 0) && !((porRecibo || conRecogidas) && deCartera.Count > 1)) return solas;

        var aLeer = deCartera.Concat(deRecaudo).ToList();
        var leidos = await entrantes.LeerAsync(aLeer.SelectMany(u => u.MessagePublicIds).ToList(), destino, ct);
        if (leidos.IsFailure) return solas;
        var porId = leidos.Value.ToDictionary(m => m.MessageId);
        var conMensajes = new Dictionary<UnidadDeConsumo, IReadOnlyList<MensajeEntrante>>();
        foreach (var u in aLeer)
        {
            // Una unidad que no se pudo leer entera va sola: su consumo dirá qué le pasa.
            if (!u.MessagePublicIds.All(porId.ContainsKey)) return solas;
            conMensajes[u] = u.MessagePublicIds.Select(id => porId[id]).ToList();
        }

        var trabajoDe = new Dictionary<UnidadDeConsumo, TrabajoDeConsumo>();
        var enGrupo = new HashSet<UnidadDeConsumo>();
        void Agregar(TrabajoDeConsumo t)
        {
            trabajoDe[t.Unidades[0]] = t;
            foreach (var u in t.Unidades.Skip(1)) enGrupo.Add(u);
        }

        var libresDeCartera = deCartera;
        if (deRecaudo.Count > 0)
        {
            foreach (var t in ReciboConSuCaja.Planear(deRecaudo.Select(u => (u, conMensajes[u])).ToList(), deCartera.Select(u => (u, conMensajes[u])).ToList()))
                Agregar(t);
            libresDeCartera = deCartera.Where(u => !trabajoDe.ContainsKey(u) && !enGrupo.Contains(u)).ToList();
        }
        // Antes que el recibo de Cartera: los recibos de las recogidas comparten el número del desembolso y, sin esto, se juntarían sólo entre ellos.
        if (conRecogidas && libresDeCartera.Count > 1)
        {
            foreach (var t in DesembolsoConSusRecogidas.Planear(libresDeCartera.Select(u => (u, conMensajes[u])).ToList()))
                Agregar(t);
            libresDeCartera = libresDeCartera.Where(u => !trabajoDe.ContainsKey(u) && !enGrupo.Contains(u)).ToList();
        }
        if (porRecibo && libresDeCartera.Count > 1)
            foreach (var t in ComprobantePorRecibo.Planear(libresDeCartera.Select(u => (u, conMensajes[u])).ToList()).Where(t => t.Unidades.Count > 1))
                Agregar(t);

        // El orden de la tanda: cada trabajo en el lugar de su primera unidad; lo demás, solo donde estaba.
        var salida = new List<TrabajoDeConsumo>();
        foreach (var u in unidades)
        {
            if (enGrupo.Contains(u)) continue;
            salida.Add(trabajoDe.TryGetValue(u, out var t) ? t : TrabajoDeConsumo.DeUnaUnidad(u));
        }
        return salida;
    }
}

/// <summary>
/// 2026-11-26 (<see cref="ReciboEnUnSoloComprobante"/>): qué unidades de una tanda son el mismo recibo de caja. La de Recaudo es la que trae el
/// <c>ReciboEmitido</c>; las de Cartera, las que abonan con ese recibo por el recaudador <c>REC</c> (su <c>CollectorRefV1.Receipt</c>). Casan por número,
/// oficina y fecha contable. Un grupo es <b>una</b> unidad de Recaudo con una o más de Cartera; lo que no casa va como siempre. Puro.
/// </summary>
public static class ReciboConSuCaja
{
    /// <summary>La clave de la unidad de Recaudo del recibo de caja, o nula si no lo es.</summary>
    public static string? ClaveDeRecaudo(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var recibo = unidad.FirstOrDefault(m => m.Payload is Common.Integration.Contracts.Receipts.ReciboEmitidoV1);
        if (recibo?.Payload is not Common.Integration.Contracts.Receipts.ReciboEmitidoV1 r || string.IsNullOrWhiteSpace(r.Number)) return null;
        return Clave(r.Number, recibo.Envelope.BranchPublicId, recibo.Envelope.Origin.OperationDate);
    }

    /// <summary>La clave de una unidad de Cartera que abona con un recibo de caja de Recaudo, o nula.</summary>
    public static string? ClaveDeCartera(IReadOnlyList<MensajeEntrante> unidad)
    {
        if (unidad.Count == 0 || unidad.Any(m => m.Envelope.Kind == IntegrationMessageKind.Informational)) return null;
        var principal = ConstructorDeLineasDeCartera.Principal(unidad.Select(HechoDeCartera.De).ToList());
        if (principal is null || !ComprobantePorRecibo.HechosDeRecibo.Contains(principal.Tipo, StringComparer.Ordinal)) return null;
        if (ComprobantePorRecibo.Recaudo(principal.Contenido) is not { Receipt.Number: { Length: > 0 } numero } recaudo
            || !string.Equals(recaudo.CollectorCode, ModulosDeOrigen.Recaudo, StringComparison.Ordinal))
            return null;
        // La oficina del recibo; si el recaudo no la trae, la sucursal del sobre (la del recibo, que también es la del hecho de Recaudo).
        return Clave(numero, recaudo.Receipt.OfficePublicId ?? principal.Sobre.BranchPublicId, principal.Sobre.Origin.OperationDate);
    }

    private static string Clave(string numero, Guid oficina, DateOnly fecha) =>
        string.Create(CultureInfo.InvariantCulture, $"{ReciboEnUnSoloComprobante.PrefijoDeGrupo}{numero.Trim()}|{oficina:N}|{fecha:yyyy-MM-dd}");

    /// <summary>Los grupos del recibo con su caja: la unidad de Recaudo primero y las de Cartera detrás, en su orden. Sólo los que casan.</summary>
    public static IReadOnlyList<TrabajoDeConsumo> Planear(
        IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> deRecaudo,
        IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> deCartera)
    {
        ArgumentNullException.ThrowIfNull(deRecaudo);
        ArgumentNullException.ThrowIfNull(deCartera);
        var cajas = deRecaudo.Select(x => (x.Unidad, Clave: ClaveDeRecaudo(x.Mensajes))).Where(x => x.Clave is not null)
            .GroupBy(x => x.Clave!, StringComparer.Ordinal)
            .Where(g => g.Count() == 1) // dos unidades de Recaudo con la misma clave: no se adivina cuál es; van como siempre
            .ToDictionary(g => g.Key, g => g.Single().Unidad, StringComparer.Ordinal);
        var abonos = new Dictionary<string, List<UnidadDeConsumo>>(StringComparer.Ordinal);
        foreach (var (unidad, mensajes) in deCartera)
        {
            if (ClaveDeCartera(mensajes) is not { } clave || !cajas.ContainsKey(clave)) continue;
            if (!abonos.TryGetValue(clave, out var lista)) abonos[clave] = lista = [];
            lista.Add(unidad);
        }
        return deRecaudo.Select(x => ClaveDeRecaudo(x.Mensajes)).Where(c => c is not null && abonos.ContainsKey(c)).Distinct()
            .Select(c => new TrabajoDeConsumo(c!, [cajas[c!], .. abonos[c!]])).ToList();
    }
}
