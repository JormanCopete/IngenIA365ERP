using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.CreateWorkRiskRate;

public record CreateWorkRiskRateCommand : IRequest<Result<Guid>>
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public decimal Rate { get; init; }
}

public class CreateWorkRiskRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkRiskRateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWorkRiskRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new WorkRiskRate
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            Rate = request.Rate,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WorkRiskRates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWorkRiskRateCommandValidator : AbstractValidator<CreateWorkRiskRateCommand>
{
    public CreateWorkRiskRateCommandValidator()
    {
        // La clase es lo que el motor traduce a ARL_CLASE_{I..V}_PCT: fuera de 1..5 no
        // existe porcentaje y el aporte no se calcularía.
        RuleFor(x => x.Code)
            .InclusiveBetween(1, 5).WithMessage("La clase de riesgo ARL va de 1 (I) a 5 (V).");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
