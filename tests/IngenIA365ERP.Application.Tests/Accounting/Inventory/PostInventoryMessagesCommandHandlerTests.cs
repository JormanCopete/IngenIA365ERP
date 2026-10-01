using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T453 (T512; contracts/contabilidad.md §3.1): el consumidor por documento, una fila por fila de la tabla de §3.1.
/// Comprobante y recibos en un guardado; recibo existente o colisión del índice → <c>AlreadyProcessed</c> con la misma
/// referencia; versión, moneda, contenido, matriz, tipo y reglas de la 009 → <c>Rejected</c> con su código y las líneas del
/// documento; original pendiente → <c>Retry</c>; informativo y valor cero → recibo sin comprobante. El comprobante se fecha en la
/// operación, es <c>Regular</c>, su origen es el documento, lo registra el usuario de origen y el actor es el de
/// <c>IActorActual</c>. El consumidor nunca escribe tablas de la plataforma.
/// </summary>
public class PostInventoryMessagesCommandHandlerTests
{
    private readonly EscenarioContable E = new();

    private PostInventoryMessagesCommandHandler Handler() => new(
        E.D.Db, new MensajesEntrantes(E.D.Db), new ConsumoDeInventario(E.D.Db, E.ActorActual, E.D.Clock, E.Emisor), E.D.Poster,
        new ResolutorDeReglas(E.D.Db), new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock);

    private async Task<ResultadoDeConsumo> ConsumirAsync(IReadOnlyList<MensajeDeUnidad> unidad, Guid? lote = null)
    {
        var r = await Handler().Handle(new PostInventoryMessagesCommand(unidad.Select(m => m.Sobre.MessageId).ToList(), lote), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        E.D.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task Procesa_la_unidad_en_un_comprobante_con_sus_recibos()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("05-nota-credito-con-devolucion-cruza-con-la-venta"));
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        var procesado = r.Should().BeOfType<ResultadoDeConsumo.Processed>().Subject;
        procesado.VoucherTypeCode.Should().Be("NV");
        procesado.VoucherNumber.Should().Be("1");
        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync();
        documento.PublicId.Should().Be(procesado.AccountingDocumentPublicId!.Value);
        documento.Date.Should().Be(new DateOnly(2026, 3, 14), "la fecha de operación, nunca la de hoy");
        documento.Kind.Should().Be(DocumentKind.Regular);
        documento.Status.Should().Be(Domain.Enums.Accounting.DocumentStatus.Posted);
        documento.OriginModule.Should().Be("INV");
        documento.SourceType.Should().Be(OrigenesDeInventario.Documento);
        documento.SourcePublicId.Should().Be(unidad[0].Sobre.Origin.PublicId);
        documento.RegisteredBy.Should().Be("Usuario de ensayo", "el usuario de origen es dato (FR-083)");
        documento.Lines.Should().HaveCount(5);

        var recibos = await E.D.Db.InventoryPostings.OrderBy(p => p.Id).ToListAsync();
        recibos.Select(p => p.MessagePublicId).Should().Equal(unidad.Select(m => m.Sobre.MessageId));
        recibos.Should().OnlyContain(p => p.AccountingDocumentId == documento.Id && p.NoVoucherReason == null
            && p.ActorKind == ActorKind.Process && p.ActorName == "Proceso de integración" && p.OriginUserName == "Usuario de ensayo"
            && p.RelatedDocumentPublicId == unidad[0].Sobre.Related!.PublicId && p.OperationDate == new DateOnly(2026, 3, 14));

        (await E.D.Db.IntegrationMessageDeliveries.AllAsync(d => d.Status == DeliveryStatus.Pending && d.Attempts == 0 && d.ResultReference == null))
            .Should().BeTrue("el consumidor no toca tablas de la plataforma: lo registra el despachador");
        (await E.D.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_mensaje_ya_procesado_devuelve_la_misma_referencia_sin_segundo_comprobante()
    {
        var unidad = EscenarioContable.Compra(1000m);
        E.Emitir(unidad);
        var primero = (ResultadoDeConsumo.Processed)await ConsumirAsync(unidad);

        var segundo = await ConsumirAsync(unidad);

        segundo.Should().Be(new ResultadoDeConsumo.AlreadyProcessed(primero.AccountingDocumentPublicId, "EI", "1"));
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
        (await E.D.Db.InventoryPostings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void La_colision_del_indice_del_recibo_se_reconoce()
    {
        var choque = new DbUpdateException("fallo", new InvalidOperationException(
            "duplicate key value violates unique constraint \"UK_ACC_InventoryPostings_MessagePublicId\""));
        ConsumoDeInventario.EsColisionDelRecibo(choque).Should().BeTrue();
        ConsumoDeInventario.EsColisionDelRecibo(new DbUpdateException("otro", new InvalidOperationException("UK_ACC_Documents_Type_Number"))).Should().BeFalse();
    }

    [Fact]
    public async Task Una_version_no_aceptada_se_rechaza()
    {
        var unidad = EscenarioContable.Compra(1000m);
        E.Emitir(unidad[0], version: 2);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Integration.VersionNotAccepted");
        (await E.D.Db.InventoryPostings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Otra_moneda_se_rechaza()
    {
        var unidad = EscenarioContable.Compra(1000m);
        E.Emitir(unidad[0], moneda: "USD");

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.InventoryMessage.CurrencyNotSupported");
    }

    [Fact]
    public async Task Un_contenido_que_no_cuadra_se_rechaza_como_defecto_del_emisor()
    {
        var caso = EscenarioContable.Caso("06-nota-debito-un-impuesto-por-renglon");
        caso["mensajes"]![0]!["payload"]!["totals"]!["amountDue"] = 1.00m;
        var unidad = EscenarioContable.Unidad(caso);
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.InventoryMessage.Unbalanced");
    }

    [Fact]
    public async Task Sin_regla_se_rechaza_con_las_lineas_del_documento()
    {
        var unidad = EscenarioContable.Compra(1000m, "CARNES");
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        var rechazo = r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;
        rechazo.Code.Should().Be("Accounting.InventoryRule.Missing");
        rechazo.DataJson.Should().Contain("\"documentLines\":[1]").And.Contain("CompraRecibida");
        E.Auditoria.ReceivedCalls().Should().NotBeEmpty("el rechazo queda auditado (Accounting.Inventory.Rejected)");
    }

    [Fact]
    public async Task Sin_mapeo_de_tipo_de_comprobante_se_rechaza()
    {
        E.D.Db.InventoryVoucherMappings.RemoveRange(E.D.Db.InventoryVoucherMappings.Where(m => m.Operation == "Compra"));
        E.D.Db.SaveChanges();
        var unidad = EscenarioContable.Compra(1000m);
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.VoucherType.NotFound");
    }

    [Fact]
    public async Task Un_periodo_cerrado_se_rechaza_con_el_codigo_de_la_009()
    {
        var unidad = EscenarioContable.Compra(1000m, fecha: new DateOnly(2026, 2, 10));
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.Period.Closed");
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await E.D.Db.VoucherTypes.SingleAsync(v => v.Code == "EI")).NextNumber.Should().Be(1, "un rechazo no consume número");
    }

    [Fact]
    public async Task Una_regla_de_la_009_se_traduce_a_las_lineas_del_documento()
    {
        // Sin proveedor en el sobre, la mercancía por facturar (exige tercero) no tiene tercero: regla 7 de la 009.
        var unidad = EscenarioContable.Compra(1000m, persona: Guid.Empty);
        unidad = [unidad[0] with { Sobre = unidad[0].Sobre with { PersonPublicId = null } }];
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        var rechazo = r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;
        rechazo.Code.Should().Be("Accounting.Line.ThirdPartyRequired");
        rechazo.DataJson.Should().Contain("\"documentLines\":[1]").And.Contain("\"accountCode\":\"22050501\"");
    }

    [Fact]
    public async Task Un_original_sin_contabilizar_hace_esperar()
    {
        var original = EscenarioContable.Compra(1000m, numero: "REC-9");
        E.Emitir(original);
        var devolucion = new DevolucionRegistradaV1
        {
            Operation = "DevolucionAProveedor",
            Lines = [new CostLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", Movement = KardexEntryKind.Exit, Cost = 500m, DocumentLines = [1] }],
        };
        var relacionado = new DocumentRefV1 { PublicId = original[0].Sobre.Origin.PublicId, DocumentClass = DocumentClass.PurchaseReceipt, Number = "REC-9" };
        var sobre = EscenarioContable.Sobre(DevolucionRegistradaV1.Type, "SupplierReturn", "DPR", "DP-9", persona: EscenarioContable.Proveedor, relacionado: relacionado);
        var unidad = new[] { new MensajeDeUnidad(sobre with { Payload = devolucion }, devolucion) };
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        var retry = r.Should().BeOfType<ResultadoDeConsumo.Retry>().Subject;
        retry.Code.Should().Be("Accounting.InventoryMessage.WaitingForOriginal");

        await ConsumirAsync(original);
        (await ConsumirAsync(unidad)).Should().BeOfType<ResultadoDeConsumo.Processed>("contabilizado el original, sigue");
    }

    [Fact]
    public async Task Un_informativo_deja_su_recibo_sin_comprobante()
    {
        var saldo = new SaldoInicialCargadoV1
        {
            CutoffDate = new DateOnly(2026, 3, 1),
            Lines = [new CostLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", Movement = KardexEntryKind.Entry, Cost = 5000m, DocumentLines = [1] }],
        };
        var sobre = EscenarioContable.Sobre(SaldoInicialCargadoV1.Type, "OpeningBalance", "SIN", "SI-1", kind: IntegrationMessageKind.Informational);
        var unidad = new[] { new MensajeDeUnidad(sobre with { Payload = saldo }, saldo) };
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().Be(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
        var recibo = await E.D.Db.InventoryPostings.SingleAsync();
        recibo.AccountingDocumentId.Should().BeNull();
        recibo.NoVoucherReason.Should().Be(InventoryPosting.SinComprobanteInformativo);
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_documento_de_valor_cero_deja_su_recibo_sin_comprobante()
    {
        var unidad = EscenarioContable.Compra(0m);
        E.Emitir(unidad);

        var r = await ConsumirAsync(unidad);

        r.Should().Be(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.ZeroValue));
        (await E.D.Db.InventoryPostings.SingleAsync()).NoVoucherReason.Should().Be(InventoryPosting.SinComprobanteValorCero);
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_lote_queda_en_el_recibo()
    {
        var lote = E.Lote();
        var unidad = EscenarioContable.Compra(1000m);
        E.Emitir(unidad, DeliveryStatus.InBatch, lote);

        await ConsumirAsync(unidad, lote.PublicId);

        (await E.D.Db.InventoryPostings.SingleAsync()).BatchPublicId.Should().Be(lote.PublicId);
    }

    [Fact]
    public async Task El_validador_exige_mensajes()
    {
        var v = new PostInventoryMessagesCommandValidator();
        (await v.ValidateAsync(new PostInventoryMessagesCommand([], null))).IsValid.Should().BeFalse();
        (await v.ValidateAsync(new PostInventoryMessagesCommand([Guid.NewGuid()], Guid.Empty))).IsValid.Should().BeFalse();
        (await v.ValidateAsync(new PostInventoryMessagesCommand([Guid.NewGuid()], null))).IsValid.Should().BeTrue();
    }
}
