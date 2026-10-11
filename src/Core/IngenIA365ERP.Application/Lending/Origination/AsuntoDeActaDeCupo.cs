using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// El asunto de acta <c>CreditLimit</c> (feature 013, US2 bloque 6B; T0399; FR-039, FR-040, FR-049, T64): el cupo que espera decisión de un
/// órgano (el comité por monto, el consejo por un vinculado). Describe el punto (persona, clase, línea, monto, nivel, vinculado y el fundamento
/// del enrutamiento), su expediente y la huella del enrutamiento de hoy; no votan quien lo pidió (<c>Filer</c>) ni la persona del cupo
/// (<c>Applicant</c>). Al cerrar revalida como la decisión individual (si falla, el cierre se revierte) y, con la decisión guardada, otorga el cupo.
/// (nuevo)
/// </summary>
public sealed class AsuntoDeActaDeCupo(IApplicationDbContext db, EnrutamientoDeCupo enrutamiento, DecisionSobreCupo decisiones) : IAsuntoDeActa
{
    private readonly Dictionary<Guid, AprobacionRevalidada> _aprobaciones = [];

    public string SubjectType => LendingDecisionSubjects.CreditLimit;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var cupo = await db.CreditLimits.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == subjectPublicId && !c.IsDeleted, ct);
        if (cupo is null) return Result.Failure<AsuntoDeActa>(ErroresDeDecisionesYCupos.CupoInexistente());

        var estado = await decisiones.EstadoAsync(cupo, fecha, ct);
        var persona = (await ReferenciasDeGobierno.PersonasAsync(db, [cupo.PersonId], ct))[cupo.PersonId];
        var personas = new List<PersonaDelAsunto> { new(cupo.PersonId, MotivosDeExclusion.Solicitante) };
        if (cupo.AssignedByUserId is { } quien
            && await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == quien).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } suPersona)
            personas.Insert(0, new PersonaDelAsunto(suPersona, MotivosDeExclusion.Radicador));
        var linea = cupo.CreditLineId is { } l
            ? await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == l).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        var titulo = string.Create(CultureInfo.InvariantCulture,
            $"Cupo {Clase(cupo.Kind)}{(linea is null ? string.Empty : " de la línea " + linea)} de {persona.FullName} por {ErroresDeOriginacion.Pesos(cupo.Amount)}");

        var enrutado = await enrutamiento.CalcularAsync(cupo.PersonId, cupo.Kind, cupo.CreditLineId, cupo.Amount, fecha, ct);
        if (enrutado.IsFailure) return Result.Failure<AsuntoDeActa>(enrutado.Error);
        var e = enrutado.Value;
        var resumen = string.Join(" ", new[]
        {
            $"Persona: {persona.FullName}.",
            $"Cupo {Clase(cupo.Kind)} por {ErroresDeOriginacion.Pesos(cupo.Amount)}; motivo: {cupo.Reason}.",
            $"Endeudamiento consolidado: {ErroresDeOriginacion.Pesos(e.Consolidado)}.",
            $"Nivel: {e.Nivel.Name}.",
            e.Vinculado ? "La persona es vinculada (FR-040)." : "Sin personas vinculadas.",
            "Fundamento: " + string.Join(" ", e.Enrutamiento.Razones.Select(r => r.Detalle)),
        });
        var pendiente = estado == EstadosDeCupo.PendienteDeDecision;
        return Result.Success(new AsuntoDeActa(titulo, resumen, pendiente, estado, new Segregation.ExpedienteDeCartera(ExpedienteDeCupo.Tipo, cupo.PublicId), e.Huella)
        {
            Personas = personas,
            GoverningBodyId = e.Nivel.GoverningBodyId,
            MontoConsolidado = e.Consolidado,
            RoutingSha256 = e.Huella,
            AttributionMatrixId = e.Matriz.Matriz.Id,
            LevelId = e.Nivel.Id,
        });
    }

    public async Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var cupo = await db.CreditLimits.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == decision.Punto.SubjectPublicId && !c.IsDeleted, ct);
        if (cupo is null) return Result.Failure(ErroresDeDecisionesYCupos.CupoInexistente());
        var estado = await decisiones.EstadoAsync(cupo, decision.Fecha, ct, decision.Decision);
        if (estado != EstadosDeCupo.PendienteDeDecision) return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(estado));
        if (decision.Resultado is not (CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)) return Result.Success();

        var condiciones = decision.Resultado == CreditDecisionOutcome.ApproveWithConditions ? DecisionConditionsInput.DeJson(decision.Condiciones) : null;
        var revalidada = await decisiones.RevalidarAprobacionAsync(cupo, condiciones, decision.Fecha, ct);
        if (revalidada.IsFailure) return Result.Failure(revalidada.Error);
        _aprobaciones[cupo.PublicId] = revalidada.Value;
        return Result.Success();
    }

    public async Task<Result> TrasGuardarAsync(DecisionDeActa decision, CancellationToken ct)
    {
        if (!_aprobaciones.TryGetValue(decision.Punto.SubjectPublicId, out var aprobacion)) return Result.Success();
        var cupo = await db.CreditLimits.AsNoTracking().FirstAsync(c => c.PublicId == decision.Punto.SubjectPublicId, ct);
        await decisiones.OtorgarAsync(cupo, decision.Decision.Id, aprobacion with { Condiciones = aprobacion.Condiciones ?? decision.Condiciones }, ct);
        return Result.Success();
    }

    private static string Clase(CreditLimitKind clase) => clase switch
    {
        CreditLimitKind.Consolidated => "consolidado",
        CreditLimitKind.ByLine => "por línea",
        _ => "rotativo",
    };
}
