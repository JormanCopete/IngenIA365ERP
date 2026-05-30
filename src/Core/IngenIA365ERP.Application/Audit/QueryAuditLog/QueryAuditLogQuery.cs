using FluentValidation;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;

namespace IngenIA365ERP.Application.Audit.QueryAuditLog;

/// <summary>
/// T087 — Consulta paginada del audit log (FR-024 / SC-004). Wrapper CQRS
/// sobre <see cref="IAuditService.QueryAsync"/> que añade:
///  * Validación de rango ≤ 6 meses (consulta interactiva, evitar OOM en
///    front). Si el rango excede, el cliente debe usar el endpoint export.
///  * Resolución implícita del <c>TenantId</c> a partir del usuario actual
///    (no se acepta tenant override por query string — FR-004 aislamiento).
///  * Mapeo a <see cref="AuditLogEntryDto"/> con campos limpios y JSON
///    serializado para old/new values.
/// </summary>
public sealed record QueryAuditLogQuery(
    string? UserId,
    string? EntityType,
    string? EntityId,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To,
    PageRequest Paging) : IRequest<Result<PagedResult<AuditLogEntryDto>>>;

public sealed class QueryAuditLogQueryValidator : AbstractValidator<QueryAuditLogQuery>
{
    /// <summary>Máximo rango aceptado en consulta interactiva.</summary>
    public static readonly TimeSpan MaxInteractiveRange = TimeSpan.FromDays(180);

    public QueryAuditLogQueryValidator()
    {
        RuleFor(x => x).Custom((query, ctx) =>
        {
            if (query.From is not null && query.To is not null)
            {
                if (query.To.Value < query.From.Value)
                {
                    ctx.AddFailure(nameof(query.To),
                        "El rango 'To' debe ser posterior a 'From'.");
                    return;
                }

                if (query.To.Value - query.From.Value > MaxInteractiveRange)
                {
                    ctx.AddFailure(nameof(query.To),
                        $"El rango excede 6 meses (máximo {MaxInteractiveRange.TotalDays:N0} días). " +
                        "Usa el endpoint /export para rangos mayores.");
                }
            }
        });
    }
}

public sealed class QueryAuditLogQueryHandler
    : IRequestHandler<QueryAuditLogQuery, Result<PagedResult<AuditLogEntryDto>>>
{
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public QueryAuditLogQueryHandler(IAuditService audit, ICurrentUserService currentUser)
    {
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<AuditLogEntryDto>>> Handle(
        QueryAuditLogQuery request, CancellationToken ct)
    {
        // Aislamiento por tenant (FR-004): el TenantId SIEMPRE viene del
        // claim del usuario actual, NUNCA del cuerpo del query. Sin tenant
        // no hay query — devuelve 401-equivalente vía envelope.
        var tenantId = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<PagedResult<AuditLogEntryDto>>(
                "Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;

        var serviceParams = new AuditQueryParameters
        {
            TenantId = tenantId,
            UserId = NullIfBlank(request.UserId),
            EntityType = NullIfBlank(request.EntityType),
            EntityId = NullIfBlank(request.EntityId),
            Module = NullIfBlank(request.Module),
            Action = NullIfBlank(request.Action),
            From = request.From,
            To = request.To,
            PageNumber = page,
            PageSize = pageSize
        };

        var paged = await _audit.QueryAsync(serviceParams, ct);

        var items = paged.Items.Select(MapToDto).ToList();
        var result = new PagedResult<AuditLogEntryDto>(
            items, page, pageSize, paged.TotalCount);

        return Result.Success(result);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static AuditLogEntryDto MapToDto(AuditLogEntry e) => new(
        Id: e.Id,
        TenantId: e.TenantId ?? string.Empty,
        UserId: e.UserId ?? string.Empty,
        UserName: e.UserName ?? string.Empty,
        Action: e.Action,
        EntityType: e.EntityType,
        EntityId: e.EntityId,
        Module: e.Module ?? string.Empty,
        IpAddress: e.IpAddress,
        Endpoint: e.Endpoint,
        // HttpMethod/HttpStatusCode no están en la proyección actual de
        // AuditLogEntry (legacy). T091 puede ampliarlos cuando reescriba
        // el endpoint REST si los necesita en la UI.
        HttpMethod: null,
        HttpStatusCode: null,
        DurationMs: e.DurationMs,
        Timestamp: e.Timestamp,
        OldValuesJson: e.OldValues,
        NewValuesJson: e.NewValues,
        ChangedFields: e.ChangedFields);
}
