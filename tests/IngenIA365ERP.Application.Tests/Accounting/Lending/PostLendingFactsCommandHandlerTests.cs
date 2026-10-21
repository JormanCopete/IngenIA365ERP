using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0616 (T0647; contracts/contabilidad.md §7.1 y §7.2): el consumidor por unidad de los hechos de Cartera, un caso por fila de la
/// tabla de §7.2. Comprobante y un recibo por hecho en un guardado, del tipo del hecho principal (C01: el pago con sus acompañantes en un RC;
/// con recaudador Payroll, DN); recibo existente o colisión del índice → <c>AlreadyProcessed</c>; versión, moneda, cuadre, regla, tipo y
/// período cerrado → <c>Rejected</c>; original pendiente y período inexistente → <c>Retry</c>; informativo, valor cero y mismas cuentas →
/// recibo sin comprobante. El comprobante se fecha en la fecha contable, es <c>Regular</c>, su origen es <c>LendingIssuedDocument</c> (C114) y
/// lo registra el usuario de origen, o nadie si lo originó el proceso (C23). El consumidor nunca escribe tablas de la plataforma.
/// </summary>
public class PostLendingFactsCommandHandlerTests
{
    private readonly EscenarioContableDeCartera E = new();

    private async Task<ResultadoDeConsumo> ConsumirAsync(IReadOnlyList<MensajeEntrante> unidad, Guid? lote = null)
    {
        var r = await E.PorUnidad().Handle(new PostLendingFactsCommand(unidad.Select(m => m.MessageId).ToList(), lote), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        E.D.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private static IReadOnlyList<MensajeEntrante> Caso(string nombre, Action<JsonObject>? cambio = null)
    {
        var caso = EscenarioContableDeCartera.Caso(nombre);
        cambio?.Invoke(caso);
        return EscenarioContableDeCartera.Unidad(caso);
    }

    [Fact]
    public async Task El_pago_con_sus_acompanantes_sale_en_un_RC_con_un_recibo_por_hecho_en_un_guardado()
    {
        var unidad = Caso("05-pago-con-anticipo-suspenso-y-saldo-a-favor-en-un-RC");
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        var procesado = r.Should().BeOfType<ResultadoDeConsumo.Processed>().Subject;
        procesado.VoucherTypeCode.Should().Be("RC", "el tipo de la unidad lo da el hecho principal (C01)");
        procesado.VoucherNumber.Should().NotBeNullOrEmpty();
        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync();
        documento.PublicId.Should().Be(procesado.AccountingDocumentPublicId!.Value);
        documento.Date.Should().Be(new DateOnly(2026, 11, 21), "la fecha contable del hecho, nunca la de hoy");
        documento.Kind.Should().Be(DocumentKind.Regular);
        documento.Status.Should().Be(DocumentStatus.Posted);
        documento.OriginModule.Should().Be("CAR");
        documento.SourceType.Should().Be(OrigenesDeCartera.Documento).And.Be("LendingIssuedDocument", "C114: no LendingDocument");
        documento.SourcePublicId.Should().Be(unidad[0].Envelope.Origin.PublicId);
        documento.RegisteredBy.Should().Be("Usuario de ensayo", "el usuario de origen es dato (§7.3)");
        documento.Description.Should().Be("Aplicación de pago PAG-000990 · crédito 000200");
        documento.Lines.Should().HaveCount(7);

        var recibos = await E.D.Db.LendingPostings.OrderBy(p => p.Id).ToListAsync();
        recibos.Select(p => p.MessagePublicId).Should().BeEquivalentTo(unidad.Select(m => m.MessageId));
        recibos.Should().OnlyContain(p => p.AccountingDocumentId == documento.Id && p.NoVoucherReason == null && p.SourceModule == "CAR"
            && p.LoanNumber == "000200" && p.OperationDate == new DateOnly(2026, 11, 21) && p.OriginActorKind == ActorKind.Person
            && p.OriginUserName == "Usuario de ensayo" && p.ActorKind == ActorKind.Process && p.ActorName == "Proceso de integración"
            && p.SourceDocumentClass == "PaymentApplication");

        (await E.D.Db.IntegrationMessageDeliveries.AllAsync(d => d.Status == DeliveryStatus.Pending && d.Attempts == 0 && d.ResultReference == null))
            .Should().BeTrue("el consumidor no toca tablas de la plataforma: lo registra el despachador");
        (await E.D.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Lo_que_recauda_Nomina_va_en_DN()
    {
        var unidad = Caso("08-descuento-de-la-nomina-propia-en-DN");
        E.Emitir(unidad);

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Processed>().Which.VoucherTypeCode.Should().Be("DN");
    }

    [Fact]
    public async Task Un_hecho_que_origino_el_proceso_no_tiene_registrado_por_propio()
    {
        var unidad = Caso("03-causacion-del-dia");
        E.Emitir(unidad);

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Processed>();

        var documento = await E.D.Db.AccountingDocuments.SingleAsync();
        documento.RegisteredBy.Should().Be(E.D.User.UserName, "sin usuario de origen, el comprobante lo registra el actor que contabiliza (C23)");
        var recibo = await E.D.Db.LendingPostings.SingleAsync();
        recibo.OriginActorKind.Should().Be(ActorKind.Process);
        recibo.OriginUserCentralId.Should().BeNull();
        recibo.LoanNumber.Should().BeNull("una parte reúne varios créditos");
    }

    [Fact]
    public async Task Un_hecho_ya_procesado_devuelve_la_misma_referencia_sin_segundo_comprobante()
    {
        var unidad = Caso("01-desembolso-por-transferencia");
        E.Emitir(unidad);
        var primero = (ResultadoDeConsumo.Processed)await ConsumirAsync(unidad);

        var segundo = await ConsumirAsync(unidad);

        segundo.Should().Be(new ResultadoDeConsumo.AlreadyProcessed(primero.AccountingDocumentPublicId, "DS", primero.VoucherNumber));
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void La_colision_del_indice_del_recibo_se_reconoce()
    {
        var choque = new DbUpdateException("fallo", new InvalidOperationException(
            "duplicate key value violates unique constraint \"UK_ACC_LendingPostings_MessagePublicId\""));
        ConsumoDeCartera.EsColisionDelRecibo(choque).Should().BeTrue();
        ConsumoDeCartera.EsColisionDelRecibo(new DbUpdateException("otro", new InvalidOperationException("UK_ACC_InventoryPostings_MessagePublicId")))
            .Should().BeFalse("el índice de Inventario es de otro consumidor");
    }

    [Fact]
    public async Task Una_version_no_aceptada_se_rechaza()
    {
        var unidad = Caso("01-desembolso-por-transferencia");
        E.Emitir(unidad[0], version: 2);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Integration.VersionNotAccepted");
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(0);
        await E.Auditoria.Received().AppendAsync(Arg.Is<IngenIA365ERP.Application.Common.Interfaces.Audit.AuditEventDocument>(a => a.Action == "Accounting.Lending.Rejected"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otra_moneda_se_rechaza()
    {
        var unidad = Caso("01-desembolso-por-transferencia");
        E.Emitir(unidad[0], moneda: "USD");

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.LendingFact.CurrencyNotSupported");
    }

    [Fact]
    public async Task Un_importe_con_mas_de_dos_decimales_es_defecto_del_emisor()
    {
        var unidad = Caso("01-desembolso-por-transferencia", c => c["mensajes"]![0]!["payload"]!["amounts"]![0]!["amount"] = 10000000.005m);
        E.Emitir(unidad);

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.LendingFact.Unbalanced");
    }

    [Fact]
    public async Task Sin_regla_se_rechaza_con_el_hecho_el_rol_y_el_credito()
    {
        var unidad = Caso("01-desembolso-por-transferencia", c => c["mensajes"]![0]!["payload"]!["dimensions"]!["riskCategory"] = "B");
        E.Emitir(unidad);

        var r = (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;

        r.Code.Should().Be("Accounting.LendingRule.Missing");
        r.DataJson.Should().Contain("\"factType\":\"CreditoDesembolsado\"").And.Contain("\"role\":\"Capital\"").And.Contain("000123");
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_mapeo_de_tipo_de_comprobante_se_rechaza()
    {
        var mapeo = await E.D.Db.LendingVoucherMappings.SingleAsync(m => m.MappingKey == "CreditoDesembolsado|*");
        E.D.Db.LendingVoucherMappings.Remove(mapeo);
        await E.D.Db.SaveChangesAsync();
        var unidad = Caso("01-desembolso-por-transferencia");
        E.Emitir(unidad);

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.VoucherType.NotFound");
    }

    [Fact]
    public async Task Un_mes_cerrado_en_Contabilidad_se_rechaza()
    {
        var noviembre = await E.D.Db.AccountingPeriods.SingleAsync(p => p.StartDate == new DateOnly(2026, 11, 1));
        noviembre.Status = PeriodStatus.Closed;
        await E.D.Db.SaveChangesAsync();
        var unidad = Caso("01-desembolso-por-transferencia");
        E.Emitir(unidad);

        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.Period.Closed");
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_mes_que_no_existe_es_reintento_con_alerta_al_que_abre_ejercicios()
    {
        var unidad = EscenarioContableDeCartera.Copia(Caso("01-desembolso-por-transferencia"), fecha: new DateOnly(2027, 1, 15));
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Retry>().Which.Code.Should().Be("Accounting.Period.NotFound");
        await E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.PeriodoInexistente
            && a.DedupKey == "Contabilidad.PeriodoInexistente:2027-01"), Arg.Any<CancellationToken>());
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.InBatch)]
    [InlineData(DeliveryStatus.Rejected)]
    public async Task Un_reverso_espera_a_su_original_pendiente(DeliveryStatus estadoDelOriginal)
    {
        var reverso = Caso("07-reverso-de-pago-que-restituye-en-C");
        var original = EscenarioContableDeCartera.Copia(Caso("04-pago-por-caja-con-anticipo"), reverso[0].Envelope.Related!.PublicId);
        E.Emitir(original, estadoDelOriginal);
        E.Emitir(reverso);

        var r = await ConsumirAsync(reverso);

        r.Should().BeOfType<ResultadoDeConsumo.Retry>().Which.Code.Should().Be("Accounting.LendingFact.WaitingForOriginal");
    }

    [Fact]
    public async Task Un_original_que_no_pasa_no_retiene_al_reverso()
    {
        var reverso = Caso("07-reverso-de-pago-que-restituye-en-C");
        var original = EscenarioContableDeCartera.Copia(Caso("04-pago-por-caja-con-anticipo"), reverso[0].Envelope.Related!.PublicId);
        E.Emitir(original, DeliveryStatus.NotApplicable);
        E.Emitir(reverso);

        var r = await ConsumirAsync(reverso);

        r.Should().BeOfType<ResultadoDeConsumo.Processed>().Which.VoucherTypeCode.Should().Be("RC");
        (await E.D.Db.LendingPostings.SingleAsync()).RelatedMessagePublicId.Should().Be(Guid.Parse("0a0a0033-0000-4000-8000-000000000033"),
            "el recibo enlaza el hecho original que nombra el reverso (§4.3)");
    }

    [Theory]
    [InlineData("45-cartera-activada-informativo", LendingPosting.SinComprobanteInformativo, MotivoSinComprobante.Informational)]
    [InlineData("32-modificacion-sin-efecto-contable", LendingPosting.SinComprobanteValorCero, MotivoSinComprobante.ZeroValue)]
    [InlineData("39-traslado-que-resuelve-las-mismas-cuentas", LendingPosting.SinComprobanteMismasCuentas, MotivoSinComprobante.SameAccounts)]
    public async Task Sin_comprobante_deja_su_recibo_con_el_motivo(string caso, string motivo, MotivoSinComprobante sinComprobante)
    {
        var unidad = Caso(caso);
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().Be(new ResultadoDeConsumo.Processed(null, null, null, sinComprobante));
        var recibo = await E.D.Db.LendingPostings.SingleAsync();
        recibo.AccountingDocumentId.Should().BeNull();
        recibo.NoVoucherReason.Should().Be(motivo);
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await ConsumirAsync(unidad)).Should().Be(new ResultadoDeConsumo.AlreadyProcessed(null, null, null, sinComprobante));
    }

    [Fact]
    public void El_validador_exige_hechos_y_ningun_identificador_vacio()
    {
        var v = new PostLendingFactsCommandValidator();
        v.Validate(new PostLendingFactsCommand([], null)).IsValid.Should().BeFalse();
        v.Validate(new PostLendingFactsCommand([Guid.Empty], null)).IsValid.Should().BeFalse();
        v.Validate(new PostLendingFactsCommand([Guid.NewGuid()], Guid.Empty)).IsValid.Should().BeFalse();
        v.Validate(new PostLendingFactsCommand([Guid.NewGuid()], Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
