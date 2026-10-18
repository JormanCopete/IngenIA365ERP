using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

// Feature 013, US2 (bloque 6B; T0405; FR-029, FR-049, FR-058, FR-059, FR-142, T34, T63; api.md §7.1; data-model §2.10): los cupos consolidado,
// por línea y rotativo. Se otorgan antes y después de activar (FR-142). Cada cambio es una versión de sólo inserción; el estado y el disponible se
// calculan al leer. (nuevo)

/// <summary>El tipo de expediente de segregación de un cupo (la asignación es su «radicación», T66). (nuevo)</summary>
public static class ExpedienteDeCupo
{
    public const string Tipo = nameof(CreditLimit);

    public const string PermisoDeAsignar = "Lending.CreditLimits.Assign";
}

/// <summary>Un cupo por su <c>PublicId</c> dentro del alcance: un cupo de una línea fuera del alcance es el mismo 404 que uno inexistente. (nuevo)</summary>
public static class CupoEnAlcance
{
    public static async Task<Result<CreditLimit>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid publicId,
        CancellationToken ct)
    {
        var cupo = await db.CreditLimits.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == publicId && !c.IsDeleted, ct);
        if (cupo is null) return Result.Failure<CreditLimit>(ErroresDeDecisionesYCupos.CupoInexistente());
        var dentro = await alcance.AsegurarAsync(null, cupo.CreditLineId, ErroresDeDecisionesYCupos.CupoInexistente(), auditoria, ExpedienteDeCupo.Tipo,
            cupo.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<CreditLimit>(dentro.Error) : Result.Success(cupo);
    }
}

/// <summary>
/// Otorga un cupo (api.md §7.1, <c>POST /api/lending/credit-limits</c>, <c>Lending.CreditLimits.Assign</c>, con clave y motivo; también antes de
/// activar, FR-142):
/// <list type="number">
/// <item>la persona, la línea en el alcance (fuera, el 404) y la forma del cupo (<see cref="CreditLimit.Validar"/>); el calculado sale del saldo
/// de aportes (<see cref="SaldoDeAportesVigente"/>, D19) por la relación cupo/aportes de la línea y sella la carga usada;</item>
/// <item>un no asociado sólo con una línea que lo admita (<c>Lending.CreditLine.NonAssociatesNotAllowed</c>), la autorización para el estudio
/// del crédito, ningún bloqueo vigente, sin cruce con una versión pendiente o posterior (<c>Lending.CreditLimit.Overlaps</c>), la consulta a
/// centrales si la línea la exige y la concentración por deudor y grupo (FR-041);</item>
/// <item>el enrutamiento (sujeto <c>CreditLimit</c>): rige de una vez (<c>Active</c>) si el monto cabe en la atribución operativa de quien lo
/// asigna (<c>SEC_PermissionAmountLimits</c>) y la matriz no pide otro nivel que el del techo; si no, queda <c>PendingDecision</c> con su
/// enrutamiento, sin error, y la asignación es la «radicación» del expediente (quien la pidió no decide ni vota);</item>
/// <item>en una transacción con el cerrojo del deudor (T34): la versión, la consulta de listas con su resultado (una coincidencia deja el cupo
/// bloqueado y la respuesta lo avisa con <c>Lending.Screening.Blocked</c>), la preferencia del rotativo (<c>CreditLimitGrant</c>) y, con
/// <c>validTo</c>, la versión en cero del día siguiente.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record AssignCreditLimitCommand(
    Guid PersonPublicId,
    CreditLimitKind Kind,
    Guid? CreditLinePublicId,
    CreditLimitOrigin Origin,
    decimal? Amount,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    PrepaymentChoice? PrepaymentPreference,
    string Reason,
    Guid? ScreeningResultPublicId = null,
    Guid? BureauQueryPublicId = null,
    IReadOnlyList<AuthorizationInput>? Authorizations = null) : IRequest<Result<CreditLimitDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AssignCreditLimitCommandValidator : ValidadorConMotivo<AssignCreditLimitCommand>
{
    public AssignCreditLimitCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Origin).Must(o => o is CreditLimitOrigin.Calculated or CreditLimitOrigin.Assigned)
            .WithMessage("Un cupo se calcula o se asigna; el migrado lo trae la carga de migración.");
        RuleFor(x => x.Amount).NotNull().GreaterThan(0m).PrecisionScale(18, 2, true).When(x => x.Origin == CreditLimitOrigin.Assigned)
            .WithMessage("El cupo asignado es mayor que cero y admite hasta 2 decimales.");
        RuleFor(x => x.CreditLinePublicId).NotNull().When(x => x.Kind == CreditLimitKind.ByLine).WithMessage("El cupo por línea nombra su línea.");
        RuleFor(x => x.CreditLinePublicId).Null().When(x => x.Kind == CreditLimitKind.Consolidated).WithMessage("El cupo consolidado no es de una línea.");
        RuleFor(x => x.CreditLinePublicId).NotNull().When(x => x.Origin == CreditLimitOrigin.Calculated)
            .WithMessage("Calcular el cupo exige la línea con su relación cupo/aportes.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige el cupo.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("El cupo no puede terminar antes de empezar.");
        RuleFor(x => x.PrepaymentPreference).NotNull().IsInEnum().When(x => x.Kind == CreditLimitKind.Revolving)
            .WithMessage("Indique qué prefiere el deudor al abonar a capital en los créditos del cupo rotativo: reducir el plazo o la cuota.");
    }
}

public sealed class AssignCreditLimitCommandHandler(
    IApplicationDbContext db,
    EnrutamientoDeCupo enrutamiento,
    ValidacionDeConcentracion concentracion,
    IConsultaDeListasDeCartera listas,
    BloqueosDelDeudor bloqueos,
    SaldoDeAportesVigente saldos,
    VersionVigenteDeLinea versiones,
    IAutorizacionDeDatos autorizaciones,
    IPerfilRegulatorio perfil,
    GuardiaDeCanalesExternos guardia,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj,
    Integration.MovimientoContingenteDeCartera? contingentes = null) : IRequestHandler<AssignCreditLimitCommand, Result<CreditLimitDto>>
{
    public async Task<Result<CreditLimitDto>> Handle(AssignCreditLimitCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var hoy = reloj.HoyLocal;

        CreditLine? linea = null;
        VersionDeLineaLeida? version = null;
        if (request.CreditLinePublicId is { } lineaPedida)
        {
            var enAlcance = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, lineaPedida, seguir: false, ct);
            if (enAlcance.IsFailure) return Falla(enAlcance.Error);
            linea = enAlcance.Value;
            version = await versiones.LeerAsync(linea.Id, request.ValidFrom, ct: ct) ?? await versiones.LeerAsync(linea.Id, hoy, ct: ct);
            if (version is null) return Falla(ErroresDeGobierno.LineaInexistente());
        }

        // El monto: asignado o calculado con el saldo de aportes (D19) por la relación cupo/aportes de la línea.
        var detalle = new DetalleDeCupo { PrepaymentPreference = request.PrepaymentPreference, ValidTo = request.ValidTo };
        int? carga = null;
        decimal monto;
        if (request.Origin == CreditLimitOrigin.Calculated)
        {
            if (version?.Version.ContributionsMultiple is not { } multiplo || multiplo <= 0m)
                return Falla(ErroresDeDecisionesYCupos.CupoSinCalculo("NoContributionsMultiple",
                    "La línea no tiene relación cupo/aportes: asigne el cupo con un monto o configure la relación en la línea."));
            var saldo = await saldos.DeAsync(persona.Id, request.ValidFrom, ct: ct);
            if (saldo is null)
                return Falla(ErroresDeDecisionesYCupos.CupoSinCalculo("NoContributionsBalance",
                    "La persona no tiene saldo de aportes conocido a la fecha del cupo. Cárguelo con la plantilla de saldos de aportes o asigne el cupo con un monto."));
            monto = AritmeticaDeCupo.Redondear(saldo.Aportes * multiplo);
            carga = saldo.CargaId;
            detalle = detalle with
            {
                Contributions = saldo.Aportes, ContributionsAsOf = saldo.Corte, ContributionsSource = saldo.Fuente, ContributionsMultiple = multiplo,
            };
        }
        else monto = request.Amount ?? 0m;
        if (CreditLimit.Validar(request.Kind, linea?.Id, monto, request.Reason) is { } forma) return Falla(ErroresDeGobierno.De(forma));

        // Un no asociado sólo tiene cupo otorgado en una línea que lo admita (FR-049, FR-018).
        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, persona, hoy, ct);
        if (!afiliacion.Activo && !await AdmiteNoAsociadosAsync(request.Kind, version, hoy, ct))
            return Falla(ErroresDeDecisionesYCupos.NoAsociadosNoAdmitidos(persona.PublicId, ReferenciasDeGobierno.Nombre(persona)));

        // Las autorizaciones del cuerpo se agregan sin guardar; la del estudio del crédito tiene que estar vigente.
        foreach (var a in request.Authorizations ?? [])
        {
            if (await autorizaciones.VigenteAsync(a.PersonPublicId, a.Purpose, a.ContactChannel?.ToString(), ct) is { Autoriza: true }) continue;
            var agregada = await autorizaciones.AgregarAsync(new AutorizacionAAgregar(a.PersonPublicId, a.Purpose, DecisionDeAutorizacion.Accepted,
                a.PolicyVersionPublicId, a.ContactChannel, a.EvidenceAttachmentPublicId, DataAuthorizationOrigin.CreditApplication, a.GrantedAt ?? reloj.UtcNow), ct);
            if (agregada.IsFailure) return Falla(agregada.Error);
        }
        var traeLaDelEstudio = (request.Authorizations ?? [])
            .Any(a => a.PersonPublicId == persona.PublicId && a.Purpose == DataAuthorizationPurpose.CreditProcessing);
        if (!traeLaDelEstudio
            && await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct) is not { Autoriza: true })
            return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, ReferenciasDeGobierno.Nombre(persona), DataAuthorizationPurpose.CreditProcessing));

        var vigentes = await bloqueos.VigentesAsync([persona.Id], ct);
        if (vigentes.Count > 0) return Falla(DeudorBloqueado(vigentes));

        var todas = (await VersionesDeCupo.DeAsync(db, [persona.Id], ct)).Where(v => v.EsDelMismoCupo(persona.Id, request.Kind, linea?.Id)).ToList();
        var decididas = await VersionesDeCupo.DecididasAsync(db, todas.Select(v => v.PublicId).ToList(), ct);
        if (VersionesDeCupo.Pendiente(todas, decididas) is not null) return Falla(ErroresDeDecisionesYCupos.CupoSeCruza("Pending", null));
        if (VersionesDeCupo.UltimaFirme(todas) is { } ultima && ultima.ValidFrom > request.ValidFrom)
            return Falla(ErroresDeDecisionesYCupos.CupoSeCruza("Later", ultima.ValidFrom));

        // Centrales, si la línea las exige (T73): la consulta vigente que respondió, y no simulada en producción.
        if (version?.Version.RequiresBureauQuery == true)
        {
            var consultas = await db.BureauQueries.AsNoTracking().Where(q => q.PersonId == persona.Id && !q.IsDeleted).ToListAsync(ct);
            var consulta = request.BureauQueryPublicId is { } pedida
                ? consultas.FirstOrDefault(q => q.PublicId == pedida && q.VigenteEn(hoy))
                : BureauQuery.UltimaVigente(consultas.Where(q => q.Respondio), persona.Id, hoy);
            if (consulta is not { Respondio: true })
                return Falla(new ErrorConDatos(ErroresDeOriginacion.CodigoConsultaACentralesRequerida,
                    "La línea exige una consulta a centrales de riesgo vigente: consulte la central antes de otorgar el cupo.", new { required = true }));
            if (guardia.Evaluar(EfectoLegalDeCanal.BureauEvidenceForApproval, consulta.ChannelCode, consulta.IsSimulated).ComoError() is { } simulada)
                return Falla(simulada);
            detalle = detalle with { BureauQueryPublicId = consulta.PublicId };
        }

        // Concentración por deudor y por grupo (FR-041): excedida o sin patrimonio técnico, no se otorga.
        var conc = await concentracion.ValidarAsync(persona.Id, monto, conGarantiaAdmisible: false, hoy, null, ct);
        if (conc.IsFailure) return Falla(conc.Error);

        var enrutado = await enrutamiento.CalcularAsync(persona.Id, request.Kind, linea?.Id, monto, hoy, ct);
        if (enrutado.IsFailure) return Falla(enrutado.Error);
        var operativo = await LimitesDePermisoPorUsuario.DeAsync(db, usuario, ExpedienteDeCupo.PermisoDeAsignar, hoy, ct);
        var pide = (operativo is { } maximo && monto > maximo) || enrutado.Value.PideOtroNivel;
        detalle = detalle with { RequiresDecision = pide, OperationalLimit = operativo, Routing = pide ? enrutado.Value.Ruta() : null };

        var publicId = Guid.NewGuid();
        IntervencionVerificada? radicacion = null;
        if (pide)
        {
            var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(ExpedienteDeCupo.Tipo, publicId), AccionesDeSegregacion.Radicar, hoy, ct);
            if (verificada.IsFailure) return Falla(verificada.Error);
            radicacion = verificada.Value;
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);

            var numero = (todas.Count == 0 ? 0 : todas.Max(v => v.VersionNumber)) + 1;
            var cupo = CreditLimit.NuevaVersion(persona.Id, request.Kind, linea?.Id, monto, request.Origin, request.ValidFrom, numero, reloj.UtcNow,
                request.Reason, usuario, detalle: detalle.Serializar(), cargaDeAportes: carga);
            cupo.PublicId = publicId;
            db.CreditLimits.Add(cupo);
            await db.SaveChangesAsync(ct);

            var avisos = new List<AvisoDeCarteraDto>();
            var consultada = await listas.ConsultarAsync(new PedidoDeConsultaDeListas(persona.Id, ScreeningContext.CreditLimit, CreditLimitId: cupo.Id), ct);
            if (!consultada.CuentaComoConsulta)
                return Falla(new ErrorConDatos(ErroresDeOriginacion.CodigoCanalNoDisponible,
                    "La consulta de listas restrictivas no está disponible: no se puede otorgar el cupo sin ella. Intente más tarde.",
                    new { channelCode = consultada.Resultado.ChannelCode }));
            if (consultada.Bloquea)
            {
                var aviso = ErroresDeDecisionesYCupos.ListasBloquean(consultada.Resultado.PublicId);
                avisos.Add(new AvisoDeCarteraDto(aviso.Code, aviso.Message, consultada.Resultado.PublicId));
            }

            if (request.Kind == CreditLimitKind.Revolving)
                db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(persona.Id, null, cupo.Id, request.PrepaymentPreference, 1, reloj.UtcNow,
                    PreferenceOrigin.CreditLimitGrant, usuario));
            if (!pide && request.ValidTo is { } hasta)
                db.CreditLimits.Add(CreditLimit.NuevaVersion(persona.Id, request.Kind, linea?.Id, 0m, request.Origin, hasta.AddDays(1), numero + 1, reloj.UtcNow,
                    $"Fin de la vigencia del cupo otorgado: {request.Reason.Trim()}", usuario));
            if (radicacion is not null) intervenciones.Registrar(radicacion, ExpedienteDeCupo.Tipo, cupo.PublicId);
            // T0659 (A3): el cupo rotativo otorgado de una vez mueve el contingente 913005 (C92); el que espera decisión, al decidirse.
            if (contingentes is not null && await contingentes.CupoAsync(cupo, hoy, ct) is { IsFailure: true } contingente) return Falla(contingente.Error);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeCupos.DtoAsync(db, cupo, hoy, ct, avisos));
        }, ct);
    }

    /// <summary>
    /// ¿Hay una línea que admita a un no asociado? La del cupo; sin línea, alguna vigente que lo admita (y de origen comercial, si es rotativo). El
    /// perfil regulatorio también tiene que permitirlo (FR-018).
    /// </summary>
    private async Task<bool> AdmiteNoAsociadosAsync(CreditLimitKind clase, VersionDeLineaLeida? version, DateOnly hoy, CancellationToken ct)
    {
        if (!ReglasDeLineaDeCredito.PermiteNoAsociados(await perfil.VigenteAsync(hoy, null, ct))) return false;
        if (version is not null) return version.Version.AllowsNonAssociates;
        var lineas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Status == CreditLineStatus.Active && l.Nature == LineNature.Placement).Select(l => l.Id).ToListAsync(ct);
        var vigentes = await versiones.LeerVariasAsync(lineas, hoy, ct: ct);
        return vigentes.Values.Any(v => v.Version.AllowsNonAssociates && (clase != CreditLimitKind.Revolving || v.Version.AllowsCommercialOrigin));
    }

    internal static Error DeudorBloqueado(IReadOnlyList<DebtorBlock> vigentes) => new ErrorConDatos(ErroresDeOriginacion.CodigoDeudorBloqueado,
        $"La persona tiene un {ErroresDeDecisionesYCupos.Bloqueo(vigentes[0].Reason)} vigente desde el {ErroresDeOriginacion.Fecha(vigentes[0].StartedOn)}: " +
        "no se le otorga cupo ni se origina hasta que se levante.",
        new { blocks = vigentes.Select(b => new { kind = b.Reason.ToString(), since = ErroresDeOriginacion.Fecha(b.StartedOn), blockPublicId = b.PublicId }).ToList() });

    private static Result<CreditLimitDto> Falla(Error e) => Result.Failure<CreditLimitDto>(e);
}

/// <summary>El redondeo de un cupo calculado: a pesos con dos decimales, lejos de cero (alias <c>Monto</c>). (nuevo)</summary>
public static class AritmeticaDeCupo
{
    public static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Termina un cupo (api.md §7.1, <c>POST /{id}/end</c>, <c>Lending.CreditLimits.Assign</c>, con clave y motivo): la versión que rige (activa o
/// bloqueada) sigue hasta <see cref="ValidTo"/> y desde el día siguiente rige una versión en cero. Una versión que no rige →
/// <c>Lending.CreditLimit.NotActive</c>. (nuevo)
/// </summary>
public sealed record EndCreditLimitCommand(Guid CreditLimitPublicId, DateOnly ValidTo, string Reason) : IRequest<Result<CreditLimitDto>>, IOperacionIdempotente,
    IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndCreditLimitCommandValidator : ValidadorConMotivo<EndCreditLimitCommand>
{
    public EndCreditLimitCommandValidator()
    {
        RuleFor(x => x.CreditLimitPublicId).NotEmpty();
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indique hasta cuándo rige el cupo.");
    }
}

public sealed class EndCreditLimitCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<EndCreditLimitCommand, Result<CreditLimitDto>>
{
    public async Task<Result<CreditLimitDto>> Handle(EndCreditLimitCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CreditLimitDto>(ErroresDeGobierno.SinUsuario());
        var leido = await CupoEnAlcance.LeerAsync(db, alcance, auditoria, request.CreditLimitPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditLimitDto>(leido.Error);
        var cupo = leido.Value;
        var hoy = reloj.HoyLocal;

        var todas = (await VersionesDeCupo.DeAsync(db, [cupo.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, cupo)).ToList();
        var decididas = await VersionesDeCupo.DecididasAsync(db, todas.Select(v => v.PublicId).ToList(), ct);
        var estado = VersionesDeCupo.Estado(cupo, todas, decididas, [], hoy);
        if (estado != EstadosDeCupo.Activo || VersionesDeCupo.UltimaFirme(todas)?.PublicId != cupo.PublicId)
            return Result.Failure<CreditLimitDto>(ErroresDeDecisionesYCupos.CupoNoActivo(estado));
        if (request.ValidTo < cupo.ValidFrom)
            return Result.Failure<CreditLimitDto>(ErroresDeDecisionesYCupos.CondicionesInvalidas("validTo", "El cupo no puede terminar antes de empezar."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cupo.PersonId], ModoDeEspera.Esperar, ct);
            var fin = CreditLimit.NuevaVersion(cupo.PersonId, cupo.Kind, cupo.CreditLineId, 0m, cupo.Origin, request.ValidTo.AddDays(1),
                todas.Max(v => v.VersionNumber) + 1, reloj.UtcNow, request.Reason, usuario);
            db.CreditLimits.Add(fin);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeCupos.DtoAsync(db, fin, hoy, ct));
        }, ct);
    }
}

/// <summary>
/// Revalida un cupo (api.md §7.1, <c>POST /{id}/revalidate</c>, <c>Lending.CreditLimits.Assign</c>, con clave y motivo; FR-142): si ya cumple
/// —la persona no es vinculada o el cupo lo otorgó el consejo, y el disponible cabe en el límite de concentración— levanta el bloqueo «cupo por
/// revalidar»; si no, lo deja bloqueado y dice qué falta (<c>Lending.Attribution.LevelRequired</c>, <c>Lending.Concentration.LimitExceeded</c>).
/// (nuevo)
/// </summary>
public sealed record RevalidateCreditLimitCommand(Guid CreditLimitPublicId, string Reason) : IRequest<Result<CreditLimitDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RevalidateCreditLimitCommandValidator : ValidadorConMotivo<RevalidateCreditLimitCommand>
{
    public RevalidateCreditLimitCommandValidator() => RuleFor(x => x.CreditLimitPublicId).NotEmpty();
}

public sealed class RevalidateCreditLimitCommandHandler(
    IApplicationDbContext db,
    RevalidacionDeCupos revalidacion,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RevalidateCreditLimitCommand, Result<CreditLimitDto>>
{
    public async Task<Result<CreditLimitDto>> Handle(RevalidateCreditLimitCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CreditLimitDto>(ErroresDeGobierno.SinUsuario());
        var leido = await CupoEnAlcance.LeerAsync(db, alcance, auditoria, request.CreditLimitPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditLimitDto>(leido.Error);
        var cupo = leido.Value;
        var hoy = reloj.HoyLocal;

        var todas = (await VersionesDeCupo.DeAsync(db, [cupo.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, cupo)).ToList();
        if (VersionesDeCupo.Vigente(todas, hoy) is not { Amount: > 0m } vigente || vigente.PublicId != cupo.PublicId)
            return Result.Failure<CreditLimitDto>(ErroresDeDecisionesYCupos.CupoNoActivo(EstadosDeCupo.Terminado));

        var falta = await revalidacion.QueFaltaAsync(cupo, hoy, ct);
        if (falta is not null) return Result.Failure<CreditLimitDto>(falta);

        foreach (var bloqueo in await revalidacion.BloqueosDeRevalidacionAsync(cupo.PersonId, todas, ct))
            bloqueo.Levantar(hoy, reloj.UtcNow, usuario, $"Cupo revalidado: {request.Reason.Trim()}");
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeCupos.DtoAsync(db, cupo, hoy, ct));
    }
}

/// <summary>
/// Cambia la preferencia de abono a capital de un cupo rotativo (api.md §7.1, <c>POST /{id}/prepayment-preference</c>,
/// <c>Lending.PrepaymentPreferences.Manage</c>, con clave; D24): una versión nueva (<c>DebtorRequest</c>) que heredan los créditos que nazcan
/// después. Otro cupo → <c>Lending.CreditLimit.NotRevolving</c> (la de un crédito la cambia US3). (nuevo)
/// </summary>
public sealed record ChangePrepaymentPreferenceCommand(Guid CreditLimitPublicId, PrepaymentChoice Choice, string? Reason = null,
    Guid? EvidenceAttachmentPublicId = null) : IRequest<Result<CreditLimitDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangePrepaymentPreferenceCommandValidator : AbstractValidator<ChangePrepaymentPreferenceCommand>
{
    public ChangePrepaymentPreferenceCommandValidator()
    {
        RuleFor(x => x.CreditLimitPublicId).NotEmpty();
        RuleFor(x => x.Choice).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ChangePrepaymentPreferenceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ChangePrepaymentPreferenceCommand, Result<CreditLimitDto>>
{
    public async Task<Result<CreditLimitDto>> Handle(ChangePrepaymentPreferenceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CreditLimitDto>(ErroresDeGobierno.SinUsuario());
        var leido = await CupoEnAlcance.LeerAsync(db, alcance, auditoria, request.CreditLimitPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditLimitDto>(leido.Error);
        var cupo = leido.Value;
        if (cupo.Kind != CreditLimitKind.Revolving) return Result.Failure<CreditLimitDto>(ErroresDeDecisionesYCupos.CupoNoRotativo(cupo.Kind));

        var todas = (await VersionesDeCupo.DeAsync(db, [cupo.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, cupo)).ToList();
        var ids = todas.Select(v => v.Id).ToArray();
        var numero = await db.PrepaymentPreferences.AsNoTracking().Where(p => p.CreditLimitId != null && ids.Contains(p.CreditLimitId.Value))
            .MaxAsync(p => (int?)p.VersionNumber, ct) ?? 0;
        db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(cupo.PersonId, null, cupo.Id, request.Choice, numero + 1, reloj.UtcNow,
            PreferenceOrigin.DebtorRequest, usuario, evidencia: request.EvidenceAttachmentPublicId));
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeCupos.DtoAsync(db, cupo, reloj.HoyLocal, ct));
    }
}
