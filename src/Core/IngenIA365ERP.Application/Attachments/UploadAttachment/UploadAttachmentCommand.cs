using System.Security.Cryptography;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Attachments.UploadAttachment;

/// <summary>
/// T108 — Guarda un archivo que <b>genera un módulo</b>: la planilla PILA, el archivo de dispersión, el
/// documento de la liquidación definitiva. Es un camino interno, sin ruta HTTP: las personas suben
/// directo al almacén con una autorización firmada (<c>SolicitarSubidaDeAdjuntoCommand</c>).
///
/// <para>
/// Feature 011 (research R11): guarda el contenido <b>tal como se generó</b>, en formato
/// <c>Direct</c> y ya <c>Available</c> —lo produjo el programa, no hay nada que validar (FR-030)—, y lo
/// cifra el bucket. Hasta el 2026-09-23 lo cifraba antes la aplicación y la descarga tenía que pasar por
/// la API para descifrarlo, con el archivo entero en memoria; ahora se baja con un enlace firmado. La
/// huella es la del contenido, y el almacén la verifica al recibirlo.
/// </para>
///
/// <para>
/// Devuelve el <c>PublicId</c> del adjunto creado. Si falla la persistencia
/// SQL después de subir el blob, el handler lo retira; si eso también falla,
/// queda huérfano. Ningún proceso lo limpia solo (feature 011, FR-001): lo
/// encuentra un inventario manual del bucket contra las bases.
/// </para>
/// </summary>
public sealed record UploadAttachmentCommand(
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string FileName,
    string ContentType,
    byte[] Content) : IRequest<Result<Guid>>;

public sealed class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    public UploadAttachmentCommandValidator(IOptions<LimitesDeAdjuntos> limites)
    {
        var maximo = limites.Value;
        RuleFor(x => x.OwnerEntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEntityPublicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content)
            .NotNull()
            .Must(c => c is { Length: > 0 })
                .WithMessage("El archivo está vacío.")
                .WithErrorCode(AttachmentErrorCodes.Validation_FileEmpty)
            .Must(c => c is null || c.Length <= maximo.MaxBytes)
                .WithMessage($"El archivo excede el tamaño máximo de {maximo.MaxBytesLegible}.")
                .WithErrorCode(AttachmentErrorCodes.Validation_FileTooLarge);
        RuleFor(x => x.ContentType)
            .Must(ct => AttachmentPolicy.AllowedMimeTypes.Contains(ct))
                .WithMessage("El tipo MIME no está permitido.")
                .WithErrorCode(AttachmentErrorCodes.Validation_MimeTypeNotAllowed);
    }
}

public sealed class UploadAttachmentCommandHandler
    : IRequestHandler<UploadAttachmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBlobStore _store;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UploadAttachmentCommandHandler> _logger;

    public UploadAttachmentCommandHandler(
        IApplicationDbContext db,
        IBlobStore store,
        ICurrentUserService currentUser,
        ILogger<UploadAttachmentCommandHandler> logger)
    {
        _db = db;
        _store = store;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UploadAttachmentCommand request, CancellationToken ct)
    {
        var tenantIdStr = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantIdStr) || !int.TryParse(tenantIdStr, out var tenantInternalId))
        {
            return Result.Failure<Guid>("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var sha256Hex = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();

        var metadata = new BlobMetadata(
            TenantId: tenantIdStr,
            OwnerEntityType: request.OwnerEntityType,
            OwnerEntityPublicId: request.OwnerEntityPublicId,
            OriginalFileName: request.FileName,
            ContentType: request.ContentType,
            SizeBytes: request.Content.LongLength,
            Sha256Hex: sha256Hex);

        BlobReference reference;
        await using (var ms = new MemoryStream(request.Content, writable: false))
        {
            reference = await _store.PutAsync(ms, metadata, ct);
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var attachment = new Attachment
        {
            TenantId = tenantInternalId,
            OwnerEntityType = request.OwnerEntityType,
            OwnerEntityPublicId = request.OwnerEntityPublicId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.Content.LongLength,
            Sha256Hex = sha256Hex,
            StoragePath = reference.Uri,
            EncryptedDek = string.Empty,
            Format = FormatoDeAdjunto.Direct,
            Status = EstadoDeAdjunto.Available,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.Attachments.Add(attachment);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception saveEx)
        {
            // El objeto ya está en el almacén pero la fila no: se retira para no dejar un archivo
            // que ninguna fila nombra.
            _logger.LogError(saveEx,
                "SaveChanges falló tras subir blob {Uri}; intentando rollback.",
                reference.Uri);
            try
            {
                await _store.DeleteAsync(reference, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                // No hay ningún proceso que lo limpie después (feature 011, FR-001: nada se borra
                // solo): queda huérfano hasta que alguien haga el inventario del bucket.
                _logger.LogError(cleanupEx,
                    "Rollback de blob huérfano {Uri} también falló. " +
                    "El objeto queda en el almacén sin fila que lo nombre; nada lo limpia solo.",
                    reference.Uri);
            }
            throw;
        }

        return Result.Success(attachment.PublicId);
    }
}
