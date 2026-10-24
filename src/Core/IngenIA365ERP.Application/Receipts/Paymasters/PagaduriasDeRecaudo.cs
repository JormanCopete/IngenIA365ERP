using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

/// <summary>Una línea del archivo de descuentos en Recaudo. (nuevo)</summary>
public sealed record PayrollDeductionFileLineDto(Guid PublicId, Guid DeductionFileLinePublicId, PayrollDeductionLineKind Kind, Guid ProductPublicId, string ProductNumber,
    int? InstallmentNumber, Guid PersonPublicId, string PersonName, string PayerEmployeeId, string? PaymasterConceptCode, DateOnly DueDate, decimal Amount,
    decimal PastDueAmount, PaymasterDeductionOutcome? DeductionOutcome, decimal? DeductedAmount, DateOnly? DeductionDate, decimal RemittedAmount)
{
    public decimal PendingRemittance => Math.Max(0m, (DeductedAmount ?? 0m) - RemittedAmount);
}

/// <summary>El archivo de descuentos de una pagaduría como lo ve Recaudo, con sus totales. (nuevo)</summary>
public sealed record PayrollDeductionFileDto(
    Guid PublicId,
    Guid FilePublicId,
    string FileNumber,
    int FileVersion,
    short MessageVersion,
    Guid PaymasterPersonPublicId,
    string PaymasterName,
    string AgreementCode,
    int AgreementVersion,
    string? FileGroupCode,
    int PeriodYear,
    int PeriodMonth,
    int? SubPeriod,
    string FileName,
    string FileSha256,
    DateOnly SendBy,
    int LineCount,
    decimal Amount,
    decimal PastDueAmount,
    PayrollDeductionFileState Status,
    DateTime? SentAt,
    string? SentReference,
    IReadOnlyList<PayrollDeductionFileLineDto> Lines);

/// <summary>Una línea de la respuesta de la pagaduría. (nuevo)</summary>
public sealed record PaymasterResponseLineDto(int LineNumber, Guid? DeductionFileLinePublicId, string? PayerEmployeeId, string? PaymasterConceptCode, string? ProductNumber,
    decimal Expected, decimal Deducted, DateOnly? DeductionDate, PaymasterDeductionOutcome Outcome, string? PaymasterResultCode, string? Reason, bool Unmatched);

/// <summary>Una versión de la respuesta de la pagaduría con su estado ante Cartera. (nuevo)</summary>
public sealed record PaymasterResponseDto(Guid PublicId, Guid FilePublicId, int Version, PaymasterResponseSource Source, string? FileName, PaymasterResponseState Status,
    short? EmittedMessageVersion, string? RejectionCode, string? RejectionDataJson, int LineCount, decimal Expected, decimal Deducted, int Unmatched, DateTime LoadedAt,
    IReadOnlyList<PaymasterResponseLineDto> Lines);

/// <summary>Lo que se ve de la respuesta agrupado por concepto de la pagaduría (v2) o por línea (v1). (nuevo)</summary>
public sealed record PaymasterResponsePreviewDto(Guid ResponsePublicId, string GroupBy, IReadOnlyList<PaymasterResponsePreviewRowDto> Rows, decimal Expected, decimal Deducted,
    int Unmatched);

public sealed record PaymasterResponsePreviewRowDto(string Key, int Lines, decimal Expected, decimal Deducted, int Unmatched);

/// <summary>
/// Lo común de las pagadurías de Recaudo (feature 015, US7; FR-075, FR-175): el archivo visible con el alcance por pagaduría (<c>RCD_UserPaymasterScopes</c>) —fuera de él, el 404
/// genérico— y los DTO. (nuevo)
/// </summary>
public static class PagaduriasDeRecaudo
{
    public static async Task<PayrollDeductionFile?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.ReceiptsPayrollDeductionFiles : db.ReceiptsPayrollDeductionFiles.AsNoTracking();
        var f = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        return f is not null && (await alcance.ObtenerAsync(ct)).IncluyePagaduria(f.PaymasterPersonId) ? f : null;
    }

    public static async Task<PayrollDeductionFileDto> DtoAsync(IApplicationDbContext db, PayrollDeductionFile f, bool conLineas, CancellationToken ct)
    {
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == f.PaymasterPersonId)
            .Select(p => new { p.PublicId, Nombre = p.BusinessName ?? (p.FirstName + " " + p.LastName) }).FirstAsync(ct);
        var envio = await db.PayrollFileSendings.AsNoTracking().Where(s => s.PayrollDeductionFileId == f.Id).Select(s => new { s.SentAt, s.Reference }).FirstOrDefaultAsync(ct);
        var lineas = conLineas
            ? await (from l in db.ReceiptsPayrollDeductionFileLines.AsNoTracking()
                     join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.PersonId equals p.Id
                     where l.PayrollDeductionFileId == f.Id
                     orderby l.Id
                     select new PayrollDeductionFileLineDto(l.PublicId, l.DeductionFileLinePublicId, l.Kind, l.ProductPublicId, l.ProductNumber, l.InstallmentNumber, p.PublicId,
                         (p.FirstName + " " + p.LastName).Trim(), l.PayerEmployeeId, l.PaymasterConceptCode, l.DueDate, l.Amount, l.PastDueAmount, l.DeductionOutcome,
                         l.DeductedAmount, l.DeductionDate, l.RemittedAmount)).ToListAsync(ct)
            : [];
        return new PayrollDeductionFileDto(f.PublicId, f.FilePublicId, f.FileNumber, f.FileVersion, f.MessageVersion, pagaduria.PublicId, pagaduria.Nombre.Trim(),
            f.AgreementCode, f.AgreementVersion, f.FileGroupCode, f.PeriodYear, f.PeriodMonth, f.SubPeriod, f.FileName, f.FileSha256, f.SendBy, f.LineCount, f.Amount,
            f.PastDueAmount, f.Status, envio?.SentAt, envio?.Reference, lineas);
    }

    public static async Task<PaymasterResponseDto> DtoAsync(IApplicationDbContext db, PaymasterResponse r, Guid archivo, CancellationToken ct)
    {
        var lineas = await db.PaymasterResponseLines.AsNoTracking().Where(l => l.PaymasterResponseId == r.Id).OrderBy(l => l.LineNumber)
            .Select(l => new PaymasterResponseLineDto(l.LineNumber, l.DeductionFileLinePublicId, l.PayerEmployeeId, l.PaymasterConceptCode, l.ProductNumber, l.Expected,
                l.Deducted, l.DeductionDate, l.Outcome, l.PaymasterResultCode, l.Reason, l.Unmatched)).ToListAsync(ct);
        return new PaymasterResponseDto(r.PublicId, archivo, r.Version, r.Source, r.FileName, r.Status, r.EmittedMessageVersion, r.RejectionCode, r.RejectionDataJson,
            r.LineCount, r.Expected, r.Deducted, r.Unmatched, r.LoadedAt, lineas);
    }

    public static string Clave(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}
