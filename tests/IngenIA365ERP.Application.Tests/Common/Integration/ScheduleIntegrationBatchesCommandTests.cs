using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T466 (feature 012, US7; contracts/contabilidad.md §5.4; T12; Principios III y X): los lotes programados. Pasada la franja de
/// <c>Contabilidad.HoraDeLote</c> de hoy (hora de Colombia) crea un lote <c>Scheduled</c> por <c>ScheduleKey</c> con <c>HoraDiaria</c>,
/// con su corte y sus entregas; dos envíos seguidos no lo duplican (y la colisión del índice se traduce a «ya existe»); devuelve los
/// programados vencidos más la tolerancia que siguen sin correr; el validador rechaza una tolerancia negativa; y es un comando de proceso
/// que pasa por el pipeline (validación y auditoría), no una escritura del despachador.
/// </summary>
public class ScheduleIntegrationBatchesCommandTests
{
    private const string Diario = "COMPRA|HoraDiaria|23:00|Resumido";
    private const string OtroDiario = "AJUS|HoraDiaria|06:30|PorDocumento";
    private const string DeTurno = "DEPOS|CierreDeTurno||PorDocumento";

    private readonly BandejaDePrueba _b = new();
    private readonly ILectorDeParametros _parametros = Substitute.For<ILectorDeParametros>();

    private ScheduleIntegrationBatchesCommandHandler Handler() => new(_b.Db, _b.ActorActual, _b.Reloj, _parametros);

    /// <summary>La hora local de Colombia del 14 de noviembre.</summary>
    private void Son(int hora, int minuto) => _b.FijarReloj(new DateTime(2026, 11, 14, hora, minuto, 0, DateTimeKind.Utc).AddHours(5));

    [Fact]
    public async Task Pasada_la_hora_crea_un_lote_Scheduled_por_clave_con_su_corte_y_sus_entregas()
    {
        _b.ActorActual.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(BandejaDePrueba.Proceso("Tarea:integration.dispatch"));
        var primero = _b.Mensaje(numero: "CO-1");
        var e1 = _b.Entrega(primero, DeliveryStatus.InBatch, horario: Diario);
        var segundo = _b.Mensaje(numero: "CO-2");
        var e2 = _b.Entrega(segundo, DeliveryStatus.InBatch, horario: Diario);
        var temprano = _b.Mensaje(numero: "AJ-1", tipoDeDocumento: "AJUS");
        _b.Entrega(temprano, DeliveryStatus.InBatch, horario: OtroDiario);
        _b.Entrega(_b.Mensaje(numero: "PV-1", tipoDeDocumento: "DEPOS"), DeliveryStatus.InBatch, horario: DeTurno);
        Son(23, 1);

        var r = await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Created.Should().HaveCount(2, "una por clave diaria vencida; la de cierre de turno la crea el cierre de caja");
        var lote = await _b.Db.IntegrationBatches.SingleAsync(l => l.ScheduleKey == Diario);
        lote.Trigger.Should().Be(BatchTrigger.Scheduled);
        lote.Status.Should().Be(BatchStatus.Requested);
        lote.ScheduledFor.Should().Be(new DateTime(2026, 11, 14, 23, 0, 0), "hora local de Colombia");
        lote.Granularity.Should().Be(PostingGranularity.Summarized);
        lote.CutoffMessageId.Should().Be(segundo.Id);
        lote.MessageCount.Should().Be(2);
        lote.RequestedByKind.Should().Be(ActorKind.Process);
        _b.Olvidar();
        (await _b.Db.IntegrationMessageDeliveries.Where(d => d.BatchId == lote.Id).Select(d => d.Id).ToListAsync())
            .Should().BeEquivalentTo([e1.Id, e2.Id]);
        (await _b.Db.IntegrationBatches.Select(b => b.Number).ToListAsync()).Should().BeEquivalentTo([1L, 2L]);
    }

    [Fact]
    public async Task Antes_de_la_hora_no_crea_nada()
    {
        _b.Entrega(_b.Mensaje(), DeliveryStatus.InBatch, horario: Diario);
        Son(22, 59);

        var r = await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        r.Value.Created.Should().BeEmpty();
        (await _b.Db.IntegrationBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dos_envios_seguidos_no_duplican_la_franja()
    {
        _b.Entrega(_b.Mensaje(), DeliveryStatus.InBatch, horario: Diario);
        Son(23, 5);
        await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        // Llega otra entrega de la misma clave después de creado el lote de hoy: espera a la franja de mañana.
        _b.Entrega(_b.Mensaje(numero: "CO-2"), DeliveryStatus.InBatch, horario: Diario);
        var r = await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        r.Value.Created.Should().BeEmpty();
        (await _b.Db.IntegrationBatches.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void La_colision_del_indice_de_la_franja_se_reconoce()
    {
        var choque = new DbUpdateException("error", new Exception(
            "23505: duplicate key value violates unique constraint \"UK_COR_IntegrationBatches_Schedule\""));

        ScheduleIntegrationBatchesCommandHandler.EsColisionDeLaFranja(choque).Should().BeTrue();
        ScheduleIntegrationBatchesCommandHandler.EsColisionDeLaFranja(new DbUpdateException("otro", new Exception("UK_COR_Otra"))).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_hora_en_la_clave_lee_Contabilidad_HoraDeLote_vigente()
    {
        const string SinHora = "COMPRA|HoraDiaria||PorDocumento";
        _b.Entrega(_b.Mensaje(), DeliveryStatus.InBatch, horario: SinHora);
        var definicion = ParametrosDeInventario.Definiciones.Single(d => d.Clave == ParametrosDeInventario.ContabilidadHoraDeLote);
        _parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadHoraDeLote, Arg.Any<DateOnly>(),
                Arg.Any<ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, "21:00", new TimeOnly(21, 0), null)));
        Son(21, 10);

        var r = await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        r.Value.Created.Should().ContainSingle().Which.ScheduledFor.Should().Be(new DateTime(2026, 11, 14, 21, 0, 0));
    }

    [Fact]
    public async Task Devuelve_los_programados_vencidos_mas_la_tolerancia_que_siguen_sin_correr()
    {
        var atrasado = _b.Lote(BatchTrigger.Scheduled, BatchStatus.Requested, numero: 7, horario: OtroDiario, franja: new DateTime(2026, 11, 14, 6, 30, 0));
        _b.Lote(BatchTrigger.Scheduled, BatchStatus.Requested, numero: 8, horario: Diario, franja: new DateTime(2026, 11, 14, 22, 50, 0));
        _b.Lote(BatchTrigger.Scheduled, BatchStatus.Running, numero: 9, horario: Diario, franja: new DateTime(2026, 11, 13, 23, 0, 0));
        Son(23, 0);

        var r = await Handler().Handle(new ScheduleIntegrationBatchesCommand(30), default);

        r.Value.Late.Should().ContainSingle().Which.BatchPublicId.Should().Be(atrasado.PublicId);
    }

    [Fact]
    public void El_validador_rechaza_una_tolerancia_negativa()
    {
        var v = new ScheduleIntegrationBatchesCommandValidator();

        v.Validate(new ScheduleIntegrationBatchesCommand(-1)).IsValid.Should().BeFalse();
        v.Validate(new ScheduleIntegrationBatchesCommand(0)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Es_un_comando_de_MediatR_para_que_pase_por_validacion_y_auditoria()
    {
        typeof(IRequest<Result<LotesProgramadosDto>>).IsAssignableFrom(typeof(ScheduleIntegrationBatchesCommand)).Should().BeTrue();
        typeof(IRequestHandler<ScheduleIntegrationBatchesCommand, Result<LotesProgramadosDto>>)
            .IsAssignableFrom(typeof(ScheduleIntegrationBatchesCommandHandler)).Should().BeTrue();
    }
}
