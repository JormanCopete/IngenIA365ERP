using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Una calificación por análisis (api.md §12.2). (nuevo)</summary>
public sealed record AnalysisRatingDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    string Category,
    string? Subcategory,
    string Reason,
    Guid? SupportAttachmentPublicId,
    DateOnly Since,
    DateTime SetAt,
    DateTime? LiftedAt,
    string? LiftReason);

/// <summary>
/// <c>POST /api/lending/risk/analysis-ratings</c> (feature 013, US9, T0701; api.md §12.2, <c>Lending.Ratings.Override</c>; FR-101, data-model §23.6):
/// pone una calificación por análisis a un crédito del alcance (fuera, el 404 del crédito). Reglas: el motivo es obligatorio; sólo la E lleva
/// subcategoría; una vigente por crédito (<c>Lending.AnalysisRating.AlreadyActive</c>); la categoría tiene que ser <b>peor</b> que la que dan las
/// reglas hoy —el mismo motor de la calificación informativa, sin análisis— (<c>Lending.AnalysisRating.NotWorse</c> con <c>data.ruleCategory</c>):
/// el análisis nunca mejora (FR-099). El cierre la lee a R. (nuevo)
/// </summary>
public sealed record SetAnalysisRatingCommand(
    Guid LoanPublicId,
    RatingCategory Category,
    RatingSubcategory? Subcategory,
    string Reason,
    Guid? SupportAttachmentPublicId = null) : IRequest<Result<AnalysisRatingDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetAnalysisRatingCommandValidator : ValidadorConMotivo<SetAnalysisRatingCommand>
{
    public SetAnalysisRatingCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Subcategory).IsInEnum().When(x => x.Subcategory is not null);
        RuleFor(x => x.Subcategory).Must((c, s) => s is null or RatingSubcategory.None || c.Category == RatingCategory.E)
            .WithMessage("Sólo la categoría E lleva subcategoría (E1 o E2).");
    }
}

public sealed class SetAnalysisRatingCommandHandler(
    IApplicationDbContext db,
    CalificacionInformativa informativa,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SetAnalysisRatingCommand, Result<AnalysisRatingDto>>
{
    public async Task<Result<AnalysisRatingDto>> Handle(SetAnalysisRatingCommand request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<AnalysisRatingDto>(leido.Error);
        var credito = leido.Value;

        var vigente = await db.AnalysisRatings.AsNoTracking().FirstOrDefaultAsync(a => a.LoanId == credito.Id && a.LiftedAt == null && !a.IsDeleted, ct);
        if (vigente is not null) return Result.Failure<AnalysisRatingDto>(ErroresDeRiesgo.AnalisisYaVigente(vigente.PublicId));

        var hoy = reloj.HoyLocal;
        var reglas = await informativa.CalcularAsync([credito.Id], hoy, null, sinAnalisis: true, ct);
        if (reglas.IsFailure) return Result.Failure<AnalysisRatingDto>(reglas.Error);
        if (reglas.Value.Errores.TryGetValue(credito.Id, out var error)) return Result.Failure<AnalysisRatingDto>(error);
        var deLasReglas = reglas.Value.Calculados.Single(c => c.Credito.Credito.Id == credito.Id).Calificacion.Categoria;
        if (request.Category <= deLasReglas) return Result.Failure<AnalysisRatingDto>(ErroresDeRiesgo.AnalisisNoEmpeora(request.Category, deLasReglas));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AnalysisRatingDto>(ErroresDeGobierno.SinUsuario());
        var analisis = new AnalysisRating
        {
            LoanId = credito.Id,
            Rating = request.Category,
            Subcategory = request.Category == RatingCategory.E ? request.Subcategory ?? RatingSubcategory.None : RatingSubcategory.None,
            EffectiveFrom = hoy,
            Reason = request.Reason.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            SetByUserId = usuario,
            SetAt = reloj.UtcNow,
        };
        db.AnalysisRatings.Add(analisis);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConsultasDeAnalisis.Dto(analisis, credito.PublicId, credito.Number));
    }
}

/// <summary>
/// <c>POST /api/lending/risk/analysis-ratings/{id}/lift</c> (feature 013, US9, T0701; <c>Lending.Ratings.Override</c>): levanta con motivo la
/// calificación por análisis vigente; una levantada o inexistente responde <c>Lending.AnalysisRating.NotActive</c> (o el 404). (nuevo)
/// </summary>
public sealed record LiftAnalysisRatingCommand(Guid AnalysisRatingPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class LiftAnalysisRatingCommandValidator : ValidadorConMotivo<LiftAnalysisRatingCommand>
{
    public LiftAnalysisRatingCommandValidator() => RuleFor(x => x.AnalysisRatingPublicId).NotEmpty();
}

public sealed class LiftAnalysisRatingCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<LiftAnalysisRatingCommand, Result>
{
    public async Task<Result> Handle(LiftAnalysisRatingCommand request, CancellationToken ct)
    {
        var analisis = await db.AnalysisRatings.FirstOrDefaultAsync(a => a.PublicId == request.AnalysisRatingPublicId && !a.IsDeleted, ct);
        if (analisis is null) return Result.Failure(ErroresDeRiesgo.AnalisisInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == analisis.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeRiesgo.AnalisisInexistente(), auditoria, nameof(AnalysisRating),
            analisis.PublicId, ct);
        if (dentro.IsFailure) return dentro;
        if (!analisis.Vigente) return Result.Failure(ErroresDeRiesgo.AnalisisNoVigente());

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());
        analisis.Levantar(usuario, reloj.UtcNow, reloj.HoyLocal, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// <c>GET /api/lending/risk/analysis-ratings?active=&amp;page=</c> (feature 013, US9, T0701; <c>Lending.Ratings.Read</c>): las calificaciones por
/// análisis de los créditos del alcance de oficina y línea (lo de fuera no aparece), las vigentes o todas. (nuevo)
/// </summary>
public sealed record ListAnalysisRatingsQuery(bool? Active = null, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<AnalysisRatingDto>>>;

public sealed class ListAnalysisRatingsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListAnalysisRatingsQuery, Result<PagedResult<AnalysisRatingDto>>>
{
    public async Task<Result<PagedResult<AnalysisRatingDto>>> Handle(ListAnalysisRatingsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        var consulta = from a in db.AnalysisRatings.AsNoTracking()
                       join l in creditos on a.LoanId equals l.Id
                       where !a.IsDeleted
                       select new { Analisis = a, l.PublicId, l.Number };
        if (request.Active is true) consulta = consulta.Where(x => x.Analisis.LiftedAt == null);
        if (request.Active is false) consulta = consulta.Where(x => x.Analisis.LiftedAt != null);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.Analisis.SetAt).ThenByDescending(x => x.Analisis.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<AnalysisRatingDto>(filas.Select(x => ConsultasDeAnalisis.Dto(x.Analisis, x.PublicId, x.Number)).ToList(),
            pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Lo que comparten los comandos y la lista de análisis. (nuevo)</summary>
public static class ConsultasDeAnalisis
{
    public static AnalysisRatingDto Dto(AnalysisRating a, Guid loanPublicId, string loanNumber) =>
        new(a.PublicId, loanPublicId, loanNumber, a.Rating.ToString(), a.Subcategory == RatingSubcategory.None ? null : a.Subcategory.ToString(),
            a.Reason, a.SupportAttachmentPublicId, a.EffectiveFrom, a.SetAt, a.LiftedAt, a.LiftReason);
}
