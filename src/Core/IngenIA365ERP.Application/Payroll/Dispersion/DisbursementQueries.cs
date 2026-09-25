using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Dispersion;

// ------------------------------------------------------------------ listado --

/// <summary>Los archivos de dispersión: de una corrida, de un año o todos, del más reciente al más viejo.</summary>
public sealed record ListDisbursementFilesQuery(Guid? RunPublicId = null, int? Year = null, string? Status = null) : IRequest<Result<IReadOnlyList<DisbursementFileSummaryDto>>>;

public sealed class ListDisbursementFilesQueryValidator : AbstractValidator<ListDisbursementFilesQuery>
{
    public ListDisbursementFilesQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2200).When(x => x.Year is not null);
        RuleFor(x => x.Status).Must(s => s is null || Enum.TryParse<BankDisbursementFileStatus>(s, true, out _)).WithMessage("Estado desconocido.");
    }
}

public sealed class ListDisbursementFilesQueryHandler(IApplicationDbContext db, PayrollDisbursementLines lineas)
    : IRequestHandler<ListDisbursementFilesQuery, Result<IReadOnlyList<DisbursementFileSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<DisbursementFileSummaryDto>>> Handle(ListDisbursementFilesQuery request, CancellationToken ct)
    {
        var q = db.BankDisbursementFiles.AsNoTracking().Include(a => a.PayrollRun).Include(a => a.Bank).Include(a => a.Format).AsQueryable();
        if (request.RunPublicId is { } runId) q = q.Where(a => a.PayrollRun!.PublicId == runId);
        if (request.Year is { } year) q = q.Where(a => a.PaymentDate.Year == year);
        if (request.Status is { } s) { var estado = Enum.Parse<BankDisbursementFileStatus>(s, true); q = q.Where(a => a.Status == estado); }
        var archivos = await q.OrderByDescending(a => a.GeneratedAt).Take(500).ToListAsync(ct);
        var lista = new List<DisbursementFileSummaryDto>(archivos.Count);
        foreach (var a in archivos) lista.Add(await MapAsync(a, lineas, ct));
        return Result.Success<IReadOnlyList<DisbursementFileSummaryDto>>(lista);
    }

    public static async Task<DisbursementFileSummaryDto> MapAsync(BankDisbursementFile a, PayrollDisbursementLines lineas, CancellationToken ct) => new(
        a.PublicId, a.PayrollRun!.PublicId, await lineas.EtiquetaAsync(a.PayrollRun, ct), a.PayrollRun.Kind.ToString(), a.Status.ToString(),
        a.FormatCode, a.Format?.Name ?? a.FormatCode, a.Bank?.PublicId, a.Bank?.Name, a.PaymentDate, a.Sequence, a.Reference,
        a.LineCount, a.TotalAmount, a.ExcludedCount, a.FileName, a.FileSha256, a.GeneratedAt, a.GeneratedBy,
        a.SentAt, a.SentBy, a.BankReference, a.VoidedAt, a.VoidedBy, a.VoidReason);
}

// ------------------------------------------------------------------ detalle --

public sealed record GetDisbursementFileQuery(Guid FilePublicId) : IRequest<Result<DisbursementFileDetailDto>>;

public sealed class GetDisbursementFileQueryValidator : AbstractValidator<GetDisbursementFileQuery>
{
    public GetDisbursementFileQueryValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class GetDisbursementFileQueryHandler(IApplicationDbContext db, PayrollDisbursementLines lineas)
    : IRequestHandler<GetDisbursementFileQuery, Result<DisbursementFileDetailDto>>
{
    public async Task<Result<DisbursementFileDetailDto>> Handle(GetDisbursementFileQuery request, CancellationToken ct)
    {
        var a = await db.BankDisbursementFiles.AsNoTracking().Include(x => x.PayrollRun).Include(x => x.Bank).Include(x => x.Format).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PublicId == request.FilePublicId, ct);
        if (a is null) return Result.Failure<DisbursementFileDetailDto>(DisbursementErrors.FileNotFound);

        var idsRe = a.Lines.Select(l => l.PayrollRunEmployeeId).ToList();
        var empleados = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where idsRe.Contains(re.Id)
            select new { re.Id, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId }).ToDictionaryAsync(x => x.Id, ct);
        var pagos = await db.PayrollPayments.AsNoTracking().Where(p => idsRe.Contains(p.PayrollRunEmployeeId) && !p.IsReverted).ToDictionaryAsync(p => p.PayrollRunEmployeeId, ct);
        var idsBanco = a.Lines.Where(l => l.DestinationBankId.HasValue).Select(l => l.DestinationBankId!.Value).Distinct().ToList();
        var bancos = await db.Banks.AsNoTracking().Where(b => idsBanco.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var lines = a.Lines.OrderBy(l => l.LineNumber).Select(l =>
        {
            empleados.TryGetValue(l.PayrollRunEmployeeId, out var e);
            pagos.TryGetValue(l.PayrollRunEmployeeId, out var pago);
            return new DisbursementLineDto(l.LineNumber, e?.PublicId ?? Guid.Empty, e is null ? "?" : NombreDePersona.Completo(e.FirstName, e.OtherNames, e.LastName, e.SecondLastName), e?.TaxId ?? "?",
                l.DestinationBankId is { } bid ? bancos.GetValueOrDefault(bid) : null, l.AccountType switch { 1 => "Ahorros", 2 => "Corriente", _ => null },
                l.AccountNumber, l.Amount, l.RecordText, pago is not null, pago?.PublicId);
        }).ToList();
        var excluidos = string.IsNullOrEmpty(a.ExcludedJson) ? [] :
            JsonSerializer.Deserialize<List<DisbursementExcludedDto>>(a.ExcludedJson, GenerateDisbursementFileCommandHandler.JsonWeb) ?? [];

        return Result.Success(new DisbursementFileDetailDto(await ListDisbursementFilesQueryHandler.MapAsync(a, lineas, ct), lines, excluidos, a.SourceAccountNumber, a.FileAttachmentPublicId));
    }
}

// ----------------------------------------------------------------- descarga --

/// <summary>El archivo tal como se generó, con el nombre, el tipo y la codificación del formato.</summary>
public sealed record DownloadDisbursementFileQuery(Guid FilePublicId) : IRequest<Result<DisbursementDownloadDto>>;

public sealed class DownloadDisbursementFileQueryValidator : AbstractValidator<DownloadDisbursementFileQuery>
{
    public DownloadDisbursementFileQueryValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class DownloadDisbursementFileQueryHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<DownloadDisbursementFileQuery, Result<DisbursementDownloadDto>>
{
    public async Task<Result<DisbursementDownloadDto>> Handle(DownloadDisbursementFileQuery request, CancellationToken ct)
    {
        var a = await db.BankDisbursementFiles.AsNoTracking().Include(x => x.Format).FirstOrDefaultAsync(x => x.PublicId == request.FilePublicId, ct);
        if (a is null) return Result.Failure<DisbursementDownloadDto>(DisbursementErrors.FileNotFound);
        if (a.FileAttachmentPublicId is not { } adjunto)
            return Result.Failure<DisbursementDownloadDto>(new Error("Payroll.Disbursement.FileMissing", "El archivo no quedó guardado; genere uno nuevo."));
        var descarga = await sender.Send(new DownloadAttachmentQuery(adjunto), ct);
        if (descarga.IsFailure) return Result.Failure<DisbursementDownloadDto>(descarga.Error);
        if (!string.Equals(descarga.Value.Sha256Hex, a.FileSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<DisbursementDownloadDto>(new Error("Payroll.Disbursement.FileTampered", "El archivo guardado no coincide con la huella registrada al generarlo."));
        return Result.Success(new DisbursementDownloadDto(a.FileName, a.Format?.ContentType ?? descarga.Value.ContentType, descarga.Value.Content, a.Format?.Encoding ?? "us-ascii"));
    }
}

/// <summary>
/// Feature 011 (US4, contracts/api.md §9): el archivo de dispersión se baja con un enlace firmado, con el
/// nombre, el tipo y la codificación del formato del banco. Uno guardado con el formato anterior
/// responde <c>direct: false</c> y se baja por <c>GET /{id}/file</c>. Es un comando porque cada enlace
/// queda en la auditoría.
/// </summary>
public sealed record EmitirEnlaceDeDispersionCommand(Guid FilePublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class EmitirEnlaceDeDispersionCommandValidator : AbstractValidator<EmitirEnlaceDeDispersionCommand>
{
    public EmitirEnlaceDeDispersionCommandValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class EmitirEnlaceDeDispersionCommandHandler(IApplicationDbContext db, IBlobStore store, IDateTimeService reloj, IOptions<LimitesDeAdjuntos> limites)
    : IRequestHandler<EmitirEnlaceDeDispersionCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(EmitirEnlaceDeDispersionCommand request, CancellationToken ct)
    {
        var a = await db.BankDisbursementFiles.AsNoTracking().Include(x => x.Format).FirstOrDefaultAsync(x => x.PublicId == request.FilePublicId, ct);
        if (a is null) return Result.Failure<EnlaceDeDescargaDto>(DisbursementErrors.FileNotFound);
        var adjunto = a.FileAttachmentPublicId is { } adjuntoId
            ? await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == adjuntoId, ct)
            : null;
        if (adjunto is null)
            return Result.Failure<EnlaceDeDescargaDto>(new Error("Payroll.Disbursement.FileMissing", "El archivo no quedó guardado; genere uno nuevo."));
        if (!string.Equals(adjunto.Sha256Hex, a.FileSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<EnlaceDeDescargaDto>(new Error("Payroll.Disbursement.FileTampered", "El archivo guardado no coincide con la huella registrada al generarlo."));
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);

        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct,
            contentType: TipoConCodificacion(a.Format?.ContentType ?? adjunto.ContentType, a.Format?.Encoding ?? "us-ascii"), nombre: a.FileName));
    }

    /// <summary>El mismo tipo que servía <c>GET /{id}/file</c>: el del formato, con su codificación si no la trae.</summary>
    public static string TipoConCodificacion(string tipo, string codificacion) =>
        tipo.Contains("charset", StringComparison.OrdinalIgnoreCase) ? tipo : $"{tipo}; charset={codificacion}";
}
