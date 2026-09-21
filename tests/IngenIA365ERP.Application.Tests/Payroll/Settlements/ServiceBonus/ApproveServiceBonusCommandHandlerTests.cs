using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Identity.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.ServiceBonus;

/// <summary>
/// T041 (feature 010, US1; FR-004, FR-006, SC-003): aprobar la prima por su comando deja el comprobante
/// <c>NM</c> con <c>SourceType = ServiceBonusRun</c> fechado al corte (D-04), cancela la provisión
/// acumulada y lleva la diferencia al gasto, no toca el período ordinario, y el saldo de la cuenta de
/// provisión en el libro queda igual a lo pendiente de liquidar antes, después y tras reversar
/// (SC-003). El <c>Operator</c> no tiene el permiso de aprobar ni una ruta alterna: la de la ordinaria
/// lo manda a la suya.
/// </summary>
public class ApproveServiceBonusCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private const decimal ProvisionMensual = 187_424m;
    private const int MesesProvisionados = 6;

    /// <summary>Seis meses aprobados de Ana con provisión de prima (6 × 187.424 = 1.124.544), contabilidad iniciada y las tres cuentas de la prima sobre UNA cuenta de provisión.</summary>
    private static (NominaTestData D, ChartOfAccount Provision) EscenarioContable()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre(ProvisionMensual);
        PrimaDePrueba.RedondearAlCentavo(d);
        LiquidacionDePrueba.ConContabilidad(d);
        for (var mes = 1; mes <= 5; mes++) if (mes != 3) d.PeriodoContable(2026, mes);

        // La provisión de prima vive en una sola cuenta: la acredita PROV_PRIMA cada mes, la debita PRIMA
        // al liquidar y la acredita (o debita, si es liberación) PRIMA_AJUSTE_PROV por la diferencia.
        var provision = d.Cuenta("26100501", "Provisión prima de servicios", Domain.Enums.Accounting.AccountNature.Credit);
        foreach (var cuentas in d.Db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == "PROV_PRIMA" || a.ConceptCode == "PRIMA_AJUSTE_PROV").ToList())
            cuentas.CreditAccountId = provision.Id;
        d.Db.PayrollConceptDefinitionAccounts.Single(a => a.ConceptCode == "PRIMA").DebitAccountId = provision.Id;
        d.Db.SaveChanges();
        return (d, provision);
    }

    /// <summary>Los seis meses aprobados pasan por el contabilizador de la ordinaria (el único camino al libro) para que la provisión exista en el libro, como en producción.</summary>
    private static async Task ContabilizarMesesAprobadosAsync(NominaTestData d)
    {
        var runs = await d.Db.PayrollRuns.Include(r => r.PayPeriod).Where(r => r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved).ToListAsync();
        foreach (var run in runs)
        {
            var filas = await LiquidacionDePrueba.FilasAsync(d, run);
            var doc = await d.Poster.PostAsync(run, filas, DateOnly.FromDateTime(run.PayPeriod!.EndDate), $"Nómina {run.PayPeriod.EndDate:MM/yyyy}", CancellationToken.None);
            doc.IsSuccess.Should().BeTrue(doc.Error.Message);
            run.AccountingDocument = doc.Value;
            await d.Db.SaveChangesAsync();
        }
    }

    /// <summary>Saldo crédito de la cuenta en el libro: sólo asientos contabilizados (no hay saldos guardados, Principio XI).</summary>
    private static async Task<decimal> SaldoEnElLibroAsync(NominaTestData d, ChartOfAccount cuenta) =>
        await d.Db.JournalEntries.AsNoTracking().Where(j => j.AccountId == cuenta.Id && j.IsPosted).SumAsync(j => j.Credit - j.Debit);

    private static async Task<Guid> PrimaCalculadaAsync(NominaTestData d)
    {
        var r = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value.RunPublicId;
    }

    [Fact]
    public async Task Aprobar_deja_el_comprobante_ServiceBonusRun_al_corte_cancela_la_provision_y_lleva_la_diferencia_al_gasto()
    {
        var (d, provision) = EscenarioContable();
        var runId = await PrimaCalculadaAsync(d);
        var marzoAntes = (await d.Db.PayPeriods.AsNoTracking().SingleAsync(p => p.Id == d.Marzo.Id)).Status;

        var r = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.PostingDate.Should().Be(new DateOnly(2026, 6, 30), "D-04: la fecha del comprobante es el corte del semestre");
        r.Value.Number.Should().Be("NM-1");

        var run = await d.Db.PayrollRuns.Include(x => x.AccountingDocument).SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.ApprovedBy.Should().Be("contadora@demo");
        run.PayDate.Should().Be(new DateOnly(2026, 6, 30));
        run.AccountingDocument!.SourceType.Should().Be("ServiceBonusRun");
        run.AccountingDocument.Date.Should().Be(new DateOnly(2026, 6, 30));
        run.AccountingDocument.TotalDebit.Should().Be(run.AccountingDocument.TotalCredit);

        // La provisión acumulada (6 × 187.424) se cancela y la diferencia contra la prima liquidada va al gasto.
        var prima = (await PrimaDePrueba.LineaAsync(d, runId, d.Ana, "PRIMA"))!;
        var ajuste = (await PrimaDePrueba.LineaAsync(d, runId, d.Ana, "PRIMA_AJUSTE_PROV"))!;
        prima.Amount.Should().Be(1_124_547.50m);
        ajuste.Amount.Should().Be(prima.Amount - ProvisionMensual * MesesProvisionados);
        ajuste.BaseAmount.Should().Be(ProvisionMensual * MesesProvisionados, "la explicación dice contra qué provisión se cruzó");

        var asientos = await d.Db.JournalEntries.AsNoTracking().Where(j => j.DocumentId == run.AccountingDocument.Id).ToListAsync();
        asientos.Where(j => j.AccountId == provision.Id).Sum(j => j.Debit).Should().Be(prima.Amount, "PRIMA debita la provisión por lo liquidado");
        asientos.Where(j => j.AccountId == provision.Id).Sum(j => j.Credit).Should().Be(ajuste.Amount, "el ajuste acredita la diferencia que faltaba");

        // El período ordinario no cambia: la prima no es un período (contracts/api.md §2).
        (await d.Db.PayPeriods.AsNoTracking().SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(marzoAntes);
        (await d.Db.PayPeriods.AsNoTracking().CountAsync(p => p.Status == PayPeriodStatus.Approved)).Should().Be(MesesProvisionados);

        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementApproved), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SC003_el_saldo_de_la_provision_en_el_libro_es_lo_pendiente_de_liquidar_antes_despues_y_tras_reversar()
    {
        var (d, provision) = EscenarioContable();
        await ContabilizarMesesAprobadosAsync(d);
        var acumulada = ProvisionMensual * MesesProvisionados;
        (await SaldoEnElLibroAsync(d, provision)).Should().Be(acumulada, "antes de liquidar, el libro debe toda la prima provisionada");

        var runId = await PrimaCalculadaAsync(d);
        (await SaldoEnElLibroAsync(d, provision)).Should().Be(acumulada, "calcular no contabiliza nada");

        var aprobada = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, Confirm: true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        (await SaldoEnElLibroAsync(d, provision)).Should().Be(0m, "la prima aprobada dejó cero pendiente: la provisión se canceló y la diferencia fue al gasto");

        var reversada = await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(runId, "Faltó una comisión"), CancellationToken.None);
        reversada.IsSuccess.Should().BeTrue(reversada.Error.Message);
        (await SaldoEnElLibroAsync(d, provision)).Should().Be(acumulada, "reversar devuelve la provisión al libro: la prima vuelve a estar pendiente");
    }

    [Fact]
    public async Task La_fecha_del_comprobante_se_puede_mover_entre_el_corte_y_hoy_y_fuera_de_ese_rango_se_rechaza()
    {
        var (d, _) = EscenarioContable();
        var runId = await PrimaCalculadaAsync(d);

        var antesDelCorte = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, true, new DateOnly(2026, 6, 29)), CancellationToken.None);
        antesDelCorte.Error.Code.Should().Be("Payroll.Settlement.PostingDateInvalid");
        var despuesDeHoy = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, true, new DateOnly(2026, 7, 11)), CancellationToken.None);
        despuesDeHoy.Error.Code.Should().Be("Payroll.Settlement.PostingDateInvalid");
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);

        var enJulio = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, true, new DateOnly(2026, 7, 3)), CancellationToken.None);
        enJulio.IsSuccess.Should().BeTrue(enJulio.Error.Message);
        enJulio.Value.PostingDate.Should().Be(new DateOnly(2026, 7, 3));
    }

    [Fact]
    public async Task Sin_confirmar_o_sin_cuentas_no_se_aprueba_y_el_libro_queda_intacto()
    {
        var (d, _) = EscenarioContable();
        var runId = await PrimaCalculadaAsync(d);

        (await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, Confirm: false), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.ConfirmationRequired");

        d.Db.PayrollConceptDefinitionAccounts.Remove(await d.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "PRIMA"));
        await d.Db.SaveChangesAsync();
        var sinCuentas = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(runId, Confirm: true), CancellationToken.None);
        sinCuentas.Error.Code.Should().Be("Payroll.Settlement.ConceptAccountsMissing");
        sinCuentas.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { conceptCodes = new[] { "PRIMA" } });
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await d.Db.JournalEntries.CountAsync()).Should().Be(0);
        (await d.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Quien_calculo_no_aprueba_sin_politica_y_segunda_confirmacion()
    {
        var (d, _) = EscenarioContable();
        var runId = await PrimaCalculadaAsync(d); // la calcula ana@demo

        var misma = await PrimaDePrueba.Aprobar(d, d.User).Handle(new ApproveServiceBonusCommand(runId, Confirm: true), CancellationToken.None);
        misma.Error.Code.Should().Be("Payroll.Settlement.SegregationViolation");

        d.Politica(Domain.Payroll.Policies.CompanyPolicyKeys.AllowSameUserApproval, Domain.Payroll.Policies.CompanyPolicyKeys.Verdadero, new DateOnly(2026, 1, 1));
        (await PrimaDePrueba.Aprobar(d, d.User).Handle(new ApproveServiceBonusCommand(runId, Confirm: true), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.ConfirmationRequired");
        var ok = await PrimaDePrueba.Aprobar(d, d.User).Handle(new ApproveServiceBonusCommand(runId, Confirm: true, ConfirmWithoutSegregation: true), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        ok.Value.ApprovedWithoutSegregation.Should().BeTrue();
    }

    [Fact]
    public async Task El_Operator_no_tiene_el_permiso_de_aprobar_y_la_ruta_de_la_ordinaria_lo_manda_a_la_de_la_prima()
    {
        // Sin permiso: el reparto de roles da al Operador calcular y ver, nunca aprobar ni reversar (contracts/api.md §1).
        var catalogo = PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}").ToList();
        var operador = BuiltInRolesSeeder.CodigosParaRol("Operator", catalogo);
        operador.Should().Contain("Payroll.ServiceBonus.Calculate").And.Contain("Payroll.ServiceBonus.View");
        operador.Should().NotContain("Payroll.ServiceBonus.Approve").And.NotContain("Payroll.ServiceBonus.Reverse");
        BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", catalogo).Should().Contain("Payroll.ServiceBonus.Approve");

        // Sin ruta alterna: /api/payroll/runs/{runId}/approve rechaza una prima con su código y su ruta.
        var (d, _) = EscenarioContable();
        var runId = await PrimaCalculadaAsync(d);
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var ordinaria = new ApprovePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Policies, permisos, d.Clock, Contadora,
            new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance), d.StaleMarker);

        var r = await ordinaria.Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.UseSettlementRoute");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { kind = "ServiceBonus", route = "/api/payroll/settlements/service-bonus/{runId}" });
        (await d.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Draft);
    }
}
