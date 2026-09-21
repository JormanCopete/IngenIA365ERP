using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;

namespace IngenIA365ERP.Application.Payroll.Dispersion;

/// <summary>Errores de la dispersión bancaria (feature 010, US8; contracts/api.md §9).</summary>
public static class DisbursementErrors
{
    public static readonly Error RunNotFound = new("Payroll.RunNotFound", "No existe la corrida indicada.");
    public static readonly Error RunNotApproved = new("Payroll.Disbursement.RunNotApproved", "El archivo de dispersión se genera desde una liquidación aprobada.");
    public static readonly Error FileNotFound = new("Payroll.Disbursement.FileNotFound", "No existe el archivo de dispersión indicado.");
    public static readonly Error FormatNotFound = new("Payroll.Disbursement.FormatNotFound", "No existe el formato indicado.");
    public static Error NoFormat(string? banco) =>
        new ErrorConDatos("Payroll.Disbursement.NoFormat",
            banco is null
                ? "No hay ningún formato de dispersión vigente para la fecha de pago. Cargue uno en Maestros › Formatos bancarios o elija uno genérico."
                : $"El banco {banco} no tiene un formato de dispersión vigente para la fecha de pago. Cargue uno en Maestros › Formatos bancarios o elija uno genérico.",
            new { bank = banco });
    public static Error FormatNotCurrent(string code, DateOnly desde, DateOnly? hasta) =>
        new ErrorConDatos("Payroll.Disbursement.FormatNotCurrent",
            $"El formato {code} no está vigente para la fecha de pago (rige del {desde:dd/MM/yyyy} al {(hasta is { } h ? h.ToString("dd/MM/yyyy") : "sin fin")}).",
            new { code, validFrom = desde, validTo = hasta });
    public static readonly Error FormatScopeMismatch = new("Payroll.Disbursement.FormatScopeMismatch", "Ese formato no es de dispersión de nómina.");
    public static readonly Error SourceAccountRequired = new("Payroll.Disbursement.SourceAccountRequired",
        "El formato escribe la cuenta origen: elija la cuenta bancaria de la cooperativa desde la que se paga.");
    public static readonly Error SourceAccountNotFound = new("Payroll.Disbursement.SourceAccountNotFound", "La cuenta origen no existe o no es una cuenta bancaria del plan.");
    public static Error SourceAccountBankMismatch(string cuenta, string bancoCuenta, string bancoFormato) =>
        new ErrorConDatos("Payroll.Disbursement.SourceAccountBankMismatch",
            $"La cuenta {cuenta} es de {bancoCuenta} y el formato es de {bancoFormato}: elija una cuenta de ese banco o un formato genérico.",
            new { account = cuenta, accountBank = bancoCuenta, formatBank = bancoFormato });
    public static readonly Error NothingToPay = new("Payroll.Disbursement.NothingToPay",
        "Ningún empleado de la corrida puede ir al archivo: todos están pagados, ya en otro archivo o sin datos bancarios (vea la lista de pendientes).");
    public static Error LineTooLong(int lineNumber, string field, string empleado) =>
        new ErrorConDatos("Payroll.Disbursement.LineTooLong",
            $"La línea {lineNumber} ({empleado}) no cabe en el formato: el campo «{field}» es más largo que su posición y el formato no lo recorta.",
            new { lineNumber, field, employee = empleado });
    public static Error NotGenerated(string status) =>
        new ErrorConDatos("Payroll.Disbursement.NotGenerated", $"El archivo está {status}: sólo un archivo generado se marca enviado o se anula.", new { status });
    public static readonly Error CompanyMissing = new("Payroll.Disbursement.CompanyMissing", "La cooperativa no tiene empresa registrada (NIT y razón social) para la cabecera del archivo.");
    public static Error PaymentsAlreadyMarked(IReadOnlyList<Guid> employeePublicIds) =>
        new ErrorConDatos("Payroll.Disbursement.PaymentsAlreadyMarked",
            $"{employeePublicIds.Count} empleado(s) del archivo ya tenían marca de pago vigente: se dejaron como estaban.",
            new { employeePublicIds });
}

/// <summary>Un empleado que no entró al archivo y por qué (queda en «pendientes»).</summary>
public sealed record DisbursementExcludedDto(Guid EmployeePublicId, string Name, string ReasonCode, string Reason, decimal NetPay);

public sealed record DisbursementLineDto(int LineNumber, Guid EmployeePublicId, string Name, string Document, string? BankName, string? AccountType, string? AccountNumber, decimal Amount, string RecordText, bool Paid, Guid? PaymentPublicId);

public sealed record DisbursementFileSummaryDto(
    Guid FilePublicId, Guid RunPublicId, string RunLabel, string RunKind, string Status, string FormatCode, string FormatName,
    Guid? BankPublicId, string? BankName, DateOnly PaymentDate, int Sequence, string? Reference, int LineCount, decimal TotalAmount,
    int ExcludedCount, string FileName, string FileSha256, DateTime GeneratedAt, string GeneratedBy,
    DateTime? SentAt, string? SentBy, string? BankReference, DateTime? VoidedAt, string? VoidedBy, string? VoidReason);

public sealed record DisbursementFileDetailDto(
    DisbursementFileSummaryDto Summary,
    IReadOnlyList<DisbursementLineDto> Lines,
    IReadOnlyList<DisbursementExcludedDto> Excluded,
    string? SourceAccountNumber,
    Guid? FileAttachmentPublicId);

public sealed record DisbursementGeneratedDto(Guid FilePublicId, string FileName, int LineCount, decimal TotalAmount, IReadOnlyList<DisbursementExcludedDto> Excluded, IReadOnlyList<WarningDto> Warnings);

public sealed record DisbursementPreviewDto(string FileName, string ContentType, IReadOnlyList<string> Lines, int LineCount, decimal TotalAmount, IReadOnlyList<DisbursementExcludedDto> Excluded, IReadOnlyList<BankFileFormatError> FormatErrors);

public sealed record DisbursementDownloadDto(string FileName, string ContentType, byte[] Content, string Encoding);

public sealed record DisbursementSentDto(Guid FilePublicId, int MarkedPaid, IReadOnlyList<Guid> AlreadyMarked, DateTime PaidAt, string Reference);
