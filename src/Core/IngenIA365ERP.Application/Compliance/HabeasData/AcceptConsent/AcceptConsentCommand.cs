using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData.AcceptConsent;

/// <summary>
/// T128 — Registra el consentimiento del titular sobre la versión vigente
/// de la política. Falla si el tenant no tiene política vigente.
/// </summary>
public sealed record AcceptConsentCommand(
    int PersonId,
    string Channel,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class AcceptConsentCommandValidator : AbstractValidator<AcceptConsentCommand>
{
    public AcceptConsentCommandValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class AcceptConsentCommandHandler
    : IRequestHandler<AcceptConsentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public AcceptConsentCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db; _currentUser = currentUser; _clock = clock;
    }

    public async Task<Result<Guid>> Handle(AcceptConsentCommand request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<Guid>("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var current = await _db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId && p.EffectiveTo == null)
            .FirstOrDefaultAsync(ct);

        if (current is null)
        {
            return Result.Failure<Guid>(HabeasDataErrorCodes.NoCurrentPolicy,
                "La cooperativa no tiene una política habeas data vigente. " +
                "Publica una versión antes de registrar consentimientos.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var consent = new HabeasDataConsent
        {
            TenantId = tenantId,
            PersonId = request.PersonId,
            PolicyVersionId = current.Id,
            Action = "Accepted",
            ActionAt = _clock.UtcNow,
            ActionBy = actor,
            Channel = request.Channel,
            Notes = request.Notes,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.HabeasDataConsents.Add(consent);
        await _db.SaveChangesAsync(ct);

        return Result.Success(consent.PublicId);
    }
}
