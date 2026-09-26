using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T462 (feature 012, US7; FR-080; contracts/mensajes.md §11, §13; T10, T11): registrar el resultado de una unidad escribe estado,
/// <c>ProcessedAt</c>, referencia, tipo y número del comprobante en <b>toda</b> entrega de la unidad y una fila de intento por entrega;
/// <c>Retry</c> programa <c>min(15 s·2^(n−1), 15 min)</c> más dispersión; al tercer intento o a los 15 minutos levanta
/// <c>Integracion.MensajeSinEntregar</c>; <c>Rejected</c> levanta <c>Integracion.MensajeRechazado</c>; lo que otra réplica ya registró
/// no se duplica.
/// </summary>
public class RegisterDeliveryResultCommandHandlerTests
{
    private readonly BandejaDePrueba _b = new();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public RegisterDeliveryResultCommandHandlerTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 2, false)));
    }

    private RegisterDeliveryResultCommandHandler Handler() => new(
        _b.Db, _b.ActorActual, _alertas, Options.Create(new ReintentosDeIntegracion()), NullLogger<RegisterDeliveryResultCommandHandler>.Instance);

    private static RegisterDeliveryResultCommand Comando(IEnumerable<Guid> mensajes, ResultadoDeConsumo resultado, int intentosLeidos = 0) => new(
        IntegrationDestinations.Accounting, mensajes.ToList(), resultado, intentosLeidos,
        BandejaDePrueba.Ahora, BandejaDePrueba.Ahora.AddMilliseconds(250), "api-7f9c");

    [Fact]
    public async Task Processed_escribe_el_resultado_en_toda_la_unidad_y_un_intento_por_entrega()
    {
        var venta = Guid.NewGuid();
        var factura = _b.Mensaje(origen: venta, tipo: "VentaFacturada");
        var costo = _b.Mensaje(origen: venta, tipo: "CostoDeVentaReconocido");
        _b.Entrega(factura);
        _b.Entrega(costo);
        var comprobante = Guid.NewGuid();
        _b.ActorActual.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(BandejaDePrueba.Proceso("Mensaje:1"));

        var r = await Handler().Handle(Comando([factura.PublicId, costo.PublicId], new ResultadoDeConsumo.Processed(comprobante, "FV", "FV-1532")), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Registrado.Should().BeTrue();
        _b.Olvidar();
        var entregas = await _b.Db.IntegrationMessageDeliveries.ToListAsync();
        entregas.Should().HaveCount(2).And.OnlyContain(e =>
            e.Status == DeliveryStatus.Processed && e.ProcessedAt == BandejaDePrueba.Ahora.AddMilliseconds(250)
            && e.ResultReference == comprobante.ToString("D") && e.ResultVoucherTypeCode == "FV" && e.ResultVoucherNumber == "FV-1532"
            && e.Attempts == 1 && e.NextAttemptAt == null);

        var intentos = await _b.Db.IntegrationDeliveryAttempts.ToListAsync();
        intentos.Should().HaveCount(2).And.OnlyContain(a =>
            a.AttemptNumber == 1 && a.Outcome == DeliveryAttemptOutcome.Processed && a.DurationMs == 250
            && a.ActorKind == ActorKind.Process && a.ActorName == "Proceso de integración" && a.Instance == "api-7f9c");
        intentos.Select(a => a.MessageId).Should().BeEquivalentTo([factura.Id, costo.Id]);
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_recibo_sin_comprobante_guarda_su_referencia_y_ningun_tipo()
    {
        var m = _b.Mensaje(tipo: "SaldoInicialCargado", kind: IntegrationMessageKind.Informational);
        _b.Entrega(m, modo: DeliveryMode.Always);

        await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational)), default);

        var entrega = await _b.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.ResultReference.Should().Be(ReferenciasDeResultado.Informativo);
        entrega.ResultVoucherTypeCode.Should().BeNull();
    }

    [Fact]
    public async Task AlreadyProcessed_deja_la_misma_referencia_y_marca_el_intento()
    {
        var m = _b.Mensaje();
        _b.Entrega(m);
        var comprobante = Guid.NewGuid();

        await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.AlreadyProcessed(comprobante, "EI", "EI-7")), default);

        (await _b.Db.IntegrationMessageDeliveries.SingleAsync()).ResultReference.Should().Be(comprobante.ToString("D"));
        (await _b.Db.IntegrationDeliveryAttempts.SingleAsync()).Outcome.Should().Be(DeliveryAttemptOutcome.AlreadyProcessed);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(6, 480)]
    [InlineData(7, 900)]
    [InlineData(20, 900)]
    public void La_espera_es_min_de_15s_por_2_a_la_n_menos_1_y_15_minutos(int intento, int segundos)
    {
        var politica = new ReintentosDeIntegracion();

        politica.Espera(intento, 0).Should().Be(TimeSpan.FromSeconds(segundos));
        politica.Espera(intento, 0.999).Should().BeGreaterThan(TimeSpan.FromSeconds(segundos))
            .And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(segundos * (1 + ReintentosDeIntegracion.Dispersion)));
    }

    [Fact]
    public async Task Retry_deja_la_entrega_pendiente_con_la_espera_y_el_error()
    {
        var m = _b.Mensaje();
        _b.Entrega(m);

        var r = await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Retry("El original todavía no está contabilizado.", "Accounting.InventoryMessage.WaitingForOriginal")), default);

        var entrega = await _b.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.Status.Should().Be(DeliveryStatus.Pending);
        entrega.Attempts.Should().Be(1);
        entrega.LastErrorCode.Should().Be("Accounting.InventoryMessage.WaitingForOriginal");
        var fin = BandejaDePrueba.Ahora.AddMilliseconds(250);
        entrega.NextAttemptAt.Should().BeOnOrAfter(fin.AddSeconds(15)).And.BeOnOrBefore(fin.AddSeconds(16.5));
        r.Value.NextAttemptAt.Should().Be(entrega.NextAttemptAt);
        _levantadas.Should().BeEmpty("es el primer intento y no pasaron 15 minutos");
    }

    [Fact]
    public async Task Retry_en_un_lote_sigue_InBatch_con_el_mismo_lote()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);
        var m = _b.Mensaje();
        _b.Entrega(m, DeliveryStatus.InBatch, lote: lote);

        await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Retry("Base ocupada")), default);

        var entrega = await _b.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.Status.Should().Be(DeliveryStatus.InBatch);
        entrega.BatchId.Should().Be(lote.Id);
        (await _b.Db.IntegrationDeliveryAttempts.SingleAsync()).BatchId.Should().Be(lote.Id);
    }

    [Fact]
    public async Task Al_tercer_intento_levanta_MensajeSinEntregar()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, intentos: 2);

        var r = await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Retry("Tiempo agotado"), intentosLeidos: 2), default);

        r.Value.Attempt.Should().Be(3);
        r.Value.Alerts.Should().Equal(TiposDeAlerta.MensajeSinEntregar);
        _levantadas.Should().ContainSingle().Which.Should().Match<AlertaALevantar>(a =>
            a.TypeCode == TiposDeAlerta.MensajeSinEntregar && a.EntityPublicId == m.PublicId && a.RecipientPermissions == null);
    }

    [Fact]
    public async Task A_los_quince_minutos_del_primer_intento_levanta_MensajeSinEntregar_aunque_sea_el_segundo()
    {
        var m = _b.Mensaje();
        var entrega = _b.Entrega(m, intentos: 1);
        _b.Db.IntegrationDeliveryAttempts.Add(new Domain.Entities.Integration.Transactions.IntegrationDeliveryAttempt
        {
            DeliveryId = entrega.Id, MessageId = m.Id, AttemptNumber = 1, StartedAt = BandejaDePrueba.Ahora.AddMinutes(-16),
            FinishedAt = BandejaDePrueba.Ahora.AddMinutes(-16), Outcome = DeliveryAttemptOutcome.Retry, ActorName = "Proceso de integración", Instance = "api-1",
        });
        await _b.Db.SaveChangesAsync();

        var r = await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Retry("Tiempo agotado"), intentosLeidos: 1), default);

        r.Value.Alerts.Should().Equal(TiposDeAlerta.MensajeSinEntregar);
    }

    [Fact]
    public async Task Rejected_deja_el_codigo_el_motivo_y_los_datos_y_levanta_MensajeRechazado()
    {
        var m = _b.Mensaje();
        _b.Entrega(m);

        var r = await Handler().Handle(Comando([m.PublicId],
            new ResultadoDeConsumo.Rejected("Accounting.InventoryRule.Missing", "Falta la regla de inventario del grupo MERC.", "{\"errors\":[]}")), default);

        var entrega = await _b.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.Status.Should().Be(DeliveryStatus.Rejected);
        entrega.LastErrorCode.Should().Be("Accounting.InventoryRule.Missing");
        entrega.LastErrorDataJson.Should().Be("{\"errors\":[]}");
        entrega.NextAttemptAt.Should().BeNull();
        (await _b.Db.IntegrationDeliveryAttempts.SingleAsync()).Should().Match<Domain.Entities.Integration.Transactions.IntegrationDeliveryAttempt>(a =>
            a.Outcome == DeliveryAttemptOutcome.Rejected && a.ErrorCode == "Accounting.InventoryRule.Missing");
        r.Value.Alerts.Should().Equal(TiposDeAlerta.MensajeRechazado);
        _levantadas.Should().ContainSingle().Which.TypeCode.Should().Be(TiposDeAlerta.MensajeRechazado);
    }

    [Fact]
    public async Task Lo_que_otra_replica_ya_registro_no_se_duplica()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, intentos: 1);

        // El despachador leyó 0 intentos; otra réplica ya registró el primero (el choque de RowVersion relee y ve esto).
        var r = await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Processed(Guid.NewGuid(), "EI", "EI-1")), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Registrado.Should().BeFalse();
        (await _b.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0);
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync()).Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Una_entrega_ya_procesada_no_se_vuelve_a_registrar()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, DeliveryStatus.Processed);

        var r = await Handler().Handle(Comando([m.PublicId], new ResultadoDeConsumo.Rejected("X.Y", "otro")), default);

        r.Value.Registrado.Should().BeFalse();
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync()).Status.Should().Be(DeliveryStatus.Processed);
    }

    [Fact]
    public void Se_reintenta_entero_ante_un_choque_de_RowVersion()
    {
        typeof(IReintentableAnteConcurrencia).IsAssignableFrom(typeof(RegisterDeliveryResultCommand)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_mensaje_que_no_existe_responde_NotFound()
    {
        var r = await Handler().Handle(Comando([Guid.NewGuid()], new ResultadoDeConsumo.Retry("x")), default);

        r.Error.Code.Should().Be("Integration.Message.NotFound");
    }

    [Fact]
    public void El_validador_exige_la_forma()
    {
        var v = new RegisterDeliveryResultCommandValidator();

        v.Validate(Comando([Guid.NewGuid()], new ResultadoDeConsumo.Processed(Guid.NewGuid(), "EI", "1"))).IsValid.Should().BeTrue();
        v.Validate(Comando([], new ResultadoDeConsumo.Processed(Guid.NewGuid(), "EI", "1"))).IsValid.Should().BeFalse();
        v.Validate(Comando([Guid.NewGuid()], new ResultadoDeConsumo.Rejected("", "sin código"))).IsValid.Should().BeFalse();
        v.Validate(Comando([Guid.NewGuid()], new ResultadoDeConsumo.Retry("x")) with { FinishedAt = BandejaDePrueba.Ahora.AddSeconds(-1) })
            .IsValid.Should().BeFalse();
    }
}
