using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

/// <summary>
/// <c>GET /api/receipts/payroll-files?paymaster=&amp;year=&amp;month=&amp;state=</c> (<c>Receipts.Paymasters.View</c>; FR-075): los archivos de descuentos recibidos de Cartera, de
/// las pagadurías al alcance (<c>RCD_UserPaymasterScopes</c>), los más recientes primero, sin líneas. (nuevo)
/// </summary>
public sealed record ListPayrollDeductionFilesQuery(Guid? PaymasterPersonPublicId = null, int? Year = null, int? Month = null, PayrollDeductionFileState? State = null)
    : IRequest<Result<IReadOnlyList<PayrollDeductionFileDto>>>;

public sealed class ListPayrollDeductionFilesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListPayrollDeductionFilesQuery, Result<IReadOnlyList<PayrollDeductionFileDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollDeductionFileDto>>> Handle(ListPayrollDeductionFilesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.ReceiptsPayrollDeductionFiles.AsNoTracking();
        if (request.PaymasterPersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(f => f.PaymasterPersonId == id);
        }
        if (request.Year is { } y) consulta = consulta.Where(f => f.PeriodYear == y);
        if (request.Month is { } m) consulta = consulta.Where(f => f.PeriodMonth == m);
        if (request.State is { } s) consulta = consulta.Where(f => f.Status == s);
        var filas = (await consulta.OrderByDescending(f => f.Id).Take(300).ToListAsync(ct)).Where(f => mio.IncluyePagaduria(f.PaymasterPersonId)).ToList();
        var lista = new List<PayrollDeductionFileDto>(filas.Count);
        foreach (var f in filas) lista.Add(await PagaduriasDeRecaudo.DtoAsync(db, f, conLineas: false, ct));
        return Result.Success<IReadOnlyList<PayrollDeductionFileDto>>(lista);
    }
}

/// <summary><c>GET /api/receipts/payroll-files/{id}</c> (<c>Receipts.Paymasters.View</c>): el archivo con sus líneas; fuera del alcance, el 404 genérico. (nuevo)</summary>
public sealed record GetPayrollDeductionFileQuery(Guid FilePublicId) : IRequest<Result<PayrollDeductionFileDto>>;

public sealed class GetPayrollDeductionFileQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<GetPayrollDeductionFileQuery, Result<PayrollDeductionFileDto>>
{
    public async Task<Result<PayrollDeductionFileDto>> Handle(GetPayrollDeductionFileQuery request, CancellationToken ct)
    {
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct);
        return f is null
            ? Result.Failure<PayrollDeductionFileDto>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"))
            : Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, f, conLineas: true, ct));
    }
}

/// <summary>
/// <c>GET /api/receipts/payroll-files/{id}/file</c> (<c>Receipts.Paymasters.View</c>; FR-075): el enlace firmado del archivo tal como lo generó Cartera, auditado
/// (<c>Receipts.PayrollFile.Downloaded</c>). El adjunto es de Cartera; Recaudo lo firma por el archivo que ve, sin pedir el permiso de Cartera. (nuevo)
/// </summary>
public sealed record DownloadPayrollDeductionFileCommand(Guid FilePublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class DownloadPayrollDeductionFileCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    AuditoriaDeRecaudo auditoria,
    IBlobStore store,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites) : IRequestHandler<DownloadPayrollDeductionFileCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(DownloadPayrollDeductionFileCommand request, CancellationToken ct)
    {
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct);
        if (f is null) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"));
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == f.FileAttachmentPublicId, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"));
        var actor = await actorActual.ObtenerAsync(ct);
        await auditoria.AgregarAsync("Receipts.PayrollFile.Downloaded", "PayrollDeductionFile", f.PublicId, null,
            new { file = f.FileNumber, version = f.FileVersion, sha256 = f.FileSha256, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct, nombre: f.FileName));
    }
}

/// <summary><c>GET /api/receipts/payroll-files/{id}/responses</c> (<c>Receipts.Paymasters.View</c>): las versiones de la respuesta de la pagaduría. (nuevo)</summary>
public sealed record ListPaymasterResponsesQuery(Guid FilePublicId) : IRequest<Result<IReadOnlyList<PaymasterResponseDto>>>;

public sealed class ListPaymasterResponsesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListPaymasterResponsesQuery, Result<IReadOnlyList<PaymasterResponseDto>>>
{
    public async Task<Result<IReadOnlyList<PaymasterResponseDto>>> Handle(ListPaymasterResponsesQuery request, CancellationToken ct)
    {
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct);
        if (f is null) return Result.Failure<IReadOnlyList<PaymasterResponseDto>>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"));
        var filas = await db.PaymasterResponses.AsNoTracking().Where(r => r.PayrollDeductionFileId == f.Id).OrderByDescending(r => r.Version).ToListAsync(ct);
        var lista = new List<PaymasterResponseDto>(filas.Count);
        foreach (var r in filas) lista.Add(await PagaduriasDeRecaudo.DtoAsync(db, r, f.PublicId, ct));
        return Result.Success<IReadOnlyList<PaymasterResponseDto>>(lista);
    }
}

/// <summary>
/// <c>GET /api/receipts/payroll-files/{id}/responses/{rid}/preview</c> (<c>Receipts.Paymasters.LoadResponse</c>; FR-077): lo que se devolverá a Cartera, agrupado por
/// concepto de la pagaduría (archivo v2) o por crédito (v1), con lo esperado, lo descontado y las filas sin equivalencia. (nuevo)
/// </summary>
public sealed record PreviewPaymasterResponseQuery(Guid FilePublicId, Guid ResponsePublicId) : IRequest<Result<PaymasterResponsePreviewDto>>;

public sealed class PreviewPaymasterResponseQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<PreviewPaymasterResponseQuery, Result<PaymasterResponsePreviewDto>>
{
    public async Task<Result<PaymasterResponsePreviewDto>> Handle(PreviewPaymasterResponseQuery request, CancellationToken ct)
    {
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct);
        var r = f is null ? null : await db.PaymasterResponses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ResponsePublicId && x.PayrollDeductionFileId == f.Id, ct);
        if (f is null || r is null) return Result.Failure<PaymasterResponsePreviewDto>(ErroresDeRecaudo.NoEncontrado("La respuesta de la pagaduría"));
        var lineas = await db.PaymasterResponseLines.AsNoTracking().Where(l => l.PaymasterResponseId == r.Id).ToListAsync(ct);
        var porConcepto = f.MessageVersion >= 2;
        var filas = lineas
            .GroupBy(l => porConcepto ? PagaduriasDeRecaudo.Clave(l.PaymasterConceptCode) : PagaduriasDeRecaudo.Clave(l.ProductNumber))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PaymasterResponsePreviewRowDto(g.Key, g.Count(), g.Sum(l => l.Expected), g.Sum(l => l.Deducted), g.Count(l => l.Unmatched)))
            .ToList();
        return Result.Success(new PaymasterResponsePreviewDto(r.PublicId, porConcepto ? "PaymasterConcept" : "Loan", filas, lineas.Sum(l => l.Expected),
            lineas.Sum(l => l.Deducted), lineas.Count(l => l.Unmatched)));
    }
}
