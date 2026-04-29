using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.CreateWithdrawalStatus;

public record CreateWithdrawalStatusCommand : IRequest<Result<Guid>>
{
    public string PersonCode { get; init; } = string.Empty;
    public DateOnly RequestDate { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? EffectiveDate { get; init; }
    public string? Remarks { get; init; }
    public int Period { get; init; }
}

public class CreateWithdrawalStatusCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWithdrawalStatusCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWithdrawalStatusCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new WithdrawalStatus
        {
            PersonCode = request.PersonCode,
            RequestDate = request.RequestDate,
            ReasonCode = request.ReasonCode,
            Status = request.Status,
            EffectiveDate = request.EffectiveDate,
            Remarks = request.Remarks,
            Period = request.Period,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WithdrawalStatuses.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWithdrawalStatusCommandValidator : AbstractValidator<CreateWithdrawalStatusCommand>
{
    public CreateWithdrawalStatusCommandValidator()
    {
        RuleFor(x => x.PersonCode)
            .NotEmpty().WithMessage("PersonCode is required.")
            .MaximumLength(20).WithMessage("PersonCode must not exceed 20 characters.");

        RuleFor(x => x.ReasonCode)
            .MaximumLength(5).WithMessage("ReasonCode must not exceed 5 characters.");

        RuleFor(x => x.Status)
            .MaximumLength(2).WithMessage("Status must not exceed 2 characters.");
    }
}
