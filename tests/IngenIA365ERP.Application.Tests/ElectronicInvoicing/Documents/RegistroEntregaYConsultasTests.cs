using System.IO.Compression;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 012, I4, sección «emisión, estado, artefactos y entrega» (T711, T714, T715, T717, T718, T720, T721): el registro en la confirmación
/// (canal sellado, contingencia 03), la entrega al comprador (sólo validado o en contingencia, nunca los dos canales), la bandeja, el cambio al
/// canal vigente, el enlace de artefactos con la regla del dueño, los tres dueños nuevos de adjuntos y las alertas por tiempo.
/// </summary>
public class RegistroEntregaYConsultasTests
{
    private readonly EscenarioDeEmision _s = new();

    // ------------------------------------------------------------------------------------------ registro (T711) --

    [Fact]
    public async Task El_registro_sella_canal_modo_ambiente_y_correo_y_crea_la_version_1_sin_llamar_al_canal()
    {
        var d = await _s.FacturaAsync();

        d.Status.Should().Be(ElectronicDocumentStatus.Pending);
        d.ChannelCode.Should().Be("SIMULADO");
        d.EmissionSettingId.Should().Be(_s.Configuracion.Id);
        d.Mode.Should().Be(_s.Configuracion.Mode);
        d.EmailDeliveryBy.Should().Be(EmailDeliveryBy.Erp);
        d.Number.Should().Be("SETP990000123");
        d.ResolutionId.Should().Be(_s.Resolucion.Id);
        d.NextAttemptAt.Should().NotBeNull();
        var v = d.Versions.Should().ContainSingle().Subject;
        v.VersionNumber.Should().Be(1);
        v.Reason.Should().Be(DocumentVersionReason.Initial);
        v.CanonicalSha256.Should().Be(_s.Reconstruccion.Canonico!.CanonicalSha256);
        v.EconomicFingerprint.Should().HaveLength(64);
        _s.Canal.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Numerado_en_contingencia_03_nace_IssuerContingency_unido_al_evento_abierto_y_sin_evento_no_se_registra()
    {
        var entrada = Entradas.Factura() with { DocumentPublicId = Guid.NewGuid() };
        var canonico = ConstructorDelCanonico.Construir(entrada, new ContextoDelCanonico
        {
            Configuracion = _s.Configuracion, Resolucion = _s.Resolucion, Prefijo = "SETP", Consecutivo = 990000200, Contingencia = ContingencyType.Issuer03,
        }).Value;
        var pedido = new PedidoDeRegistroElectronico("INV", entrada.DocumentPublicId, "FV", _s.Configuracion, canonico, _s.Resolucion.Id, ResolutionKind.Contingency);

        (await _s.Registro().RegistrarAsync(pedido, default)).Error.Code.Should().Be(ErroresDeDocumentosElectronicos.ContingencyNotOpenCode);

        var evento = _s.E.Contingencia03();
        var r = await _s.Registro().RegistrarAsync(pedido, default);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(ElectronicDocumentStatus.IssuerContingency);
        r.Value.ContingencyType.Should().Be(ContingencyType.Issuer03);
        r.Value.ContingencyEvent.Should().BeSameAs(evento);
    }

    // ------------------------------------------------------------------------------------------ entrega (T720) --

    private EntregaAlComprador Entrega(IEmailSender correo) =>
        new(_s.E.Db, correo, Substitute.For<IBlobStore>(), _s.Reconstruccion, _s.E.Reloj, NullLogger<EntregaAlComprador>.Instance);

    [Fact]
    public async Task Un_pendiente_no_se_entrega()
    {
        var d = await _s.FacturaAsync();
        var r = await Entrega(Substitute.For<IEmailSender>()).EntregarAsync(d, d.Versions.Single(), null, null, false, default);

        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.NotDeliverableCode);
    }

    [Fact]
    public async Task Validado_y_por_el_ERP_envia_un_ZIP_con_el_AttachedDocument_y_el_PDF_a_la_copia_fiscal()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        var correo = Substitute.For<IEmailSender>();

        var r = await Entrega(correo).EntregarAsync(d, d.Versions.Single(), [1, 2, 3], [9, 9], false, default);

        r.Value.Should().BeTrue();
        await correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m =>
            m.To == "ana@correo.co" && m.Subject.Contains("SETP990000123") && m.Attachments!.Count == 1
            && m.Attachments[0].FileName == "SETP990000123.zip"), Arg.Any<CancellationToken>());
        d.EmailSentAt.Should().NotBeNull();
        d.DeliveredAt.Should().NotBeNull();

        // Una segunda vez no reenvía, salvo que se pida.
        (await Entrega(correo).EntregarAsync(d, d.Versions.Single(), [1], [9], false, default)).Value.Should().BeFalse();
        (await Entrega(correo).EntregarAsync(d, d.Versions.Single(), [1], [9], true, default)).Value.Should().BeTrue();
    }

    [Fact]
    public async Task Con_entrega_por_el_canal_el_ERP_no_envia_nunca_los_dos()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        d.EmailDeliveryBy = EmailDeliveryBy.Channel;
        var correo = Substitute.For<IEmailSender>();

        (await Entrega(correo).EntregarAsync(d, d.Versions.Single(), [1], [9], false, default)).Value.Should().BeFalse();

        await correo.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        d.DeliveredAt.Should().NotBeNull();
        d.EmailSentAt.Should().BeNull();
    }

    // ------------------------------------------------------------------ envío por correo a pedido (2026-11-25) --

    /// <summary>La representación gráfica guardada de la versión, que el almacén devuelve al leerla.</summary>
    private async Task<IBlobStore> ConPdfAsync(ElectronicDocument d)
    {
        var adjunto = new Attachment
        {
            OwnerEntityType = AdjuntosDeModulo.DocumentoElectronicoDeVenta, OwnerEntityPublicId = d.PublicId, FileName = "SETP990000123.pdf",
            ContentType = "application/pdf", SizeBytes = 3, Sha256Hex = new string('a', 64), StoragePath = "fe/pdf", Status = EstadoDeAdjunto.Available,
            CreatedBy = "test",
        };
        _s.E.Db.Attachments.Add(adjunto);
        d.Versions.Single().FijarArtefacto(ArtefactoDeVersion.RepresentacionGrafica, adjunto.PublicId);
        await _s.E.Db.SaveChangesAsync();
        var almacen = Substitute.For<IBlobStore>();
        almacen.GetAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        return almacen;
    }

    private EntregaAlComprador Entrega(IEmailSender correo, IBlobStore almacen) =>
        new(_s.E.Db, correo, almacen, _s.Reconstruccion, _s.E.Reloj, NullLogger<EntregaAlComprador>.Instance);

    [Fact]
    public async Task A_pedido_sin_correo_escrito_sale_a_la_copia_fiscal_y_anota_el_envio()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        var correo = Substitute.For<IEmailSender>();

        var r = await Entrega(correo, await ConPdfAsync(d)).EnviarAPedidoAsync(d, d.Versions.Single(), null, default);

        r.Value.Should().Be(("ana@correo.co", true));
        await correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "ana@correo.co" && m.Attachments!.Single().FileName == "SETP990000123.zip"),
            Arg.Any<CancellationToken>());
        d.EmailSentAt.Should().NotBeNull();
        d.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_pedido_sale_al_correo_escrito_aunque_lo_entregue_el_canal_y_es_una_copia()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        d.EmailDeliveryBy = EmailDeliveryBy.Channel;
        var correo = Substitute.For<IEmailSender>();

        var r = await Entrega(correo, await ConPdfAsync(d)).EnviarAPedidoAsync(d, d.Versions.Single(), " otro@correo.co ", default);

        r.Value.Should().Be(("otro@correo.co", false), "la persona lo pidió: no es la entrega automática");
        await correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "otro@correo.co"), Arg.Any<CancellationToken>());
        d.EmailSentAt.Should().BeNull("a otra dirección es una copia: la entrega al comprador no cambia");
    }

    [Fact]
    public async Task A_pedido_no_sale_sin_validar_ni_sin_la_representacion_grafica_ni_si_el_correo_falla()
    {
        var d = await _s.FacturaAsync();
        var correo = Substitute.For<IEmailSender>();

        (await Entrega(correo).EnviarAPedidoAsync(d, d.Versions.Single(), null, default)).Error.Code
            .Should().Be(ErroresDeDocumentosElectronicos.NotDeliverableCode);

        d.Status = ElectronicDocumentStatus.Validated;
        (await Entrega(correo).EnviarAPedidoAsync(d, d.Versions.Single(), null, default)).Error.Code
            .Should().Be(ErroresDeDocumentosElectronicos.RepresentationPendingCode);

        var almacen = await ConPdfAsync(d);
        correo.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("SMTP caído")));
        (await Entrega(correo, almacen).EnviarAPedidoAsync(d, d.Versions.Single(), null, default)).Error.Code
            .Should().Be(ErroresDeDocumentosElectronicos.EmailFailedCode);
        d.EmailSentAt.Should().BeNull();
        await correo.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_el_permiso_del_dueno_el_envio_responde_lo_mismo_que_si_no_existiera_y_no_envia()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        var correo = Substitute.For<IEmailSender>();
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new SendElectronicDocumentByEmailCommandHandler(_s.E.Db, [], permisos, Entrega(correo, await ConPdfAsync(d)),
            new ServiceCollection().BuildServiceProvider());

        var r = await handler.Handle(new SendElectronicDocumentByEmailCommand(d.PublicId, "otro@correo.co"), default);

        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.NotFoundCode);
        await correo.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_el_permiso_del_dueno_el_envio_sale_y_responde_a_donde()
    {
        var d = await _s.FacturaAsync();
        d.Status = ElectronicDocumentStatus.Validated;
        var correo = Substitute.For<IEmailSender>();
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync("Inventory.Sales.View", Arg.Any<CancellationToken>()).Returns(true);
        var handler = new SendElectronicDocumentByEmailCommandHandler(_s.E.Db, [], permisos, Entrega(correo, await ConPdfAsync(d)),
            new ServiceCollection().BuildServiceProvider());

        var r = await handler.Handle(new SendElectronicDocumentByEmailCommand(d.PublicId), default);

        r.Value.Should().Be(new EnvioPorCorreoDto(d.PublicId, "ana@correo.co", true));
        await correo.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_ZIP_lleva_el_AttachedDocument_y_el_PDF()
    {
        var zip = EntregaAlComprador.Empaquetar("FE1", [1, 2], [3])!;
        using var archivo = new ZipArchive(new MemoryStream(zip));
        archivo.Entries.Select(e => e.FullName).Should().BeEquivalentTo(["FE1.xml", "FE1.pdf"]);
    }

    // ------------------------------------------------------------------------------------------ bandeja (T715) --

    [Fact]
    public async Task La_bandeja_filtra_por_estado_y_trae_los_vencidos_sin_validar()
    {
        var viejo = await _s.FacturaAsync(990000123);
        var nuevo = await _s.FacturaAsync(990000124);
        nuevo.Status = ElectronicDocumentStatus.Validated;
        viejo.IssuedAt = _s.Ahora.AddHours(-1);
        nuevo.IssuedAt = _s.Ahora;
        await _s.E.Db.SaveChangesAsync();
        var handler = new ListElectronicDocumentsQueryHandler(_s.E.Db, _s.E.Lector(), _s.E.Reloj, []);

        var validados = await handler.Handle(new ListElectronicDocumentsQuery(Status: ElectronicDocumentStatus.Validated), default);
        validados.Value.Items.Should().ContainSingle().Which.ElectronicDocumentPublicId.Should().Be(nuevo.PublicId);

        var vencidos = await handler.Handle(new ListElectronicDocumentsQuery(Overdue: true), default);
        vencidos.Value.Items.Should().ContainSingle().Which.Number.Should().Be("SETP990000123");
    }

    [Fact]
    public async Task La_bandeja_filtra_por_cliente_y_por_valor()
    {
        var deAna = await _s.FacturaAsync(990000125);
        var deRedeban = await _s.FacturaAsync(990000126);
        deAna.CounterpartyTaxId = "31999888";
        deAna.CounterpartyName = "ANA PÉREZ";
        deAna.TotalAmount = 15900m;
        deRedeban.CounterpartyTaxId = "860034313";
        deRedeban.CounterpartyName = "REDEBAN S.A.";
        deRedeban.TotalAmount = 2000m;
        await _s.E.Db.SaveChangesAsync();
        var handler = new ListElectronicDocumentsQueryHandler(_s.E.Db, _s.E.Lector(), _s.E.Reloj, []);

        async Task<List<Guid>> Ids(ListElectronicDocumentsQuery q) =>
            [.. (await handler.Handle(q, default)).Value.Items.Select(i => i.ElectronicDocumentPublicId)];

        (await Ids(new ListElectronicDocumentsQuery(Customer: "pérez ana"))).Should().Equal(deAna.PublicId);
        (await Ids(new ListElectronicDocumentsQuery(Customer: "8600343"))).Should().Equal(deRedeban.PublicId);
        (await Ids(new ListElectronicDocumentsQuery(MinTotal: 10000m))).Should().Equal(deAna.PublicId);
        (await Ids(new ListElectronicDocumentsQuery(MaxTotal: 2000m))).Should().Equal(deRedeban.PublicId);
        new ListElectronicDocumentsQueryValidator().Validate(new ListElectronicDocumentsQuery(MinTotal: 5m, MaxTotal: 1m)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task El_detalle_trae_versiones_y_transmisiones()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        await _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId), default);

        var r = await new GetElectronicDocumentQueryHandler(_s.E.Db, []).Handle(new GetElectronicDocumentQuery(d.PublicId), default);

        r.Value.Document.Status.Should().Be(ElectronicDocumentStatus.Validated);
        r.Value.Versions.Should().ContainSingle().Which.Artifacts.Select(a => a.Artifact)
            .Should().Contain([ArtefactosElectronicos.Canonical, ArtefactosElectronicos.SignedXml, ArtefactosElectronicos.AttachedDocument]);
        r.Value.Transmissions.Should().ContainSingle().Which.Operation.Should().Be(TransmissionOperation.Emit);
    }

    // ------------------------------------------------------------------------------------------ canal vigente (T714) --

    [Fact]
    public async Task Con_el_canal_sellado_vigente_no_se_pasa_al_nuevo()
    {
        var d = await _s.FacturaAsync();
        var r = await new TransmitByCurrentChannelCommandHandler(_s.E.Db, _s.E.Canales, _s.E.Reloj)
            .Handle(new TransmitByCurrentChannelCommand(d.PublicId, "cambio de proveedor"), default);

        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.ChannelNotLinkedCode);
    }

    [Fact]
    public async Task Retirado_el_canal_pasa_al_vigente_solo_si_la_resolucion_esta_asociada()
    {
        var d = await _s.FacturaAsync();
        _s.Configuracion.ValidTo = new DateOnly(2026, 11, 30);
        _s.E.Configuracion("PROV", desde: new DateOnly(2026, 12, 1));
        var handler = new TransmitByCurrentChannelCommandHandler(_s.E.Db, _s.E.Canales, _s.E.Reloj);

        (await handler.Handle(new TransmitByCurrentChannelCommand(d.PublicId, "cambio"), default)).Error.Code
            .Should().Be(ErroresDeDocumentosElectronicos.ChannelNotLinkedCode, "la resolución no está asociada a PROV");

        _s.E.Db.DianResolutionChannels.Add(new DianResolutionChannel { ResolutionId = _s.Resolucion.Id, ChannelCode = "PROV", ValidFrom = new DateOnly(2026, 1, 1) });
        await _s.E.Db.SaveChangesAsync();
        var r = await handler.Handle(new TransmitByCurrentChannelCommand(d.PublicId, "cambio"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.PreviousChannelCode.Should().Be("SIMULADO");
        d.ChannelCode.Should().Be("PROV");
    }

    // ------------------------------------------------------------------------------------------ enlace y dueños (T717, T718) --

    [Fact]
    public async Task Sin_el_permiso_del_dueno_el_enlace_responde_lo_mismo_que_si_no_existiera()
    {
        var d = await _s.FacturaAsync();
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new GetElectronicArtifactLinkQueryHandler(_s.E.Db, Substitute.For<IBlobStore>(), permisos, _s.E.Reloj,
            Options.Create(new LimitesDeAdjuntos()), new ServiceCollection().BuildServiceProvider());

        var r = await handler.Handle(new GetElectronicArtifactLinkQuery(d.PublicId, "Canonical"), default);

        r.Error.Code.Should().Be("Generic.NotFound");
        await permisos.Received().HasPermissionAsync("Inventory.Sales.View", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AdjuntosDeModulo.DocumentoElectronicoDeVenta, "Inventory.Sales.View")]
    [InlineData(AdjuntosDeModulo.DocumentoElectronicoDeCompra, "Inventory.Purchases.View")]
    public async Task Los_artefactos_se_leen_con_el_permiso_del_modulo_y_no_se_borran_ni_reciben_subidas(string dueno, string permiso)
    {
        AdjuntosDeModulo.De(dueno)!.PermisoDeLectura.Should().Be(permiso);
        (await AdjuntosDeModulo.PuedeBorrarAsync(_s.E.Db, dueno, Guid.NewGuid(), default))!.Code.Should().Be(AttachmentErrorCodes.OwnedByModule);
        (await AdjuntosDeModulo.PuedeSubirAsync(_s.E.Db, Substitute.For<IPermissionChecker>(), dueno, Guid.NewGuid(), default))!.Code
            .Should().Be(AttachmentErrorCodes.OwnerNotAllowed);
    }

    [Fact]
    public async Task Las_evidencias_de_una_contingencia_admiten_subidas_con_Declare_y_no_se_borran()
    {
        var evento = _s.E.Contingencia03();
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync("ElectronicInvoicing.Contingencies.Declare", Arg.Any<CancellationToken>()).Returns(true);

        (await AdjuntosDeModulo.PuedeSubirAsync(_s.E.Db, permisos, AdjuntosDeModulo.EventoDeContingencia, evento.PublicId, default)).Should().BeNull();
        (await AdjuntosDeModulo.PuedeSubirAsync(_s.E.Db, permisos, AdjuntosDeModulo.EventoDeContingencia, Guid.NewGuid(), default))!.Code
            .Should().Be("Generic.NotFound");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_s.E.Db, AdjuntosDeModulo.EventoDeContingencia, evento.PublicId, default))!.Code
            .Should().Be(AttachmentErrorCodes.OwnerLocked);
        AttachmentPolicy.AdmiteGeneradoPorModulo("application/xml").Should().BeTrue();
        AttachmentPolicy.AllowedMimeTypes.Should().NotContain("application/xml", "las subidas de personas no cambian");
    }

    // ------------------------------------------------------------------------------------------ alertas (T721) --

    [Fact]
    public async Task Las_alertas_por_tiempo_levantan_sin_validar_y_resolucion_por_agotar_una_por_condicion()
    {
        var d = await _s.FacturaAsync();
        d.IssuedAt = _s.Ahora.AddHours(-1);
        _s.Resolucion.RangeFrom = 1;
        _s.Resolucion.RangeTo = 10;
        _s.Resolucion.LastIssuedNumber = 10;
        await _s.E.Db.SaveChangesAsync();
        var enviados = new List<AlertaALevantar>();
        _s.Sender.Send(Arg.Any<RaiseAlertCommand>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            enviados.Add(c.Arg<RaiseAlertCommand>().Alerta);
            return Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false));
        });

        var r = await new AlertasDeFacturacionElectronica(_s.E.Db, _s.E.Lector(), _s.E.Reloj, _s.Sender).RevisarAsync(default);

        r.SinValidar.Should().Be(1);
        r.PorAgotar.Should().Be(1);
        r.PorVencer.Should().Be(1, "la resolución vence el 31 de diciembre y el aviso es a 30 días");
        enviados.Should().Contain(a => a.TypeCode == TiposDeAlerta.DocumentoSinValidar && a.DedupKey == GuardadoDeArtefactos.ClaveSinValidar(d.PublicId));
        enviados.Select(a => a.DedupKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void La_tarea_corre_cada_intervalo()
    {
        var tarea = new TareaDeAlertasDeFacturacionElectronica();
        var ahora = new DateTimeOffset(2026, 12, 5, 10, 0, 0, TimeSpan.FromHours(-5));

        tarea.Nombre.Should().Be("einvoicing.alerts");
        tarea.DebeCorrer(ahora, null).Should().BeTrue();
        tarea.DebeCorrer(ahora, ahora.AddMinutes(-1)).Should().BeFalse();
        tarea.DebeCorrer(ahora, ahora - tarea.Intervalo).Should().BeTrue();
    }
}
