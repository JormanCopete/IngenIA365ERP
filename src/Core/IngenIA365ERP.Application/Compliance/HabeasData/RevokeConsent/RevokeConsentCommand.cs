using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Compliance.HabeasData.Events;
using IngenIA365ERP.Domain.Entities.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData.RevokeConsent;

/// <summary>
/// T128 — Registra la revocación del titular. Requiere que la última acción
/// del titular sea <c>Accepted</c> (estado vigente). Publica
/// <see cref="HabeasDataRevokedEvent"/> tras commit exitoso para que otros
/// módulos reaccionen (cortar marketing, anonimizar derivados, etc.).
/// </summary>
public sealed record RevokeConsentCommand(
    int PersonId,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class RevokeConsentCommandValidator : AbstractValidator<RevokeConsentCommand>
{
    public RevokeConsentCommandValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class RevokeConsentCommandHandler
    : IRequestHandler<RevokeConsentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IPublisher _publisher;

    public RevokeConsentCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IPublisher publisher)
    {
        _db = db; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(RevokeConsentCommand request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<Guid>("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var lastAction = await _db.HabeasDataConsents
            .Where(c => c.TenantId == tenantId && c.PersonId == request.PersonId)
            .OrderByDescending(c => c.ActionAt)
            .Include(c => c.PolicyVersion)
            .FirstOrDefaultAsync(ct);

        if (lastAction is null)
        {
            return Result.Failure<Guid>(HabeasDataErrorCodes.NoActiveConsent,
                "El titular no tiene consentimiento registrado — no hay nada que revocar.");
        }

        if (string.Equals(lastAction.Action, "Revoked", StringComparison.Ordinal))
        {
            return Result.Failure<Guid>(HabeasDataErrorCodes.AlreadyRevoked,
                "El titular ya tiene revocado el consentimiento.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var now = _clock.UtcNow;
        var revocation = new HabeasDataConsent
        {
            TenantId = tenantId,
            PersonId = request.PersonId,
            PolicyVersionId = lastAction.PolicyVersionId,
            Action = "Revoked",
            ActionAt = now,
            ActionBy = actor,
            Channel = "Portal",
            Notes = request.Notes,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.HabeasDataConsents.Add(revocation);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new HabeasDataRevokedEvent(
            TenantId: tenantId,
            PersonId: request.PersonId,
            PolicyVersionPublicId: lastAction.PolicyVersion!.PublicId,
            RevokedAt: now,
            RevokedBy: actor,
            Notes: request.Notes), ct);

        return Result.Success(revocation.PublicId);
    }
}
