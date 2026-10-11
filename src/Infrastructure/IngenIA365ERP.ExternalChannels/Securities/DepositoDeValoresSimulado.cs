using System.Collections.Concurrent;
using System.Diagnostics;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.ExternalChannels.Messaging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ExternalChannels.Securities;

/// <summary>
/// Lo que el depósito simulado ya creó, por cooperativa (feature 013, US3, T0437; §3.6: «recuerda por cooperativa lo que respondió, para que la
/// consulta siguiente sea coherente»). Singleton: el estado vive en memoria del proceso, como el de la central simulada. (nuevo)
/// </summary>
public sealed class MemoriaDeDepositoSimulado
{
    private readonly ConcurrentDictionary<(Guid Cooperativa, string IdExterno), PagareSimulado> _porId = new();
    private readonly ConcurrentDictionary<(Guid Cooperativa, Guid Pagare), string> _porClave = new();

    internal PagareSimulado? Buscar(Guid cooperativa, string idExterno) =>
        _porId.TryGetValue((cooperativa, idExterno.Trim()), out var p) ? p : null;

    internal PagareSimulado? BuscarPorClave(Guid cooperativa, Guid pagare) =>
        _porClave.TryGetValue((cooperativa, pagare), out var id) ? Buscar(cooperativa, id) : null;

    /// <summary>Crea el pagaré una sola vez por clave (el <c>PublicId</c> del pagaré de Cartera): «ya existe» devuelve el mismo (§3.1 regla 3).</summary>
    internal PagareSimulado Crear(Guid cooperativa, Guid pagare, Func<PagareSimulado> crear)
    {
        var id = _porClave.GetOrAdd((cooperativa, pagare), _ =>
        {
            var nuevo = crear();
            _porId[(cooperativa, nuevo.IdExterno)] = nuevo;
            return nuevo.IdExterno;
        });
        return _porId[(cooperativa, id)];
    }
}

/// <summary>Un pagaré del depósito simulado: su dígito, sus firmantes, las consultas recibidas y su estado. (nuevo)</summary>
internal sealed class PagareSimulado(string idExterno, char? digito, IReadOnlyList<FirmanteDePagare> firmantes)
{
    private readonly Lock _cerrojo = new();

    public string IdExterno { get; } = idExterno;

    public char? Digito { get; } = digito;

    public IReadOnlyList<FirmanteDePagare> Firmantes { get; } = firmantes;

    public int Consultas { get; private set; }

    public PromissoryNoteStatus Estado { get; private set; } = PromissoryNoteStatus.PendingSignature;

    public DateTimeOffset? CustodiaDesde { get; private set; }

    public DateTimeOffset? FirmadoEn { get; private set; }

    /// <summary>Una consulta más (§3.7): el 3 firma a la tercera; los demás que no son 1 firman en la siguiente a la creación.</summary>
    public void Consultar(DateTimeOffset ahora)
    {
        lock (_cerrojo)
        {
            Consultas++;
            if (Estado != PromissoryNoteStatus.PendingSignature || Digito == '1') return;
            var aLaQueFirma = Digito == '3' ? DepositoDeValoresSimulado.ConsultaQueFirmaConElTres : 1;
            if (Consultas < aLaQueFirma) return;
            // El depósito custodia desde la firma: el pagaré desmaterializado firmado queda en custodia (perfeccionado, data-model §2.9).
            Estado = PromissoryNoteStatus.InCustody;
            FirmadoEn = ahora;
            CustodiaDesde = ahora;
        }
    }

    public void Endosar()
    {
        lock (_cerrojo) Estado = PromissoryNoteStatus.Endorsed;
    }

    public void Anular()
    {
        lock (_cerrojo) Estado = PromissoryNoteStatus.Voided;
    }

    public EstadoDePagare Foto()
    {
        lock (_cerrojo)
        {
            // Con el 1 el depósito crea el pagaré y la firma se rechaza: lo cuenta desde la primera consulta.
            var firma = Digito == '1' && Consultas > 0
                ? EstadoDeFirmaDePagare.Rechazada
                : FirmadoEn is null ? EstadoDeFirmaDePagare.Pendiente : EstadoDeFirmaDePagare.Firmada;
            var firmas = Firmantes.Select((f, i) => new FirmaDePagare(f.PersonaPublicId, f.NumeroDeDocumento,
                // El rechazo es del firmante que decide (el primero, el deudor); los demás quedan pendientes.
                firma == EstadoDeFirmaDePagare.Rechazada && i > 0 ? EstadoDeFirmaDePagare.Pendiente : firma,
                firma == EstadoDeFirmaDePagare.Pendiente ? null : FirmadoEn)).ToList();
            return new EstadoDePagare(IdExterno, Estado, firmas, CustodiaDesde);
        }
    }
}

/// <summary>
/// El canal <c>SIMULADO</c> del depósito de valores (feature 013, US3, T0437; T71, T73; contracts/adaptadores-y-archivos.md §3.6, §3.7; DT §2.22).
/// Decide por el último dígito del documento del <b>deudor</b> (el primer firmante con rol <c>Debtor</c>):
/// <list type="bullet">
/// <item>1: crea el pagaré (<c>Ok</c>, firma pendiente) y toda consulta responde <c>Rejected</c> (firma rechazada): nunca se perfecciona;</item>
/// <item>3: <c>InProcess</c> (firma pendiente) al crear y en las dos consultas siguientes; firmado a la tercera;</item>
/// <item>5: <c>ChannelUnavailable</c> al crear (no crea nada);</item>
/// <item>cualquier otro: <c>Ok</c> al crear y firmado en la consulta siguiente.</item>
/// </list>
/// Firmado, el depósito lo custodia: el estado que informa es <c>InCustody</c>, con <c>CustodiaDesde</c>. Sus ids empiezan con <c>SIM-</c>, todo
/// resultado va marcado como simulado (en producción la guardia le quita el efecto de perfeccionar) y recuerda por cooperativa lo que creó. Crear
/// dos veces el mismo pagaré devuelve el que ya existe. Con un retardo mayor que el tiempo total responde <c>ChannelUnavailable</c>. Nunca lanza
/// ni reintenta. (nuevo)
/// </summary>
public sealed class DepositoDeValoresSimulado(IOptions<ExternalChannelsOptions> opciones, MemoriaDeDepositoSimulado memoria) : IDepositoDeValores
{
    public const string Codigo = CodigosDeCanalExterno.Simulado;

    /// <summary>Con el dígito 3, la consulta en que el depósito informa la firma (§3.7).</summary>
    public const int ConsultaQueFirmaConElTres = 3;

    public string ChannelCode => Codigo;

    public CapacidadesDeDeposito Capacidades { get; } = new(Endoso: true, Anulacion: true, CertificadoDeCustodia: false);

    public async Task<ResultadoExterno<EstadoDePagare>> CrearPagareAsync(SolicitudDePagare solicitud, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        if (await LentoAsync(ct) is { } lento) return lento;

        if (memoria.BuscarPorClave(contexto.TenantPublicId, solicitud.PromissoryNotePublicId) is { } existente)
            return Responder(existente, cronometro);

        var deudor = solicitud.Firmantes.FirstOrDefault(f => f.Rol == FirmanteDePagare.Deudor) ?? solicitud.Firmantes.FirstOrDefault();
        if (deudor is null || solicitud.Valor <= 0m || solicitud.Firmantes.Any(f => string.IsNullOrWhiteSpace(f.NumeroDeDocumento)))
            return ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.InvalidData, Codigo, true,
                "SIMULADO: el pagaré necesita valor y el documento de cada firmante.", codigoDelProveedor: "SIM-0", duracionMs: cronometro.ElapsedMilliseconds);

        var digito = MensajeriaSimulada.UltimoDigito(deudor.NumeroDeDocumento);
        if (digito == '5')
            return ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true, "SIMULADO: el depósito no está disponible.",
                codigoDelProveedor: "SIM-5", duracionMs: cronometro.ElapsedMilliseconds);

        var pagare = memoria.Crear(contexto.TenantPublicId, solicitud.PromissoryNotePublicId,
            () => new PagareSimulado($"SIM-{Guid.NewGuid():N}", digito, solicitud.Firmantes.ToList()));
        return Responder(pagare, cronometro);
    }

    public async Task<ResultadoExterno<EstadoDePagare>> ConsultarPagareAsync(string idExterno, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        if (await LentoAsync(ct) is { } lento) return lento;
        if (Buscar(idExterno, contexto) is not { } pagare) return NoExiste(idExterno, cronometro);
        pagare.Consultar(DateTimeOffset.UtcNow);
        return Responder(pagare, cronometro);
    }

    public async Task<ResultadoExterno<EstadoDePagare>> EndosarPagareAsync(string idExterno, string endosatario, ContextoDeCanalExterno contexto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        if (await LentoAsync(ct) is { } lento) return lento;
        if (string.IsNullOrWhiteSpace(endosatario))
            return ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.InvalidData, Codigo, true, "SIMULADO: el endoso necesita el endosatario.",
                codigoDelProveedor: "SIM-0", duracionMs: cronometro.ElapsedMilliseconds);
        if (Buscar(idExterno, contexto) is not { } pagare) return NoExiste(idExterno, cronometro);
        var antes = pagare.Foto();
        if (antes.Estado is not (PromissoryNoteStatus.InCustody or PromissoryNoteStatus.Signed or PromissoryNoteStatus.Endorsed))
            return new ResultadoExterno<EstadoDePagare>(ExternalOutcome.Rejected, antes, Codigo, true, pagare.IdExterno, "SIM-E",
                $"SIMULADO: el pagaré está en «{antes.Estado}» y sólo se endosa en custodia.", [], cronometro.ElapsedMilliseconds);
        pagare.Endosar();
        return Responder(pagare, cronometro);
    }

    public async Task<ResultadoExterno<EstadoDePagare>> AnularPagareAsync(string idExterno, string motivo, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        if (await LentoAsync(ct) is { } lento) return lento;
        if (Buscar(idExterno, contexto) is not { } pagare) return NoExiste(idExterno, cronometro);
        pagare.Anular();
        return Responder(pagare, cronometro);
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, Codigo, true));

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private PagareSimulado? Buscar(string? idExterno, ContextoDeCanalExterno contexto) =>
        string.IsNullOrWhiteSpace(idExterno) ? null : memoria.Buscar(contexto.TenantPublicId, idExterno);

    /// <summary>Lo que el pagaré responde hoy: <c>Rejected</c> si la firma se rechazó, <c>InProcess</c> si sigue pendiente con el 3, si no <c>Ok</c>.</summary>
    private static ResultadoExterno<EstadoDePagare> Responder(PagareSimulado pagare, Stopwatch cronometro)
    {
        var foto = pagare.Foto();
        var (outcome, detalle) = foto switch
        {
            { FirmaRechazada: true, Estado: PromissoryNoteStatus.PendingSignature } => (ExternalOutcome.Rejected, "SIMULADO: el firmante rechazó la firma."),
            { Estado: PromissoryNoteStatus.PendingSignature } when pagare.Digito == '3' => (ExternalOutcome.InProcess, "SIMULADO: la firma sigue pendiente."),
            _ => (ExternalOutcome.Ok, (string?)null),
        };
        return new ResultadoExterno<EstadoDePagare>(outcome, foto, Codigo, true, pagare.IdExterno, $"SIM-{pagare.Digito}", detalle, [],
            cronometro.ElapsedMilliseconds);
    }

    private static ResultadoExterno<EstadoDePagare> NoExiste(string? idExterno, Stopwatch cronometro) =>
        ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.NotFound, Codigo, true, $"SIMULADO: no hay un pagaré {idExterno}.",
            referenciaExterna: idExterno, codigoDelProveedor: "SIM-404", duracionMs: cronometro.ElapsedMilliseconds);

    /// <summary>El retardo configurado; mayor que el tiempo total, el transporte no sube: <c>ChannelUnavailable</c> (§3.1 regla 2).</summary>
    private async Task<ResultadoExterno<EstadoDePagare>?> LentoAsync(CancellationToken ct)
    {
        var canal = opciones.Value.Canal(Codigo);
        if (canal.DelayMilliseconds <= 0) return null;
        await Task.Delay(Math.Min(canal.DelayMilliseconds, canal.TotalTimeoutMilliseconds), ct);
        return canal.DelayMilliseconds > canal.TotalTimeoutMilliseconds
            ? ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true, "SIMULADO: el depósito no respondió a tiempo.")
            : null;
    }
}
