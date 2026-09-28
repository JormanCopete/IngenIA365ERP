using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// El enlace de descarga de un artefacto de un documento electrónico (feature 012, I4, T718; api.md §24.4
/// <c>POST /documents/{id}/download-link?artifact=&amp;version=&amp;transmission=</c>; contracts/dian.md §12; FR-068): <paramref name="Artifact"/>
/// es texto (<c>Canonical|SignedXml|AttachedDocument|ApplicationResponse|GraphicRepresentation</c>), <paramref name="Version"/> la versión (la
/// vigente si no viene) y <paramref name="Transmission"/> el intento del <c>ApplicationResponse</c> (el último que lo trajo si no viene).
/// Delega en el enlace firmado de 60 s de la 011 y en la <b>regla del dueño del adjunto</b> (<see cref="AdjuntosDeModulo.PuedeLeerAsync"/>:
/// <c>Inventory.Sales.View</c> o <c>Inventory.Purchases.View</c>); sin ella, el mismo 404 que si no existiera. Cada enlace queda en la
/// auditoría encadenada con el módulo <c>ElectronicInvoicing</c>. (nuevo)
/// </summary>
public sealed record GetElectronicArtifactLinkQuery(Guid ElectronicDocumentPublicId, string Artifact, int? Version = null, int? Transmission = null)
    : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class GetElectronicArtifactLinkQueryValidator : AbstractValidator<GetElectronicArtifactLinkQuery>
{
    public GetElectronicArtifactLinkQueryValidator()
    {
        RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
        RuleFor(x => x.Artifact).Must(a => ArtefactosElectronicos.Normalizar(a) is not null)
            .WithMessage($"El artefacto es uno de: {string.Join(", ", ArtefactosElectronicos.Todos)}.");
        RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version is not null);
        RuleFor(x => x.Transmission).GreaterThan(0).When(x => x.Transmission is not null);
    }
}

public sealed class GetElectronicArtifactLinkQueryHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IPermissionChecker permisos,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites,
    IServiceProvider servicios)
    : IRequestHandler<GetElectronicArtifactLinkQuery, Result<EnlaceDeDescargaDto>>
{
    /// <summary>La acción auditada.</summary>
    public const string Accion = "ElectronicInvoicing.Artifact.LinkIssued";

    public async Task<Result<EnlaceDeDescargaDto>> Handle(GetElectronicArtifactLinkQuery request, CancellationToken ct)
    {
        var artefacto = ArtefactosElectronicos.Normalizar(request.Artifact);
        var documento = await db.ElectronicDocuments.AsNoTracking().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.PublicId == request.ElectronicDocumentPublicId, ct);
        if (documento is null || artefacto is null) return NoEncontrado();

        // Sin la regla del dueño, lo mismo que si no existiera (no revela qué documentos hay).
        if (!await AdjuntosDeModulo.PuedeLeerAsync(permisos, GuardadoDeArtefactos.DuenoDe(documento.Kind), ct)) return NoEncontrado();

        Guid? adjuntoId;
        int? versionNumero = null;
        if (artefacto == ArtefactosElectronicos.ApplicationResponse)
        {
            var transmisiones = db.ElectronicDocumentTransmissions.AsNoTracking()
                .Where(t => t.ElectronicDocumentId == documento.Id && t.ApplicationResponseAttachmentPublicId != null);
            if (request.Transmission is { } intento) transmisiones = transmisiones.Where(t => t.AttemptNumber == intento);
            adjuntoId = await transmisiones.OrderByDescending(t => t.AttemptNumber).Select(t => t.ApplicationResponseAttachmentPublicId).FirstOrDefaultAsync(ct);
        }
        else
        {
            var version = request.Version is { } v
                ? documento.Versions.FirstOrDefault(x => x.VersionNumber == v)
                : documento.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            versionNumero = version?.VersionNumber;
            adjuntoId = artefacto switch
            {
                ArtefactosElectronicos.Canonical => version?.CanonicalAttachmentPublicId,
                ArtefactosElectronicos.SignedXml => version?.SignedXmlAttachmentPublicId,
                ArtefactosElectronicos.AttachedDocument => version?.AttachedDocumentAttachmentPublicId,
                _ => version?.GraphicPdfAttachmentPublicId,
            };
        }
        if (adjuntoId is not { } id) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeDocumentosElectronicos.ArtifactNotFound());

        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == id, ct);
        if (adjunto is null) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeDocumentosElectronicos.ArtifactNotFound());
        if (adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(AdjuntosDirectos.NoDisponible(adjunto, "El archivo no está disponible."));

        await AuditarAsync(documento.PublicId, artefacto, versionNumero, request.Transmission, adjunto.PublicId, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }

    /// <summary>Anota el enlace en la auditoría encadenada (<c>COR_AuditOutbox</c>) o, sin cooperativa, por <see cref="IAuditService"/>.</summary>
    private async Task AuditarAsync(Guid documento, string artefacto, int? version, int? transmision, Guid adjunto, CancellationToken ct)
    {
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct);
        var evento = new AuditLogCommand
        {
            Action = Accion,
            EntityType = GuardadoDeArtefactos.EntidadDelDocumento,
            EntityId = documento.ToString(),
            Module = ModuloDeAuditoria.ElectronicInvoicing,
            NewValues = new { artifact = artefacto, version, transmission = transmision, attachmentPublicId = adjunto },
            HttpStatusCode = 200,
        };
        var flujo = AuditoriaEncadenada.Flujo(contexto.TenantId);
        if (flujo is null)
        {
            if (servicios.GetService<IAuditService>() is { } mongo) await mongo.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
            return;
        }
        db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento)));
        await db.SaveChangesAsync(ct);
    }

    private static Result<EnlaceDeDescargaDto> NoEncontrado() => Result.Failure<EnlaceDeDescargaDto>("Generic.NotFound", "Documento no encontrado.");
}
