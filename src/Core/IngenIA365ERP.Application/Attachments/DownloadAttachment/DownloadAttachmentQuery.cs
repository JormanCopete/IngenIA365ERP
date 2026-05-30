using System.Security.Cryptography;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Attachments.DownloadAttachment;

/// <summary>
/// T108 — Descarga + descifrado de un adjunto (FR-030).
///
/// Verificación de integridad doble:
/// <list type="bullet">
///   <item>GCM tag → garantiza que el blob no fue alterado (excepción CryptographicException).</item>
///   <item>SHA-256 recalculado tras descifrar vs <c>Attachment.Sha256Hex</c> → defensa
///         adicional contra corrupción accidental (bit flip de disco, etc.).</item>
/// </list>
///
/// El query scoped al tenant del usuario actual — un adjunto de otro tenant
/// devuelve <c>Generic.NotFound</c> (404 indistinguible).
/// </summary>
public sealed record DownloadAttachmentQuery(Guid AttachmentPublicId)
    : IRequest<Result<AttachmentDownload>>;

public sealed class DownloadAttachmentQueryValidator
    : AbstractValidator<DownloadAttachmentQuery>
{
    public DownloadAttachmentQueryValidator()
    {
        RuleFor(x => x.AttachmentPublicId).NotEmpty();
    }
}

public sealed class DownloadAttachmentQueryHandler
    : IRequestHandler<DownloadAttachmentQuery, Result<AttachmentDownload>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBlobStore _store;
    private readonly IAttachmentCipher _cipher;
    private readonly ICurrentUserService _currentUser;

    public DownloadAttachmentQueryHandler(
        IApplicationDbContext db,
        IBlobStore store,
        IAttachmentCipher cipher,
        ICurrentUserService currentUser)
    {
        _db = db;
        _store = store;
        _cipher = cipher;
        _currentUser = currentUser;
    }

    public async Task<Result<AttachmentDownload>> Handle(
        DownloadAttachmentQuery request, CancellationToken ct)
    {
        var tenantIdStr = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantIdStr) || !int.TryParse(tenantIdStr, out var tenantInternalId))
        {
            return Result.Failure<AttachmentDownload>("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var attachment = await _db.Attachments
            .Where(a => a.TenantId == tenantInternalId && a.PublicId == request.AttachmentPublicId)
            .FirstOrDefaultAsync(ct);

        if (attachment is null)
        {
            return Result.Failure<AttachmentDownload>(
                "Generic.NotFound", "Adjunto no encontrado.");
        }

        byte[] encryptedBlob;
        try
        {
            await using var blobStream = await _store.GetAsync(new BlobReference(attachment.StoragePath), ct);
            await using var ms = new MemoryStream();
            await blobStream.CopyToAsync(ms, ct);
            encryptedBlob = ms.ToArray();
        }
        catch (FileNotFoundException)
        {
            return Result.Failure<AttachmentDownload>(
                AttachmentErrorCodes.BlobMissing,
                "El blob asociado al adjunto no se encuentra en el almacén.");
        }

        var plaintext = _cipher.Decrypt(encryptedBlob, attachment.EncryptedDek);

        var recomputedHash = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
        if (!string.Equals(recomputedHash, attachment.Sha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AttachmentDownload>(
                AttachmentErrorCodes.IntegrityFailed,
                "El hash del adjunto no coincide — el contenido pudo haberse alterado.");
        }

        return Result.Success(new AttachmentDownload(
            Content: plaintext,
            FileName: attachment.FileName,
            ContentType: attachment.ContentType,
            Sha256Hex: attachment.Sha256Hex));
    }
}
