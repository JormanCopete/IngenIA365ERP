using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData.PublishPolicyVersion;

/// <summary>
/// T128 — Publica una nueva versión de la política habeas data.
///
/// <para>Pasos:</para>
/// <list type="number">
///   <item>Resuelve <c>VersionNumber</c> = max + 1 por tenant.</item>
///   <item>Cierra la versión vigente fijando <c>EffectiveTo</c> = nueva <c>EffectiveFrom</c>.</item>
///   <item>Calcula SHA-256 sobre <c>ContentMarkdown</c> al momento del commit.</item>
/// </list>
///
/// <para>
/// El <c>UK_CMP_HabeasPolicyVersions_Current</c> a nivel BD garantiza que
/// nunca hay 2 versiones vigentes — si dos publicaciones simultáneas
/// pasaran el chequeo de aplicación, el índice las rechaza.
/// </para>
/// </summary>
public sealed record PublishPolicyVersionCommand(
    string Title,
    string ContentMarkdown,
    DateTime EffectiveFrom) : IRequest<Result<Guid>>;

public sealed class PublishPolicyVersionCommandValidator
    : AbstractValidator<PublishPolicyVersionCommand>
{
    public PublishPolicyVersionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContentMarkdown).NotEmpty();
        RuleFor(x => x.EffectiveFrom)
            .Must(d => d >= DateTime.UtcNow.AddMinutes(-5))
                .WithMessage("La fecha de vigencia no puede ser pasada.")
                .WithErrorCode(HabeasDataErrorCodes.Validation_EffectiveFromInPast);
    }
}

public sealed class PublishPolicyVersionCommandHandler
    : IRequestHandler<PublishPolicyVersionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public PublishPolicyVersionCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db; _currentUser = currentUser; _clock = clock;
    }

    public async Task<Result<Guid>> Handle(PublishPolicyVersionCommand request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<Guid>("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";

        var currentVersion = await _db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId && p.EffectiveTo == null)
            .FirstOrDefaultAsync(ct);

        if (currentVersion is not null)
        {
            currentVersion.EffectiveTo = request.EffectiveFrom;
            currentVersion.UpdatedBy = actor;
        }

        var maxNumber = await _db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.VersionNumber)
            .MaxAsync(ct) ?? 0;

        var sha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.ContentMarkdown)))
            .ToLowerInvariant();

        var newVersion = new HabeasDataPolicyVersion
        {
            TenantId = tenantId,
            VersionNumber = maxNumber + 1,
            Title = request.Title,
            ContentMarkdown = request.ContentMarkdown,
            Sha256Hex = sha256,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null,
            PublishedBy = actor,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.HabeasDataPolicyVersions.Add(newVersion);

        await _db.SaveChangesAsync(ct);
        return Result.Success(newVersion.PublicId);
    }
}
