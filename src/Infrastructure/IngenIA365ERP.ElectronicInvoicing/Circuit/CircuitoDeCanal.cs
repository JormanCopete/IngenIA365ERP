using System.Collections.Concurrent;
using System.Globalization;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.ElectronicInvoicing.Circuit;

/// <summary>
/// La cuenta de fallas seguidas de cada circuito, por cooperativa y canal (feature 012, I4, T730; contracts/dian.md §7.2). Vive en memoria
/// del proceso (singleton): cada réplica cuenta lo suyo y un reinicio la pone en cero, que sólo retrasa la apertura; la contingencia que
/// ya se abrió está en la base y el sondeo la encuentra ahí. (nuevo)
/// </summary>
public sealed class EstadoDeLosCircuitos
{
    private readonly ConcurrentDictionary<(Guid Cooperativa, string Canal), int> _fallas = new();

    /// <summary>Las fallas seguidas de <paramref name="canal"/> en <paramref name="cooperativa"/>.</summary>
    public int Fallas(Guid cooperativa, string canal) => _fallas.TryGetValue((cooperativa, Normalizar(canal)), out var n) ? n : 0;

    internal int Sumar(Guid cooperativa, string canal) => _fallas.AddOrUpdate((cooperativa, Normalizar(canal)), 1, (_, n) => n + 1);

    internal void Reiniciar(Guid cooperativa, string canal) => _fallas.TryRemove((cooperativa, Normalizar(canal)), out _);

    internal IReadOnlyList<string> ConFallas(Guid cooperativa) =>
        _fallas.Where(p => p.Key.Cooperativa == cooperativa && p.Value > 0).Select(p => p.Key.Canal).ToList();

    private static string Normalizar(string canal) => canal.Trim().ToUpperInvariant();
}

/// <summary>Qué respondió el sondeo de un canal y qué hizo el circuito. (nuevo)</summary>
public sealed record SondeoDelCanal(string Canal, ChannelOutcome Resultado, bool AbrioContingencia, bool CerroContingencia);

/// <summary>
/// El circuito de cada canal (feature 012, I4, T730; contracts/dian.md §7.2), en memoria por cooperativa y canal. Implementa
/// <see cref="IRegistroDeFallasDelCanal"/>: le avisan la emisión (un <c>ChannelUnavailable</c> definitivo) y el cobro del POS (la espera de
/// <c>Dian.EsperaMaximaPosSegundos</c> vencida, T736); los sondeos <c>ProbarAsync</c> fallidos los cuenta él mismo en
/// <see cref="SondearAsync"/>. Al llegar a <c>Dian.UmbralFallasCircuito</c> fallas <b>seguidas</b> abre una contingencia <c>Issuer03</c> con
/// <c>DetectedBy = Process</c> (por <see cref="ContingenciaDeLaDian.AbrirDeFacturacionAsync"/>, la misma de la apertura manual) y levanta
/// <c>Dian.ContingenciaAbierta</c>. Una respuesta del canal reinicia la cuenta. Mientras haya fallas o una 03 abierta por el proceso, el
/// procesador sondea en cada pasada; un <c>ProbarAsync</c> exitoso reinicia la cuenta y <b>cierra</b> la 03 que abrió el proceso (la que declaró
/// una persona la cierra una persona). Nunca lanza hacia quien avisa: la venta no se detiene por el circuito. (nuevo)
/// </summary>
public sealed class CircuitoDeCanal(
    EstadoDeLosCircuitos estado,
    IApplicationDbContext db,
    ICurrentTenantService tenant,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    ContingenciaDeLaDian contingencias,
    ICanalesDeEmision canales,
    ICredencialesDeCanal credenciales,
    ILogger<CircuitoDeCanal> logger) : IRegistroDeFallasDelCanal
{
    private static readonly QuienDeclara Proceso = new(ActorKind.Process, null, Actor.NombreDelProceso);

    public async Task RegistrarFallaAsync(string channelCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || Cooperativa() is not { } cooperativa) return;
        var canal = channelCode.Trim().ToUpperInvariant();
        var fallas = estado.Sumar(cooperativa, canal);
        var umbral = await UmbralAsync(ct);
        if (fallas < umbral) return;

        try
        {
            await AbrirAsync(canal, fallas, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FE.Circuito.NoAbrio] {Canal}: {Fallas} fallas seguidas y no se pudo abrir la contingencia 03.", canal, fallas);
        }
    }

    public Task RegistrarRespuestaAsync(string channelCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(channelCode) && Cooperativa() is { } cooperativa) estado.Reiniciar(cooperativa, channelCode);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sondea con <c>ProbarAsync</c> cada canal con fallas contadas o con una 03 abierta por el proceso. Lo llama el procesador de fondo en
    /// cada pasada de la cooperativa. (nuevo)
    /// </summary>
    public async Task<IReadOnlyList<SondeoDelCanal>> SondearAsync(CancellationToken ct)
    {
        if (Cooperativa() is not { } cooperativa) return [];

        var abiertasPorElProceso = await db.DianContingencyEvents.AsNoTracking()
            .Where(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.DetectedByKind == ActorKind.Process)
            .Select(e => e.ChannelCode)
            .ToListAsync(ct);
        var porSondear = estado.ConFallas(cooperativa).Concat(abiertasPorElProceso)
            .Select(c => c.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var sondeos = new List<SondeoDelCanal>();
        foreach (var canal in porSondear)
        {
            ct.ThrowIfCancellationRequested();
            if (!canales.Codigos.Contains(canal, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("[FE.Circuito.CanalSinAdaptador] {Canal}: no hay adaptador registrado; no se sondea.", canal);
                continue;
            }

            var resultado = await ProbarAsync(canal, cooperativa, ct);
            if (resultado is ChannelOutcome.Validated or ChannelOutcome.ValidatedWithNotices)
            {
                estado.Reiniciar(cooperativa, canal);
                var cerro = await CerrarAsync(canal, ct);
                sondeos.Add(new SondeoDelCanal(canal, resultado, false, cerro));
            }
            else
            {
                var abiertasAntes = abiertasPorElProceso.Contains(canal, StringComparer.OrdinalIgnoreCase);
                await RegistrarFallaAsync(canal, ct);
                var abrio = !abiertasAntes && await db.DianContingencyEvents.AsNoTracking()
                    .AnyAsync(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal, ct);
                sondeos.Add(new SondeoDelCanal(canal, resultado, abrio, false));
            }
        }
        return sondeos;
    }

    private async Task AbrirAsync(string canal, int fallas, CancellationToken ct)
    {
        var yaAbierta = await db.DianContingencyEvents.AsNoTracking()
            .AnyAsync(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal, ct);
        if (yaAbierta) return;

        var motivo = string.Create(CultureInfo.InvariantCulture, $"El canal {canal} falló {fallas} veces seguidas") +
            " (sin conexión, sondeo fallido o espera del punto de venta vencida): las ventas nuevas se numeran en contingencia y se transmiten al restablecerse.";
        var abierta = await contingencias.AbrirDeFacturacionAsync(canal, reloj.UtcNow, Proceso, motivo, ct);
        if (abierta.IsFailure)
        {
            logger.LogInformation("[FE.Circuito.YaAbierta] {Canal}: {Codigo}", canal, abierta.Error.Code);
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogWarning("[FE.Circuito.Abierto] {Canal}: {Fallas} fallas seguidas; contingencia 03 {Evento} abierta por el proceso.",
            canal, fallas, abierta.Value.PublicId);
        try
        {
            await contingencias.AvisarAperturaAsync(abierta.Value, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FE.Circuito.AlertaNoLevantada] {Canal}: la contingencia quedó abierta pero la alerta no salió.", canal);
        }
    }

    private async Task<bool> CerrarAsync(string canal, CancellationToken ct)
    {
        var abiertas = await db.DianContingencyEvents
            .Where(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal
                        && e.DetectedByKind == ActorKind.Process)
            .ToListAsync(ct);
        if (abiertas.Count == 0) return false;

        var ahora = reloj.UtcNow;
        foreach (var evento in abiertas.Where(e => e.StartedAt <= ahora))
            await contingencias.CerrarAsync(evento, ahora, Proceso, "El sondeo del canal volvió a responder.", ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("[FE.Circuito.Cerrado] {Canal}: el sondeo respondió; {Eventos} contingencia(s) 03 cerrada(s).", canal, abiertas.Count);
        return true;
    }

    private async Task<ChannelOutcome> ProbarAsync(string canal, Guid cooperativa, CancellationToken ct)
    {
        try
        {
            var configuracion = await db.ElectronicEmissionSettings.AsNoTracking()
                .Where(s => s.ChannelCode == canal)
                .OrderByDescending(s => s.ValidFrom)
                .FirstOrDefaultAsync(ct);
            if (configuracion is null) return ChannelOutcome.ChannelUnavailable;

            var credencial = await credenciales.ResolverAsync(canal, ct);
            if (credencial.IsFailure && !string.Equals(canal, Application.ElectronicInvoicing.GuardiaDeEmisionFiscal.CanalSimulado, StringComparison.OrdinalIgnoreCase))
                return ChannelOutcome.ChannelUnavailable;

            var contexto = new ContextoDeCanal(cooperativa, configuracion.IssuerTaxId, configuracion.IssuerCheckDigit, configuracion.Mode,
                configuracion.Environment, configuracion.SoftwareId, configuracion.TestSetId, null, null,
                credencial.IsSuccess ? credencial.Value : null);
            return (await canales.Resolver(canal).ProbarAsync(contexto, ct)).Outcome;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "[FE.Circuito.SondeoLanzo] {Canal}: el sondeo lanzó; cuenta como falla.", canal);
            return ChannelOutcome.ChannelUnavailable;
        }
    }

    private async Task<int> UmbralAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.UmbralFallasCircuito,
            reloj.HoyLocal, ct: ct);
        var umbral = leido.IsSuccess ? leido.Value.Como<int>() : 0;
        if (umbral > 0) return umbral;
        return int.Parse(ParametrosDeFacturacionElectronica.Definiciones
            .First(d => d.Clave == ParametrosDeFacturacionElectronica.UmbralFallasCircuito).DefectoSeguro, CultureInfo.InvariantCulture);
    }

    private Guid? Cooperativa()
    {
        if (Guid.TryParse(tenant.TenantId, out var publica) && publica != Guid.Empty) return publica;
        return ContextoAmbiental.Cooperativa?.PublicId is { } delTrabajo && delTrabajo != Guid.Empty ? delTrabajo : null;
    }
}
