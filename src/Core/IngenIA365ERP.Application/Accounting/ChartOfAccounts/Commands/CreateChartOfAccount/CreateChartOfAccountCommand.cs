using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.CreateChartOfAccount;

public record CreateChartOfAccountCommand : IRequest<Result<Guid>>
{
    public string AccountCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public byte Level { get; init; }
    public string Nature { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}

public class CreateChartOfAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateChartOfAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateChartOfAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ChartOfAccount
        {
            AccountCode = request.AccountCode,
            Name = request.Name,
            Level = request.Level,
            Nature = request.Nature,
            Rate = request.Rate,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ChartOfAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateChartOfAccountCommandValidator : AbstractValidator<CreateChartOfAccountCommand>
{
    public CreateChartOfAccountCommandValidator()
    {
        RuleFor(x => x.AccountCode)
            .NotEmpty().WithMessage("Account code is required.")
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Nature)
            .NotEmpty().WithMessage("Nature is required.")
            .MaximumLength(1).WithMessage("Nature must be 1 character (D or C).");
    }
}
