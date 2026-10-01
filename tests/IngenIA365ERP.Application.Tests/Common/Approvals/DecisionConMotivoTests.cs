using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// Feature 012, T420 (US12-4, SC-012): el motivo de un rechazo va a la auditoría como el de cualquier operación con motivo
/// (<see cref="IConMotivo"/>, que <c>AuditoriaEncadenada.ContextoAsync</c> copia a la metadata <c>Reason</c>). Lo destapó la e2e
/// de auditoría completa del cierre de I1: la decisión quedaba auditada sin el motivo con que se rechazó. (nuevo)
/// </summary>
public class DecisionConMotivoTests
{
    [Fact]
    public void El_motivo_de_la_decision_llega_a_la_auditoria()
    {
        var rechazo = new DecideApprovalCommand(Guid.NewGuid(), ApprovalDecisionKind.Reject, "Merma sin soporte", ApprovalMethod.OwnSession, null, "abc");

        rechazo.Should().BeAssignableTo<IConMotivo>().Which.Reason.Should().Be("Merma sin soporte");
    }

    [Fact]
    public void Sin_motivo_la_auditoria_no_recibe_uno_inventado()
    {
        IConMotivo aprobacion = new DecideApprovalCommand(Guid.NewGuid(), ApprovalDecisionKind.Approve, null, ApprovalMethod.OwnSession, null, "abc");

        aprobacion.Reason.Should().BeEmpty();
    }
}
