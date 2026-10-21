using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Scoring;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Lo que se cargó de una solicitud para evaluarla: la línea, las partes vivas, sus personas y la simulación con la versión vigente. (nuevo)</summary>
public sealed record SolicitudCargada(
    CreditApplication Solicitud,
    CreditLine Linea,
    IReadOnlyList<CreditApplicationParty> Partes,
    IReadOnlyDictionary<int, Person> Personas,
    SimulacionCalculada Simulacion)
{
    public CreditApplicationParty Solicitante => Partes.Single(p => p.Role == CreditPartyRole.Applicant);
}

/// <summary>Una solicitud evaluada: las validaciones registradas, el puntaje (si la línea lo tiene) y el enrutamiento. (nuevo)</summary>
public sealed record SolicitudEvaluada(
    ValidacionesEvaluadas Validaciones,
    IReadOnlyList<CreditApplicationCheck> Checks,
    CreditScore? Puntaje,
    SolicitudEnrutada Enrutamiento);

/// <summary>
/// La evaluación de una solicitud (feature 013, T0396; FR-036–FR-038): carga lo suyo, corre <see cref="ValidacionesDeOriginacion"/> en la etapa
/// pedida, calcula el puntaje con el modelo de la versión de la línea (<see cref="MotorDePuntaje"/> → <c>LND_CreditScores</c>), registra las
/// validaciones con su evidencia y la enruta (<see cref="EnrutamientoDeSolicitud"/>). Lo usan la radicación, el análisis, volver a correr las
/// validaciones y, en 6B, la decisión. Guarda dos veces dentro de la transacción de quien llama: la evidencia (listas, puntaje) primero, para
/// que las validaciones lleven sus Ids. (nuevo)
/// </summary>
public sealed class EvaluacionDeSolicitud(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    ValidacionesDeOriginacion validaciones,
    EnrutamientoDeSolicitud enrutamiento,
    IDateTimeService reloj)
{
    /// <summary>Carga la solicitud con su línea, sus partes y personas, y la simula con la versión vigente a su fecha de desembolso.</summary>
    public async Task<Result<SolicitudCargada>> CargarAsync(CreditApplication s, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var partes = await db.CreditApplicationParties.Where(p => p.ApplicationId == s.Id && !p.IsDeleted).OrderBy(p => p.Order).ToListAsync(ct);
        var ids = partes.Select(p => p.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var simulada = await SimularAsync(s, linea, personas[s.ApplicantPersonId], s.RequestedAmount, s.Installments ?? 1, ct);
        return simulada.IsFailure
            ? Result.Failure<SolicitudCargada>(simulada.Error)
            : Result.Success(new SolicitudCargada(s, linea, partes, personas, simulada.Value));
    }

    /// <summary>
    /// La simulación de la solicitud con otro monto y otro número de cuotas (bloque 6B): las condiciones menores con que se aprueba se revalidan
    /// contra la usura del día con el mismo plan, la misma línea y los mismos seguros (FR-038, T0372).
    /// </summary>
    public Task<Result<SimulacionCalculada>> SimularConAsync(SolicitudCargada c, decimal monto, int cuotas, CancellationToken ct) =>
        SimularAsync(c.Solicitud, c.Linea, c.Personas[c.Solicitud.ApplicantPersonId], monto, cuotas, ct);

    private async Task<Result<SimulacionCalculada>> SimularAsync(CreditApplication s, CreditLine linea, Person solicitante, decimal monto, int cuotas,
        CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var perfil = await PersonaDeOriginacion.PerfilAsync(db, solicitante, s.ExpectedDisbursementDate ?? hoy, ct);
        var gracia = s.GraceKind is { } g ? new ScheduleGraceInput(g, s.GraceInstallments ?? 0) : null;
        var extraordinarias = s.ExtraordinaryInstallments is { } x
            ? System.Text.Json.JsonSerializer.Deserialize<List<ScheduleExtraordinaryInput>>(x, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            : null;
        var seguros = s.SelectedInsurances is { } y ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(y) : null;
        var entrada = new ScheduleSimulationInput(linea.PublicId, monto, cuotas, s.Periodicity, s.ExpectedDisbursementDate,
            s.FirstDueDate, s.AmortizationSystem, extraordinarias, gracia, seguros, solicitante.PublicId);
        return await simulador.SimularAsync(linea, entrada, perfil, hoy, ct);
    }

    /// <summary>
    /// Evalúa y enruta. <paramref name="detenerSi"/> decide si una validación evaluada detiene todo antes de escribir (la radicación se niega con
    /// la concentración excedida o sin patrimonio técnico, api.md §4.2); el resto queda registrado como validación.
    /// </summary>
    public async Task<Result<SolicitudEvaluada>> EvaluarAsync(SolicitudCargada c, CheckStage etapa, IReadOnlySet<ApplicationCheckKind>? solo,
        Func<ValidacionesEvaluadas, Error?>? detenerSi, CancellationToken ct)
    {
        var s = c.Solicitud;
        var hoy = reloj.HoyLocal;
        var contexto = new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, etapa, hoy) { Solo = solo };
        var evaluadas = await validaciones.EvaluarAsync(contexto, ct);
        if (detenerSi?.Invoke(evaluadas) is { } alto) return Result.Failure<SolicitudEvaluada>(alto);

        CreditScore? puntaje = null;
        var version = c.Simulacion.Version.Version;
        if ((solo is null || solo.Contains(ApplicationCheckKind.Scoring)) && ModeloDePuntaje.Leer(version.ScoringModel) is { Variables.Count: > 0 } modelo)
        {
            var valores = await validaciones.ValoresDePuntajeAsync(contexto, evaluadas, ct);
            puntaje = CreditScore.Registrar(s.Id, s.ApplicantPersonId, version.Id, MotorDePuntaje.Calcular(modelo, version.MinimumScore, valores), reloj.UtcNow);
            db.CreditScores.Add(puntaje);
        }
        await db.SaveChangesAsync(ct);

        var filas = validaciones.Registrar(evaluadas, s.Id, etapa, reloj.UtcNow, puntaje);
        var puntajeBajo = puntaje?.BelowMinimum ?? (await db.CreditScores.AsNoTracking().Where(x => x.ApplicationId == s.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CalculatedAt).Select(x => (bool?)x.BelowMinimum).FirstOrDefaultAsync(ct) ?? false);
        var datos = new DatosDeEnrutamiento(evaluadas.MontoConsolidado, evaluadas.HayVinculado, puntajeBajo,
            evaluadas.Pendientes.Any(p => p.Resultado == CheckOutcome.Warning), evaluadas.Hallazgos);
        var enrutada = await enrutamiento.CalcularAsync(s, c.Linea, version, datos, hoy, ct);
        if (enrutada.IsFailure) return Result.Failure<SolicitudEvaluada>(enrutada.Error);
        return Result.Success(new SolicitudEvaluada(evaluadas, filas, puntaje, enrutada.Value));
    }
}
