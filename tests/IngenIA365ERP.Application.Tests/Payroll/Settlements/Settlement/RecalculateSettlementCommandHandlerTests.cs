using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// Feature 010, US3 (T065): recalcular crea la versión siguiente, deja la anterior reemplazada,
/// vuelve a leer Cartera, conserva los ajustes cuyo propuesto no cambió y repropone —con aviso— los
/// que sí cambiaron; el descuento que Cartera ya no trae se retira.
/// </summary>
public class RecalculateSettlementCommandHandlerTests
{
    [Fact]
    public async Task Recalcular_conserva_el_ajuste_si_el_saldo_no_cambio_y_repropone_con_aviso_si_cambio()
    {
        var p = new DefinitivaDePrueba();
        var v1 = await p.RegistrarAnaAsync();
        (await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(v1.RunPublicId, p.Descuento(v1, 1001).ObligationPublicId, 1_000_000m, "Refinancia el resto"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(v1.RunPublicId, p.Descuento(v1, 1002).ObligationPublicId, 100_000m, "Paga el resto por caja"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        // El asociado abonó al crédito 1002 después de registrar: su saldo en Cartera cambió.
        p.Credito1002.CurrentBalance = 250_000m;
        p.Credito1002.CapitalBalanceCurrent = 250_000m;
        await p.D.Db.SaveChangesAsync();

        var r = await p.Recalcular().Handle(new RecalculateSettlementCommand(v1.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Version.Should().Be(2);
        r.Value.RunPublicId.Should().NotBe(v1.RunPublicId);
        r.Value.TerminationPublicId.Should().Be(v1.TerminationPublicId, "misma terminación, otra versión de la corrida");

        var corridas = await p.D.Db.PayrollRuns.Where(x => x.Kind == PayrollRunKind.Settlement).OrderBy(x => x.Version).ToListAsync();
        corridas.Select(c => (c.Version, c.Status)).Should().Equal((1, PayrollRunStatus.Superseded), (2, PayrollRunStatus.Draft));

        // 1001: propuesto igual → conserva el ajuste con su motivo. 1002: propuesto cambió → vuelve al saldo y avisa.
        var d1001 = p.Descuento(r.Value, 1001);
        d1001.ObligationPublicId.Should().Be(p.Descuento(v1, 1001).ObligationPublicId, "la misma fila, no otra");
        d1001.Proposed.Should().Be(1_500_000m);
        d1001.Applied.Should().Be(1_000_000m);
        d1001.Reason.Should().Be("Refinancia el resto");
        d1001.Status.Should().Be(SettlementDeductionStatus.Adjusted);
        var d1002 = p.Descuento(r.Value, 1002);
        d1002.Proposed.Should().Be(250_000m);
        d1002.Applied.Should().Be(250_000m);
        d1002.Reason.Should().BeNull();
        d1002.Status.Should().Be(SettlementDeductionStatus.Proposed);
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Payroll.Settlement.DeductionReproposed").Which.Message.Should().Contain("1002");
        (await p.D.Db.SettlementDeductions.CountAsync()).Should().Be(3, "ni duplica ni pierde filas");

        // Las líneas de la versión nueva llevan lo aplicado y siguen enlazadas.
        var lineas = await p.D.Db.PayrollRunLines.Include(l => l.RunEmployee).Where(l => l.RunEmployee!.Run!.PublicId == r.Value.RunPublicId && l.SettlementDeductionId != null).ToListAsync();
        lineas.Should().HaveCount(3);
        lineas.Where(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction).Sum(l => l.Amount).Should().Be(1_000_000m + 250_000m);
    }

    [Fact]
    public async Task El_credito_que_Cartera_ya_no_trae_se_retira_de_la_propuesta()
    {
        var p = new DefinitivaDePrueba();
        var v1 = await p.RegistrarAnaAsync();
        p.Credito1002.CurrentBalance = 0m;
        p.Credito1002.ClosingDate = new DateOnly(2026, 9, 16);
        await p.D.Db.SaveChangesAsync();

        var r = await p.Recalcular().Handle(new RecalculateSettlementCommand(v1.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Deductions.Items.Should().HaveCount(2);
        r.Value.Deductions.Items.Should().NotContain(i => i.Description.Contains("Crédito 1002"));
        (await p.D.Db.SettlementDeductions.IgnoreQueryFilters().CountAsync(d => d.IsDeleted)).Should().Be(1, "retiro en blando, no borrado");
    }

    [Fact]
    public async Task Sobre_una_version_reemplazada_o_una_aprobada_no_se_recalcula()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        var v1 = await p.RegistrarAnaAsync();
        var v2 = await p.Recalcular().Handle(new RecalculateSettlementCommand(v1.RunPublicId), CancellationToken.None);
        v2.IsSuccess.Should().BeTrue(v2.Error.Message);

        (await p.Recalcular().Handle(new RecalculateSettlementCommand(v1.RunPublicId), CancellationToken.None)).Error.Code.Should().Be("Payroll.Settlement.NotDraft");

        (await p.Aprobar().Handle(new ApproveSettlementCommand(v2.Value.RunPublicId, true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await p.Recalcular().Handle(new RecalculateSettlementCommand(v2.Value.RunPublicId), CancellationToken.None)).Error.Code.Should().Be("Payroll.Settlement.NotDraft");
    }
}
