using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Common.ExternalChannels;

/// <summary>
/// La cooperativa resuelta para un canal externo (feature 013, T0079; §3.4): el <c>PublicId</c> de <see cref="ICurrentTenantService"/> en
/// una petición, o el de la entrada del directorio que fijó <c>IEjecutorEnCooperativa</c> en un trabajo de fondo. Nulo si no hay ninguna:
/// sin cooperativa no se lee una credencial ni se cuenta una falla. (nuevo)
/// </summary>
public static class CooperativaDelCanal
{
    public static Guid? Resuelta(ICurrentTenantService tenant)
    {
        if (Guid.TryParse(tenant.TenantId, out var publica) && publica != Guid.Empty) return publica;
        return ContextoAmbiental.Cooperativa?.PublicId is { } delTrabajo && delTrabajo != Guid.Empty ? delTrabajo : null;
    }
}

/// <summary>
/// Un adaptador registrado en el despliegue, con su puerto y su código (feature 013, T0081; §3.1 regla 6). <c>AddApplicationServices</c>
/// registra el del correo y <c>AddExternalChannels</c> los suyos; el segundo con el mismo puerto y código hace fallar la composición —el
/// arranque— antes de construir nada. (nuevo)
/// </summary>
/// <param name="Puerto">El nombre del puerto (<see cref="PuertosExternos"/>).</param>
/// <param name="ChannelCode">El código del adaptador.</param>
/// <param name="Implementacion">El tipo que lo implementa.</param>
public sealed record CanalExternoRegistrado(string Puerto, string ChannelCode, Type Implementacion);

/// <summary>Los nombres de los puertos externos, para <see cref="CanalExternoRegistrado"/>. (nuevo)</summary>
public static class PuertosExternos
{
    public const string Mensajeria = "Mensajeria";
    public const string ListasRestrictivas = "ListasRestrictivas";
    public const string DepositoDeValores = "DepositoDeValores";
    public const string CentralesDeRiesgo = "CentralesDeRiesgo";
}

/// <summary>
/// Lo que la configuración por cooperativa necesita de cada registro de canales (feature 013, T0080; §3.3): qué códigos hay instalados para
/// una clase y probar uno. Lo implementan <c>ICanalesDeMensajeria</c> (<c>Sms</c>, <c>WhatsApp</c>) y <c>IProveedoresDeListas</c>
/// (<c>RestrictiveLists</c>); el depósito de valores y las centrales lo suman en sus fases (US3, US2). Una clase sin registro no tiene
/// canales instalados. (nuevo)
/// </summary>
public interface IRegistroDeCanalesExternos
{
    /// <summary>Las clases que atiende este registro.</summary>
    IReadOnlyCollection<ExternalChannelKind> Clases { get; }

    /// <summary>Los códigos instalados para <paramref name="clase"/>.</summary>
    IReadOnlyCollection<string> CodigosPara(ExternalChannelKind clase);

    /// <summary><c>ProbarAsync</c> del adaptador de <paramref name="channelCode"/>; un código sin adaptador responde <c>ChannelUnavailable</c>.</summary>
    Task<ResultadoExterno<bool>> ProbarAsync(ExternalChannelKind clase, string channelCode, ContextoDeCanalExterno contexto, CancellationToken ct);
}

/// <summary>Estado del circuito de un canal en una cooperativa (api.md §21.2, <c>circuit: Closed | Open</c>). (nuevo)</summary>
public enum EstadoDelCircuito
{
    Closed = 1,
    Open = 2,
}

/// <summary>
/// El circuito por cooperativa y canal (feature 013, T0081; T72; contracts/adaptadores-y-archivos.md §3.5). Los registros lo consultan antes
/// de llamar a un adaptador y le avisan el resultado: con el circuito abierto la operación recibe <c>ChannelUnavailable</c> sin llamar al
/// proveedor, salvo el sondeo de prueba cada cinco minutos. <b>Nunca lanza</b> hacia quien avisa. El defecto de Application
/// (<see cref="CircuitoSiempreCerrado"/>) no cuenta nada; <c>AddExternalChannels</c> registra <c>CircuitoDeCanalExterno</c>. (nuevo)
/// </summary>
public interface ICircuitoDeCanalExterno
{
    /// <summary>¿Se puede llamar al canal ahora? Abierto y con el sondeo vencido, prueba el canal con <paramref name="probar"/>.</summary>
    Task<bool> PuedeLlamarAsync(ExternalChannelKind clase, string channelCode, Func<CancellationToken, Task<ExternalOutcome>> probar,
        CancellationToken ct);

    /// <summary>Le avisa el resultado de una llamada: una falla (<c>ChannelUnavailable</c> o <c>InProcess</c>) suma; cualquier otro la reinicia.</summary>
    Task RegistrarAsync(ExternalChannelKind clase, string channelCode, ExternalOutcome resultado, CancellationToken ct);

    /// <summary>El estado del circuito de <paramref name="channelCode"/> en la cooperativa resuelta.</summary>
    EstadoDelCircuito Estado(string channelCode);
}

/// <summary>El circuito que nunca se abre (el defecto de Application, sin <c>AddExternalChannels</c>). (nuevo)</summary>
public sealed class CircuitoSiempreCerrado : ICircuitoDeCanalExterno
{
    public Task<bool> PuedeLlamarAsync(ExternalChannelKind clase, string channelCode, Func<CancellationToken, Task<ExternalOutcome>> probar,
        CancellationToken ct) => Task.FromResult(true);

    public Task RegistrarAsync(ExternalChannelKind clase, string channelCode, ExternalOutcome resultado, CancellationToken ct) => Task.CompletedTask;

    public EstadoDelCircuito Estado(string channelCode) => EstadoDelCircuito.Closed;
}
