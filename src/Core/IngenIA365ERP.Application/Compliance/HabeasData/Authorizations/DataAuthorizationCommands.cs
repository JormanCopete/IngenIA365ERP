using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData.Authorizations;

/// <summary>Una versión de la política en una autorización (contracts/api.md §22.2). (nuevo)</summary>
public sealed record PolicyVersionRefDto(Guid PublicId, int Version);

/// <summary>
/// Una autorización del titular por finalidad (feature 013, T69 a, T0086; contracts/api.md §22.2): una decisión <c>Accepted</c> o
/// <c>Declined</c> con su estado: <c>Active</c>, <c>Declined</c>, <c>Revoked</c> (con la fecha de la revocación) o <c>Replaced</c>
/// (hay una decisión posterior de la misma finalidad y canal). (nuevo)
/// </summary>
public sealed record DataAuthorizationDto(
    Guid PublicId,
    DataAuthorizationPurpose Purpose,
    DataAuthorizationChannel? ContactChannel,
    PolicyVersionRefDto PolicyVersion,
    DataAuthorizationOrigin Origin,
    DateTime GrantedAt,
    Guid? Evidence,
    DateTime? RevokedAt,
    string Status);

/// <summary>Los estados de <see cref="DataAuthorizationDto.Status"/>. (nuevo)</summary>
public static class EstadosDeAutorizacion
{
    public const string Active = "Active";
    public const string Declined = "Declined";
    public const string Revoked = "Revoked";
    public const string Replaced = "Replaced";
}

/// <summary>
/// Las autorizaciones de un titular, por <c>PublicId</c> (feature 013, T0086; <c>GET /api/compliance/habeas-data/authorizations?personPublicId=</c>,
/// <c>Compliance.HabeasData.ViewHistory</c>). Reemplaza a <c>consents/history/{personId:int}</c>, que queda en desuso (Principio VI). (nuevo)
/// </summary>
public sealed record ListDataAuthorizationsQuery(Guid PersonPublicId) : IRequest<Result<IReadOnlyList<DataAuthorizationDto>>>;

public sealed class ListDataAuthorizationsQueryValidator : AbstractValidator<ListDataAuthorizationsQuery>
{
    public ListDataAuthorizationsQueryValidator() => RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique el titular (personPublicId).");
}

public sealed class ListDataAuthorizationsQueryHandler(IApplicationDbContext db, ICurrentUserService usuario)
    : IRequestHandler<ListDataAuthorizationsQuery, Result<IReadOnlyList<DataAuthorizationDto>>>
{
    public async Task<Result<IReadOnlyList<DataAuthorizationDto>>> Handle(ListDataAuthorizationsQuery request, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<IReadOnlyList<DataAuthorizationDto>>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");
        var personaId = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (personaId is null)
            return Result.Failure<IReadOnlyList<DataAuthorizationDto>>(new Error("Core.Person.NotFound", "La persona no existe en esta cooperativa."));

        var filas = await db.HabeasDataConsents.AsNoTracking()
            .Where(c => c.TenantId == cooperativa && c.PersonId == personaId)
            .OrderBy(c => c.ActionAt).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.PublicId, c.Action, c.ActionAt, c.Purpose, c.ContactChannel, c.Origin, c.EvidenceAttachmentPublicId,
                Version = c.PolicyVersion!.PublicId, Numero = c.PolicyVersion.VersionNumber,
            })
            .ToListAsync(ct);

        var lista = new List<DataAuthorizationDto>();
        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            if (f.Action == AccionesDeConsentimiento.Revoked) continue;
            var siguiente = filas.Skip(i + 1).FirstOrDefault(o => o.Purpose == f.Purpose && o.ContactChannel == f.ContactChannel);
            var (estado, revocada) = f.Action == AccionesDeConsentimiento.Declined
                ? (EstadosDeAutorizacion.Declined, (DateTime?)null)
                : siguiente is null
                    ? (EstadosDeAutorizacion.Active, null)
                    : siguiente.Action == AccionesDeConsentimiento.Revoked
                        ? (EstadosDeAutorizacion.Revoked, siguiente.ActionAt)
                        : (EstadosDeAutorizacion.Replaced, null);
            lista.Add(new DataAuthorizationDto(f.PublicId, f.Purpose, f.ContactChannel, new PolicyVersionRefDto(f.Version, f.Numero), f.Origin,
                f.ActionAt, f.EvidenceAttachmentPublicId, revocada, estado));
        }

        lista.Reverse();
        return Result.Success<IReadOnlyList<DataAuthorizationDto>>(lista);
    }
}

/// <summary>
/// Registra la autorización del titular para una finalidad (feature 013, T69 a, T0086; <c>POST /api/compliance/habeas-data/authorizations</c>,
/// <c>Compliance.HabeasData.RecordConsent</c>; contracts/api.md §22.2). La escribe <see cref="IAutorizacionDeDatos.AgregarAsync"/> y se
/// guarda aquí, en un solo <c>SaveChanges</c>. Sin versión de política vigente para la finalidad, <c>Compliance.HabeasData.NoCurrentPolicy</c>.
/// La evidencia es un adjunto de dueño <c>DataAuthorization</c> sobre la persona (<c>AdjuntosDeModulo</c>, T81), disponible; sólo una
/// autorización migrada puede ir sin ella (FR-006). (nuevo)
/// </summary>
public sealed record RecordDataAuthorizationCommand(
    Guid PersonPublicId,
    DataAuthorizationPurpose Purpose,
    DataAuthorizationChannel? ContactChannel,
    Guid? PolicyVersionPublicId,
    Guid? EvidenceAttachmentPublicId,
    DataAuthorizationOrigin Origin,
    DateTime GrantedAt,
    DecisionDeAutorizacion Decision = DecisionDeAutorizacion.Accepted,
    string? Notes = null)
    : IRequest<Result<Guid>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordDataAuthorizationCommandValidator : AbstractValidator<RecordDataAuthorizationCommand>
{
    public RecordDataAuthorizationCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique el titular.");
        RuleFor(x => x.Purpose).IsInEnum().WithMessage("La finalidad es GeneralTreatment, CreditProcessing, CreditBureauQuery, CreditBureauReport o ContactChannel.");
        RuleFor(x => x.ContactChannel).NotNull().When(x => x.Purpose == DataAuthorizationPurpose.ContactChannel)
            .WithMessage("Indique el canal de contacto que se autoriza (Sms, WhatsApp, Email, Call o Visit).");
        RuleFor(x => x.ContactChannel).Null().When(x => x.Purpose != DataAuthorizationPurpose.ContactChannel)
            .WithMessage("Sólo la finalidad ContactChannel lleva canal.");
        RuleFor(x => x.ContactChannel).IsInEnum().When(x => x.ContactChannel is not null);
        RuleFor(x => x.Origin).IsInEnum();
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.GrantedAt).NotEqual(default(DateTime)).WithMessage("Indique cuándo autorizó el titular.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class RecordDataAuthorizationCommandHandler(IApplicationDbContext db, IAutorizacionDeDatos autorizaciones, IDateTimeService reloj)
    : IRequestHandler<RecordDataAuthorizationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RecordDataAuthorizationCommand request, CancellationToken ct)
    {
        if (request.GrantedAt > reloj.UtcNow.AddMinutes(5))
            return Result.Failure<Guid>(new Error(Error.Validation.Code, "La autorización no puede tener fecha futura."));

        if (request.EvidenceAttachmentPublicId is { } evidencia)
        {
            var valida = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == evidencia
                && a.OwnerEntityType == AdjuntosDeModulo.AutorizacionDeDatos
                && a.OwnerEntityPublicId == request.PersonPublicId
                && a.Status == EstadoDeAdjunto.Available, ct);
            if (!valida)
                return Result.Failure<Guid>(new Error(HabeasDataErrorCodes.EvidenceNotFound,
                    "La evidencia no existe, no es de este titular o no terminó de subirse: súbala como soporte de autorización de datos de la persona."));
        }
        else if (request.Origin != DataAuthorizationOrigin.Migration)
        {
            return Result.Failure<Guid>(new Error(HabeasDataErrorCodes.EvidenceRequired,
                "Adjunte la evidencia de la autorización (el formato firmado o la grabación): sólo una autorización migrada puede registrarse sin ella."));
        }

        var agregada = await autorizaciones.AgregarAsync(new AutorizacionAAgregar(
            request.PersonPublicId, request.Purpose, request.Decision, request.PolicyVersionPublicId, request.ContactChannel,
            request.EvidenceAttachmentPublicId, request.Origin, request.GrantedAt, request.Notes), ct);
        if (agregada.IsFailure) return agregada;

        await db.SaveChangesAsync(ct);
        return agregada;
    }
}
