using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.CreateWithdrawalReason;

public record CreateWithdrawalReasonCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class CreateWithdrawalReasonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWithdrawalReasonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWithdrawalReasonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new WithdrawalReason
        {
            Name = request.Name,
            ShortName = request.ShortName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WithdrawalReasons.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWithdrawalReasonCommandValidator : AbstractValidator<CreateWithdrawalReasonCommand>
{
    public CreateWithdrawalReasonCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
