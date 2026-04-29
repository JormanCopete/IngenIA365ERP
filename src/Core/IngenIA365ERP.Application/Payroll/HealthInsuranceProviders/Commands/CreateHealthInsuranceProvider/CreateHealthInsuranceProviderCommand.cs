using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.CreateHealthInsuranceProvider;

public record CreateHealthInsuranceProviderCommand : IRequest<Result<Guid>>
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
}

public class CreateHealthInsuranceProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateHealthInsuranceProviderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateHealthInsuranceProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new HealthInsuranceProvider
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            TaxId = request.TaxId,
            CheckDigit = request.CheckDigit,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.HealthInsuranceProviders.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateHealthInsuranceProviderCommandValidator : AbstractValidator<CreateHealthInsuranceProviderCommand>
{
    public CreateHealthInsuranceProviderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");

        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");
    }
}
