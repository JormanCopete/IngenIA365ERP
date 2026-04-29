using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Shifts.Commands.CreateShift;

public record CreateShiftCommand : IRequest<Result<Guid>>
{
    public int ShiftCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}

public class CreateShiftCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateShiftCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateShiftCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Shift
        {
            ShiftCode = request.ShiftCode,
            Name = request.Name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Shifts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateShiftCommandValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must not exceed 50 characters.");

        RuleFor(x => x.StartTime)
            .MaximumLength(10).WithMessage("Start time must not exceed 10 characters.");

        RuleFor(x => x.EndTime)
            .MaximumLength(10).WithMessage("End time must not exceed 10 characters.");
    }
}
