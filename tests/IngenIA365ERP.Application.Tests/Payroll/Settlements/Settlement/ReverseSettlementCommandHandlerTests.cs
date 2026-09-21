using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// Feature 010, US3 (FR-020, T065): reversar la definitiva deja el espejo, reabre la ficha, marca la
/// terminación como reintegro y los descuentos como reversados, y lista los recaudos de Cartera que
/// hay que reversar allá; descartar el borrador anula la terminación sin tocar la ficha.
/// </summary>
public class ReverseSettlementCommandHandlerTests
{
    [Fact]
    public async Task Reversar_reabre_la_ficha_reintegra_la_terminacion_y_lista_los_recaudos_de_Cartera()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var persona = await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId);
        persona.IsEmployee = true;
        await p.D.Db.SaveChangesAsync();
        var t = await p.RegistrarAnaAsync();
        var aprobada = await p.Aprobar().Handle(new ApproveSettlementCommand(t.RunPublicId, true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);

        var r = await p.Reversar().Handle(new ReverseSettlementCommand(t.RunPublicId, "Se equivocó la fecha de retiro"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.ReversalNumber.Should().StartWith("NM-");
        r.Value.PortfolioPayments.Should().HaveCount(2, "los dos recaudos que la aprobación dejó en Cartera");
        r.Value.PortfolioPayments.Should().OnlyContain(x => x.PaymentPublicId != null && x.Applied > 0m);
        r.Value.Message.Should().Contain("no se reversan desde aquí");

        var ficha = await p.D.Db.Employees.SingleAsync(e => e.Id == p.D.Ana.Id);
        ficha.Status.Should().Be(1);
        ficha.TerminationDate.Should().Be(DateTime.MaxValue.Date);
        ficha.TerminationCause.Should().BeNull();
        (await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId)).IsEmployee.Should().BeTrue();

        var terminacion = await p.D.Db.EmploymentTerminations.Include(x => x.Deductions).SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Reinstated);
        terminacion.ReinstateReason.Should().Be("Se equivocó la fecha de retiro");
        terminacion.ReinstatedBy.Should().Be("contadora@demo");
        terminacion.Deductions.Should().OnlyContain(d => d.Status == SettlementDeductionStatus.Reverted);

        var run = await p.D.Db.PayrollRuns.Include(x => x.ReversalAccountingDocument).SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Reversed);
        run.ReversalAccountingDocument.Should().NotBeNull();
        (await p.D.Db.VacationMovements.SingleAsync(m => m.PayrollRunId == run.Id)).Status.Should().Be(VacationMovementStatus.Cancelled);

        await p.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollEmployeeReinstated), Arg.Any<CancellationToken>());

        // Reintegrado, se puede volver a terminar: fila nueva.
        var otra = await p.Registrar().Handle(new Application.Payroll.Terminations.RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "RENUNCIA", DianContractType.Indefinite), CancellationToken.None);
        otra.IsSuccess.Should().BeTrue(otra.Error.Message);
        otra.Value.TerminationPublicId.Should().NotBe(t.TerminationPublicId);
        otra.Value.Version.Should().Be(2, "misma llave (empleado y corte): versión siguiente");
    }

    [Fact]
    public async Task Reversar_un_borrador_o_sin_motivo_se_rechaza()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();

        (await p.Reversar().Handle(new ReverseSettlementCommand(t.RunPublicId, "x"), CancellationToken.None)).Error.Code.Should().Be("Payroll.Settlement.NotApproved");
        (await p.Reversar().Handle(new ReverseSettlementCommand(t.RunPublicId, " "), CancellationToken.None)).Error.Code.Should().Be("Payroll.Settlement.ReasonRequired");
    }

    [Fact]
    public async Task Descartar_el_borrador_anula_la_terminacion_y_la_ficha_nunca_se_toco()
    {
        var p = new DefinitivaDePrueba();
        var t = await p.RegistrarAnaAsync();

        var r = await p.Descartar().Handle(new DiscardSettlementCommand(t.RunPublicId, "Todavía no se va"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var run = await p.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Superseded);
        run.DiscardReason.Should().Be("Todavía no se va");
        var terminacion = await p.D.Db.EmploymentTerminations.SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Cancelled);
        terminacion.Notes.Should().Contain("Descartada");
        (await p.D.Db.Employees.SingleAsync(e => e.Id == p.D.Ana.Id)).Status.Should().Be(1);

        // Anulada, se puede registrar otra terminación.
        (await p.Registrar().Handle(new Application.Payroll.Terminations.RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "RENUNCIA", DianContractType.Indefinite), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }
}
