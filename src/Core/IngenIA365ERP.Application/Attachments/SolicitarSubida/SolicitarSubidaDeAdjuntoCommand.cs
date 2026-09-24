using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Attachments.SolicitarSubida;

/// <summary>
/// Feature 011 (US1, contracts/api.md §1): una persona pide subir un archivo. El ERP registra el
/// adjunto como <c>Uploading</c> y le entrega una autorización firmada que sirve para <b>ese</b> archivo
/// —clave, tipo, tamaño exacto y huella— durante unos minutos. El archivo va del navegador al almacén
/// sin pasar por el servidor; después, <c>ConfirmarSubidaDeAdjuntoCommand</c> lo revisa.
///
/// <para>
/// Tamaño, tipo y archivo vacío los responde el <b>handler</b> con los códigos del contrato
/// (<c>Validation.Attachments.*</c>, 400, y <c>data.maxBytes</c> en el tamaño): el validador sólo mira la
/// forma, porque un fallo del validador sale siempre como <c>Validation.Invalid</c> y el código del
/// contrato no llegaría nunca al cliente.
/// </para>
/// </summary>
public sealed record SolicitarSubidaDeAdjuntoCommand(
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Base64) : IRequest<Result<AutorizacionDeSubidaDto>>;

public sealed class SolicitarSubidaDeAdjuntoCommandValidator : AbstractValidator<SolicitarSubidaDeAdjuntoCommand>
{
    public SolicitarSubidaDeAdjuntoCommandValidator()
    {
        RuleFor(x => x.OwnerEntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEntityPublicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500)
            .Must(n => AdjuntosDirectos.NombreLimpio(n).Length > 0).WithMessage("El nombre del archivo está vacío.");
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sha256Base64).Must(AdjuntosDirectos.EsHuellaValida)
            .WithMessage("La huella debe ser un SHA-256 en base64 (44 caracteres).");
    }
}

public sealed class SolicitarSubidaDeAdjuntoCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IPermissionChecker permisos,
    ICurrentUserService usuario,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites,
    ILogger<SolicitarSubidaDeAdjuntoCommandHandler> log)
    : IRequestHandler<SolicitarSubidaDeAdjuntoCommand, Result<AutorizacionDeSubidaDto>>
{
    public async Task<Result<AutorizacionDeSubidaDto>> Handle(SolicitarSubidaDeAdjuntoCommand request, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<AutorizacionDeSubidaDto>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var maximo = limites.Value;
        if (request.SizeBytes == 0)
            return Result.Failure<AutorizacionDeSubidaDto>(AttachmentErrorCodes.Validation_FileEmpty, "El archivo está vacío.");
        if (request.SizeBytes > maximo.MaxBytes)
            return Result.Failure<AutorizacionDeSubidaDto>(new ErrorConDatos(AttachmentErrorCodes.Validation_FileTooLarge,
                $"El archivo excede el tamaño máximo de {maximo.MaxBytesLegible}.", new { maxBytes = maximo.MaxBytes }));
        if (!AttachmentPolicy.AllowedMimeTypes.Contains(request.ContentType))
            return Result.Failure<AutorizacionDeSubidaDto>(AttachmentErrorCodes.Validation_MimeTypeNotAllowed,
                "Ese tipo de archivo no se admite como soporte.");
        if (await AdjuntosDeModulo.PuedeSubirAsync(db, permisos, request.OwnerEntityType, request.OwnerEntityPublicId, ct) is { } prohibido)
            return Result.Failure<AutorizacionDeSubidaDto>(prohibido);

        var quien = usuario.UserName ?? "SYSTEM";
        var adjunto = new Attachment
        {
            TenantId = cooperativa,
            OwnerEntityType = request.OwnerEntityType,
            OwnerEntityPublicId = request.OwnerEntityPublicId,
            FileName = AdjuntosDirectos.NombreLimpio(request.FileName),
            // El tipo tal como está en la lista blanca: es el que se firma y el que se exige al almacén.
            ContentType = AttachmentPolicy.AllowedMimeTypes.First(t => string.Equals(t, request.ContentType, StringComparison.OrdinalIgnoreCase)),
            SizeBytes = request.SizeBytes,
            Sha256Hex = AdjuntosDirectos.Base64AHex(request.Sha256Base64),
            EncryptedDek = string.Empty,
            Format = FormatoDeAdjunto.Direct,
            Status = EstadoDeAdjunto.Uploading,
            CreatedBy = quien,
            UpdatedBy = quien,
        };

        // Primero se firma —así se sabe la clave— y después se registra la fila. Si el guardado falla, la
        // autorización nunca llega al navegador: no hay nada que subir ni nada que limpiar.
        var vence = AdjuntosDirectos.Utc(reloj.UtcNow).AddMinutes(maximo.SubidaMinutos);
        var firma = await store.FirmarSubidaAsync(AdjuntosDirectos.Solicitud(adjunto, vence), ct);
        adjunto.StoragePath = firma.Referencia.Uri;
        adjunto.UploadExpiresAt = vence.UtcDateTime;
        db.Attachments.Add(adjunto);
        await db.SaveChangesAsync(ct);

        log.LogInformation(
            "Subida autorizada: adjunto {Adjunto} de la cooperativa {Cooperativa} ({Tipo}, {Tamano} bytes) para {Dueno} {DuenoId}, vence {Vence:o}.",
            adjunto.PublicId, cooperativa, adjunto.ContentType, adjunto.SizeBytes, adjunto.OwnerEntityType, adjunto.OwnerEntityPublicId, vence);
        return Result.Success(AdjuntosDirectos.Dto(adjunto, firma, maximo));
    }
}
