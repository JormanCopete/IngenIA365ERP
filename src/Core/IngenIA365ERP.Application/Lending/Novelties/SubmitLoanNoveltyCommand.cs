using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Radica una novedad para decidirla (feature 013, K3, T0878, T0879; api.md §11.1, <c>POST /api/lending/novelties/{id}/submit</c>,
/// <c>Lending.Novelties.Request</c>, con clave; FR-091, T63): simula a hoy y no la deja pasar si el plan supera la usura (<c>Lending.Usury.Exceeded</c>)
/// o si lo que capitaliza excede la concentración (<c>Lending.Concentration.LimitExceeded</c>); sella la simulación y su huella
/// (<c>SimulationSha256</c>), la enruta por la matriz de atribuciones (sujeto <c>Novelty</c>; un vinculado, al consejo con acta, FR-040) y queda en
/// <c>Submitted</c> con su <c>routing</c>. Quien la radicó no la decide ni la aplica (segregación). Las decisiones van por
/// <see cref="Attributions.RecordCreditDecisionCommand"/> (individuales) o por el acta (colegiadas). (nuevo)
/// </summary>
public sealed record SubmitLoanNoveltyCommand(Guid NoveltyPublicId) : IRequest<Result<LoanNoveltyDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SubmitLoanNoveltyCommandValidator : AbstractValidator<SubmitLoanNoveltyCommand>
{
    public SubmitLoanNoveltyCommandValidator() => RuleFor(x => x.NoveltyPublicId).NotEmpty();
}

public sealed class SubmitLoanNoveltyCommandHandler(
    IApplicationDbContext db,
    SimulacionDeNovedad simulacion,
    EnrutamientoDeNovedad enrutamiento,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<SubmitLoanNoveltyCommand, Result<LoanNoveltyDto>>
{
    public async Task<Result<LoanNoveltyDto>> Handle(SubmitLoanNoveltyCommand request, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (n, credito) = leida.Value;
        if (n.ExigirEditable() is { } problema) return Falla(ErroresDeGobierno.De(problema));
        var hoy = reloj.HoyLocal;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);

            var simulada = await simulacion.SimularAsync(n, hoy, alertar: false, ct);
            if (simulada.IsFailure) return Falla(simulada.Error);
            var s = simulada.Value;
            if (s.UsuraExcedida)
                return Falla(ErroresDeLineasDeCredito.UsuraExcedida("newTerms.rate", hoy, s.Calculo.Usura, s.Calculo.Principal.Cargado.Vigentes.ModalityCode));
            if (s.Concentracion is { } concentracion) return Falla(concentracion);

            var datos = new DatosDeEnrutamientoDeNovedad(s.Calculo.Capitalizado, s.Calculo.Entrada.Cuotas,
                (s.Calculo.Entrada.Periodicidad ?? s.Calculo.Entrada.Base.Periodicidad).ToString(), s.Calculo.Principal.Estado.AlturaDeMora.Dias);
            try
            {
                // La huella de la simulación se sella primero: el enrutamiento la cubre.
                n.RegistrarSimulacion(new SimulacionGuardada(s.Dto, datos, null).Serializar(), s.Dto.SimulationSha256);
                var enrutada = await enrutamiento.CalcularAsync(n, datos, hoy, ct);
                if (enrutada.IsFailure) return Falla(enrutada.Error);
                var e = enrutada.Value;
                var organos = await EnrutamientoDeNovedad.OrganosAsync(db, ct);
                n.RegistrarSimulacion(new SimulacionGuardada(s.Dto, datos, e.Dto(organos), e.Matriz.Matriz.Id, e.Nivel.Id).Serializar(), s.Dto.SimulationSha256);
                n.Radicar();
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            await db.SaveChangesAsync(ct);
            return Result.Success(await NovedadesDeCartera.DtoAsync(db, n, ct));
        }, ct);
    }

    private static Result<LoanNoveltyDto> Falla(Error e) => Result.Failure<LoanNoveltyDto>(e);
}
