using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Files;

// Feature 013, K2 (US10, bloque B; T0836; api.md §13.2): las consultas del archivo de descuentos. Un archivo se ve si alguna de sus líneas es de un
// crédito del alcance de oficina y línea de quien pregunta, y de él sólo esas líneas; fuera de alcance, el mismo 404 que si no existiera. (nuevo)

/// <summary>Un archivo de descuentos (api.md §13.2). (nuevo)</summary>
public sealed record PayrollDeductionFileDto(
    Guid PublicId,
    string Number,
    Guid AgreementPublicId,
    string AgreementCode,
    int Year,
    int Month,
    int SubPeriod,
    int Version,
    string Status,
    int Lines,
    decimal Total,
    int NewCount,
    int ChangeCount,
    int CancellationCount,
    int UnchangedCount,
    string Sha256,
    string FileName,
    DateTime GeneratedAt,
    DateTime? SentAt,
    string? PaymasterReference,
    string? VoidReason,
    Guid? SupersededByPublicId);

/// <summary>Una línea del archivo: empleado, crédito, cuota, valor y novedad (api.md §13.2). (nuevo)</summary>
public sealed record PayrollDeductionFileLineDto(
    Guid PublicId,
    int LineNumber,
    string? EmployeeId,
    Guid LoanPublicId,
    string LoanNumber,
    string Debtor,
    int? InstallmentNumber,
    DateOnly? DueDate,
    decimal CurrentAmount,
    decimal PastDueAmount,
    decimal Total,
    string Novelty,
    decimal? PreviousAmount,
    DateOnly LibranzaDate);

/// <summary>El archivo con una página de sus líneas del alcance. (nuevo)</summary>
public sealed record PayrollDeductionFileDetailDto(PayrollDeductionFileDto File, IReadOnlyList<PayrollDeductionFileLineDto> Lines, int TotalLines, int Page,
    int PageSize);

/// <summary>Lo que saldría (api.md §13.2, <c>POST /preview</c>): las líneas con sus novedades, el texto del archivo y los totales. (nuevo)</summary>
public sealed record PayrollDeductionFilePreviewDto(
    string FileName,
    DateOnly SendBy,
    int Version,
    IReadOnlyList<PayrollDeductionFileLineDto> Lines,
    IReadOnlyList<string> Text,
    decimal Total,
    int NewCount,
    int ChangeCount,
    int CancellationCount,
    int UnchangedCount);

/// <summary>Los archivos de descuentos dentro del alcance y sus DTO. (nuevo)</summary>
public static class ArchivosDeDescuentos
{
    /// <summary>El archivo por su <c>PublicId</c> si alguna de sus líneas es de un crédito del alcance; si no, el mismo 404 (y el evento de fuera de alcance).</summary>
    public static async Task<Result<PayrollDeductionFile>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LendingAuditEmitter auditoria,
        Guid publicId, CancellationToken ct)
    {
        var archivo = await db.PayrollDeductionFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == publicId && !f.IsDeleted, ct);
        if (archivo is null) return Result.Failure<PayrollDeductionFile>(ErroresDeLibranza.ArchivoInexistente());
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        if (await db.PayrollDeductionFileLines.AsNoTracking().AnyAsync(l => l.PayrollDeductionFileId == archivo.Id && creditos.Contains(l.LoanId), ct))
            return Result.Success(archivo);
        await auditoria.EmitirFueraDeAlcanceAsync(nameof(PayrollDeductionFile), archivo.PublicId, ct);
        return Result.Failure<PayrollDeductionFile>(ErroresDeLibranza.ArchivoInexistente());
    }

    public static async Task<IReadOnlyList<PayrollDeductionFileDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PayrollDeductionFile> filas, CancellationToken ct)
    {
        var convenios = filas.Select(f => f.PayrollAgreementId).Distinct().ToList();
        var deConvenio = await db.PayrollAgreements.AsNoTracking().IgnoreQueryFilters().Where(a => convenios.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Code), ct);
        var reemplazos = filas.Where(f => f.SupersededByFileId is not null).Select(f => f.SupersededByFileId!.Value).ToList();
        var deReemplazo = await db.PayrollDeductionFiles.AsNoTracking().Where(f => reemplazos.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.PublicId, ct);
        return filas.Select(f => new PayrollDeductionFileDto(f.PublicId, f.Number, deConvenio[f.PayrollAgreementId].PublicId, deConvenio[f.PayrollAgreementId].Code,
            f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber, f.Version, f.Status.ToString(), f.LineCount, f.TotalAmount, f.NewCount, f.ChangeCount,
            f.CancellationCount, f.UnchangedCount, f.FileSha256, f.FileName, f.GeneratedAt, f.SentAt, f.PaymasterReference, f.VoidReason,
            f.SupersededByFileId is { } r ? deReemplazo.GetValueOrDefault(r) : null)).ToList();
    }

    /// <summary>Las líneas como las muestra la pantalla, con el número del crédito y el nombre del deudor.</summary>
    public static async Task<IReadOnlyList<PayrollDeductionFileLineDto>> LineasAsync(IApplicationDbContext db, IReadOnlyList<PayrollDeductionFileLine> lineas,
        CancellationToken ct)
    {
        var creditos = lineas.Select(l => l.LoanId).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Number), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, lineas.Select(l => l.PersonId), ct);
        return lineas.Select(l => new PayrollDeductionFileLineDto(l.PublicId, l.LineNumber, l.EmployeeIdAtPaymaster, deCredito[l.LoanId].PublicId,
            deCredito[l.LoanId].Number, personas.GetValueOrDefault(l.PersonId)?.FullName ?? string.Empty, l.InstallmentNumber, l.DueDate, l.CurrentAmount,
            l.PastDueAmount, l.TotalAmount, l.NoveltyKind.ToString(), l.PreviousAmount, l.LibranzaDate)).ToList();
    }
}

// ---------------------------------------------------------------------------------------------------------------------- vista previa --

/// <summary>
/// La vista previa del archivo (api.md §13.2, <c>POST /preview</c>, <c>Lending.PayrollDeductionFiles.Generate</c>, sin clave: no escribe): las líneas y
/// novedades que saldrían, el texto del archivo con el lote todavía sin número y los totales. (nuevo)
/// </summary>
public sealed record PreviewPayrollDeductionFileQuery(Guid AgreementPublicId, int Year, int Month, int? SubPeriod = null)
    : IRequest<Result<PayrollDeductionFilePreviewDto>>;

public sealed class PreviewPayrollDeductionFileQueryValidator : AbstractValidator<PreviewPayrollDeductionFileQuery>
{
    public PreviewPayrollDeductionFileQueryValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty().WithMessage("Elija el convenio.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class PreviewPayrollDeductionFileQueryHandler(IApplicationDbContext db, PreparacionDeArchivoDeDescuentos preparacion)
    : IRequestHandler<PreviewPayrollDeductionFileQuery, Result<PayrollDeductionFilePreviewDto>>
{
    public async Task<Result<PayrollDeductionFilePreviewDto>> Handle(PreviewPayrollDeductionFileQuery request, CancellationToken ct)
    {
        var prep = await preparacion.PrepararAsync(request.AgreementPublicId, request.Year, request.Month, request.SubPeriod,
            PayrollDeductionLines.LoteDeVistaPrevia, ct);
        if (prep.IsFailure) return Result.Failure<PayrollDeductionFilePreviewDto>(prep.Error);
        var p = prep.Value;
        var version = await db.PayrollDeductionFiles.AsNoTracking()
            .Where(f => f.PayrollAgreementId == p.Convenio.Id && f.PeriodYear == p.Periodo.Anio && f.PeriodMonth == p.Periodo.Mes
                        && f.SubPeriodNumber == p.Periodo.SubPeriodo && !f.IsDeleted)
            .MaxAsync(f => (int?)f.Version, ct) ?? 0;
        try
        {
            var salida = FlatFileWriter.Escribir(p.Formato, p.Contexto, p.Lineas.Select(l => l.Valores).ToList());
            var texto = salida.Text.Split(p.Formato.LineEnding == BankFileLineEnding.Lf ? "\n" : "\r\n", StringSplitOptions.RemoveEmptyEntries);
            var lineas = p.Lineas.Select(l => new PayrollDeductionFileLineDto(Guid.Empty, l.Novedad.LineNumber, l.Novedad.EmployeeIdAtPaymaster, l.Credito.PublicId,
                l.Credito.Number, ReferenciasDeGobierno.Nombre(l.Deudor), l.Novedad.InstallmentNumber, l.Novedad.DueDate, l.Novedad.Corriente, l.Novedad.Vencido,
                l.Novedad.Total, l.Novedad.Novedad.ToString(), l.Novedad.Anterior, l.Novedad.LibranzaDate)).ToList();
            var t = p.Novedades.Totales;
            return Result.Success(new PayrollDeductionFilePreviewDto(salida.FileName, p.EnviarA, version + 1, lineas, texto, t.Total, t.Nuevas, t.Cambios,
                t.Cancelaciones, t.SinCambio));
        }
        catch (BankFileWriteException ex)
        {
            var credito = ex.LineNumber >= 1 && ex.LineNumber <= p.Lineas.Count ? p.Lineas[ex.LineNumber - 1].Credito.Number : "?";
            return Result.Failure<PayrollDeductionFilePreviewDto>(ErroresDeLibranza.NoCabe(ex.LineNumber, ex.Field, credito));
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------------- listar --

/// <summary>
/// Los archivos de descuentos (api.md §13.2, <c>GET /?agreementPublicId=&amp;year=&amp;month=&amp;status=&amp;page=</c>,
/// <c>Lending.PayrollDeductionFiles.Read</c>): los que tienen alguna línea de un crédito del alcance, los más recientes primero. (nuevo)
/// </summary>
public sealed record ListPayrollDeductionFilesQuery(Guid? AgreementPublicId = null, int? Year = null, int? Month = null, PayrollFileStatus? Status = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<PayrollDeductionFileDto>>>;

public sealed class ListPayrollDeductionFilesQueryValidator : AbstractValidator<ListPayrollDeductionFilesQuery>
{
    public ListPayrollDeductionFilesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListPayrollDeductionFilesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListPayrollDeductionFilesQuery, Result<PagedResult<PayrollDeductionFileDto>>>
{
    public async Task<Result<PagedResult<PayrollDeductionFileDto>>> Handle(ListPayrollDeductionFilesQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PayrollDeductionFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && db.PayrollDeductionFileLines.Any(l => l.PayrollDeductionFileId == f.Id && creditos.Contains(l.LoanId)));
        if (request.AgreementPublicId is { } convenio)
            consulta = consulta.Where(f => db.PayrollAgreements.Any(a => a.Id == f.PayrollAgreementId && a.PublicId == convenio));
        if (request.Year is { } anio) consulta = consulta.Where(f => f.PeriodYear == anio);
        if (request.Month is { } mes) consulta = consulta.Where(f => f.PeriodMonth == mes);
        if (request.Status is { } estado) consulta = consulta.Where(f => f.Status == estado);
        var total = await consulta.CountAsync(ct);
        var pagina = await consulta.OrderByDescending(f => f.PeriodYear).ThenByDescending(f => f.PeriodMonth).ThenByDescending(f => f.SubPeriodNumber)
            .ThenByDescending(f => f.Version).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<PayrollDeductionFileDto>(await ArchivosDeDescuentos.DtosAsync(db, pagina, ct), request.Page, request.PageSize, total));
    }
}

// --------------------------------------------------------------------------------------------------------------------------- detalle --

/// <summary>
/// Un archivo con sus líneas (api.md §13.2, <c>GET /{id}?page=</c>, <c>Lending.PayrollDeductionFiles.Read</c>): sólo las líneas de créditos del alcance;
/// fuera de él, <c>Lending.PayrollDeductionFile.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetPayrollDeductionFileQuery(Guid FilePublicId, int Page = 1, int PageSize = 100) : IRequest<Result<PayrollDeductionFileDetailDto>>;

public sealed class GetPayrollDeductionFileQueryValidator : AbstractValidator<GetPayrollDeductionFileQuery>
{
    public GetPayrollDeductionFileQueryValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}

public sealed class GetPayrollDeductionFileQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LendingAuditEmitter auditoria)
    : IRequestHandler<GetPayrollDeductionFileQuery, Result<PayrollDeductionFileDetailDto>>
{
    public async Task<Result<PayrollDeductionFileDetailDto>> Handle(GetPayrollDeductionFileQuery request, CancellationToken ct)
    {
        var leido = await ArchivosDeDescuentos.LeerAsync(db, alcanceDeLaPeticion, auditoria, request.FilePublicId, ct);
        if (leido.IsFailure) return Result.Failure<PayrollDeductionFileDetailDto>(leido.Error);
        var archivo = leido.Value;
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id && creditos.Contains(l.LoanId));
        var total = await consulta.CountAsync(ct);
        var pagina = await consulta.OrderBy(l => l.LineNumber).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Result.Success(new PayrollDeductionFileDetailDto((await ArchivosDeDescuentos.DtosAsync(db, [archivo], ct))[0],
            await ArchivosDeDescuentos.LineasAsync(db, pagina, ct), total, request.Page, request.PageSize));
    }
}
