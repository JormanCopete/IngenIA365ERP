using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.CreateAccountingPeriod;

public record CreateAccountingPeriodCommand : IRequest<Result<Guid>>
{
    public string ModuleCode { get; init; } = string.Empty;
    public int Year { get; init; }
    public byte PeriodNumber { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = "O";
}

public class CreateAccountingPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAccountingPeriodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateAccountingPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new AccountingPeriod
        {
            ModuleCode = request.ModuleCode,
            Year = request.Year,
            PeriodNumber = request.PeriodNumber,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AccountingPeriods.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateAccountingPeriodCommandValidator : AbstractValidator<CreateAccountingPeriodCommand>
{
    public CreateAccountingPeriodCommandValidator()
    {
        RuleFor(x => x.ModuleCode)
            .NotEmpty().WithMessage("Module code is required.")
            .MaximumLength(4).WithMessage("Module code must not exceed 4 characters.");

        RuleFor(x => x.Year)
            .GreaterThan(0).WithMessage("Year must be greater than 0.");

        RuleFor(x => x.PeriodNumber)
            .GreaterThan((byte)0).WithMessage("Period number must be greater than 0.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .MaximumLength(1).WithMessage("Status must be 1 character.");
    }
}
