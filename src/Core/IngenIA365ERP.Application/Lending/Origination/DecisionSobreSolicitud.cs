using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Lo que se aprueba después de revalidar (FR-038): el monto, el JSON de condiciones y si es una excepción a la política. (nuevo)</summary>
public sealed record AprobacionRevalidada(decimal Monto, string? Condiciones, bool EsExcepcion);

/// <summary>
/// La decisión sobre una solicitud de crédito (feature 013, US2 bloque 6B; T0398; FR-038, FR-042, T34, T63, T66; api.md §4.3). La individual
/// (<see cref="DecidirAsync"/>) y la colegiada (<c>AsuntoDeActaDeSolicitud</c>) revalidan igual (<see cref="RevalidarAprobacionAsync"/>):
/// <list type="number">
/// <item>la solicitud en el alcance (fuera, el 404); el nivel efectivo del enrutamiento <b>de hoy</b> y la atribución de quien decide
/// (<see cref="AtribucionDelDecisor"/>); pendiente de decisión o aprobada y vencida (C53: se vuelve a decidir, <c>Approved → PendingDecision</c>);
/// la segregación (<c>Decidir</c>; quien radicó no decide);</item>
/// <item>la huella que vio quien decide contra la de hoy: si cambió, se reenruta y responde <c>Lending.Decision.ContentChanged</c>;</item>
/// <item>en una transacción, con el cerrojo de los deudores (T34): para aprobar corre las validaciones en <c>BeforeDecision</c> (las registra con
/// su evidencia), revalida las condiciones contra la usura y la concentración, rechaza lo no exceptuable y lo bloqueante que nadie exceptúa, y
/// marca como excepción lo exceptuable (con motivo obligatorio); escribe <c>LND_CreditDecisions</c> con su huella, mueve la solicitud y deja la
/// intervención <c>Decidir</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class DecisionSobreSolicitud(
    IApplicationDbContext db,
    EvaluacionDeSolicitud evaluacion,
    EnrutamientoDeSolicitud enrutamiento,
    ValidacionDeConcentracion concentracion,
    RegistroDeIntervenciones intervenciones,
    AtribucionDelDecisor atribucion,
    ReglasDeParametrosDeCartera parametros,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IDecisionIndividual
{
    public string SubjectType => LendingDecisionSubjects.CreditApplication;

    public async Task<Result<CreditDecisionResultDto>> DecidirAsync(DecisionPedida pedido, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, pedido.SubjectPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var s = leida.Value;
        var hoy = reloj.HoyLocal;

        // El nivel que rige hoy y la atribución de quien decide (sin el permiso, el 404 de la solicitud).
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstAsync(v => v.Id == s.CreditLineVersionId, ct);
        var datos = await enrutamiento.DatosVigentesAsync(s, hoy, ct);
        var calculado = await enrutamiento.CalcularAsync(s, linea, version, datos, hoy, ct);
        if (calculado.IsFailure) return Falla(calculado.Error);
        var enrutada = calculado.Value;
        if (await atribucion.ExigirAsync(enrutada.Nivel, enrutada.Matriz.Niveles, ErroresDeOriginacion.SolicitudInexistente(), ct) is { } sinAtribucion)
            return Falla(sinAtribucion);

        var vencida = s.AprobacionVencida(hoy);
        if (s.Status != CreditApplicationStatus.PendingDecision && !vencida) return Falla(ErroresDeDecisionesYCupos.NoPendiente(s.Status.ToString()));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var personas = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted)
                .Select(p => p.PersonId).Distinct().OrderBy(p => p).ToListAsync(ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);

            // La huella que vio quien decide contra la de hoy: si cambió, se reenruta (también una aprobada que venció).
            var anterior = s.ApprovalDecisionId;
            if (!string.Equals(enrutada.Huella, pedido.ExpectedContentSha256, StringComparison.Ordinal))
                return await CambioAsync(s, enrutada, ct);
            if (vencida) enrutamiento.Aplicar(s, enrutada);

            AprobacionRevalidada? aprobacion = null;
            if (pedido.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)
            {
                var cargada = await evaluacion.CargarAsync(s, ct);
                if (cargada.IsFailure) return Falla(cargada.Error);
                var evaluada = await evaluacion.EvaluarAsync(cargada.Value, CheckStage.BeforeDecision, null, null, ct);
                if (evaluada.IsFailure) return Falla(evaluada.Error);
                if (!string.Equals(evaluada.Value.Enrutamiento.Huella, pedido.ExpectedContentSha256, StringComparison.Ordinal))
                    return await CambioAsync(s, evaluada.Value.Enrutamiento, ct);

                var revalidada = await RevalidarAprobacionAsync(cargada.Value, evaluada.Value.Validaciones,
                    pedido.Outcome == CreditDecisionOutcome.ApproveWithConditions ? pedido.Conditions : null, ct);
                if (revalidada.IsFailure) return Falla(revalidada.Error);
                aprobacion = revalidada.Value;
            }

            var esExcepcion = aprobacion?.EsExcepcion ?? false;
            DateOnly? hasta = null;
            if (aprobacion is not null)
            {
                var vigencia = await VigenciaAsync(s.CreditLineId, hoy, ct);
                if (vigencia.IsFailure) return Falla(vigencia.Error);
                hasta = vigencia.Value;
            }
            var lo = new LoDecidido(LendingDecisionSubjects.CreditApplication, s.PublicId, s.PublicId, pedido.Outcome, enrutada.Datos.MontoConsolidado,
                enrutada.Huella)
            {
                AttributionMatrixId = enrutada.Matriz.Matriz.Id,
                LevelId = enrutada.Nivel.Id,
                Conditions = aprobacion?.Condiciones,
                Reason = pedido.Reason,
                IsPolicyException = esExcepcion,
                ValidUntil = hasta,
                SupersedesDecisionId = vencida ? anterior : null,
            };
            if (CreditDecision.Validar(lo) is { } sinMotivo) return Falla(ErroresDeGobierno.De(sinMotivo));

            var decision = CreditDecision.Individual(lo, verificada.Value.UsuarioId, verificada.Value.PersonaId, reloj.UtcNow);
            db.CreditDecisions.Add(decision);
            await db.SaveChangesAsync(ct);

            switch (pedido.Outcome)
            {
                case CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions:
                    s.Aprobar(decision.Id, aprobacion!.Monto, aprobacion.Condiciones, hasta!.Value);
                    break;
                case CreditDecisionOutcome.Deny:
                    s.Negar();
                    break;
                default:
                    s.Devolver();
                    break;
            }
            intervenciones.Registrar(verificada.Value, SubmitCreditApplicationCommandHandler.Expediente, s.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreditDecisionResultDto(decision.PublicId, decision.Outcome, s.Status.ToString(), s.ApprovalValidUntil,
                s.ApprovedAmount));
        }, ct);
    }

    /// <summary>
    /// Revalida una aprobación (individual o de acta; FR-038, T63): las condiciones menores (monto y cuotas que no pasan de lo pedido) contra la
    /// usura del día y la concentración con el monto aprobado; lo no exceptuable (<c>Lending.Attribution.NotExceptionable</c>); la consulta a
    /// centrales simulada en producción en una línea que la exige (<c>Lending.Adapter.SimulatedInProduction</c>); lo bloqueante que nadie
    /// exceptúa (bloqueos, capacidad de descuento, un canal caído), con su código. Lo bloqueante exceptuable y la excepción pedida hacen de la
    /// aprobación una excepción a la política, que exige motivo. No escribe.
    /// </summary>
    public async Task<Result<AprobacionRevalidada>> RevalidarAprobacionAsync(SolicitudCargada c, ValidacionesEvaluadas v, DecisionConditionsInput? condiciones,
        CancellationToken ct)
    {
        var s = c.Solicitud;
        var monto = condiciones?.Amount ?? s.RequestedAmount;
        var cuotas = condiciones?.Installments ?? s.Installments ?? 1;
        if (monto <= 0m || monto > s.RequestedAmount)
            return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.CondicionesInvalidas("conditions.amount",
                "Las condiciones aprueban un monto mayor que cero que no pasa de lo pedido."));
        if (s.Installments is { } pedidas && cuotas > pedidas)
            return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.CondicionesInvalidas("conditions.installments",
                "Las condiciones aprueban un plazo que no pasa del pedido."));

        var hallazgos = v.Hallazgos.Where(h => h != MotivoNoExceptuable.Usury).ToList();
        if (hallazgos.Count > 0) return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.NoExceptuable(hallazgos));

        // La usura del día con lo que se aprueba (con otras condiciones, otra simulación).
        var simulacion = c.Simulacion;
        if (monto != s.RequestedAmount || cuotas != (s.Installments ?? 1))
        {
            var otra = await evaluacion.SimularConAsync(c, monto, cuotas, ct);
            if (otra.IsFailure) return Result.Failure<AprobacionRevalidada>(otra.Error);
            simulacion = otra.Value;
        }
        if (!simulacion.Usura.Pasa)
            return Result.Failure<AprobacionRevalidada>(ErroresDeLineasDeCredito.UsuraExcedida("conditions.amount", simulacion.Fecha, simulacion.Usura,
                simulacion.Combinacion.ModalityCode));

        // La concentración con el monto aprobado (FR-041): excedida o sin patrimonio técnico, no se aprueba.
        var medida = await concentracion.ValidarAsync(s.ApplicantPersonId, monto, conGarantiaAdmisible: false, reloj.HoyLocal, s.Id, ct);
        if (medida.IsFailure) return Result.Failure<AprobacionRevalidada>(medida.Error);

        var excepcion = s.PolicyExceptionRequested;
        foreach (var p in v.Pendientes.Where(p => p.Bloquea && p.Clase is not (ApplicationCheckKind.Concentration or ApplicationCheckKind.Usury)))
        {
            if (p.Clase == ApplicationCheckKind.CreditBureau && p.Codigo == ErroresDeOriginacion.CodigoSimuladoEnProduccion)
                return Result.Failure<AprobacionRevalidada>(VeredictoDeEfectoLegal.SinEfecto(
                    "La consulta a centrales es del canal simulado: en producción no sirve de evidencia para aprobar.",
                    "Configure la central de riesgo en Administración › Canales externos.").ComoError()!);
            if (!p.Exceptuable) return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.ValidacionBloqueante(p.Clase, p.Codigo));
            excepcion = true;
        }

        return Result.Success(new AprobacionRevalidada(monto, condiciones?.ComoJson(), excepcion));
    }

    /// <summary>La vigencia de una aprobación desde <paramref name="fecha"/> (<c>Originacion.DiasVigenciaDeAprobacion</c> de la línea, FR-042).</summary>
    public async Task<Result<DateOnly>> VigenciaAsync(int lineaId, DateOnly fecha, CancellationToken ct)
    {
        var dias = await parametros.LeerAsync(ParametrosDeCartera.OriginacionDiasVigenciaDeAprobacion, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        if (dias.IsFailure) return Result.Failure<DateOnly>(dias.Error);
        return dias.Value.Valor is int d ? Result.Success(fecha.AddDays(d)) : Result.Success(fecha);
    }

    /// <summary>Reenruta con lo de hoy, lo guarda y responde que el contenido cambió (api.md §4.3).</summary>
    private async Task<Result<CreditDecisionResultDto>> CambioAsync(CreditApplication s, SolicitudEnrutada enrutada, CancellationToken ct)
    {
        if (s.Status is CreditApplicationStatus.PendingDecision or CreditApplicationStatus.Approved
            && !string.Equals(s.RoutingSha256, enrutada.Huella, StringComparison.Ordinal))
        {
            enrutamiento.Aplicar(s, enrutada);
            await db.SaveChangesAsync(ct);
        }
        return Falla(ErroresDeDecisionesYCupos.ContenidoCambiado(enrutada.Huella));
    }

    private static Result<CreditDecisionResultDto> Falla(Error e) => Result.Failure<CreditDecisionResultDto>(e);
}
