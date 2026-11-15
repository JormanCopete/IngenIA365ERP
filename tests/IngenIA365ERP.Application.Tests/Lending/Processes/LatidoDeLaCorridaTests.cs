using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Processes;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Processes;

/// <summary>
/// 2026-11-15: el latido con que los pasos largos de una corrida de Cartera renuevan <c>lending.process</c>. El 13-nov de 2026 el cierre del ciclo
/// de COOFLOPAL tardó 11 minutos sin renovar el arrendamiento de dos minutos: la otra réplica tomó la misma corrida y quedó <c>Failed</c>.
/// </summary>
public class LatidoDeLaCorridaTests
{
    private static readonly DateTime Inicio = new(2026, 11, 13, 5, 30, 0, DateTimeKind.Utc);

    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private DateTime _ahora = Inicio;

    public LatidoDeLaCorridaTests() => _reloj.UtcNow.Returns(_ => _ahora);

    [Fact]
    public async Task Apagado_no_hace_nada()
    {
        var latido = new LatidoDeLaCorrida(_reloj);

        latido.Encendido.Should().BeFalse();
        await latido.LatirAsync(default);
    }

    [Fact]
    public async Task Renueva_a_lo_sumo_una_vez_por_periodo()
    {
        var latido = new LatidoDeLaCorrida(_reloj);
        var renovaciones = 0;
        latido.Encender(_ => { renovaciones++; return Task.FromResult(true); }, TimeSpan.FromSeconds(30));

        await latido.LatirAsync(default);
        renovaciones.Should().Be(0, "recién encendido: el conductor acaba de renovar");

        _ahora = Inicio.AddSeconds(31);
        await latido.LatirAsync(default);
        await latido.LatirAsync(default);
        renovaciones.Should().Be(1);

        _ahora = Inicio.AddSeconds(62);
        await latido.LatirAsync(default);
        renovaciones.Should().Be(2);

        latido.Apagar();
        _ahora = Inicio.AddMinutes(10);
        await latido.LatirAsync(default);
        renovaciones.Should().Be(2, "apagado al soltar la corrida");
    }

    [Fact]
    public async Task Si_el_arrendamiento_ya_es_de_otra_replica_avisa_que_perdio_el_turno()
    {
        var latido = new LatidoDeLaCorrida(_reloj);
        latido.Encender(_ => Task.FromResult(false), TimeSpan.FromSeconds(30));
        _ahora = Inicio.AddMinutes(1);

        await FluentActions.Awaiting(() => latido.LatirAsync(default)).Should().ThrowAsync<TurnoDeCarteraPerdidoException>();
    }
}
