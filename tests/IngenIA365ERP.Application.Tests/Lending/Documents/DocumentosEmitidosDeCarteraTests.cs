using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, T0766 (FR-132, D21; api.md §15) y T0787 (adaptadores-y-archivos.md §8.8): la verificación normaliza el código y responde
/// auténtico con su estado, clase, número, fecha, titular enmascarado y datos esenciales —anulado, con motivo y fecha— y se audita; un código de
/// otra base o inexistente es <c>Lending.Document.NotFound</c>; la reimpresión es el enlace del <b>mismo</b> PDF, auditada, y un anulado no se
/// reimprime; la anulación manual exige motivo y una segunda es <c>AlreadyVoided</c>; un anulado no se envía; cuando el mensaje de un documento
/// queda <c>Sent</c> el documento recibe <c>Delivered</c>; la lista filtra y respeta el alcance; y el reverso del pago que canceló anula, en su misma
/// unidad de trabajo, el paz y salvo emitido después del pago y no el emitido antes.
/// </summary>
public sealed class DocumentosEmitidosDeCarteraTests : IAsyncLifetime
{
    private DocumentosDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DocumentosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private Task<IssuedLendingDocumentDto> PlanAsync(DocumentDeliveryMode entrega = DocumentDeliveryMode.Download) =>
        _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId, entrega: entrega));

    private async Task AnularAsync(Guid documento, string motivo = "Se entregó con un error de digitación")
    {
        var r = await _d.Anular().Handle(new VoidLendingDocumentCommand(documento, motivo) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _d.Db.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------------------------------------------ verificar --

    [Fact]
    public async Task La_verificacion_normaliza_el_codigo_responde_lo_esencial_y_se_audita()
    {
        var emitido = await PlanAsync();
        var digitado = "  " + emitido.VerificationCode.ToLowerInvariant().Replace('-', ' ') + " ";

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(digitado), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var v = r.Value;
        v.Authentic.Should().BeTrue();
        v.Status.Should().Be(nameof(IssuedDocumentStatus.Valid));
        v.Kind.Should().Be(nameof(IssuedDocumentKind.PaymentPlan));
        v.Number.Should().Be(emitido.Number);
        v.Holder.FullName.Should().Contain("Ana");
        v.Holder.DocumentNumberMasked.Should().EndWith(_d.Ana.TaxId[^4..]).And.StartWith("*").And.NotContain(_d.Ana.TaxId);
        v.EssentialData.GetProperty("loan").GetString().Should().Be(_d.Credito.Number);
        v.Voided.Should().BeNull();
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingDocumentVerified);
    }

    [Fact]
    public async Task Un_anulado_se_verifica_como_anulado_con_motivo_y_fecha()
    {
        var emitido = await PlanAsync();
        await AnularAsync(emitido.DocumentPublicId, "Se emitió a la persona equivocada");

        var v = (await _d.Verificar().Handle(new VerifyLendingDocumentQuery(emitido.VerificationCode), default)).Value;

        v.Authentic.Should().BeTrue();
        v.Status.Should().Be(nameof(IssuedDocumentStatus.Voided));
        v.Voided!.Reason.Should().Be("Se emitió a la persona equivocada");
        v.Voided.At.Should().Be(_d.G.UtcNow);
    }

    [Theory]
    [InlineData("0000-0000-0000-0000")]
    [InlineData("no es un código")]
    public async Task Un_codigo_inexistente_o_de_otra_cooperativa_es_NotFound(string codigo)
    {
        await PlanAsync();

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(codigo), default);

        r.Error.Code.Should().Be("Lending.Document.NotFound");
        r.Error.Message.Should().Be("El código no corresponde a un documento de esta cooperativa.");
    }

    // ------------------------------------------------------------------------------------------- reimprimir y anular --

    [Fact]
    public async Task La_reimpresion_es_el_enlace_del_mismo_pdf_guardado_y_se_audita()
    {
        var emitido = await PlanAsync();
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        var adjunto = await _d.Db.Attachments.AsNoTracking().SingleAsync(a => a.PublicId == documento.PdfAttachmentPublicId);

        var r = await _d.Enlace().Handle(new IssueLendingDocumentDownloadLinkCommand(emitido.DocumentPublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Direct.Should().BeTrue();
        r.Value.Url.Should().Be($"https://almacen.test/{adjunto.StoragePath}", "se descarga el PDF guardado, no uno pintado de nuevo");
        _d.Pintados.Should().ContainSingle("reimprimir no vuelve a pintar");
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingDocumentReprinted);
    }

    [Fact]
    public async Task Un_anulado_no_se_reimprime_ni_se_envia()
    {
        var emitido = await PlanAsync();
        await AnularAsync(emitido.DocumentPublicId);

        var enlace = await _d.Enlace().Handle(new IssueLendingDocumentDownloadLinkCommand(emitido.DocumentPublicId), default);
        var envio = await _d.Enviar().Handle(new SendLendingDocumentCommand(emitido.DocumentPublicId) { OperationKey = Guid.NewGuid() }, default);

        enlace.Error.Code.Should().Be("Lending.Document.VoidedCannotReissue");
        envio.Error.Code.Should().Be("Lending.Document.VoidedCannotReissue");
        enlace.Error.Message.Should().Contain("emita uno nuevo");
    }

    [Fact]
    public async Task La_anulacion_exige_motivo_y_no_se_repite()
    {
        var emitido = await PlanAsync();
        new VoidLendingDocumentCommandValidator().Validate(new VoidLendingDocumentCommand(emitido.DocumentPublicId, " ")).IsValid.Should().BeFalse();
        await AnularAsync(emitido.DocumentPublicId);

        var otra = await _d.Anular().Handle(new VoidLendingDocumentCommand(emitido.DocumentPublicId, "otra vez") { OperationKey = Guid.NewGuid() }, default);

        otra.Error.Code.Should().Be("Lending.Document.AlreadyVoided");
        var eventos = await _d.EventosAsync(emitido.DocumentPublicId);
        eventos.Should().ContainSingle(e => e.Kind == IssuedDocumentEventKind.Voided).Which.ActorUserId.Should().Be(_d.D.Cajera.Id);
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingDocumentVoided);
    }

    [Fact]
    public async Task Un_documento_inexistente_o_fuera_del_alcance_es_NotFound()
    {
        var emitido = await PlanAsync();
        (await _d.Detalle().Handle(new GetIssuedLendingDocumentQuery(Guid.NewGuid()), default)).Error.Code.Should().Be("Lending.Document.NotFound");

        _d.G.AlcanceActual = AlcanceDeCartera.Vacio;
        var fuera = await _d.Detalle().Handle(new GetIssuedLendingDocumentQuery(emitido.DocumentPublicId), default);

        fuera.Error.Code.Should().Be("Lending.Document.NotFound");
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingAccessOutOfScope);
    }

    // ----------------------------------------------------------------------------------------------------- entregar --

    [Fact]
    public async Task Enviar_despues_encola_el_mismo_pdf_y_al_quedar_Sent_el_documento_recibe_Delivered()
    {
        var emitido = await PlanAsync();
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);

        var r = await _d.Enviar().Handle(new SendLendingDocumentCommand(emitido.DocumentPublicId) { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var mensaje = await _d.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.PublicId == r.Value.DebtorMessagePublicId);
        mensaje.AttachmentPublicId.Should().Be(documento.PdfAttachmentPublicId);
        mensaje.IssuedDocumentId.Should().Be(documento.Id);

        var registro = new RegisterDebtorMessageAttemptCommandHandler(_d.Db, _d.Cola(), _d.D.Parametros(), _d.D.Alertas, _d.G.Reloj,
            NullLogger<RegisterDebtorMessageAttemptCommandHandler>.Instance);
        var enviado = await registro.Handle(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, 1, PasoDeDespacho.Envio)
        {
            Outcome = ExternalOutcome.Ok, Destino = _d.Ana.Email, ReferenciaExterna = "CORREO-1",
        }, default);
        _d.Db.ChangeTracker.Clear();

        enviado.Value.Should().Be(DebtorMessageEventKind.Sent);
        var entrega = (await _d.EventosAsync(emitido.DocumentPublicId)).Should().ContainSingle(e => e.Kind == IssuedDocumentEventKind.Delivered).Subject;
        entrega.DebtorMessageId.Should().Be(mensaje.Id);
        var detalle = (await _d.Detalle().Handle(new GetIssuedLendingDocumentQuery(emitido.DocumentPublicId), default)).Value;
        detalle.Events.Select(e => e.Kind).Should().Equal(nameof(IssuedDocumentEventKind.Issued), nameof(IssuedDocumentEventKind.Delivered));
        detalle.Events[1].DebtorMessagePublicId.Should().Be(mensaje.PublicId);
    }

    [Fact]
    public async Task El_paz_y_salvo_general_solo_se_envia_al_titular()
    {
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Beto.PublicId));

        var r = await _d.Enviar().Handle(new SendLendingDocumentCommand(emitido.DocumentPublicId, _d.Ana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.RecipientNotObligated");
    }

    // -------------------------------------------------------------------------------------------------------- listar --

    [Fact]
    public async Task La_lista_filtra_por_clase_persona_credito_estado_y_fechas_y_respeta_el_alcance()
    {
        var plan = await PlanAsync();
        var general = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Beto.PublicId));
        await AnularAsync(plan.DocumentPublicId);
        var hoy = _d.G.HoyLocal;

        async Task<IReadOnlyList<Guid>> ListarAsync(ListIssuedLendingDocumentsQuery q) =>
            (await _d.Listar().Handle(q, default)).Value.Items.Select(i => i.PublicId).ToList();

        (await ListarAsync(new())).Should().BeEquivalentTo([plan.DocumentPublicId, general.DocumentPublicId]);
        (await ListarAsync(new(Kind: IssuedDocumentKind.PaymentPlan))).Should().Equal(plan.DocumentPublicId);
        (await ListarAsync(new(PersonPublicId: _d.Beto.PublicId))).Should().Equal(general.DocumentPublicId);
        (await ListarAsync(new(LoanPublicId: _d.Credito.PublicId))).Should().Equal(plan.DocumentPublicId);
        (await ListarAsync(new(Status: IssuedDocumentStatus.Voided))).Should().Equal(plan.DocumentPublicId);
        (await ListarAsync(new(Status: IssuedDocumentStatus.Valid))).Should().Equal(general.DocumentPublicId);
        (await ListarAsync(new(From: hoy, To: hoy))).Should().HaveCount(2);
        (await ListarAsync(new(From: hoy.AddDays(1)))).Should().BeEmpty();

        var fila = (await _d.Listar().Handle(new(Kind: IssuedDocumentKind.PaymentPlan), default)).Value.Items.Single();
        fila.Status.Should().Be(nameof(IssuedDocumentStatus.Voided));
        fila.Loans.Should().ContainSingle(l => l.LoanPublicId == _d.Credito.PublicId);
        fila.Holder.PersonPublicId.Should().Be(_d.Ana.PublicId);

        _d.G.AlcanceActual = AlcanceDeCartera.Vacio;
        (await ListarAsync(new())).Should().Equal([general.DocumentPublicId], "el del crédito de otra oficina no aparece; el general sin créditos, sí");
    }

    // -------------------------------------------------------------------------------------------- anulación por reverso --

    [Fact]
    public async Task El_reverso_del_pago_que_cancelo_anula_el_paz_y_salvo_emitido_despues_y_no_el_anterior()
    {
        var anterior = await PlanAsync();
        _d.G.UtcNow = _d.G.UtcNow.AddMinutes(5);
        var pago = await _d.CancelarAsync();
        _d.G.UtcNow = _d.G.UtcNow.AddMinutes(5);
        var pazYSalvo = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.LoanClearance, _d.Ana.PublicId, _d.Credito.PublicId));

        var vista = (await _d.P.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_d.Credito.PublicId, pago), default)).Value;
        vista.DocumentsToVoid.Select(x => x.Number).Should().Equal(pazYSalvo.Number);
        var r = await _d.P.Reversar().Handle(new ReverseLoanPaymentCommand(_d.Credito.PublicId, pago, "Cheque devuelto", vista.PreviewSha256)
            { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.VoidedDocuments.Should().Equal(pazYSalvo.DocumentPublicId);
        var anulacion = (await _d.EventosAsync(pazYSalvo.DocumentPublicId)).Should().ContainSingle(e => e.Kind == IssuedDocumentEventKind.Voided).Subject;
        var reverso = await _d.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.PaymentReversal);
        anulacion.CausingOperationId.Should().Be(reverso.Id);
        anulacion.Reason.Should().StartWith("Se reversó el pago").And.Contain(_d.Credito.Number);
        (await _d.EventosAsync(anterior.DocumentPublicId)).Should().NotContain(e => e.Kind == IssuedDocumentEventKind.Voided,
            "emitido antes del pago, no lo conocía");

        var v = (await _d.Verificar().Handle(new VerifyLendingDocumentQuery(pazYSalvo.VerificationCode), default)).Value;
        v.Status.Should().Be(nameof(IssuedDocumentStatus.Voided));
        v.Voided!.Reason.Should().StartWith("Se reversó el pago");
    }

    [Fact]
    public async Task El_reverso_del_desembolso_anula_lo_emitido_desde_su_registro()
    {
        var plan = await PlanAsync();
        var desembolso = await _d.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.Disbursement);
        var reverso = new LoanOperation
        {
            LoanId = _d.Credito.Id, Kind = LoanOperationKind.DisbursementReversal, Number = "RD-1", RegisteredAt = _d.G.UtcNow.AddMinutes(1),
            ReversesOperationId = desembolso.Id, ActorKind = ActorKind.Process,
        };
        _d.Db.LoanOperations.Add(reverso);
        await _d.Db.SaveChangesAsync();

        var anulados = await new AnulacionDeDocumentosPorReverso(_d.Db).AnularAsync(_d.Credito.Id, desembolso, reverso, default);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        anulados.Should().Equal(plan.DocumentPublicId);
        var evento = (await _d.EventosAsync(plan.DocumentPublicId)).Single(e => e.Kind == IssuedDocumentEventKind.Voided);
        evento.Reason.Should().StartWith("Se reversó el desembolso");
        evento.ActorKind.Should().Be(ActorKind.Process);
        (await new AnulacionDeDocumentosPorReverso(_d.Db).AnularAsync(_d.Credito.Id, desembolso, reverso, default)).Should().BeEmpty(
            "un documento ya anulado no se anula dos veces");
    }
}
