using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// <c>POST /api/lending/documents/{id}/download-link</c> (feature 013, US13, T0785; api.md §15, adaptadores-y-archivos.md §8.7,
/// <c>Lending.Documents.Reprint</c>, sin clave: repetirlo emite otro enlace, como el del archivo de desembolsos): el enlace firmado de 60 s del
/// <b>mismo</b> PDF guardado al emitir —reimprimir es descargar ese archivo, idéntico byte a byte, nunca volver a pintar—, auditado como
/// <c>Lending.Document.Reprinted</c>. Un documento anulado no se reimprime para entregarlo (<c>Lending.Document.VoidedCannotReissue</c>): se emite
/// otro. Fuera del alcance o inexistente, <c>Lending.Document.NotFound</c>. (nuevo)
/// </summary>
public sealed record IssueLendingDocumentDownloadLinkCommand(Guid DocumentPublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class IssueLendingDocumentDownloadLinkCommandValidator : AbstractValidator<IssueLendingDocumentDownloadLinkCommand>
{
    public IssueLendingDocumentDownloadLinkCommandValidator() => RuleFor(x => x.DocumentPublicId).NotEmpty();
}

public sealed class IssueLendingDocumentDownloadLinkCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<IssueLendingDocumentDownloadLinkCommand, Result<EnlaceDeDescargaDto>>
{
    /// <summary>El formato de la reimpresión que queda en la auditoría.</summary>
    public const string Formato = "pdf";

    public async Task<Result<EnlaceDeDescargaDto>> Handle(IssueLendingDocumentDownloadLinkCommand request, CancellationToken ct)
    {
        var leido = await DocumentosEmitidos.LeerAsync(db, alcance, auditoria, request.DocumentPublicId, ct);
        if (leido.IsFailure) return Result.Failure<EnlaceDeDescargaDto>(leido.Error);
        var documento = leido.Value;
        if (await DocumentosEmitidos.AnulacionAsync(db, documento.Id, ct) is not null)
            return Result.Failure<EnlaceDeDescargaDto>(DocumentErrors.AnuladoNoSeReemite(documento.Number));

        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == documento.PdfAttachmentPublicId, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(new ErrorConDatos(DocumentErrors.CodigoInexistente,
                "El PDF del documento no está disponible para descargar.", new { documentPublicId = documento.PublicId }));

        await auditoria.EmitirDocumentoReimpresoAsync(documento.PublicId, documento.Kind.ToString(), Formato, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}

/// <summary>
/// <c>POST /api/lending/documents/{id}/void</c> (feature 013, US13, T0785; api.md §15, adaptadores-y-archivos.md §8.8, <c>Lending.Documents.Void</c>,
/// con clave): la anulación manual con motivo —el evento <c>Voided</c>; el documento y su PDF no se tocan (Principio XI)—. Una segunda anulación
/// responde <c>Lending.Document.AlreadyVoided</c>; el índice único filtrado de los eventos es la segunda defensa. (nuevo)
/// </summary>
public sealed record VoidLendingDocumentCommand(Guid DocumentPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidLendingDocumentCommandValidator : AbstractValidator<VoidLendingDocumentCommand>
{
    public VoidLendingDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el documento.")
            .MaximumLength(IssuedDocumentEvent.LargoDelMotivo);
    }
}

public sealed class VoidLendingDocumentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidLendingDocumentCommand, Result>
{
    public async Task<Result> Handle(VoidLendingDocumentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());
        var leido = await DocumentosEmitidos.LeerAsync(db, alcance, auditoria, request.DocumentPublicId, ct);
        if (leido.IsFailure) return Result.Failure(leido.Error);
        var documento = leido.Value;
        if (await DocumentosEmitidos.AnulacionAsync(db, documento.Id, ct) is { } anterior)
            return Result.Failure(DocumentErrors.YaAnulado(documento.Number, anterior.OccurredAt));

        var motivo = request.Reason.Trim();
        db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Anulado(documento.Id, motivo, reloj.UtcNow, ActorKind.Person, usuario));
        await db.SaveChangesAsync(ct);
        await auditoria.EmitirDocumentoAnuladoAsync(documento.PublicId, documento.Number, motivo, ct);
        return Result.Success();
    }
}
