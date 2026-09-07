using System.Text.Json;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Application.Payroll.Runs;

public sealed record RunTotalsDto(
    decimal Earnings,
    decimal Deductions,
    decimal EmployerContributions,
    decimal Provisions,
    decimal Net,
    decimal RoundingAdjustment);

public sealed record RunConceptTotalDto(string Code, string Name, string Nature, int Employees, decimal Amount);

public sealed record RunBlockerDto(Guid EmployeePublicId, string EmployeeName, string Flag, string Detail);

public sealed record RunSummaryDto(
    Guid PublicId,
    Guid PeriodPublicId,
    int Version,
    string Status,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? ReversedAt,
    string? ReversedBy,
    string? ReversalReason,
    int EmployeeCount,
    RunTotalsDto Totals,
    IReadOnlyList<RunConceptTotalDto> ByConcept,
    IReadOnlyList<RunBlockerDto> Blockers,
    int ChangedEmployees,
    string InputsHash,
    Guid? AccountingDocumentPublicId,
    string? AccountingDocumentNumber,
    Guid? ReversalAccountingDocumentPublicId,
    bool ApprovedWithoutSegregation,
    IReadOnlyList<ApprovalExceptionDto> Exceptions);

public sealed record RunEmployeeRowDto(
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    string EmployeeClass,
    int DaysWorked,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal TotalEmployerContributions,
    decimal TotalProvisions,
    decimal NetPay,
    IReadOnlyList<string> Flags,
    bool Changed,
    bool HasNotes);

public sealed record SalaryTrancheDto(DateTime From, DateTime To, int Days, int AbsenceDays, decimal MonthlySalary, int PaidDays);

public sealed record RunLineDto(
    Guid PublicId,
    string ConceptCode,
    string ConceptName,
    string Nature,
    decimal? Quantity,
    decimal? BaseAmount,
    decimal? Factor,
    decimal? RangeFrom,
    decimal? RangeTo,
    decimal Amount,
    bool AffectsAccounting,
    Guid? NoveltyPublicId,
    int Order,
    JsonElement Explanation);

public sealed record RunEmployeeDetailDto(
    Guid RunPublicId,
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    string EmployeeClass,
    int DaysWorked,
    IReadOnlyList<SalaryTrancheDto> SalaryTranches,
    IReadOnlyList<RunLineDto> Lines,
    RunTotalsDto Totals,
    IReadOnlyList<string> Flags,
    bool Changed,
    IReadOnlyList<ExplanationStep> Bases,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<string> Skips);

/// <summary>Lo que se guarda en <c>PayrollRunEmployee.NotesJson</c>.</summary>
public sealed record RunEmployeeNotes(IReadOnlyList<string> Refusals, IReadOnlyList<string> Skips);

/// <summary>Excepción autorizada a un bloqueo (FR-022): quién, qué bandera, por qué.</summary>
public sealed record ApprovalExceptionDto(Guid EmployeePublicId, string Flag, string Reason, string? AuthorizedBy = null, DateTime? AuthorizedAt = null);

public static class RunJson
{
    /// <summary>Sin escapar acentos: el JSON guardado lo lee gente, no sólo el programa.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
    };

    public static IReadOnlyList<string> FlagNames(RunEmployeeFlag flags) =>
        flags == RunEmployeeFlag.None
            ? []
            : Enum.GetValues<RunEmployeeFlag>().Where(f => f != RunEmployeeFlag.None && flags.HasFlag(f)).Select(f => f.ToString()).ToList();

    public static string FlagLabel(RunEmployeeFlag flag) => flag switch
    {
        RunEmployeeFlag.NegativeNet => "Neto negativo",
        RunEmployeeFlag.DeductionsOverMax => "Deducciones sobre el máximo legal (saldo diferido)",
        RunEmployeeFlag.MissingAffiliation => "Afiliación faltante (salud, pensión o ARL)",
        RunEmployeeFlag.ConceptWithoutAccounts => "Concepto sin cuentas contables",
        RunEmployeeFlag.WithholdingRateMissing => "Procedimiento 2 sin porcentaje de retención vigente",
        _ => flag.ToString(),
    };
}
