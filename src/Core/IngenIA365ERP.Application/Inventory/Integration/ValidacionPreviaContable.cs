using System.Diagnostics;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>Lo que respondió Contabilidad a la pregunta «¿es contabilizable?»: si respondió, su resultado y cuánto tardó. (nuevo)</summary>
public sealed record EvaluacionPreviaContable(bool Respondio, ResultadoDeContabilizacionDto? Resultado, long ElapsedMs);

/// <summary>
/// La validación previa contable del lado de Inventario (feature 012, T520, T521; FR-074; T30; contracts/contabilidad.md §4;
/// api.md §9.3, §25.1). Es el <see cref="IPasoDeValidacionPrevia"/> registrado desde I2: la confirmación le pasa los sobres que
/// emitiría (<see cref="MensajesDelDocumento.Sobres"/>, con los costos provisionales) <b>antes del cerrojo</b>, y ella:
/// <list type="number">
/// <item>si no hay mensajes de negocio a Contabilidad, responde <c>NotApplicable</c> sin preguntar;</item>
/// <item>pregunta por <see cref="IContabilidadParaInventario.EvaluarAsync"/>, en proceso, con un <see cref="CancellationTokenSource"/>
/// de <c>Contabilidad.ValidacionPreviaSegundos</c> (3 por defecto);</item>
/// <item>no contabilizable → <c>Inventory.Prevalidation.NotPostable</c> con <c>data.errors[] { lineNumber, account, rule, message,
/// whoFixes }</c> (una entrada por línea del documento que el hallazgo nombra) y el documento sigue en borrador;</item>
/// <item>«no responde» —una excepción, un fallo del puerto o el tiempo agotado— aplica <c>Contabilidad.PoliticaSinRespuesta</c>:
/// <c>ConfirmarConPendiente</c> confirma con el aviso <c>Inventory.Prevalidation.NoResponse</c> y sella <c>NoResponse</c>;
/// <c>Bloquear</c> responde 422 <c>Inventory.Prevalidation.NoResponse</c>;</item>
/// <item>contabilizable → <c>Postable</c>, con los avisos (<c>Accounting.Line.TaxAmountDiffers</c>) que no impiden.</item>
/// </list>
/// El puerto corre en el mismo contexto de datos de la petición y sin seguimiento: cancelarlo por tiempo no deja nada a medias. (nuevo)
/// </summary>
public sealed class ValidacionPreviaContable(
    IContabilidadParaInventario contabilidad,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ILogger<ValidacionPreviaContable>? logger = null) : IPasoDeValidacionPrevia
{
    public const string ConfirmarConPendiente = "ConfirmarConPendiente";
    public const string Bloquear = "Bloquear";

    /// <summary>El código del aviso cuando se confirma sin respuesta de Contabilidad.</summary>
    public const string AvisoSinRespuesta = "Inventory.Prevalidation.NoResponse";

    /// <summary>Los segundos por defecto si el parámetro no se puede leer (el defecto seguro del catálogo).</summary>
    public const int SegundosPorDefecto = 3;

    private readonly ILogger _log = logger ?? NullLogger<ValidacionPreviaContable>.Instance;

    public async Task<Result<ResultadoDeValidacionPrevia>> EvaluarAsync(ContextoDeEfecto contexto, IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct)
    {
        if (!MensajesDelDocumento.HayNegocioAContabilidad(mensajes))
            return Result.Success(new ResultadoDeValidacionPrevia(PrevalidationOutcome.NotApplicable, []));

        var evaluacion = await PreguntarAsync(mensajes, ct);
        if (!evaluacion.Respondio)
        {
            if (await PoliticaAsync(ct) == Bloquear) return Result.Failure<ResultadoDeValidacionPrevia>(InventoryErrors.PrevalidationNoResponse());
            return Result.Success(new ResultadoDeValidacionPrevia(PrevalidationOutcome.NoResponse,
                [new AvisoDto(AvisoSinRespuesta, "Contabilidad no respondió a tiempo: el documento se confirma y sus mensajes quedan pendientes de entrega.", null)]));
        }

        var resultado = evaluacion.Resultado!;
        if (!resultado.IsPostable) return Result.Failure<ResultadoDeValidacionPrevia>(InventoryErrors.PrevalidationNotPostable(Errores(resultado)));
        return Result.Success(new ResultadoDeValidacionPrevia(PrevalidationOutcome.Postable, Avisos(resultado)));
    }

    /// <summary>
    /// Pregunta a Contabilidad con el tiempo máximo de <c>Contabilidad.ValidacionPreviaSegundos</c>. Nunca lanza por el puerto: una
    /// excepción, un fallo o el tiempo agotado es «no respondió» (se anota en el log). Una cancelación de la petición sí se propaga.
    /// </summary>
    public async Task<EvaluacionPreviaContable> PreguntarAsync(IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct)
    {
        var segundos = await SegundosAsync(ct);
        var cronometro = Stopwatch.StartNew();
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tiempo.CancelAfter(TimeSpan.FromSeconds(segundos));
        try
        {
            var r = await contabilidad.EvaluarAsync(mensajes, tiempo.Token);
            cronometro.Stop();
            if (r.IsFailure)
            {
                _log.LogWarning("[Inventario.ValidacionPrevia] Contabilidad respondió con falla {Codigo}: {Mensaje}", r.Error.Code, r.Error.Message);
                return new EvaluacionPreviaContable(false, null, cronometro.ElapsedMilliseconds);
            }
            return new EvaluacionPreviaContable(true, r.Value, cronometro.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("[Inventario.ValidacionPrevia] Contabilidad no respondió en {Segundos} s", segundos);
            return new EvaluacionPreviaContable(false, null, cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Inventario.ValidacionPrevia] La consulta a Contabilidad falló");
            return new EvaluacionPreviaContable(false, null, cronometro.ElapsedMilliseconds);
        }
    }

    /// <summary><c>Contabilidad.PoliticaSinRespuesta</c> vigente hoy (<c>ConfirmarConPendiente</c> por defecto).</summary>
    public async Task<string> PoliticaAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadPoliticaSinRespuesta, reloj.HoyLocal, ct: ct);
        return leido.IsSuccess && leido.Value.Texto == Bloquear ? Bloquear : ConfirmarConPendiente;
    }

    private async Task<int> SegundosAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadValidacionPreviaSegundos, reloj.HoyLocal, ct: ct);
        return leido.IsSuccess && int.TryParse(leido.Value.Texto, out var s) && s > 0 ? s : SegundosPorDefecto;
    }

    /// <summary>Los errores del 422: uno por línea del documento que nombra cada hallazgo (o uno sin línea).</summary>
    public static IReadOnlyList<InventoryErrors.ErrorDeValidacionPrevia> Errores(ResultadoDeContabilizacionDto resultado) =>
        resultado.Errors
            .SelectMany(h => (h.DocumentLines.Count == 0 ? [(int?)null] : h.DocumentLines.Distinct().Order().Select(n => (int?)n))
                .Select(linea => new InventoryErrors.ErrorDeValidacionPrevia(linea, h.AccountCode, h.Rule, h.Message,
                    new { module = h.WhoFixes.Module, page = h.WhoFixes.Page, permission = h.WhoFixes.Permission })))
            .ToList();

    /// <summary>Los avisos que no impiden confirmar, con el tipo de mensaje, las líneas y la cuenta en <c>data</c>.</summary>
    public static IReadOnlyList<AvisoDto> Avisos(ResultadoDeContabilizacionDto resultado) =>
        resultado.Warnings
            .Select(h => new AvisoDto(h.Rule, h.Message, new { messageType = h.MessageType, documentLines = h.DocumentLines, account = h.AccountCode }))
            .ToList();
}
