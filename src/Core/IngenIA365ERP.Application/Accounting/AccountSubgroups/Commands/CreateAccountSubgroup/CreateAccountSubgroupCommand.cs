using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.AccountSubgroups.Commands.CreateAccountSubgroup;

public record CreateAccountSubgroupCommand : IRequest<Result<Guid>>
{
    public int? GroupId { get; init; }
    public int SubgroupNumber { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
}

public class CreateAccountSubgroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAccountSubgroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateAccountSubgroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new AccountSubgroup
        {
            GroupId = request.GroupId,
            SubgroupNumber = request.SubgroupNumber,
            AccountCode = request.AccountCode,
            Description = request.Description,
            ReportOrder = request.ReportOrder,
            Level = request.Level,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AccountSubgroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateAccountSubgroupCommandValidator : AbstractValidator<CreateAccountSubgroupCommand>
{
    public CreateAccountSubgroupCommandValidator()
    {
        RuleFor(x => x.AccountCode)
            .NotEmpty().WithMessage("Account code is required.")
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");
    }
}
