using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
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
    DateTime EffectiveFrom,
    DataAuthorizationPurpose Purpose = DataAuthorizationPurpose.GeneralTreatment) : IRequest<Result<Guid>>;

public sealed class PublishPolicyVersionCommandValidator
    : AbstractValidator<PublishPolicyVersionCommand>
{
    public PublishPolicyVersionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContentMarkdown).NotEmpty();
        RuleFor(x => x.Purpose).IsInEnum();
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
        var newVersion = await PublicacionDePolitica.AgregarAsync(_db, tenantId, actor, request.Purpose, request.Title, request.ContentMarkdown,
            request.EffectiveFrom, ct);

        await _db.SaveChangesAsync(ct);
        return Result.Success(newVersion.PublicId);
    }
}

/// <summary>
/// La publicación de una versión de la política para una finalidad, sin guardar: cierra la vigente de esa finalidad (su <c>EffectiveTo</c> = la nueva
/// <c>EffectiveFrom</c>), numera por finalidad y calcula el SHA-256 del contenido. La usan la publicación de una finalidad y la de varias. (nuevo)
/// </summary>
public static class PublicacionDePolitica
{
    public static async Task<HabeasDataPolicyVersion> AgregarAsync(IApplicationDbContext db, int tenantId, string actor, DataAuthorizationPurpose finalidad,
        string titulo, string contenido, DateTime vigenteDesde, CancellationToken ct)
    {
        var currentVersion = await db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId && p.Purpose == finalidad && p.EffectiveTo == null)
            .FirstOrDefaultAsync(ct);

        if (currentVersion is not null)
        {
            currentVersion.EffectiveTo = vigenteDesde;
            currentVersion.UpdatedBy = actor;
        }

        var maxNumber = await db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId && p.Purpose == finalidad)
            .Select(p => (int?)p.VersionNumber)
            .MaxAsync(ct) ?? 0;

        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))).ToLowerInvariant();

        var newVersion = new HabeasDataPolicyVersion
        {
            TenantId = tenantId,
            VersionNumber = maxNumber + 1,
            Title = titulo,
            ContentMarkdown = contenido,
            Sha256Hex = sha256,
            EffectiveFrom = vigenteDesde,
            EffectiveTo = null,
            PublishedBy = actor,
            Purpose = finalidad,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        db.HabeasDataPolicyVersions.Add(newVersion);
        return newVersion;
    }
}

/// <summary>Una versión publicada para una finalidad. (nuevo)</summary>
public sealed record PolicyPublishedDto(DataAuthorizationPurpose Purpose, Guid PublicId, int VersionNumber);

/// <summary>
/// 2026-11-24 (pedido del dueño; <c>POST /api/compliance/habeas-data/policies/several</c>, <c>Compliance.HabeasData.Publish</c>): publica <b>la misma
/// política</b> para varias finalidades en un solo guardado, una versión por finalidad (cada una con su número). Cada autorización se registra contra la
/// política de su finalidad; sin ella, <c>Compliance.HabeasData.NoCurrentPolicy</c>. Una cooperativa con un solo texto que lo cubre todo la publica así
/// para todas de una vez. (nuevo)
/// </summary>
public sealed record PublishPolicyVersionsCommand(
    string Title,
    string ContentMarkdown,
    DateTime EffectiveFrom,
    IReadOnlyList<DataAuthorizationPurpose> Purposes) : IRequest<Result<IReadOnlyList<PolicyPublishedDto>>>;

public sealed class PublishPolicyVersionsCommandValidator : AbstractValidator<PublishPolicyVersionsCommand>
{
    public PublishPolicyVersionsCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContentMarkdown).NotEmpty();
        RuleFor(x => x.Purposes).NotEmpty().WithMessage("Elija al menos una finalidad.");
        RuleForEach(x => x.Purposes).IsInEnum();
        RuleFor(x => x.Purposes).Must(p => p is null || p.Distinct().Count() == p.Count).WithMessage("Cada finalidad va una sola vez.");
        RuleFor(x => x.EffectiveFrom)
            .Must(d => d >= DateTime.UtcNow.AddMinutes(-5))
                .WithMessage("La fecha de vigencia no puede ser pasada.")
                .WithErrorCode(HabeasDataErrorCodes.Validation_EffectiveFromInPast);
    }
}

public sealed class PublishPolicyVersionsCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<PublishPolicyVersionsCommand, Result<IReadOnlyList<PolicyPublishedDto>>>
{
    public async Task<Result<IReadOnlyList<PolicyPublishedDto>>> Handle(PublishPolicyVersionsCommand request, CancellationToken ct)
    {
        if (!int.TryParse(currentUser.TenantId, out var tenantId))
            return Result.Failure<IReadOnlyList<PolicyPublishedDto>>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var actor = currentUser.UserName ?? "SYSTEM";
        var publicadas = new List<HabeasDataPolicyVersion>();
        foreach (var finalidad in request.Purposes.Distinct().Order())
            publicadas.Add(await PublicacionDePolitica.AgregarAsync(db, tenantId, actor, finalidad, request.Title, request.ContentMarkdown,
                request.EffectiveFrom, ct));

        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<PolicyPublishedDto>>(
            publicadas.Select(p => new PolicyPublishedDto(p.Purpose, p.PublicId, p.VersionNumber)).ToList());
    }
}
