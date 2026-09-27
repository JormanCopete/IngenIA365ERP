using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// T071 — US3: cerrar un mes exige que no queden borradores fechados en él (y los lista);
/// reabrir pide motivo, queda registrado y desactualiza las conciliaciones cerradas; abrir el
/// ejercicio siguiente crea sus doce meses.
/// </summary>
public class PeriodCommandsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public ISender Sender { get; } = Substitute.For<ISender>();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
        }

        /// <summary>Un mensaje de Inventario con su entrega a Contabilidad en el estado pedido (feature 012, I2).</summary>
        public void Entrega(DateOnly fecha, DeliveryStatus estado, string tipo = "AJ", string destino = IntegrationDestinations.Accounting)
        {
            var mensaje = new IntegrationMessage
            {
                Type = "AjusteRegistrado", Kind = IntegrationMessageKind.Business, OriginModule = "INV", OriginKind = MessageOriginKind.Document,
                OriginPublicId = Guid.NewGuid(), OriginDocumentTypeCode = tipo, OriginEventKey = Guid.NewGuid().ToString("N"),
                ChainRootPublicId = Guid.NewGuid(), OperationDate = fecha, BranchPublicId = Guid.NewGuid(), PayloadJson = "{}", PayloadSha256 = "x",
                OriginUserCentralId = Guid.NewGuid(), OriginUserName = "bodega@demo", EmittedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
            };
            D.Db.IntegrationMessages.Add(mensaje);
            D.Db.SaveChanges();
            D.Db.IntegrationMessageDeliveries.Add(new IntegrationMessageDelivery
            {
                MessageId = mensaje.Id, Destination = destino, Mode = DeliveryMode.Online, Status = estado, CreatedBy = "test",
            });
            D.Db.SaveChanges();
        }

        public ClosePeriodCommandHandler Cerrador() => new(D.Db, D.Clock, D.User, Emisor);
        public ReopenPeriodCommandHandler Reabridor() => new(D.Db, D.Clock, D.User, Emisor);
        public ListPeriodsQueryHandler Listador() => new(D.Db);

        public async Task<Guid> BorradorEnMarzoAsync()
        {
            var caja = D.Cuenta("110505");
            var bancos = D.Cuenta("111005");
            var r = await new SaveDraftDocumentCommandHandler(D.Db, D.Clock, D.User, D.Alcance, D.Poster).Handle(
                new SaveDraftDocumentCommand(null, "CG", ContabilidadTestData.Marzo15, "Pendiente",
                    [new LineaDeBorradorInput(caja.Code, null, null, null, null, null, 100m, 0m, null, null), new LineaDeBorradorInput(bancos.Code, null, null, null, null, null, 0m, 100m, null, null)]),
                CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error?.Message);
            return r.Value.PublicId;
        }
    }

    [Fact]
    public async Task Cerrar_un_mes_con_borradores_falla_y_los_lista()
    {
        var e = new Escenario();
        var borrador = await e.BorradorEnMarzoAsync();

        var r = await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Period.HasDrafts");
        r.Error.Should().BeOfType<ErrorConDatos>();
        r.Error.Message.Should().Contain("Pendiente");
        var ejercicio = await e.Listador().Handle(new ListPeriodsQuery(2026), CancellationToken.None);
        ejercicio.Value.Periods.Single(p => p.Month == 3).Should().Match<PeriodoContableDto>(p => p.Status == "Open" && p.Drafts == 1);
        borrador.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Cerrar_y_reabrir_quedan_registrados_y_la_reapertura_desactualiza_conciliaciones()
    {
        var e = new Escenario();
        var marzo = await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 3);
        var cuenta = e.D.Cuenta("111005");
        e.D.Db.BankReconciliations.Add(new BankReconciliation { AccountId = cuenta.Id, PeriodId = marzo.Id, Status = ReconciliationStatus.Closed, CreatedBy = "test" });
        await e.D.Db.SaveChangesAsync();

        var cierre = await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3), CancellationToken.None);
        cierre.IsSuccess.Should().BeTrue(cierre.Error?.Message);
        marzo = await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 3);
        marzo.Status.Should().Be(PeriodStatus.Closed);
        marzo.ClosedBy.Should().Be("contadora@demo");
        (await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3), CancellationToken.None)).Error.Code.Should().Be("Accounting.Period.AlreadyClosed");

        var sinMotivo = await e.Reabridor().Handle(new ReopenPeriodCommand(2026, 4, "x"), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Accounting.Period.NotClosed");

        var reapertura = await e.Reabridor().Handle(new ReopenPeriodCommand(2026, 3, "Llegó una factura tardía"), CancellationToken.None);
        reapertura.IsSuccess.Should().BeTrue(reapertura.Error?.Message);
        marzo = await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 3);
        marzo.Status.Should().Be(PeriodStatus.Open);
        marzo.ReopenReason.Should().Be("Llegó una factura tardía");
        (await e.D.Db.BankReconciliations.SingleAsync()).Status.Should().Be(ReconciliationStatus.Outdated);
    }

    [Fact]
    public async Task Abrir_el_ejercicio_siguiente_crea_doce_meses_y_no_se_repite()
    {
        var e = new Escenario();
        e.Sender.Send(Arg.Any<ListPeriodsQuery>(), Arg.Any<CancellationToken>()).Returns(ci => e.Listador().Handle(ci.Arg<ListPeriodsQuery>(), CancellationToken.None));
        var abridor = new OpenFiscalYearCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor, e.Sender);

        var r = await abridor.Handle(new OpenFiscalYearCommand(2027), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Year.Should().Be(2027);
        r.Value.Periods.Should().HaveCount(12).And.OnlyContain(p => p.Status == "Open");
        (await abridor.Handle(new OpenFiscalYearCommand(2027), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.AlreadyExists");
        (await abridor.Handle(new OpenFiscalYearCommand(2030), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.NotFound");
    }

    // ---- feature 012, I2 (T459; contracts/contabilidad.md §8): aviso de mensajes de Inventario antes del cierre ----

    [Fact]
    public async Task Cerrar_con_mensajes_de_inventario_pendientes_exige_reconocerlos()
    {
        var e = new Escenario();
        e.Entrega(new DateOnly(2026, 3, 5), DeliveryStatus.Pending, "AJ");
        e.Entrega(new DateOnly(2026, 3, 2), DeliveryStatus.InBatch, "TR");
        e.Entrega(new DateOnly(2026, 3, 9), DeliveryStatus.Rejected, "AJ");
        e.Entrega(new DateOnly(2026, 3, 9), DeliveryStatus.InBatch, "AJ");
        // No cuentan: lo que no pasa, lo ya procesado, otro mes y otro destino.
        e.Entrega(new DateOnly(2026, 3, 1), DeliveryStatus.NotApplicable, "CO");
        e.Entrega(new DateOnly(2026, 3, 1), DeliveryStatus.Processed, "CO");
        e.Entrega(new DateOnly(2026, 4, 1), DeliveryStatus.Pending, "CO");
        e.Entrega(new DateOnly(2026, 3, 1), DeliveryStatus.Pending, "CO", IntegrationDestinations.Lending);

        var sinReconocer = await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3), CancellationToken.None);

        sinReconocer.IsFailure.Should().BeTrue();
        sinReconocer.Error.Code.Should().Be("Accounting.Period.InventoryPending");
        var datos = sinReconocer.Error.Should().BeOfType<ErrorConDatos>().Which.Data;
        System.Text.Json.JsonSerializer.Serialize(datos, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            .Should().Be("""{"pending":1,"inBatch":2,"rejected":1,"oldestOperationDate":"2026-03-02","types":["AJ","TR"]}""");
        sinReconocer.Error.Message.Should().Contain("4");
        (await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 3)).Status.Should().Be(PeriodStatus.Open);

        AuditEventDocument? evento = null;
        e.Auditoria.AppendAsync(Arg.Do<AuditEventDocument>(d => evento = d), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var reconocido = await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3, AcknowledgeInventoryPending: true), CancellationToken.None);

        reconocido.IsSuccess.Should().BeTrue(reconocido.Error?.Message);
        (await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 3)).Status.Should().Be(PeriodStatus.Closed);
        evento.Should().NotBeNull();
        evento!.Action.Should().Be("Accounting.Period.Closed");
        evento.NewValuesJson.Should().Contain("\"inventoryPendingAcknowledged\":true").And.Contain("\"pending\":1").And.Contain("\"rejected\":1");
    }

    [Fact]
    public async Task Lo_que_no_pasa_a_contabilidad_no_estorba_el_cierre()
    {
        var e = new Escenario();
        e.Entrega(new DateOnly(2026, 3, 5), DeliveryStatus.NotApplicable);
        e.Entrega(new DateOnly(2026, 3, 5), DeliveryStatus.Processed);

        var r = await e.Cerrador().Handle(new ClosePeriodCommand(2026, 3), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
    }
}
