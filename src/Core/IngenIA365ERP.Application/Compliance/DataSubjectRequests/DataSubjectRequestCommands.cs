using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.DataSubjectRequests;

/// <summary>Los errores de las solicitudes del titular (contracts/api.md §22.3; data-model parte 3 §10.2). (nuevo)</summary>
public static class ErroresDeSolicitudesDelTitular
{
    public const string CodigoNoEncontrada = "Compliance.DataSubjectRequest.NotFound";
    public const string CodigoYaProrrogada = "Compliance.DataSubjectRequest.AlreadyExtended";
    public const string CodigoNoAbierta = "Compliance.DataSubjectRequest.NotOpen";

    public static readonly Error NoEncontrada = new(CodigoNoEncontrada, "La solicitud del titular no existe.");

    public static Error YaProrrogada(DateOnly vence) => new ErrorConDatos(CodigoYaProrrogada,
        $"La solicitud ya se prorrogó (vence el {vence:yyyy-MM-dd}): la prórroga se concede una sola vez.", new { extendedDueDate = vence });

    public static Error NoAbierta(DataSubjectRequestStatus estado) => new ErrorConDatos(CodigoNoAbierta,
        $"La solicitud está {estado}: sólo se prorroga o responde una solicitud abierta.", new { status = estado.ToString() });

    public static readonly Error PersonaNoEncontrada = new("Core.Person.NotFound", "La persona no existe en esta cooperativa.");

    public static readonly Error SinPersonaQueRegistre = new("Auth.UserRequired", "Una solicitud del titular la registra o responde una persona, no un proceso.");
}

/// <summary>Los medios de recibo admitidos (data-model parte 3 §4.9). (nuevo)</summary>
public static class MediosDeRecibo
{
    public static readonly IReadOnlyList<string> Todos = ["Presencial", "Correo", "Escrito", "Telefono"];
}

/// <summary>
/// Los plazos del titular a la fecha del recibo: los cuatro límites <c>TITULAR_*</c> de <see cref="IValoresNacionales"/> y el calendario de
/// días hábiles nacionales (sábado y domingo no son hábiles, <see cref="DiasDeDescanso.SabadoYDomingo"/>). Ningún plazo está en el código.
/// </summary>
internal static class LecturaDePlazos
{
    /// <summary>Lo que cubre el calendario cargado: vencimiento más prórroga de un reclamo, con holgura.</summary>
    private const int DiasDeCalendario = 120;

    public static async Task<Result<(LimitesDelTitular Limites, Domain.Common.Calendar.CalendarioHabil Calendario)>> LeerAsync(
        IValoresNacionales valores, ICalendarioHabil calendario, DateOnly desde, CancellationToken ct)
    {
        var dias = new List<int>();
        foreach (var clave in new[]
                 {
                     CatalogoDeLimitesLegales.TitularConsultaDias, CatalogoDeLimitesLegales.TitularConsultaProrrogaDias,
                     CatalogoDeLimitesLegales.TitularReclamoDias, CatalogoDeLimitesLegales.TitularReclamoProrrogaDias,
                 })
        {
            var leido = await valores.LeerAsync(CodigosDeValorNacional.Limite, clave, desde, null, ct);
            if (leido.IsFailure) return Result.Failure<(LimitesDelTitular, Domain.Common.Calendar.CalendarioHabil)>(leido.Error);
            dias.Add((int)(leido.Value.Valor ?? 0m));
        }
        var cal = await calendario.ObtenerAsync(desde, desde.AddDays(DiasDeCalendario), DiasDeDescanso.SabadoYDomingo, null, ct);
        return Result.Success((new LimitesDelTitular(dias[0], dias[1], dias[2], dias[3]), cal));
    }
}

// ------------------------------------------------------------------------------------------------------------------- radicar --

/// <summary>
/// Radica una solicitud del titular (feature 013, T69 b, T0088; <c>POST /api/compliance/data-subject-requests</c>,
/// <c>Compliance.DataSubjectRequests.Register</c>). El vencimiento sale de <see cref="PlazosDelTitular"/> en días hábiles nacionales desde
/// el día local del recibo: la consulta con el plazo de consulta y las demás con el de reclamo. Revocar y suprimir dicen la finalidad;
/// <c>ContactChannel</c> dice además el canal. Los soportes son adjuntos <c>DataSubjectRequest</c> de la persona; el primero queda como
/// evidencia. (nuevo)
/// </summary>
public sealed record RegisterDataSubjectRequestCommand(
    Guid PersonPublicId,
    DataSubjectRequestKind Kind,
    DataAuthorizationPurpose? Purpose,
    DataAuthorizationChannel? ContactChannel,
    string Channel,
    DateTime ReceivedAt,
    string Description,
    IReadOnlyList<Guid>? AttachmentPublicIds = null)
    : IRequest<Result<DataSubjectRequestDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDataSubjectRequestCommandValidator : AbstractValidator<RegisterDataSubjectRequestCommand>
{
    public RegisterDataSubjectRequestCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique el titular.");
        RuleFor(x => x.Kind).IsInEnum().WithMessage("La clase es Query, Update, Rectification, Suppression o Revocation.");
        RuleFor(x => x.Purpose).NotNull().When(x => x.Kind is DataSubjectRequestKind.Revocation or DataSubjectRequestKind.Suppression)
            .WithMessage("Una revocación o supresión dice sobre qué finalidad recae.");
        RuleFor(x => x.Purpose).IsInEnum().When(x => x.Purpose is not null);
        RuleFor(x => x.ContactChannel).NotNull().When(x => x.Purpose == DataAuthorizationPurpose.ContactChannel)
            .WithMessage("Indique el canal de contacto que se revoca (Sms, WhatsApp, Email, Call o Visit).");
        RuleFor(x => x.ContactChannel).Null().When(x => x.Purpose != DataAuthorizationPurpose.ContactChannel)
            .WithMessage("Sólo la finalidad ContactChannel lleva canal.");
        RuleFor(x => x.Channel).Must(c => MediosDeRecibo.Todos.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"El medio de recibo es {string.Join(", ", MediosDeRecibo.Todos)}.");
        RuleFor(x => x.ReceivedAt).NotEqual(default(DateTime)).WithMessage("Indique cuándo se recibió.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Escriba lo que pide el titular.").MaximumLength(2000);
    }
}

public sealed class RegisterDataSubjectRequestCommandHandler(
    IApplicationDbContext db,
    IValoresNacionales valores,
    ICalendarioHabil calendario,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<RegisterDataSubjectRequestCommand, Result<DataSubjectRequestDto>>
{
    public async Task<Result<DataSubjectRequestDto>> Handle(RegisterDataSubjectRequestCommand request, CancellationToken ct)
    {
        if (request.ReceivedAt > reloj.UtcNow.AddMinutes(5))
            return Result.Failure<DataSubjectRequestDto>(new Error(Error.Validation.Code, "La solicitud no puede tener fecha de recibo futura."));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.PersonaNoEncontrada);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.SinPersonaQueRegistre);

        var recibidaEl = DateOnly.FromDateTime(DateTime.SpecifyKind(request.ReceivedAt, DateTimeKind.Utc).Add(IDateTimeService.DesfaseColombia));
        var plazos = await LecturaDePlazos.LeerAsync(valores, calendario, recibidaEl, ct);
        if (plazos.IsFailure) return Result.Failure<DataSubjectRequestDto>(plazos.Error);

        var solicitud = new DataSubjectRequest
        {
            PersonId = persona.Value,
            Kind = request.Kind,
            Purpose = request.Purpose,
            ContactChannel = request.ContactChannel,
            ReceivedVia = MediosDeRecibo.Todos.First(m => string.Equals(m, request.Channel, StringComparison.OrdinalIgnoreCase)),
            ReceivedAt = request.ReceivedAt,
            ReceivedOn = recibidaEl,
            Description = request.Description.Trim(),
            EvidenceAttachmentPublicId = request.AttachmentPublicIds?.FirstOrDefault(),
            DueDate = PlazosDelTitular.Vencimiento(request.Kind, recibidaEl, plazos.Value.Limites, plazos.Value.Calendario),
            Status = DataSubjectRequestStatus.Received,
            RegisteredByUserId = usuario,
            RegisteredByName = Recortar(actor.Name, 150),
        };
        db.DataSubjectRequests.Add(solicitud);
        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeSolicitudes.ADto(solicitud, request.PersonPublicId));
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}

// ------------------------------------------------------------------------------------------------------------------- prorrogar --

/// <summary>
/// Prorroga una solicitud abierta, una sola vez (<c>POST /{id}/extend</c>, <c>Compliance.DataSubjectRequests.Respond</c>): suma los días
/// hábiles de prórroga de su clase al vencimiento, con motivo. Ya prorrogada: <c>Compliance.DataSubjectRequest.AlreadyExtended</c>. (nuevo)
/// </summary>
public sealed record ExtendDataSubjectRequestCommand(Guid RequestPublicId, string Reason)
    : IRequest<Result<DataSubjectRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ExtendDataSubjectRequestCommandValidator : ValidadorConMotivo<ExtendDataSubjectRequestCommand>
{
    public ExtendDataSubjectRequestCommandValidator() => RuleFor(x => x.RequestPublicId).NotEmpty();
}

public sealed class ExtendDataSubjectRequestCommandHandler(IApplicationDbContext db, IValoresNacionales valores, ICalendarioHabil calendario)
    : IRequestHandler<ExtendDataSubjectRequestCommand, Result<DataSubjectRequestDto>>
{
    public async Task<Result<DataSubjectRequestDto>> Handle(ExtendDataSubjectRequestCommand request, CancellationToken ct)
    {
        var solicitud = await db.DataSubjectRequests.FirstOrDefaultAsync(s => s.PublicId == request.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.NoEncontrada);
        if (solicitud.ExtendedDueDate is { } yaVence) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.YaProrrogada(yaVence));
        if (!solicitud.Abierta) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.NoAbierta(solicitud.Status));

        // Los límites que regían al recibirla; el calendario cubre desde su vencimiento.
        var plazos = await LecturaDePlazos.LeerAsync(valores, calendario, solicitud.ReceivedOn, ct);
        if (plazos.IsFailure) return Result.Failure<DataSubjectRequestDto>(plazos.Error);
        var cal = await calendario.ObtenerAsync(solicitud.ReceivedOn, solicitud.DueDate.AddDays(120), DiasDeDescanso.SabadoYDomingo, null, ct);

        solicitud.ExtendedDueDate = PlazosDelTitular.Prorroga(solicitud.Kind, solicitud.DueDate, yaProrrogada: false, plazos.Value.Limites, cal);
        solicitud.ExtensionReason = request.Reason.Trim().Length > 500 ? request.Reason.Trim()[..500] : request.Reason.Trim();
        solicitud.Status = DataSubjectRequestStatus.Extended;
        await db.SaveChangesAsync(ct);

        var persona = await db.People.AsNoTracking().Where(p => p.Id == solicitud.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        return Result.Success(ProyeccionDeSolicitudes.ADto(solicitud, persona));
    }
}

// ------------------------------------------------------------------------------------------------------------------- responder --

/// <summary>
/// Responde una solicitud abierta (<c>POST /{id}/respond</c>, <c>Compliance.DataSubjectRequests.Respond</c>; contracts/api.md §22.3). Una
/// revocación o supresión aplica <see cref="ReglaDeRevocacion"/> sobre su finalidad: <c>ContactChannel</c> procede siempre; una finalidad de
/// crédito consulta el deber por <see cref="IDeberDeConservacionDelTitular"/> y, si lo hay, la respuesta es <c>Denied</c> con la constancia
/// en <c>DutyBasis</c> y la consulta sellada en <c>DutyEvidenceJson</c>, cualquiera que fuera el desenlace pedido. Si procede y se concede,
/// la fila <c>Revoked</c> de <c>CMP_HabeasDataConsents</c> (<c>Origin = TitularRequest</c>) entra en el mismo <c>SaveChanges</c>. Cerrada:
/// <c>Compliance.DataSubjectRequest.NotOpen</c>. (nuevo)
/// </summary>
public sealed record RespondDataSubjectRequestCommand(Guid RequestPublicId, DataSubjectRequestOutcome Outcome, string Answer, Guid? AttachmentPublicId = null)
    : IRequest<Result<DataSubjectRequestDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RespondDataSubjectRequestCommandValidator : AbstractValidator<RespondDataSubjectRequestCommand>
{
    public RespondDataSubjectRequestCommandValidator()
    {
        RuleFor(x => x.RequestPublicId).NotEmpty();
        RuleFor(x => x.Outcome).IsInEnum().WithMessage("El desenlace es Granted, PartiallyGranted o Denied.");
        RuleFor(x => x.Answer).NotEmpty().WithMessage("Escriba la respuesta al titular.").MaximumLength(4000);
    }
}

public sealed class RespondDataSubjectRequestCommandHandler(
    IApplicationDbContext db,
    IDeberDeConservacionDelTitular deber,
    IValoresNacionales valores,
    AutorizacionDeDatos autorizaciones,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<RespondDataSubjectRequestCommand, Result<DataSubjectRequestDto>>
{
    // La constancia se guarda legible (tildes y eñes sin escapar): no se muestra como HTML.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<Result<DataSubjectRequestDto>> Handle(RespondDataSubjectRequestCommand request, CancellationToken ct)
    {
        var solicitud = await db.DataSubjectRequests.FirstOrDefaultAsync(s => s.PublicId == request.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.NoEncontrada);
        if (!solicitud.Abierta) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.NoAbierta(solicitud.Status));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DataSubjectRequestDto>(ErroresDeSolicitudesDelTitular.SinPersonaQueRegistre);
        var persona = await db.People.AsNoTracking().Where(p => p.Id == solicitud.PersonId).Select(p => p.PublicId).FirstAsync(ct);

        var ahora = reloj.UtcNow;
        var hoy = reloj.HoyLocal;
        var desenlace = request.Outcome;
        if (solicitud.Kind is DataSubjectRequestKind.Revocation or DataSubjectRequestKind.Suppression && solicitud.Purpose is { } finalidad)
        {
            var evaluada = await EvaluarAsync(persona, finalidad, hoy, ct);
            if (evaluada.IsFailure) return Result.Failure<DataSubjectRequestDto>(evaluada.Error);
            var (decision, consulta) = evaluada.Value;
            solicitud.DutyEvidenceJson = JsonSerializer.Serialize(new
            {
                purpose = finalidad.ToString(),
                consulta?.Fuente,
                consultadoEl = consulta?.ConsultadoEl,
                migradaSinActivar = consulta?.MigradaSinActivar,
                obligaciones = consulta?.Obligaciones,
                procede = decision.Procede,
                deberes = decision.Deberes,
                constancia = decision.Constancia,
            }, Json);
            if (!decision.Procede)
            {
                desenlace = DataSubjectRequestOutcome.Denied;
                solicitud.DutyBasis = Recortar(decision.Constancia, 1000);
            }
            else if (desenlace != DataSubjectRequestOutcome.Denied)
            {
                await autorizaciones.AgregarRevocacionAsync(solicitud.PersonId, finalidad, solicitud.ContactChannel, ahora,
                    $"Revocada por la solicitud del titular {solicitud.PublicId}.", ct);
            }
        }

        solicitud.Outcome = desenlace;
        solicitud.ResponseText = request.Answer.Trim();
        solicitud.ResponseAttachmentPublicId = request.AttachmentPublicId;
        solicitud.RespondedAt = ahora;
        solicitud.RespondedByUserId = usuario;
        solicitud.RespondedByName = Recortar(actor.Name, 150);
        solicitud.Status = DataSubjectRequestStatus.Answered;
        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeSolicitudes.ADto(solicitud, persona));
    }

    /// <summary>La regla de revocación con el deber consultado (no se consulta para un canal de contacto, que procede siempre).</summary>
    private async Task<Result<(DecisionDeRevocacion Decision, DeberDelTitular? Consulta)>> EvaluarAsync(
        Guid persona, DataAuthorizationPurpose finalidad, DateOnly hoy, CancellationToken ct)
    {
        if (finalidad == DataAuthorizationPurpose.ContactChannel)
            return Result.Success<(DecisionDeRevocacion, DeberDelTitular?)>(
                (ReglaDeRevocacion.Evaluar(finalidad, hoy, false, [], new LimitesDePermanencia(0m, 0)), null));

        var consulta = await deber.ConsultarAsync(persona, hoy, ct);
        var permanencia = new LimitesDePermanencia(0m, 0);
        if (finalidad == DataAuthorizationPurpose.CreditBureauReport && consulta.Obligaciones.Any(o => !o.IsLive && o.DiasDeMoraDelDatoNegativo > 0))
        {
            var veces = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PermanenciaNegativoVecesMora, hoy, null, ct);
            if (veces.IsFailure) return Result.Failure<(DecisionDeRevocacion, DeberDelTitular?)>(veces.Error);
            var meses = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PermanenciaNegativoMesesMaximos, hoy, null, ct);
            if (meses.IsFailure) return Result.Failure<(DecisionDeRevocacion, DeberDelTitular?)>(meses.Error);
            permanencia = new LimitesDePermanencia(veces.Value.Valor ?? 0m, (int)(meses.Value.Valor ?? 0m));
        }
        return Result.Success<(DecisionDeRevocacion, DeberDelTitular?)>(
            (ReglaDeRevocacion.Evaluar(finalidad, hoy, consulta.MigradaSinActivar, consulta.Obligaciones, permanencia), consulta));
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}
