using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit;

/// <summary>
/// FR-083 y T5 (T045): un trabajo de fondo nunca audita en la base global. Sin petición, la
/// cooperativa sale del <see cref="ContextoAmbiental"/>; si hay contexto y aun así no se resolvió
/// la cooperativa, algo está mal cableado, y caer a <c>_Global</c> escondería el evento donde
/// nadie de esa cooperativa lo ve. Se niega en voz alta: <c>Critical</c> y excepción.
/// </summary>
public class AuditoriaEnSegundoPlanSinCooperativaTests
{
    private sealed class LoggerQueAnota<T> : ILogger<T>
    {
        public List<(LogLevel Nivel, string Mensaje)> Entradas { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, formatter(state, exception)));
    }

    private static readonly TenantDirectoryEntry Coop = new(5, Guid.NewGuid(), "coop_x", "Coop X", DatabaseName: "coop_x");

    private static (MongoAuditService Servicio, LoggerQueAnota<MongoAuditService> Log) Crear()
    {
        var cooperativa = Substitute.For<ICurrentTenantService>();
        cooperativa.TenantId.Returns((string?)null);
        var log = new LoggerQueAnota<MongoAuditService>();
        var servicio = new MongoAuditService(
            // No conecta hasta el primer uso; la guarda lanza antes de encolar nada.
            new MongoClient("mongodb://127.0.0.1:1"),
            Options.Create(new MongoDbSettings { FlushIntervalSeconds = 3600 }),
            Substitute.For<ICurrentUserService>(),
            cooperativa,
            log);
        return (servicio, log);
    }

    [Fact]
    public async Task Escribir_con_contexto_ambiental_sin_cooperativa_lanza_y_registra_Critical()
    {
        var (servicio, log) = Crear();
        using var _servicio = servicio;
        using var _ = ContextoAmbiental.Fijar(Coop, Actor.ProcesoDeIntegracion("Tarea:prueba"), "Tarea:prueba");

        await FluentActions.Invoking(() => servicio.LogAsync(new AuditLogCommand { Action = "Create", EntityType = "Prueba", EntityId = "1" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Global*");

        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical && e.Mensaje.Contains("Tarea:prueba"));
    }

    [Fact]
    public async Task Tampoco_el_registro_de_acceso_ni_las_lecturas_caen_a_la_global()
    {
        var (servicio, _) = Crear();
        using var _servicio = servicio;
        using var _ = ContextoAmbiental.Fijar(Coop, Actor.ProcesoDeIntegracion("Lote:1"), "Lote:1");

        await FluentActions.Invoking(() => servicio.LogAccessAsync(new AccessLogCommand { Action = "Login" }))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Invoking(() => servicio.GetByEntityAsync("Prueba", "1"))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
