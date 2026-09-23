using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;

/// <summary>
/// Feature 010 (T029): el ciclo de vida común de las liquidaciones especiales —duplicados (FR-005),
/// tipo de la ruta, segregación, fecha del comprobante, cuentas faltantes, aprobar en una
/// transacción con el saldo inicial consumido, reversar con espejo y liberación, descartar con
/// motivo— sobre una prima real calculada por el motor y persistida por el molde de la ordinaria.
/// </summary>
public class SettlementRunWorkflowTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static SettlementApprovalRequest Aprobar(Guid runId, PayrollRunKind kind = PayrollRunKind.ServiceBonus, DateOnly? fecha = null, bool sinSegregacion = false) =>
        new(runId, kind, Confirm: true, fecha, ConfirmWithoutSegregation: sinSegregacion);

    // ------------------------------------------------------------- persistencia --

    [Fact]
    public async Task El_borrador_lleva_Kind_corte_anio_semestre_version_y_lineas_explicadas_y_recalcular_reemplaza_al_anterior()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();

        var v1 = await LiquidacionDePrueba.PrimaAsync(d);
        var v2 = await LiquidacionDePrueba.PrimaAsync(d, recalculo: true);

        var corridas = await d.Db.PayrollRuns.Where(r => r.Kind == PayrollRunKind.ServiceBonus).OrderBy(r => r.Version).ToListAsync();
        corridas.Select(r => (r.Version, r.Status)).Should().Equal((1, PayrollRunStatus.Superseded), (2, PayrollRunStatus.Draft));
        v2.PayPeriodId.Should().BeNull();
        v2.CutoffDate.Should().Be(new DateOnly(2026, 6, 30));
        (v2.Year, v2.Semester).Should().Be(((short)2026, (byte)1));
        v2.EsCoherente.Should().BeTrue();
        v2.CalculatedBy.Should().Be("ana@demo");
        v2.InputsHash.Should().HaveLength(64).And.Be(v1.InputsHash, "misma entrada, mismo hash (FR-014)");

        var fila = await d.Db.PayrollRunEmployees.Include(e => e.Lines).SingleAsync(e => e.PayrollRunId == v2.Id);
        fila.Lines.Should().Contain(l => l.ConceptCode == "PRIMA" && l.Amount > 0m && l.ExplanationJson.Contains("Prima de servicios"));
        fila.Lines.Should().Contain(l => l.ConceptCode == "PRIMA_AJUSTE_PROV");
        fila.DaysWorked.Should().Be(180, "los días del semestre que causan prima");
        fila.NetPay.Should().Be(fila.TotalEarnings - fila.TotalDeductions);
        v2.TotalNet.Should().Be(fila.NetPay);
    }

    [Fact]
    public async Task Duplicate_mientras_la_anterior_esta_en_borrador_o_aprobada_y_admitida_tras_reversar()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var key = SettlementRunKey.Prima(2026, 1);
        var persistidor = d.Persistidor();
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var conBorrador = SettlementRunPersister.Duplicado(await persistidor.CorridasDeAsync(key, CancellationToken.None), key, recalculo: false);
        conBorrador.Should().NotBeNull();
        conBorrador!.Code.Should().Be("Payroll.Settlement.Duplicate");
        conBorrador.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicId = run.PublicId, status = "Draft" });
        SettlementRunPersister.Duplicado(await persistidor.CorridasDeAsync(key, CancellationToken.None), key, recalculo: true).Should().BeNull("recalcular sí reemplaza el borrador");

        (await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var conAprobada = SettlementRunPersister.Duplicado(await persistidor.CorridasDeAsync(key, CancellationToken.None), key, recalculo: true);
        conAprobada.Should().NotBeNull("una aprobada no se reemplaza ni recalculando: se reversa");
        conAprobada!.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicId = run.PublicId, status = "Approved" });

        (await d.Flujo(Contadora).ReverseAsync(run.PublicId, PayrollRunKind.ServiceBonus, "Salario mal registrado", null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        SettlementRunPersister.Duplicado(await persistidor.CorridasDeAsync(key, CancellationToken.None), key, recalculo: false).Should().BeNull("tras reversar se admite otra");
        var otra = await LiquidacionDePrueba.PrimaAsync(d);
        otra.Version.Should().Be(2);
    }

    // ------------------------------------------------------------------ aprobar --

    [Fact]
    public async Task KindMismatch_cuando_la_ruta_no_es_la_del_tipo_de_la_corrida()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var r = await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId, PayrollRunKind.Severance), null, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { kind = "ServiceBonus", expected = "Severance" });
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Quien_calculo_no_aprueba_salvo_politica_vigente_al_corte_y_segunda_confirmacion()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d); // la calcula ana@demo

        var misma = await d.Flujo(d.User).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None);
        misma.Error.Code.Should().Be("Payroll.Settlement.SegregationViolation");
        misma.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { calculatedBy = "ana@demo" });

        // La política sólo rige desde julio: al corte (30-06) no aplica.
        d.Politica(CompanyPolicyKeys.AllowSameUserApproval, CompanyPolicyKeys.Verdadero, new DateOnly(2026, 7, 1));
        (await d.Flujo(d.User).ApproveAsync(Aprobar(run.PublicId, sinSegregacion: true), null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.SegregationViolation", "la política se lee a la fecha de corte, no a hoy");

        var vigente = await d.Db.CompanyPolicies.SingleAsync();
        vigente.ValidFrom = new DateOnly(2026, 1, 1);
        await d.Db.SaveChangesAsync();
        (await d.Flujo(d.User).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.SegregationConfirmationRequired", "con la política sí, pero exige la segunda confirmación");

        var ok = await d.Flujo(d.User).ApproveAsync(Aprobar(run.PublicId, sinSegregacion: true), null, CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        ok.Value.ApprovedWithoutSegregation.Should().BeTrue();
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).ApprovedWithoutSegregation.Should().BeTrue("queda registrado");
    }

    [Fact]
    public async Task PostingDateInvalid_fuera_del_rango_corte_hoy()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var r = await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId, fecha: new DateOnly(2026, 6, 15)), null, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.PostingDateInvalid");
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task ConceptAccountsMissing_deja_cero_filas_en_las_tablas_contables_y_la_corrida_en_borrador()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        d.Db.PayrollConceptDefinitionAccounts.Remove(await d.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "PRIMA_AJUSTE_PROV"));
        await d.Db.SaveChangesAsync();

        var r = await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.ConceptAccountsMissing");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { conceptCodes = new[] { "PRIMA_AJUSTE_PROV" } });
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await d.Db.JournalEntries.CountAsync()).Should().Be(0);
        var corrida = await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id);
        corrida.Status.Should().Be(PayrollRunStatus.Draft);
        corrida.AccountingDocumentId.Should().BeNull();
    }

    [Fact]
    public async Task Aprobar_contabiliza_en_la_misma_transaccion_fija_PayDate_consume_el_saldo_inicial_y_audita()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var saldo = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 300_000m, diasPrima: 45);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var ganchoCorrio = false;

        var r = await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId), (corrida, empleados, _) =>
        {
            ganchoCorrio = corrida.Status == PayrollRunStatus.Approved && empleados.Count == 1;
            return Task.FromResult(Result.Success());
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        ganchoCorrio.Should().BeTrue("lo propio de cada tipo corre antes de guardar, con la corrida ya aprobada");
        r.Value.Number.Should().Be("NM-1");
        r.Value.PostingDate.Should().Be(new DateOnly(2026, 6, 30));
        r.Value.Total.Should().Be(run.TotalNet);

        var corridaGuardada = await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id);
        corridaGuardada.Status.Should().Be(PayrollRunStatus.Approved);
        corridaGuardada.ApprovedBy.Should().Be("contadora@demo");
        corridaGuardada.PayDate.Should().Be(new DateOnly(2026, 6, 30));
        corridaGuardada.AccountingDocumentId.Should().NotBeNull();
        var doc = await d.Db.AccountingDocuments.SingleAsync(x => x.Id == corridaGuardada.AccountingDocumentId);
        doc.SourceType.Should().Be("ServiceBonusRun");
        doc.TotalDebit.Should().Be(doc.TotalCredit).And.BeGreaterThan(0m);
        (await d.Db.EmployeeBenefitOpeningBalances.SingleAsync(b => b.Id == saldo.Id)).ConsumedByRunId.Should().Be(run.Id, "R3: el saldo ya no se edita, se ajusta");

        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementApproved), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_gancho_que_falla_no_deja_nada_guardado()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var r = await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId),
            (_, _, _) => Task.FromResult(Result.Failure(new Error("Payroll.Test.Propio", "Cartera no aceptó el pago."))), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Test.Propio");
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        d.Db.ChangeTracker.Clear();
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    // ----------------------------------------------------------------- reversar --

    [Fact]
    public async Task Reversar_deja_el_espejo_libera_el_saldo_inicial_y_admite_liquidar_de_nuevo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var saldo = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 300_000m, diasPrima: 45);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        (await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var r = await d.Flujo(Contadora).ReverseAsync(run.PublicId, PayrollRunKind.ServiceBonus, "Faltó una comisión", null, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.ReversalNumber.Should().Be("NM-2");
        var corrida = await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id);
        corrida.Status.Should().Be(PayrollRunStatus.Reversed);
        corrida.ReversalReason.Should().Be("Faltó una comisión");
        var original = await d.Db.AccountingDocuments.SingleAsync(x => x.Id == corrida.AccountingDocumentId);
        var espejo = await d.Db.AccountingDocuments.SingleAsync(x => x.Id == corrida.ReversalAccountingDocumentId);
        espejo.TotalDebit.Should().Be(original.TotalDebit);
        espejo.SourceType.Should().Be("ServiceBonusRun");
        espejo.Date.Should().Be(new DateOnly(2026, 7, 10), "el espejo se fecha hoy");
        (await d.Db.EmployeeBenefitOpeningBalances.SingleAsync(b => b.Id == saldo.Id)).ConsumedByRunId.Should().BeNull("liberado");

        var otra = await LiquidacionDePrueba.PrimaAsync(d);
        otra.Status.Should().Be(PayrollRunStatus.Draft);
        otra.Version.Should().Be(2);
    }

    /// <summary>
    /// Revisión N1 (2026-09-21): el saldo inicial lo lee toda liquidación especial aprobada con corte igual o
    /// posterior a su fecha, pero sólo la primera dejaba <c>ConsumedByRunId</c>; reversar esa primera lo liberaba
    /// y el PUT reemplazaba el saldo (y borraba los ajustes) con las cesantías todavía aprobadas sobre las cifras viejas.
    /// </summary>
    [Fact]
    public async Task Reversar_la_primera_liquidacion_no_libera_el_saldo_inicial_que_otra_aprobada_tambien_uso()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var saldo = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 300_000m, cesantias: 1_000_000m, intereses: 120_000m, diasPrima: 45, diasCesantias: 180);
        var prima = await LiquidacionDePrueba.PrimaAsync(d);
        (await d.Flujo(Contadora).ApproveAsync(Aprobar(prima.PublicId), null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var cesantias = await LiquidacionDePrueba.CesantiasAsync(d);
        var aprobadas = await d.Flujo(Contadora).ApproveAsync(Aprobar(cesantias.PublicId, PayrollRunKind.Severance), null, CancellationToken.None);
        aprobadas.IsSuccess.Should().BeTrue(aprobadas.Error.Message);
        (await d.Db.EmployeeBenefitOpeningBalances.AsNoTracking().SingleAsync(b => b.Id == saldo.Id)).ConsumedByRunId.Should().Be(prima.Id, "la más antigua lleva la marca");

        var r = await d.Flujo(Contadora).ReverseAsync(prima.PublicId, PayrollRunKind.ServiceBonus, "Faltó una comisión", null, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.EmployeeBenefitOpeningBalances.AsNoTracking().SingleAsync(b => b.Id == saldo.Id)).ConsumedByRunId
            .Should().Be(cesantias.Id, "las cesantías aprobadas también lo usaron: la marca pasa a ellas");
        var put = await new Application.Payroll.OpeningBalances.UpsertBenefitBalanceCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter, d.StaleMarker)
            .Handle(new Application.Payroll.OpeningBalances.UpsertBenefitBalanceCommand(d.Ana.PublicId, new DateOnly(2025, 12, 31), 0m, 900_000m, 100_000m, 250_000m), CancellationToken.None);
        put.Error.Code.Should().Be("Payroll.BenefitBalance.Consumed");
        put.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicIds = new[] { cesantias.PublicId } });

        // Reversadas también las cesantías, ya nadie lo usa: se libera y el PUT entra.
        (await d.Flujo(Contadora).ReverseAsync(cesantias.PublicId, PayrollRunKind.Severance, "x", null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await d.Db.EmployeeBenefitOpeningBalances.AsNoTracking().SingleAsync(b => b.Id == saldo.Id)).ConsumedByRunId.Should().BeNull();
        d.Db.ChangeTracker.Clear();
        (await new Application.Payroll.OpeningBalances.UpsertBenefitBalanceCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter, d.StaleMarker)
            .Handle(new Application.Payroll.OpeningBalances.UpsertBenefitBalanceCommand(d.Ana.PublicId, new DateOnly(2025, 12, 31), 0m, 900_000m, 100_000m, 250_000m), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reversar_exige_aprobada_y_no_admite_marcas_de_pago_vigentes()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        (await d.Flujo(Contadora).ReverseAsync(run.PublicId, PayrollRunKind.ServiceBonus, "x", null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.NotApproved");

        (await d.Flujo(Contadora).ApproveAsync(Aprobar(run.PublicId), null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var fila = await d.Db.PayrollRunEmployees.SingleAsync(e => e.PayrollRunId == run.Id);
        d.Db.PayrollPayments.Add(new Domain.Entities.Payroll.PayrollPayment { PayrollRunEmployeeId = fila.Id, PaidAt = NominaTestData.Ahora, PaidBy = "tesorera", CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        (await d.Flujo(Contadora).ReverseAsync(run.PublicId, PayrollRunKind.ServiceBonus, "x", null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.PaymentBlocksReversal");
    }

    // ---------------------------------------------------------------- descartar --

    [Fact]
    public async Task Descartar_deja_Superseded_con_quien_cuando_y_por_que_sin_contabilidad()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var r = await d.Flujo(Contadora).DiscardAsync(run.PublicId, PayrollRunKind.ServiceBonus, "Se calculó con el salario viejo", null, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var corrida = await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id);
        corrida.Status.Should().Be(PayrollRunStatus.Superseded);
        corrida.DiscardedBy.Should().Be("contadora@demo");
        corrida.DiscardReason.Should().Be("Se calculó con el salario viejo");
        corrida.DiscardedAt.Should().Be(d.Clock.UtcNow);
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await d.Flujo(Contadora).DiscardAsync(run.PublicId, PayrollRunKind.ServiceBonus, "otra vez", null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.NotDraft");
    }

    [Fact]
    public async Task Aprobar_sin_confirmar_o_un_borrador_ya_reemplazado_se_rechaza()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var v1 = await LiquidacionDePrueba.PrimaAsync(d);
        var v2 = await LiquidacionDePrueba.PrimaAsync(d, recalculo: true);

        (await d.Flujo(Contadora).ApproveAsync(new SettlementApprovalRequest(v2.PublicId, PayrollRunKind.ServiceBonus, Confirm: false), null, CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.ConfirmationRequired");
        var reemplazado = await d.Flujo(Contadora).ApproveAsync(Aprobar(v1.PublicId), null, CancellationToken.None);
        reemplazado.Error.Code.Should().Be("Payroll.Settlement.NotDraft");
        reemplazado.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { status = "Superseded" });
    }
}
