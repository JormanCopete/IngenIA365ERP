using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>El reemplazo hecho: la anulación sin efecto fiscal, el reemplazo, la versión nueva y el estado (<c>Pending</c>). (nuevo)</summary>
public sealed record ReemplazoDelRechazoDto(
    Guid ElectronicDocumentPublicId,
    Guid VoidingDocumentPublicId,
    Guid ReplacementDocumentPublicId,
    short VersionNumber,
    ElectronicDocumentStatus Status);

/// <summary>
/// Caso b de un documento rechazado: reemplazarlo con el mismo número (feature 012, I4, T724; FR-066; contracts/dian.md §8.3; api.md §24.5
/// <c>POST /documents/{id}/replace</c>, <c>ElectronicInvoicing.Documents.Correct</c> más el permiso de confirmar de la clase —
/// <c>Inventory.Sales.Confirm</c> o <c>Inventory.Purchases.Confirm</c>, 422 <c>ClassPermissionRequired</c>—). Exige el rechazo confirmado por
/// la consulta de estado. En <b>una</b> transacción (la de <c>IdempotencyBehavior</c>):
/// <list type="number">
/// <item>la fuente anula el documento comercial con un <c>Voiding</c> <b>sin efecto fiscal</b> (<c>DocumentoAnulado</c> con
/// <c>fiscalCase = DianRejectionReplaced</c>, devolución a existencia, <c>AjusteDeVentaACredito</c> <c>VoidingByDianRejection</c> si hubo
/// crédito) y marca <c>FiscalNumberReleased</c>;</item>
/// <item>la fuente confirma el borrador de reemplazo por su flujo canónico (validación previa y cerrojo incluidos) con el <b>mismo</b> número
/// fiscal, que le pone <see cref="NumeradorFiscal.ReutilizarNumeroParaReemplazo"/> —la vía exclusiva del caso b, sin consumir la
/// resolución— (<c>AjusteDeVentaACredito</c> <c>Replacement</c> si hay crédito);</item>
/// <item>el documento electrónico pasa a apuntar al reemplazo (<c>SourceDocumentPublicId</c>), nace la versión n + 1 <c>CaseB</c> con su
/// canónico y queda <c>Pending</c> por el canal sellado.</item>
/// </list>
/// La emisión va después del commit (procesador o «Reintentar ahora»; contracts/dian.md §6.1). (nuevo)
/// </summary>
public sealed record ReplaceRejectedDocumentCommand(Guid ElectronicDocumentPublicId, Guid ReplacementDocumentPublicId, string Reason)
    : IRequest<Result<ReemplazoDelRechazoDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReplaceRejectedDocumentCommandValidator : ValidadorConMotivo<ReplaceRejectedDocumentCommand>
{
    public ReplaceRejectedDocumentCommandValidator()
    {
        RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
        RuleFor(x => x.ReplacementDocumentPublicId).NotEmpty();
    }
}

public sealed class ReplaceRejectedDocumentCommandHandler(
    IApplicationDbContext db,
    CasosDeRechazo casos,
    ConstructorDelCanonico constructor,
    IDateTimeService reloj) : IRequestHandler<ReplaceRejectedDocumentCommand, Result<ReemplazoDelRechazoDto>>
{
    public async Task<Result<ReemplazoDelRechazoDto>> Handle(ReplaceRejectedDocumentCommand request, CancellationToken ct)
    {
        var preparado = await casos.PrepararAsync(request.ElectronicDocumentPublicId, EventoDelDocumentoElectronico.ReemplazarCasoB, ct);
        if (preparado.IsFailure) return Falla(preparado.Error);
        var (documento, fuente) = preparado.Value;
        var rechazado = documento.SourceDocumentPublicId;
        var prefijo = documento.Prefix;
        var consecutivo = documento.Consecutive;

        // 1. La anulación sin efecto fiscal del rechazado (libera su número frente al índice único).
        var anulacion = await fuente.AnularSinEfectoFiscalAsync(rechazado, CasoFiscalDeAnulacion.DianRejectionReplaced, request.Reason, ct);
        if (anulacion.IsFailure) return Falla(anulacion.Error);

        // 2. El reemplazo con el mismo número, por la vía exclusiva del caso b.
        var reemplazo = await fuente.ConfirmarReemplazoAsync(rechazado, request.ReplacementDocumentPublicId,
            borrador => NumeradorFiscal.ReutilizarNumeroParaReemplazo(borrador, prefijo, consecutivo), ct);
        if (reemplazo.IsFailure) return Falla(reemplazo.Error);

        // 3. El canónico del reemplazo con lo sellado al numerar, y la versión n + 1.
        var entrada = await fuente.LeerAsync(request.ReplacementDocumentPublicId, ct);
        if (entrada.IsFailure) return Falla(entrada.Error);
        var canonico = await constructor.ConstruirAsync(entrada.Value, ReconstruccionDelCanonico.NumeracionDe(documento.EmissionSetting!,
            documento.Resolution, prefijo, consecutivo, documento.ContingencyType == ContingencyType.Issuer03, documento.CorrectsDocument), ct);
        if (canonico.IsFailure) return Falla(canonico.Error);

        var ahora = reloj.UtcNow;
        var transicion = documento.AplicarEvento(EventoDelDocumentoElectronico.ReemplazarCasoB, null, ahora, rechazoConfirmado: true);
        if (!transicion.Procede) return Falla(ErroresDeDocumentosElectronicos.DeLaTransicion(transicion));

        documento.SourceDocumentPublicId = request.ReplacementDocumentPublicId;
        var parte = canonico.Value.Documento.Counterparty;
        documento.CounterpartyTaxId = Recortar(parte.TaxId, 20);
        documento.CounterpartyName = Recortar(parte.Name, 200);
        documento.TotalAmount = canonico.Value.Documento.Totals.Payable;

        var version = await casos.AgregarVersionAsync(documento, request.ReplacementDocumentPublicId, DocumentVersionReason.CaseB, canonico.Value,
            request.Reason, null, ahora, ct);

        return Result.Success(new ReemplazoDelRechazoDto(documento.PublicId, anulacion.Value.VoidingDocumentPublicId,
            request.ReplacementDocumentPublicId, version.VersionNumber, documento.Status));
    }

    private static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<ReemplazoDelRechazoDto> Falla(Error error) => Result.Failure<ReemplazoDelRechazoDto>(error);
}
