using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Lending.Rates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Simula una novedad en borrador (feature 013, K3, T0878; api.md §11.1, <c>POST /api/lending/novelties/{id}/simulate</c>, <c>Lending.Novelties.Request</c>,
/// sin clave; FR-091, FR-093): el plan nuevo desde hoy, la calificación hoy y después, el conteo de reestructuraciones, la usura vigente
/// (<c>Lending.Usury.Exceeded</c> si el plan la pasa), la concentración con lo que se capitaliza, la validación previa contable (sin alertar) y la huella
/// que se sella al radicar. No escribe. (nuevo)
/// </summary>
public sealed record SimulateLoanNoveltyQuery(Guid NoveltyPublicId) : IRequest<Result<NoveltySimulationDto>>;

public sealed class SimulateLoanNoveltyQueryHandler(
    IApplicationDbContext db,
    SimulacionDeNovedad simulacion,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<SimulateLoanNoveltyQuery, Result<NoveltySimulationDto>>
{
    public async Task<Result<NoveltySimulationDto>> Handle(SimulateLoanNoveltyQuery request, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<NoveltySimulationDto>(leida.Error);
        var (n, _) = leida.Value;
        if (n.ExigirEditable() is { } problema) return Result.Failure<NoveltySimulationDto>(ErroresDeGobierno.De(problema));
        var simulada = await simulacion.SimularAsync(n, reloj.HoyLocal, alertar: false, ct);
        return simulada.IsFailure ? Result.Failure<NoveltySimulationDto>(simulada.Error) : Result.Success(simulada.Value.Dto);
    }
}

/// <summary>Una novedad simulada: el cálculo y lo que se muestra. (nuevo)</summary>
public sealed record NovedadSimulada(NovedadCalculada Calculo, NoveltySimulationDto Dto, bool UsuraExcedida, Error? Concentracion);

/// <summary>
/// La simulación de una novedad (feature 013, K3, T0878): el cálculo (<see cref="CalculoDeNovedad"/>), la concentración con lo que se capitaliza
/// (FR-041; con patrimonio técnico vigente) y la validación previa contable de los hechos que dejaría. La usan la consulta de simulación, el radicado
/// y la decisión. (nuevo)
/// </summary>
public sealed class SimulacionDeNovedad(
    CalculoDeNovedad calculo,
    HechosDeLaNovedad hechos,
    ValidacionDeConcentracion concentracion)
{
    public async Task<Result<NovedadSimulada>> SimularAsync(LoanNovelty n, DateOnly fecha, bool alertar, CancellationToken ct)
    {
        var calculada = await calculo.CalcularAsync(n, fecha, ct);
        if (calculada.IsFailure) return Result.Failure<NovedadSimulada>(calculada.Error);
        var c = calculada.Value;

        Error? conc = null;
        string? textoDeConcentracion = null;
        if (c.Capitalizado > 0m)
        {
            var medida = await concentracion.ValidarAsync(c.Principal.Credito.DebtorPersonId, c.Capitalizado, conGarantiaAdmisible: false, fecha, null, ct);
            if (medida.IsFailure)
            {
                conc = medida.Error;
                textoDeConcentracion = medida.Error.Message;
            }
        }

        var prevalidacion = "NotApplicable";
        var avisos = new List<string>(c.Avisos);
        var armada = await hechos.ArmarAsync(c, null, null, null, null, hechos.Ahora, ct);
        if (armada.IsSuccess)
        {
            var decision = await hechos.ValidarAsync(Integration.EmisionDeCartera.HechosDeNovedad(armada.Value), $"la novedad {n.Number}", alertar, ct);
            prevalidacion = decision.Bloquea ? "NotPostable" : decision.Outcome.ToString();
            avisos.AddRange(decision.Avisos);
            if (decision.Bloqueo is { } bloqueo) avisos.Add(bloqueo.Message);
        }
        else
        {
            avisos.Add(armada.Error.Message);
        }
        if (c.Usura.Veredicto == VeredictoDeUsura.Excede)
            avisos.Add("El plan nuevo pasa la usura vigente: ajuste la tasa o las cuotas antes de radicar.");
        return Result.Success(new NovedadSimulada(c, c.Dto(prevalidacion, textoDeConcentracion) with { Warnings = avisos }, c.Usura.Veredicto == VeredictoDeUsura.Excede,
            conc));
    }
}
