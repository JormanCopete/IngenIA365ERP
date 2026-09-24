using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Attachments.ConfirmarSubida;

/// <summary>
/// Feature 011 (US1, contracts/api.md §3): revisa lo que llegó al almacén y deja el adjunto en su estado
/// final. Es <b>idempotente</b>: confirmar algo disponible o rechazado devuelve el estado sin volver a
/// mirar.
///
/// <list type="bullet">
///   <item><c>Available</c>: el objeto está, mide lo autorizado y su inicio corresponde al tipo declarado
///   (<see cref="FirmaDeContenido"/>, sobre los primeros 8 KiB: el archivo no entra entero a la memoria
///   del servidor).</item>
///   <item><c>Rejected</c>: el objeto está pero no corresponde. Se retira a la papelera del almacén
///   (research R7, FR-001 opción A: la segunda y última eliminación automática, y sólo alcanza a algo que
///   nunca fue aceptado) y la fila queda visible con el motivo.</item>
///   <item><c>Incomplete</c>: la autorización venció y no llegó nada. No se borra: queda para que alguien
///   decida (reintentar o borrar).</item>
///   <item><c>Uploading</c>: no llegó nada pero la autorización sigue vigente; la subida puede estar en
///   curso.</item>
/// </list>
/// </summary>
public sealed record ConfirmarSubidaDeAdjuntoCommand(Guid AttachmentPublicId)
    : IRequest<Result<ConfirmacionDeSubidaDto>>, IReintentableAnteConcurrencia;

public sealed class ConfirmarSubidaDeAdjuntoCommandValidator : AbstractValidator<ConfirmarSubidaDeAdjuntoCommand>
{
    public ConfirmarSubidaDeAdjuntoCommandValidator() => RuleFor(x => x.AttachmentPublicId).NotEmpty();
}

public sealed class ConfirmarSubidaDeAdjuntoCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IPermissionChecker permisos,
    ICurrentUserService usuario,
    IDateTimeService reloj,
    ILogger<ConfirmarSubidaDeAdjuntoCommandHandler> log)
    : IRequestHandler<ConfirmarSubidaDeAdjuntoCommand, Result<ConfirmacionDeSubidaDto>>
{
    public async Task<Result<ConfirmacionDeSubidaDto>> Handle(ConfirmarSubidaDeAdjuntoCommand request, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<ConfirmacionDeSubidaDto>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var adjunto = await db.Attachments.FirstOrDefaultAsync(a => a.TenantId == cooperativa && a.PublicId == request.AttachmentPublicId, ct);
        if (adjunto is null || !await AdjuntosDeModulo.PuedeLeerAsync(permisos, adjunto.OwnerEntityType, ct))
            return Result.Failure<ConfirmacionDeSubidaDto>("Generic.NotFound", "Adjunto no encontrado.");

        if (adjunto.Status != EstadoDeAdjunto.Uploading)
            return Result.Success(Dto(adjunto));

        var ahora = reloj.UtcNow;
        var referencia = new BlobReference(adjunto.StoragePath);
        var objeto = await store.ConsultarAsync(referencia, ct);
        if (objeto is null)
        {
            if (!AdjuntosDirectos.AutorizacionVencida(adjunto, ahora))
                return Result.Success(Dto(adjunto));
            adjunto.Status = EstadoDeAdjunto.Incomplete;
        }
        else
        {
            var motivo = await MotivoDeRechazoAsync(adjunto.ContentType, adjunto.SizeBytes, adjunto.Sha256Hex, objeto, referencia, ct);
            if (motivo is not null)
            {
                // Primero el objeto a la papelera, después la fila (el mismo orden que borrar, R8).
                await store.DeleteAsync(referencia, ct);
                adjunto.Status = EstadoDeAdjunto.Rejected;
                adjunto.RejectionReason = motivo.Length <= 300 ? motivo : motivo[..300];
            }
            else
            {
                adjunto.Status = EstadoDeAdjunto.Available;
            }
        }

        var quien = usuario.UserName ?? "SYSTEM";
        adjunto.ConfirmedAt = ahora;
        adjunto.ConfirmedBy = quien.Length <= 100 ? quien : quien[..100];
        adjunto.UpdatedBy = adjunto.ConfirmedBy;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Subida del adjunto {Adjunto} de la cooperativa {Cooperativa}: {Estado}{Motivo}.",
            adjunto.PublicId, cooperativa, adjunto.Status, adjunto.RejectionReason is null ? string.Empty : $" ({adjunto.RejectionReason})");
        return Result.Success(Dto(adjunto));
    }

    private async Task<string?> MotivoDeRechazoAsync(
        string tipo, long tamanoAutorizado, string sha256Hex, EstadoDelObjeto objeto, BlobReference referencia, CancellationToken ct)
    {
        if (objeto.Tamano != tamanoAutorizado)
            return $"El archivo mide {objeto.Tamano:N0} bytes y se autorizaron {tamanoAutorizado:N0}.";
        // Con la política firmada, el almacén no acepta otra huella; esto cubre a un almacén que no la guarde.
        if (objeto.Sha256Base64 is { } huella && huella != AdjuntosDirectos.HexABase64(sha256Hex))
            return "El contenido no corresponde a la huella autorizada.";
        var inicio = await store.LeerInicioAsync(referencia, AdjuntosDirectos.BytesAExaminar, ct);
        return FirmaDeContenido.Motivo(tipo, inicio, objeto.Tamano);
    }

    private static ConfirmacionDeSubidaDto Dto(Domain.Entities.Core.Attachment a) => new(a.Status, a.RejectionReason, a.ConfirmedAt);
}
