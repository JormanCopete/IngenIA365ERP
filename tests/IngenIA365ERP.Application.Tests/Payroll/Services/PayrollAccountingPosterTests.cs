using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// T104 (feature 005) adaptada al contrato de la feature 009 (T038): el asiento NM se arma por concepto
/// y centro de costo, cuadra, excluye lo que no afecta contabilidad, pasa por las reglas de cuenta y se
/// reversa espejo con referencia en ambos sentidos.
/// </summary>
public class PayrollAccountingPosterTests
{
    private static readonly DateOnly FinDeMarzo = new(2026, 3, 31);

    private sealed record Calculada(NominaTestData D, PayrollRun Run, List<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> Empleados);

    private static async Task<Calculada> Calcular(bool periodoContableAbierto = true, bool conCuotasDeCartera = false)
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad(periodoContableAbierto);
        if (conCuotasDeCartera)
        {
            // Dos cuotas de Ana: 150.000 cabe entera; 2.500.000 sólo en parte (lo no descontado no tiene asiento).
            var persona = d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId;
            d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, new Guid("00000000-0000-0000-0000-00000000000a"), "A-1", "CONSUMO", 3,
                new DateOnly(2026, 3, 31), 150_000m, 0m, 1));
            d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, new Guid("00000000-0000-0000-0000-000000000001"), "B-1", "CONSUMO", 9,
                new DateOnly(2026, 3, 31), 2_500_000m, 0m, 2));
        }
        var comercial = new CostCenter { LegacyCode = "02", Name = "Comercial", CreatedBy = "test" };
        d.Db.CostCenters.Add(comercial);
        await d.Db.SaveChangesAsync();
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        bruno.CostCenterRefId = comercial.Id;
        await d.Db.SaveChangesAsync();

        var calc = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
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

    private static Task<IngenIA365ERP.Application.Common.Models.Result<AccountingDocument>> Contabilizar(Calculada c) =>
        c.D.Poster.PostAsync(c.Run, c.Empleados, FinDeMarzo, "Nómina marzo 2026", CancellationToken.None);

    [Fact]
    public async Task Agrupa_por_concepto_y_cuadra_y_la_cuenta_decide_si_conserva_el_centro_de_costo()
    {
        var c = await Calcular();
        var cc01 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "01");
        var cc02 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "02");
        // El gasto de SALARIO exige centro de costo; el pasivo no lo maneja (se descarta sin error).
        var cuentasSalario = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALARIO");
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalario.DebitAccountId)).RequiresCostCenter = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var doc = r.Value;
        doc.VoucherType!.Code.Should().Be("NM");
        doc.Number.Should().Be(1);
        doc.OriginModule.Should().Be("NOM");
        doc.SourceType.Should().Be("PayrollRun");
        doc.SourcePublicId.Should().Be(c.Run.PublicId);
        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.Kind.Should().Be(DocumentKind.Regular);
        doc.Date.Should().Be(FinDeMarzo);
        doc.PeriodId.Should().NotBeNull();
        doc.TotalDebit.Should().Be(doc.TotalCredit).And.BeGreaterThan(0m);
        var asientos = doc.Lines.ToList();
        asientos.Sum(a => a.Debit).Should().Be(asientos.Sum(a => a.Credit));

        // Un débito y un crédito por (concepto, centro de costo): SALARIO aparece en 01 (Ana) y en 02 (Bruno), por separado.
        var salario = asientos.Where(a => a.Description == "Nómina SALARIO").ToList();
        salario.Should().HaveCount(4);
        salario.Where(a => a.CostCenterId == cc01.Id && a.Debit > 0).Should().ContainSingle().Which.Debit.Should().Be(2_000_000m);
        salario.Where(a => a.CostCenterId == cc02.Id && a.Debit > 0).Should().ContainSingle().Which.Debit.Should().Be(3_000_000m);
        salario.Where(a => a.Credit > 0).Should().OnlyContain(a => a.CostCenterId == null, "el pasivo no maneja centro de costo");

        // Y el total contabilizado es exactamente la suma de las líneas que afectan contabilidad.
        var esperado = c.Empleados.SelectMany(e => e.Lines).Where(l => l.AffectsAccounting).Sum(l => l.Amount);
        doc.TotalDebit.Should().Be(esperado);
        asientos.Should().OnlyContain(a => a.Date == FinDeMarzo && a.IsPosted && a.BranchId == c.D.Principal.Id);
        asientos.Select(a => a.LineNumber).Should().BeEquivalentTo(Enumerable.Range(1, asientos.Count));

        // Nada guardado todavía: el comando de aprobación guarda todo junto.
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.JournalEntries.CountAsync(j => j.DocumentId == doc.Id)).Should().Be(asientos.Count);
        (await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(2);
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalario.DebitAccountId)).FirstMovementAt.Should().Be(FinDeMarzo);
    }

    [Fact]
    public async Task Las_lineas_que_no_afectan_contabilidad_quedan_fuera()
    {
        var c = await Calcular();
        var lineaSalud = c.Empleados[0].Lines.Single(l => l.ConceptCode == "SALUD_EMP");
        lineaSalud.AffectsAccounting = false;

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var esperado = c.Empleados.SelectMany(e => e.Lines).Where(l => l.AffectsAccounting).Sum(l => l.Amount);
        r.Value.TotalDebit.Should().Be(esperado);
        // La salud del otro empleado sí se contabiliza; la de éste no: el grupo SALUD_EMP suma sólo una.
        var salud = r.Value.Lines.Where(a => a.Description == "Nómina SALUD_EMP" && a.Debit > 0).ToList();
        salud.Should().ContainSingle();
        salud[0].Debit.Should().Be(c.Empleados[1].Lines.Single(l => l.ConceptCode == "SALUD_EMP").Amount);
    }

    [Fact]
    public async Task Un_concepto_sin_cuentas_impide_contabilizar_y_lo_nombra()
    {
        var c = await Calcular();
        var cuentas = await c.D.Db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == "SALARIO" || a.ConceptCode == "PENSION_EMP").ToListAsync();
        foreach (var a in cuentas) a.IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Payroll.ConceptWithoutAccounts");
        r.Error.Message.Should().Contain("PENSION_EMP").And.Contain("SALARIO");
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_cuenta_que_no_cumple_las_reglas_impide_contabilizar_y_dice_cual()
    {
        var c = await Calcular();
        var cuentasSalario = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALARIO");
        var gasto = await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalario.DebitAccountId);
        gasto.EnabledModules = AccountingModules.Accounting; // ya no aplica a Nómina
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Document.Invalid", "hay dos líneas de SALARIO (una por centro de costo) con la misma cuenta");
        r.Error.Message.Should().Contain(gasto.Code).And.Contain("Nómina");
        r.Error.Should().BeOfType<IngenIA365ERP.Application.Common.Models.ErrorConDatos>();
        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0, "nada se agrega si una regla falla");
        (await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(1, "no consumió numeración");
    }

    [Fact]
    public async Task Un_periodo_contable_cerrado_impide_contabilizar()
    {
        var c = await Calcular(periodoContableAbierto: false);

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Accounting.Period.Closed");
        (await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(1, "no consumió numeración");
    }

    [Fact]
    public async Task Sin_tipo_de_comprobante_NM_no_contabiliza()
    {
        var c = await Calcular();
        var nm = await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM");
        nm.IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Accounting.VoucherType.NotFound");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_no_contabiliza()
    {
        var c = await Calcular();
        c.D.Db.AccountingSetups.RemoveRange(c.D.Db.AccountingSetups);
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Accounting.NotInitialized");
    }

    [Fact]
    public async Task La_reversion_es_el_espejo_del_original_y_lo_referencia()
    {
        var c = await Calcular();
        var original = await Contabilizar(c);
        original.IsSuccess.Should().BeTrue(original.Error.Message);
        await c.D.Db.SaveChangesAsync();

        var r = await c.D.Poster.ReverseAsync(original.Value, FinDeMarzo, "Error en las novedades", CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var reverso = r.Value;
        reverso.Number.Should().Be(2);
        reverso.Kind.Should().Be(DocumentKind.Reversal);
        reverso.Description.Should().Contain("Reversión del comprobante NM-1").And.Contain("Error en las novedades");
        reverso.TotalDebit.Should().Be(original.Value.TotalCredit);
        reverso.TotalCredit.Should().Be(original.Value.TotalDebit);
        reverso.ReversesDocument.Should().BeSameAs(original.Value);
        original.Value.Status.Should().Be(DocumentStatus.Reversed);
        original.Value.ReversedByDocument.Should().BeSameAs(reverso);
        var asientos = reverso.Lines.OrderBy(l => l.LineNumber).ToList();
        asientos.Should().HaveCount(original.Value.Lines.Count);
        foreach (var (o, rv) in original.Value.Lines.OrderBy(l => l.LineNumber).Zip(asientos))
        {
            rv.AccountId.Should().Be(o.AccountId);
            rv.CostCenterId.Should().Be(o.CostCenterId);
            rv.BranchId.Should().Be(o.BranchId);
            rv.Debit.Should().Be(o.Credit);
            rv.Credit.Should().Be(o.Debit);
            rv.Description.Should().StartWith("Reversión NM-1");
        }

        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.AccountingDocuments.SingleAsync(d => d.Id == original.Value.Id)).ReversedByDocumentId.Should().Be(reverso.Id);
    }

    [Fact]
    public async Task Un_comprobante_ya_reversado_no_se_reversa_dos_veces_ni_se_reversa_la_reversion()
    {
        var c = await Calcular();
        var original = await Contabilizar(c);
        await c.D.Db.SaveChangesAsync();
        var reverso = await c.D.Poster.ReverseAsync(original.Value, FinDeMarzo, "una vez", CancellationToken.None);
        reverso.IsSuccess.Should().BeTrue(reverso.Error.Message);
        await c.D.Db.SaveChangesAsync();

        (await c.D.Poster.ReverseAsync(original.Value, FinDeMarzo, "otra vez", CancellationToken.None)).Error.Code.Should().Be("Accounting.Document.AlreadyReversed");
        (await c.D.Poster.ReverseAsync(reverso.Value, FinDeMarzo, "la reversión", CancellationToken.None)).Error.Code.Should().Be("Accounting.Document.IsReversal");
    }

    [Fact]
    public async Task La_reversion_en_periodo_contable_cerrado_va_al_primer_periodo_abierto_o_se_niega()
    {
        var c = await Calcular();
        var original = await Contabilizar(c);
        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.AccountingPeriods.SingleAsync()).Status = PeriodStatus.Closed;
        await c.D.Db.SaveChangesAsync();

        // Sin ningún período abierto, no hay dónde fecharla.
        (await c.D.Poster.ReverseAsync(original.Value, FinDeMarzo, "tarde", CancellationToken.None)).Error.Code.Should().Be("Accounting.Period.Closed");

        // Con abril abierto (y el reloj en marzo, la fecha de abril sería futura): se abre el reloj y se fecha el 1 de abril, y lo dice.
        c.D.PeriodoContable(2026, 4);
        c.D.Clock.TodayUtc.Returns(new DateOnly(2026, 4, 10));
        var r = await c.D.Poster.ReverseAsync(original.Value, FinDeMarzo, "tarde", CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Date.Should().Be(new DateOnly(2026, 4, 1));
        r.Value.Description.Should().Contain("2026-04-01").And.Contain("cerrado");
        r.Value.Lines.Should().OnlyContain(l => l.Date == new DateOnly(2026, 4, 1));
    }

    [Fact]
    public async Task Cada_linea_lleva_tercero_el_empleado_en_devengos_y_la_entidad_vinculada_en_aportes()
    {
        var c = await Calcular();
        var representante = new Person { FirstName = "EPS", LastName = "Salud Total", BusinessName = "EPS Salud Total S.A.", TaxId = "800000001", Status = "A", CreatedBy = "test" };
        c.D.Db.People.Add(representante);
        await c.D.Db.SaveChangesAsync();
        var eps = new HealthInsuranceProvider { Code = "EPS01", Name = "Salud Total", TaxId = "800000001", PersonId = representante.Id, CreatedBy = "test" };
        c.D.Db.HealthInsuranceProviders.Add(eps);
        await c.D.Db.SaveChangesAsync();
        foreach (var e in c.Empleados) e.Employee.HealthInsuranceId = eps.Id;
        var cuentasSalud = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALUD_EMPLEADOR");
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalud.CreditAccountId)).RequiresThirdParty = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var lineas = r.Value.Lines.ToList();
        var ana = c.Empleados.Single(e => e.Employee.Salary == 2_000_000m).Employee;
        lineas.Where(l => l.Description == "Nómina SALARIO").Should().OnlyContain(l => l.PersonId != null, "devengos y deducciones llevan al empleado");
        lineas.Where(l => l.Description == "Nómina SALARIO" && l.PersonId == ana.PersonId && l.Debit > 0).Should().ContainSingle().Which.Debit.Should().Be(2_000_000m);
        var salud = lineas.Where(l => l.Description.StartsWith("Nómina SALUD_EMPLEADOR")).ToList();
        salud.Should().NotBeEmpty().And.OnlyContain(l => l.PersonId == representante.Id, "los aportes van con la persona vinculada a la EPS");
        salud.Should().OnlyContain(l => l.Description.Contains("Salud Total"));
        lineas.Where(l => l.Description.StartsWith("Nómina SENA")).Should().OnlyContain(l => l.PersonId == null, "SENA no tiene catálogo en el ERP");
    }

    [Fact]
    public async Task Una_entidad_sin_persona_vinculada_impide_contabilizar_si_la_cuenta_exige_tercero_y_no_agrega_nada()
    {
        var c = await Calcular();
        var eps = new HealthInsuranceProvider { Code = "EPS02", Name = "Compensar", TaxId = "800000002", PersonId = null, CreatedBy = "test" };
        c.D.Db.HealthInsuranceProviders.Add(eps);
        await c.D.Db.SaveChangesAsync();
        foreach (var e in c.Empleados) e.Employee.HealthInsuranceId = eps.Id;
        var cuentasSalud = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALUD_EMPLEADOR");
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalud.CreditAccountId)).RequiresThirdParty = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Line.ThirdPartyRequired");
        r.Error.Message.Should().Contain("Compensar").And.Contain("SALUD_EMPLEADOR").And.Contain("Nómina › EPS");
        r.Error.Should().BeOfType<IngenIA365ERP.Application.Common.Models.ErrorConDatos>();
        await c.D.Db.SaveChangesAsync();
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await c.D.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(1);

        // Sin exigencia de tercero, la misma entidad sin vínculo no estorba: la línea va sin tercero.
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalud.CreditAccountId)).RequiresThirdParty = false;
        await c.D.Db.SaveChangesAsync();
        var ok = await Contabilizar(c);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        ok.Value.Lines.Where(l => l.Description.StartsWith("Nómina SALUD_EMPLEADOR")).Should().OnlyContain(l => l.PersonId == null);
    }

    // ----------------------------------------------------------------- enmienda D-08 de la 010 (feature 013, T53, T0631; pregunta C1) --

    [Fact]
    public async Task DESC_CARTERA_contabiliza_debito_salarios_por_pagar_y_credito_la_cuenta_del_concepto_por_lo_descontado()
    {
        var c = await Calcular(conCuotasDeCartera: true);
        var lineas = c.Empleados.SelectMany(e => e.Lines).Where(l => l.ConceptCode == "DESC_CARTERA").ToList();
        lineas.Should().NotBeEmpty().And.OnlyContain(l => l.AffectsAccounting, "la cuota de Cartera contabiliza (enmienda D-08)");
        (await c.D.Db.PayrollLoanDeductions.AnyAsync(x => x.NotDeductedAmount > 0m)).Should().BeTrue("la segunda cuota sólo cabe en parte");
        var cuentas = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "DESC_CARTERA");

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var asientos = r.Value.Lines.Where(a => a.Description == "Nómina DESC_CARTERA").ToList();
        var descontado = lineas.Sum(l => l.Amount);
        asientos.Where(a => a.Debit > 0).Sum(a => a.Debit).Should().Be(descontado, "sólo lo descontado; lo no descontado no tiene asiento");
        asientos.Where(a => a.Credit > 0).Should().OnlyContain(a => a.AccountId == cuentas.CreditAccountId,
            "el crédito va a la cuenta del concepto: el puente «recaudos por nómina para Cartera»");
        asientos.Where(a => a.Credit > 0).Sum(a => a.Credit).Should().Be(descontado);
    }

    [Fact]
    public async Task DESC_CARTERA_sin_cuentas_impide_contabilizar_y_lo_nombra()
    {
        var c = await Calcular(conCuotasDeCartera: true);
        (await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "DESC_CARTERA")).IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Payroll.ConceptWithoutAccounts");
        r.Error.Message.Should().Contain("DESC_CARTERA");
    }

    // ---------------- Centro de costo del empleado (decisión del 2026-10-22): FK, no texto ----------------

    private static async Task ExigirCentroEnElGastoDeSalarioAsync(Calculada c)
    {
        var cuentas = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALARIO");
        (await c.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentas.DebitAccountId)).RequiresCostCenter = true;
        await c.D.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task El_centro_sale_de_la_FK_del_empleado_y_no_del_codigo_heredado_de_texto()
    {
        var c = await Calcular();
        await ExigirCentroEnElGastoDeSalarioAsync(c);
        // El texto viejo ya no cuenta: uno vacío y el otro con un código que no casa con nada.
        foreach (var e in await c.D.Db.Employees.ToListAsync()) e.CostCenterId = e.CostCenterRefId is null ? "" : "ZZ";
        await c.D.Db.SaveChangesAsync();
        var cc01 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "01");
        var cc02 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "02");

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var debitos = r.Value.Lines.Where(a => a.Description == "Nómina SALARIO" && a.Debit > 0).ToList();
        debitos.Should().HaveCount(2);
        debitos.Single(a => a.CostCenterId == cc01.Id).Debit.Should().Be(2_000_000m);
        debitos.Single(a => a.CostCenterId == cc02.Id).Debit.Should().Be(3_000_000m);
    }

    [Fact]
    public async Task Un_empleado_sin_centro_usa_las_cuentas_por_defecto_y_la_linea_queda_sin_centro()
    {
        var c = await Calcular(); // las cuentas de la prueba no exigen centro
        foreach (var e in await c.D.Db.Employees.ToListAsync()) e.CostCenterRefId = null;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Lines.Should().OnlyContain(a => a.CostCenterId == null);
        r.Value.Lines.Where(a => a.Description == "Nómina SALARIO" && a.Debit > 0).Sum(a => a.Debit).Should().Be(5_000_000m, "el empleado es el tercero: una línea por cada uno");
    }

    [Fact]
    public async Task La_fila_de_cuentas_del_centro_manda_sobre_la_fila_por_defecto_del_concepto()
    {
        var c = await Calcular();
        var cc02 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "02");
        var porDefecto = await c.D.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALARIO");
        var gastoComercial = c.D.Cuenta("510599", "Gasto salario comercial", IngenIA365ERP.Domain.Enums.Accounting.AccountNature.Debit, exigeCentroDeCosto: true);
        c.D.Db.PayrollConceptDefinitionAccounts.Add(new PayrollConceptDefinitionAccount
        {
            ConceptCode = "SALARIO", CostCenterId = cc02.Id, DebitAccountId = gastoComercial.Id, CreditAccountId = porDefecto.CreditAccountId, CreatedBy = "test",
        });
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var debitos = r.Value.Lines.Where(a => a.Description == "Nómina SALARIO" && a.Debit > 0).ToList();
        debitos.Should().ContainSingle(a => a.AccountId == gastoComercial.Id && a.CostCenterId == cc02.Id && a.Debit == 3_000_000m);
        debitos.Should().ContainSingle(a => a.AccountId == porDefecto.DebitAccountId && a.Debit == 2_000_000m);
    }

    [Fact]
    public async Task Si_la_cuenta_exige_centro_y_el_empleado_no_lo_tiene_el_error_nombra_al_empleado()
    {
        var c = await Calcular();
        await ExigirCentroEnElGastoDeSalarioAsync(c);
        var bruno = c.Empleados.Single(e => e.Employee.Salary == 3_000_000m).Employee;
        bruno.CostCenterRefId = null;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Employee.CostCenterRequired");
        r.Error.Message.Should().Contain("Bruno Prueba").And.Contain("centro de costo").And.Contain("ficha de cada empleado");
        r.Error.Message.Should().NotContain("Ana", "Ana sí tiene centro");
        var datos = System.Text.Json.JsonSerializer.Serialize(((IngenIA365ERP.Application.Common.Models.ErrorConDatos)r.Error).Data);
        datos.Should().Contain(bruno.PublicId.ToString()).And.Contain("SALARIO");
        (await c.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Varios_empleados_sin_centro_se_nombran_todos_y_el_mensaje_va_en_plural()
    {
        var c = await Calcular();
        await ExigirCentroEnElGastoDeSalarioAsync(c);
        foreach (var e in await c.D.Db.Employees.ToListAsync()) e.CostCenterRefId = null;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Payroll.Employee.CostCenterRequired");
        r.Error.Message.Should().StartWith("2 empleados").And.Contain("Bruno Prueba").And.Contain("Ana");
    }

    [Fact]
    public async Task Un_centro_eliminado_despues_de_asignarlo_se_dice_con_el_empleado()
    {
        var c = await Calcular();
        var cc02 = await c.D.Db.CostCenters.SingleAsync(x => x.LegacyCode == "02");
        cc02.IsDeleted = true;
        await c.D.Db.SaveChangesAsync();

        var r = await Contabilizar(c);

        r.Error.Code.Should().Be("Payroll.Employee.CostCenterDeleted");
        r.Error.Message.Should().Contain("Bruno Prueba").And.Contain("eliminado");
    }
}
