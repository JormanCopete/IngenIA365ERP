using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payments.Services;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment;

public record ProcessPaymentCommand : IRequest<Result<PaymentResultDto>>, IReintentableAnteConcurrencia
{
    public Guid PortfolioPublicId { get; init; }
    public decimal Amount { get; init; }
    public DateOnly PaymentDate { get; init; }
    public string PaymentMethod { get; init; } = "EF"; // EF=Efectivo, CH=Cheque, TR=Transferencia
    public string? Reference { get; init; }
    /// <summary>Banco que recibe el recaudo: su cuenta contable va al débito (feature 009).</summary>
    public Guid? BankPublicId { get; init; }
    /// <summary>O, en su defecto, el código de la cuenta de caja.</summary>
    public string? CashAccountCode { get; init; }
}

public record PaymentResultDto(
    Guid TransactionPublicId,
    long DocumentNumber,
    decimal PaidCapital,
    decimal PaidInterest,
    decimal PaidDefault,
    decimal Remaining,
    int InstallmentsCovered,
    bool IsFullyPaid);

public record PaymentDistributionLine(
    int Period,
    decimal Capital,
    decimal Interest,
    decimal Default);

/// <summary>
/// El recaudo por el pipeline (validación, auditoría, reintento ante concurrencia). El cuerpo vive en
/// <see cref="RecaudoDeCredito"/>, que la aprobación de la liquidación definitiva llama directo dentro
/// de su propia transacción (feature 010): anidar este comando allí hacía que un reintento vaciara el
/// <c>ChangeTracker</c> compartido y dejara el recaudo guardado con la corrida todavía en borrador.
/// </summary>
public class ProcessPaymentCommandHandler(RecaudoDeCredito recaudo)
    : IRequestHandler<ProcessPaymentCommand, Result<PaymentResultDto>>
{
    public Task<Result<PaymentResultDto>> Handle(ProcessPaymentCommand request, CancellationToken ct) => recaudo.AplicarAsync(request, ct);
}

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.PortfolioPublicId)
            .NotEmpty().WithMessage("Credito requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El valor del pago debe ser mayor a cero.");

        RuleFor(x => x.PaymentDate)
            .NotEmpty().WithMessage("Fecha de pago requerida.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Forma de pago requerida.");
    }
}
