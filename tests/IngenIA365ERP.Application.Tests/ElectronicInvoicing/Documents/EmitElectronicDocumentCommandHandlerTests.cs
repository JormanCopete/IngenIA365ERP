using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 012, I4, T679 (contracts/dian.md §5, §6, §7.1, §7.2; api.md §24.4): la emisión con un canal falso. <c>Validated</c> sin código
/// único o sin <c>ApplicationResponse</c> deja <c>Sent</c>; <c>InProcess</c> → <c>Sent</c> y el siguiente intento <b>consulta</b> antes de
/// reenviar; <c>NotFound</c> → <c>Pending</c> y reenvía la <b>misma</b> versión y número; <c>DianUnavailable</c> → <c>DianContingency</c>
/// unido al evento 04 abierto o abriendo uno; <c>ChannelUnavailable</c> → <c>Pending</c> con la siguiente espera y una falla al circuito;
/// <c>Rejected</c> levanta <c>Dian.DocumentoRechazado</c>; fila arrendada por otro → <c>ElectronicInvoicing.Document.InProgress</c>
/// (<c>data.leaseUntil</c>); cada llamada agrega exactamente una transmisión con la clave <c>{tenant:N}:{Environment}:{Number}:v{n}</c>.
/// </summary>
public class EmitElectronicDocumentCommandHandlerTests
{
    private readonly EscenarioDeEmision _s = new();

    private Task<Result<ElectronicTransmissionResultDto>> EmitirAsync(ElectronicDocument d, bool ahora = false) =>
        _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId, ahora), default);

    private async Task<ElectronicDocument> RecargarAsync(ElectronicDocument d)
    {
        _s.E.Db.DescartarCambios();
        return await _s.E.Db.ElectronicDocuments.Include(x => x.Versions).Include(x => x.Transmissions).Include(x => x.ContingencyEvent)
            .SingleAsync(x => x.Id == d.Id);
    }

    private string Clave(int version = 1) => $"{_s.E.Cooperativa:N}:Testing:SETP990000123:v{version}";

    [Fact]
    public async Task Validated_con_codigo_y_respuesta_valida_guarda_codigo_QR_artefactos_y_una_transmision()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));

        var r = await EmitirAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Validated);
        x.UniqueCode.Should().HaveLength(96);
        x.UniqueCodeKind.Should().Be(UniqueCodeKind.Cufe);
        x.QrContent.Should().Be("https://qr/simulado");
        x.NextAttemptAt.Should().BeNull();
        x.LeaseOwner.Should().BeNull("el arrendamiento se suelta al terminar");
        x.AttemptCount.Should().Be(1);

        var t = x.Transmissions.Should().ContainSingle().Subject;
        t.Operation.Should().Be(TransmissionOperation.Emit);
        t.IdempotencyKey.Should().Be(Clave());
        t.Outcome.Should().Be(ChannelOutcome.Validated);
        t.ApplicationResponseAttachmentPublicId.Should().NotBeNull();
        t.ExternalReference.Should().Be("track-1");

        var v = x.Versions.Single();
        v.CanonicalAttachmentPublicId.Should().NotBeNull("el canónico se sube si falta");
        v.SignedXmlAttachmentPublicId.Should().NotBeNull();
        v.AttachedDocumentAttachmentPublicId.Should().NotBeNull();
        _s.Subidas.Should().Contain(u => u.FileName == "SETP990000123-v1-canonico.json" && u.OwnerEntityType == "ElectronicSalesDocument"
            && u.OwnerEntityPublicId == d.PublicId);
        _s.Subidas.Should().Contain(u => u.FileName == "SETP990000123-v1-xml-firmado.xml");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Validated_sin_codigo_unico_o_sin_ApplicationResponse_deja_Sent(bool codigo, bool respuesta)
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated, codigo, respuesta));

        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Sent);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Sent);
        x.NextAttemptAt.Should().Be(_s.Ahora + _s.Esperas.Esperas[0]);
    }

    [Fact]
    public async Task InProcess_deja_Sent_y_el_siguiente_intento_consulta_antes_de_reenviar()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InProcess, false, false));
        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Sent);

        _s.Adelantar();
        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        var r = await EmitirAsync(d);

        r.Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        _s.Canal.Llamadas.Select(l => l.Operacion).Should().Equal("Emitir", "Consultar");
        var x = await RecargarAsync(d);
        x.Transmissions.OrderBy(t => t.AttemptNumber).Select(t => t.Operation).Should().Equal(TransmissionOperation.Emit, TransmissionOperation.QueryStatus);
        x.Transmissions.Select(t => t.AttemptNumber).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task NotFound_en_la_consulta_vuelve_a_Pending_y_reenvia_la_misma_version_con_el_mismo_numero()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InProcess, false, false));
        await EmitirAsync(d);

        _s.Adelantar();
        _s.Canal.Consultas.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.NotFound));
        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Pending);

        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Validated, "tras NotFound se reenvía de inmediato");

        _s.Canal.Llamadas.Select(l => l.Operacion).Should().Equal("Emitir", "Consultar", "Emitir");
        _s.Canal.Llamadas.Where(l => l.Operacion == "Emitir").Select(l => (l.Clave, l.Numero)).Distinct().Should().ContainSingle()
            .Which.Should().Be((Clave(), "SETP990000123"));
        var x = await RecargarAsync(d);
        x.Versions.Should().ContainSingle("reenviar no crea versión");
    }

    [Fact]
    public async Task DianUnavailable_abre_un_evento_04_y_el_siguiente_documento_se_une_al_mismo()
    {
        var d1 = await _s.FacturaAsync(990000123, "16000110");
        var d2 = await _s.FacturaAsync(990000124, "16000110");
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.DianUnavailable, respuestaDeValidacion: false, tipoDian: "04"));
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.DianUnavailable, respuestaDeValidacion: false, tipoDian: "04"));

        (await EmitirAsync(d1)).Value.Status.Should().Be(ElectronicDocumentStatus.DianContingency);
        (await EmitirAsync(d2)).Value.Status.Should().Be(ElectronicDocumentStatus.DianContingency);

        var x1 = await RecargarAsync(d1);
        var x2 = await RecargarAsync(d2);
        x1.ContingencyType.Should().Be(ContingencyType.Dian04);
        x1.DianDocumentTypeCode.Should().Be("04");
        x1.ContingencyEventId.Should().NotBeNull().And.Be(x2.ContingencyEventId);
        var evento = await _s.E.Db.DianContingencyEvents.SingleAsync();
        evento.Type.Should().Be(ContingencyType.Dian04);
        evento.Status.Should().Be(ContingencyEventStatus.Open);
        evento.ChannelCode.Should().Be("SIMULADO");
        await _s.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.ContingenciaAbierta), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_respuesta_definitiva_posterior_cierra_el_evento_04_del_canal()
    {
        var d1 = await _s.FacturaAsync(990000123);
        var d2 = await _s.FacturaAsync(990000124);
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.DianUnavailable, respuestaDeValidacion: false, tipoDian: "04"));
        await EmitirAsync(d1);

        _s.Adelantar(TimeSpan.FromMinutes(30));
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        await EmitirAsync(d2);

        _s.E.Db.DescartarCambios();
        var evento = await _s.E.Db.DianContingencyEvents.SingleAsync();
        evento.Status.Should().Be(ContingencyEventStatus.Closed);
        evento.EndedAt.Should().Be(_s.Ahora);
        evento.DeadlineAt.Should().NotBeNull();
        (await RecargarAsync(d1)).TransmissionDeadline.Should().Be(evento.DeadlineAt);
    }

    [Fact]
    public async Task ChannelUnavailable_deja_Pending_con_la_siguiente_espera_y_una_falla_al_circuito()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable));

        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Pending);

        var x = await RecargarAsync(d);
        x.NextAttemptAt.Should().Be(_s.Ahora + _s.Esperas.Esperas[0]);
        x.AttemptCount.Should().Be(1);
        await _s.Fallas.Received(1).RegistrarFallaAsync("SIMULADO", Arg.Any<CancellationToken>());

        // La segunda espera es la siguiente de la lista.
        _s.Adelantar(_s.Esperas.Esperas[0]);
        _s.Canal.Emisiones.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable));
        await EmitirAsync(d);
        (await RecargarAsync(d)).NextAttemptAt.Should().Be(_s.Ahora + _s.Esperas.Esperas[1]);
    }

    [Fact]
    public async Task Antes_de_la_espera_el_procesador_no_lo_toma_pero_Reintentar_ahora_si()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable));
        await EmitirAsync(d);

        var sinTurno = await EmitirAsync(d);
        sinTurno.Value.Attempted.Should().BeFalse();
        _s.Canal.Llamadas.Should().HaveCount(1);

        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        (await EmitirAsync(d, ahora: true)).Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
    }

    [Fact]
    public async Task Rejected_levanta_Dian_DocumentoRechazado_y_no_programa_otro_intento()
    {
        var d = await _s.FacturaAsync(taxId: "16000111");
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, false, true, null,
            new MensajeDelCanal("FAJ43b", TipoDeMensajeDelCanal.Rechazo, "Nombre no coincide", "El nombre del comprador no coincide con el RUT.")));

        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Rejected);

        var x = await RecargarAsync(d);
        x.RejectedBy.Should().Be(RejectedBy.Dian);
        x.NextAttemptAt.Should().BeNull();
        x.LastMessagesJson.Should().Contain("FAJ43b");
        await _s.Alertas.Received(1).LevantarAsync(
            Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.DocumentoRechazado && a.EntityPublicId == d.PublicId && a.DedupKey != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_fila_arrendada_por_otro_responde_InProgress_con_leaseUntil()
    {
        var d = await _s.FacturaAsync();
        var hasta = _s.Ahora.AddMinutes(1);
        d.LeaseUntil = hasta;
        d.LeaseOwner = "otra-replica";
        await _s.E.Db.SaveChangesAsync();

        var r = await EmitirAsync(d, ahora: true);

        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.InProgressCode);
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("leaseUntil").GetDateTime().Should().Be(hasta);
        _s.Canal.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_arrendamiento_vencido_sin_soltar_hace_que_el_siguiente_intento_consulte_antes_de_reenviar()
    {
        var d = await _s.FacturaAsync();
        d.LeaseUntil = _s.Ahora.AddMinutes(-1);
        d.LeaseOwner = "replica-que-cayo";
        await _s.E.Db.SaveChangesAsync();
        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));

        (await EmitirAsync(d)).Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        _s.Canal.Llamadas.Select(l => l.Operacion).Should().Equal("Consultar");
    }

    [Fact]
    public async Task Reintentar_ahora_en_Sent_responde_AwaitingResponse_y_en_final_Final()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InProcess, false, false));
        await EmitirAsync(d);

        (await EmitirAsync(d, ahora: true)).Error.Code.Should().Be(TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta);

        _s.Adelantar();
        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        await EmitirAsync(d);
        (await EmitirAsync(d, ahora: true)).Error.Code.Should().Be(TransicionesDelDocumentoElectronico.CodigoFinal);
    }

    [Fact]
    public async Task Cada_llamada_agrega_exactamente_una_transmision_con_la_clave_de_idempotencia()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable));
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InProcess, false, false));
        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.ValidatedWithNotices));

        await EmitirAsync(d);
        _s.Adelantar();
        await EmitirAsync(d);
        _s.Adelantar();
        await EmitirAsync(d);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.ValidatedWithNotices);
        x.Transmissions.Should().HaveCount(3).And.OnlyContain(t => t.IdempotencyKey == Clave() && t.ChannelCode == "SIMULADO");
        x.Transmissions.Select(t => t.AttemptNumber).Should().BeEquivalentTo([1, 2, 3]);
        x.Transmissions.Should().OnlyContain(t => t.RequestedByName == IngenIA365ERP.Application.Common.Execution.Actor.NombreDelProceso);
        _s.Canal.Llamadas.Should().HaveCount(3);
    }

    [Fact]
    public async Task No_transmite_mientras_el_documento_que_espera_no_este_validado()
    {
        var original = await _s.FacturaAsync(990000123);
        var siguiente = await _s.FacturaAsync(990000124);
        siguiente.WaitsForDocumentId = original.Id;
        await _s.E.Db.SaveChangesAsync();

        var r = await EmitirAsync(siguiente);

        r.IsSuccess.Should().BeTrue();
        r.Value.Attempted.Should().BeFalse();
        _s.Canal.Llamadas.Should().BeEmpty();
        (await RecargarAsync(siguiente)).Transmissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_el_canonico_reconstruido_no_coincide_no_emite_y_alerta_sin_validar()
    {
        var d = await _s.FacturaAsync();
        _s.Reconstruccion.Canonico = _s.Reconstruccion.Canonico! with { CanonicalSha256 = new string('f', 64) };

        var r = await EmitirAsync(d);

        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.CanonicalMismatchCode);
        _s.Canal.Llamadas.Should().BeEmpty();
        await _s.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.DocumentoSinValidar), Arg.Any<CancellationToken>());
        (await RecargarAsync(d)).LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task La_consulta_confirma_un_rechazo_y_una_validacion_tardia_deja_el_pendiente_validado()
    {
        var rechazado = await _s.FacturaAsync(990000123, "16000111");
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, false, true));
        await EmitirAsync(rechazado);

        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, false, true));
        var r = await _s.Consultar().Handle(new QueryElectronicDocumentStatusCommand(rechazado.PublicId), default);
        r.Value.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        var x = await RecargarAsync(rechazado);
        x.Transmissions.Should().Contain(t => t.Operation == TransmissionOperation.QueryStatus && t.Outcome == ChannelOutcome.Rejected);

        var pendiente = await _s.FacturaAsync(990000124);
        _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        (await _s.Consultar().Handle(new QueryElectronicDocumentStatusCommand(pendiente.PublicId), default)).Value.Status
            .Should().Be(ElectronicDocumentStatus.Validated);
    }
}
