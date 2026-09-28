using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Un intento de emisión de un documento electrónico por su canal <b>sellado</b> (feature 012, I4, T712; contracts/dian.md §6; api.md §24.4
/// <c>POST /documents/{id}/retry</c>). Lo envían el procesador (<c>ProcesadorDeDocumentosElectronicos</c>, T731), el intento en línea del POS
/// después del commit (T736) y «Reintentar ahora» (<paramref name="RetryNow"/>), que sólo adelanta la espera y respeta la regla del ambiguo: en
/// <c>Sent</c> responde <c>ElectronicInvoicing.Document.AwaitingResponse</c> y se consulta con <c>query-status</c>; en un estado final,
/// <c>.Final</c>. Decide la operación (<c>Emit</c>, <c>TransmitContingency</c> o <c>QueryStatus</c>) por el estado; ver
/// <see cref="IntentoAnteElCanal"/>.
///
/// <para>
/// <b>No</b> es <c>IOperacionIdempotente</c>: la clave de operación abriría una transacción alrededor de la llamada al canal, que va sola, y el
/// registro del resultado en otra. Repetirlo es inocuo por el arrendamiento de la fila, la unicidad del número y la regla del ambiguo. La
/// cabecera <c>Idempotency-Key</c> de la ruta la honra el endpoint (T746). (nuevo)
/// </para>
/// </summary>
public sealed record EmitElectronicDocumentCommand(Guid ElectronicDocumentPublicId, bool RetryNow = false)
    : IRequest<Result<ElectronicTransmissionResultDto>>;

public sealed class EmitElectronicDocumentCommandValidator : AbstractValidator<EmitElectronicDocumentCommand>
{
    public EmitElectronicDocumentCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class EmitElectronicDocumentCommandHandler(IntentoAnteElCanal intento)
    : IRequestHandler<EmitElectronicDocumentCommand, Result<ElectronicTransmissionResultDto>>
{
    public Task<Result<ElectronicTransmissionResultDto>> Handle(EmitElectronicDocumentCommand request, CancellationToken ct) =>
        intento.IntentarAsync(request.ElectronicDocumentPublicId, request.RetryNow ? PedidoDeIntento.ReintentarAhora : PedidoDeIntento.Automatico, ct);
}
