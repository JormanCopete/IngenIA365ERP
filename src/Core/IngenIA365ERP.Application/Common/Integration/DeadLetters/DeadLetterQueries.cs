using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration.DeadLetters;

/// <summary>Qué clase de trabajo murió (feature 023; contracts/api.md §4). (nuevo)</summary>
public enum DeadLetterKind
{
    /// <summary>Un documento electrónico que agotó sus intentos ante el canal.</summary>
    ElectronicDocument = 1,

    /// <summary>El seguimiento de un documento (canónico, representación, correo) que agotó sus intentos.</summary>
    ElectronicFollowUp = 2,

    /// <summary>Una entrega de la bandeja de integración que agotó sus intentos.</summary>
    IntegrationDelivery = 3,
}

/// <summary>Una fila de la pantalla de mensajes muertos. (nuevo)</summary>
public sealed record DeadLetterDto(
    DeadLetterKind Kind,
    Guid PublicId,
    string Reference,
    string? Counterparty,
    decimal? Amount,
    DateTime DeadAt,
    int? AttemptLimit,
    int Attempts,
    string? LastError,
    string Module,
    string? Destination);

/// <summary>Un intento de la historia de un trabajo muerto. (nuevo)</summary>
public sealed record DeadLetterAttemptDto(int Number, DateTime At, string Outcome, string? Code, string? Message);

/// <summary>El detalle: la fila y su historia de intentos (las transmisiones, o los intentos de entrega). (nuevo)</summary>
public sealed record DeadLetterDetailDto(DeadLetterDto Item, IReadOnlyList<DeadLetterAttemptDto> Attempts);

/// <summary>La tarjeta del tablero: cuántos hay de cada clase y el más viejo. (nuevo)</summary>
public sealed record DeadLettersSummaryDto(int ElectronicDocuments, int ElectronicFollowUps, int IntegrationDeliveries, DateTime? Oldest);

/// <summary>Los mensajes muertos de la cooperativa (<c>GET /api/platform/dead-letters</c>, <c>Platform.DeadLetters.View</c>). (nuevo)</summary>
public sealed record ListDeadLettersQuery(DeadLetterKind? Kind, DateTime? From, DateTime? To, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<DeadLetterDto>>>;

/// <summary>El detalle de un trabajo muerto. (nuevo)</summary>
public sealed record GetDeadLetterQuery(DeadLetterKind Kind, Guid PublicId) : IRequest<Result<DeadLetterDetailDto>>;

/// <summary>El resumen para el tablero. (nuevo)</summary>
public sealed record GetDeadLettersSummaryQuery : IRequest<Result<DeadLettersSummaryDto>>;

/// <summary>Las lecturas de los mensajes muertos (feature 023). Leen la base de la cooperativa; nada de la mensajería. (nuevo)</summary>
public sealed class DeadLetterQueryHandlers(IApplicationDbContext db) :
    IRequestHandler<ListDeadLettersQuery, Result<PagedResult<DeadLetterDto>>>,
    IRequestHandler<GetDeadLetterQuery, Result<DeadLetterDetailDto>>,
    IRequestHandler<GetDeadLettersSummaryQuery, Result<DeadLettersSummaryDto>>
{
    public async Task<Result<PagedResult<DeadLetterDto>>> Handle(ListDeadLettersQuery request, CancellationToken ct)
    {
        var tamano = Math.Clamp(request.PageSize, 1, PageRequest.MaxPageSize);
        var pagina = Math.Max(1, request.Page);
        var filas = new List<DeadLetterDto>();
        if (request.Kind is null or DeadLetterKind.ElectronicDocument) filas.AddRange(await DocumentosAsync(request.From, request.To, null, ct));
        if (request.Kind is null or DeadLetterKind.ElectronicFollowUp) filas.AddRange(await SeguimientosAsync(request.From, request.To, null, ct));
        if (request.Kind is null or DeadLetterKind.IntegrationDelivery) filas.AddRange(await EntregasAsync(request.From, request.To, null, ct));
        var ordenadas = filas.OrderBy(f => f.DeadAt).ToList();
        var items = ordenadas.Skip((pagina - 1) * tamano).Take(tamano).ToList();
        return Result.Success(new PagedResult<DeadLetterDto>(items, pagina, tamano, ordenadas.Count));
    }

    public async Task<Result<DeadLetterDetailDto>> Handle(GetDeadLetterQuery request, CancellationToken ct)
    {
        var fila = request.Kind switch
        {
            DeadLetterKind.ElectronicDocument => (await DocumentosAsync(null, null, request.PublicId, ct)).FirstOrDefault(),
            DeadLetterKind.ElectronicFollowUp => (await SeguimientosAsync(null, null, request.PublicId, ct)).FirstOrDefault(),
            _ => (await EntregasAsync(null, null, request.PublicId, ct)).FirstOrDefault(),
        };
        if (fila is null) return Result.Failure<DeadLetterDetailDto>(ErroresDeMensajesMuertos.NotFound());

        IReadOnlyList<DeadLetterAttemptDto> intentos = request.Kind == DeadLetterKind.IntegrationDelivery
            ? await db.IntegrationDeliveryAttempts.AsNoTracking()
                .Where(a => db.IntegrationMessageDeliveries.Any(d => d.Id == a.DeliveryId && d.PublicId == request.PublicId))
                .OrderByDescending(a => a.AttemptNumber).Take(50)
                .Select(a => new DeadLetterAttemptDto(a.AttemptNumber, a.FinishedAt, a.Outcome.ToString(), a.ErrorCode, a.ErrorMessage))
                .ToListAsync(ct)
            : await db.ElectronicDocumentTransmissions.AsNoTracking()
                .Where(t => db.ElectronicDocuments.Any(d => d.Id == t.ElectronicDocumentId && d.PublicId == request.PublicId))
                .OrderByDescending(t => t.AttemptNumber).Take(50)
                .Select(t => new DeadLetterAttemptDto(t.AttemptNumber, t.RequestedAt, t.Outcome.ToString(), t.ProviderCode, t.TranslatedMessagesJson))
                .ToListAsync(ct);
        return Result.Success(new DeadLetterDetailDto(fila, intentos));
    }

    public async Task<Result<DeadLettersSummaryDto>> Handle(GetDeadLettersSummaryQuery request, CancellationToken ct)
    {
        var documentos = await db.ElectronicDocuments.AsNoTracking().Where(d => d.DeadAt != null).Select(d => d.DeadAt).ToListAsync(ct);
        var seguimientos = await db.ElectronicDocuments.AsNoTracking().Where(d => d.FollowUpDeadAt != null).Select(d => d.FollowUpDeadAt).ToListAsync(ct);
        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.DeadAt != null && d.Status == DeliveryStatus.Pending).Select(d => d.DeadAt).ToListAsync(ct);
        var todas = documentos.Concat(seguimientos).Concat(entregas).Where(x => x is not null).ToList();
        return Result.Success(new DeadLettersSummaryDto(documentos.Count, seguimientos.Count, entregas.Count, todas.Count == 0 ? null : todas.Min()));
    }

    private Task<List<DeadLetterDto>> DocumentosAsync(DateTime? desde, DateTime? hasta, Guid? uno, CancellationToken ct) =>
        db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.DeadAt != null && (uno == null || d.PublicId == uno)
                        && (desde == null || d.DeadAt >= desde) && (hasta == null || d.DeadAt <= hasta))
            .Select(d => new DeadLetterDto(DeadLetterKind.ElectronicDocument, d.PublicId, d.Number, d.CounterpartyName, d.TotalAmount,
                d.DeadAt!.Value, d.DeadAttemptLimit, d.AttemptCount, d.LastOutcome == null ? null : d.LastOutcome.ToString(), d.SourceModule, null))
            .ToListAsync(ct);

    private Task<List<DeadLetterDto>> SeguimientosAsync(DateTime? desde, DateTime? hasta, Guid? uno, CancellationToken ct) =>
        db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.FollowUpDeadAt != null && (uno == null || d.PublicId == uno)
                        && (desde == null || d.FollowUpDeadAt >= desde) && (hasta == null || d.FollowUpDeadAt <= hasta))
            .Select(d => new DeadLetterDto(DeadLetterKind.ElectronicFollowUp, d.PublicId, d.Number, d.CounterpartyName, d.TotalAmount,
                d.FollowUpDeadAt!.Value, null, d.FollowUpAttempts, d.FollowUpLastError, d.SourceModule, null))
            .ToListAsync(ct);

    private Task<List<DeadLetterDto>> EntregasAsync(DateTime? desde, DateTime? hasta, Guid? uno, CancellationToken ct) =>
        db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.DeadAt != null && d.Status == DeliveryStatus.Pending && (uno == null || d.PublicId == uno)
                        && (desde == null || d.DeadAt >= desde) && (hasta == null || d.DeadAt <= hasta))
            .Select(d => new DeadLetterDto(DeadLetterKind.IntegrationDelivery, d.PublicId, d.Message!.Type + " " + d.Message.OriginNumber, null, null,
                d.DeadAt!.Value, d.DeadAttemptLimit, d.Attempts, d.LastErrorMessage, d.Message.OriginModule, d.Destination))
            .ToListAsync(ct);
}

/// <summary>Los errores de los mensajes muertos (feature 023). (nuevo)</summary>
public static class ErroresDeMensajesMuertos
{
    public const string NotFoundCode = "DeadLetters.NotFound";
    public const string NotDeadCode = "DeadLetters.NotDead";

    public static Error NotFound() => new(NotFoundCode, "El trabajo no existe o ya no está entre los mensajes muertos.");

    public static Error NotDead() => new(NotDeadCode, "Ya no está muerto: lo reprocesó otra persona o se resolvió solo.");
}
