using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>
/// El registro de los canales de depósito de valores (feature 013, US3, T0436; T71, T72; contracts/adaptadores-y-archivos.md §3.1): resuelve por
/// el código <b>sellado</b> en el pagaré (<c>CustodianChannelCode</c>), nunca por la configuración de hoy. Sin adaptadores registrados (sin
/// <c>AddExternalChannels</c>) es el defecto «sin canal»: todo código responde <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public interface IDepositosDeValores : IRegistroDeCanalesExternos
{
    IReadOnlyCollection<string> Codigos { get; }

    IDepositoDeValores Resolver(string channelCode);
}

/// <summary>
/// El registro de depósitos (§3.1): por código sin distinguir mayúsculas; dos adaptadores con el mismo código lanzan al construirse (regla 6, el
/// arranque falla); un código sin adaptador responde <c>ChannelUnavailable</c> con «el canal {código} no está instalado» (regla 7); cada llamada
/// pasa por el circuito de la cooperativa (§3.5) y le avisa el resultado. (nuevo)
/// </summary>
public sealed class DepositosDeValores : IDepositosDeValores
{
    private static readonly ExternalChannelKind[] ClasesAtendidas = [ExternalChannelKind.SecuritiesDepository];

    private readonly Dictionary<string, IDepositoDeValores> _porCodigo;
    private readonly ICircuitoDeCanalExterno _circuito;

    public DepositosDeValores(IEnumerable<IDepositoDeValores> canales, ICircuitoDeCanalExterno circuito)
    {
        ArgumentNullException.ThrowIfNull(canales);
        _circuito = circuito ?? throw new ArgumentNullException(nameof(circuito));
        _porCodigo = new Dictionary<string, IDepositoDeValores>(StringComparer.OrdinalIgnoreCase);
        foreach (var canal in canales)
        {
            if (string.IsNullOrWhiteSpace(canal.ChannelCode))
                throw new InvalidOperationException($"El adaptador {canal.GetType().Name} no declara su ChannelCode.");
            if (!_porCodigo.TryAdd(canal.ChannelCode.Trim(), canal))
                throw new InvalidOperationException($"Dos adaptadores de depósito de valores declaran el canal «{canal.ChannelCode}».");
        }
        Codigos = _porCodigo.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyCollection<string> Codigos { get; }

    public IReadOnlyCollection<ExternalChannelKind> Clases => ClasesAtendidas;

    public IReadOnlyCollection<string> CodigosPara(ExternalChannelKind clase) => clase == ExternalChannelKind.SecuritiesDepository ? Codigos : [];

    public IDepositoDeValores Resolver(string channelCode) =>
        !string.IsNullOrWhiteSpace(channelCode) && _porCodigo.TryGetValue(channelCode.Trim(), out var canal)
            ? new DepositoVigilado(canal, _circuito)
            : new DepositoNoInstalado(channelCode?.Trim() ?? string.Empty);

    public Task<ResultadoExterno<bool>> ProbarAsync(ExternalChannelKind clase, string channelCode, ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Resolver(channelCode).ProbarAsync(contexto, ct);

    private sealed class DepositoNoInstalado(string channelCode) : IDepositoDeValores
    {
        public string ChannelCode { get; } = channelCode;

        public CapacidadesDeDeposito Capacidades { get; } = new(false, false, false);

        public Task<ResultadoExterno<EstadoDePagare>> CrearPagareAsync(SolicitudDePagare solicitud, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            NoInstalado();

        public Task<ResultadoExterno<EstadoDePagare>> ConsultarPagareAsync(string idExterno, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            NoInstalado();

        public Task<ResultadoExterno<EstadoDePagare>> EndosarPagareAsync(string idExterno, string endosatario, ContextoDeCanalExterno contexto,
            CancellationToken ct) => NoInstalado();

        public Task<ResultadoExterno<EstadoDePagare>> AnularPagareAsync(string idExterno, string motivo, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            NoInstalado();

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<bool>.NoInstalado(ChannelCode));

        private Task<ResultadoExterno<EstadoDePagare>> NoInstalado() => Task.FromResult(ResultadoExterno<EstadoDePagare>.NoInstalado(ChannelCode));
    }

    private sealed class DepositoVigilado(IDepositoDeValores canal, ICircuitoDeCanalExterno circuito) : IDepositoDeValores
    {
        private const ExternalChannelKind Clase = ExternalChannelKind.SecuritiesDepository;

        public string ChannelCode => canal.ChannelCode;

        public CapacidadesDeDeposito Capacidades => canal.Capacidades;

        public Task<ResultadoExterno<EstadoDePagare>> CrearPagareAsync(SolicitudDePagare solicitud, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            VigilarAsync(contexto, c => canal.CrearPagareAsync(solicitud, contexto, c), ct);

        public Task<ResultadoExterno<EstadoDePagare>> ConsultarPagareAsync(string idExterno, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            VigilarAsync(contexto, c => canal.ConsultarPagareAsync(idExterno, contexto, c), ct);

        public Task<ResultadoExterno<EstadoDePagare>> EndosarPagareAsync(string idExterno, string endosatario, ContextoDeCanalExterno contexto,
            CancellationToken ct) => VigilarAsync(contexto, c => canal.EndosarPagareAsync(idExterno, endosatario, contexto, c), ct);

        public Task<ResultadoExterno<EstadoDePagare>> AnularPagareAsync(string idExterno, string motivo, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            VigilarAsync(contexto, c => canal.AnularPagareAsync(idExterno, motivo, contexto, c), ct);

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) => canal.ProbarAsync(contexto, ct);

        private async Task<ResultadoExterno<EstadoDePagare>> VigilarAsync(ContextoDeCanalExterno contexto,
            Func<CancellationToken, Task<ResultadoExterno<EstadoDePagare>>> llamada, CancellationToken ct)
        {
            if (!await circuito.PuedeLlamarAsync(Clase, ChannelCode, async c => (await canal.ProbarAsync(contexto, c)).Outcome, ct))
                return ResultadoExterno<EstadoDePagare>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, CodigosDeCanalExterno.EsSimulado(ChannelCode),
                    $"El circuito del canal {ChannelCode} está abierto: no se llamó al depósito.");
            var r = await llamada(ct);
            await circuito.RegistrarAsync(Clase, ChannelCode, r.Outcome, ct);
            return r;
        }
    }
}
