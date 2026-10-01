using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Attachments.RenovarSubida;

/// <summary>
/// Feature 011 (US1, contracts/api.md §2): una subida que no llegó a tiempo se reintenta sobre la
/// <b>misma fila y la misma clave</b>, con una autorización nueva. Sólo para <c>Incomplete</c> o para
/// <c>Uploading</c> con la autorización vencida; en cualquier otro estado, 409
/// <c>Attachments.NotAvailable</c>. El archivo tiene que ser el mismo: la autorización nueva fija la
/// misma huella que la anterior.
/// </summary>
public sealed record RenovarSubidaDeAdjuntoCommand(Guid AttachmentPublicId) : IRequest<Result<AutorizacionDeSubidaDto>>;

public sealed class RenovarSubidaDeAdjuntoCommandValidator : AbstractValidator<RenovarSubidaDeAdjuntoCommand>
{
    public RenovarSubidaDeAdjuntoCommandValidator() => RuleFor(x => x.AttachmentPublicId).NotEmpty();
}

public sealed class RenovarSubidaDeAdjuntoCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IPermissionChecker permisos,
    ICurrentUserService usuario,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites)
    : IRequestHandler<RenovarSubidaDeAdjuntoCommand, Result<AutorizacionDeSubidaDto>>
{
    public async Task<Result<AutorizacionDeSubidaDto>> Handle(RenovarSubidaDeAdjuntoCommand request, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<AutorizacionDeSubidaDto>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var adjunto = await db.Attachments.FirstOrDefaultAsync(a => a.TenantId == cooperativa && a.PublicId == request.AttachmentPublicId, ct);
        if (adjunto is null)
            return Result.Failure<AutorizacionDeSubidaDto>("Generic.NotFound", "Adjunto no encontrado.");
        // Las mismas reglas que pedir la subida: el comprobante tiene que seguir existiendo y la persona
        // poder escribir en él.
        if (await AdjuntosDeModulo.PuedeSubirAsync(db, permisos, adjunto.OwnerEntityType, adjunto.OwnerEntityPublicId, ct, adjunto.ContentType) is { } prohibido)
            return Result.Failure<AutorizacionDeSubidaDto>(prohibido);

        var ahora = reloj.UtcNow;
        if (adjunto.Status != EstadoDeAdjunto.Incomplete && !AdjuntosDirectos.AutorizacionVencida(adjunto, ahora))
            return Result.Failure<AutorizacionDeSubidaDto>(AdjuntosDirectos.NoDisponible(adjunto,
                "Sólo se reintenta una subida que no llegó a tiempo."));
        if (adjunto.Status == EstadoDeAdjunto.Uploading && await store.ConsultarAsync(new BlobReference(adjunto.StoragePath), ct) is not null)
            return Result.Failure<AutorizacionDeSubidaDto>(AdjuntosDirectos.NoDisponible(adjunto,
                "El archivo ya llegó al almacén: hay que confirmarlo, no volver a subirlo."));

        var maximo = limites.Value;
        var vence = AdjuntosDirectos.Utc(ahora).AddMinutes(maximo.SubidaMinutos);
        var firma = await store.FirmarSubidaAsync(AdjuntosDirectos.Solicitud(adjunto, vence, mismaClave: true), ct);
        adjunto.Status = EstadoDeAdjunto.Uploading;
        adjunto.UploadExpiresAt = vence.UtcDateTime;
        adjunto.UpdatedBy = usuario.UserName ?? "SYSTEM";
        await db.SaveChangesAsync(ct);
        return Result.Success(AdjuntosDirectos.Dto(adjunto, firma, maximo));
    }
}
