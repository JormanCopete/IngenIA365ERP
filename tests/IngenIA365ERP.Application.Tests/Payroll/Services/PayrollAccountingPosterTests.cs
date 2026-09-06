using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>T104 — FR-019..FR-022, FR-032: el asiento NM se arma por concepto y centro de costo, cuadra, excluye lo que no afecta contabilidad y se reversa espejo.</summary>
public class PayrollAccountingPosterTests
{
    private static readonly DateOnly FinDeMarzo = new(2026, 3, 31);

    private sealed record Calculada(NominaTestData D, PayrollRun Run, List<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> Empleados);

    private static async Task<Calculada> Calcular(bool periodoContableAbierto = true)
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad(periodoContableAbierto);
        d.Db.CostCenters.Add(new CostCenter { LegacyCode = "02", Name = "Comercial", CreatedBy = "test" });
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        bruno.CostCenterId = "02";
        await d.Db.SaveChangesAsync();

        var calc = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var r = await calc.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);

        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == r.Value.RunPublicId);
        var filas = await d.Db.PayrollRunEmployees.Where(e => e.PayrollRunId == run.Id).ToListAsync();
        var empleados = new List<(PayrollRunEmployee, Employee, IReadOnlyList<PayrollRunLine>)>();
        foreach (var f in filas)
        {
            var emp = await d.Db.Employees.SingleAsync(e => e.Id == f.EmployeeId);
            var lineas = await d.Db.PayrollRunLines.Where(l => l.PayrollRunEmployeeId == f.Id).OrderBy(l => l.Order).ToListAsync();
            empleados.Add((f, emp, lineas));
        }
        return new Calculada(d, run, empleados);
    }

    [Fact]
    public async Task Agrupa_por_concepto_y_centro_de_costo_y_cuadra()
    {
        var c = await Calcular();
        var cc01 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "01");
        var cc02 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "02");

        var r = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var (doc, asientos) = r.Value;
        doc.VoucherTypeCode.Should().Be("NM");
        doc.DocumentNumber.Should().Be(1);
        doc.ModuleCode.Should().Be("NOM");
        doc.PeriodCode.Should().Be(202603);
        doc.TotalDebit.Should().Be(doc.TotalCredit).And.BeGreaterThan(0m);
        asientos.Sum(a => a.DebitAmount).Should().Be(asientos.Sum(a => a.CreditAmount));

        // Un débito y un crédito por (concepto, centro de costo): SALARIO aparece en 01 (Ana) y en 02 (Bruno), por separado.
        var salario = asientos.Where(a => a.Description == "Nómina SALARIO").ToList();
        salario.Should().HaveCount(4);
        salario.Where(a => a.CostCenterId == cc01.Id && a.DebitAmount > 0).Should().ContainSingle().Which.DebitAmount.Should().Be(2_000_000m);
        salario.Where(a => a.CostCenterId == cc02.Id && a.DebitAmount > 0).Should().ContainSingle().Which.DebitAmount.Should().Be(3_000_000m);

        // Y el total contabilizado es exactamente la suma de las líneas que afectan contabilidad.
        var esperado = c.Empleados.SelectMany(e => e.Lines).Where(l => l.AffectsAccounting).Sum(l => l.Amount);
        doc.TotalDebit.Should().Be(esperado);
        asientos.Should().OnlyContain(a => a.TransactionDate == FinDeMarzo && a.VoucherTypeCode == "NM" && a.DocumentNumber == 1);
    }

    [Fact]
    public async Task Las_lineas_que_no_afectan_contabilidad_quedan_fuera()
    {
        var c = await Calcular();
        var lineaSalud = c.Empleados[0].Lines.Single(l => l.ConceptCode == "SALUD_EMP");
        lineaSalud.AffectsAccounting = false;

        var r = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var (doc, asientos) = r.Value;
        var esperado = c.Empleados.SelectMany(e => e.Lines).Where(l => l.AffectsAccounting).Sum(l => l.Amount);
        doc.TotalDebit.Should().Be(esperado);
        // La salud del otro empleado sí se contabiliza; la de éste no: el grupo SALUD_EMP suma sólo una.
        var salud = asientos.Where(a => a.Description == "Nómina SALUD_EMP" && a.DebitAmount > 0).ToList();
        salud.Should().ContainSingle();
        salud[0].DebitAmount.Should().Be(c.Empleados[1].Lines.Single(l => l.ConceptCode == "SALUD_EMP").Amount);
    }

    [Fact]
    public async Task Un_concepto_sin_cuentas_impide_contabilizar_y_lo_nombra()
    {
        var c = await Calcular();
        var cuentas = await c.D.Db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == "SALARIO" || a.ConceptCode == "PENSION_EMP").ToListAsync();
        foreach (var a in cuentas) a.IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptWithoutAccounts");
        r.Error.Message.Should().Contain("PENSION_EMP").And.Contain("SALARIO");
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_periodo_contable_cerrado_impide_contabilizar()
    {
        var c = await Calcular(periodoContableAbierto: false);

        var r = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.AccountingPeriodClosed");
        (await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextSequenceNumber.Should().Be(0, "no consumió numeración");
    }

    [Fact]
    public async Task Sin_tipo_de_comprobante_NM_no_contabiliza()
    {
        var c = await Calcular();
        var nm = await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM");
        nm.IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.VoucherTypeMissing");
    }

    [Fact]
    public async Task La_reversion_es_el_espejo_del_original_y_lo_referencia()
    {
        var c = await Calcular();
        var original = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);
        original.IsSuccess.Should().BeTrue(original.Error.Message);
        await c.D.Db.SaveChangesAsync();

        var r = await c.D.Poster.ReverseAsync(original.Value.Document, FinDeMarzo, "Error en las novedades", CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var (reverso, asientos) = r.Value;
        reverso.DocumentNumber.Should().Be(2);
        reverso.Detail.Should().Contain("Reversión del comprobante NM-1").And.Contain("Error en las novedades");
        reverso.TotalDebit.Should().Be(original.Value.Document.TotalCredit);
        reverso.TotalCredit.Should().Be(original.Value.Document.TotalDebit);
        asientos.Should().HaveCount(original.Value.Entries.Count);
        foreach (var (o, rv) in original.Value.Entries.Zip(asientos))
        {
            rv.AccountId.Should().Be(o.AccountId);
            rv.CostCenterId.Should().Be(o.CostCenterId);
            rv.DebitAmount.Should().Be(o.CreditAmount);
            rv.CreditAmount.Should().Be(o.DebitAmount);
            rv.Description.Should().StartWith("Reversión NM-1");
        }
    }

    [Fact]
    public async Task La_reversion_en_periodo_contable_cerrado_se_niega()
    {
        var c = await Calcular();
        var original = await c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);
        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.AccountingPeriods.SingleAsync()).Status = "C";
        await c.D.Db.SaveChangesAsync();

        var r = await c.D.Poster.ReverseAsync(original.Value.Document, FinDeMarzo, "tarde", CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.AccountingPeriodClosedForReversal");
    }
}
