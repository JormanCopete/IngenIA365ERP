using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Behaviors;

/// <summary>
/// 2026-11-12: la búsqueda de productos que el cliente abandona (el buscador cancela su consulta en cada tecla) salía en el registro
/// como <c>ERR Unhandled exception in SearchProductsQuery</c>, 46 veces al día. Una cancelación del que pidió es Information; una
/// cancelación propia del servidor, con el token del pedido vivo, sigue siendo un error.
/// </summary>
public class LoggingBehaviorTests
{
    public sealed record ConsultaDePrueba : IRequest<int>;

    private readonly LogEnLista _registro = new();

    private LoggingBehavior<ConsultaDePrueba, int> Comportamiento() =>
        new(_registro, Substitute.For<ICurrentUserService>(), Substitute.For<ICurrentTenantService>());

    [Fact]
    public async Task La_cancelacion_del_que_pidio_se_registra_como_informacion_y_sigue_su_camino()
    {
        using var cliente = new CancellationTokenSource();
        await cliente.CancelAsync();

        var correr = () => Comportamiento().Handle(new ConsultaDePrueba(), t => throw new OperationCanceledException(t), cliente.Token);

        await correr.Should().ThrowAsync<OperationCanceledException>();
        _registro.Lineas.Should().NotContain(l => l.Nivel >= LogLevel.Error);
        _registro.Lineas.Should().Contain(l => l.Nivel == LogLevel.Information && l.Texto.Contains("cancelada por el cliente"));
    }

    [Fact]
    public async Task Una_cancelacion_con_el_pedido_vivo_sigue_siendo_un_error()
    {
        var correr = () => Comportamiento().Handle(new ConsultaDePrueba(), _ => throw new OperationCanceledException("tiempo interno"), CancellationToken.None);

        await correr.Should().ThrowAsync<OperationCanceledException>();
        _registro.Lineas.Should().Contain(l => l.Nivel == LogLevel.Error && l.Texto.Contains("Unhandled exception in ConsultaDePrueba"));
    }

    private sealed class LogEnLista : ILogger<LoggingBehavior<ConsultaDePrueba, int>>
    {
        public List<(LogLevel Nivel, string Texto)> Lineas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lineas.Add((logLevel, formatter(state, exception)));
    }
}
