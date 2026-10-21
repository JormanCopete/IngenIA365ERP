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
    PageRequest Paging) : IRequest<Result<PagedResult<AuditLogEntryDto>>>
{
    /// <summary>
    /// Feature 012 (T423, FR-007): varios módulos a la vez; la consola ofrece los encadenados
    /// (<c>AuditoriaEncadenada.Modulos</c>). Nulo o vacío no filtra.
    /// </summary>
    public IReadOnlyList<string>? Modules { get; init; }

    /// <summary>Feature 012 (T423; <c>?result=</c>): <c>Rejected</c> sólo los rechazos, <c>Accepted</c> lo demás; nulo, todo.</summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// Feature 013 (US5, T0560; api.md §24, A11): <c>?operationKey=</c> trae juntos el intento (<c>Lending.ObligationContract.Attempted</c>) y el hecho
    /// del contrato de obligaciones; un intento sin hecho pasado el tiempo máximo de una transacción se rotula «revertido con el recaudador».
    /// </summary>
    public string? OperationKey { get; init; }
}

public sealed class QueryAuditLogQueryValidator : AbstractValidator<QueryAuditLogQuery>
{
    /// <summary>Máximo rango aceptado en consulta interactiva.</summary>
    public static readonly TimeSpan MaxInteractiveRange = TimeSpan.FromDays(180);

    public QueryAuditLogQueryValidator()
    {
        RuleFor(x => x.Outcome)
            .Must(r => r is null or "Rejected" or "Accepted")
            .WithMessage("«result» admite Rejected o Accepted.");
        RuleForEach(x => x.Modules).NotEmpty().MaximumLength(60);
        RuleFor(x => x.OperationKey).MaximumLength(200);

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
    /// <summary>
    /// La cooperativa sale de aquí y NO de <c>ICurrentUserService.TenantId</c>.
    ///
    /// <para>
    /// No son lo mismo y esa confusión costaba caro: <c>ICurrentUserService</c>
    /// devuelve el <b>Id interno</b> —«3»— y el escritor de auditoría usa el
    /// <b>PublicId</b> en formato N. Como el nombre de la base de auditoría se
    /// compone con ese identificador, el rastro se escribía en
    /// <c>…_Audit_{guid}</c> y la consola leía <c>…_Audit_3</c>: una base vacía
    /// que ni siquiera existe. Sin error, sin log, sin nada. La consola mostraba
    /// «no hay eventos» mientras el rastro se guardaba correctamente al lado.
    /// </para>
    /// </summary>
    private readonly ICurrentTenantService _cooperativaActual;

    private readonly IDateTimeService? _reloj;

    /// <summary>
    /// Feature 013 (A11): lo que puede durar la transacción de un recaudador. Un intento del contrato sin su hecho después de esto fue revertido con el
    /// recaudador (el intento se escribe aparte y sobrevive; el hecho viaja en la misma transacción).
    /// </summary>
    public static readonly TimeSpan TiempoMaximoDeUnaTransaccion = TimeSpan.FromMinutes(5);

    /// <summary>El rótulo de un intento del contrato sin hecho (A11).</summary>
    public const string RotuloRevertidoConElRecaudador = "Revertido con el recaudador";

    private static readonly HashSet<string> HechosDelContrato =
    [
        IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingObligationContractApplied,
        IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingObligationContractReversed,
        IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingObligationContractCreditBalanceResolved,
        IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingObligationContractRejected,
    ];

    public QueryAuditLogQueryHandler(IAuditService audit, ICurrentTenantService cooperativaActual, IDateTimeService? reloj = null)
    {
        _audit = audit;
        _cooperativaActual = cooperativaActual;
        _reloj = reloj;
    }

    public async Task<Result<PagedResult<AuditLogEntryDto>>> Handle(
        QueryAuditLogQuery request, CancellationToken ct)
    {
        // Aislamiento por tenant (FR-004): el TenantId SIEMPRE viene del
        // claim del usuario actual, NUNCA del cuerpo del query. Sin tenant
        // no hay query — devuelve 401-equivalente vía envelope.
        var tenantId = _cooperativaActual.TenantId;
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
            PageSize = pageSize,
            Modules = request.Modules?.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct().ToList(),
            Outcome = NullIfBlank(request.Outcome),
            OperationKey = NullIfBlank(request.OperationKey)?.Trim(),
        };

        var paged = await _audit.QueryAsync(serviceParams, ct);

        var items = paged.Items.Select(MapToDto).ToList();
        if (serviceParams.OperationKey is not null) items = Rotular(items);
        var result = new PagedResult<AuditLogEntryDto>(
            items, page, pageSize, paged.TotalCount);

        return Result.Success(result);
    }

    /// <summary>
    /// A11: con <c>?operationKey=</c>, un intento del contrato sin hecho entre él y el intento siguiente de la misma clave (o ahora), pasado el tiempo
    /// máximo de una transacción, se rotula «revertido con el recaudador»: el recaudador deshizo su transacción y con ella el hecho que Cartera había
    /// dejado dentro. El hecho de un reintento posterior no cuenta para el intento anterior.
    /// </summary>
    private List<AuditLogEntryDto> Rotular(List<AuditLogEntryDto> items)
    {
        const string intento = IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingObligationContractAttempted;
        var ahora = _reloj?.UtcNow ?? DateTime.UtcNow;
        var hechos = items.Where(i => HechosDelContrato.Contains(i.Action)).Select(i => i.Timestamp).ToList();
        var intentos = items.Where(i => i.Action == intento).Select(i => i.Timestamp).Order().ToList();
        bool Revertido(AuditLogEntryDto i)
        {
            var siguiente = intentos.Where(t => t > i.Timestamp).Cast<DateTime?>().FirstOrDefault() ?? DateTime.MaxValue;
            return !hechos.Any(h => h >= i.Timestamp && h < siguiente) && ahora - i.Timestamp > TiempoMaximoDeUnaTransaccion;
        }
        return items.Select(i => i.Action == intento && Revertido(i) ? i with { Label = RotuloRevertidoConElRecaudador } : i).ToList();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Meta(AuditLogEntry e, string clave) =>
        e.Metadata is { } m && m.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

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
        ChangedFields: e.ChangedFields,
        Channel: Meta(e, "Channel"),
        ActorKind: Meta(e, "ActorKind"),
        Origin: Meta(e, "Origin"),
        Reason: Meta(e, "Reason"),
        Result: e.Action == IngenIA365ERP.Application.Common.Audit.AuditEventTypes.CommandRejected ? "Rejected" : "Accepted",
        ErrorCode: Meta(e, "ErrorCode"),
        OperationKey: Meta(e, "OperationKey"),
        ChainSeq: e.ChainSeq);
}
