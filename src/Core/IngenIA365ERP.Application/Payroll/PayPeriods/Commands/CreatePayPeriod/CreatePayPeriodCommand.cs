using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Commands.CreatePayPeriod;

public record CreatePayPeriodCommand : IRequest<Result<Guid>>
{
    public int PlanId { get; init; }
    public int PayrollCompanyId { get; init; }
    public string? Description { get; init; }
    public string? PayDate { get; init; }
    public string? LiquidationCompanyId { get; init; }
    public int? CycleMonth { get; init; }
    public int? CycleHours { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? Periodicity { get; init; }
    public int Status { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public int PeriodId { get; init; }
}

public class CreatePayPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePayPeriodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePayPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PayPeriod
        {
            PlanId = request.PlanId,
            PayrollCompanyId = request.PayrollCompanyId,
            Description = request.Description,
            PayDate = request.PayDate,
            LiquidationCompanyId = request.LiquidationCompanyId,
            CycleMonth = request.CycleMonth,
            CycleHours = request.CycleHours,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Periodicity = request.Periodicity,
            Status = request.Status,
            StatusMessage = request.StatusMessage,
            PeriodId = request.PeriodId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PayPeriods.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePayPeriodCommandValidator : AbstractValidator<CreatePayPeriodCommand>
{
    public CreatePayPeriodCommandValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be on or after start date.");

        RuleFor(x => x.StatusMessage)
            .MaximumLength(100).WithMessage("Status message must not exceed 100 characters.");
    }
}
