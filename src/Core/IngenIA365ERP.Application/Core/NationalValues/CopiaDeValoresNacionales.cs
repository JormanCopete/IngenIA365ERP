using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.NationalValues;

// ----------------------------------------------------------------------------------------------------------- DTO --

/// <summary>Una revisión resumida en los listados (contracts/api.md §21.3). (nuevo)</summary>
public sealed record NationalValueReviewDto(NationalValueReviewDecision Decision, string By, DateTime At, string? Note);

/// <summary>Una copia de valor nacional en la cooperativa (contracts/api.md §21.3). (nuevo)</summary>
public sealed record NationalValueDto(
    Guid PublicId,
    string Code,
    string Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string? Label,
    string Source,
    long Sequence,
    DateTime KnownAt,
    Guid? Supersedes,
    EstadoDeRevision Status,
    NationalValueReviewDto? Review);

/// <summary>Una réplica de la bitácora (contracts/api.md §21.3). (nuevo)</summary>
public sealed record NationalValueReplicationDto(
    Guid PublicId,
    DateTime At,
    long FromSequence,
    long ToSequence,
    int Values,
    int Documents,
    NationalValueReplicationOrigin Origin,
    string Outcome,
    string ActorName,
    JsonElement? Effects);

/// <summary>Una copia de documento normativo, sin su contenido. (nuevo)</summary>
public sealed record RegulatoryDocumentDto(
    Guid PublicId,
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    short SchemaVersion,
    string Sha256,
    bool Verified,
    DateTime KnownAt,
    EstadoDeRevision Status,
    NationalValueReviewDto? Review);

/// <summary>Una copia de documento normativo con su contenido. (nuevo)</summary>
public sealed record RegulatoryDocumentDetailDto(RegulatoryDocumentDto Document, string Source, string Content);

// ------------------------------------------------------------------------------------------------------ revisar --

/// <summary>
/// La conformidad del contador o del oficial de cumplimiento con una copia de valor nacional (feature 013, T41, R7;
/// contracts/api.md §21.3; data-model parte 3 §4.4): una fila de <c>COR_NationalValueReviews</c> (hecho) que no toca la copia.
/// <c>Objected</c> exige nota. Una segunda revisión se admite y vale la más reciente (data-model §4.4, que rige sobre el
/// <c>NationalValues.AlreadyReviewed</c> de api.md según la Nota de contradicciones); repetir la misma decisión que ya rige
/// responde <c>NationalValues.AlreadyReviewed</c>, porque no agrega nada. Permiso <c>Core.NationalValues.Review</c>. (nuevo)
/// </summary>
public sealed record ReviewNationalValueCommand(Guid NationalValuePublicId, NationalValueReviewDecision Decision, string? Note)
    : IRequest<Result<Guid>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>Lo mismo sobre un documento normativo (<c>POST /api/core/regulatory-documents/{id}/review</c>). (nuevo)</summary>
public sealed record ReviewRegulatoryDocumentCommand(Guid RegulatoryDocumentPublicId, NationalValueReviewDecision Decision, string? Note)
    : IRequest<Result<Guid>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReviewNationalValueCommandValidator : AbstractValidator<ReviewNationalValueCommand>
{
    public ReviewNationalValueCommandValidator()
    {
        RuleFor(x => x.NationalValuePublicId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum().WithMessage("La decisión es Confirmed u Objected.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ReviewRegulatoryDocumentCommandValidator : AbstractValidator<ReviewRegulatoryDocumentCommand>
{
    public ReviewRegulatoryDocumentCommandValidator()
    {
        RuleFor(x => x.RegulatoryDocumentPublicId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum().WithMessage("La decisión es Confirmed u Objected.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ReviewNationalValueCommandHandler(IApplicationDbContext db, IActorActual actor, IDateTimeService reloj)
    : IRequestHandler<ReviewNationalValueCommand, Result<Guid>>, IRequestHandler<ReviewRegulatoryDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReviewNationalValueCommand request, CancellationToken ct)
    {
        var existe = await db.NationalValues.AsNoTracking().AnyAsync(v => v.PublicId == request.NationalValuePublicId, ct);
        return existe
            ? await RevisarAsync(NationalValueReviewTarget.Value, request.NationalValuePublicId, request.Decision, request.Note, ct)
            : Result.Failure<Guid>(ErroresDeValoresNacionales.NoEncontrado);
    }

    public async Task<Result<Guid>> Handle(ReviewRegulatoryDocumentCommand request, CancellationToken ct)
    {
        var existe = await db.RegulatoryDocuments.AsNoTracking().AnyAsync(d => d.PublicId == request.RegulatoryDocumentPublicId, ct);
        return existe
            ? await RevisarAsync(NationalValueReviewTarget.Document, request.RegulatoryDocumentPublicId, request.Decision, request.Note, ct)
            : Result.Failure<Guid>(ErroresDeValoresNacionales.DocumentoNoEncontrado);
    }

    private async Task<Result<Guid>> RevisarAsync(NationalValueReviewTarget clase, Guid copia, NationalValueReviewDecision decision, string? nota, CancellationToken ct)
    {
        nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        if (decision == NationalValueReviewDecision.Objected && nota is null)
            return Result.Failure<Guid>(ErroresDeValoresNacionales.NotaObligatoria);

        var quien = await actor.ObtenerAsync(ct);
        if (quien.UserId is not { } usuario)
            return Result.Failure<Guid>("Identity.Unauthenticated", "No se sabe quién revisa: la revisión la firma una persona de la cooperativa.");

        var ultimas = await RevisionesDeCopias.UltimasAsync(db, [copia], ct);
        if (ultimas.TryGetValue(copia, out var ultima) && ultima.Decision == decision && ultima.Note == nota)
            return Result.Failure<Guid>(ErroresDeValoresNacionales.YaRevisado());

        var ahora = reloj.UtcNow;
        var revision = new NationalValueReview
        {
            TargetKind = clase,
            TargetPublicId = copia,
            Decision = decision,
            Note = nota,
            ReviewedByUserId = usuario,
            ReviewedByName = quien.Name.Length > 150 ? quien.Name[..150] : quien.Name,
            ReviewedAt = ahora,
            CreatedAt = ahora,
            CreatedBy = quien.Name.Length > 150 ? quien.Name[..150] : quien.Name,
        };
        db.NationalValueReviews.Add(revision);
        await db.SaveChangesAsync(ct);
        return Result.Success(revision.PublicId);
    }
}

// ----------------------------------------------------------------------------------------------------- consultar --

/// <summary>
/// Las copias de valores nacionales de la cooperativa (feature 013, T40, C83; contracts/api.md §21.3). Sin <c>date</c>, todas las
/// copias (más nuevas primero). Con <c>date</c> (D) y, si viene, <c>recordedAsOf</c> (R), la regla bitemporal: la versión que regía
/// D con lo replicado hasta R, una por código y clave. (nuevo)
/// </summary>
public sealed record ListNationalValuesQuery(string? Code = null, string? Key = null, DateOnly? Date = null, DateTime? RecordedAsOf = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedList<NationalValueDto>>>;

public sealed class ListNationalValuesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListNationalValuesQuery, Result<PagedList<NationalValueDto>>>
{
    public async Task<Result<PagedList<NationalValueDto>>> Handle(ListNationalValuesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 500);
        var q = db.NationalValues.AsNoTracking().Where(v => !v.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Code)) q = q.Where(v => v.Code == request.Code.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(request.Key)) q = q.Where(v => v.Key == request.Key.Trim());

        List<NationalValue> filas;
        int total;
        if (request.Date is { } d)
        {
            var r = request.RecordedAsOf ?? DateTime.MaxValue;
            var vigentes = (await q.Where(v => v.ValidFrom <= d).ToListAsync(ct))
                .GroupBy(v => (v.Code, v.Key))
                .Select(g => VigenciaBitemporal.Vigente(g, d, r))
                .Where(v => v is not null)
                .Select(v => v!)
                .OrderBy(v => v.Code).ThenBy(v => v.Key)
                .ToList();
            total = vigentes.Count;
            filas = vigentes.Skip((page - 1) * size).Take(size).ToList();
        }
        else
        {
            if (request.RecordedAsOf is { } hasta) q = q.Where(v => v.KnownAt <= hasta);
            total = await q.CountAsync(ct);
            filas = await q.OrderByDescending(v => v.SourceSequence).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        }

        var revisiones = await RevisionesDeCopias.UltimasAsync(db, filas.Select(f => f.PublicId).ToList(), ct);
        return Result.Success(new PagedList<NationalValueDto>(filas.Select(f =>
        {
            var rev = revisiones.GetValueOrDefault(f.PublicId);
            return new NationalValueDto(f.PublicId, f.Code, f.Key, f.ValidFrom, f.ValidTo, f.ReferenceDate, f.Value, f.Unit, f.Label, f.Source,
                f.SourceSequence, f.KnownAt, f.SupersedesSourcePublicId, RevisionesDeCopias.Estado(rev), Dto(rev));
        }).ToList(), total, page, size));
    }

    internal static NationalValueReviewDto? Dto(UltimaRevision? r) => r is null ? null : new(r.Decision, r.By, r.At, r.Note);
}

/// <summary>La bitácora de réplicas, más nuevas primero (contracts/api.md §21.3). (nuevo)</summary>
public sealed record ListNationalValueReplicationsQuery(int Page = 1, int PageSize = 50) : IRequest<Result<PagedList<NationalValueReplicationDto>>>;

public sealed class ListNationalValueReplicationsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListNationalValueReplicationsQuery, Result<PagedList<NationalValueReplicationDto>>>
{
    public async Task<Result<PagedList<NationalValueReplicationDto>>> Handle(ListNationalValueReplicationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var q = db.NationalValueReplications.AsNoTracking().Where(r => !r.IsDeleted);
        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result.Success(new PagedList<NationalValueReplicationDto>(filas.Select(r => new NationalValueReplicationDto(
            r.PublicId, r.StartedAt, r.FromSequence, r.ToSequence, r.ValueCount, r.DocumentCount, r.Origin, "Copied", r.ActorName,
            Leer(r.EffectsSummaryJson))).ToList(), total, page, size));
    }

    private static JsonElement? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Las copias de documentos normativos; con <c>date</c>, la vigente de cada clase (contracts/api.md §21.3). (nuevo)</summary>
public sealed record ListRegulatoryDocumentsQuery(string? Kind = null, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<RegulatoryDocumentDto>>>;

public sealed class ListRegulatoryDocumentsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListRegulatoryDocumentsQuery, Result<IReadOnlyList<RegulatoryDocumentDto>>>
{
    public async Task<Result<IReadOnlyList<RegulatoryDocumentDto>>> Handle(ListRegulatoryDocumentsQuery request, CancellationToken ct)
    {
        var q = db.RegulatoryDocuments.AsNoTracking().Where(d => !d.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Kind)) q = q.Where(d => d.Kind == request.Kind.Trim());
        var filas = await q.ToListAsync(ct);
        if (request.Date is { } fecha)
            filas = filas.GroupBy(d => d.Kind).Select(g => VigenciaBitemporal.Vigente(g, fecha, DateTime.MaxValue)).Where(d => d is not null).Select(d => d!).ToList();
        var revisiones = await RevisionesDeCopias.UltimasAsync(db, filas.Select(f => f.PublicId).ToList(), ct);
        return Result.Success<IReadOnlyList<RegulatoryDocumentDto>>(filas.OrderByDescending(d => d.SourceSequence)
            .Select(d => Mapear(d, revisiones.GetValueOrDefault(d.PublicId))).ToList());
    }

    internal static RegulatoryDocumentDto Mapear(RegulatoryDocument d, UltimaRevision? rev) =>
        new(d.PublicId, d.Kind, d.Code, d.ValidFrom, d.ValidTo, d.SchemaVersion, d.ContentSha256, d.Verified, d.KnownAt,
            RevisionesDeCopias.Estado(rev), ListNationalValuesQueryHandler.Dto(rev));
}

/// <summary>Una copia de documento normativo con su contenido JSON (contracts/api.md §21.3). (nuevo)</summary>
public sealed record GetRegulatoryDocumentQuery(Guid PublicId) : IRequest<Result<RegulatoryDocumentDetailDto>>;

public sealed class GetRegulatoryDocumentQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRegulatoryDocumentQuery, Result<RegulatoryDocumentDetailDto>>
{
    public async Task<Result<RegulatoryDocumentDetailDto>> Handle(GetRegulatoryDocumentQuery request, CancellationToken ct)
    {
        var d = await db.RegulatoryDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.PublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure<RegulatoryDocumentDetailDto>(ErroresDeValoresNacionales.DocumentoNoEncontrado);
        var revisiones = await RevisionesDeCopias.UltimasAsync(db, [d.PublicId], ct);
        return Result.Success(new RegulatoryDocumentDetailDto(
            ListRegulatoryDocumentsQueryHandler.Mapear(d, revisiones.GetValueOrDefault(d.PublicId)), d.Source, d.Content));
    }
}
