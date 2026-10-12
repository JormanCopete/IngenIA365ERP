using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Payments.Commands;

/// <summary>Un mensaje que emitió el reverso. (nuevo)</summary>
public sealed record EmittedReversalMessageDto(string Type, Guid MessagePublicId);

/// <summary>Lo que devuelve el reverso por pantalla (api.md §10). (nuevo)</summary>
public sealed record PaymentReversalResultDto(
    Guid ReversalOperationPublicId,
    string Number,
    IReadOnlyList<string> Reallocations,
    IReadOnlyList<Guid> VoidedDocuments,
    IReadOnlyList<EmittedReversalMessageDto> Messages,
    bool Reopened);

/// <summary>
/// <c>POST /api/lending/loans/{id}/operations/{operationId}/reverse</c> (feature 013, US5, T0553; api.md §10; <c>Lending.Payments.Reverse</c>, con clave):
/// reversa un pago desde Cartera (un cheque devuelto, un error de caja) con motivo y la huella de la vista previa que vio quien reversa. (nuevo)
/// </summary>
public sealed record ReverseLoanPaymentCommand(Guid LoanPublicId, Guid OperationPublicId, string Reason, string ExpectedPreviewSha256)
    : IRequest<Result<PaymentReversalResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseLoanPaymentCommandValidator : AbstractValidator<ReverseLoanPaymentCommand>
{
    public ReverseLoanPaymentCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se reversa el pago.").MaximumLength(500);
        RuleFor(x => x.ExpectedPreviewSha256).NotEmpty().WithMessage("Confirme el reverso desde su vista previa.");
    }
}

/// <summary>
/// En una <see cref="TransaccionExplicita"/> (T0553; FR-080, T31, C37): el pago de la pantalla (<see cref="PagoDeLaPantalla"/>: nunca uno de Nómina, de
/// Aportes ni una recogida —<c>Lending.Reversal.OwnedByCollector</c>—); el cerrojo de T34; la vista previa recalculada bajo el cerrojo y comparada con
/// la que se vio (<c>Lending.Reversal.PreviewChanged</c> con <c>data.previewSha256</c>); el reverso por <see cref="ReversionDePago"/> con
/// <c>initiatedBy = Lending</c> y la clave <c>{clave}:reverso</c>, que emite <c>PagoReversado</c> y <c>PagoReversadoPorCartera</c>; y el evento de
/// auditoría con la distribución completa (T23). (nuevo)
/// </summary>
public sealed class ReverseLoanPaymentCommandHandler(
    IApplicationDbContext db,
    ReversionDePago reversion,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<ReverseLoanPaymentCommand, Result<PaymentReversalResultDto>>
{
    public async Task<Result<PaymentReversalResultDto>> Handle(ReverseLoanPaymentCommand request, CancellationToken ct)
    {
        var leido = await PagoDeLaPantalla.LeerAsync(db, alcance, auditoria, request.LoanPublicId, request.OperationPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (credito, pago) = leido.Value;

        var resultado = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);

            var vista = await reversion.PrevisualizarAsync(pago, ct);
            if (vista.IsFailure) return Falla2(vista.Error);
            if (!string.Equals(vista.Value.Huella, request.ExpectedPreviewSha256, StringComparison.Ordinal))
                return Falla2(ErroresDePagos.VistaPreviaCambiada(vista.Value.Huella));

            var clave = ReversionDePago.ClaveDeReverso(pago.IdempotencyKey ?? pago.PublicId.ToString("N"));
            var r = await reversion.ReversarAsync(new ReversoEnCurso(pago, request.Reason.Trim(), EmisionDeCartera.IniciadoPorCartera, clave), ct);
            if (r.IsFailure) return Falla2(r.Error);
            var c = r.Value.Comprobante;
            var dto = new PaymentReversalResultDto(c.OperationPublicId, c.Number, c.ReallocatedPaymentNumbers, c.VoidedDocumentPublicIds,
                r.Value.Mensajes.Mensajes.Select(m => new EmittedReversalMessageDto(m.Type, m.PublicId)).ToList(), c.ObligationReopened);
            return Result.Success((dto, c));
        }, ct);
        if (resultado.IsFailure) return Falla(resultado.Error);
        var (salida, comprobante) = resultado.Value;
        await auditoria.EmitirOperacionRegistradaAsync(salida.ReversalOperationPublicId, salida.Number, nameof(LoanOperationKind.PaymentReversal), new
        {
            reversed = comprobante.ReversedNumber,
            valueDate = pago.ValueDate,
            amount = comprobante.ReversedAmount,
            reason = comprobante.Reason,
            adjustments = comprobante.Adjustments,
            reallocated = comprobante.ReallocatedPaymentNumbers,
            reopened = comprobante.ObligationReopened,
            initiatedBy = EmisionDeCartera.IniciadoPorCartera,
        }, ct);
        return Result.Success(salida);
    }

    private static Result<PaymentReversalResultDto> Falla(Error e) => Result.Failure<PaymentReversalResultDto>(e);

    private static Result<(PaymentReversalResultDto, IngenIA365ERP.Application.Common.Integration.Lending.ComprobanteDeReversoDto)> Falla2(Error e) =>
        Result.Failure<(PaymentReversalResultDto, IngenIA365ERP.Application.Common.Integration.Lending.ComprobanteDeReversoDto)>(e);
}
