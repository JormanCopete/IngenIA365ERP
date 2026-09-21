using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Pila;

/// <summary>Errores de la PILA (feature 010, US5; contracts/api.md §7).</summary>
public static class PilaErrors
{
    public static Error BlockingIssues(IReadOnlyList<PilaIssueDto> issues) =>
        new ErrorConDatos("Payroll.Pila.BlockingIssues", $"La planilla tiene {issues.Count(i => i.Severity == PilaIssueSeverity.Blocking)} inconsistencia(s) bloqueante(s): corríjalas y vuelva a validar.", new { issues });
    public static readonly Error NoApprovedRuns = new("Payroll.Pila.NoApprovedRuns", "No hay corridas aprobadas imputadas a ese mes: apruebe la nómina antes de generar la planilla.");
    public static readonly Error LayoutMissing = new("Payroll.Pila.LayoutMissing", "No hay un layout de la Resolución 2388 vigente para ese período.");
    public static Error ParametersMissing(IReadOnlyList<string> codes) =>
        new ErrorConDatos("Payroll.Pila.ParametersMissing", $"Sin vigencia de los parámetros legales: {string.Join(", ", codes)}.", new { codes });
    public static Error SettingsIncomplete(IReadOnlyList<string> missing) =>
        new ErrorConDatos("Payroll.Pila.SettingsIncomplete", $"Faltan datos del aportante: {string.Join(", ", missing)}.", new { missing });
    public static readonly Error WarningsNotAcknowledged = new("Payroll.Pila.WarningsNotAcknowledged", "La planilla tiene alertas: revíselas y confirme que las reconoce para generar.");
    public static Error AlreadyUploaded(Guid generationPublicId) =>
        new ErrorConDatos("Payroll.Pila.AlreadyUploaded", "La planilla vigente de ese período ya se marcó cargada en el operador: una corrección se digita allí (planilla N).", new { generationPublicId });
    public static Error NotGenerated(string status) =>
        new ErrorConDatos("Payroll.Pila.NotGenerated", $"La generación está {status}: sólo una planilla generada tiene archivo y se marca cargada.", new { status });
    public static readonly Error GenerationNotFound = new("Payroll.Pila.GenerationNotFound", "No existe la generación indicada.");
    public static Error Unreconciled(object reconciliation) =>
        new ErrorConDatos("Payroll.Pila.Unreconciled", "Los aportes del archivo no cuadran con los comprobantes de nómina del mes: revise la diferencia por subsistema antes de descargar, o descargue reconociéndola.", new { reconciliation });
    public static readonly Error LineNotFound = new("Payroll.Pila.LineNotFound", "No existe esa línea en la generación.");
    public static readonly Error CompanyMissing = new("Payroll.Pila.CompanyMissing", "La cooperativa no tiene empresa registrada (NIT y razón social) para la cabecera de la planilla.");
}

public sealed record PilaSettingsDto(
    string ContributorType, string ContributorClass, string PresentationForm, string? BranchCode, string? BranchName,
    string? ArlPilaCode, string? EconomicActivityCode, string? DivipolaDepartment, string? DivipolaMunicipality,
    string? OperatorCode, string? OperatorName, string PlanillaType, string? NitLastTwoDigits, bool Complete, IReadOnlyList<string> Missing);

public sealed record PilaSettingsInput(
    string ContributorType, string ContributorClass, string PresentationForm, string? BranchCode, string? BranchName,
    string? ArlPilaCode, string? EconomicActivityCode, string? DivipolaDepartment, string? DivipolaMunicipality,
    string? OperatorCode, string? OperatorName);

public sealed record PilaLayoutDto(string Code, string Version, DateOnly ValidFrom, DateOnly? ValidTo, int Type1Fields, int Type1Length, int Type2Fields, int Type2Length, string Source, bool Verified, int UnverifiedFields);

public sealed record PilaDueDateDto(DateOnly? DueDate, string Rule, string NitDigits, string? Note);

public sealed record PilaIssueDto(PilaIssueSeverity Severity, string Code, byte? Field, string Message, Guid? EmployeePublicId, string? EmployeeName, string? Link);

public sealed record PilaSourceRunDto(Guid RunPublicId, string Kind, string Status, int Version, string Label);

public sealed record PilaValidationDto(bool CanGenerate, int Blocking, int Warnings, IReadOnlyList<PilaIssueDto> Issues, IReadOnlyList<PilaSourceRunDto> Sources, int Contributors, int Lines);

public sealed record PilaTotalsDto(decimal Pension, decimal Health, decimal Arl, decimal Ccf, decimal Sena, decimal Icbf, decimal Fsp, decimal Total);

public sealed record PilaReconciliationRowDto(string Subsystem, decimal FileTotal, decimal LedgerTotal, decimal Difference);

public sealed record PilaReconciliationDto(IReadOnlyList<PilaReconciliationRowDto> BySubsystem, bool Balanced, string? Note);

public sealed record PilaGeneratedDto(Guid GenerationPublicId, int Version, PilaGenerationStatus Status, string? FileName, int Contributors, int Lines, PilaTotalsDto Totals, PilaReconciliationDto Reconciliation, IReadOnlyList<PilaIssueDto> Issues, DateOnly? DueDate);

public sealed record PilaGenerationSummaryDto(
    Guid GenerationPublicId, string Period, short Year, byte Month, int Version, PilaGenerationStatus Status, string LayoutCode,
    DateTime GeneratedAt, string GeneratedBy, PilaTotalsDto Totals, bool Balanced, int Contributors, int Lines, int Blocking, int Warnings,
    string? FileName, DateOnly? DueDate, DateTime? UploadedAt, string? UploadedBy, string? OperatorReference, DateOnly? OperatorFilingDate, DateOnly? PaidAt);

public sealed record PilaLineDto(
    int LineNumber, Guid EmployeePublicId, string EmployeeName, string Document, string ContributorType, string SubType, IReadOnlyList<string> Novelties,
    int DaysPension, int DaysHealth, int DaysArl, int DaysCcf, decimal Salary,
    decimal IbcPension, decimal IbcHealth, decimal IbcArl, decimal IbcCcf,
    decimal Pension, decimal Fsp, decimal Health, decimal Arl, decimal Ccf, decimal Sena, decimal Icbf, decimal Total, bool Exempt,
    IReadOnlyDictionary<string, string> Fields);

public sealed record PilaGenerationDetailDto(PilaGenerationSummaryDto Summary, IReadOnlyList<PilaLineDto> Lines, IReadOnlyList<PilaIssueDto> Issues, PilaReconciliationDto Reconciliation, IReadOnlyList<PilaSourceRunDto> Sources, bool ExemptionApplied);

public sealed record PilaFieldExplanationDto(int Field, string Name, string Value, string Source, string? Detail, decimal? Amount);

public sealed record PilaLineExplanationDto(int LineNumber, Guid EmployeePublicId, string EmployeeName, string RecordText, IReadOnlyList<PilaFieldExplanationDto> Fields);

public sealed record PilaDownloadDto(string FileName, string ContentType, byte[] Content, string Encoding);

public sealed record PilaUploadedDto(Guid GenerationPublicId, PilaGenerationStatus Status, DateTime UploadedAt, string OperatorReference);
