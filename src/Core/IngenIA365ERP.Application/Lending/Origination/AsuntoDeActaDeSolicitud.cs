using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// El asunto de acta <c>CreditApplication</c> (feature 013, US2 bloque 6B; T0399; FR-039, FR-040, T64): la solicitud que decide un órgano.
/// <list type="bullet">
/// <item><see cref="DescribirAsync"/>: el resumen del punto (número, persona, monto, nivel, vinculado y el fundamento del enrutamiento), pendiente
/// mientras espera decisión (o aprobada y vencida), el expediente de la solicitud, la huella del enrutamiento <b>de hoy</b> (si cambia entre la
/// revisión del acta y su cierre, <c>Lending.Decision.ContentChanged</c>), el órgano del nivel efectivo, y quienes no votan: el radicador
/// (<c>Filer</c>) y el solicitante y sus codeudores (<c>Applicant</c>).</item>
/// <item><see cref="AlDecidirAsync"/>: con las <b>mismas revalidaciones</b> que la decisión individual
/// (<see cref="DecisionSobreSolicitud.RevalidarAprobacionAsync"/>, sin escribir validaciones) aplica la negación o la devolución, y deja lista la
/// aprobación; si una revalidación falla, el cierre entero se revierte.</item>
/// <item><see cref="TrasGuardarAsync"/>: con la decisión ya guardada, aprueba la solicitud con su vigencia y las condiciones del punto.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class AsuntoDeActaDeSolicitud(
    IApplicationDbContext db,
    EvaluacionDeSolicitud evaluacion,
    ValidacionesDeOriginacion validaciones,
    EnrutamientoDeSolicitud enrutamiento,
    DecisionSobreSolicitud decisiones,
    IDateTimeService reloj) : IAsuntoDeActa
{
    private readonly Dictionary<int, AprobacionRevalidada> _aprobaciones = [];

    public string SubjectType => LendingDecisionSubjects.CreditApplication;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var s = await db.CreditApplications.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == subjectPublicId && !a.IsDeleted, ct);
        if (s is null) return Result.Failure<AsuntoDeActa>(ErroresDeOriginacion.SolicitudInexistente());

        var pendiente = s.Status == CreditApplicationStatus.PendingDecision || s.AprobacionVencida(fecha);
        var partes = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).Select(p => p.PersonId)
            .Distinct().ToListAsync(ct);
        var personas = partes.Select(p => new PersonaDelAsunto(p, MotivosDeExclusion.Solicitante)).ToList();
        if (s.SubmittedByUserId is { } radicador
            && await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == radicador).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } suPersona)
            personas.Insert(0, new PersonaDelAsunto(suPersona, MotivosDeExclusion.Radicador));

        var solicitante = (await ReferenciasDeGobierno.PersonasAsync(db, [s.ApplicantPersonId], ct))[s.ApplicantPersonId];
        var titulo = string.Create(CultureInfo.InvariantCulture,
            $"Solicitud {s.Number ?? "sin radicar"} de {solicitante.FullName} por {ErroresDeOriginacion.Pesos(s.RequestedAmount)}");
        if (s.Status is CreditApplicationStatus.Draft or CreditApplicationStatus.Returned or CreditApplicationStatus.Submitted)
            return Result.Success(new AsuntoDeActa(titulo, null, false, s.Status.ToString(),
                new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId), s.RoutingSha256 ?? string.Empty) { Personas = personas });

        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstAsync(v => v.Id == s.CreditLineVersionId, ct);
        var datos = await enrutamiento.DatosVigentesAsync(s, fecha, ct);
        var calculado = await enrutamiento.CalcularAsync(s, linea, version, datos, fecha, ct);
        if (calculado.IsFailure) return Result.Failure<AsuntoDeActa>(calculado.Error);
        var e = calculado.Value;

        var resumen = string.Join(" ", new[]
        {
            $"Solicitante: {solicitante.FullName}.",
            $"Línea {linea.Code}, {ErroresDeOriginacion.Pesos(s.RequestedAmount)}" + (s.Installments is { } n ? string.Create(CultureInfo.InvariantCulture, $" a {n} cuotas.") : "."),
            $"Endeudamiento consolidado: {ErroresDeOriginacion.Pesos(e.Datos.MontoConsolidado)}.",
            $"Nivel: {e.Nivel.Name}.",
            e.Datos.HayVinculado ? "Hay una persona vinculada (FR-040)." : "Sin personas vinculadas.",
            "Fundamento: " + string.Join(" ", e.Enrutamiento.Razones.Select(r => r.Detalle)),
        });
        return Result.Success(new AsuntoDeActa(titulo, resumen, pendiente, pendiente ? CreditApplicationStatus.PendingDecision.ToString() : s.Status.ToString(),
            new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId), e.Huella)
        {
            Personas = personas,
            GoverningBodyId = e.Nivel.GoverningBodyId,
            MontoConsolidado = e.Datos.MontoConsolidado,
            RoutingSha256 = e.Huella,
            AttributionMatrixId = e.Matriz.Matriz.Id,
            LevelId = e.Nivel.Id,
        });
    }

    public async Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var s = await db.CreditApplications.FirstOrDefaultAsync(a => a.PublicId == decision.Punto.SubjectPublicId && !a.IsDeleted, ct);
        if (s is null) return Result.Failure(ErroresDeOriginacion.SolicitudInexistente());
        if (s.Status != CreditApplicationStatus.PendingDecision && !s.AprobacionVencida(decision.Fecha))
            return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(s.Status.ToString()));

        switch (decision.Resultado)
        {
            case CreditDecisionOutcome.Deny:
                if (s.Status == CreditApplicationStatus.Approved && await ReenrutarVencidaAsync(s, decision.Fecha, ct) is { } sinRuta1) return Result.Failure(sinRuta1);
                s.Negar();
                return Result.Success();
            case CreditDecisionOutcome.Return:
                if (s.Status == CreditApplicationStatus.Approved && await ReenrutarVencidaAsync(s, decision.Fecha, ct) is { } sinRuta2) return Result.Failure(sinRuta2);
                s.Devolver();
                return Result.Success();
        }

        // Aprobar: las mismas revalidaciones que la decisión individual, sin escribir validaciones (el cierre no guarda hasta el final).
        var cargada = await evaluacion.CargarAsync(s, ct);
        if (cargada.IsFailure) return Result.Failure(cargada.Error);
        var c = cargada.Value;
        var evaluadas = await validaciones.EvaluarAsync(new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, CheckStage.BeforeDecision,
            reloj.HoyLocal), ct);
        var condiciones = decision.Resultado == CreditDecisionOutcome.ApproveWithConditions ? DecisionConditionsInput.DeJson(decision.Condiciones) : null;
        var revalidada = await decisiones.RevalidarAprobacionAsync(c, evaluadas, condiciones, ct);
        if (revalidada.IsFailure) return Result.Failure(revalidada.Error);
        if (s.Status == CreditApplicationStatus.Approved && await ReenrutarVencidaAsync(s, decision.Fecha, ct) is { } sinRuta3) return Result.Failure(sinRuta3);
        _aprobaciones[s.Id] = revalidada.Value;
        return Result.Success();
    }

    public async Task<Result> TrasGuardarAsync(DecisionDeActa decision, CancellationToken ct)
    {
        if (decision.Resultado is not (CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)) return Result.Success();
        var s = await db.CreditApplications.FirstAsync(a => a.PublicId == decision.Punto.SubjectPublicId, ct);
        if (!_aprobaciones.TryGetValue(s.Id, out var aprobacion)) return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(s.Status.ToString()));
        var vigencia = await decisiones.VigenciaAsync(s.CreditLineId, decision.Fecha, ct);
        if (vigencia.IsFailure) return Result.Failure(vigencia.Error);
        s.Aprobar(decision.Decision.Id, aprobacion.Monto, aprobacion.Condiciones ?? decision.Condiciones, vigencia.Value);
        return Result.Success();
    }

    /// <summary>Una aprobada que venció vuelve a <c>PendingDecision</c> antes de decidirla otra vez (C53), con el enrutamiento de hoy.</summary>
    private async Task<Error?> ReenrutarVencidaAsync(CreditApplication s, DateOnly fecha, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstAsync(v => v.Id == s.CreditLineVersionId, ct);
        var calculado = await enrutamiento.CalcularAsync(s, linea, version, await enrutamiento.DatosVigentesAsync(s, fecha, ct), fecha, ct);
        if (calculado.IsFailure) return calculado.Error;
        enrutamiento.Aplicar(s, calculado.Value);
        return null;
    }
}
