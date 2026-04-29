using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.TransactionCodes.Commands.CreateTransactionCode;

public record CreateTransactionCodeCommand : IRequest<Result<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string AccountCode { get; init; } = string.Empty;
    public string AdjustAccrual { get; init; } = string.Empty;
    public string DebitCreditFlag { get; init; } = string.Empty;
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public int SourceId { get; init; }
}

public class CreateTransactionCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTransactionCodeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateTransactionCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new TransactionCode
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            TransactionType = request.TransactionType,
            AccountCode = request.AccountCode,
            AdjustAccrual = request.AdjustAccrual,
            DebitCreditFlag = request.DebitCreditFlag,
            FormatId = request.FormatId,
            ConceptId = request.ConceptId,
            SourceId = request.SourceId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.TransactionCodes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateTransactionCodeCommandValidator : AbstractValidator<CreateTransactionCodeCommand>
{
    public CreateTransactionCodeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(3).WithMessage("Code must not exceed 3 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must not exceed 50 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(25).WithMessage("Short name must not exceed 25 characters.");

        RuleFor(x => x.TransactionType)
            .MaximumLength(3).WithMessage("Transaction type must not exceed 3 characters.");

        RuleFor(x => x.AccountCode)
            .MaximumLength(15).WithMessage("Account code must not exceed 15 characters.");

        RuleFor(x => x.DebitCreditFlag)
            .MaximumLength(2).WithMessage("Debit/Credit flag must not exceed 2 characters.");
    }
}
