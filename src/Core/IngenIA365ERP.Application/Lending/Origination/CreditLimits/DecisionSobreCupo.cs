using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

/// <summary>
/// La decisión sobre un cupo que espera decisión (feature 013, US2 bloque 6B; T0398, T0405; FR-049, T63; api.md §4.3
/// <c>POST /credit-limits/{id}/decisions</c>): el mismo ciclo que la de una solicitud —atribución del nivel efectivo de hoy, pendiente, segregación
/// (quien pidió el cupo no decide), huella vista, cerrojo del deudor— y, para aprobar, las revalidaciones del cupo: monto que no pasa de lo pedido,
/// concentración con el monto aprobado, ninguna coincidencia en listas sin liberar ni bloqueo vigente. Aprobar <b>otorga</b> el cupo: una versión
/// nueva y firme con la decisión (<see cref="Otorgar"/>), que también usa el acta. Negar o devolver lo deja terminado. (nuevo)
/// </summary>
public sealed class DecisionSobreCupo(
    IApplicationDbContext db,
    EnrutamientoDeCupo enrutamiento,
    ValidacionDeConcentracion concentracion,
    BloqueosDelDeudor bloqueos,
    RegistroDeIntervenciones intervenciones,
    AtribucionDelDecisor atribucion,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IDecisionIndividual
{
    public string SubjectType => LendingDecisionSubjects.CreditLimit;

    public async Task<Result<CreditDecisionResultDto>> DecidirAsync(DecisionPedida pedido, CancellationToken ct)
    {
        var leido = await CupoEnAlcance.LeerAsync(db, alcance, auditoria, pedido.SubjectPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var cupo = leido.Value;
        var hoy = reloj.HoyLocal;

        var enrutado = await enrutamiento.CalcularAsync(cupo.PersonId, cupo.Kind, cupo.CreditLineId, cupo.Amount, hoy, ct);
        if (enrutado.IsFailure) return Falla(enrutado.Error);
        var e = enrutado.Value;
        if (await atribucion.ExigirAsync(e.Nivel, e.Matriz.Niveles, ErroresDeDecisionesYCupos.CupoInexistente(), ct) is { } sinAtribucion)
            return Falla(sinAtribucion);

        var estado = await EstadoAsync(cupo, hoy, ct);
        if (estado != EstadosDeCupo.PendienteDeDecision) return Falla(ErroresDeDecisionesYCupos.NoPendiente(estado));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(ExpedienteDeCupo.Tipo, cupo.PublicId), AccionesDeSegregacion.Decidir,
            hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        if (!string.Equals(e.Huella, pedido.ExpectedContentSha256, StringComparison.Ordinal))
            return Falla(ErroresDeDecisionesYCupos.ContenidoCambiado(e.Huella));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cupo.PersonId], ModoDeEspera.Esperar, ct);

            AprobacionRevalidada? aprobacion = null;
            if (pedido.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)
            {
                var revalidada = await RevalidarAprobacionAsync(cupo,
                    pedido.Outcome == CreditDecisionOutcome.ApproveWithConditions ? pedido.Conditions : null, hoy, ct);
                if (revalidada.IsFailure) return Falla(revalidada.Error);
                aprobacion = revalidada.Value;
            }

            var lo = new LoDecidido(LendingDecisionSubjects.CreditLimit, cupo.PublicId, cupo.PublicId, pedido.Outcome, e.Consolidado, e.Huella)
            {
                AttributionMatrixId = e.Matriz.Matriz.Id,
                LevelId = e.Nivel.Id,
                Conditions = aprobacion?.Condiciones,
                Reason = pedido.Reason,
            };
            if (CreditDecision.Validar(lo) is { } sinMotivo) return Falla(ErroresDeGobierno.De(sinMotivo));
            var decision = CreditDecision.Individual(lo, verificada.Value.UsuarioId, verificada.Value.PersonaId, reloj.UtcNow);
            db.CreditDecisions.Add(decision);
            await db.SaveChangesAsync(ct);

            CreditLimit? otorgado = null;
            if (aprobacion is not null) otorgado = await OtorgarAsync(cupo, decision.Id, aprobacion, ct);
            intervenciones.Registrar(verificada.Value, ExpedienteDeCupo.Tipo, cupo.PublicId);
            await db.SaveChangesAsync(ct);
            var final = otorgado ?? cupo;
            return Result.Success(new CreditDecisionResultDto(decision.PublicId, decision.Outcome,
                otorgado is null ? EstadosDeCupo.Terminado : await EstadoAsync(otorgado, hoy, ct), null, otorgado?.Amount, final.PublicId));
        }, ct);
    }

    /// <summary>
    /// Revalida la aprobación de un cupo (individual o de acta): el monto aprobado no pasa del pedido; la concentración con ese monto; ninguna
    /// coincidencia en listas sin liberar (no exceptuable) ni bloqueo vigente. No escribe.
    /// </summary>
    public async Task<Result<AprobacionRevalidada>> RevalidarAprobacionAsync(CreditLimit cupo, DecisionConditionsInput? condiciones, DateOnly fecha,
        CancellationToken ct)
    {
        var monto = condiciones?.Amount ?? cupo.Amount;
        if (monto <= 0m || monto > cupo.Amount)
            return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.CondicionesInvalidas("conditions.amount",
                "Las condiciones aprueban un cupo mayor que cero que no pasa de lo pedido."));

        var listas = await db.ScreeningResults.AsNoTracking().Where(r => r.PersonId == cupo.PersonId && r.ReleasedAt == null && !r.IsDeleted
            && (r.Outcome == ScreeningOutcome.Match || r.Outcome == ScreeningOutcome.PossibleMatch)).AnyAsync(ct);
        if (listas) return Result.Failure<AprobacionRevalidada>(ErroresDeDecisionesYCupos.NoExceptuable([MotivoNoExceptuable.RestrictiveListMatch]));
        var vigentes = await bloqueos.VigentesAsync([cupo.PersonId], ct);
        if (vigentes.Count > 0) return Result.Failure<AprobacionRevalidada>(AssignCreditLimitCommandHandler.DeudorBloqueado(vigentes));

        var conc = await concentracion.ValidarAsync(cupo.PersonId, monto, conGarantiaAdmisible: false, fecha, null, ct);
        if (conc.IsFailure) return Result.Failure<AprobacionRevalidada>(conc.Error);
        return Result.Success(new AprobacionRevalidada(monto, condiciones?.ComoJson(), false));
    }

    /// <summary>
    /// Otorga el cupo aprobado: una versión firme con la decisión, el monto aprobado y lo de la pendiente (aportes usados, preferencia), desde el
    /// día de la aprobación si la pendiente empezaba antes; con la vigencia final pedida, también la versión en cero del día siguiente. No guarda.
    /// </summary>
    public async Task<CreditLimit> OtorgarAsync(CreditLimit pendiente, int decisionId, AprobacionRevalidada aprobacion, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var todas = (await VersionesDeCupo.DeAsync(db, [pendiente.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, pendiente)).ToList();
        var numero = todas.Max(v => v.VersionNumber) + 1;
        var detalle = DetalleDeCupo.De(pendiente);
        var desde = pendiente.ValidFrom < hoy ? hoy : pendiente.ValidFrom;
        var otorgado = CreditLimit.NuevaVersion(pendiente.PersonId, pendiente.Kind, pendiente.CreditLineId, aprobacion.Monto, pendiente.Origin, desde, numero,
            reloj.UtcNow, pendiente.Reason, pendiente.AssignedByUserId, pendiente.ApplicationId, decisionId,
            (detalle with { RequiresDecision = false, Routing = null, ApprovesPublicId = pendiente.PublicId, Conditions = aprobacion.Condiciones }).Serializar(),
            pendiente.ContributionBalanceLoadId);
        db.CreditLimits.Add(otorgado);
        if (detalle.ValidTo is { } hasta && hasta >= desde)
            db.CreditLimits.Add(CreditLimit.NuevaVersion(pendiente.PersonId, pendiente.Kind, pendiente.CreditLineId, 0m, pendiente.Origin, hasta.AddDays(1),
                numero + 1, reloj.UtcNow, $"Fin de la vigencia del cupo otorgado: {pendiente.Reason}", pendiente.AssignedByUserId, decisionId: decisionId));
        return otorgado;
    }

    /// <summary>El estado calculado de una versión de cupo a la fecha.</summary>
    public async Task<string> EstadoAsync(CreditLimit cupo, DateOnly fecha, CancellationToken ct, CreditDecision? salvo = null)
    {
        var todas = (await VersionesDeCupo.DeAsync(db, [cupo.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, cupo)).ToList();
        var decididas = await VersionesDeCupo.DecididasAsync(db, todas.Select(v => v.PublicId).ToList(), ct, salvo);
        var pendiente = VersionesDeCupo.Pendiente(todas, decididas);
        if (!VersionesDeCupo.EsFirme(cupo) && !decididas.Contains(cupo.PublicId))
            return pendiente?.PublicId == cupo.PublicId ? EstadosDeCupo.PendienteDeDecision : EstadosDeCupo.Terminado;
        return VersionesDeCupo.Estado(cupo, todas, decididas, await bloqueos.VigentesAsync([cupo.PersonId], ct), fecha);
    }

    private static Result<CreditDecisionResultDto> Falla(Error e) => Result.Failure<CreditDecisionResultDto>(e);
}
