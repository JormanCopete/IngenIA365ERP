using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>La cancelación hecha: la anulación sin efecto fiscal y el estado final (<c>CancelledWithoutReplacement</c>). (nuevo)</summary>
public sealed record CancelacionDelRechazoDto(Guid ElectronicDocumentPublicId, Guid VoidingDocumentPublicId, ElectronicDocumentStatus Status);

/// <summary>
/// Caso c de un documento rechazado: cancelarlo sin reemplazo (feature 012, I4, T725; FR-066, FR-038; contracts/dian.md §8.4; api.md §24.5
/// <c>POST /documents/{id}/cancel</c>, los mismos permisos que el caso b). Exige el rechazo confirmado. En una transacción la fuente anula
/// el documento comercial igual que en el caso b (<c>Voiding</c> sin efecto fiscal, <c>fiscalCase = DianRejectionCancelled</c>, devolución a
/// existencia, <c>VoidingByDianRejection</c> si hubo crédito) y el documento electrónico queda <c>CancelledWithoutReplacement</c> con
/// <c>RejectionReason</c> y <c>CancelledByUserId</c>: el número queda ligado a su documento anulado y no cuenta como hueco (un rechazado no
/// está expedido; nada lo reemite). (nuevo)
/// </summary>
public sealed record CancelRejectedDocumentCommand(Guid ElectronicDocumentPublicId, string Reason)
    : IRequest<Result<CancelacionDelRechazoDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelRejectedDocumentCommandValidator : ValidadorConMotivo<CancelRejectedDocumentCommand>
{
    public CancelRejectedDocumentCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class CancelRejectedDocumentCommandHandler(
    IApplicationDbContext db,
    CasosDeRechazo casos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CancelRejectedDocumentCommand, Result<CancelacionDelRechazoDto>>
{
    public async Task<Result<CancelacionDelRechazoDto>> Handle(CancelRejectedDocumentCommand request, CancellationToken ct)
    {
        var preparado = await casos.PrepararAsync(request.ElectronicDocumentPublicId, EventoDelDocumentoElectronico.CancelarCasoC, ct);
        if (preparado.IsFailure) return Falla(preparado.Error);
        var (documento, fuente) = preparado.Value;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(Error.Unauthorized);

        var anulacion = await fuente.AnularSinEfectoFiscalAsync(documento.SourceDocumentPublicId, CasoFiscalDeAnulacion.DianRejectionCancelled,
            request.Reason, ct);
        if (anulacion.IsFailure) return Falla(anulacion.Error);

        var motivo = request.Reason.Trim();
        var transicion = documento.CancelarSinReemplazo(motivo.Length <= ValidadorConMotivo<CancelRejectedDocumentCommand>.LargoMaximo
            ? motivo
            : motivo[..ValidadorConMotivo<CancelRejectedDocumentCommand>.LargoMaximo], usuario, rechazoConfirmado: true);
        if (!transicion.Procede) return Falla(ErroresDeDocumentosElectronicos.DeLaTransicion(transicion));

        // Final: el procesador no lo vuelve a tomar.
        documento.NextAttemptAt = null;
        documento.LeaseUntil = null;
        documento.LeaseOwner = null;
        await db.SaveChangesAsync(ct);

        return Result.Success(new CancelacionDelRechazoDto(documento.PublicId, anulacion.Value.VoidingDocumentPublicId, documento.Status));
    }

    private static Result<CancelacionDelRechazoDto> Falla(Error error) => Result.Failure<CancelacionDelRechazoDto>(error);
}
