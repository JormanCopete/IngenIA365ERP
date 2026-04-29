using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.CreateWithholdingCause;

public record CreateWithholdingCauseCommand : IRequest<Result<Guid>>
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int IndemnityType { get; init; }
    public int AutoDeductions { get; init; }
}

public class CreateWithholdingCauseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWithholdingCauseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWithholdingCauseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new WithholdingCause
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            IndemnityType = request.IndemnityType,
            AutoDeductions = request.AutoDeductions,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WithholdingCauses.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWithholdingCauseCommandValidator : AbstractValidator<CreateWithholdingCauseCommand>
{
    public CreateWithholdingCauseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
