using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// T110 — US6 (FR-023, FR-024): el cierre del ejercicio exige los doce meses cerrados, el anterior
/// cerrado y la cuenta de resultado definida; genera un <c>CI</c> cuadrado del 31/12 que deja en cero
/// las cuentas de resultado (por sucursal y centro de costo) y lleva el excedente a la cuenta de
/// resultado; reabrir lo reversa en su misma fecha y de su misma clase.
/// </summary>
public class FiscalYearCommandsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Caja { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Gasto { get; }
        public ChartOfAccount Resultado { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Caja = D.Cuenta("11050501");
            // El ingreso exige tercero y sólo lo mueve Nómina: el cierre igual tiene que poder cancelarlo.
            Ingreso = D.Cuenta("41350501", AccountNature.Credit, AccountingModules.Payroll, tercero: true);
            Gasto = D.Cuenta("51100501", centro: true);
            Resultado = D.Cuenta("35050501", AccountNature.Credit);
        }

        public CloseFiscalYearCommandHandler Cerrador() => new(D.Db, D.Clock, D.User, D.Poster, Emisor);
        public ReopenFiscalYearCommandHandler Reabridor() => new(D.Db, D.Clock, D.User, D.Poster, Emisor);

        /// <summary>Ingresos de 1.000 (Principal) y 300 (Norte), gasto de 400 con centro de costo: excedente esperado 900.</summary>
        public async Task MovimientosDelAnioAsync()
        {
            var marzo = new DateOnly(2026, 3, 10);
            await Contabilizar(new PostingRequest("NM", marzo, "Ingreso principal", ContabilidadTestData.Nomina(),
                [new PostingLine { AccountId = Caja.Id, Debit = 1000m, BranchId = D.Principal.Id }, new PostingLine { AccountId = Ingreso.Id, Credit = 1000m, PersonId = D.Tercero.Id, BranchId = D.Principal.Id }]));
            await Contabilizar(new PostingRequest("NM", marzo, "Ingreso norte", ContabilidadTestData.Nomina(),
                [new PostingLine { AccountId = Caja.Id, Debit = 300m, BranchId = D.Norte.Id }, new PostingLine { AccountId = Ingreso.Id, Credit = 300m, PersonId = D.Tercero.Id, BranchId = D.Norte.Id }]));
            await Contabilizar(new PostingRequest("CG", marzo, "Gasto", ContabilidadTestData.Manual(),
                [new PostingLine { AccountId = Gasto.Id, Debit = 400m, CostCenterId = D.Centro.Id }, new PostingLine { AccountId = Caja.Id, Credit = 400m }]));
        }

        private async Task Contabilizar(PostingRequest request)
        {
            var r = await D.Poster.PrepareAsync(request, CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error?.Message);
            await D.Db.SaveChangesAsync();
        }

        public void ConCuentaDeResultado()
        {
            D.Setup.ResultAccountId = Resultado.Id;
            D.Db.SaveChanges();
        }
    }

    [Fact]
    public async Task No_cierra_con_meses_abiertos_y_los_nombra()
    {
        var e = new Escenario();
        e.D.EjercicioCompleto(PeriodStatus.Closed);
        var mayo = await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 5);
        mayo.Status = PeriodStatus.Open;
        var octubre = await e.D.Db.AccountingPeriods.SingleAsync(p => p.Month == 10);
        octubre.Status = PeriodStatus.Open;
        await e.D.Db.SaveChangesAsync();

        var r = await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.FiscalYear.PeriodsOpen");
        r.Error.Should().BeOfType<ErrorConDatos>();
        r.Error.Message.Should().Contain("5, 10");
    }

    [Fact]
    public async Task No_cierra_con_el_anterior_abierto_ni_sin_cuenta_de_resultado()
    {
        var e = new Escenario();
        e.D.EjercicioCompleto();
        e.D.OtroEjercicio(2025, PeriodStatus.Closed);
        var anterior = await e.D.Db.FiscalYears.SingleAsync(f => f.Year == 2025);
        anterior.Status = PeriodStatus.Open;
        await e.D.Db.SaveChangesAsync();

        (await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.PreviousOpen");

        anterior.Status = PeriodStatus.Closed;
        await e.D.Db.SaveChangesAsync();
        (await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.ResultAccountMissing");
        (await e.Cerrador().Handle(new CloseFiscalYearCommand(2031), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.NotFound");
    }

    [Fact]
    public async Task El_cierre_deja_en_cero_las_cuentas_de_resultado_con_un_CI_cuadrado_del_31_de_diciembre()
    {
        var e = new Escenario();
        await e.MovimientosDelAnioAsync();
        e.D.EjercicioCompleto();
        e.ConCuentaDeResultado();

        var r = await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Result.Should().Be(900m, "1.000 + 300 de ingresos menos 400 de gasto");
        r.Value.ClosingDocumentPublicId.Should().NotBeNull();

        var ci = await e.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync(d => d.PublicId == r.Value.ClosingDocumentPublicId);
        ci.VoucherType!.Code.Should().Be("CI");
        ci.Kind.Should().Be(DocumentKind.Closing);
        ci.Status.Should().Be(DocumentStatus.Posted);
        ci.Date.Should().Be(new DateOnly(2026, 12, 31));
        ci.PeriodId.Should().NotBeNull("el 31/12 está en diciembre, cerrado, y aun así el cierre entra");
        ci.TotalDebit.Should().Be(ci.TotalCredit);
        ci.Lines.Should().HaveCount(r.Value.Lines);

        // El ingreso (crédito) se cancela por débito, sucursal por sucursal; el gasto (débito) por crédito, con su centro de costo.
        ci.Lines.Where(l => l.AccountId == e.Ingreso.Id).Select(l => (l.BranchId, l.Debit)).Should().BeEquivalentTo([(e.D.Principal.Id, 1000m), (e.D.Norte.Id, 300m)]);
        ci.Lines.Single(l => l.AccountId == e.Gasto.Id).Should().Match<Domain.Entities.Accounting.Transactions.JournalEntry>(l => l.Credit == 400m && l.CostCenterId == e.D.Centro.Id);
        // La cuenta de resultado recibe el excedente por sucursal: 600 en Principal (1.000 − 400) y 300 en Norte.
        ci.Lines.Where(l => l.AccountId == e.Resultado.Id).Select(l => (l.BranchId, l.Credit)).Should().BeEquivalentTo([(e.D.Principal.Id, 600m), (e.D.Norte.Id, 300m)]);

        // Saldo de cada cuenta de resultado al 31/12 con el cierre: cero.
        var saldoIngreso = await e.D.Db.JournalEntries.Where(j => j.AccountId == e.Ingreso.Id && j.IsPosted).SumAsync(j => j.Credit - j.Debit);
        var saldoGasto = await e.D.Db.JournalEntries.Where(j => j.AccountId == e.Gasto.Id && j.IsPosted).SumAsync(j => j.Debit - j.Credit);
        saldoIngreso.Should().Be(0m);
        saldoGasto.Should().Be(0m);

        var ejercicio = await e.D.Db.FiscalYears.SingleAsync(f => f.Year == 2026);
        ejercicio.Status.Should().Be(PeriodStatus.Closed);
        ejercicio.ClosingDocumentId.Should().Be(ci.Id);
        ejercicio.ClosedBy.Should().Be("contadora@demo");
        (await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.AlreadyClosed");
        await e.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.FiscalYear.Closed"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_resultados_el_ejercicio_cierra_sin_comprobante()
    {
        var e = new Escenario();
        e.D.EjercicioCompleto();
        e.ConCuentaDeResultado();

        var r = await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.ClosingDocumentPublicId.Should().BeNull();
        r.Value.Lines.Should().Be(0);
        (await e.D.Db.FiscalYears.SingleAsync(f => f.Year == 2026)).Status.Should().Be(PeriodStatus.Closed);
    }

    [Fact]
    public async Task Reabrir_reversa_el_cierre_en_su_misma_fecha_y_de_su_misma_clase_y_exige_motivo()
    {
        var e = new Escenario();
        await e.MovimientosDelAnioAsync();
        e.D.EjercicioCompleto();
        e.ConCuentaDeResultado();
        var cierre = await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None);
        cierre.IsSuccess.Should().BeTrue(cierre.Error?.Message);

        (await e.Reabridor().Handle(new ReopenFiscalYearCommand(2027, "x"), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.NotFound");

        var r = await e.Reabridor().Handle(new ReopenFiscalYearCommand(2026, "Faltó la depreciación de diciembre"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var reverso = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == r.Value.ClosingDocumentPublicId);
        reverso.Kind.Should().Be(DocumentKind.Closing, "la reversión del cierre se consulta como cierre: fuera salvo includeClosing");
        reverso.Date.Should().Be(new DateOnly(2026, 12, 31));
        reverso.ReversesDocument.Should().NotBeNull();
        var original = await e.D.Db.AccountingDocuments.SingleAsync(d => d.PublicId == cierre.Value.ClosingDocumentPublicId);
        original.Status.Should().Be(DocumentStatus.Reversed);

        // Con cierre y reverso, el ingreso vuelve a tener su saldo del año.
        (await e.D.Db.JournalEntries.Where(j => j.AccountId == e.Ingreso.Id && j.IsPosted).SumAsync(j => j.Credit - j.Debit)).Should().Be(1300m);

        var ejercicio = await e.D.Db.FiscalYears.SingleAsync(f => f.Year == 2026);
        ejercicio.Status.Should().Be(PeriodStatus.Open);
        ejercicio.ClosingDocumentId.Should().BeNull();
        ejercicio.ReopenReason.Should().Be("Faltó la depreciación de diciembre");
        (await e.D.Db.AccountingPeriods.Where(p => p.FiscalYear!.Year == 2026).AllAsync(p => p.Status == PeriodStatus.Closed)).Should().BeTrue("reabrir el año no reabre los meses");
        (await e.Reabridor().Handle(new ReopenFiscalYearCommand(2026, "otra vez"), CancellationToken.None)).Error.Code.Should().Be("Accounting.FiscalYear.NotClosed");

        // Y se puede volver a cerrar: el segundo CI cancela lo mismo.
        var otraVez = await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None);
        otraVez.IsSuccess.Should().BeTrue(otraVez.Error?.Message);
        otraVez.Value.Result.Should().Be(900m);
    }

    [Fact]
    public async Task Solo_se_reabre_el_ultimo_ejercicio_cerrado()
    {
        var e = new Escenario();
        e.D.EjercicioCompleto();
        e.ConCuentaDeResultado();
        (await e.Cerrador().Handle(new CloseFiscalYearCommand(2026), CancellationToken.None)).IsSuccess.Should().BeTrue();
        e.D.OtroEjercicio(2027, PeriodStatus.Closed);

        var r = await e.Reabridor().Handle(new ReopenFiscalYearCommand(2026, "x"), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.FiscalYear.NotLast");
    }
}
