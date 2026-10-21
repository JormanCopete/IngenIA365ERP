using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Waivers;

/// <summary>
/// La decisión individual sobre una condonación radicada (feature 013, K3, T0884; api.md §4.3 y §11.2, <c>POST /api/lending/waivers/{id}/decisions</c>,
/// <c>Lending.Loans.Read</c> más el permiso del nivel; FR-094, T63, T66): en el alcance (fuera, el 404); el nivel efectivo por concepto, monto, días de
/// mora y vinculado —por encima de la atribución del gerente va al nivel siguiente; un nivel colegiado va a sesión con acta—; pendiente; quien la pidió no
/// decide; la huella vista. Toda decisión lleva motivo. Escribe <c>LND_CreditDecisions</c> y mueve la condonación. (nuevo)
/// </summary>
public sealed class DecisionSobreCondonacion(
    IApplicationDbContext db,
    CondonacionesDeCartera condonaciones,
    RegistroDeIntervenciones intervenciones,
    AtribucionDelDecisor atribucion,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IDecisionIndividual
{
    public string SubjectType => LendingDecisionSubjects.Condonation;

    /// <summary>El motivo que exige toda decisión sobre una condonación (FR-094: «siempre con motivo»).</summary>
    public const string CodigoMotivoRequerido = "Validation.Invalid";

    public async Task<Result<CreditDecisionResultDto>> DecidirAsync(DecisionPedida pedido, CancellationToken ct)
    {
        var leida = await CondonacionesDeCartera.LeerAsync(db, alcance, auditoria, pedido.SubjectPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (w, credito) = leida.Value;
        var hoy = reloj.HoyLocal;

        var enrutada = await condonaciones.EnrutarAsync(w, credito, hoy, ct);
        if (enrutada.IsFailure) return Falla(enrutada.Error);
        var e = enrutada.Value;
        if (await atribucion.ExigirAsync(e.Nivel, e.Matriz.Niveles, CondonacionesDeCartera.Inexistente(), ct) is { } sinAtribucion) return Falla(sinAtribucion);
        if (w.Status != WaiverStatus.Submitted) return Falla(ErroresDeDecisionesYCupos.NoPendiente(w.Status.ToString()));
        if (string.IsNullOrWhiteSpace(pedido.Reason))
            return Falla(new ErrorConDatos(CodigoMotivoRequerido, "Toda decisión sobre una condonación lleva su motivo.",
                new { errors = new[] { new { field = "reason", code = "NotEmpty", message = "Toda decisión sobre una condonación lleva su motivo." } } }));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, w.PublicId), AccionesDeSegregacion.Decidir,
            hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        if (!string.Equals(e.Huella, pedido.ExpectedContentSha256, StringComparison.Ordinal)) return Falla(ErroresDeDecisionesYCupos.ContenidoCambiado(e.Huella));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            var lo = new LoDecidido(LendingDecisionSubjects.Condonation, w.PublicId, w.PublicId, pedido.Outcome, e.Monto, e.Huella)
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
            Mover(w, decision.Id, pedido.Outcome);
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.Condonation, w.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreditDecisionResultDto(decision.PublicId, decision.Outcome, w.Status.ToString(), null, w.TotalAmount, w.PublicId));
        }, ct);
    }

    /// <summary>Mueve la condonación con la decisión: aprobada, rechazada o devuelta a borrador.</summary>
    public static void Mover(Waiver w, int decisionId, CreditDecisionOutcome resultado)
    {
        switch (resultado)
        {
            case CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions:
                w.Aprobar(decisionId);
                break;
            case CreditDecisionOutcome.Deny:
                w.Rechazar(decisionId);
                break;
            default:
                w.Devolver();
                break;
        }
    }

    private static Result<CreditDecisionResultDto> Falla(Error e) => Result.Failure<CreditDecisionResultDto>(e);
}

/// <summary>
/// El asunto de acta <c>Condonation</c> (feature 013, K3, T0884; FR-094, T64): la condonación que el nivel colegiado decide con acta. Describe el punto
/// (crédito, conceptos y valores, días de mora, vinculado, nivel y fundamento) y la huella de su enrutamiento de hoy; no votan quien la pidió
/// (<c>Filer</c>) ni el deudor y sus codeudores (<c>Applicant</c>). Con la decisión guardada, mueve la condonación. (nuevo)
/// </summary>
public sealed class AsuntoDeCondonacion(IApplicationDbContext db, CondonacionesDeCartera condonaciones) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.Condonation;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var w = await db.Waivers.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.PublicId == subjectPublicId && !x.IsDeleted, ct);
        if (w is null) return Result.Failure<AsuntoDeActa>(CondonacionesDeCartera.Inexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == w.LoanId, ct);
        var deudor = (await ReferenciasDeGobierno.PersonasAsync(db, [credito.DebtorPersonId], ct))[credito.DebtorPersonId];
        var personas = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == w.RequestedByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } radicador)
            personas.Add(new PersonaDelAsunto(radicador, MotivosDeExclusion.Radicador));
        var partes = await db.LoanParties.AsNoTracking().Where(p => p.LoanId == credito.Id && !p.IsDeleted && p.ReleasedOn == null && p.ExtinguishedOn == null)
            .Select(p => p.PersonId).ToListAsync(ct);
        foreach (var persona in partes.Prepend(credito.DebtorPersonId).Distinct())
            if (personas.All(p => p.PersonId != persona)) personas.Add(new PersonaDelAsunto(persona, MotivosDeExclusion.Solicitante));

        var titulo = $"Condonación {w.Number} del crédito {credito.Number} de {deudor.FullName} por {ErroresDeOriginacion.Pesos(w.TotalAmount)}";
        var pendiente = w.Status == WaiverStatus.Submitted;
        if (!pendiente)
            return Result.Success(new AsuntoDeActa(titulo, null, false, w.Status.ToString(), new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, w.PublicId),
                string.Empty) { Personas = personas });
        var enrutada = await condonaciones.EnrutarAsync(w, credito, fecha, ct);
        if (enrutada.IsFailure) return Result.Failure<AsuntoDeActa>(enrutada.Error);
        var e = enrutada.Value;
        var resumen = string.Join(" ", new[]
        {
            $"Motivo: {w.Reason}.",
            "Conceptos: " + string.Join("; ", w.Lines.Select(l => $"{l.Concept} {ErroresDeOriginacion.Pesos(l.Amount)}"
                + (l.InstallmentNumber is { } q ? $" (cuota {q})" : string.Empty))) + ".",
            $"Días de mora al pedirla: {w.DaysPastDueAtRequest}.",
            $"Nivel: {e.Nivel.Name}.",
            e.Vinculado ? "El deudor o una parte es vinculada (FR-040)." : "Sin personas vinculadas.",
            "Fundamento: " + string.Join(" ", e.Enrutamiento.Razones.Select(r => r.Detalle)),
        });
        return Result.Success(new AsuntoDeActa(titulo, resumen, true, w.Status.ToString(), new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, w.PublicId),
            e.Huella)
        {
            Personas = personas,
            GoverningBodyId = e.Nivel.GoverningBodyId,
            MontoConsolidado = e.Monto,
            RoutingSha256 = e.Huella,
            AttributionMatrixId = e.Matriz.Matriz.Id,
            LevelId = e.Nivel.Id,
        });
    }

    public async Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var w = await db.Waivers.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == decision.Punto.SubjectPublicId && !x.IsDeleted, ct);
        if (w is null) return Result.Failure(CondonacionesDeCartera.Inexistente());
        return w.Status == WaiverStatus.Submitted ? Result.Success() : Result.Failure(ErroresDeGobierno.SujetoNoPendiente(w.Status.ToString()));
    }

    public async Task<Result> TrasGuardarAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var w = await db.Waivers.FirstAsync(x => x.PublicId == decision.Punto.SubjectPublicId, ct);
        try
        {
            DecisionSobreCondonacion.Mover(w, decision.Decision.Id, decision.Resultado);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        return Result.Success();
    }
}
