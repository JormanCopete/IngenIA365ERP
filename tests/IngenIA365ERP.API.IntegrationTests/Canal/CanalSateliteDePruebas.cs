using System.Collections.Concurrent;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.IntegrationTests.CanalDigital;

/// <summary>
/// Feature 021, fase 18: el satélite visto desde el ERP en las pruebas de integración. Está «configurado» (habilitar el canal y reversar lo exigen),
/// reserva el código <c>TST</c>, no conoce intentos —como un canal que no tiene el aviso— y responde a los reversos como el servicio Go: por la
/// pasarela, siempre fuera de corte; por Tesorería, marcado. Anota cada reverso para que la prueba lo vea.
/// </summary>
public sealed class CanalSateliteDePruebas : ICanalSatelite
{
    public ConcurrentQueue<(Guid Cooperativa, string Referencia, string Modo)> Reversos { get; } = new();

    public bool Configurado => true;

    public Task<Result<string>> ReservarCodigoAsync(Guid cooperativa, string sugerido, CancellationToken ct) => Task.FromResult(Result.Success("TST"));

    public Task<Result<VerificacionDeCredencialesDto>> GuardarCredencialesAsync(Guid cooperativa, Guid sitio, CredencialesDePasarela credenciales,
        CancellationToken ct) =>
        Task.FromResult(Result.Success(new VerificacionDeCredencialesDto(true, DateTime.UtcNow, credenciales.Login[^Math.Min(4, credenciales.Login.Length)..], null)));

    public Task<Result<IReadOnlyList<IntentoDelCanalDto>>> BuscarIntentosAsync(Guid cooperativa, string? tipoDocumento, string? documento, string? referencia,
        CancellationToken ct) =>
        Task.FromResult(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([]));

    public Task<Result<CuentaDelCanalDto?>> BuscarCuentaAsync(Guid cooperativa, string tipoDocumento, string documento, CancellationToken ct) =>
        Task.FromResult(Result.Success<CuentaDelCanalDto?>(null));

    public Task<Result> DesbloquearCuentaAsync(Guid cooperativa, Guid cuenta, CancellationToken ct) => Task.FromResult(Result.Success());

    public Task<Result> CerrarSesionesAsync(Guid cooperativa, Guid cuenta, CancellationToken ct) => Task.FromResult(Result.Success());

    public Task<Result<ReversoEnPasarelaDto>> ReversarAsync(Guid cooperativa, string referencia, PedidoDeReversoAlCanal pedido, CancellationToken ct)
    {
        Reversos.Enqueue((cooperativa, referencia, pedido.Mode));
        return Task.FromResult(pedido.Mode == PedidoDeReversoAlCanal.PorLaPasarela
            ? Result.Failure<ReversoEnPasarelaDto>(ErroresDelReversoDelCanal.FueraDeCorte, "Pasó la hora de corte de la pasarela.")
            : Result.Success(new ReversoEnPasarelaDto(true, null, pedido.Mode, DateTime.UtcNow)));
    }

    public Task<Result<MetricasDelCanalDto>> MetricasAsync(Guid cooperativa, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult(Result.Success(new MetricasDelCanalDto(0, 0, 0, 0, 0, 0m, new Dictionary<string, int>())));

    public Task<Result> SincronizarAsync(Guid cooperativa, CancellationToken ct) => Task.FromResult(Result.Success());
}
