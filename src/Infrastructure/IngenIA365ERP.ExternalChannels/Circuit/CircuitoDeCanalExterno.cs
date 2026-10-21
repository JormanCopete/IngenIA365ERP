using System.Collections.Concurrent;
using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.ExternalChannels.Circuit;

/// <summary>
/// Las fallas seguidas y la apertura de cada circuito, por cooperativa y canal (feature 013, T0081; §3.5). Vive en memoria del proceso
/// (singleton): cada réplica cuenta lo suyo y un reinicio lo pone en cero, que sólo retrasa la apertura. (nuevo)
/// </summary>
public sealed class EstadoDeLosCircuitosExternos
{
    /// <summary>Cada cuánto se sondea un circuito abierto (§3.5).</summary>
    public static readonly TimeSpan IntervaloDeSondeo = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(Guid Cooperativa, string Canal), Estado> _estados = new();

    public int Fallas(Guid cooperativa, string canal) => _estados.TryGetValue(Clave(cooperativa, canal), out var e) ? e.Fallas : 0;

    public bool Abierto(Guid cooperativa, string canal) => _estados.TryGetValue(Clave(cooperativa, canal), out var e) && e.AbiertoDesde is not null;

    internal Estado Leer(Guid cooperativa, string canal) => _estados.TryGetValue(Clave(cooperativa, canal), out var e) ? e : Estado.Cerrado;

    internal Estado SumarFalla(Guid cooperativa, string canal) =>
        _estados.AddOrUpdate(Clave(cooperativa, canal), _ => new Estado(1, null, null), (_, e) => e with { Fallas = e.Fallas + 1 });

    internal void Abrir(Guid cooperativa, string canal, DateTime ahora) =>
        _estados.AddOrUpdate(Clave(cooperativa, canal), _ => new Estado(1, ahora, ahora), (_, e) => e with { AbiertoDesde = ahora, UltimoSondeo = ahora });

    internal void AnotarSondeo(Guid cooperativa, string canal, DateTime ahora) =>
        _estados.AddOrUpdate(Clave(cooperativa, canal), _ => new Estado(0, null, ahora), (_, e) => e with { UltimoSondeo = ahora });

    internal void Cerrar(Guid cooperativa, string canal) => _estados.TryRemove(Clave(cooperativa, canal), out _);

    private static (Guid, string) Clave(Guid cooperativa, string canal) => (cooperativa, canal.Trim().ToUpperInvariant());

    internal sealed record Estado(int Fallas, DateTime? AbiertoDesde, DateTime? UltimoSondeo)
    {
        public static Estado Cerrado { get; } = new(0, null, null);
    }
}

/// <summary>
/// El circuito de cada canal externo por cooperativa (feature 013, T0081; T72; contracts/adaptadores-y-archivos.md §3.5). Cuenta las fallas
/// <b>seguidas</b> —<c>ChannelUnavailable</c> o <c>InProcess</c>— y al llegar a <c>Canales.UmbralDeFallas</c> (LND, defecto 5) abre el
/// circuito y levanta <c>Cartera.CanalExternoNoDisponible</c> con <c>DedupKey</c> <c>Cartera.CanalExternoNoDisponible:{clase}:{canal}</c>.
/// Abierto, las operaciones reciben <c>ChannelUnavailable</c> sin llamar al proveedor; cada cinco minutos la siguiente llamada sondea con
/// <c>ProbarAsync</c> y, si responde <c>Ok</c>, el circuito se cierra y la alerta se da por atendida. Cualquier respuesta que no sea falla
/// reinicia la cuenta. <b>Nunca lanza</b> hacia quien avisa: un error al leer el umbral o al levantar la alerta queda en el log. (nuevo)
/// </summary>
public sealed class CircuitoDeCanalExterno(
    EstadoDeLosCircuitosExternos estado,
    ICurrentTenantService tenant,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    IAlertas alertas,
    ILogger<CircuitoDeCanalExterno> logger) : ICircuitoDeCanalExterno
{
    /// <summary>El tipo de alerta que levanta (data-model §11).</summary>
    public const string TipoDeAlerta = "Cartera.CanalExternoNoDisponible";

    public static string DedupKey(ExternalChannelKind clase, string canal) => $"{TipoDeAlerta}:{clase}:{canal.Trim().ToUpperInvariant()}";

    public async Task<bool> PuedeLlamarAsync(ExternalChannelKind clase, string channelCode, Func<CancellationToken, Task<ExternalOutcome>> probar,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || CooperativaDelCanal.Resuelta(tenant) is not { } cooperativa) return true;
        var actual = estado.Leer(cooperativa, channelCode);
        if (actual.AbiertoDesde is null) return true;

        var ahora = reloj.UtcNow;
        if (actual.UltimoSondeo is { } ultimo && ahora - ultimo < EstadoDeLosCircuitosExternos.IntervaloDeSondeo) return false;

        estado.AnotarSondeo(cooperativa, channelCode, ahora);
        ExternalOutcome sondeo;
        try
        {
            sondeo = await probar(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "[Canales.Circuito.SondeoLanzo] {Canal}: el sondeo lanzó; el circuito sigue abierto.", channelCode);
            return false;
        }

        if (sondeo != ExternalOutcome.Ok)
        {
            logger.LogInformation("[Canales.Circuito.SigueAbierto] {Canal}: el sondeo respondió {Resultado}.", channelCode, sondeo);
            return false;
        }

        estado.Cerrar(cooperativa, channelCode);
        logger.LogInformation("[Canales.Circuito.Cerrado] {Canal}: el sondeo respondió; el circuito se cierra.", channelCode);
        try
        {
            await alertas.AtenderPorProcesoAsync(DedupKey(clase, channelCode), "El canal volvió a responder.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Canales.Circuito.AlertaNoAtendida] {Canal}: el circuito se cerró pero la alerta no se dio por atendida.", channelCode);
        }
        return true;
    }

    public async Task RegistrarAsync(ExternalChannelKind clase, string channelCode, ExternalOutcome resultado, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || CooperativaDelCanal.Resuelta(tenant) is not { } cooperativa) return;
        if (resultado is not (ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess))
        {
            if (!estado.Abierto(cooperativa, channelCode)) estado.Cerrar(cooperativa, channelCode);
            return;
        }

        var cuenta = estado.SumarFalla(cooperativa, channelCode);
        if (cuenta.AbiertoDesde is not null) return;

        int umbral;
        try
        {
            umbral = await UmbralAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Canales.Circuito.UmbralIlegible] {Canal}: no se pudo leer el umbral; se usa el defecto.", channelCode);
            umbral = UmbralPorDefecto();
        }
        if (cuenta.Fallas < umbral) return;

        estado.Abrir(cooperativa, channelCode, reloj.UtcNow);
        logger.LogWarning("[Canales.Circuito.Abierto] {Clase} {Canal}: {Fallas} fallas seguidas; el circuito se abre.", clase, channelCode, cuenta.Fallas);
        try
        {
            var canal = channelCode.Trim().ToUpperInvariant();
            var levantada = await alertas.LevantarAsync(new AlertaALevantar(
                TipoDeAlerta,
                $"El canal externo {canal} no está disponible",
                string.Create(CultureInfo.InvariantCulture,
                    $"El canal {canal} ({clase}) falló {cuenta.Fallas} veces seguidas. Las operaciones que lo usan reciben «no disponible» hasta que vuelva a responder; se prueba cada cinco minutos."),
                DedupKey: DedupKey(clase, canal)), ct);
            if (levantada.IsFailure)
                logger.LogWarning("[Canales.Circuito.AlertaNoLevantada] {Canal}: {Codigo}", canal, levantada.Error.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[Canales.Circuito.AlertaNoLevantada] {Canal}: el circuito quedó abierto pero la alerta no salió.", channelCode);
        }
    }

    public EstadoDelCircuito Estado(string channelCode) =>
        !string.IsNullOrWhiteSpace(channelCode) && CooperativaDelCanal.Resuelta(tenant) is { } cooperativa && estado.Abierto(cooperativa, channelCode)
            ? EstadoDelCircuito.Open
            : EstadoDelCircuito.Closed;

    private async Task<int> UmbralAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.CanalesUmbralDeFallas, reloj.HoyLocal, ct: ct);
        var umbral = leido.IsSuccess ? leido.Value.Como<int>() : 0;
        return umbral > 0 ? umbral : UmbralPorDefecto();
    }

    private static int UmbralPorDefecto() => int.Parse(ParametrosDeCartera.Definiciones
        .First(d => d.Clave == ParametrosDeCartera.CanalesUmbralDeFallas).DefectoSeguro, CultureInfo.InvariantCulture);
}
