using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Registra el consentimiento de un codeudor, avalista o propietario de una garantía a una novedad (feature 013, K3, T0880; api.md §11.1,
/// <c>POST /api/lending/novelties/{id}/consents</c>, <c>Lending.Novelties.Request</c>, con clave; FR-092, US11-2): la persona tiene que ser una de las
/// partes que la novedad pidió consentir (<c>Lending.Novelty.PartyUnknown</c>); con <paramref name="Consented"/> la aceptación va con su documento
/// firmado —un adjunto <c>LendingNoveltyConsent</c> de la novedad, ya confirmado— y sin él se rechaza (<c>Lending.Novelty.ConsentAttachmentRequired</c>);
/// sin consentir, el adjunto es la constancia si la hay. El otrosí o el pagaré nuevo (<paramref name="AddendumAttachmentPublicId"/>) queda en la
/// auditoría del comando. Sólo mientras la novedad no esté decidida: después, quien aplica resuelve cada rechazo o pendiente con motivo. (nuevo)
/// </summary>
public sealed record RegisterNoveltyConsentCommand(
    Guid NoveltyPublicId,
    Guid PartyPersonPublicId,
    bool Consented,
    Guid? AttachmentPublicId = null,
    Guid? AddendumAttachmentPublicId = null,
    Guid? CollateralPublicId = null) : IRequest<Result<NoveltyConsentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterNoveltyConsentCommandValidator : AbstractValidator<RegisterNoveltyConsentCommand>
{
    public RegisterNoveltyConsentCommandValidator()
    {
        RuleFor(x => x.NoveltyPublicId).NotEmpty();
        RuleFor(x => x.PartyPersonPublicId).NotEmpty();
    }
}

public sealed class RegisterNoveltyConsentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterNoveltyConsentCommand, Result<NoveltyConsentDto>>
{
    public async Task<Result<NoveltyConsentDto>> Handle(RegisterNoveltyConsentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var n = leida.Value.Novedad;
        if (AdjuntosDeModulo.NovedadDecidida(n.Status)) return Falla(AdjuntosDeModulo.NovedadBloqueada());

        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PartyPersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        int? garantia = request.CollateralPublicId is { } g
            ? await db.Collaterals.AsNoTracking().Where(c => c.PublicId == g).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct)
            : null;
        var consentimientos = persona is { } id
            ? await db.LoanNoveltyConsents.Where(c => c.LoanNoveltyId == n.Id && c.PersonId == id && !c.IsDeleted).ToListAsync(ct)
            : [];
        var consentimiento = request.CollateralPublicId is null
            ? consentimientos.FirstOrDefault(c => c.Status == ConsentStatus.Pending && c.CollateralId is null) ?? consentimientos.FirstOrDefault(c => c.Status == ConsentStatus.Pending)
            : consentimientos.FirstOrDefault(c => c.CollateralId == garantia);
        if (consentimiento is null) return Falla(ErroresDeNovedades.ParteDesconocida());

        if (request.AttachmentPublicId is { } adjunto)
        {
            var valido = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == adjunto && a.OwnerEntityType == AdjuntosDeModulo.ConsentimientoDeNovedad
                && a.OwnerEntityPublicId == n.PublicId && a.Status == EstadoDeAdjunto.Available, ct);
            if (!valido)
                return Falla(new ErrorConDatos(Domain.Entities.Lending.Operations.LoanNoveltyConsent.CodigoSinAdjunto,
                    "El documento no está entre los soportes de consentimiento de esta novedad: súbalo a la novedad y vuelva a registrarlo.",
                    new { attachmentPublicId = adjunto }));
        }
        try
        {
            if (request.Consented) consentimiento.Aceptar(request.AttachmentPublicId ?? Guid.Empty, usuario, reloj.UtcNow);
            else consentimiento.Rehusar(usuario, reloj.UtcNow, request.AttachmentPublicId);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        var nombre = (await ReferenciasDeGobierno.PersonasAsync(db, [consentimiento.PersonId], ct))[consentimiento.PersonId];
        return Result.Success(new NoveltyConsentDto(nombre, consentimiento.PartyRole, consentimiento.Status, request.CollateralPublicId,
            consentimiento.SignedAttachmentPublicId, consentimiento.RecordedAt, consentimiento.RefusalResolution, consentimiento.ResolutionReason));
    }

    private static Result<NoveltyConsentDto> Falla(Error e) => Result.Failure<NoveltyConsentDto>(e);
}
