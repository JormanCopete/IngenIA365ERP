using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (T0396; FR-035–FR-038, T34, T63, T66; api.md §4.2; data-model §2.1, §8.2): radicar, analizar, volver a correr las
// validaciones y anular. (nuevo)

/// <summary>
/// Envía la solicitud a análisis (api.md §4.2, <c>POST /{id}/submit</c>, <c>Lending.Applications.Submit</c>, con clave):
/// <list type="number">
/// <item>Cartera activada; la solicitud en el alcance, en <c>Draft</c> o <c>Returned</c> y con la versión leída vigente;</item>
/// <item>quien radica tiene persona y no choca con la segregación (<c>Radicar</c>, T66);</item>
/// <item>la línea admite créditos y la usura del día pasa (simulación con la versión vigente);</item>
/// <item>los documentos obligatorios que casan por monto, tipo de solicitante, forma de pago y parte (<c>Lending.Application.DocumentsMissing</c>
/// con <c>missing[{ code, name }]</c>) y la autorización para el estudio de crédito de cada parte (<c>Lending.Authorization.Missing</c>);</item>
/// <item>en una transacción: el cerrojo de <c>LND_Setup</c> y de los deudores (T34), las validaciones de FR-036 para solicitante y codeudores
/// —con la concentración excedida o sin patrimonio técnico se niega, api.md §4.2—, el puntaje, el enrutamiento a <c>PendingDecision</c> con su
/// huella, el número (<c>NumeradorDeCartera</c>, al final del cerrojo) y la intervención <c>Radicar</c>.</item>
/// </list>
/// La preferencia de abono a capital queda sellada en la solicitud (<c>PrepaymentChoice</c>) y nace como fila de <c>LND_PrepaymentPreferences</c>
/// cuando nace el crédito o el cupo: su <c>CK_LND_PrepaymentPreferences_Scope</c> exige uno de los dos. (nuevo)
/// </summary>
public sealed record SubmitCreditApplicationCommand(Guid ApplicationPublicId, byte[]? RowVersion = null)
    : IRequest<Result<CreditApplicationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SubmitCreditApplicationCommandValidator : AbstractValidator<SubmitCreditApplicationCommand>
{
    public SubmitCreditApplicationCommandValidator() => RuleFor(x => x.ApplicationPublicId).NotEmpty();
}

public sealed class SubmitCreditApplicationCommandHandler(
    IApplicationDbContext db,
    EvaluacionDeSolicitud evaluacion,
    EnrutamientoDeSolicitud enrutamiento,
    RegistroDeIntervenciones intervenciones,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IAutorizacionDeDatos autorizaciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<SubmitCreditApplicationCommand, Result<CreditApplicationDto>>
{
    /// <summary>El prefijo del radicado de una solicitud (fila de <c>LND_NumberSequences</c> de la clase <c>Application</c>).</summary>
    public const string PrefijoDelRadicado = "SOL-";

    /// <summary>El tipo del expediente de segregación de una solicitud.</summary>
    public const string Expediente = nameof(CreditApplication);

    public async Task<Result<CreditApplicationDto>> Handle(SubmitCreditApplicationCommand request, CancellationToken ct)
    {
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var s = leida.Value;
        if (SolicitudEnAlcance.Concurrencia(s, request.RowVersion) is { } vieja) return Falla(vieja);
        if (s.PuedeIrA(CreditApplicationStatus.Submitted) is not null)
            return Falla(ErroresDeOriginacion.EstadoInvalido(s.Status.ToString(), CreditApplicationStatus.Submitted.ToString()));

        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(Expediente, s.PublicId), AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        var cargada = await evaluacion.CargarAsync(s, ct);
        if (cargada.IsFailure) return Falla(cargada.Error);
        var c = cargada.Value;
        if (!c.Simulacion.Usura.Pasa)
            return Falla(Configuration.ErroresDeLineasDeCredito.UsuraExcedida("amount", c.Simulacion.Fecha, c.Simulacion.Usura,
                c.Simulacion.Combinacion.ModalityCode));
        // La versión que rige hoy (o la de la fecha del desembolso) es la que se sella al radicar (FR-042).
        s.CreditLineVersionId = c.Simulacion.Version.Version.Id;

        var faltan = await ValidacionesDeOriginacion.DocumentosFaltantesAsync(db, s, c.Partes, c.Personas, c.Simulacion.Version, hoy, ct);
        if (faltan.Count > 0) return Falla(ErroresDeOriginacion.DocumentosFaltantes(faltan));
        foreach (var parte in c.Partes)
        {
            var persona = c.Personas[parte.PersonId];
            var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
            if (autorizacion is not { Autoriza: true })
                return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, ReferenciasDeGobierno.Nombre(persona), DataAuthorizationPurpose.CreditProcessing));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(c.Partes.Select(p => p.PersonId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

            s.Radicar(verificada.Value.UsuarioId, reloj.UtcNow);
            s.SimulationSha256 = c.Simulacion.Huella;
            var evaluada = await evaluacion.EvaluarAsync(c, CheckStage.AtSubmission, null, DetenerAlRadicar, ct);
            if (evaluada.IsFailure) return Falla(evaluada.Error);

            enrutamiento.Aplicar(s, evaluada.Value.Enrutamiento);
            intervenciones.Registrar(verificada.Value, Expediente, s.PublicId);
            // Una devuelta que se reenvía conserva su radicado.
            if (s.Number is null) await numerador.NumerarSolicitudAsync(s, PrefijoDelRadicado, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, hoy, ct));
        }, ct);
    }

    /// <summary>
    /// Lo que niega la radicación en vez de quedar como validación (api.md §4.2): la concentración excedida (FR-041 bloquea) y la falta de
    /// patrimonio técnico para medirla (T59, con su alerta).
    /// </summary>
    public static Error? DetenerAlRadicar(ValidacionesEvaluadas v)
    {
        if (v.Concentracion?.Error is { } excedida) return excedida;
        // K2 (T0842; api.md §13.3): la libranza con pagaduría que excede la capacidad de descuento no se radica (422 con lo disponible).
        if (v.Bloqueante(ApplicationCheckKind.DeductionCapacity) is { Codigo: Payroll.ErroresDeLibranza.CodigoCapacidadExcedida, Detalle: DetalleDeCapacidadDeLibranza d })
            return Payroll.ErroresDeLibranza.CapacidadExcedida(d.Available, d.Requested);
        return v.Bloqueante(ApplicationCheckKind.Concentration) is { Codigo: ErroresDeOriginacion.CodigoSinPatrimonioTecnico }
            ? new Error(ErroresDeOriginacion.CodigoSinPatrimonioTecnico,
                "No hay patrimonio técnico vigente registrado en Contabilidad: no se puede validar el límite de concentración. Pida al contador registrarlo.")
            : null;
    }

    private static Result<CreditApplicationDto> Falla(Error e) => Result.Failure<CreditApplicationDto>(e);
}

/// <summary>
/// El análisis de una solicitud que espera decisión (api.md §4.2, <c>POST /{id}/analysis</c>, <c>Lending.Applications.Analyze</c>, con clave):
/// registra lo que el analista verificó de ingresos, egresos y cuotas con terceros de una parte (el solicitante si no se dice), vuelve a correr
/// las validaciones de capacidad en <c>BeforeDecision</c> y reenruta. Deja la intervención <c>Analizar</c> (T66). Fuera de
/// <c>PendingDecision</c>, <c>Lending.Application.NotInAnalysis</c>. (nuevo)
/// </summary>
public sealed record RecordCreditApplicationAnalysisCommand(
    Guid ApplicationPublicId,
    decimal? Income,
    decimal? Expenses,
    decimal? ExternalInstallments,
    string? Notes = null,
    string? Recommendation = null,
    Guid? PersonPublicId = null) : IRequest<Result<CreditApplicationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordCreditApplicationAnalysisCommandValidator : AbstractValidator<RecordCreditApplicationAnalysisCommand>
{
    public RecordCreditApplicationAnalysisCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleFor(x => x.Income).GreaterThanOrEqualTo(0m).When(x => x.Income is not null);
        RuleFor(x => x.Expenses).GreaterThanOrEqualTo(0m).When(x => x.Expenses is not null);
        RuleFor(x => x.ExternalInstallments).GreaterThanOrEqualTo(0m).When(x => x.ExternalInstallments is not null);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Recommendation).MaximumLength(500);
    }
}

public sealed class RecordCreditApplicationAnalysisCommandHandler(
    IApplicationDbContext db,
    EvaluacionDeSolicitud evaluacion,
    EnrutamientoDeSolicitud enrutamiento,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RecordCreditApplicationAnalysisCommand, Result<CreditApplicationDto>>
{
    private static readonly IReadOnlySet<ApplicationCheckKind> DeCapacidad =
        new HashSet<ApplicationCheckKind> { ApplicationCheckKind.PaymentCapacity, ApplicationCheckKind.DeductionCapacity, ApplicationCheckKind.Scoring };

    public async Task<Result<CreditApplicationDto>> Handle(RecordCreditApplicationAnalysisCommand request, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<CreditApplicationDto>(leida.Error);
        var s = leida.Value;
        if (s.Status != CreditApplicationStatus.PendingDecision)
            return Result.Failure<CreditApplicationDto>(ErroresDeOriginacion.NoEstaEnAnalisis(s.Status.ToString()));

        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId),
            AccionesDeSegregacion.Analizar, hoy, ct);
        if (verificada.IsFailure) return Result.Failure<CreditApplicationDto>(verificada.Error);

        var cargada = await evaluacion.CargarAsync(s, ct);
        if (cargada.IsFailure) return Result.Failure<CreditApplicationDto>(cargada.Error);
        var c = cargada.Value;
        var parte = request.PersonPublicId is { } persona
            ? c.Partes.FirstOrDefault(p => c.Personas[p.PersonId].PublicId == persona)
            : c.Partes.Single(p => p.Role == CreditPartyRole.Applicant);
        if (parte is null) return Result.Failure<CreditApplicationDto>(ErroresDeGobierno.PersonaInexistente());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            parte.Declarar(request.Income, request.Expenses, request.ExternalInstallments);
            var evaluada = await evaluacion.EvaluarAsync(c, CheckStage.BeforeDecision, DeCapacidad, null, ct);
            if (evaluada.IsFailure) return Result.Failure<CreditApplicationDto>(evaluada.Error);
            if (!string.Equals(s.RoutingSha256, evaluada.Value.Enrutamiento.Huella, StringComparison.Ordinal))
                enrutamiento.Aplicar(s, evaluada.Value.Enrutamiento);
            intervenciones.Registrar(verificada.Value, SubmitCreditApplicationCommandHandler.Expediente, s.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, hoy, ct));
        }, ct);
    }
}

/// <summary>
/// Vuelve a correr validaciones de una solicitud radicada (api.md §4.2, <c>POST /{id}/checks/rerun</c>, <c>Lending.Applications.Analyze</c>, con
/// clave): todas o las nombradas (<c>Screening</c>, <c>Bureau</c>, <c>Capacity</c>, <c>PayrollCapacity</c>, <c>Consolidated</c>,
/// <c>CreditLimit</c>, <c>Concentration</c>, <c>Usury</c>, <c>Requirements</c>), en <c>BeforeDecision</c>, y reenruta si la huella cambió. Así se
/// repite una consulta de listas o de centrales que no estuvo disponible. (nuevo)
/// </summary>
public sealed record RerunCreditApplicationChecksCommand(Guid ApplicationPublicId, IReadOnlyList<string>? Checks = null)
    : IRequest<Result<IReadOnlyList<CreditApplicationCheckDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>Los nombres de api.md §4.2 y las clases que vuelven a correr.</summary>
    public static readonly IReadOnlyDictionary<string, ApplicationCheckKind[]> Nombres = new Dictionary<string, ApplicationCheckKind[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Screening"] = [ApplicationCheckKind.RestrictiveLists],
        ["Bureau"] = [ApplicationCheckKind.CreditBureau],
        ["Capacity"] = [ApplicationCheckKind.PaymentCapacity],
        ["PayrollCapacity"] = [ApplicationCheckKind.DeductionCapacity],
        ["Consolidated"] = [ApplicationCheckKind.ConsolidatedExposure],
        ["CreditLimit"] = [ApplicationCheckKind.CreditLimit],
        ["Concentration"] = [ApplicationCheckKind.Concentration],
        ["Usury"] = [ApplicationCheckKind.Usury],
        ["Requirements"] = [ApplicationCheckKind.LineRequirements, ApplicationCheckKind.Documents, ApplicationCheckKind.DataAuthorization],
    };
}

public sealed class RerunCreditApplicationChecksCommandValidator : AbstractValidator<RerunCreditApplicationChecksCommand>
{
    public RerunCreditApplicationChecksCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleForEach(x => x.Checks).Must(c => RerunCreditApplicationChecksCommand.Nombres.ContainsKey(c ?? string.Empty))
            .WithMessage("Elija validaciones de la lista: Screening, Bureau, Capacity, PayrollCapacity, Consolidated, CreditLimit, Concentration, Usury o Requirements.");
    }
}

public sealed class RerunCreditApplicationChecksCommandHandler(
    IApplicationDbContext db,
    EvaluacionDeSolicitud evaluacion,
    EnrutamientoDeSolicitud enrutamiento,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RerunCreditApplicationChecksCommand, Result<IReadOnlyList<CreditApplicationCheckDto>>>
{
    public async Task<Result<IReadOnlyList<CreditApplicationCheckDto>>> Handle(RerunCreditApplicationChecksCommand request, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<IReadOnlyList<CreditApplicationCheckDto>>(leida.Error);
        var s = leida.Value;
        if (s.Status is not (CreditApplicationStatus.Submitted or CreditApplicationStatus.PendingDecision))
            return Result.Failure<IReadOnlyList<CreditApplicationCheckDto>>(ErroresDeOriginacion.NoEstaEnAnalisis(s.Status.ToString()));

        var cargada = await evaluacion.CargarAsync(s, ct);
        if (cargada.IsFailure) return Result.Failure<IReadOnlyList<CreditApplicationCheckDto>>(cargada.Error);
        IReadOnlySet<ApplicationCheckKind>? solo = request.Checks is { Count: > 0 } nombres
            ? nombres.SelectMany(n => RerunCreditApplicationChecksCommand.Nombres[n]).ToHashSet()
            : null;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var evaluada = await evaluacion.EvaluarAsync(cargada.Value, CheckStage.BeforeDecision, solo, null, ct);
            if (evaluada.IsFailure) return Result.Failure<IReadOnlyList<CreditApplicationCheckDto>>(evaluada.Error);
            if (s.Status == CreditApplicationStatus.PendingDecision && !string.Equals(s.RoutingSha256, evaluada.Value.Enrutamiento.Huella, StringComparison.Ordinal))
                enrutamiento.Aplicar(s, evaluada.Value.Enrutamiento);
            await db.SaveChangesAsync(ct);
            var dto = await ConsultasDeSolicitudes.DtoAsync(db, s, reloj.HoyLocal, ct);
            return Result.Success<IReadOnlyList<CreditApplicationCheckDto>>(dto.Checks);
        }, ct);
    }
}

/// <summary>
/// Anula una solicitud o registra el desistimiento del solicitante (api.md §4.2, <c>POST /{id}/cancel</c>, <c>Lending.Applications.Cancel</c>, con
/// clave y motivo; FR-047): en cualquier estado anterior a <c>Disbursed</c>/<c>Granted</c>, sin generar cartera. Desembolsada u otorgada,
/// <c>Lending.Application.AlreadyDisbursed</c>. (nuevo)
/// </summary>
public sealed record CancelCreditApplicationCommand(Guid ApplicationPublicId, string Kind, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public const string Anulacion = "Annulment";
    public const string Desistimiento = "Withdrawal";
}

public sealed class CancelCreditApplicationCommandValidator : ValidadorConMotivo<CancelCreditApplicationCommand>
{
    public CancelCreditApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(k => k is CancelCreditApplicationCommand.Anulacion or CancelCreditApplicationCommand.Desistimiento)
            .WithMessage("Indique si es una anulación (Annulment) o el desistimiento del solicitante (Withdrawal).");
    }
}

public sealed class CancelCreditApplicationCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    Integration.MovimientoContingenteDeCartera? contingentes = null) : IRequestHandler<CancelCreditApplicationCommand, Result>
{
    public async Task<Result> Handle(CancelCreditApplicationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        var s = leida.Value;
        if (s.Status is CreditApplicationStatus.Disbursed or CreditApplicationStatus.Granted) return Result.Failure(ErroresDeOriginacion.YaDesembolsada());
        if (s.PuedeIrA(CreditApplicationStatus.Cancelled) is not null)
            return Result.Failure(ErroresDeOriginacion.EstadoInvalido(s.Status.ToString(), CreditApplicationStatus.Cancelled.ToString()));

        var prefijo = request.Kind == CancelCreditApplicationCommand.Desistimiento ? "Desistimiento" : "Anulación";
        s.Cancelar(usuario, reloj.UtcNow, $"{prefijo}: {request.Reason.Trim()}");
        // T0659 (A3): desistir o anular una aprobada baja el contingente 9115 en el mismo guardado.
        if (contingentes is not null && await contingentes.SolicitudAsync(s, reloj.HoyLocal, ct) is { IsFailure: true } contingente)
            return Result.Failure(contingente.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
