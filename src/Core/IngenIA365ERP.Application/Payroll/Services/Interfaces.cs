using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Marca como desactualizada (<c>Stale</c>) la corrida en borrador de un período cuando
/// cambia un insumo que influye en ella: novedad, salario, concepto o parámetro (FR-016).
/// <b>No guarda</b>: el comando que provocó el cambio guarda en su misma transacción, así
/// que o quedan las dos cosas o ninguna.
/// </summary>
public interface IPayrollRunStaleMarker
{
    /// <summary>Corridas <c>Draft</c> del período que pasan a <c>Stale</c> (0 ó 1).</summary>
    Task<int> MarkStaleAsync(int payPeriodId, string reason, CancellationToken ct);

    /// <summary>Toda corrida <c>Draft</c> viva pasa a <c>Stale</c> (cambió un concepto o un parámetro legal).</summary>
    Task<int> MarkAllDraftsStaleAsync(string reason, CancellationToken ct);
}

/// <summary>
/// Permiso del usuario actual dentro de un handler. Los endpoints exigen el permiso de la
/// operación; esto es para decisiones que dependen de un permiso adicional dentro de la
/// misma operación (autorizar una excepción al aprobar, FR-022). La implementación vive
/// en Identity.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct);
}

// ------------------------------------------------------------------ carga masiva --

public sealed record NoveltyFileRow(
    int Row,
    string EmployeeDocument,
    string ConceptCode,
    decimal? Quantity,
    decimal? Amount,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Notes);

public sealed record NoveltyFileError(int Row, string Column, string Message);

public sealed record NoveltyFileParseResult(IReadOnlyList<NoveltyFileRow> Rows, IReadOnlyList<NoveltyFileError> Errors);

/// <summary>
/// Lee el archivo de novedades (CSV con <c>;</c>, UTF-8, encabezados en español; D-13).
/// Sólo formato: las reglas de negocio las aplica el comando de importación fila por fila.
/// Implementado en Storage con CsvHelper.
/// </summary>
public interface INoveltyFileParser
{
    NoveltyFileParseResult Parse(Stream content);

    /// <summary>La plantilla descargable, con los encabezados que el parser espera.</summary>
    byte[] Template();
}

// ------------------------------------------------------------- comprobante de pago --

public sealed record PayslipLineModel(string Code, string Name, ConceptNature Nature, decimal? Quantity, decimal Amount, string Summary);

/// <summary>Todo lo que el comprobante de pago imprime. Lo arma Application; lo pinta el renderizador (QuestPDF, en la API).</summary>
public sealed record PayslipModel(
    string CooperativeName,
    string? CooperativeTaxId,
    string EmployeeName,
    string EmployeeDocument,
    string? EmployeePosition,
    string PlanName,
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int RunVersion,
    DateTime? ApprovedAt,
    int DaysWorked,
    decimal MonthlySalary,
    IReadOnlyList<PayslipLineModel> Earnings,
    IReadOnlyList<PayslipLineModel> Deductions,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    string? BankName,
    string? BankAccount,
    string? PaymentStatus,
    DateTime GeneratedAt);

public interface IPayslipPdfRenderer
{
    byte[] Render(PayslipModel payslip);
    byte[] RenderMany(IReadOnlyList<PayslipModel> payslips);
}

public sealed record PayslipRecipientDto(Guid EmployeePublicId, string EmployeeName);
public sealed record PayslipFailureDto(Guid EmployeePublicId, string EmployeeName, string Error);

public sealed record PayslipDispatchResult(
    int Sent,
    int Failed,
    IReadOnlyList<PayslipRecipientDto> WithoutEmail,
    IReadOnlyList<PayslipFailureDto> Failures);

/// <summary>
/// Envía los comprobantes de una corrida aprobada, uno a uno, registrando cada intento en
/// <c>PAY_PayslipDeliveries</c>. Nunca se dispara solo (FR-026): lo invoca el comando de
/// envío manual.
/// </summary>
public interface IPayslipEmailDispatcher
{
    Task<PayslipDispatchResult> DispatchAsync(Guid runPublicId, IReadOnlyList<Guid>? employeePublicIds, CancellationToken ct);
}

// -------------------------------------------- documento de liquidación definitiva --

/// <summary>Un rubro del documento: código, nombre, base y días con los que se calculó, y el valor.</summary>
public sealed record SettlementDocumentLineModel(string Code, string Name, ConceptNature Nature, decimal? BaseAmount, decimal? Days, decimal Amount, string Summary);

/// <summary>Un descuento del documento: obligación, propuesto, aplicado y motivo si se bajó.</summary>
public sealed record SettlementDocumentDeductionModel(string Description, decimal Proposed, decimal Applied, string? Reason, decimal? RemainingAfter);

/// <summary>
/// Todo lo que el documento de liquidación definitiva para firma imprime (feature 010, FR-020).
/// Lo arma Application (<c>SettlementDocumentModelBuilder</c>); lo pinta el renderizador QuestPDF de
/// la API. Antes de aprobar sale con la marca «BORRADOR».
/// </summary>
public sealed record SettlementDocumentModel(
    string CooperativeName,
    string? CooperativeTaxId,
    string EmployeeName,
    string EmployeeDocumentType,
    string EmployeeDocument,
    string? EmployeePosition,
    DateTime HireDate,
    DateOnly TerminationDate,
    string ReasonName,
    string? ReasonLegalBasis,
    bool GeneratesSeverancePay,
    string ContractType,
    DateOnly? ContractEndDate,
    decimal BaseSalary,
    int DaysOfService,
    IReadOnlyList<SettlementDocumentLineModel> Earnings,
    IReadOnlyList<SettlementDocumentLineModel> Deductions,
    IReadOnlyList<SettlementDocumentDeductionModel> PortfolioDeductions,
    IReadOnlyList<string> Omitted,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    bool IsDraft,
    int RunVersion,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    string? AccountingDocumentNumber,
    DateTime GeneratedAt);

/// <summary>Pinta el documento de liquidación definitiva. Vive en la API (QuestPDF sólo se conoce allí), como <see cref="IPayslipPdfRenderer"/>.</summary>
public interface ISettlementDocumentRenderer
{
    byte[] Render(SettlementDocumentModel document);
}
