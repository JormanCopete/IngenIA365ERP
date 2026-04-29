using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.AccountGroups.Commands.CreateAccountGroup;

public record CreateAccountGroupCommand : IRequest<Result<Guid>>
{
    public int? GroupNumber { get; init; }
    public int? GroupType { get; init; }
    public string? AccountCode { get; init; }
    public string? Description { get; init; }
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
    public int? ParentGroupId { get; init; }
}

public class CreateAccountGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAccountGroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateAccountGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new AccountGroup
        {
            GroupNumber = request.GroupNumber,
            GroupType = request.GroupType,
            AccountCode = request.AccountCode,
            Description = request.Description,
            ReportOrder = request.ReportOrder,
            Level = request.Level,
            ParentGroupId = request.ParentGroupId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AccountGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateAccountGroupCommandValidator : AbstractValidator<CreateAccountGroupCommand>
{
    public CreateAccountGroupCommandValidator()
    {
        RuleFor(x => x.AccountCode)
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");
    }
}
