using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Notifications;

// Feature 020, US9 (R18; FR-049 a FR-053; contracts/api.md §3, /api/transport/notifications): el tablero de envíos a sistemas externos de la
// cooperativa —pendiente, enviado, con aviso, reintentando, en espera, para revisar, pago anulado, cerrado a mano—, con el detalle de cada
// intento (código, extracto, duración y cuerpo saneado) y dos acciones: reintentar y cerrar a mano con motivo.

public sealed record EnvioDto(
    Guid PublicId,
    string Status,
    string Connector,
    Guid? VehiclePublicId,
    string? Plate,
    string? InternalNumber,
    string? Service,
    string? Period,
    string? InvoiceNumber,
    decimal? Amount,
    Guid ExternalId,
    int Attempts,
    int? LastStatusCode,
    string? LastResponseExcerpt,
    DateTime? LastAttemptAt,
    DateTime? SentAt,
    string? Warning,
    string? ReviewFlag,
    DateTime CreatedAt,
    string? ResolvedBy,
    string? ResolutionReason);

public sealed record TableroDeEnviosDto(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<EnvioDto> Items, bool Truncated);

public sealed record IntentoDeEnvioDto(int AttemptNumber, string Kind, DateTime StartedAt, int DurationMs, int StatusCode, string Outcome, string? ResponseExcerpt,
    string? RequestBodySanitized, string? Url);

public sealed record EnvioDetalleDto(EnvioDto Envio, IReadOnlyList<IntentoDeEnvioDto> Attempts);

/// <summary>Los envíos con filtros combinables y los conteos por estado del mismo rango (T163). A lo sumo <see cref="Tope"/> filas.</summary>
public sealed record ListConnectorRequestsQuery(string? Status, DateOnly? From, DateOnly? To, Guid? VehiclePublicId, Guid? ServicePublicId)
    : IRequest<Result<TableroDeEnviosDto>>
{
    public const int Tope = 500;
}

public sealed class ListConnectorRequestsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListConnectorRequestsQuery, Result<TableroDeEnviosDto>>
{
    public async Task<Result<TableroDeEnviosDto>> Handle(ListConnectorRequestsQuery request, CancellationToken ct)
    {
        var q = Consultas.Base(db);
        if (request.From is { } desde) q = q.Where(x => x.Envio.CreatedAt >= desde.ToDateTime(TimeOnly.MinValue));
        if (request.To is { } hasta) q = q.Where(x => x.Envio.CreatedAt < hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (request.VehiclePublicId is { } vehiculo) q = q.Where(x => x.Vehiculo == vehiculo);
        if (request.ServicePublicId is { } servicio) q = q.Where(x => x.ServicioId == servicio);

        var conteos = await q.GroupBy(x => x.Envio.Status).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ConnectorRequestStatus>(request.Status, ignoreCase: true, out var estado))
                return Result.Failure<TableroDeEnviosDto>("Connector.Request.StatusUnknown", $"«{request.Status}» no es un estado de envío.");
            q = q.Where(x => x.Envio.Status == estado);
        }
        var filas = await q.OrderByDescending(x => x.Envio.Id).Take(ListConnectorRequestsQuery.Tope + 1).ToListAsync(ct);
        return Result.Success(new TableroDeEnviosDto(
            conteos.ToDictionary(c => c.Key.ToString(), c => c.Total),
            filas.Take(ListConnectorRequestsQuery.Tope).Select(Consultas.Dto).ToList(),
            filas.Count > ListConnectorRequestsQuery.Tope));
    }
}

public sealed record GetConnectorRequestQuery(Guid PublicId) : IRequest<Result<EnvioDetalleDto>>;

public sealed class GetConnectorRequestQueryHandler(IApplicationDbContext db) : IRequestHandler<GetConnectorRequestQuery, Result<EnvioDetalleDto>>
{
    public async Task<Result<EnvioDetalleDto>> Handle(GetConnectorRequestQuery request, CancellationToken ct)
    {
        var fila = await Consultas.Base(db).FirstOrDefaultAsync(x => x.Envio.PublicId == request.PublicId, ct);
        if (fila is null) return Result.Failure<EnvioDetalleDto>("Connector.Request.NotFound", "No existe ese envío.");
        var intentos = await db.ConnectorCalls.AsNoTracking().Where(c => c.RequestId == fila.Envio.Id).OrderBy(c => c.Id)
            .Select(c => new IntentoDeEnvioDto(c.AttemptNumber, c.Kind.ToString(), c.StartedAt, c.DurationMs, c.StatusCode, c.Outcome.ToString(),
                c.ResponseExcerpt, c.RequestBodySanitized, c.Url))
            .ToListAsync(ct);
        return Result.Success(new EnvioDetalleDto(Consultas.Dto(fila), intentos));
    }
}

/// <summary>
/// Reintenta un envío (T162). En revisión (su entrega está rechazada): le devuelve el cupo de intentos —las llamadas anteriores quedan como
/// hechos— y lo reprocesa por la bandeja con la clave de la operación. Reintentando, en espera o pendiente: adelanta su entrega a ahora.
/// Siempre con el mismo identificador externo. Un envío enviado, anulado o cerrado a mano no se reintenta.
/// </summary>
public sealed record RetryConnectorRequestCommand(Guid PublicId) : IRequest<Result>
{
    /// <summary>La clave de la operación (<c>Idempotency-Key</c>), que pasa al reproceso de la bandeja.</summary>
    public Guid OperationKey { get; init; }
}

public sealed class RetryConnectorRequestCommandHandler(IApplicationDbContext db, ISender sender, IDateTimeService reloj)
    : IRequestHandler<RetryConnectorRequestCommand, Result>
{
    public const string Motivo = "Reintento manual desde el tablero de envíos de Transporte.";

    public async Task<Result> Handle(RetryConnectorRequestCommand request, CancellationToken ct)
    {
        var envio = await db.ConnectorRequests.FirstOrDefaultAsync(r => r.PublicId == request.PublicId, ct);
        if (envio is null) return Result.Failure("Connector.Request.NotFound", "No existe ese envío.");
        var entrega = await db.IntegrationMessageDeliveries
            .FirstOrDefaultAsync(d => d.Destination == IntegrationDestinations.Connectors && d.Message!.PublicId == envio.MessagePublicId, ct);

        switch (envio.Status)
        {
            case ConnectorRequestStatus.Review:
                envio.Attempts = 0;
                envio.Status = ConnectorRequestStatus.Retrying;
                await db.SaveChangesAsync(ct);
                if (entrega?.Status == DeliveryStatus.Rejected)
                {
                    var r = await sender.Send(new ReprocessMessagesCommand([envio.MessagePublicId], Motivo, IntegrationDestinations.Connectors)
                    {
                        OperationKey = request.OperationKey,
                    }, ct);
                    if (r.IsFailure) return Result.Failure(r.Error);
                }
                else if (entrega is not null)
                {
                    entrega.NextAttemptAt = reloj.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                return Result.Success();
            case ConnectorRequestStatus.Pending or ConnectorRequestStatus.Retrying or ConnectorRequestStatus.OnHold:
                if (entrega is not null) entrega.NextAttemptAt = reloj.UtcNow;
                await db.SaveChangesAsync(ct);
                return Result.Success();
            default:
                return Result.Failure("Connector.Request.NotRetryable", $"Un envío {Consultas.Estado(envio.Status)} no se reintenta.");
        }
    }
}

/// <summary>Cierra un envío a mano con motivo (T162): lo resuelto en el sistema externo por otra vía. Ya no sale.</summary>
public sealed record ResolveConnectorRequestCommand(Guid PublicId, string Reason) : IRequest<Result>, IConMotivo;

public sealed class ResolveConnectorRequestCommandValidator : AbstractValidator<ResolveConnectorRequestCommand>
{
    public ResolveConnectorRequestCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo.").MaximumLength(500);
}

public sealed class ResolveConnectorRequestCommandHandler(IApplicationDbContext db, ICurrentUserService usuario, IDateTimeService reloj)
    : IRequestHandler<ResolveConnectorRequestCommand, Result>
{
    public async Task<Result> Handle(ResolveConnectorRequestCommand request, CancellationToken ct)
    {
        var envio = await db.ConnectorRequests.FirstOrDefaultAsync(r => r.PublicId == request.PublicId, ct);
        if (envio is null) return Result.Failure("Connector.Request.NotFound", "No existe ese envío.");
        if (envio.Status is ConnectorRequestStatus.Sent or ConnectorRequestStatus.ResolvedManually)
            return Result.Failure("Connector.Request.AlreadyClosed", $"El envío ya está {Consultas.Estado(envio.Status)}.");
        envio.Status = ConnectorRequestStatus.ResolvedManually;
        envio.ResolvedBy = usuario.UserName ?? "sistema";
        envio.ResolvedAt = reloj.UtcNow;
        envio.ResolutionReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal static class Consultas
{
    /// <summary>
    /// Una fila del tablero. Es una clase con propiedades <c>init</c> y no un <c>record</c> posicional a propósito: EF compone filtros, conteos
    /// y orden sobre una proyección por inicialización de miembros, no sobre un constructor (la e2e del recorrido de contado lo destapó).
    /// </summary>
    public sealed class Fila
    {
        public Domain.Entities.Core.Connectors.ConnectorRequest Envio { get; init; } = null!;
        public string Conector { get; init; } = string.Empty;
        public Domain.Entities.Transport.ServiceCharge? Cargo { get; init; }
        public string? Placa { get; init; }
        public string? Interno { get; init; }
        public Guid? Vehiculo { get; init; }
        public Guid? ServicioId { get; init; }
        public string? Servicio { get; init; }
    }

    public static IQueryable<Fila> Base(IApplicationDbContext db) =>
        from e in db.ConnectorRequests.AsNoTracking()
        join c in db.Connectors.AsNoTracking() on e.ConnectorId equals c.Id
        join g in db.ServiceCharges.AsNoTracking() on (Guid?)e.PublicId equals g.NotifiedRequestPublicId into cargos
        from g in cargos.DefaultIfEmpty()
        select new Fila
        {
            Envio = e,
            Conector = c.Name,
            Cargo = g,
            Placa = g != null ? g.Vehicle!.Plate : null,
            Interno = g != null ? g.Vehicle!.InternalNumber : null,
            Vehiculo = g != null ? (Guid?)g.Vehicle!.PublicId : null,
            ServicioId = g != null ? (Guid?)g.Service!.PublicId : null,
            Servicio = g != null ? g.Service!.Name : null,
        };

    public static EnvioDto Dto(Fila f) => new(
        f.Envio.PublicId, f.Envio.Status.ToString(), f.Conector, f.Vehiculo, f.Placa, f.Interno, f.Servicio, f.Cargo?.Period, f.Cargo?.InvoiceNumber,
        f.Cargo?.Amount, f.Envio.ExternalId, f.Envio.Attempts, f.Envio.LastStatusCode, f.Envio.LastResponseExcerpt, f.Envio.LastAttemptAt, f.Envio.SentAt,
        f.Envio.Warning, f.Cargo is { ReviewFlag: not Domain.Enums.Transport.ChargeReviewFlag.None } cargo ? cargo.ReviewFlag.ToString() : null,
        f.Envio.CreatedAt, f.Envio.ResolvedBy, f.Envio.ResolutionReason);

    public static string Estado(ConnectorRequestStatus estado) => estado switch
    {
        ConnectorRequestStatus.Sent => "enviado",
        ConnectorRequestStatus.SentWithWarning => "enviado con aviso",
        ConnectorRequestStatus.PaymentVoided => "con el pago anulado",
        ConnectorRequestStatus.ResolvedManually => "cerrado a mano",
        _ => estado.ToString(),
    };
}
