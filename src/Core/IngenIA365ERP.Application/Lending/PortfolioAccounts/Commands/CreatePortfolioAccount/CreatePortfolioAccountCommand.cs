using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.PortfolioAccounts.Commands.CreatePortfolioAccount;

public record CreatePortfolioAccountCommand : IRequest<Result<Guid>>
{
    public string AccountCode { get; init; } = string.Empty;
    public string RecordClass { get; init; } = string.Empty;
    public int Category { get; init; }
    public int GuaranteeType { get; init; }
    public int DeductionClass { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
}

public class CreatePortfolioAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePortfolioAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePortfolioAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PortfolioAccount
        {
            AccountCode = request.AccountCode,
            RecordClass = request.RecordClass,
            Category = request.Category,
            GuaranteeType = request.GuaranteeType,
            DeductionClass = request.DeductionClass,
            RiskLevel = request.RiskLevel,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PortfolioAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePortfolioAccountCommandValidator : AbstractValidator<CreatePortfolioAccountCommand>
{
    public CreatePortfolioAccountCommandValidator()
    {
        RuleFor(x => x.AccountCode)
            .MaximumLength(15).WithMessage("AccountCode must not exceed 15 characters.");

        RuleFor(x => x.RecordClass)
            .MaximumLength(3).WithMessage("RecordClass must not exceed 3 characters.");

        RuleFor(x => x.RiskLevel)
            .MaximumLength(2).WithMessage("RiskLevel must not exceed 2 characters.");
    }
}
