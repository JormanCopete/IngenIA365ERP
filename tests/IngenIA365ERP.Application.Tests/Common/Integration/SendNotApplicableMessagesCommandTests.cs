using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T465 (feature 012, US7; FR-078; api.md §25.2; T9): el envío posterior de lo que no pasó. Se niega si algún tipo del rango sigue en
/// <c>NoPasa</c> a hoy; toma la clausura de dependencias de otras fechas (anulación, nota y ajuste de costo fuera del rango) en orden de
/// emisión; nada posterior al corte entra aunque haya llegado después de la vista previa; el motivo y la persona quedan en el lote; y con el
/// lote en curso, un período cerrado rechaza el documento completo —su clausura en el lote— sin tocar a los demás.
/// </summary>
public class SendNotApplicableMessagesCommandTests
{
    private readonly BandejaDePrueba _b = new();

    internal static ILectorDeParametros ParametrosConModo(string modo)
    {
        var definicion = ParametrosDeInventario.Definiciones.Single(d => d.Clave == ParametrosDeInventario.ContabilidadModoDePaso);
        var lector = Substitute.For<ILectorDeParametros>();
        lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, Arg.Any<DateOnly>(),
                Arg.Any<ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, modo, modo, null)));
        return lector;
    }

    internal static InventoryDocumentType TipoDeDocumento(BandejaDePrueba b, string codigo)
    {
        var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = DocumentClass.TransferDispatch };
        b.Db.InventoryDocumentTypes.Add(tipo);
        b.Db.SaveChanges();
        return tipo;
    }

    private static IAlertas Alertas()
    {
        var alertas = Substitute.For<IAlertas>();
        alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        return alertas;
    }

    private SendNotApplicableMessagesCommandHandler Handler(ILectorDeParametros? parametros = null) =>
        new(_b.Db, _b.ActorActual, _b.Reloj, parametros ?? ParametrosConModo("EnLinea"), Substitute.For<ISenalDeMensajes>());

    private static SendNotApplicableMessagesCommand Enviar(Guid corte, IReadOnlyList<Guid>? tipos = null) =>
        new(BandejaDePrueba.Fecha.AddDays(-5), BandejaDePrueba.Fecha, tipos, corte, "Los traslados ya pasan a contabilidad") { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Si_el_tipo_sigue_en_NoPasa_a_hoy_se_niega_con_los_codigos()
    {
        TipoDeDocumento(_b, "TRASL");
        var m = _b.Mensaje(tipoDeDocumento: "TRASL");
        _b.Entrega(m, DeliveryStatus.NotApplicable);

        var r = await Handler(ParametrosConModo("NoPasa")).Handle(Enviar(m.PublicId), default);

        r.Error.Code.Should().Be("Integration.SendNotApplicable.ModeStillNotPosted");
        (await _b.Db.IntegrationBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Toma_la_clausura_de_otras_fechas_y_nada_posterior_al_corte()
    {
        TipoDeDocumento(_b, "TRASL");
        var anterior = _b.Mensaje(tipoDeDocumento: "TRASL", numero: "TR-0", fecha: BandejaDePrueba.Fecha.AddDays(-60));
        var eAnterior = _b.Entrega(anterior, DeliveryStatus.NotApplicable);
        var traslado = _b.Mensaje(tipoDeDocumento: "TRASL", numero: "TR-1");
        var eTraslado = _b.Entrega(traslado, DeliveryStatus.NotApplicable);
        _b.Depende(traslado, anterior);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", tipoDeDocumento: "ANUL", numero: "AN-1", relacionado: traslado.OriginPublicId,
            fecha: BandejaDePrueba.Fecha.AddDays(20));
        var eAnulacion = _b.Entrega(anulacion, DeliveryStatus.NotApplicable);
        _b.Depende(anulacion, traslado);
        var procesado = _b.Mensaje(tipoDeDocumento: "TRASL", numero: "TR-5");
        _b.Entrega(procesado, DeliveryStatus.Processed);
        var corte = anulacion;
        var tardio = _b.Mensaje(tipo: "AjusteDeCostoReconocido", tipoDeDocumento: "TRASL", numero: "TR-9", clave: $"Confirmation:{traslado.OriginPublicId:N}");
        var eTardio = _b.Entrega(tardio, DeliveryStatus.NotApplicable);
        _b.Depende(tardio, traslado);

        var r = await Handler().Handle(Enviar(corte.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Trigger.Should().Be(BatchTrigger.SendNotApplicable);
        r.Value.Messages.Should().Be(3);
        r.Value.Documents.Should().Be(3);
        var lote = await _b.Db.IntegrationBatches.SingleAsync();
        lote.CutoffMessageId.Should().Be(corte.Id);
        lote.RequestedByKind.Should().Be(ActorKind.Person);
        lote.RequestedByName.Should().Be("Laura Contadora");
        lote.Reason.Should().Be("Los traslados ya pasan a contabilidad");
        _b.Olvidar();
        var enLote = await _b.Db.IntegrationMessageDeliveries.Where(d => d.BatchId == lote.Id).OrderBy(d => d.MessageId).ToListAsync();
        enLote.Select(d => d.Id).Should().Equal(eAnterior.Id, eTraslado.Id, eAnulacion.Id);
        enLote.Should().OnlyContain(d => d.Status == DeliveryStatus.InBatch);
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == eTardio.Id)).Status.Should().Be(DeliveryStatus.NotApplicable,
            "llegó después del corte de la vista previa");
    }

    [Fact]
    public void Exige_motivo_y_clave()
    {
        new SendNotApplicableMessagesCommandValidator().Validate(Enviar(Guid.NewGuid()) with { Reason = "" }).IsValid.Should().BeFalse();
        typeof(IOperacionIdempotente).IsAssignableFrom(typeof(SendNotApplicableMessagesCommand)).Should().BeTrue();
        typeof(IConMotivo).IsAssignableFrom(typeof(SendNotApplicableMessagesCommand)).Should().BeTrue();
    }

    [Fact]
    public async Task Con_el_lote_en_curso_un_periodo_cerrado_rechaza_el_documento_con_su_clausura_y_los_demas_siguen()
    {
        var lote = _b.Lote(BatchTrigger.SendNotApplicable, BatchStatus.Running);
        var traslado = _b.Mensaje(tipoDeDocumento: "TRASL", numero: "TR-1");
        _b.Entrega(traslado, DeliveryStatus.InBatch, lote: lote);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: traslado.OriginPublicId);
        var eAnulacion = _b.Entrega(anulacion, DeliveryStatus.InBatch, lote: lote);
        _b.Depende(anulacion, traslado);
        var otro = _b.Mensaje(tipoDeDocumento: "TRASL", numero: "TR-2");
        var eOtro = _b.Entrega(otro, DeliveryStatus.InBatch, lote: lote);

        var handler = new RegisterDeliveryResultCommandHandler(_b.Db, _b.ActorActual, Alertas(),
            Options.Create(new ReintentosDeIntegracion()), NullLogger<RegisterDeliveryResultCommandHandler>.Instance);
        var r = await handler.Handle(new RegisterDeliveryResultCommand(IntegrationDestinations.Accounting, [traslado.PublicId],
            new ResultadoDeConsumo.Rejected("Accounting.Period.Closed", "El período contable 2026-10 está cerrado."), 0,
            BandejaDePrueba.Ahora, BandejaDePrueba.Ahora, "api-1"), default);

        r.Value.DraggedToRejected.Should().Be(1);
        _b.Olvidar();
        var arrastrada = await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == eAnulacion.Id);
        arrastrada.Status.Should().Be(DeliveryStatus.Rejected);
        arrastrada.LastErrorCode.Should().Be("Accounting.Period.Closed");
        arrastrada.LastErrorMessage.Should().Be("El período contable 2026-10 está cerrado.");
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == eOtro.Id)).Status.Should().Be(DeliveryStatus.InBatch);
    }

    [Fact]
    public async Task En_otro_lote_el_periodo_cerrado_no_arrastra()
    {
        var lote = _b.Lote(BatchTrigger.Manual, BatchStatus.Running);
        var traslado = _b.Mensaje(numero: "TR-1");
        _b.Entrega(traslado, DeliveryStatus.InBatch, lote: lote);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", relacionado: traslado.OriginPublicId);
        var eAnulacion = _b.Entrega(anulacion, DeliveryStatus.InBatch, lote: lote);
        _b.Depende(anulacion, traslado);

        var handler = new RegisterDeliveryResultCommandHandler(_b.Db, _b.ActorActual, Alertas(),
            Options.Create(new ReintentosDeIntegracion()), NullLogger<RegisterDeliveryResultCommandHandler>.Instance);
        await handler.Handle(new RegisterDeliveryResultCommand(IntegrationDestinations.Accounting, [traslado.PublicId],
            new ResultadoDeConsumo.Rejected("Accounting.Period.Closed", "cerrado"), 0, BandejaDePrueba.Ahora, BandejaDePrueba.Ahora, "api-1"), default);

        _b.Olvidar();
        (await _b.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Id == eAnulacion.Id)).Status.Should().Be(DeliveryStatus.InBatch,
            "sigue bloqueada por el rechazo, esperando el reproceso");
    }
}
