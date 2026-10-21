using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk.Evaluations;

/// <summary>
/// Prepara una evaluación de cartera (feature 013, K3, US14, T0912; api.md §12.6, <c>POST /api/lending/risk/evaluations</c>,
/// <c>Lending.PortfolioEvaluations.Prepare</c>, con clave; FR-102): el corte (fin de mes) y el mes de efecto (<c>AAAA-MM</c>, posterior al corte) que,
/// si el documento <c>TiposDeOrganizacion</c> vigente dice la periodicidad del tipo de entidad del perfil regulatorio (semestral o anual) y no son los
/// suyos, se prepara igual con el aviso <c>PortfolioEvaluationOutsidePeriodicity</c> en <c>warnings[]</c> (decisión del dueño del 2026-10-18: las
/// periodicidades regulatorias son informativas; hasta esa fecha respondía <c>Lending.PortfolioEvaluation.PeriodInvalid</c>); otra evaluación sin
/// aplicar del mismo corte → <c>Lending.PortfolioEvaluation.Overlaps</c>;
/// cada crédito, vivo al corte y del alcance, con su calificación por las reglas al corte (la oficial del cierre de ese día, <c>RuleRatingAtCutoff</c>),
/// la propuesta y su justificación. Nace en borrador, numerada (<c>EVC-</c>); <c>warnings[]</c> trae la periodicidad de la norma si sigue pendiente de
/// validar (bloque D, <see cref="NormaPendienteDeCartera"/>; no bloquea). (nuevo)
/// </summary>
public sealed record PreparePortfolioEvaluationCommand(
    DateOnly CutoffDate,
    string EffectMonth,
    string? Criteria,
    IReadOnlyList<PortfolioEvaluationLoanInput> Loans) : IRequest<Result<PortfolioEvaluationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>El mes de efecto leído (<c>AAAA-MM</c>), o nulo si no tiene esa forma.</summary>
    public static (int Anio, int Mes)? Mes(string? texto) =>
        DateOnly.TryParseExact($"{texto?.Trim()}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (d.Year, d.Month) : null;
}

public sealed class PreparePortfolioEvaluationCommandValidator : AbstractValidator<PreparePortfolioEvaluationCommand>
{
    public PreparePortfolioEvaluationCommandValidator()
    {
        RuleFor(x => x.CutoffDate).Must(d => d.AddDays(1).Day == 1).WithMessage("El corte de la evaluación es el último día de un mes.");
        RuleFor(x => x.EffectMonth).Must(m => PreparePortfolioEvaluationCommand.Mes(m) is not null)
            .WithMessage("Diga el mes de efecto como AAAA-MM.");
        RuleFor(x => x).Must(x => PreparePortfolioEvaluationCommand.Mes(x.EffectMonth) is not { } m || m.Anio * 12 + m.Mes > x.CutoffDate.Year * 12 + x.CutoffDate.Month)
            .WithName("EffectMonth").WithMessage("El mes de efecto es posterior al mes del corte: la recalificación se refleja en un cierre siguiente.");
        RuleFor(x => x.Criteria).Must(EsObjeto).WithMessage("Los criterios de la norma aplicados van como un objeto JSON.");
        RuleFor(x => x.Loans).NotEmpty().WithMessage("La evaluación lleva al menos un crédito.")
            .Must(l => l.Select(x => x.LoanPublicId).Distinct().Count() == l.Count).WithMessage("Un crédito va una sola vez en la evaluación.");
        RuleForEach(x => x.Loans).ChildRules(l =>
        {
            l.RuleFor(x => x.LoanPublicId).NotEmpty();
            l.RuleFor(x => x.ProposedCategory).IsInEnum();
            l.RuleFor(x => x.Reason).NotEmpty().WithMessage("Justifique la calificación propuesta.").MaximumLength(1000);
            l.RuleFor(x => x.ProposedSubcategory).Must((x, s) => s is null or RatingSubcategory.None || x.ProposedCategory == RatingCategory.E)
                .WithMessage("Sólo la categoría E lleva subcategoría.");
        });
    }

    private static bool EsObjeto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class PreparePortfolioEvaluationCommandHandler(
    IApplicationDbContext db,
    NumeradorDeCartera numerador,
    IPerfilRegulatorio perfiles,
    INormativaVigente normativa,
    NormaPendienteDeCartera norma,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<PreparePortfolioEvaluationCommand, Result<PortfolioEvaluationDto>>
{
    public async Task<Result<PortfolioEvaluationDto>> Handle(PreparePortfolioEvaluationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var (anio, mes) = PreparePortfolioEvaluationCommand.Mes(request.EffectMonth)!.Value;
        var corte = request.CutoffDate;

        // La periodicidad del tipo de entidad, si el documento la trae. Decisión del dueño del 2026-10-18: es informativa; fuera de ella sólo se avisa.
        AvisoDeNormaPendienteDto? fueraDePeriodicidad = null;
        var perfil = await perfiles.VigenteAsync(corte, null, ct);
        if (perfil is not null && await normativa.TiposDeOrganizacionAsync(corte, null, ct) is { IsSuccess: true } tipos
            && tipos.Value.Documento.Buscar(perfil.OrganizationTypeCode)?.PortfolioEvaluation is { } periodicidad
            && !periodicidad.Admite(corte, anio, mes))
        {
            var efecto = new DateOnly(corte.Year, corte.Month, 1).AddMonths(periodicidad.EffectMonthsAfterCutoff);
            fueraDePeriodicidad = EvaluacionesDeCartera.FueraDePeriodicidad(perfil.OrganizationTypeCode, periodicidad.CutoffMonths,
                periodicidad.EffectMonthsAfterCutoff, efecto);
        }

        var abiertas = new[] { PortfolioEvaluationStatus.Draft, PortfolioEvaluationStatus.Recommended, PortfolioEvaluationStatus.Approved };
        if (await db.PortfolioEvaluations.AsNoTracking().Where(e => !e.IsDeleted && e.CutoffDate == corte && abiertas.Contains(e.Status))
                .Select(e => e.Number).FirstOrDefaultAsync(ct) is { } otra)
            return Falla(EvaluacionesDeCartera.Superpuesta(corte, otra));

        var ids = request.Loans.Select(l => l.LoanPublicId).ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.PublicId) && !l.IsDeleted).ToListAsync(ct);
        if (creditos.Count != ids.Count) return Falla(Settlement.LecturaDelCredito.Inexistente());
        var vivos = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), corte).Where(l => ids.Contains(l.PublicId)).Select(l => l.Id).ToListAsync(ct);
        foreach (var c in creditos.OrderBy(c => c.Number, StringComparer.Ordinal))
        {
            var dentro = await alcance.AsegurarAsync(c.BranchId, c.CreditLineId, Settlement.LecturaDelCredito.Inexistente(), auditoria, "Loan", c.PublicId, ct);
            if (dentro.IsFailure) return Falla(dentro.Error);
            if (!vivos.Contains(c.Id)) return Falla(EvaluacionesDeCartera.CreditoNoVivo(c.Number, corte));
        }
        var porReglas = await new CategoriaContableDeCartera(db).AlCorteAsync(creditos.Select(c => c.Id).ToList(), corte, ct);

        var numero = await numerador.AsignarAsync(LendingNumberKind.PortfolioEvaluation, string.Empty, EvaluacionesDeCartera.Prefijo, ct);
        var evaluacion = new PortfolioEvaluation
        {
            Number = numero, CutoffDate = corte, EffectiveYear = (short)anio, EffectiveMonth = (byte)mes,
            CriteriaJson = string.IsNullOrWhiteSpace(request.Criteria) ? "{}" : request.Criteria.Trim(), PreparedByUserId = usuario, PreparedAt = reloj.UtcNow,
        };
        db.PortfolioEvaluations.Add(evaluacion);
        await db.SaveChangesAsync(ct);
        var porPublicId = creditos.ToDictionary(c => c.PublicId);
        var lineas = request.Loans.Select(l =>
        {
            var credito = porPublicId[l.LoanPublicId];
            var regla = Enum.TryParse<RatingCategory>(porReglas.GetValueOrDefault(credito.Id)?.Category, out var r) ? r : RatingCategory.A;
            return PortfolioEvaluationLoan.Nueva(evaluacion.Id, credito.Id, regla, l.ProposedCategory, l.ProposedSubcategory, l.Reason, l.CriteriaScoresJson,
                l.SupportAttachmentPublicId);
        }).ToList();
        db.PortfolioEvaluationLoans.AddRange(lineas);
        evaluacion.Recalcular(lineas);
        await db.SaveChangesAsync(ct);
        var avisos = await norma.AvisosAsync(corte, AvisosDeNorma.Evaluacion, ct);
        if (fueraDePeriodicidad is not null) avisos = [.. avisos, fueraDePeriodicidad];
        return Result.Success((await EvaluacionesDeCartera.DtoAsync(db, evaluacion, lineas, 1, lineas.Count, ct)) with { Warnings = avisos });
    }

    private static Result<PortfolioEvaluationDto> Falla(Error e) => Result.Failure<PortfolioEvaluationDto>(e);
}

/// <summary>
/// Aplica una evaluación aprobada (feature 013, K3, US14, T0912; api.md §12.6, <c>POST /api/lending/risk/evaluations/{id}/apply</c>,
/// <c>Lending.PortfolioEvaluations.Apply</c>, con clave; FR-102): los dos puntos tienen que ser los que la recomendaron (comité de riesgos) y la aprobaron
/// (consejo, cerrado y aprobatorio); si no, <c>Lending.Session.ItemMismatch</c>. Sin aprobar, <c>Lending.PortfolioEvaluation.InvalidStatus</c>.
/// Aplicada, es la candidata <c>Evaluation</c> de cada crédito evaluado desde el cierre de su mes de efecto hasta la siguiente evaluación aplicada (T0913);
/// nunca reescribe cierres anteriores. (nuevo)
/// </summary>
public sealed record ApplyPortfolioEvaluationCommand(Guid PortfolioEvaluationPublicId, Guid RecommendationSessionItemPublicId, Guid ApprovalSessionItemPublicId)
    : IRequest<Result<PortfolioEvaluationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyPortfolioEvaluationCommandValidator : AbstractValidator<ApplyPortfolioEvaluationCommand>
{
    public ApplyPortfolioEvaluationCommandValidator()
    {
        RuleFor(x => x.PortfolioEvaluationPublicId).NotEmpty();
        RuleFor(x => x.RecommendationSessionItemPublicId).NotEmpty().WithMessage("Diga el punto del acta del comité de riesgos que la recomendó.");
        RuleFor(x => x.ApprovalSessionItemPublicId).NotEmpty().WithMessage("Diga el punto del acta del consejo que la aprobó.");
    }
}

public sealed class ApplyPortfolioEvaluationCommandHandler(
    IApplicationDbContext db,
    PuntoDeActaCerrado puntoDeActa,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyPortfolioEvaluationCommand, Result<PortfolioEvaluationDto>>
{
    public async Task<Result<PortfolioEvaluationDto>> Handle(ApplyPortfolioEvaluationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leida = await EvaluacionesDeCartera.LeerAsync(db, request.PortfolioEvaluationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var e = leida.Value;
        if (e.PuedeIrA(PortfolioEvaluationStatus.Applied) is { } estado) return Falla(ErroresDeGobierno.De(estado));

        var aprobacion = await puntoDeActa.ValidarAsync(request.ApprovalSessionItemPublicId, LendingDecisionSubjects.PortfolioEvaluation, e.PublicId, ct);
        if (aprobacion.IsFailure) return Falla(aprobacion.Error);
        var recomendacion = await db.SessionItems.AsNoTracking().Where(i => i.PublicId == request.RecommendationSessionItemPublicId && !i.IsDeleted)
            .Select(i => new { i.Id, i.SubjectType, i.Outcome }).FirstOrDefaultAsync(ct);
        if (aprobacion.Value.ItemId != e.ApprovalSessionItemId || recomendacion is null || recomendacion.Id != e.RecommendationSessionItemId)
            return Falla(ErroresDeGobierno.PuntoNoCorresponde(LendingDecisionSubjects.PortfolioEvaluation, recomendacion?.SubjectType ?? LendingDecisionSubjects.PortfolioEvaluation,
                (recomendacion?.Outcome ?? aprobacion.Value.Outcome).ToString()));

        try
        {
            e.Aplicar(usuario, reloj.UtcNow);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await EvaluacionesDeCartera.DtoAsync(db, e, [], 1, 0, ct));
    }

    private static Result<PortfolioEvaluationDto> Falla(Error e) => Result.Failure<PortfolioEvaluationDto>(e);
}

/// <summary>
/// Descarta una evaluación de cartera que no se aplicó (feature 013, K3, bloque D; decisión del dueño del 2026-10-15; api.md §12.6,
/// <c>POST /api/lending/risk/evaluations/{id}/discard</c>, <c>Lending.PortfolioEvaluations.Prepare</c> —quien la prepara, mínimo privilegio—, con clave y
/// motivo): en borrador, recomendada o aprobada pasa a <c>Discarded</c>; aplicada, rechazada o ya descartada responde
/// <c>Lending.PortfolioEvaluation.InvalidStatus</c>. La tabla no guarda el motivo: va a la auditoría del comando (<see cref="IConMotivo"/>). Descartada,
/// se puede preparar otra del mismo corte. (nuevo)
/// </summary>
public sealed record DiscardPortfolioEvaluationCommand(Guid PortfolioEvaluationPublicId, string Reason)
    : IRequest<Result<PortfolioEvaluationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DiscardPortfolioEvaluationCommandValidator : ValidadorConMotivo<DiscardPortfolioEvaluationCommand>
{
    public DiscardPortfolioEvaluationCommandValidator() => RuleFor(x => x.PortfolioEvaluationPublicId).NotEmpty();
}

public sealed class DiscardPortfolioEvaluationCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DiscardPortfolioEvaluationCommand, Result<PortfolioEvaluationDto>>
{
    public async Task<Result<PortfolioEvaluationDto>> Handle(DiscardPortfolioEvaluationCommand request, CancellationToken ct)
    {
        var leida = await EvaluacionesDeCartera.LeerAsync(db, request.PortfolioEvaluationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<PortfolioEvaluationDto>(leida.Error);
        var e = leida.Value;
        try
        {
            e.Descartar();
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<PortfolioEvaluationDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await EvaluacionesDeCartera.DtoAsync(db, e, [], 1, 0, ct));
    }
}
