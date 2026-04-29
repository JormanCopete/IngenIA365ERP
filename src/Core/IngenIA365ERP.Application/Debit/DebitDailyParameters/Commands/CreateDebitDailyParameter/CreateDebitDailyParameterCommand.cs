using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Debit;
using MediatR;

namespace IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.CreateDebitDailyParameter;

public record CreateDebitDailyParameterCommand : IRequest<Result<Guid>>
{
    public int ParameterCode { get; init; }
    public string BankId { get; init; } = string.Empty;
    public string BatchVoucherCode { get; init; } = string.Empty;
    public string OnlineVoucherCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int NewCardsCount { get; init; }
    public string? LastCardNumber { get; init; }
    public string? ClosingVoucherCode { get; init; }
    public long ClosingSequenceNumber { get; init; }
    public string? PosClosingVoucherCode { get; init; }
    public int PosClosingSequence { get; init; }
    public string? ClosingFlag { get; init; }
    public decimal NetworkCommission { get; init; }
    public decimal OtherNetworkCommission { get; init; }
}

public class CreateDebitDailyParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDebitDailyParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDebitDailyParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new DebitDailyParameter
        {
            ParameterCode = request.ParameterCode,
            BankId = request.BankId,
            BatchVoucherCode = request.BatchVoucherCode,
            OnlineVoucherCode = request.OnlineVoucherCode,
            Description = request.Description,
            NewCardsCount = request.NewCardsCount,
            LastCardNumber = request.LastCardNumber,
            ClosingVoucherCode = request.ClosingVoucherCode,
            ClosingSequenceNumber = request.ClosingSequenceNumber,
            PosClosingVoucherCode = request.PosClosingVoucherCode,
            PosClosingSequence = request.PosClosingSequence,
            ClosingFlag = request.ClosingFlag,
            NetworkCommission = request.NetworkCommission,
            OtherNetworkCommission = request.OtherNetworkCommission,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DebitDailyParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDebitDailyParameterCommandValidator : AbstractValidator<CreateDebitDailyParameterCommand>
{
    public CreateDebitDailyParameterCommandValidator()
    {
        RuleFor(x => x.BankId)
            .NotEmpty().WithMessage("Bank ID is required.")
            .MaximumLength(10).WithMessage("Bank ID must not exceed 10 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(50).WithMessage("Description must not exceed 50 characters.");

        RuleFor(x => x.BatchVoucherCode)
            .MaximumLength(5).WithMessage("Batch voucher code must not exceed 5 characters.");

        RuleFor(x => x.OnlineVoucherCode)
            .MaximumLength(5).WithMessage("Online voucher code must not exceed 5 characters.");
    }
}
