using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Compliance.DataSubjectRequests;

/// <summary>Una solicitud del titular (contracts/api.md §22.3). <see cref="DutyEvidenceJson"/> sólo en el detalle. (nuevo)</summary>
public sealed record DataSubjectRequestDto(
    Guid PublicId,
    DataSubjectRequestPersonDto Person,
    DataSubjectRequestKind Kind,
    DataAuthorizationPurpose? Purpose,
    DataAuthorizationChannel? ContactChannel,
    string Channel,
    DateTime ReceivedAt,
    DateOnly DueDate,
    bool Extended,
    DateOnly? ExtendedDueDate,
    DataSubjectRequestStatus Status,
    DataSubjectRequestOutcome? Outcome,
    string Description,
    string? ResponseText,
    DateTime? RespondedAt,
    string? RespondedBy,
    string? DutyBasis,
    string? DutyEvidenceJson);

/// <summary>El titular en la solicitud: su <c>PublicId</c> y, en la bandeja, su nombre y documento. (nuevo)</summary>
public sealed record DataSubjectRequestPersonDto(Guid PublicId, string? Name = null, string? TaxId = null);

internal static class ProyeccionDeSolicitudes
{
    public static DataSubjectRequestDto ADto(DataSubjectRequest s, Guid persona, string? nombre = null, string? documento = null, bool conConstancia = true) => new(
        s.PublicId, new DataSubjectRequestPersonDto(persona, nombre, documento), s.Kind, s.Purpose, s.ContactChannel, s.ReceivedVia, s.ReceivedAt,
        s.DueDate, s.ExtendedDueDate is not null, s.ExtendedDueDate, s.Status, s.Outcome, s.Description, s.ResponseText, s.RespondedAt,
        s.RespondedByName, s.DutyBasis, conConstancia ? s.DutyEvidenceJson : null);
}

/// <summary>
/// La bandeja de solicitudes (<c>GET /api/compliance/data-subject-requests?status=&amp;kind=&amp;personPublicId=&amp;dueBefore=&amp;page=</c>),
/// por vencimiento. <c>dueBefore</c> compara con el vencimiento que rige (prorrogado si lo hay). (nuevo)
/// </summary>
public sealed record ListDataSubjectRequestsQuery(
    DataSubjectRequestStatus? Status = null,
    DataSubjectRequestKind? Kind = null,
    Guid? PersonPublicId = null,
    DateOnly? DueBefore = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<DataSubjectRequestDto>>>;

public sealed class ListDataSubjectRequestsQueryValidator : AbstractValidator<ListDataSubjectRequestsQuery>
{
    public ListDataSubjectRequestsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
    }
}

public sealed class ListDataSubjectRequestsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListDataSubjectRequestsQuery, Result<PagedResult<DataSubjectRequestDto>>>
{
    public async Task<Result<PagedResult<DataSubjectRequestDto>>> Handle(ListDataSubjectRequestsQuery request, CancellationToken ct)
    {
        var pagina = new PageRequest(request.Page, request.PageSize);
        var consulta = db.DataSubjectRequests.AsNoTracking();
        if (request.Status is { } estado) consulta = consulta.Where(s => s.Status == estado);
        if (request.Kind is { } clase) consulta = consulta.Where(s => s.Kind == clase);
        if (request.PersonPublicId is { } titular)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == titular).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => s.PersonId == (id ?? -1));
        }
        if (request.DueBefore is { } antes) consulta = consulta.Where(s => (s.ExtendedDueDate ?? s.DueDate) <= antes);

        var total = await consulta.LongCountAsync(ct);
        var solicitudes = await consulta.OrderBy(s => s.ExtendedDueDate ?? s.DueDate).ThenBy(s => s.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var ids = solicitudes.Select(s => s.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var items = solicitudes.Select(s =>
        {
            var p = personas.GetValueOrDefault(s.PersonId);
            return ProyeccionDeSolicitudes.ADto(s, p?.PublicId ?? Guid.Empty, p is null ? null : NombreDePersona.Completo(p), p?.TaxId, conConstancia: false);
        }).ToList();
        return Result.Success(new PagedResult<DataSubjectRequestDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Una solicitud con su respuesta y, en revocación, la constancia del deber (<c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetDataSubjectRequestQuery(Guid RequestPublicId) : IRequest<Result<DataSubjectRequestDto>>;

public sealed class GetDataSubjectRequestQueryValidator : AbstractValidator<GetDataSubjectRequestQuery>
{
    public GetDataSubjectRequestQueryValidator() => RuleFor(x => x.RequestPublicId).NotEmpty();
}

public sealed class GetDataSubjectRequestQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDataSubjectRequestQuery, Result<DataSubjectRequestDto>>
{
    public async Task<Result<DataSubjectRequestDto>> Handle(GetDataSubjectRequestQuery request, CancellationToken ct)
    {
        var s = await db.DataSubjectRequests.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.RequestPublicId, ct);
        if (s is null) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.NoEncontrada);
        var p = await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.PersonId, ct);
        return Result.Success(ProyeccionDeSolicitudes.ADto(s, p?.PublicId ?? Guid.Empty, p is null ? null : NombreDePersona.Completo(p), p?.TaxId));
    }
}

// ------------------------------------------------------------------------------------------------------- plazos por vencer --

/// <summary>
/// Levanta <c>Cumplimiento.PlazoDelTitularPorVencer</c> por cada solicitud abierta que vence dentro de los días de anticipación del tipo
/// (umbral <c>diasDeAnticipacion</c>, por defecto <see cref="DiasDeAnticipacionPorDefecto"/>) o ya venció sin respuesta. Una alerta por
/// solicitud (<c>DedupKey</c> <c>Cumplimiento.PlazoDelTitularPorVencer:{solicitud}</c>): mientras siga pendiente, cada pasada suma
/// ocurrencia. La corre la tarea diaria <c>cumplimiento.plazos-del-titular</c>. (nuevo)
/// </summary>
public sealed record AlertDataSubjectRequestDeadlinesCommand : IRequest<Result<int>>;

public sealed class AlertDataSubjectRequestDeadlinesCommandValidator : AbstractValidator<AlertDataSubjectRequestDeadlinesCommand>;

public sealed class AlertDataSubjectRequestDeadlinesCommandHandler(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<AlertDataSubjectRequestDeadlinesCommand, Result<int>>
{
    /// <summary>Días calendario de anticipación si el tipo no trae umbral (no es un valor legal: es cuándo avisar).</summary>
    public const int DiasDeAnticipacionPorDefecto = 3;

    public async Task<Result<int>> Handle(AlertDataSubjectRequestDeadlinesCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var anticipacion = await AnticipacionAsync(hoy, ct);
        var limite = hoy.AddDays(anticipacion);
        var abiertas = await db.DataSubjectRequests.AsNoTracking()
            .Where(s => (s.Status == DataSubjectRequestStatus.Received || s.Status == DataSubjectRequestStatus.Extended)
                && (s.ExtendedDueDate ?? s.DueDate) <= limite)
            .OrderBy(s => s.ExtendedDueDate ?? s.DueDate)
            .ToListAsync(ct);

        var levantadas = 0;
        foreach (var s in abiertas)
        {
            var vence = s.VenceEl;
            var cuerpo = vence < hoy
                ? $"La solicitud del titular ({s.Kind}) recibida el {s.ReceivedOn:yyyy-MM-dd} venció el {vence:yyyy-MM-dd} sin respuesta. Respóndala en Cumplimiento › Solicitudes del titular."
                : $"La solicitud del titular ({s.Kind}) recibida el {s.ReceivedOn:yyyy-MM-dd} vence el {vence:yyyy-MM-dd}. Respóndala o prorróguela (una vez) en Cumplimiento › Solicitudes del titular.";
            var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PlazoDelTitularPorVencer,
                "Solicitud del titular por vencer", cuerpo, EntityType: "DataSubjectRequest", EntityPublicId: s.PublicId,
                DedupKey: $"{TiposDeAlerta.PlazoDelTitularPorVencer}:{s.PublicId:D}"), ct);
            if (r.IsSuccess && r.Value.Desenlace != DesenlaceDeAlerta.TipoInactivo) levantadas++;
        }
        return Result.Success(levantadas);
    }

    private async Task<int> AnticipacionAsync(DateOnly hoy, CancellationToken ct)
    {
        var umbrales = await db.AlertTypes.AsNoTracking()
            .Where(t => t.TypeCode == TiposDeAlerta.PlazoDelTitularPorVencer && t.ValidFrom <= hoy && (t.ValidTo == null || t.ValidTo >= hoy))
            .OrderByDescending(t => t.ValidFrom)
            .Select(t => t.ThresholdsJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(umbrales)) return DiasDeAnticipacionPorDefecto;
        try
        {
            var leidos = JsonSerializer.Deserialize<Dictionary<string, decimal>>(umbrales);
            return leidos is not null && leidos.TryGetValue(TiposDeAlerta.DiasDeAnticipacion, out var dias) && dias >= 0 ? (int)dias : DiasDeAnticipacionPorDefecto;
        }
        catch (JsonException)
        {
            return DiasDeAnticipacionPorDefecto;
        }
    }
}

/// <summary>
/// La tarea diaria <c>cumplimiento.plazos-del-titular</c> (feature 013, T0088): desde <see cref="HoraDeInicio"/> local, una vez al día, envía
/// <see cref="AlertDataSubjectRequestDeadlinesCommand"/> en cada cooperativa. Idempotente: repetirla suma ocurrencias de las mismas
/// alertas. Se registra como singleton en la API. (nuevo)
/// </summary>
public sealed class TareaDePlazosDelTitular : ITareaProgramada
{
    public const string NombreDeLaTarea = "cumplimiento.plazos-del-titular";

    /// <summary>A primera hora, antes de la jornada.</summary>
    public static readonly TimeOnly HoraDeInicio = new(6, 0);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay) >= HoraDeInicio
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new AlertDataSubjectRequestDeadlinesCommand(), ct);
        if (r.IsFailure)
            throw new InvalidOperationException($"La revisión de plazos del titular falló: {r.Error.Code} {r.Error.Message}");
    }
}
