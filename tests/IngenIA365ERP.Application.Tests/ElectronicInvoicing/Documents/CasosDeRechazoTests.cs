using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 012, I4, T680 (FR-066; contracts/dian.md §8; api.md §24.5): los casos a, b y c de un documento rechazado, con una fuente falsa.
/// Caso a con la huella igual crea la versión n + 1 <c>CaseA</c>, agrega la versión de la copia fiscal y no emite mensajes de negocio; con la
/// huella distinta → <c>ElectronicInvoicing.Document.EconomicFootprintChanged</c> (<c>data.fields[]</c>). b y c sin rechazo confirmado por
/// la consulta → <c>.RejectionNotConfirmed</c>; sobre <c>Sent</c> → <c>.AwaitingResponse</c>; sobre <c>Validated</c> → <c>.NotRejected</c>.
/// b anula sin efecto fiscal (lo que marca <c>FiscalNumberReleased</c> en la fuente), confirma el reemplazo con el <b>mismo</b> número y
/// deja la versión <c>CaseB</c> en <c>Pending</c>; c deja <c>CancelledWithoutReplacement</c> con motivo y responsable.
/// </summary>
public class CasosDeRechazoTests
{
    private const int Usuario = 7;
    private const string PermisoVentas = "Inventory.Sales.Confirm";

    private readonly EscenarioDeEmision _s = new();
    private readonly IFuenteDeDocumentoElectronico _fuente = Substitute.For<IFuenteDeDocumentoElectronico>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IActorActual _persona = Substitute.For<IActorActual>();
    private readonly Guid _anulacion = Guid.NewGuid();
    private readonly Guid _reemplazo = Guid.NewGuid();
    private InventoryDocument? _numerado;

    public CasosDeRechazoTests()
    {
        _fuente.SourceModule.Returns("INV");
        _fuente.PermisoDeConfirmar(Arg.Any<ElectronicDocumentKind>()).Returns(PermisoVentas);
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _persona.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, Usuario, Guid.NewGuid(), Guid.NewGuid(), "Ana Cajera",
            null, ExecutionChannel.Web, "prueba", null, null));

        _fuente.LeerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c => Result.Success(Entradas.Factura() with { DocumentPublicId = c.Arg<Guid>() }));
        _fuente.ContraparteDelMaestroAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<FotoFiscalDeEntrada?>(Entradas.Factura().Contrapartes[0] with { Version = 2 }));
        _fuente.RegistrarVersionDeContraparteAsync(Arg.Any<Guid>(), Arg.Any<FotoFiscalDeEntrada>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _fuente.AnularSinEfectoFiscalAsync(Arg.Any<Guid>(), Arg.Any<CasoFiscalDeAnulacion>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AnulacionSinEfectoFiscal(_anulacion, "AN1")));
        _fuente.ConfirmarReemplazoAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Action<InventoryDocument>>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                _numerado = new InventoryDocument { PublicId = c.ArgAt<Guid>(1) };
                c.Arg<Action<InventoryDocument>>()(_numerado);
                return Result.Success(new ReemplazoConfirmado(c.ArgAt<Guid>(1), "SETP990000123"));
            });
        _fuente.CrearBorradorDeReemplazoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new BorradorDeReemplazo(_reemplazo, "INV", $"/api/inventory/sales/invoices/{_reemplazo}")));
    }

    private CasosDeRechazo Casos() => new(_s.E.Db, [_fuente], _permisos);

    private ConstructorDelCanonico Constructor() => new(_s.E.Lector());

    private Task<Result<CorreccionDelRechazoDto>> CorregirAsync(ElectronicDocument d, CambiosDeContraparte? cambios = null) =>
        new CorrectRejectedDocumentCommandHandler(_s.E.Db, Casos(), Constructor(), _s.E.Reloj)
            .Handle(new CorrectRejectedDocumentCommand(d.PublicId, "El correo de recepción estaba mal", cambios), default);

    private Task<Result<ReemplazoDelRechazoDto>> ReemplazarAsync(ElectronicDocument d) =>
        new ReplaceRejectedDocumentCommandHandler(_s.E.Db, Casos(), Constructor(), _s.E.Reloj)
            .Handle(new ReplaceRejectedDocumentCommand(d.PublicId, _reemplazo, "Se facturó un producto que no era"), default);

    private Task<Result<CancelacionDelRechazoDto>> CancelarAsync(ElectronicDocument d) =>
        new CancelRejectedDocumentCommandHandler(_s.E.Db, Casos(), _persona, _s.E.Reloj)
            .Handle(new CancelRejectedDocumentCommand(d.PublicId, "El cliente desistió de la compra"), default);

    private Task<Result<BorradorDeReemplazo>> BorradorAsync(ElectronicDocument d) =>
        new CreateReplacementDraftCommandHandler(Casos()).Handle(new CreateReplacementDraftCommand(d.PublicId), default);

    private async Task<ElectronicDocument> RecargarAsync(ElectronicDocument d)
    {
        _s.E.Db.DescartarCambios();
        return await _s.E.Db.ElectronicDocuments.Include(x => x.Versions).Include(x => x.Transmissions).SingleAsync(x => x.Id == d.Id);
    }

    /// <summary>Una factura rechazada por la DIAN al emitirla; con <paramref name="confirmar"/>, la consulta de estado lo confirma.</summary>
    private async Task<ElectronicDocument> RechazadaAsync(bool confirmar)
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, false, true));
        (await _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId, false), default)).Value.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        if (confirmar)
        {
            _s.Adelantar();
            _s.Canal.Consultas.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Rejected, false, true));
            (await _s.Consultar().Handle(new QueryElectronicDocumentStatusCommand(d.PublicId), default)).IsSuccess.Should().BeTrue();
        }
        return await RecargarAsync(d);
    }

    private async Task<ElectronicDocument> EnviadaAsync()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InProcess, false, false));
        (await _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId, false), default)).Value.Status.Should().Be(ElectronicDocumentStatus.Sent);
        return await RecargarAsync(d);
    }

    private async Task<ElectronicDocument> ValidadaAsync()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        (await _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId, false), default)).Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        return await RecargarAsync(d);
    }

    // ------------------------------------------------------------------------------------------------------------ caso a --

    [Fact]
    public async Task Caso_a_con_la_huella_igual_crea_la_version_2_CaseA_agrega_la_copia_fiscal_y_vuelve_a_Pending_sin_mensajes()
    {
        var d = await RechazadaAsync(confirmar: false);
        _fuente.ContraparteDelMaestroAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<FotoFiscalDeEntrada?>(Entradas.Factura().Contrapartes[0] with { Version = 2, Email = "facturas.ana@correo.co" }));

        var r = await CorregirAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.VersionNumber.Should().Be(2);
        r.Value.Status.Should().Be(ElectronicDocumentStatus.Pending);
        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Pending);
        x.RejectedBy.Should().BeNull();
        x.Number.Should().Be("SETP990000123", "el caso a reemite con el mismo número");
        x.ChannelCode.Should().Be(d.ChannelCode, "y por el mismo canal");
        x.NextAttemptAt.Should().Be(_s.Ahora);
        var v2 = x.Versions.Single(v => v.VersionNumber == 2);
        v2.Reason.Should().Be(DocumentVersionReason.CaseA);
        v2.SourceDocumentPublicId.Should().Be(d.SourceDocumentPublicId);
        v2.CorrectionReason.Should().Be("El correo de recepción estaba mal");
        v2.ChangedFieldsJson.Should().Contain("counterparty.email").And.Contain("ana@correo.co").And.Contain("facturas.ana@correo.co");
        v2.EconomicFingerprint.Should().Be(x.Versions.Single(v => v.VersionNumber == 1).EconomicFingerprint, "la huella económica no cambió");
        x.CurrentVersionId.Should().Be(v2.Id);

        await _fuente.Received(1).RegistrarVersionDeContraparteAsync(d.SourceDocumentPublicId,
            Arg.Is<FotoFiscalDeEntrada>(f => f.Version == 2 && f.Email == "facturas.ana@correo.co"), "El correo de recepción estaba mal", Arg.Any<CancellationToken>());
        await _fuente.DidNotReceiveWithAnyArgs().AnularSinEfectoFiscalAsync(default, default, default!, default);
        (await _s.E.Db.IntegrationMessages.CountAsync()).Should().Be(0, "el caso a no emite mensajes de negocio");
    }

    [Fact]
    public async Task Caso_a_toma_los_cambios_del_cuerpo_sobre_el_maestro()
    {
        var d = await RechazadaAsync(confirmar: false);

        var r = await CorregirAsync(d, new CambiosDeContraparte(Name: "ANA MARÍA PÉREZ", Phone: "3001234567"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        var x = await RecargarAsync(d);
        x.CounterpartyName.Should().Be("ANA MARÍA PÉREZ", "la bandeja copia la contraparte corregida");
        x.Versions.Single(v => v.VersionNumber == 2).ChangedFieldsJson.Should().Contain("counterparty.name").And.Contain("counterparty.phone");
        await _fuente.Received(1).RegistrarVersionDeContraparteAsync(d.SourceDocumentPublicId,
            Arg.Is<FotoFiscalDeEntrada>(f => f.LegalName == "ANA MARÍA PÉREZ" && f.Phone == "3001234567"), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caso_a_sin_cambios_en_la_contraparte_no_agrega_version_de_la_copia_fiscal()
    {
        var d = await RechazadaAsync(confirmar: false);

        var r = await CorregirAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        (await RecargarAsync(d)).Versions.Should().HaveCount(2);
        await _fuente.DidNotReceiveWithAnyArgs().RegistrarVersionDeContraparteAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Caso_a_con_la_huella_distinta_responde_EconomicFootprintChanged_con_los_campos_y_no_cambia_nada()
    {
        var d = await RechazadaAsync(confirmar: false);
        _fuente.ContraparteDelMaestroAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<FotoFiscalDeEntrada?>(Entradas.Factura().Contrapartes[0] with { Version = 2, TaxId = "16000999" }));

        var r = await CorregirAsync(d);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("ElectronicInvoicing.Document.EconomicFootprintChanged");
        System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain("counterparty.taxId");
        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        x.Versions.Should().ContainSingle();
        await _fuente.DidNotReceiveWithAnyArgs().RegistrarVersionDeContraparteAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Caso_a_no_exige_el_rechazo_confirmado()
    {
        var d = await RechazadaAsync(confirmar: false);
        (await CorregirAsync(d)).IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------------ estados --

    [Fact]
    public async Task b_y_c_sin_rechazo_confirmado_responden_RejectionNotConfirmed_y_no_anulan_nada()
    {
        var d = await RechazadaAsync(confirmar: false);

        (await ReemplazarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.RejectionNotConfirmed");
        (await CancelarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.RejectionNotConfirmed");
        (await BorradorAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.RejectionNotConfirmed");

        await _fuente.DidNotReceiveWithAnyArgs().AnularSinEfectoFiscalAsync(default, default, default!, default);
        (await RecargarAsync(d)).Status.Should().Be(ElectronicDocumentStatus.Rejected);
    }

    [Fact]
    public async Task Sobre_Sent_los_tres_casos_responden_AwaitingResponse()
    {
        var d = await EnviadaAsync();

        (await CorregirAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.AwaitingResponse");
        (await ReemplazarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.AwaitingResponse");
        (await CancelarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.AwaitingResponse");
    }

    [Fact]
    public async Task Sobre_Validated_los_tres_casos_responden_NotRejected()
    {
        var d = await ValidadaAsync();

        (await CorregirAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.NotRejected");
        (await ReemplazarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.NotRejected");
        (await CancelarAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.NotRejected");
    }

    [Fact]
    public async Task Sobre_Pending_sin_respuesta_responde_NotRejected()
    {
        var d = await _s.FacturaAsync();
        (await CorregirAsync(d)).Error.Code.Should().Be("ElectronicInvoicing.Document.NotRejected");
    }

    [Fact]
    public async Task Un_documento_que_no_existe_responde_NotFound()
    {
        var r = await new CancelRejectedDocumentCommandHandler(_s.E.Db, Casos(), _persona, _s.E.Reloj)
            .Handle(new CancelRejectedDocumentCommand(Guid.NewGuid(), "x"), default);
        r.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.NotFoundCode);
    }

    [Fact]
    public async Task b_y_c_exigen_el_permiso_de_confirmar_de_la_clase()
    {
        var d = await RechazadaAsync(confirmar: true);
        _permisos.HasPermissionAsync(PermisoVentas, Arg.Any<CancellationToken>()).Returns(false);

        var b = await ReemplazarAsync(d);
        var c = await CancelarAsync(d);

        b.Error.Code.Should().Be("ElectronicInvoicing.Document.ClassPermissionRequired");
        System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)b.Error).Data).Should().Contain(PermisoVentas);
        c.Error.Code.Should().Be("ElectronicInvoicing.Document.ClassPermissionRequired");
        await _fuente.DidNotReceiveWithAnyArgs().AnularSinEfectoFiscalAsync(default, default, default!, default);
    }

    [Fact]
    public async Task El_rechazo_de_una_consulta_que_no_encontro_el_documento_tambien_confirma()
    {
        var d = await _s.FacturaAsync();
        _s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.InvalidData, false, false));
        await _s.Emitir().Handle(new EmitElectronicDocumentCommand(d.PublicId, false), default);
        _s.Adelantar();
        _s.Canal.Consultas.Enqueue(ResultadoDeCanal.Sin(ChannelOutcome.NotFound));
        await _s.Consultar().Handle(new QueryElectronicDocumentStatusCommand(d.PublicId), default);

        var r = await CancelarAsync(await RecargarAsync(d));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
    }

    // ------------------------------------------------------------------------------------------------------------ caso b --

    [Fact]
    public async Task Caso_b_anula_sin_efecto_fiscal_confirma_el_reemplazo_con_el_mismo_numero_y_deja_la_version_CaseB_en_Pending()
    {
        var d = await RechazadaAsync(confirmar: true);
        var rechazado = d.SourceDocumentPublicId;

        var r = await ReemplazarAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.VoidingDocumentPublicId.Should().Be(_anulacion);
        r.Value.ReplacementDocumentPublicId.Should().Be(_reemplazo);
        r.Value.VersionNumber.Should().Be(2);
        r.Value.Status.Should().Be(ElectronicDocumentStatus.Pending);

        Received.InOrder(() =>
        {
            _fuente.AnularSinEfectoFiscalAsync(rechazado, CasoFiscalDeAnulacion.DianRejectionReplaced, "Se facturó un producto que no era", Arg.Any<CancellationToken>());
            _fuente.ConfirmarReemplazoAsync(rechazado, _reemplazo, Arg.Any<Action<InventoryDocument>>(), Arg.Any<CancellationToken>());
        });
        _numerado!.Prefix.Should().Be("SETP", "el reemplazo toma el número del rechazado");
        _numerado.Number.Should().Be(990000123);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.Pending);
        x.Number.Should().Be("SETP990000123");
        x.Consecutive.Should().Be(990000123);
        x.SourceDocumentPublicId.Should().Be(_reemplazo);
        x.RejectedBy.Should().BeNull();
        var v2 = x.Versions.Single(v => v.VersionNumber == 2);
        v2.Reason.Should().Be(DocumentVersionReason.CaseB);
        v2.SourceDocumentPublicId.Should().Be(_reemplazo);
        x.Versions.Single(v => v.VersionNumber == 1).SourceDocumentPublicId.Should().Be(rechazado, "el original queda en la versión 1");
        x.CurrentVersionId.Should().Be(v2.Id);
    }

    [Fact]
    public async Task Caso_b_con_la_anulacion_rechazada_por_la_fuente_no_toca_el_documento_electronico()
    {
        var d = await RechazadaAsync(confirmar: true);
        _fuente.AnularSinEfectoFiscalAsync(Arg.Any<Guid>(), Arg.Any<CasoFiscalDeAnulacion>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnulacionSinEfectoFiscal>("Inventory.Document.AlreadyVoided", "ya anulado"));

        var r = await ReemplazarAsync(d);

        r.Error.Code.Should().Be("Inventory.Document.AlreadyVoided");
        await _fuente.DidNotReceiveWithAnyArgs().ConfirmarReemplazoAsync(default, default, default!, default);
        (await RecargarAsync(d)).Status.Should().Be(ElectronicDocumentStatus.Rejected);
    }

    [Fact]
    public async Task El_borrador_de_reemplazo_lo_crea_la_fuente_con_el_rechazo_confirmado()
    {
        var d = await RechazadaAsync(confirmar: true);

        var r = await BorradorAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.ReplacementDraftPublicId.Should().Be(_reemplazo);
        r.Value.SourceModule.Should().Be("INV");
        r.Value.EditRoute.Should().Be($"/api/inventory/sales/invoices/{_reemplazo}");
        await _fuente.Received(1).CrearBorradorDeReemplazoAsync(d.SourceDocumentPublicId, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------------------------ caso c --

    [Fact]
    public async Task Caso_c_anula_sin_efecto_fiscal_y_deja_CancelledWithoutReplacement_con_motivo_y_responsable()
    {
        var d = await RechazadaAsync(confirmar: true);

        var r = await CancelarAsync(d);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.VoidingDocumentPublicId.Should().Be(_anulacion);
        r.Value.Status.Should().Be(ElectronicDocumentStatus.CancelledWithoutReplacement);
        await _fuente.Received(1).AnularSinEfectoFiscalAsync(d.SourceDocumentPublicId, CasoFiscalDeAnulacion.DianRejectionCancelled,
            "El cliente desistió de la compra", Arg.Any<CancellationToken>());
        await _fuente.DidNotReceiveWithAnyArgs().ConfirmarReemplazoAsync(default, default, default!, default);

        var x = await RecargarAsync(d);
        x.Status.Should().Be(ElectronicDocumentStatus.CancelledWithoutReplacement);
        x.RejectionReason.Should().Be("El cliente desistió de la compra");
        x.CancelledByUserId.Should().Be(Usuario);
        x.Number.Should().Be("SETP990000123", "el número queda ligado al anulado: no es un hueco");
        x.NextAttemptAt.Should().BeNull("un documento final no vuelve al procesador");
        x.Versions.Should().ContainSingle();
        x.EsFinal.Should().BeTrue();
    }
}
