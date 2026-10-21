using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// El enlace firmado de 60 s al archivo de desembolsos (feature 013, US3, T0454; api.md §9.3, <c>POST /api/lending/disbursement-files/{id}/download-link</c>,
/// <c>Lending.DisbursementFiles.Generate</c>, sin clave: repetirlo emite otro, §1.5): del archivo de un crédito del alcance, obliga a guardar con
/// nombre y tipo, y queda auditado (<c>Lending.DisbursementFile.Downloaded</c>, con el SHA-256). Un adjunto del formato anterior se baja por la API
/// (<c>Direct = false</c>). (nuevo)
/// </summary>
public sealed record IssueDisbursementFileDownloadLinkCommand(Guid FilePublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class IssueDisbursementFileDownloadLinkCommandValidator : AbstractValidator<IssueDisbursementFileDownloadLinkCommand>
{
    public IssueDisbursementFileDownloadLinkCommandValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class IssueDisbursementFileDownloadLinkCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<IssueDisbursementFileDownloadLinkCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(IssueDisbursementFileDownloadLinkCommand request, CancellationToken ct)
    {
        var leido = await ArchivosDeDesembolsos.LeerAsync(db, alcance, auditoria, request.FilePublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<EnlaceDeDescargaDto>(leido.Error);
        var archivo = leido.Value;
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == archivo.FileAttachmentPublicId, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(new ErrorConDatos(ErroresDeArchivoDeDesembolsos.CodigoInexistente,
                "El archivo no está disponible para descargar.", new { filePublicId = archivo.PublicId }));

        await auditoria.EmitirArchivoDeDesembolsosDescargadoAsync(archivo.PublicId, archivo.Number, archivo.FileSha256, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}
