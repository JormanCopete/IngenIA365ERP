using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (bloque 6B; T0403; T70, C84, C89; api.md §4.5; data-model §2.6): el resultado de las consultas de Cartera a listas restrictivas
// y su liberación por el oficial de cumplimiento. La lista, el detalle y la liberación exigen Compliance.RestrictiveLists.Release (el oficial no es
// un rol de Cartera: no lleva el alcance de oficina y línea, C84). (nuevo)

/// <summary>Una lista consultada (versión). (nuevo)</summary>
public sealed record ScreeningListLoadDto(string ListCode, int Version);

/// <summary>Una coincidencia (api.md §4.5 <c>matches[]</c>). (nuevo)</summary>
public sealed record ScreeningMatchDto(string ListCode, string EntryName, decimal Score, string? Program, bool ByDocument);

/// <summary>Un resultado de listas (api.md §4.5). <see cref="Status"/>: <c>Blocked</c>, <c>Released</c>, <c>NoMatch</c> o <c>Unavailable</c>. (nuevo)</summary>
public sealed record ScreeningResultDto(
    Guid PublicId,
    PersonaConDocumentoRefDto Person,
    ScreeningContext Context,
    Guid? ContextPublicId,
    IReadOnlyList<ScreeningListLoadDto> ListLoads,
    ScreeningOutcome Outcome,
    IReadOnlyList<ScreeningMatchDto> Matches,
    string Status,
    string ChannelCode,
    bool Simulated,
    DateTime QueriedAt,
    UsuarioRefDto? ReleasedBy,
    DateTime? ReleasedAt,
    string? Reason);

/// <summary>Una persona con su documento (rutas del oficial, api.md §4.5). (nuevo)</summary>
public sealed record PersonaConDocumentoRefDto(Guid PersonPublicId, string FullName, string? DocumentNumber);

/// <summary>Los estados calculados de un resultado (api.md §4.5). (nuevo)</summary>
public static class EstadosDeListas
{
    public const string Bloqueado = "Blocked";
    public const string Liberado = "Released";
    public const string SinCoincidencia = "NoMatch";
    public const string NoDisponible = "Unavailable";

    public static string De(ScreeningResult r) => r.Outcome switch
    {
        ScreeningOutcome.NoMatch => SinCoincidencia,
        ScreeningOutcome.Unavailable => NoDisponible,
        _ => r.ReleasedAt is null ? Bloqueado : Liberado,
    };
}

/// <summary>Arma los DTO de resultados de listas. (nuevo)</summary>
public static class ConsultasDeListas
{
    public const string PermisoDelOficial = "Compliance.RestrictiveLists.Release";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<ScreeningResultDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<ScreeningResult> filas, CancellationToken ct)
    {
        var ids = filas.Select(f => f.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new PersonaConDocumentoRefDto(p.PublicId, ReferenciasDeGobierno.Nombre(p), p.TaxId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, filas.Where(f => f.ReleasedByUserId is not null).Select(f => f.ReleasedByUserId!.Value), ct);
        var solicitudes = filas.Where(f => f.ApplicationId is not null).Select(f => f.ApplicationId!.Value).Distinct().ToList();
        var deSolicitudes = await db.CreditApplications.AsNoTracking().Where(a => solicitudes.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.PublicId, ct);
        var cupos = filas.Where(f => f.CreditLimitId is not null).Select(f => f.CreditLimitId!.Value).Distinct().ToList();
        var deCupos = await db.CreditLimits.AsNoTracking().Where(c => cupos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);

        return filas.Select(f => new ScreeningResultDto(f.PublicId, personas[f.PersonId], f.Context,
            f.ApplicationId is { } a ? deSolicitudes.GetValueOrDefault(a) : f.CreditLimitId is { } c ? deCupos.GetValueOrDefault(c) : f.SalePaymentPublicId,
            Leer<ScreeningListLoadDto>(f.ListVersions), f.Outcome, Leer<ScreeningMatchDto>(f.Matches), EstadosDeListas.De(f), f.ChannelCode, f.IsSimulated,
            f.QueriedAt, f.ReleasedByUserId is { } u ? usuarios.GetValueOrDefault(u) : null, f.ReleasedAt, f.ReleaseReason)).ToList();
    }

    /// <summary>¿Tiene quien pregunta el permiso del oficial? (C84: la ruta y el handler lo exigen; sin él, el mismo 404).</summary>
    public static async Task<bool> EsOficialAsync(ICurrentUserPermissions permisos, CancellationToken ct) =>
        permisos.EsMaestroGlobal || (await permisos.ListAsync(ct)).Contains(PermisoDelOficial, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<T> Leer<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// Libera una coincidencia (api.md §4.5, <c>POST /api/lending/screening-results/{id}/release</c>, <c>Compliance.RestrictiveLists.Release</c>, con
/// clave y motivo; quickstart §1.5): sólo el oficial (sin el permiso, el 404); no la libera quien radicó la solicitud o pidió el cupo
/// (<c>Lending.Segregation.Conflict</c>); un resultado sin coincidencia pendiente → <c>Lending.Screening.NotPending</c>. Liberar escribe la
/// liberación una sola vez (<c>[EscrituraUnica]</c>), levanta el bloqueo <c>RestrictiveListMatch</c> que el resultado causó y deja
/// <c>Lending.ScreeningMatch.Released</c>. Nunca se libera por atribución (FR-038). (nuevo)
/// </summary>
public sealed record ReleaseScreeningMatchCommand(Guid ScreeningResultPublicId, string Reason) : IRequest<Result<ScreeningResultDto>>, IOperacionIdempotente,
    IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseScreeningMatchCommandValidator : ValidadorConMotivo<ReleaseScreeningMatchCommand>
{
    public ReleaseScreeningMatchCommandValidator() => RuleFor(x => x.ScreeningResultPublicId).NotEmpty();
}

public sealed class ReleaseScreeningMatchCommandHandler(
    IApplicationDbContext db,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ReleaseScreeningMatchCommand, Result<ScreeningResultDto>>
{
    public async Task<Result<ScreeningResultDto>> Handle(ReleaseScreeningMatchCommand request, CancellationToken ct)
    {
        if (!await ConsultasDeListas.EsOficialAsync(permisos, ct)) return Falla(ErroresDeDecisionesYCupos.ResultadoDeListasInexistente());
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var resultado = await db.ScreeningResults.FirstOrDefaultAsync(r => r.PublicId == request.ScreeningResultPublicId && !r.IsDeleted, ct);
        if (resultado is null) return Falla(ErroresDeDecisionesYCupos.ResultadoDeListasInexistente());
        if (resultado.PuedeLiberar() is { } noPendiente) return Falla(ErroresDeGobierno.De(noPendiente));

        if (await ConflictoAsync(resultado, usuario, ct) is { } conflicto) return Falla(conflicto);

        var ahora = reloj.UtcNow;
        resultado.Liberar(usuario, ahora, request.Reason);
        var clave = resultado.PublicId.ToString();
        var bloqueos = await db.DebtorBlocks.Where(b => b.PersonId == resultado.PersonId && b.Reason == DebtorBlockReason.RestrictiveListMatch
            && b.SourceKey == clave && b.LiftedAt == null && !b.IsDeleted).ToListAsync(ct);
        foreach (var bloqueo in bloqueos) bloqueo.Levantar(reloj.HoyLocal, ahora, usuario, $"Coincidencia liberada por el oficial: {request.Reason.Trim()}");
        await db.SaveChangesAsync(ct);
        await auditoria.EmitirCoincidenciaLiberadaAsync(resultado.PublicId, request.Reason.Trim(), ct);
        return Result.Success((await ConsultasDeListas.DtosAsync(db, [resultado], ct))[0]);
    }

    /// <summary>
    /// La segregación de la liberación (T70, C89): no libera quien radicó o armó la solicitud del resultado, ni quien pidió el cupo; tampoco quien
    /// intervino como radicador en el expediente (por su persona).
    /// </summary>
    private async Task<Error?> ConflictoAsync(ScreeningResult r, int usuario, CancellationToken ct)
    {
        (string Tipo, Guid PublicId, DateTime? Cuando, bool Fue)? expediente = null;
        if (r.ApplicationId is { } a && await db.CreditApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a, ct) is { } s)
            expediente = (SubmitCreditApplicationCommandHandler.Expediente, s.PublicId, s.SubmittedAt ?? s.CreatedAt,
                s.SubmittedByUserId == usuario || s.CreatedByUserId == usuario);
        else if (r.CreditLimitId is { } c && await db.CreditLimits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c, ct) is { } cupo)
            expediente = (ExpedienteDeCupo.Tipo, cupo.PublicId, cupo.RegisteredAt, cupo.AssignedByUserId == usuario);
        if (expediente is not { } e) return null;

        var persona = await db.Users.AsNoTracking().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        var porPersona = persona is { } p && await db.CaseParticipations.AsNoTracking()
            .AnyAsync(x => x.CaseRootPublicId == e.PublicId && x.PersonId == p && x.Action == AccionesDeSegregacion.Radicar, ct);
        return e.Fue || porPersona
            ? new ErrorConDatos(RegistroDeIntervenciones.CodigoConflicto,
                "Usted radicó o pidió esta operación y no puede liberar su coincidencia en listas. Otra persona con el permiso del oficial debe hacerlo.",
                new { action = "Release", previousAction = AccionesDeSegregacion.Radicar, previousAt = e.Cuando })
            : null;
    }

    private static Result<ScreeningResultDto> Falla(Error e) => Result.Failure<ScreeningResultDto>(e);
}

/// <summary>
/// Los resultados de listas (api.md §4.5, <c>GET /api/lending/screening-results?status=&amp;personPublicId=&amp;page=</c>,
/// <c>Compliance.RestrictiveLists.Release</c>): por estado calculado y persona, los más recientes primero. Sin el permiso del oficial, el 404
/// genérico (C84). El oficial no lleva el alcance de Cartera: no filtra por oficina ni línea (exceptuada con motivo). (nuevo)
/// </summary>
public sealed record ListScreeningResultsQuery(string? Status = null, Guid? PersonPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<ScreeningResultDto>>>;

public sealed class ListScreeningResultsQueryValidator : AbstractValidator<ListScreeningResultsQuery>
{
    public ListScreeningResultsQueryValidator() =>
        RuleFor(x => x.Status).Must(s => s is null or EstadosDeListas.Bloqueado or EstadosDeListas.Liberado or EstadosDeListas.SinCoincidencia
                or EstadosDeListas.NoDisponible)
            .WithMessage("El estado es Blocked, Released, NoMatch o Unavailable.");
}

public sealed class ListScreeningResultsQueryHandler(IApplicationDbContext db, ICurrentUserPermissions permisos)
    : IRequestHandler<ListScreeningResultsQuery, Result<PagedResult<ScreeningResultDto>>>
{
    public async Task<Result<PagedResult<ScreeningResultDto>>> Handle(ListScreeningResultsQuery request, CancellationToken ct)
    {
        if (!await ConsultasDeListas.EsOficialAsync(permisos, ct)) return Result.Failure<PagedResult<ScreeningResultDto>>(Error.NotFound);
        var consulta = db.ScreeningResults.AsNoTracking().Where(r => !r.IsDeleted);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(r => db.People.Any(p => p.Id == r.PersonId && p.PublicId == persona));
        consulta = request.Status switch
        {
            EstadosDeListas.Bloqueado => consulta.Where(r => (r.Outcome == ScreeningOutcome.Match || r.Outcome == ScreeningOutcome.PossibleMatch) && r.ReleasedAt == null),
            EstadosDeListas.Liberado => consulta.Where(r => (r.Outcome == ScreeningOutcome.Match || r.Outcome == ScreeningOutcome.PossibleMatch) && r.ReleasedAt != null),
            EstadosDeListas.SinCoincidencia => consulta.Where(r => r.Outcome == ScreeningOutcome.NoMatch),
            EstadosDeListas.NoDisponible => consulta.Where(r => r.Outcome == ScreeningOutcome.Unavailable),
            _ => consulta,
        };

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(r => r.QueriedAt).ThenByDescending(r => r.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<ScreeningResultDto>(await ConsultasDeListas.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Un resultado con su evidencia (api.md §4.5, <c>GET /{id}</c>, <c>Compliance.RestrictiveLists.Release</c>; sin el permiso, el 404). (nuevo)</summary>
public sealed record GetScreeningResultQuery(Guid ScreeningResultPublicId) : IRequest<Result<ScreeningResultDto>>;

public sealed class GetScreeningResultQueryValidator : AbstractValidator<GetScreeningResultQuery>
{
    public GetScreeningResultQueryValidator() => RuleFor(x => x.ScreeningResultPublicId).NotEmpty();
}

public sealed class GetScreeningResultQueryHandler(IApplicationDbContext db, ICurrentUserPermissions permisos)
    : IRequestHandler<GetScreeningResultQuery, Result<ScreeningResultDto>>
{
    public async Task<Result<ScreeningResultDto>> Handle(GetScreeningResultQuery request, CancellationToken ct)
    {
        if (!await ConsultasDeListas.EsOficialAsync(permisos, ct)) return Result.Failure<ScreeningResultDto>(ErroresDeDecisionesYCupos.ResultadoDeListasInexistente());
        var r = await db.ScreeningResults.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ScreeningResultPublicId && !x.IsDeleted, ct);
        return r is null
            ? Result.Failure<ScreeningResultDto>(ErroresDeDecisionesYCupos.ResultadoDeListasInexistente())
            : Result.Success((await ConsultasDeListas.DtosAsync(db, [r], ct))[0]);
    }
}
