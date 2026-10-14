using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, T0765 (FR-131; adaptadores-y-archivos.md §8.1–§8.7; api.md §15): la emisión en un paso. Antes de activar
/// <c>Lending.NotActivated</c>; extracto y certificado tributario <c>KindNotAvailable</c> con <c>availableFrom = "K5"</c>; sin firmante
/// <c>SignerMissing</c>; fuera del alcance el 404 genérico. El paz y salvo de una obligación con saldo se niega con la obligación y en cero se emite
/// sujeto a firmeza (registro del pago que canceló + días hábiles de su clase de medio), con el pago en la línea; el general mira las codeudas sólo
/// con el parámetro, se niega con una venta en reproceso y menciona la aceptada sin migrar; el certificado de saldo es la liquidación a (D, R =
/// emisión) con su huella; el plan de pagos, la versión vigente con sus tasas. En un paso: número con el prefijo de la clase, copia de la
/// identificación, código único (una colisión se regenera), PDF subido antes de guardar con su SHA-256, evento <c>Issued</c>, ambiente y firmante
/// sellados; con correo, el mensaje <c>DocumentDelivery</c> con el PDF, exento de ventanas y negado sin correo o sin autorización general; la misma
/// clave devuelve el mismo documento y la emisión se audita.
/// </summary>
public sealed class IssueLendingDocumentCommandHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = Payments.PagosDePrueba.Hoy;

    private DocumentosDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DocumentosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Datos(Application.Common.Models.Error e) => Disbursements.DesembolsoDePrueba.Datos(e);

    // --------------------------------------------------------------------------------------------------------- guardas --

    [Fact]
    public async Task Antes_de_activar_Cartera_no_se_emite_nada()
    {
        var setup = await _d.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        await _d.Db.SaveChangesAsync();

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.NotActivated");
        _d.Subidas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IssuedDocumentKind.Statement)]
    [InlineData(IssuedDocumentKind.TaxCertificate)]
    public async Task El_extracto_y_el_certificado_tributario_llegan_con_K5(IssuedDocumentKind clase)
    {
        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(clase, _d.Ana.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.Document.KindNotAvailable");
        Datos(r.Error).GetProperty("availableFrom").GetString().Should().Be("K5");
        r.Error.Message.Should().Be("Este documento estará disponible en una entrega posterior de Cartera.");
    }

    [Fact]
    public async Task Sin_el_firmante_configurado_es_SignerMissing()
    {
        _d.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.DocumentosFirmanteCargo, ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None,
            Value = "", ValidFrom = new DateOnly(2026, 6, 1), Reason = "prueba", CreatedAt = _d.G.UtcNow.AddDays(-5),
        });
        await _d.Db.SaveChangesAsync();

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.Document.SignerMissing");
    }

    [Fact]
    public async Task Fuera_del_alcance_de_oficina_o_linea_es_el_404_generico_y_queda_auditado()
    {
        _d.G.AlcanceActual = AlcanceDeCartera.Vacio;

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.Loan.NotFound");
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingAccessOutOfScope);
        (await _d.Db.IssuedLendingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_titular_tiene_que_estar_obligado_en_el_credito()
    {
        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Beto.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.Document.HolderNotObligated");
    }

    // ----------------------------------------------------------------------------------------------------- paz y salvo --

    [Fact]
    public async Task El_paz_y_salvo_de_una_obligacion_con_saldo_se_niega_con_la_obligacion()
    {
        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.LoanClearance, _d.Ana.PublicId, _d.Credito.PublicId));

        r.Error.Code.Should().Be("Lending.Document.ClearanceDenied");
        var obligacion = Datos(r.Error).GetProperty("obligations")[0];
        obligacion.GetProperty("number").GetString().Should().Be(_d.Credito.Number);
        obligacion.GetProperty("balance").GetDecimal().Should().Be(3_000_000m);
        r.Error.Message.Should().StartWith("No se puede emitir el paz y salvo: ").And.Contain(_d.Credito.Number);
        _d.Subidas.Should().BeEmpty("se niega antes de pintar nada");
    }

    [Fact]
    public async Task En_cero_se_emite_sujeto_a_firmeza_desde_el_registro_del_pago_que_cancelo()
    {
        var pago = await _d.CancelarAsync();
        var firmeza = await _d.P.Firmeza().CalcularAsync(ClaseDeMedioDeRecaudo.Cash, Hoy, default);

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.LoanClearance, _d.Ana.PublicId, _d.Credito.PublicId));

        firmeza.Should().BeAfter(Hoy, "el efectivo tiene días hábiles de firmeza");
        emitido.Status.Should().Be(nameof(IssuedDocumentStatus.SubjectToFinality));
        emitido.FinalityUntil.Should().Be(firmeza);
        emitido.Number.Should().Be("PS-000001");
        emitido.VerificationCode.Should().MatchRegex("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$");
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.FinalityUntil.Should().Be(firmeza);
        var linea = await _d.Db.IssuedDocumentLoans.AsNoTracking().SingleAsync(x => x.IssuedDocumentId == documento.Id);
        linea.LoanId.Should().Be(_d.Credito.Id);
        linea.BalanceAtCutoff.Should().Be(0m);
        linea.PayoffOperationId.Should().Be((await _d.P.OperacionAsync(pago)).Id);
        _d.Pintados.Single().Encabezado.Leyendas.Should().Contain(l => l.StartsWith("La cancelación está sujeta a firmeza hasta el", StringComparison.Ordinal));

        documento.EstadoA(false, firmeza).Should().Be(IssuedDocumentStatus.Valid, "el paso a vigente lo da la fecha");
    }

    [Fact]
    public async Task El_general_mira_las_codeudas_solo_con_el_parametro_y_lo_dice()
    {
        _d.Db.LoanParties.Add(new LoanParty { LoanId = _d.Credito.Id, PersonId = _d.Beto.Id, Role = CreditPartyRole.CoDebtor, JoinedOn = Hoy });
        await _d.Db.SaveChangesAsync();

        var sinCodeudas = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Beto.PublicId));

        sinCodeudas.Number.Should().Be("PSG-000001");
        (await _d.DocumentoAsync(sinCodeudas.DocumentPublicId)).CoversCoDebts.Should().BeFalse();
        var cuerpo = _d.Pintados.Single().Cuerpo.Should().BeOfType<CuerpoDePazYSalvo>().Subject;
        cuerpo.General.Should().BeTrue();
        cuerpo.Creditos.Should().BeEmpty();
        _d.Pintados.Single().Encabezado.Leyendas.Should().Contain(CuerpoDePazYSalvo.LeyendaDeAlcance)
            .And.Contain(ModelosDeDocumentoDeCartera.LeyendaSinCodeudas);

        await _d.ParametroAsync(ParametrosDeCartera.DocumentosPazYSalvoIncluyeCodeudas, "true");
        var conCodeudas = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Beto.PublicId));

        conCodeudas.Error.Code.Should().Be("Lending.Document.ClearanceDenied");
        Datos(conCodeudas.Error).GetProperty("obligations")[0].GetProperty("number").GetString().Should().Be(_d.Credito.Number);
    }

    [Fact]
    public async Task Una_venta_en_reproceso_niega_el_general_con_su_factura()
    {
        await _d.CancelarAsync();
        var mensaje = Guid.NewGuid();
        _d.Db.LendingInboundMessages.Add(new LendingInboundMessage
        {
            MessagePublicId = mensaje, MessageType = VentaACreditoRegistradaV1.Type, MessageVersion = 2, OriginModule = "INV", OriginPublicId = Guid.NewGuid(),
            OriginNumber = "FV-000777", OriginEventKey = "venta", ChainRootPublicId = Guid.NewGuid(), OperationDate = Hoy, PersonId = _d.Ana.Id,
            Status = InboundMessageStatus.Rejected, FirstReceivedAt = _d.G.UtcNow, LastProcessedAt = _d.G.UtcNow,
        });
        await _d.Db.SaveChangesAsync();

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Ana.PublicId));

        r.Error.Code.Should().Be("Lending.Document.ClearanceDenied");
        var venta = Datos(r.Error).GetProperty("pendingSales")[0];
        venta.GetProperty("invoice").GetString().Should().Be("FV-000777");
        venta.GetProperty("messagePublicId").GetGuid().Should().Be(mensaje);
        r.Error.Message.Should().Contain("FV-000777");
    }

    [Fact]
    public async Task Una_venta_aceptada_sin_migrar_no_lo_niega_y_queda_como_fuera_de_Cartera()
    {
        await _d.CancelarAsync();
        _d.Db.LendingActivations.Add(new LendingActivation
        {
            MigrationLoadId = 41, CutoffDate = Hoy.AddDays(-31), ActivationDate = Hoy.AddDays(-30), RegisteredByUserId = 1, RegisteredAt = _d.G.UtcNow.AddDays(-30),
            SessionItemId = 1, CheckVersion = 2, BalanceCheckSha256 = new string('a', 64), SalesReconciliationSha256 = new string('b', 64),
        });
        _d.Db.MigrationSaleReconciliations.Add(new MigrationSaleReconciliation
        {
            MigrationLoadId = 41, CheckVersion = 2, MessagePublicId = Guid.NewGuid(), SaleDocumentPublicId = Guid.NewGuid(), InvoiceNumber = "FV-000123",
            CreditPaymentPublicId = Guid.NewGuid(), SaleDate = Hoy.AddDays(-60), Amount = 450_000m, PersonId = _d.Ana.Id,
            Outcome = SaleReconciliationOutcome.OutsideLendingD29,
        });
        await _d.Db.SaveChangesAsync();

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Ana.PublicId));

        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.OutsideLendingNote.Should().Contain("FV-000123");
        var cuerpo = _d.Pintados.Single().Cuerpo.Should().BeOfType<CuerpoDePazYSalvo>().Subject;
        cuerpo.VentasFueraDeCartera.Should().ContainSingle(v => v.Factura == "FV-000123");
        cuerpo.Creditos.Should().ContainSingle(c => c.Numero == _d.Credito.Number && c.Saldo == 0m);
    }

    // ------------------------------------------------------------------------------------- certificado de saldo y plan --

    [Fact]
    public async Task El_certificado_de_saldo_es_la_liquidacion_a_D_con_R_la_emision_y_su_huella()
    {
        var d = Hoy.AddDays(15);

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.BalanceCertificate, _d.Ana.PublicId, _d.Credito.PublicId, d));

        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.AsOfDate.Should().Be(d);
        documento.InformationCutoff.Should().BeOnOrAfter(_d.G.UtcNow, "R es la emisión, nunca antes del último registro del crédito");
        documento.RulesVersion.Should().Be("LND-2026.1");
        var liquidacion = await _d.P.C.Liquidar().Handle(new GetLoanSettlementQuery(_d.Credito.PublicId, d, documento.InformationCutoff), default);
        ("sha256:" + documento.SettlementSha256).Should().Be(liquidacion.Value.Fingerprint, "la huella de la liquidación a (D, R) queda sellada (T24)");
        var cuerpo = _d.Pintados.Single().Cuerpo.Should().BeOfType<CuerpoDeCertificadoDeSaldo>().Subject;
        cuerpo.ParaCancelar.Should().Be(liquidacion.Value.ToPayOff);
        cuerpo.Proyectada.Should().BeTrue("D es futura: se proyecta sin pagos");
        cuerpo.ValidaHasta.Should().NotBeNull().And.Subject!.Value.Should().BeOnOrBefore(d);
        emitido.Number.Should().Be("CS-000001");
        JsonDocument.Parse(documento.EssentialDataJson).RootElement.GetProperty("toPayOff").GetDecimal().Should().Be(liquidacion.Value.ToPayOff);
    }

    [Fact]
    public async Task El_plan_de_pagos_es_la_version_vigente_con_sus_tasas_efectiva_y_nominal()
    {
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        emitido.Number.Should().Be("PP-000001");
        emitido.Status.Should().Be(nameof(IssuedDocumentStatus.Valid));
        var cuerpo = _d.Pintados.Single().Cuerpo.Should().BeOfType<CuerpoDePlanDePagos>().Subject;
        cuerpo.NumeroDelCredito.Should().Be(_d.Credito.Number);
        cuerpo.VersionDelPlan.Should().Be(1);
        cuerpo.Cuotas.Should().Be(24);
        cuerpo.Filas.Should().HaveCount(24);
        cuerpo.TasaEfectivaAnual.Should().Be(0.20m);
        cuerpo.TasaNominalAnual.Should().BeGreaterThan(0m).And.BeLessThan(0.20m, "la nominal mensual vencida es menor que la efectiva");
    }

    // ------------------------------------------------------------------------------------------------------- un paso --

    [Fact]
    public async Task En_un_paso_numera_copia_la_identificacion_sube_el_pdf_antes_y_sella_todo()
    {
        _d.Ana.IdType = "C";
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.HolderTaxId.Should().Be(_d.Ana.TaxId);
        documento.HolderTaxIdType.Should().Be(_d.Ana.IdType);
        documento.HolderName.Should().Contain("Ana");
        documento.SignerName.Should().Be("María Gerente");
        documento.SignerTitle.Should().Be("Gerente general");
        documento.IsTestEnvironment.Should().BeTrue("fuera de producción el documento no tiene validez");
        documento.IssuedByUserId.Should().Be(_d.D.Cajera.Id);
        documento.DeliveryMode.Should().Be(DocumentDeliveryMode.Download);
        documento.VerificationCode.Should().HaveLength(16);
        CodigoDeVerificacion.Formatear(documento.VerificationCode).Should().Be(emitido.VerificationCode);

        var subida = _d.Subidas.Should().ContainSingle().Subject;
        subida.OwnerEntityType.Should().Be(AdjuntosDeModulo.DocumentoEmitidoDeCartera).And.Be("LendingIssuedDocument");
        subida.OwnerEntityPublicId.Should().Be(documento.PublicId);
        subida.ContentType.Should().Be("application/pdf");
        documento.ContentSha256.Should().Be(Convert.ToHexString(SHA256.HashData(subida.Content)).ToLowerInvariant());
        (await _d.Db.Attachments.AsNoTracking().SingleAsync(a => a.PublicId == documento.PdfAttachmentPublicId)).OwnerEntityPublicId.Should().Be(documento.PublicId);
        AdjuntosDeModulo.De(AdjuntosDeModulo.DocumentoEmitidoDeCartera)!.Borrable.Should().BeFalse();

        var encabezado = _d.Pintados.Single().Encabezado;
        encabezado.Numero.Should().Be(documento.Number);
        encabezado.Cooperativa.Should().Be("Cooperativa de Prueba");
        encabezado.NitDeLaCooperativa.Should().Be("900123456-7");
        encabezado.UrlDeVerificacion.Should().Be($"https://erp.coop.test/cartera/verificar?codigo={emitido.VerificationCode}");
        encabezado.Leyendas.Should().Contain(EncabezadoDeDocumentoDeCartera.MarcaDePruebas);

        var evento = (await _d.EventosAsync(documento.PublicId)).Should().ContainSingle().Subject;
        evento.Kind.Should().Be(IssuedDocumentEventKind.Issued);
        evento.ActorUserId.Should().Be(_d.D.Cajera.Id);
        emitido.Download.Should().NotBeNull();
        emitido.Download!.Url.Should().StartWith("https://almacen.test/");
        emitido.DebtorMessagePublicId.Should().BeNull();
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingDocumentIssued);
    }

    [Fact]
    public async Task En_produccion_no_lleva_la_marca_de_pruebas()
    {
        _d.EnProduccion = true;
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        (await _d.DocumentoAsync(emitido.DocumentPublicId)).IsTestEnvironment.Should().BeFalse();
        _d.Pintados.Single().Encabezado.Leyendas.Should().NotContain(EncabezadoDeDocumentoDeCartera.MarcaDePruebas);
    }

    [Fact]
    public async Task Una_colision_del_codigo_se_regenera_antes_de_guardar()
    {
        _d.CodigosAEntregar.Enqueue("7K2MQ9XD4HTPB1RW");
        var primero = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));
        _d.CodigosAEntregar.Enqueue("7K2MQ9XD4HTPB1RW");
        _d.CodigosAEntregar.Enqueue("0000000000000ZZZ");

        var segundo = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        primero.VerificationCode.Should().Be("7K2M-Q9XD-4HTP-B1RW");
        segundo.VerificationCode.Should().Be("0000-0000-0000-0ZZZ");
        segundo.Number.Should().Be("PP-000002", "cada clase lleva su consecutivo");
    }

    [Fact]
    public async Task Si_el_almacen_falla_no_queda_documento()
    {
        _d.FallaLaSubida = true;

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));

        r.IsFailure.Should().BeTrue();
        (await _d.Db.IssuedLendingDocuments.CountAsync()).Should().Be(0);
        (await _d.Db.IssuedDocumentEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_misma_clave_devuelve_el_mismo_documento()
    {
        var pedido = DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId);

        var primero = await _d.ConClaveAsync(pedido);
        var otraVez = await _d.ConClaveAsync(pedido);

        primero.IsSuccess.Should().BeTrue(primero.IsFailure ? primero.Error.Message : string.Empty);
        otraVez.Value.DocumentPublicId.Should().Be(primero.Value.DocumentPublicId);
        (await _d.Db.IssuedLendingDocuments.CountAsync()).Should().Be(1);
        _d.Subidas.Should().ContainSingle();
    }

    // ---------------------------------------------------------------------------------------------------------- correo --

    [Fact]
    public async Task Con_correo_nace_el_mensaje_DocumentDelivery_con_el_pdf_exento_de_ventanas()
    {
        _d.G.UtcNow = new DateTime(2026, 12, 2, 2, 30, 0, DateTimeKind.Utc); // 9:30 p. m. del martes en Colombia: fuera de la franja de cobranza.

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId,
            entrega: DocumentDeliveryMode.Email));

        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.DeliveryMode.Should().Be(DocumentDeliveryMode.Email);
        emitido.Download.Should().BeNull();
        var mensaje = await _d.Db.DebtorMessages.AsNoTracking().SingleAsync();
        mensaje.PublicId.Should().Be(emitido.DebtorMessagePublicId!.Value);
        mensaje.Purpose.Should().Be(DebtorMessagePurpose.DocumentDelivery);
        mensaje.Channel.Should().Be(MessageChannel.Email);
        mensaje.OriginKind.Should().Be(EntregaDeDocumentosDeCartera.Origen);
        mensaje.AttachmentPublicId.Should().Be(documento.PdfAttachmentPublicId);
        mensaje.IssuedDocumentId.Should().Be(documento.Id);
        mensaje.RenderedBody.Should().Contain(emitido.VerificationCode).And.Contain("Plan de pagos");
        var evento = await _d.Db.DebtorMessageEvents.AsNoTracking().SingleAsync(e => e.DebtorMessageId == mensaje.Id);
        evento.Kind.Should().Be(DebtorMessageEventKind.Queued, "la entrega pedida por el titular está exenta del horario (I3)");
    }

    [Fact]
    public async Task Sin_correo_registrado_no_se_envia_ni_se_pinta()
    {
        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.GeneralClearance, _d.Beto.PublicId, entrega: DocumentDeliveryMode.Email));

        r.Error.Code.Should().Be("Lending.DebtorMessage.ChannelNotAuthorized");
        Datos(r.Error).GetProperty("reason").GetString().Should().Be("NoDestination");
        _d.Subidas.Should().BeEmpty();
        _d.Pintados.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_autorizacion_de_tratamiento_general_no_se_envia()
    {
        _d.D.O.QuitarAutorizacion(_d.Ana, DataAuthorizationPurpose.GeneralTreatment);

        var r = await _d.EmitirAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId,
            entrega: DocumentDeliveryMode.Email));

        r.Error.Code.Should().Be("Lending.DebtorMessage.ChannelNotAuthorized");
        _d.Subidas.Should().BeEmpty();
        (await _d.Db.IssuedLendingDocuments.CountAsync()).Should().Be(0);
    }
}
