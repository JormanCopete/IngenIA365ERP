using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
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

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
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
}
