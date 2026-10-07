using System.Diagnostics;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La validación previa contable del lado de Cartera (feature 013, T0228, C106; FR-127, T56, enmienda A8; contracts/contabilidad.md
/// §13). Esqueleto con firma estable: pregunta por <see cref="IContabilidadParaCartera.EvaluarAsync"/> los hechos tal como se emitirían,
/// <b>fuera del cerrojo</b> y con el tiempo máximo de <c>Contabilidad.ValidacionPreviaSegundos</c>, y <b>nunca bloquea</b>: devuelve el
/// sello de <see cref="PrevalidationOutcome"/> que la operación pone en sus hechos.
/// <list type="bullet">
/// <item>sin hechos que evaluar, o con el puerto provisional (que dice que la evaluación no aplica): <c>NotApplicable</c>;</item>
/// <item>una excepción, un fallo o el tiempo agotado: <c>NoResponse</c>;</item>
/// <item>con hallazgos: <c>NotPostable</c> y los hallazgos; sin ellos: <c>Postable</c> (con los avisos).</item>
/// </list>
/// US7 suma la reducción a una pregunta por combinación distinta, el bloqueo en las pantallas de Cartera por
/// <c>Contabilidad.FaltaDeRegla</c> y <c>Contabilidad.PoliticaSinRespuesta</c> y la alerta <c>Cartera.HechoNoContabilizable</c>. (nuevo)
/// </summary>
public sealed class ValidacionPreviaContableDeCartera(
    IContabilidadParaCartera contabilidad,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ILogger<ValidacionPreviaContableDeCartera>? logger = null)
{
    /// <summary>Los segundos por defecto si el parámetro no se puede leer (el defecto seguro del catálogo).</summary>
    public const int SegundosPorDefecto = 3;

    private readonly ILogger _log = logger ?? NullLogger<ValidacionPreviaContableDeCartera>.Instance;

    /// <summary>Evalúa los hechos de una operación. Nunca falla por el puerto; una cancelación de la petición sí se propaga.</summary>
    public async Task<ResultadoDeValidacionPreviaDeCartera> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        if (hechos.Count == 0) return ResultadoDeValidacionPreviaDeCartera.NoAplica;

        var segundos = await SegundosAsync(ct);
        var cronometro = Stopwatch.StartNew();
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tiempo.CancelAfter(TimeSpan.FromSeconds(segundos));
        try
        {
            var r = await contabilidad.EvaluarAsync(hechos, tiempo.Token);
            if (r.IsFailure)
            {
                if (r.Error.Code == ContabilidadParaCarteraProvisional.CodigoEvaluacionNoDisponible)
                    return ResultadoDeValidacionPreviaDeCartera.NoAplica;
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
