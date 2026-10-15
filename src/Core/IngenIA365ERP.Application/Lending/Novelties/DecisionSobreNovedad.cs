using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// La decisión individual sobre una novedad radicada (feature 013, K3, T0879; api.md §4.3 y §11.1, <c>POST /api/lending/novelties/{id}/decisions</c>,
/// <c>Lending.Novelties.Read</c> más el permiso del nivel; FR-038, T63, T66): el mismo ciclo que la de un cupo —la novedad en el alcance (fuera, el 404),
/// el nivel efectivo de hoy y la atribución de quien decide (<see cref="AtribucionDelDecisor"/>; un nivel colegiado va a sesión,
/// <c>Lending.Attribution.LevelRequired</c>), pendiente (<c>Submitted</c>), segregación (quien la radicó no decide), la huella vista— y, para aprobar,
/// la usura del plan nuevo a hoy (<c>Lending.Usury.Exceeded</c>; nunca se exceptúa) y la concentración con lo que capitaliza. Escribe
/// <c>LND_CreditDecisions</c> y mueve la novedad: aprobada, rechazada o devuelta a borrador. (nuevo)
/// </summary>
public sealed class DecisionSobreNovedad(
    IApplicationDbContext db,
    EnrutamientoDeNovedad enrutamiento,
    SimulacionDeNovedad simulacion,
    RegistroDeIntervenciones intervenciones,
    AtribucionDelDecisor atribucion,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IDecisionIndividual
{
    public string SubjectType => LendingDecisionSubjects.Novelty;

    public async Task<Result<CreditDecisionResultDto>> DecidirAsync(DecisionPedida pedido, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, pedido.SubjectPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (n, credito) = leida.Value;
        var hoy = reloj.HoyLocal;

        var enrutada = await RutaAsync(n, hoy, ct);
        if (enrutada.IsFailure) return Falla(enrutada.Error);
        var e = enrutada.Value;
        if (await atribucion.ExigirAsync(e.Nivel, e.Matriz.Niveles, ErroresDeNovedades.Inexistente(), ct) is { } sinAtribucion) return Falla(sinAtribucion);
        if (n.Status != LoanNoveltyStatus.Submitted) return Falla(ErroresDeDecisionesYCupos.NoPendiente(n.Status.ToString()));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(NovedadesDeCartera.Expediente, n.PublicId), AccionesDeSegregacion.Decidir,
            hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        if (!string.Equals(e.Huella, pedido.ExpectedContentSha256, StringComparison.Ordinal)) return Falla(ErroresDeDecisionesYCupos.ContenidoCambiado(e.Huella));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);

            if (pedido.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)
            {
                var revalidada = await RevalidarAsync(n, hoy, ct);
                if (revalidada is not null) return Falla(revalidada);
            }

            var lo = new LoDecidido(LendingDecisionSubjects.Novelty, n.PublicId, n.PublicId, pedido.Outcome, e.Monto, e.Huella)
            {
                AttributionMatrixId = e.Matriz.Matriz.Id,
                LevelId = e.Nivel.Id,
                Conditions = pedido.Outcome == CreditDecisionOutcome.ApproveWithConditions ? pedido.Conditions?.ComoJson() : null,
                Reason = pedido.Reason,
            };
            if (CreditDecision.Validar(lo) is { } sinMotivo) return Falla(ErroresDeGobierno.De(sinMotivo));
            var decision = CreditDecision.Individual(lo, verificada.Value.UsuarioId, verificada.Value.PersonaId, reloj.UtcNow);
            db.CreditDecisions.Add(decision);
            await db.SaveChangesAsync(ct);

            Mover(n, decision.Id, pedido.Outcome, lo.Conditions);
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.Novelty, n.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreditDecisionResultDto(decision.PublicId, decision.Outcome, n.Status.ToString(), null, null, n.PublicId));
        }, ct);
    }

    /// <summary>El enrutamiento de hoy con los datos sellados al radicar.</summary>
    public async Task<Result<NovedadEnrutada>> RutaAsync(LoanNovelty n, DateOnly fecha, CancellationToken ct)
    {
        var guardada = SimulacionGuardada.Leer(n.SimulationJson);
        if (guardada is null) return Result.Failure<NovedadEnrutada>(ErroresDeDecisionesYCupos.NoPendiente(n.Status.ToString()));
        return await enrutamiento.CalcularAsync(n, guardada.Routing, fecha, ct);
    }

    /// <summary>Para aprobar: la usura del plan nuevo a hoy (no se exceptúa) y la concentración con lo que capitaliza. Nulo si pasa.</summary>
    public async Task<Error?> RevalidarAsync(LoanNovelty n, DateOnly fecha, CancellationToken ct)
    {
        var simulada = await simulacion.SimularAsync(n, fecha, alertar: false, ct);
        if (simulada.IsFailure) return simulada.Error;
        var s = simulada.Value;
        if (s.UsuraExcedida)
            return ErroresDeLineasDeCredito.UsuraExcedida("newTerms.rate", fecha, s.Calculo.Usura, s.Calculo.Principal.Cargado.Vigentes.ModalityCode);
        return s.Concentracion;
    }

    /// <summary>Mueve la novedad con la decisión: aprobada (con sus condiciones), rechazada o devuelta a borrador.</summary>
    public static void Mover(LoanNovelty n, int decisionId, CreditDecisionOutcome resultado, string? condiciones)
    {
        switch (resultado)
        {
            case CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions:
                n.Aprobar(decisionId, condiciones);
                break;
            case CreditDecisionOutcome.Deny:
                n.Rechazar(decisionId);
                break;
            default:
                n.Devolver();
                break;
        }
    }

    private static Result<CreditDecisionResultDto> Falla(Error e) => Result.Failure<CreditDecisionResultDto>(e);
}

/// <summary>
/// El asunto de acta <c>Novelty</c> (feature 013, K3, T0879; FR-039, FR-040, T64): la novedad que espera decisión de un órgano (por monto o por un
/// vinculado, al consejo). Describe el punto (mecanismo, causa, créditos, capital y plan nuevo, nivel y fundamento), su expediente y la huella de su
/// enrutamiento de hoy; no votan quien la radicó (<c>Filer</c>) ni el deudor y sus codeudores (<c>Applicant</c>). Al cerrar revalida como la individual
/// (la usura de hoy) y, con la decisión guardada, mueve la novedad. (nuevo)
/// </summary>
public sealed class AsuntoDeNovedad(IApplicationDbContext db, DecisionSobreNovedad decisiones) : Sessions.IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.Novelty;

    public async Task<Result<Sessions.AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var n = await db.LoanNovelties.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == subjectPublicId && !x.IsDeleted, ct);
        if (n is null) return Result.Failure<Sessions.AsuntoDeActa>(ErroresDeNovedades.Inexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == n.LoanId, ct);
        var deudor = (await ReferenciasDeGobierno.PersonasAsync(db, [credito.DebtorPersonId], ct))[credito.DebtorPersonId];
        var personas = new List<Sessions.PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == n.RequestedByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } radicador)
            personas.Add(new Sessions.PersonaDelAsunto(radicador, MotivosDeExclusion.Radicador));
        foreach (var persona in await EnrutamientoDeNovedad.PersonasDeLaNovedadAsync(db, n, ct))
            if (personas.All(p => p.PersonId != persona)) personas.Add(new Sessions.PersonaDelAsunto(persona, MotivosDeExclusion.Solicitante));

        var guardada = SimulacionGuardada.Leer(n.SimulationJson);
        var pendiente = n.Status == LoanNoveltyStatus.Submitted;
        var titulo = $"Novedad {n.Number} ({Textos.Mecanismo(n.Mechanism)}, {Textos.Causa(n.Cause)}) del crédito {credito.Number} de {deudor.FullName}";
        if (!pendiente || guardada is null)
            return Result.Success(new Sessions.AsuntoDeActa(titulo, null, pendiente, n.Status.ToString(),
                new ExpedienteDeCartera(NovedadesDeCartera.Expediente, n.PublicId), string.Empty) { Personas = personas });

        var enrutada = await decisiones.RutaAsync(n, fecha, ct);
        if (enrutada.IsFailure) return Result.Failure<Sessions.AsuntoDeActa>(enrutada.Error);
        var e = enrutada.Value;
        var s = guardada.Simulation;
        var resumen = string.Join(" ", new[]
        {
            $"Motivo: {n.RequestReason}.",
            $"Capital {ErroresDeNovedades.Pesos(s.NewPrincipal)}" + (s.Capitalized > 0m ? $" (capitaliza {ErroresDeNovedades.Pesos(s.Capitalized)})" : string.Empty)
                + $" en {s.NewSchedule.Count} cuotas desde la {s.FirstInstallment}.",
            $"Calificación hoy {s.RatingNow}; queda en {s.RatingAfter}; reestructuraciones: {s.RestructuringCount}.",
            $"Nivel: {e.Nivel.Name}.",
            e.Vinculado ? "Hay una persona vinculada (FR-040)." : "Sin personas vinculadas.",
            "Fundamento: " + string.Join(" ", e.Enrutamiento.Razones.Select(r => r.Detalle)),
        });
        return Result.Success(new Sessions.AsuntoDeActa(titulo, resumen, pendiente, n.Status.ToString(),
            new ExpedienteDeCartera(NovedadesDeCartera.Expediente, n.PublicId), e.Huella)
        {
            Personas = personas,
            GoverningBodyId = e.Nivel.GoverningBodyId,
            MontoConsolidado = e.Monto,
            RoutingSha256 = e.Huella,
            AttributionMatrixId = e.Matriz.Matriz.Id,
            LevelId = e.Nivel.Id,
        });
    }

    public async Task<Result> AlDecidirAsync(Sessions.DecisionDeActa decision, CancellationToken ct)
    {
        var n = await db.LoanNovelties.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == decision.Punto.SubjectPublicId && !x.IsDeleted, ct);
        if (n is null) return Result.Failure(ErroresDeNovedades.Inexistente());
        if (n.Status != LoanNoveltyStatus.Submitted) return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(n.Status.ToString()));
        if (decision.Resultado is not (CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)) return Result.Success();
        return await decisiones.RevalidarAsync(n, decision.Fecha, ct) is { } error ? Result.Failure(error) : Result.Success();
    }

    public async Task<Result> TrasGuardarAsync(Sessions.DecisionDeActa decision, CancellationToken ct)
    {
        var n = await db.LoanNovelties.FirstAsync(x => x.PublicId == decision.Punto.SubjectPublicId, ct);
        try
        {
            DecisionSobreNovedad.Mover(n, decision.Decision.Id, decision.Resultado, decision.Condiciones);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        return Result.Success();
    }
}

/// <summary>Los textos en español de los mecanismos y las causas (lo que lee una persona en el acta y en los informes). (nuevo)</summary>
public static class Textos
{
    public static string Mecanismo(NoveltyMechanism m) => m switch
    {
        NoveltyMechanism.Extension => "prórroga",
        NoveltyMechanism.PaymentDateChange => "cambio de fecha de pago",
        NoveltyMechanism.RateChange => "cambio de tasa",
        NoveltyMechanism.Novation => "novación",
        NoveltyMechanism.Refinancing => "refinanciación",
        _ => m.ToString(),
    };

    public static string Causa(NoveltyCause c) => c == NoveltyCause.Restructuring ? "reestructuración" : "modificación";
}
