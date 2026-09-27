using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T463 (feature 012, US7; FR-077 a FR-080; api.md §25.2, §26.4; T12): las tres órdenes de lote. El lote manual resuelve el corte de
/// la vista previa al <c>Id</c> interno, choca con uno en curso del mismo alcance, toma su número del contador, exige motivo y guarda a
/// la persona; el reproceso sólo toma rechazados, arrastra a sus dependientes y los cuenta; el envío posterior se niega si el tipo
/// sigue sin pasar y toma la clausura entera.
/// </summary>
public class LotesDeIntegracionTests
{
    private const string Horario = "COMPRA|HoraDiaria|23:00|PorDocumento";

    private readonly BandejaDePrueba _b = new();
    private readonly ISenalDeMensajes _senal = Substitute.For<ISenalDeMensajes>();

    // ------------------------------------------------------------------------------------------ lote manual --

    private OrderIntegrationBatchCommandHandler Orden() => new(_b.Db, _b.ActorActual, _b.Reloj, _senal);

    private static OrderIntegrationBatchCommand Ordenar(Guid corte, string motivo = "Cierre de la quincena") =>
        new(corte, BandejaDePrueba.Fecha.AddDays(-10), BandejaDePrueba.Fecha, null, null, null, motivo) { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task El_lote_manual_toma_lo_del_alcance_hasta_el_corte_con_su_numero_y_la_persona()
    {
        var primero = _b.Mensaje(numero: "CO-1");
        var e1 = _b.Entrega(primero, DeliveryStatus.InBatch, horario: Horario);
        var segundo = _b.Mensaje(numero: "CO-2");
        var e2 = _b.Entrega(segundo, DeliveryStatus.InBatch, horario: Horario);
        var despuesDelCorte = _b.Mensaje(numero: "CO-3");
        var e3 = _b.Entrega(despuesDelCorte, DeliveryStatus.InBatch, horario: Horario);
        var fueraDelRango = _b.Mensaje(numero: "CO-0", fecha: BandejaDePrueba.Fecha.AddDays(-40));
        _b.Entrega(fueraDelRango, DeliveryStatus.InBatch, horario: Horario);

        var r = await Orden().Handle(Ordenar(segundo.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Number.Should().Be(1);
        r.Value.Status.Should().Be(BatchStatus.Requested);
        r.Value.Trigger.Should().Be(BatchTrigger.Manual);
        r.Value.Messages.Should().Be(2);
        var lote = await _b.Db.IntegrationBatches.SingleAsync();
        lote.CutoffMessageId.Should().Be(segundo.Id, "el corte viaja como PublicId y se guarda como el Id interno");
        lote.RequestedByKind.Should().Be(ActorKind.Person);
        lote.RequestedByName.Should().Be("Laura Contadora");
        lote.RequestedByIp.Should().Be("10.0.0.9");
        lote.Reason.Should().Be("Cierre de la quincena");
        _b.Olvidar();
        (await _b.Db.IntegrationMessageDeliveries.Where(d => d.BatchId == lote.Id).Select(d => d.Id).ToListAsync())
            .Should().BeEquivalentTo([e1.Id, e2.Id]);
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == e3.Id)).BatchId.Should().BeNull("llegó después de la vista previa");
        _senal.Received(1).Avisar(Arg.Any<IReadOnlyCollection<Guid>>());
    }

    [Fact]
    public async Task El_numero_sale_del_contador_y_avanza()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, DeliveryStatus.InBatch, horario: Horario);
        _b.Db.IntegrationBatchCounters.Add(new IntegrationBatchCounter());
        await _b.Db.SaveChangesAsync();
        var contador = await _b.Db.IntegrationBatchCounters.SingleAsync();
        contador.TomarSiguiente();
        contador.TomarSiguiente();
        await _b.Db.SaveChangesAsync();

        var r = await Orden().Handle(Ordenar(m.PublicId), default);

        r.Value.Number.Should().Be(3);
        (await _b.Db.IntegrationBatchCounters.SingleAsync()).NextValue.Should().Be(4);
    }

    [Fact]
    public async Task Con_un_lote_manual_en_curso_del_mismo_alcance_responde_AlreadyRunning()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, DeliveryStatus.InBatch, horario: Horario);
        var enCurso = _b.Lote(BatchTrigger.Manual, BatchStatus.Running, numero: 41);
        enCurso.DateFrom = BandejaDePrueba.Fecha.AddDays(-3);
        enCurso.DateTo = BandejaDePrueba.Fecha;
        await _b.Db.SaveChangesAsync();

        var r = await Orden().Handle(Ordenar(m.PublicId), default);

        r.Error.Code.Should().Be("Accounting.InventoryBatch.AlreadyRunning");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { batchPublicId = enCurso.PublicId, number = 41L });
    }

    [Fact]
    public async Task Un_corte_que_no_existe_responde_NotFound()
    {
        var r = await Orden().Handle(Ordenar(Guid.NewGuid()), default);

        r.Error.Code.Should().Be("Integration.Message.NotFound");
    }

    [Fact]
    public void El_lote_manual_exige_motivo_y_clave()
    {
        new OrderIntegrationBatchCommandValidator().Validate(Ordenar(Guid.NewGuid(), motivo: " ")).IsValid.Should().BeFalse();
        typeof(IOperacionIdempotente).IsAssignableFrom(typeof(OrderIntegrationBatchCommand)).Should().BeTrue();
        typeof(IConMotivo).IsAssignableFrom(typeof(OrderIntegrationBatchCommand)).Should().BeTrue();
    }

    [Fact]
    public async Task La_vista_previa_delega_en_Contabilidad_con_las_entregas_del_alcance_en_orden()
    {
        var a = _b.Mensaje(numero: "CO-1");
        _b.Entrega(a, DeliveryStatus.InBatch, horario: Horario);
        var b = _b.Mensaje(numero: "CO-2", tipoDeDocumento: "OTRO");
        _b.Entrega(b, DeliveryStatus.InBatch, horario: "OTRO|HoraDiaria|23:00|PorDocumento");
        var c = _b.Mensaje(numero: "CO-3");
        _b.Entrega(c, DeliveryStatus.InBatch, horario: Horario);
        var contabilidad = Substitute.For<IContabilidadParaInventario>();
        var vista = new VistaPreviaDeLoteDto(c.PublicId, [], [], []);
        contabilidad.PrevisualizarLoteAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(Result.Success(vista));
        var servicios = new ServiceCollection().AddSingleton(contabilidad).BuildServiceProvider();

        var r = await new PreviewIntegrationBatchQueryHandler(_b.Db, servicios).Handle(
            new PreviewIntegrationBatchQuery(BandejaDePrueba.Fecha.AddDays(-1), BandejaDePrueba.Fecha, ["COMPRA"], null, null), default);

        r.Value.Should().Be(vista);
        await contabilidad.Received(1).PrevisualizarLoteAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { a.PublicId, c.PublicId })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_Contabilidad_registrada_la_vista_previa_dice_que_no_esta_disponible()
    {
        var r = await new PreviewIntegrationBatchQueryHandler(_b.Db, new ServiceCollection().BuildServiceProvider()).Handle(
            new PreviewIntegrationBatchQuery(BandejaDePrueba.Fecha, BandejaDePrueba.Fecha, null, null, null), default);

        r.Error.Code.Should().Be("Integration.Destination.Unavailable");
    }

    // ------------------------------------------------------------------------------------------- reproceso --

    private ReprocessMessagesCommandHandler Reproceso(params IDestinoDeMensajes[] destinos) =>
        new(_b.Db, _b.ActorActual, _b.Reloj, destinos.Length == 0 ? [new DestinoFalso(IntegrationDestinations.Accounting)] : destinos, _senal);

    private static ReprocessMessagesCommand Reprocesar(params Guid[] ids) => new(ids, "Se corrigió la regla del grupo MERC") { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task El_reproceso_solo_toma_rechazados()
    {
        var rechazado = _b.Mensaje();
        _b.Entrega(rechazado, DeliveryStatus.Rejected);
        var procesado = _b.Mensaje();
        _b.Entrega(procesado, DeliveryStatus.Processed);

        var r = await Reproceso().Handle(Reprocesar(rechazado.PublicId, procesado.PublicId), default);

        r.Error.Code.Should().Be("Integration.Message.NotRejected");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { messagePublicIds = new[] { procesado.PublicId } });
        (await _b.Db.IntegrationBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_reproceso_crea_un_lote_Reprocess_y_arrastra_a_los_dependientes_que_esperaban()
    {
        var original = _b.Mensaje(numero: "CO-1");
        var eOriginal = _b.Entrega(original, DeliveryStatus.Rejected);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: original.OriginPublicId);
        var eAnulacion = _b.Entrega(anulacion, DeliveryStatus.Pending);
        _b.Depende(anulacion, original);
        var ajuste = _b.Mensaje(tipo: "AjusteDeCostoReconocido", numero: "AN-1", clave: $"Confirmation:{original.OriginPublicId:N}");
        var eAjuste = _b.Entrega(ajuste, DeliveryStatus.Pending);
        _b.Depende(ajuste, anulacion);
        var ajena = _b.Mensaje(numero: "CO-9");
        var eAjena = _b.Entrega(ajena, DeliveryStatus.Pending);

        var r = await Reproceso().Handle(Reprocesar(original.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Trigger.Should().Be(BatchTrigger.Reprocess);
        r.Value.Messages.Should().Be(1);
        r.Value.Dragged.Should().Be(2);
        var lote = await _b.Db.IntegrationBatches.SingleAsync();
        lote.RequestedByKind.Should().Be(ActorKind.Person);
        lote.Reason.Should().Be("Se corrigió la regla del grupo MERC");
        _b.Olvidar();
        var entregas = await _b.Db.IntegrationMessageDeliveries.ToDictionaryAsync(d => d.Id);
        foreach (var id in new[] { eOriginal.Id, eAnulacion.Id, eAjuste.Id })
        {
            entregas[id].Status.Should().Be(DeliveryStatus.InBatch);
            entregas[id].BatchId.Should().Be(lote.Id);
        }

        entregas[eAjena.Id].BatchId.Should().BeNull();
        entregas[eAjena.Id].Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Un_mensaje_de_una_version_no_aceptada_sigue_rechazado()
    {
        var viejo = _b.Mensaje(tipo: "CompraRecibida");
        var eViejo = _b.Entrega(viejo, DeliveryStatus.Rejected);
        var destino = new DestinoFalso(IntegrationDestinations.Accounting, (tipo, version) => false);

        var r = await Reproceso(destino).Handle(Reprocesar(viejo.PublicId), default);

        r.Value.Messages.Should().Be(0);
        _b.Olvidar();
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == eViejo.Id)).Status.Should().Be(DeliveryStatus.Rejected);
    }

    // ----------------------------------------------------------------------------------- envío posterior --

    [Fact]
    public async Task El_envio_posterior_se_niega_si_el_tipo_sigue_sin_pasar()
    {
        var m = _b.Mensaje(tipoDeDocumento: "TRASL");
        _b.Entrega(m, DeliveryStatus.NotApplicable);
        var parametros = SendNotApplicableMessagesCommandTests.ParametrosConModo("NoPasa");
        SendNotApplicableMessagesCommandTests.TipoDeDocumento(_b, "TRASL");

        var r = await new SendNotApplicableMessagesCommandHandler(_b.Db, _b.ActorActual, _b.Reloj, parametros, _senal).Handle(
            new SendNotApplicableMessagesCommand(BandejaDePrueba.Fecha, BandejaDePrueba.Fecha, null, m.PublicId, "Ya pasan") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Integration.SendNotApplicable.ModeStillNotPosted");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { documentTypeCodes = new[] { "TRASL" } });
    }
}
