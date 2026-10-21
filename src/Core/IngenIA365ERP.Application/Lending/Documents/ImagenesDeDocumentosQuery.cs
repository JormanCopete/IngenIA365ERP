using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>Una imagen vigente de los documentos de Cartera: el adjunto, su nombre y cuándo se subió. (nuevo)</summary>
public sealed record ImagenDeDocumentosDto(Guid AttachmentPublicId, string FileName, string ContentType, DateTime UploadedAt);

/// <summary>
/// Lo que necesita la carga del logotipo y de la imagen de la firma en <c>/cartera/parametros</c>: el dueño de los dos adjuntos —la fila única de
/// <c>LND_Setup</c>— y las imágenes vigentes, si las hay. (nuevo)
/// </summary>
public sealed record ImagenesDeDocumentosDto(Guid SetupPublicId, string LogoOwnerType, string SignatureOwnerType, ImagenDeDocumentosDto? Logo,
    ImagenDeDocumentosDto? Signature);

/// <summary>
/// <c>GET /api/lending/documents/images</c> (feature 013, US13, T0781; pregunta I6 de DT §5, rige la propuesta por defecto; <c>Lending.Parameters.View</c>):
/// el dueño de los adjuntos <c>LendingLogo</c> y <c>LendingSignatureImage</c> y la imagen vigente de cada uno —la última disponible: subir otra la
/// reemplaza y la emisión toma siempre la vigente—. La subida va por la ruta de la plataforma (<c>POST /api/attachments/uploads</c>) con
/// <c>Lending.Parameters.Manage</c>, que revisa <see cref="AdjuntosDeModulo"/>. (nuevo)
/// </summary>
public sealed record GetLendingDocumentImagesQuery : IRequest<Result<ImagenesDeDocumentosDto>>;

public sealed class GetLendingDocumentImagesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLendingDocumentImagesQuery, Result<ImagenesDeDocumentosDto>>
{
    public async Task<Result<ImagenesDeDocumentosDto>> Handle(GetLendingDocumentImagesQuery request, CancellationToken ct)
    {
        var setup = await db.LendingSetups.AsNoTracking().Where(s => s.Id == LendingSetup.IdDeLaFilaUnica).Select(s => (Guid?)s.PublicId)
            .FirstOrDefaultAsync(ct);
        if (setup is not { } dueno) return Result.Failure<ImagenesDeDocumentosDto>("Generic.NotFound", "Configuración de Cartera no encontrada.");
        return Result.Success(new ImagenesDeDocumentosDto(dueno, AdjuntosDeModulo.LogotipoDeCartera, AdjuntosDeModulo.ImagenDeFirmaDeCartera,
            await VigenteAsync(AdjuntosDeModulo.LogotipoDeCartera, dueno, ct), await VigenteAsync(AdjuntosDeModulo.ImagenDeFirmaDeCartera, dueno, ct)));
    }

    private Task<ImagenDeDocumentosDto?> VigenteAsync(string tipo, Guid dueno, CancellationToken ct) =>
        db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == tipo && a.OwnerEntityPublicId == dueno && a.Status == EstadoDeAdjunto.Available && !a.IsDeleted)
            .OrderByDescending(a => a.Id)
            .Select(a => new ImagenDeDocumentosDto(a.PublicId, a.FileName, a.ContentType, a.CreatedAt))
            .FirstOrDefaultAsync(ct);
}
