using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// De dónde viene la operación que pregunta si es contabilizable (contabilidad.md §13.2; A8, C80). Con <c>Contabilidad.FaltaDeRegla = Bloquear</c>
/// <b>todos</b> bloquean (respuesta del dueño a A8, 2026-10-14, «Bloquear todo»), cada uno a su manera: la pantalla responde el 422, el contrato
/// rechaza el ítem con su código, el consumidor de Comercial deja la venta en reproceso y el proceso detiene la tanda y alerta. Sin respuesta de
/// Contabilidad (<c>PoliticaSinRespuesta</c>) sólo bloquea la pantalla. (nuevo)
/// </summary>
public enum OrigenDeValidacionPrevia
{
    /// <summary>Desembolso; novedad, condonación y castigo (K3) por la misma vía.</summary>
    Pantalla = 1,

    /// <summary>El contrato de obligaciones (Recaudo, Nómina, Aportes, recogida, recaudador de prueba).</summary>
    Contrato = 2,

    /// <summary>El consumidor de Comercial (obligación desde una venta, ajustes; D9).</summary>
    ConsumidorDeComercial = 3,

    /// <summary>Causación, cierre, réplica, corrida de corrección y tareas.</summary>
    Proceso = 4,

    /// <summary>Los cobros provisionales incorporados al activar.</summary>
    CobroProvisional = 5,
}

/// <summary>
/// La validación previa contable del lado de Cartera (feature 013, T0228, T0655, C106; FR-127, T56, enmienda A8, C80; contracts/
/// contabilidad.md §13; molde <c>ValidacionPreviaContable</c> de la 012). La firma de la fase 3 no cambia.
/// <list type="number">
/// <item><see cref="EvaluarAsync"/> deja fuera lo que no pasaría a Contabilidad —los informativos, los mensajes a otros destinos y los hechos
/// cuyo modo resuelto es <c>NoPasa</c> (por <see cref="ModoDePasoDeCartera"/>: el puerto no trae el modo sellado)—, reduce lo demás a <b>una
/// pregunta por combinación distinta</b> de hecho, sucursal y dimensiones, y pregunta por <see cref="IContabilidadParaCartera.EvaluarAsync"/>
/// <b>fuera del cerrojo</b> con el tiempo máximo de <c>Contabilidad.ValidacionPreviaSegundos</c>. Una excepción, una falla o el tiempo
/// agotado es <c>NoResponse</c>; con hallazgos <c>NotPostable</c>; sin ellos <c>Postable</c>; sin nada que preguntar <c>NotApplicable</c>.</item>
/// <item><see cref="DecidirAsync"/> decide: con <c>Contabilidad.FaltaDeRegla = Bloquear</c> (que gobierna <b>todo</b> hallazgo, cuenta inactiva
/// incluida) el bloqueo es <c>Lending.Prevalidation.NotPostable</c> con <c>data.errors[] { factType, role, combination, account, rule, whoFixes }</c>
/// <b>en cualquier origen</b> (A8 = «Bloquear todo», respuesta del dueño del 2026-10-14; hasta ese día sólo bloqueaba la pantalla y lo demás
/// advertía); fuera de la pantalla, además, levanta la alerta (quien opera no puede corregir la matriz). Con
/// <c>Contabilidad.PoliticaSinRespuesta = Bloquear</c> sólo la pantalla responde <c>Lending.Prevalidation.NoResponse</c> (422): A8 no la tocó.
/// Con <c>Advertir</c> (hasta el 2026-10-14 el defecto; desde entonces el defecto es <c>Bloquear</c>) la operación confirma y sella el resultado; un <c>NotPostable</c> levanta <c>Cartera.HechoNoContabilizable</c>,
/// una por combinación y día, para quienes tienen <c>Accounting.LendingRules.Manage</c> y <c>Lending.Messages.Reprocess</c>. Sin contabilidad
/// iniciada o con el hecho en <c>NoPasa</c> no se pregunta nada y no hay qué bloquear (§10.1).</item>
/// <item><see cref="DecidirEnProcesoAsync"/>: la de los procesos (causación, corrección, cierre), que sólo preguntan si la política podría bloquear
/// y sin tiempo máximo (corren dentro de su transacción: cancelar la consulta abortaría la de PostgreSQL).</item>
/// </list>
/// La reserva de número de crédito no se evalúa (no produce hecho, T22). (nuevo)
/// </summary>
public sealed class ValidacionPreviaContableDeCartera(
    IContabilidadParaCartera contabilidad,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ILogger<ValidacionPreviaContableDeCartera>? logger = null,
    ModoDePasoDeCartera? modoDePaso = null,
    IAlertas? alertas = null,
    IApplicationDbContext? db = null)
{
    /// <summary>Los segundos por defecto si el parámetro no se puede leer (el defecto seguro del catálogo).</summary>
    public const int SegundosPorDefecto = 3;

    /// <summary>El valor de <c>Contabilidad.FaltaDeRegla</c> y <c>Contabilidad.PoliticaSinRespuesta</c> que bloquea en pantalla.</summary>
    public const string Bloquear = "Bloquear";

    /// <summary>Los permisos que reciben la alerta <c>Cartera.HechoNoContabilizable</c> (§13.2).</summary>
    public static IReadOnlyList<string> DestinatariosDeLaAlerta { get; } = ["Accounting.LendingRules.Manage", "Lending.Messages.Reprocess"];

    private readonly ILogger _log = logger ?? NullLogger<ValidacionPreviaContableDeCartera>.Instance;

    /// <summary>
    /// Evalúa los hechos de una operación. Nunca falla por el puerto; una cancelación de la petición sí se propaga. Con
    /// <paramref name="sinTiempoMaximo"/> (los procesos) no corta la consulta a los <c>Contabilidad.ValidacionPreviaSegundos</c>.
    /// </summary>
    public async Task<ResultadoDeValidacionPreviaDeCartera> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct,
        bool sinTiempoMaximo = false)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        var aPreguntar = Reducir(await QueSiPasanAsync(hechos, ct));
        if (aPreguntar.Count == 0) return ResultadoDeValidacionPreviaDeCartera.NoAplica;

        var segundos = await SegundosAsync(ct);
        var cronometro = Stopwatch.StartNew();
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!sinTiempoMaximo) tiempo.CancelAfter(TimeSpan.FromSeconds(segundos));
        try
        {
            var r = await contabilidad.EvaluarAsync(aPreguntar, tiempo.Token);
            if (r.IsFailure)
            {
                _log.LogWarning("[Cartera.ValidacionPrevia] Contabilidad respondió con falla {Codigo}: {Mensaje}", r.Error.Code, r.Error.Message);
                return ResultadoDeValidacionPreviaDeCartera.SinRespuesta;
            }

            var resultado = r.Value;
            return resultado.IsPostable
                ? new ResultadoDeValidacionPreviaDeCartera(PrevalidationOutcome.Postable, [], resultado.Warnings)
                : new ResultadoDeValidacionPreviaDeCartera(PrevalidationOutcome.NotPostable, resultado.Errors, resultado.Warnings);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("[Cartera.ValidacionPrevia] Contabilidad no respondió en {Segundos} s ({Ms} ms)", segundos, cronometro.ElapsedMilliseconds);
            return ResultadoDeValidacionPreviaDeCartera.SinRespuesta;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.ValidacionPrevia] La consulta a Contabilidad falló");
            return ResultadoDeValidacionPreviaDeCartera.SinRespuesta;
        }
    }

    /// <summary>
    /// Evalúa y decide por origen (§13.2): el resultado sellable, el bloqueo (sólo en pantalla, con la política en <c>Bloquear</c>) y los
    /// avisos para el usuario. Sin bloqueo, un <c>NotPostable</c> levanta la alerta. <paramref name="operacion"/> es el rótulo de la operación
    /// (número o descripción) para la alerta.
    /// </summary>
    public async Task<DecisionDeValidacionPrevia> DecidirAsync(
        IReadOnlyList<MensajeContableDto> hechos, OrigenDeValidacionPrevia origen, string operacion, CancellationToken ct) =>
        await DecidirAsync(hechos, origen, operacion, alertar: true, ct);

    /// <summary>
    /// Como el anterior; con <paramref name="alertar"/> en falso no levanta la alerta (una vista previa no confirma nada: la operación la levanta
    /// al confirmar con <see cref="AlertarAsync(ResultadoDeValidacionPreviaDeCartera, string, CancellationToken)"/>).
    /// </summary>
    public async Task<DecisionDeValidacionPrevia> DecidirAsync(
        IReadOnlyList<MensajeContableDto> hechos, OrigenDeValidacionPrevia origen, string operacion, bool alertar, CancellationToken ct)
    {
        var resultado = await EvaluarAsync(hechos, ct);
        var hoy = reloj.HoyLocal;
        Error? bloqueo = null;
        var avisos = new List<string>();

        if (resultado.Outcome == PrevalidationOutcome.NotPostable)
        {
            // A8 = «Bloquear todo» (2026-10-14): la política bloquea en cualquier origen. Fuera de la pantalla también alerta: quien opera (Recaudo,
            // Nómina, Comercial) no corrige la matriz.
            if (await FaltaDeReglaBloqueaAsync(resultado.Errors, hoy, ct))
            {
                bloqueo = ErroresDeValidacionPreviaDeCartera.NoContabilizable(resultado.Errors);
                if (alertar && origen != OrigenDeValidacionPrevia.Pantalla) await AlertarAsync(resultado.Errors, operacion, hoy, bloqueada: true, ct);
            }
            else
            {
                avisos.Add("Contabilidad no tiene cómo registrar todo lo de esta operación: sigue y queda marcada para que la contadora complete la matriz.");
                if (alertar) await AlertarAsync(resultado.Errors, operacion, hoy, bloqueada: false, ct);
            }
        }
        else if (resultado.Outcome == PrevalidationOutcome.NoResponse)
        {
            if (origen == OrigenDeValidacionPrevia.Pantalla && await LeeBloquearAsync(ParametrosDeCartera.ContabilidadPoliticaSinRespuesta, hoy,
                    ParameterScopeKind.None, 0, ct))
                bloqueo = ErroresDeValidacionPreviaDeCartera.SinRespuesta();
            else avisos.Add("Contabilidad no respondió a tiempo: la operación sigue y queda marcada para revisión.");
        }

        avisos.AddRange(resultado.Warnings.Select(w => w.Message));
        return new DecisionDeValidacionPrevia(resultado, bloqueo, avisos);
    }

    /// <summary>
    /// La decisión de un proceso (causación, corrección, cierre; A8 = «Bloquear todo»): nulo si la política <c>Contabilidad.FaltaDeRegla</c> no podría
    /// bloquear ninguno de los tipos de <paramref name="hechos"/> —con <c>Advertir</c> los procesos no preguntan, como hasta el 2026-10-14— o si lo
    /// evaluado se puede contabilizar; si bloquea, la decisión con su error. Evalúa sin tiempo máximo y sin alertar: el proceso alerta después de
    /// deshacer su tanda (<see cref="AlertarBloqueoAsync"/>).
    /// </summary>
    public async Task<DecisionDeValidacionPrevia?> DecidirEnProcesoAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        var hoy = reloj.HoyLocal;
        if (hechos.Count == 0 || !await FaltaDeReglaPuedeBloquearAsync(hechos.Select(h => h.Envelope.Type), hoy, ct)) return null;
        var resultado = await EvaluarAsync(hechos, ct, sinTiempoMaximo: true);
        if (resultado.Outcome != PrevalidationOutcome.NotPostable || !await FaltaDeReglaBloqueaAsync(resultado.Errors, hoy, ct)) return null;
        return new DecisionDeValidacionPrevia(resultado, ErroresDeValidacionPreviaDeCartera.NoContabilizable(resultado.Errors), []);
    }

    /// <summary>La alerta de una operación que <b>no</b> se confirmó porque la política bloqueó (A8): una por combinación y día.</summary>
    public Task AlertarBloqueoAsync(ResultadoDeValidacionPreviaDeCartera resultado, string operacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.Outcome == PrevalidationOutcome.NotPostable
            ? AlertarAsync(resultado.Errors, operacion, reloj.HoyLocal, bloqueada: true, ct)
            : Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ filtro --

    /// <summary>Lo que pasaría a Contabilidad: hechos de negocio a <c>Accounting</c> cuyo modo resuelto no es <c>NoPasa</c>.</summary>
    private async Task<List<MensajeContableDto>> QueSiPasanAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct)
    {
        var quedan = new List<MensajeContableDto>(hechos.Count);
        foreach (var h in hechos)
        {
            ArgumentNullException.ThrowIfNull(h);
            var tipo = CatalogoDeMensajes.DelModulo(ModulosDeOrigen.Cartera).FirstOrDefault(t => t.Type == h.Envelope.Type);
            if (tipo is not null && (tipo.Kind == IntegrationMessageKind.Informational || !tipo.AdmiteDestino(IntegrationDestinations.Accounting))) continue;
            if (tipo is not null && modoDePaso is not null)
            {
                var fecha = h.Envelope.Origin.OperationDate == default ? reloj.HoyLocal : h.Envelope.Origin.OperationDate;
                var modo = await modoDePaso.ResolverAsync(tipo.Type, fecha, ct);
                if (modo.IsSuccess && modo.Value is ModoDeEntrega.Sellado { Modo: DeliveryMode.NotPosted or DeliveryMode.Always }) continue;
            }
            quedan.Add(h);
        }
        return quedan;
    }

    /// <summary>
    /// Una pregunta por combinación distinta (§13.1): mismo tipo, sucursal, dimensiones, clase de recaudador, forma y conceptos con su motivo y su
    /// signo; los importes no cuentan. Una nómina de 500 empleados se reduce a unas pocas combinaciones.
    /// </summary>
    public static IReadOnlyList<MensajeContableDto> Reducir(IReadOnlyList<MensajeContableDto> hechos)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var quedan = new List<MensajeContableDto>();
        foreach (var h in hechos)
            if (vistas.Add(ClaveDeCombinacion(h))) quedan.Add(h);
        return quedan;
    }

    /// <summary>La clave de combinación de un hecho: tipo, sucursal y el contenido sin importes ni identificadores de crédito.</summary>
    public static string ClaveDeCombinacion(MensajeContableDto h)
    {
        ArgumentNullException.ThrowIfNull(h);
        var b = new StringBuilder().Append(h.Envelope.Type).Append('|').Append(h.Envelope.BranchPublicId.ToString("N"));
        if (h.Payload is not null)
        {
            using var doc = JsonDocument.Parse(OpcionesDeMensajes.Serializar(h.Payload));
            Escribir(b.Append('|'), doc.RootElement);
        }
        return b.ToString();
    }

    private static readonly HashSet<string> SinCombinacion = new(StringComparer.OrdinalIgnoreCase)
    {
        "amount", "balanceAfter", "loan", "loanPublicId", "loanNumber", "valueDate", "registeredAt", "debtorPersonPublicId", "messageId",
        "number", "publicId", "dueDate", "installmentNumber", "rulesVersion", "subject", "causeFact", "original", "beneficiary",
    };

    private static void Escribir(StringBuilder b, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                b.Append('{');
                foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (SinCombinacion.Contains(p.Name)) continue;
                    b.Append(p.Name).Append(':');
                    Escribir(b, p.Value);
                    b.Append(',');
                }
                b.Append('}');
                break;
            case JsonValueKind.Array:
                var partes = e.EnumerateArray().Select(x => { var s = new StringBuilder(); Escribir(s, x); return s.ToString(); })
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
                b.Append('[').AppendJoin(';', partes).Append(']');
                break;
            case JsonValueKind.Number:
                b.Append(e.TryGetDecimal(out var n) ? Math.Sign(n).ToString(CultureInfo.InvariantCulture) : "n");
                break;
            default:
                b.Append(e.ToString());
                break;
        }
    }

    // ---------------------------------------------------------------------------------------------- políticas y alerta --

    /// <summary>
    /// <c>Contabilidad.FaltaDeRegla</c> (§13.2): con el ámbito del tipo de hecho de cada hallazgo; basta uno en <c>Bloquear</c>.
    /// </summary>
    private async Task<bool> FaltaDeReglaBloqueaAsync(IReadOnlyList<HallazgoDeCarteraDto> hallazgos, DateOnly hoy, CancellationToken ct)
    {
        var tipos = hallazgos.Select(h => h.FactType).Distinct(StringComparer.Ordinal).ToList();
        var ids = db is null ? new Dictionary<string, int>()
            : await db.LendingFactTypes.AsNoTracking().Where(f => tipos.Contains(f.Code) && !f.IsDeleted).ToDictionaryAsync(f => f.Code, f => f.Id, ct);
        if (tipos.Count == 0) tipos.Add(string.Empty);
        foreach (var tipo in tipos)
        {
            var (ambito, id) = ids.TryGetValue(tipo, out var fila) ? (ParameterScopeKind.LendingFactType, fila) : (ParameterScopeKind.None, 0);
            if (await LeeBloquearAsync(ParametrosDeCartera.ContabilidadFaltaDeRegla, hoy, ambito, id, ct)) return true;
        }
        return false;
    }

    /// <summary>¿<c>Contabilidad.FaltaDeRegla</c> está en <c>Bloquear</c> en general o para alguno de esos tipos de hecho?</summary>
    public async Task<bool> FaltaDeReglaPuedeBloquearAsync(IEnumerable<string> tiposDeHecho, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tiposDeHecho);
        var tipos = tiposDeHecho.Distinct(StringComparer.Ordinal).ToList();
        if (await LeeBloquearAsync(ParametrosDeCartera.ContabilidadFaltaDeRegla, hoy, ParameterScopeKind.None, 0, ct)) return true;
        if (db is null) return false;
        var ids = await db.LendingFactTypes.AsNoTracking().Where(f => tipos.Contains(f.Code) && !f.IsDeleted).Select(f => f.Id).ToListAsync(ct);
        foreach (var id in ids)
            if (await LeeBloquearAsync(ParametrosDeCartera.ContabilidadFaltaDeRegla, hoy, ParameterScopeKind.LendingFactType, id, ct)) return true;
        return false;
    }

    private async Task<bool> LeeBloquearAsync(string clave, DateOnly fecha, ParameterScopeKind ambito, int id, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.Modulo, clave, fecha, ambito, id, ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto, Bloquear, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>La alerta <c>Cartera.HechoNoContabilizable</c> de un resultado <c>NotPostable</c> confirmado con advertencia; si no, nada.</summary>
    public Task AlertarAsync(ResultadoDeValidacionPreviaDeCartera resultado, string operacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.Outcome == PrevalidationOutcome.NotPostable
            ? AlertarAsync(resultado.Errors, operacion, reloj.HoyLocal, bloqueada: false, ct)
            : Task.CompletedTask;
    }

    private async Task AlertarAsync(IReadOnlyList<HallazgoDeCarteraDto> hallazgos, string operacion, DateOnly hoy, bool bloqueada, CancellationToken ct)
    {
        if (alertas is null) return;
        var dia = hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var g in hallazgos.GroupBy(h => (h.FactType, Combinacion: Combinacion(h))))
        {
            var h = g.First();
            await alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.HechoNoContabilizable,
                $"{h.FactType} de {operacion} no es contabilizable",
                (bloqueada ? $"La operación {operacion} no se confirmó porque Contabilidad no puede registrar {h.FactType}"
                           : $"La operación {operacion} se confirmó, pero Contabilidad no puede registrar {h.FactType}")
                    + (h.Role is null ? string.Empty : $" en el rol {h.Role}") + (g.Key.Combinacion.Length == 0 ? string.Empty : $" ({g.Key.Combinacion})")
                    + $": {h.Message} Quien corrige: {h.WhoFixes.Module}. "
                    + (bloqueada ? "Corregida la matriz, repita la operación (un proceso detenido se reanuda con «Reintentar»)."
                                 : "Corregido, reprocese el hecho desde la bandeja de Cartera."),
                DedupKey: $"{TiposDeAlerta.HechoNoContabilizable}:{h.FactType}:{g.Key.Combinacion}:{dia}",
                RecipientPermissions: DestinatariosDeLaAlerta,
                ModuloDeOrigen: "Lending"), ct);
        }
    }

    private static string Combinacion(HallazgoDeCarteraDto h) =>
        h.Combination is null ? string.Empty
            : string.Join(";", h.Combination.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));

    private async Task<int> SegundosAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadValidacionPreviaSegundos,
            reloj.HoyLocal, ct: ct);
        return leido.IsSuccess && int.TryParse(leido.Value.Texto, out var s) && s > 0 ? s : SegundosPorDefecto;
    }
}

/// <summary>
/// El sello de la validación previa y lo que la operación muestra: los hallazgos (con <c>NotPostable</c>) y los avisos. No impide
/// nada: la operación confirma y sella <see cref="Outcome"/> en sus hechos. (nuevo)
/// </summary>
public sealed record ResultadoDeValidacionPreviaDeCartera(
    PrevalidationOutcome Outcome,
    IReadOnlyList<HallazgoDeCarteraDto> Errors,
    IReadOnlyList<HallazgoDeCarteraDto> Warnings)
{
    public static readonly ResultadoDeValidacionPreviaDeCartera NoAplica = new(PrevalidationOutcome.NotApplicable, [], []);

    public static readonly ResultadoDeValidacionPreviaDeCartera SinRespuesta = new(PrevalidationOutcome.NoResponse, [], []);
}

/// <summary>
/// La decisión por origen (§13.2): el resultado que se sella, el bloqueo —con la política en <c>Bloquear</c>, en cualquier origen desde A8 = «Bloquear
/// todo»— y los avisos en español claro para quien confirma. (nuevo)
/// </summary>
public sealed record DecisionDeValidacionPrevia(ResultadoDeValidacionPreviaDeCartera Resultado, Error? Bloqueo, IReadOnlyList<string> Avisos)
{
    public PrevalidationOutcome Outcome => Resultado.Outcome;

    public bool Bloquea => Bloqueo is not null;
}

/// <summary>Los dos errores de la validación previa de Cartera (data-model §10.1; contabilidad.md §13.3). (nuevo)</summary>
public static class ErroresDeValidacionPreviaDeCartera
{
    public const string CodigoNoContabilizable = "Lending.Prevalidation.NotPostable";
    public const string CodigoSinRespuesta = "Lending.Prevalidation.NoResponse";

    /// <summary>
    /// Hallazgos con <c>Contabilidad.FaltaDeRegla = Bloquear</c> (A8 = «Bloquear todo»): 422 en pantalla, rechazo del ítem en el contrato, venta en
    /// reproceso en el consumidor de Comercial y tanda detenida en un proceso.
    /// </summary>
    public static Error NoContabilizable(IReadOnlyList<HallazgoDeCarteraDto> hallazgos)
    {
        ArgumentNullException.ThrowIfNull(hallazgos);
        var primero = hallazgos.FirstOrDefault();
        var que = primero is null ? "lo que registra esta operación"
            : primero.Role is null ? primero.FactType : $"{primero.FactType} en el rol {primero.Role}";
        var quien = primero?.WhoFixes.Module == "Lending" ? "Es un defecto de Cartera: comuníquelo a soporte."
            : "Pida a la contadora completar la matriz de Cartera.";
        return new ErrorConDatos(CodigoNoContabilizable,
            $"Contabilidad no tiene cómo registrar {que}. {quien}",
            new
            {
                errors = hallazgos.Select(h => new
                {
                    factType = h.FactType,
                    role = h.Role,
                    combination = h.Combination,
                    account = h.AccountCode,
                    rule = h.Rule,
                    whoFixes = h.WhoFixes,
                }).ToList(),
            });
    }

    /// <summary>422: Contabilidad no respondió y <c>Contabilidad.PoliticaSinRespuesta = Bloquear</c> en una pantalla de Cartera.</summary>
    public static Error SinRespuesta() => new(CodigoSinRespuesta,
        "Contabilidad no respondió a tiempo y la cooperativa pide no confirmar sin su respuesta. Intente de nuevo en unos minutos.");
}
