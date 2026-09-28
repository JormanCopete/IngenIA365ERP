using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// «Consultar a la DIAN» (feature 012, I4, T713; api.md §24.4 <c>POST /documents/{id}/query-status</c>; contracts/dian.md §5.1 y §6.3):
/// <c>ConsultarEstadoAsync</c> por el canal sellado, con el número y el ambiente, el código único si se conoce y la última referencia externa
/// (trackId) del canal. Registra una transmisión <c>QueryStatus</c> como cualquier llamada. <c>NotFound</c> devuelve a <c>Pending</c> con la
/// <b>misma</b> versión y número, para reenviar de inmediato; sobre un <c>Rejected</c>, la consulta que responde rechazo lo <b>confirma</b>
/// (requisito de los casos b y c); y la DIAN que valida tarde un <c>Pending</c> o <c>Sent</c> lo deja validado (edge case «La DIAN valida
/// tarde»), con el cambio en la auditoría de la fila. Lo expedido en contingencia no se consulta: se transmite al cerrarse. (nuevo)
/// </summary>
public sealed record QueryElectronicDocumentStatusCommand(Guid ElectronicDocumentPublicId) : IRequest<Result<ElectronicTransmissionResultDto>>;

public sealed class QueryElectronicDocumentStatusCommandValidator : AbstractValidator<QueryElectronicDocumentStatusCommand>
{
    public QueryElectronicDocumentStatusCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class QueryElectronicDocumentStatusCommandHandler(IntentoAnteElCanal intento)
    : IRequestHandler<QueryElectronicDocumentStatusCommand, Result<ElectronicTransmissionResultDto>>
{
    public Task<Result<ElectronicTransmissionResultDto>> Handle(QueryElectronicDocumentStatusCommand request, CancellationToken ct) =>
        intento.IntentarAsync(request.ElectronicDocumentPublicId, PedidoDeIntento.Consultar, ct);
}
