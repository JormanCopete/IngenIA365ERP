using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>
/// El registro de los canales de centrales de riesgo (feature 013, US2 bloque 6B; T0400; T71, T72; contracts/adaptadores-y-archivos.md §3.1): resuelve
/// por el código <b>sellado</b> en la operación, nunca por la configuración de hoy. Sin adaptadores registrados (sin <c>AddExternalChannels</c>) es el
/// defecto «sin canal»: todo código responde <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public interface ICentralesDeRiesgo : IRegistroDeCanalesExternos
{
    IReadOnlyCollection<string> Codigos { get; }

    ICentralDeRiesgo Resolver(string channelCode);
}

/// <summary>
/// El registro de centrales (§3.1): por código sin distinguir mayúsculas; dos adaptadores con el mismo código lanzan al construirse (regla 6); un
/// código sin adaptador responde <c>ChannelUnavailable</c> con «el canal {código} no está instalado» (regla 7); las consultas pasan por el circuito
/// de la cooperativa (§3.5). (nuevo)
/// </summary>
public sealed class CentralesDeRiesgo : ICentralesDeRiesgo
{
    private static readonly ExternalChannelKind[] ClasesAtendidas = [ExternalChannelKind.CreditBureau];

    private readonly Dictionary<string, ICentralDeRiesgo> _porCodigo;
    private readonly ICircuitoDeCanalExterno _circuito;

    public CentralesDeRiesgo(IEnumerable<ICentralDeRiesgo> canales, ICircuitoDeCanalExterno circuito)
    {
        ArgumentNullException.ThrowIfNull(canales);
        _circuito = circuito ?? throw new ArgumentNullException(nameof(circuito));
        _porCodigo = new Dictionary<string, ICentralDeRiesgo>(StringComparer.OrdinalIgnoreCase);
        foreach (var canal in canales)
        {
            if (string.IsNullOrWhiteSpace(canal.ChannelCode))
                throw new InvalidOperationException($"El adaptador {canal.GetType().Name} no declara su ChannelCode.");
            if (!_porCodigo.TryAdd(canal.ChannelCode.Trim(), canal))
                throw new InvalidOperationException($"Dos adaptadores de centrales de riesgo declaran el canal «{canal.ChannelCode}».");
        }
        Codigos = _porCodigo.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyCollection<string> Codigos { get; }

    public IReadOnlyCollection<ExternalChannelKind> Clases => ClasesAtendidas;

    public IReadOnlyCollection<string> CodigosPara(ExternalChannelKind clase) => clase == ExternalChannelKind.CreditBureau ? Codigos : [];

    public ICentralDeRiesgo Resolver(string channelCode) =>
        !string.IsNullOrWhiteSpace(channelCode) && _porCodigo.TryGetValue(channelCode.Trim(), out var canal)
            ? new CentralVigilada(canal, _circuito)
            : new CentralNoInstalada(channelCode?.Trim() ?? string.Empty);

    public Task<ResultadoExterno<bool>> ProbarAsync(ExternalChannelKind clase, string channelCode, ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Resolver(channelCode).ProbarAsync(contexto, ct);

    private sealed class CentralNoInstalada(string channelCode) : ICentralDeRiesgo
    {
        public string ChannelCode { get; } = channelCode;

        public CapacidadesDeCentral Capacidades { get; } = new(false, false, false);

        public Task<ResultadoExterno<InformeDeCentral>> ConsultarAsync(ConsultaACentral consulta, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<InformeDeCentral>.NoInstalado(ChannelCode));

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<bool>.NoInstalado(ChannelCode));
    }

    private sealed class CentralVigilada(ICentralDeRiesgo canal, ICircuitoDeCanalExterno circuito) : ICentralDeRiesgo
    {
        public string ChannelCode => canal.ChannelCode;

        public CapacidadesDeCentral Capacidades => canal.Capacidades;

        public async Task<ResultadoExterno<InformeDeCentral>> ConsultarAsync(ConsultaACentral consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
        {
            if (!await circuito.PuedeLlamarAsync(ExternalChannelKind.CreditBureau, ChannelCode, async c => (await canal.ProbarAsync(contexto, c)).Outcome, ct))
                return ResultadoExterno<InformeDeCentral>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, CodigosDeCanalExterno.EsSimulado(ChannelCode),
                    $"El circuito del canal {ChannelCode} está abierto: no se llamó a la central.");
            var r = await canal.ConsultarAsync(consulta, contexto, ct);
            await circuito.RegistrarAsync(ExternalChannelKind.CreditBureau, ChannelCode, r.Outcome, ct);
            return r;
        }

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) => canal.ProbarAsync(contexto, ct);
    }
}
