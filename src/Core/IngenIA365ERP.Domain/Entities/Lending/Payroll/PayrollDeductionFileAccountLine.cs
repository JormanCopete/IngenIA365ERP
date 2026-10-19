using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionFileAccountLines</c> (feature 014, T091; decisión del dueño del 2026-10-18, D36; contrato-de-cuentas §6.4; mensajes.md §5.4): una
/// cuota de una cuenta de aportes o de ahorro en el archivo de un grupo. Van las cuentas del asociado cuya empresa empleadora (su ficha) es la pagaduría
/// del convenio, con la cuota del período (y lo vencido si el convenio lo lleva), agrupadas por el código de la pagaduría que las cubre. Una línea por
/// cuota y archivo. Hecho de sólo inserción y sin diff: la auditoría lleva el evento resumen del archivo (C46). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class PayrollDeductionFileAccountLine : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileId { get; init; }

    /// <summary>Sigue a las de crédito: único dentro del archivo con ellas.</summary>
    public int LineNumber { get; init; }

    public int MemberAccountId { get; init; }

    /// <summary>La cuota (bigint, C43).</summary>
    public long AccountInstallmentId { get; init; }

    /// <summary>El titular.</summary>
    public int PersonId { get; init; }

    /// <summary>La identificación ante la pagaduría: la del vínculo de libranza de la persona con el convenio o, sin él, su documento.</summary>
    public string? EmployeeIdAtPaymaster { get; init; }

    public string PaymasterConceptCode { get; init; } = string.Empty;

    /// <summary>La prioridad de la línea dentro de su código, sellada al generar.</summary>
    public int AllocationPriority { get; init; }

    public DateOnly DueDate { get; init; }

    /// <summary>Lo pendiente de la cuota del período.</summary>
    public decimal CurrentAmount { get; init; }

    /// <summary>Lo pendiente de cuotas anteriores, si el convenio lo lleva.</summary>
    public decimal PastDueAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public PayrollNoveltyKind NoveltyKind { get; init; }

    /// <summary>Lo que pedía el archivo base del grupo para la cuenta; nulo en un alta.</summary>
    public decimal? PreviousAmount { get; init; }
}
