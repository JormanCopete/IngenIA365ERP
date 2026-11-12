using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.DeadLetters;
using IngenIA365ERP.Application.Common.Mensajeria;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 023 (D3; research R6, R7): el tope de intentos transitorios del documento electrónico, la muerte con su alerta, el reinicio con una
/// respuesta definitiva, que el rechazo no cuenta, el seguimiento agendado en la transacción del resultado, el seguimiento durable
/// (canónico, tope y muerte) y el reproceso de los muertos.
/// </summary>
public class TopeMuerteYSeguimientoTests
{
    private readonly EscenarioDeEmision _s = new();

    private TopeDeIntentos Topes() => new(_s.E.Lector(), _s.E.Reloj);

    private async Task TopeAsync(int tope)
    {
        _s.E.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.TopeDeIntentos,
            ScopeKind = ParameterScopeKind.None, Value = tope.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        await _s.E.Db.SaveChangesAsync();
    }

    private EmitElectronicDocumentCommandHandler Emitir() => new(new IntentoAnteElCanal(
        _s.E.Db, new RegistroDeCanales(_s.Canal), _s.E.Credenciales, _s.E.Tenant, _s.E.Reloj, _s.Actor, new ArrendamientoEnMemoria(_s.E.Db),
        _s.Esperas, _s.Artefactos(), _s.Contingencias(), _s.Alertas, NullLogger<IntentoAnteElCanal>.Instance, _s.Fallas, topes: Topes()));

    private async Task<ElectronicDocument> RecargarAsync(ElectronicDocument d)
    {
        _s.E.Db.DescartarCambios();
        return await _s.E.Db.ElectronicDocuments.Include(x => x.Versions).SingleAsync(x => x.Id == d.Id);
    }

    private static ResultadoDeCanal Caido() => CanalGuionado.Respuesta(ChannelOutcome.ChannelUnavailable, codigo: false, respuestaDeValidacion: false);

    [Fact]
    public async Task Los_intentos_transitorios_cuentan_y_al_llegar_al_tope_el_documento_muere_con_su_alerta()
    {
        await TopeAsync(3);
        var d = await _s.FacturaAsync();
        for (var i = 0; i < 3; i++)
        {
            _s.Canal.Emisiones.Enqueue(Caido());
            (await Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default)).IsSuccess.Should().BeTrue();
            _s.Adelantar();
        }

        var x = await RecargarAsync(d);
        x.TransientFailures.Should().Be(3);
        x.DeadAt.Should().NotBeNull();
        x.DeadAttemptLimit.Should().Be(3);
        x.NextAttemptAt.Should().BeNull("el muerto espera a una persona");
        EstadoVisibleDelDocumento.De(x).Should().Be(EstadoVisible.Dead);
        await _s.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.DocumentoSinEnviar), Arg.Any<CancellationToken>());

        var otro = await Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default);
        otro.IsSuccess.Should().BeTrue();
        otro.Value.Attempted.Should().BeFalse("nadie lo intenta solo");
        _s.Canal.Llamadas.Should().HaveCount(3);
    }

    [Fact]
    public async Task Una_respuesta_definitiva_reinicia_la_cuenta_y_agenda_el_seguimiento()
    {
        await TopeAsync(3);
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(Caido());
        await Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default);
        _s.Adelantar();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        await Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Validated);
        x.TransientFailures.Should().Be(0);
        x.DeadAt.Should().BeNull();
        x.FollowUpDueAt.Should().Be(_s.Ahora, "la representación y el correo quedan agendados en la misma transacción del resultado");
    }

    [Fact]
    public async Task Un_rechazo_no_cuenta_como_intento_transitorio_ni_muere()
    {
        await TopeAsync(1);
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, codigo: false));
        await Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        x.TransientFailures.Should().Be(0);
        x.DeadAt.Should().BeNull("los rechazos se corrigen por su flujo, no por mensajes muertos");
    }

    // ------------------------------------------------------------------------------------------------ seguimiento --

    private SeguimientoDelDocumento Seguimiento() => new(_s.E.Db, new ArrendamientoEnMemoria(_s.E.Db), _s.Artefactos(), _s.Esperas, Topes(),
        _s.Alertas, _s.E.Reloj, NullLogger<SeguimientoDelDocumento>.Instance);

    [Fact]
    public async Task El_seguimiento_sube_el_canonico_que_falta_y_queda_al_dia()
    {
        var d = await _s.FacturaAsync();
        _s.Adelantar(SeguimientoDelDocumento.PrimeraEspera);

        var r = await Seguimiento().CompletarAsync(d.PublicId, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Hecho.Should().BeTrue();
        _s.Subidas.Should().ContainSingle(u => u.FileName.Contains("Canonical", StringComparison.OrdinalIgnoreCase) || u.ContentType == "application/json");
        var x = await RecargarAsync(d);
        x.FollowUpDueAt.Should().BeNull();
        x.Versions.Single().CanonicalAttachmentPublicId.Should().NotBeNull();
    }

    [Fact]
    public async Task Antes_de_su_hora_el_seguimiento_no_hace_nada()
    {
        var d = await _s.FacturaAsync();

        var r = await Seguimiento().CompletarAsync(d.PublicId, default);

        r.Value.Hecho.Should().BeFalse();
        _s.Subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_el_almacen_falla_el_seguimiento_reintenta_y_al_tope_muere_con_su_alerta()
    {
        await TopeAsync(2);
        _s.Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(new Error("Attachments.StoreUnavailable", "El almacén no responde.")));
        var d = await _s.FacturaAsync();

        for (var i = 0; i < 2; i++)
        {
            _s.Adelantar();
            (await Seguimiento().CompletarAsync(d.PublicId, default)).Value.Hecho.Should().BeFalse();
        }

        var x = await RecargarAsync(d);
        x.FollowUpAttempts.Should().Be(2);
        x.FollowUpDeadAt.Should().NotBeNull();
        x.FollowUpDueAt.Should().BeNull();
        x.FollowUpLastError.Should().NotBeNullOrWhiteSpace();
        await _s.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.SeguimientoDetenido), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------------ reproceso --

    [Fact]
    public async Task Reprocesar_un_muerto_lo_deja_para_ya_con_la_cuenta_en_cero_y_omite_lo_que_no_esta_muerto()
    {
        var muerto = await _s.FacturaAsync(990000123);
        var vivo = await _s.FacturaAsync(990000124, "16000111");
        muerto.DeadAt = _s.Ahora;
        muerto.DeadAttemptLimit = 12;
        muerto.TransientFailures = 12;
        muerto.NextAttemptAt = null;
        await _s.E.Db.SaveChangesAsync();
        var publicacion = new PublicacionDeAvisos(_s.E.Db, new ColaNula(), _s.E.Tenant, _s.E.Reloj);
        var handler = new ReprocessDeadLettersCommandHandler(_s.E.Db, _s.Alertas, _s.E.Reloj, publicacion);

        var r = await handler.Handle(new ReprocessDeadLettersCommand(
            [new DeadLetterRef(DeadLetterKind.ElectronicDocument, muerto.PublicId), new DeadLetterRef(DeadLetterKind.ElectronicDocument, vivo.PublicId)],
            "El proveedor volvió a responder"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Reprocessed.Should().Be(1);
        r.Value.Skipped.Should().ContainSingle(o => o.PublicId == vivo.PublicId && o.Code == ErroresDeMensajesMuertos.NotDeadCode);
        var x = await RecargarAsync(muerto);
        x.DeadAt.Should().BeNull();
        x.TransientFailures.Should().Be(0);
        x.NextAttemptAt.Should().Be(_s.Ahora);
        await _s.Alertas.Received(1).AtenderPorProcesoAsync(IntentoAnteElCanal.ClaveDeMuerto(x), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_reproceso_exige_motivo_y_entre_uno_y_doscientos_trabajos()
    {
        var v = new ReprocessDeadLettersCommandValidator();
        v.Validate(new ReprocessDeadLettersCommand([], "motivo suficiente")).IsValid.Should().BeFalse();
        v.Validate(new ReprocessDeadLettersCommand([new DeadLetterRef(DeadLetterKind.ElectronicDocument, Guid.NewGuid())], "corto")).IsValid.Should().BeFalse();
        v.Validate(new ReprocessDeadLettersCommand([new DeadLetterRef(DeadLetterKind.ElectronicDocument, Guid.NewGuid())], "El canal volvió a responder")).IsValid
            .Should().BeTrue();
    }
}
