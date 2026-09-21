using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Severance;

/// <summary>
/// Feature 010 US2 (T048, FR-011, SC-003): aprobar las cesantías e intereses deja el comprobante
/// <c>SeveranceRun</c> con la cuenta por pagar de <c>CESANTIAS</c> a la persona del fondo y la de
/// <c>INT_CESANTIAS</c> al empleado, cancela las provisiones de cesantías e intereses (saldo cero
/// tras aprobar; vuelve al acumulado tras reversar), fija la fecha de pago de los intereses y
/// respeta el ciclo común (tipo de la ruta, borrador, pago que bloquea la reversión, descarte).
/// </summary>
public class ApproveSeveranceCommandHandlerTests
{
    [Fact]
    public async Task Aprobar_deja_CxP_al_fondo_por_las_cesantias_y_al_empleado_por_los_intereses_con_SourceType_SeveranceRun()
    {
        var e = new EscenarioDeCesantias(conProvisiones: true, conContabilidad: true);
        var calculo = await e.CalcularAsync();
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);

        var r = await e.AprobarAsync(calculo.Value.RunPublicId, payDate: new DateOnly(2027, 1, 30));

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.PostingDate.Should().Be(new DateOnly(2026, 12, 31), "D-04: la provisión se cancela en el mes del corte");
        r.Value.Number.Should().StartWith("NM-");

        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calculo.Value.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.PayDate.Should().Be(new DateOnly(2027, 1, 30), "la fecha de pago de los intereses es propia de la liquidación");
        run.AccountingDocumentId.Should().NotBeNull();

        var doc = await e.D.Db.AccountingDocuments.AsNoTracking().SingleAsync(d => d.Id == run.AccountingDocumentId);
        doc.SourceType.Should().Be("SeveranceRun");
        doc.Date.Should().Be(new DateOnly(2026, 12, 31));
        doc.TotalDebit.Should().Be(doc.TotalCredit);

        var cuentas = await e.D.Db.PayrollConceptDefinitionAccounts.ToDictionaryAsync(a => a.ConceptCode);
        var asientos = await e.D.Db.JournalEntries.AsNoTracking().Where(j => j.DocumentId == doc.Id).ToListAsync();

        // CESANTIAS: crédito (CxP) por fondo como tercero, agrupado por fondo → un crédito a Porvenir (A + G) y otro a Protección (F).
        var cxpCesantias = asientos.Where(a => a.AccountId == cuentas["CESANTIAS"].CreditAccountId && a.Credit > 0m).ToList();
        cxpCesantias.Select(a => a.PersonId).Should().BeEquivalentTo([e.Porvenir.Id, e.Proteccion.Id], "la consignación va al fondo (FR-011, FR-088)");
        cxpCesantias.Single(a => a.PersonId == e.Porvenir.Id).Credit.Should().Be(2_749_095m + 666_666.67m);
        cxpCesantias.Single(a => a.PersonId == e.Proteccion.Id).Credit.Should().Be(2_315_761.67m);

        // INT_CESANTIAS: crédito (CxP) al empleado, uno por persona.
        var cxpIntereses = asientos.Where(a => a.AccountId == cuentas["INT_CESANTIAS"].CreditAccountId && a.Credit > 0m).ToList();
        cxpIntereses.Select(a => a.PersonId).Should().BeEquivalentTo([e.A.PersonId, e.F.PersonId, e.G.PersonId], "los intereses se pagan al empleado");
        cxpIntereses.Single(a => a.PersonId == e.A.PersonId).Credit.Should().Be(329_891.40m);

        // Provisiones canceladas: A tenía 12 meses provisionados y el ajuste lleva sólo la diferencia (+3 cesantías;
        // −0,60 intereses liberados); F y G no tienen NADA provisionado, así que su ajuste es todo lo liquidado al gasto
        // (sin ese ajuste la provisión quedaría en negativo: lo atrapó la e2e de la prima el 2026-09-21). Los ajustes de
        // provisión son gasto contra pasivo estimado, SIN tercero (como la provisión que corrigen): el poster los netea
        // en una sola línea por cuenta. Hasta la revisión N1 el de cesantías salía agrupado por fondo, con el fondo como
        // tercero de un movimiento que no es suyo.
        var ajusteCesantias = asientos.Where(a => a.AccountId == cuentas["CESANTIAS_AJUSTE_PROV"].DebitAccountId && a.Debit > 0m).ToList();
        ajusteCesantias.Should().ContainSingle().Which.Should().Match<Domain.Entities.Accounting.Transactions.JournalEntry>(
            a => a.PersonId == null && a.Debit == 3m + 666_666.67m + 2_315_761.67m,
            "A: +3 de diferencia; F y G: sin provisión, toda su cesantía al gasto; sin fondo como tercero");
        asientos.Where(a => a.AccountId == cuentas["CESANTIAS_AJUSTE_PROV"].CreditAccountId && a.Credit > 0m)
            .Should().ContainSingle().Which.PersonId.Should().BeNull("la provisión que se corrige tampoco lleva tercero");
        asientos.Single(a => a.AccountId == cuentas["INT_CESANTIAS_AJUSTE_PROV"].CreditAccountId).Credit
            .Should().Be(277_891.40m + 26_666.67m - 0.60m, "F y G al gasto completo, menos la liberación de 0,60 de A, neteados en la misma cuenta");
    }

    [Fact]
    public async Task SC003_el_saldo_de_las_provisiones_de_cesantias_e_intereses_queda_en_cero_tras_aprobar_y_vuelve_tras_reversar()
    {
        var e = new EscenarioDeCesantias(conProvisiones: true, conContabilidad: true);
        var corte = new DateOnly(2026, 12, 31);
        var antes = (await e.D.SaldosDeProvision.LeerAsync([e.A.Id], corte, null, CancellationToken.None))[e.A.Id];
        antes.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceProvision).Balance.Should().Be(12 * 229_091m);
        antes.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceInterestProvision).Balance.Should().Be(12 * 27_491m);

        var calculo = await e.CalcularAsync([e.A.PublicId]);
        (await e.AprobarAsync(calculo.Value.RunPublicId)).IsSuccess.Should().BeTrue();

        var despues = (await e.D.SaldosDeProvision.LeerAsync([e.A.Id], corte, null, CancellationToken.None))[e.A.Id];
        despues.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceProvision).Balance.Should().Be(0m, "SC-003: liquidado = provisión acumulada + ajuste");
        despues.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceInterestProvision).Balance.Should().Be(0m);
        despues.Single(s => s.ProvisionCode == WellKnownConceptCodes.ServiceBonusProvision).Balance.Should().Be(12 * 208_333m, "la prima no se toca");

        var reversa = await e.Reversor().Handle(new ReverseSeveranceCommand(calculo.Value.RunPublicId, "Salario de A mal registrado"), CancellationToken.None);
        reversa.IsSuccess.Should().BeTrue(reversa.Error.Message);
        reversa.Value.ReversalNumber.Should().StartWith("NM-");

        var tras = (await e.D.SaldosDeProvision.LeerAsync([e.A.Id], corte, null, CancellationToken.None))[e.A.Id];
        tras.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceProvision).Balance.Should().Be(12 * 229_091m, "reversada, la provisión vuelve a estar acumulada");
        tras.Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceInterestProvision).Balance.Should().Be(12 * 27_491m);

        // Y se admite liquidar de nuevo.
        (await e.CalcularAsync([e.A.PublicId])).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_fecha_de_pago_de_los_intereses_no_puede_ser_anterior_al_corte_y_por_defecto_es_la_del_comprobante()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();

        var antesDelCorte = await e.AprobarAsync(calculo.Value.RunPublicId, payDate: new DateOnly(2026, 12, 15));
        antesDelCorte.Error.Code.Should().Be("Payroll.Severance.PayDateBeforeCutoff");
        (await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calculo.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft, "el gancho falló: nada se guardó");
        (await e.D.Db.AccountingDocuments.AsNoTracking().CountAsync()).Should().Be(0);
        // En producción cada petición trae su propio DbContext; aquí la prueba comparte uno y suelta lo rastreado por el intento fallido.
        e.D.Db.ChangeTracker.Clear();

        var ok = await e.AprobarAsync(calculo.Value.RunPublicId);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        (await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calculo.Value.RunPublicId)).PayDate.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task La_ruta_de_cesantias_no_aprueba_una_corrida_de_otro_tipo_ni_sin_confirmacion_ni_a_quien_calculo()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();

        var ordinaria = e.D.Borrador(e.D.Marzo);
        (await e.AprobarAsync(ordinaria.PublicId)).Error.Code.Should().Be("Payroll.Settlement.KindMismatch");

        var sinConfirmar = await e.Aprobador().Handle(new ApproveSeveranceCommand(calculo.Value.RunPublicId, Confirm: false), CancellationToken.None);
        sinConfirmar.Error.Code.Should().Be("Payroll.Settlement.ConfirmationRequired");

        var quienCalculo = await e.Aprobador(e.D.User).Handle(new ApproveSeveranceCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        quienCalculo.Error.Code.Should().Be("Payroll.Settlement.SegregationViolation");
    }

    [Fact]
    public async Task Con_los_intereses_pagados_la_reversion_se_bloquea_y_descartar_deja_el_borrador_Superseded_con_motivo()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();
        (await e.AprobarAsync(calculo.Value.RunPublicId)).IsSuccess.Should().BeTrue();

        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calculo.Value.RunPublicId);
        var fila = await e.D.Db.PayrollRunEmployees.FirstAsync(f => f.PayrollRunId == run.Id);
        e.D.Db.PayrollPayments.Add(new Domain.Entities.Payroll.PayrollPayment
        {
            PayrollRunEmployeeId = fila.Id, PaidAt = new DateTime(2027, 1, 20), PaymentMethod = PayrollPaymentMethod.Transfer, PaidBy = "tesoreria@demo", CreatedBy = "test",
        });
        await e.D.Db.SaveChangesAsync();

        var bloqueada = await e.Reversor().Handle(new ReverseSeveranceCommand(calculo.Value.RunPublicId, "Error"), CancellationToken.None);
        bloqueada.Error.Code.Should().Be("Payroll.PaymentBlocksReversal");

        // Un borrador nuevo del mismo año no cabe mientras la aprobada viva; se comprueba el descarte sobre otra liquidación (corte anticipado) del año siguiente.
        e.D.HoyEs(new DateTime(2027, 7, 15));
        var otra = await e.Calculador().Handle(new CalculateSeveranceCommand(2027, new DateOnly(2027, 6, 30), [e.A.PublicId]), CancellationToken.None);
        otra.IsSuccess.Should().BeTrue(otra.Error.Message);
        var descarte = await e.Descartador().Handle(new DiscardSeveranceCommand(otra.Value.RunPublicId, "Corte de prueba"), CancellationToken.None);
        descarte.IsSuccess.Should().BeTrue(descarte.Error.Message);
        var descartada = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == otra.Value.RunPublicId);
        descartada.Status.Should().Be(PayrollRunStatus.Superseded);
        descartada.DiscardReason.Should().Be("Corte de prueba");
        descartada.DiscardedBy.Should().Be("contadora@demo");
    }
}
