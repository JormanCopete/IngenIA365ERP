using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Aprueba la excepción de cobertura de una novedad aprobada (feature 013, K3, bloque D; decisión del dueño del 2026-10-15; api.md §11.1,
/// <c>POST /api/lending/novelties/{id}/coverage-exception</c>, <c>Lending.Novelties.Read</c> en la ruta y la atribución del nivel de excepción en el
/// handler, con clave y motivo; FR-092): «aplicar con la garantía extinguida sólo si la cobertura restante cumple la línea o se aprueba la excepción».
/// La excepción la aprueba <b>otra persona</b> antes de aplicar —cuatro ojos—: queda como decisión individual <c>Approve</c> con
/// <c>IsPolicyException</c> sobre la novedad, con el nivel de excepción de la matriz vigente y el motivo; <see cref="ApplyLoanNoveltyCommand"/> la usa
/// (<c>useCoverageException</c>) sólo si su autor no es quien aplica.
/// <list type="bullet">
/// <item>sólo con la novedad <c>Approved</c> (<c>Lending.Novelty.NotApproved</c>; aplicada, <c>.AlreadyApplied</c>) y una sola vez
/// (<c>Lending.Novelty.CoverageExceptionExists</c>);</item>
/// <item>la segregación como un decidir (<c>Decidir</c> sobre el expediente de la novedad: quien la radicó no la aprueba,
/// <c>Lending.Segregation.Conflict</c>);</item>
/// <item>la atribución del nivel de excepción (<c>IsExceptionLevel</c>; un nivel colegiado o uno que no alcanza responde
/// <c>Lending.Attribution.LevelRequired</c>); sin matriz o sin nivel de excepción, no hay excepción posible.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record ApproveNoveltyCoverageExceptionCommand(Guid NoveltyPublicId, string Reason)
    : IRequest<Result<LoanNoveltyDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveNoveltyCoverageExceptionCommandValidator : ValidadorConMotivo<ApproveNoveltyCoverageExceptionCommand>
{
    public ApproveNoveltyCoverageExceptionCommandValidator() => RuleFor(x => x.NoveltyPublicId).NotEmpty();
}

public sealed class ApproveNoveltyCoverageExceptionCommandHandler(
    IApplicationDbContext db,
    RegistroDeIntervenciones intervenciones,
    MatrizVigenteDeAtribuciones matrices,
    AtribucionDelDecisor atribucion,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ApproveNoveltyCoverageExceptionCommand, Result<LoanNoveltyDto>>
{
    /// <summary>El prefijo del motivo de la decisión de excepción (la consulta y la pantalla la reconocen por él y por <c>IsPolicyException</c>).</summary>
    public const string PrefijoDelMotivo = "Excepción de cobertura";

    public async Task<Result<LoanNoveltyDto>> Handle(ApproveNoveltyCoverageExceptionCommand request, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (n, principal) = leida.Value;
        var v = reloj.HoyLocal;

        var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([principal.DebtorPersonId], ModoDeEspera.Esperar, ct);
            var novedad = await db.LoanNovelties.AsNoTracking().FirstAsync(x => x.Id == n.Id, ct);
            if (novedad.PuedeIrA(LoanNoveltyStatus.Applied) is { } estado) return Result.Failure(ErroresDeGobierno.De(estado));
            if (await ExcepcionesDeCobertura.VigenteAsync(db, novedad.PublicId, ct) is not null)
                return Result.Failure(ErroresDeNovedades.ExcepcionYaAprobada(novedad.Number));

            var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(NovedadesDeCartera.Expediente, novedad.PublicId),
                AccionesDeSegregacion.Decidir, v, ct);
            if (verificada.IsFailure) return Result.Failure(verificada.Error);

            var matriz = await matrices.LeerAsync(v, reloj.UtcNow, ct);
            if (matriz is null) return Result.Failure(ErroresDeGobierno.SinMatrizVigente(v));
            var nivel = matriz.Niveles.Where(l => l.IsExceptionLevel).OrderByDescending(l => l.Rank).FirstOrDefault();
            if (nivel is null) return Result.Failure(ErroresDeNovedades.SinNivelDeExcepcion());
            if (await atribucion.ExigirAsync(nivel, matriz.Niveles, ErroresDeDecisionesYCupos.NivelRequerido(nivel.Code, nivel.Name, null), ct) is { } sin)
                return Result.Failure(sin);

            var motivo = request.Reason.Trim();
            var texto = $"{PrefijoDelMotivo}: {motivo}";
            var decision = CreditDecision.Individual(new LoDecidido(LendingDecisionSubjects.Novelty, novedad.PublicId, novedad.PublicId, CreditDecisionOutcome.Approve)
            {
                AttributionMatrixId = matriz.Matriz.Id,
                LevelId = nivel.Id,
                IsPolicyException = true,
                Reason = texto.Length <= 500 ? texto : texto[..500],
            }, verificada.Value.UsuarioId, verificada.Value.PersonaId, reloj.UtcNow);
            db.CreditDecisions.Add(decision);
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.Novelty, novedad.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
        if (r.IsFailure) return Falla(r.Error);

        db.DescartarCambios();
        var actualizada = await db.LoanNovelties.AsNoTracking().FirstAsync(x => x.Id == n.Id, ct);
        return Result.Success(await NovedadesDeCartera.DtoAsync(db, actualizada, ct));
    }

    private static Result<LoanNoveltyDto> Falla(Error e) => Result.Failure<LoanNoveltyDto>(e);
}

/// <summary>
/// La excepción de cobertura aprobada de una novedad (bloque D): la decisión individual <c>Approve</c> con <c>IsPolicyException</c> sobre la novedad
/// que escribe <see cref="ApproveNoveltyCoverageExceptionCommand"/>. (nuevo)
/// </summary>
public static class ExcepcionesDeCobertura
{
    public static Task<CreditDecision?> VigenteAsync(IApplicationDbContext db, Guid novedad, CancellationToken ct) =>
        db.CreditDecisions.AsNoTracking()
            .Where(d => d.SubjectType == LendingDecisionSubjects.Novelty && d.SubjectPublicId == novedad && d.IsPolicyException
                        && d.Outcome == CreditDecisionOutcome.Approve && d.DecidedByUserId != null)
            .OrderByDescending(d => d.DecidedAt).FirstOrDefaultAsync(ct);
}
