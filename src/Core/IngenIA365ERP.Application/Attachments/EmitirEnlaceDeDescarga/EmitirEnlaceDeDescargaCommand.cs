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

namespace IngenIA365ERP.Application.Attachments.EmitirEnlaceDeDescarga;

/// <summary>
/// Feature 011 (US2, contracts/api.md §5): después de verificar cooperativa y permiso del módulo, firma
/// un enlace de descarga directa que vence en segundos y obliga a guardar el archivo con su nombre y su
/// tipo. El archivo no pasa por el servidor. Es un comando y no una consulta porque cada enlace queda en
/// la auditoría (FR-046): quién pidió bajar qué y cuándo.
///
/// <para>
/// Lo escrito con el formato anterior (<c>AppEncrypted</c>) responde <c>direct: false</c> y el cliente
/// lo baja por la API, que lo descifra (§6). Lo que no está disponible —subiendo, rechazado,
/// incompleto— responde 409 <c>Attachments.NotAvailable</c>: el ERP no firma descargas de algo que no
/// aceptó.
/// </para>
/// </summary>
public sealed record EmitirEnlaceDeDescargaCommand(Guid AttachmentPublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class EmitirEnlaceDeDescargaCommandValidator : AbstractValidator<EmitirEnlaceDeDescargaCommand>
{
    public EmitirEnlaceDeDescargaCommandValidator() => RuleFor(x => x.AttachmentPublicId).NotEmpty();
}

public sealed class EmitirEnlaceDeDescargaCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IPermissionChecker permisos,
    ICurrentUserService usuario,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites)
    : IRequestHandler<EmitirEnlaceDeDescargaCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(EmitirEnlaceDeDescargaCommand request, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<EnlaceDeDescargaDto>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var adjunto = await db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == cooperativa && a.PublicId == request.AttachmentPublicId, ct);
        if (adjunto is null || !await AdjuntosDeModulo.PuedeLeerAsync(permisos, adjunto.OwnerEntityType, ct))
            return Result.Failure<EnlaceDeDescargaDto>("Generic.NotFound", "Adjunto no encontrado.");
        if (adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(AdjuntosDirectos.NoDisponible(adjunto,
                "El archivo no está disponible: todavía se está subiendo, se rechazó o la subida quedó incompleta."));
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted)
            return Result.Success(EnlaceDeDescargaDto.PorLaApi);

        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}
