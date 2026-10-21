using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionFileLines</c> (feature 013, K2; data-model §24.7): una obligación en el archivo de descuentos, con lo exigible del
/// período, lo vencido (si el convenio lo admite), su novedad frente al archivo base y la fecha de la libranza para la prelación (FR-111). Una
/// línea por obligación y archivo. Hecho de sólo inserción (<see cref="IHechoInmutable"/>) y <see cref="SinDiffDeAuditoriaAttribute"/>: la
/// auditoría lleva un evento resumen por archivo (C46). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class PayrollDeductionFileLine : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileId { get; init; }

    public int LineNumber { get; init; }

    public int LoanId { get; init; }

    public int LoanPayrollLinkId { get; init; }

    /// <summary>El deudor.</summary>
    public int PersonId { get; init; }

    /// <summary>Copia del vínculo (FR-109).</summary>
    public string? EmployeeIdAtPaymaster { get; init; }

    /// <summary>La cuota del período (bigint, C43); nula en una cancelación.</summary>
    public long? RepaymentInstallmentId { get; init; }

    public int? InstallmentNumber { get; init; }

    public DateOnly? DueDate { get; init; }

    /// <summary>Exigible del período.</summary>
    public decimal CurrentAmount { get; init; }

    /// <summary>Vencido, si el convenio lo admite (<c>IncludesPastDue</c>).</summary>
    public decimal PastDueAmount { get; init; }

    /// <summary><see cref="CurrentAmount"/> + <see cref="PastDueAmount"/>; cero en una cancelación.</summary>
    public decimal TotalAmount { get; init; }

    public PayrollNoveltyKind NoveltyKind { get; init; }

    /// <summary>Lo que pedía el archivo base; nulo en un alta.</summary>
    public decimal? PreviousAmount { get; init; }

    /// <summary>Fecha de la libranza (prelación cronológica, FR-111).</summary>
    public DateOnly LibranzaDate { get; init; }

    /// <summary>
    /// Feature 014 (T091, D36): el código de la pagaduría que agrupa la línea (su equivalencia), en un archivo con grupos; nulo sin grupos.
    /// </summary>
    public string? PaymasterConceptCode { get; init; }

    /// <summary>Feature 014 (T091): la prioridad de la línea dentro de su código, sellada al generar (el reparto por <c>Priority</c> la usa).</summary>
    public int? AllocationPriority { get; init; }
}
