using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// La preparación del caso b (feature 012, I4, T723; api.md §24.5 <c>POST /documents/{id}/replacement-draft</c>,
/// <c>ElectronicInvoicing.Documents.Correct</c>): exige el documento <c>Rejected</c> con el rechazo <b>confirmado</b> por la consulta de
/// estado y delega en el módulo dueño (<see cref="IFuenteDeDocumentoElectronico.CrearBorradorDeReemplazoAsync"/>), que crea un borrador de la
/// misma clase, precargado, vinculado con <c>ReplacementOf</c> y sin número → 201
/// <c>{ replacementDraftPublicId, sourceModule, editRoute }</c>. Si ya hay uno vivo, <c>ElectronicInvoicing.Document.ReplacementDraftExists</c>
/// (<c>data.replacementDraftPublicId</c>). El borrador se edita por la ruta de su grupo y se confirma con
/// <see cref="ReplaceRejectedDocumentCommand"/>. (nuevo)
/// </summary>
public sealed record CreateReplacementDraftCommand(Guid ElectronicDocumentPublicId) : IRequest<Result<BorradorDeReemplazo>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateReplacementDraftCommandValidator : AbstractValidator<CreateReplacementDraftCommand>
{
    public CreateReplacementDraftCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class CreateReplacementDraftCommandHandler(CasosDeRechazo casos) : IRequestHandler<CreateReplacementDraftCommand, Result<BorradorDeReemplazo>>
{
    public async Task<Result<BorradorDeReemplazo>> Handle(CreateReplacementDraftCommand request, CancellationToken ct)
    {
        // La misma decisión que el reemplazo (b): rechazado y confirmado. El permiso de la clase lo exige confirmar el reemplazo.
        var preparado = await casos.PrepararAsync(request.ElectronicDocumentPublicId, EventoDelDocumentoElectronico.ReemplazarCasoB, ct,
            exigirPermisoDeLaClase: false);
        if (preparado.IsFailure) return Result.Failure<BorradorDeReemplazo>(preparado.Error);
        var (documento, fuente) = preparado.Value;
        return await fuente.CrearBorradorDeReemplazoAsync(documento.SourceDocumentPublicId, ct);
    }
}
