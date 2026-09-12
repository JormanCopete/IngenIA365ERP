using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.UpdateWorkRiskRate;

public record UpdateWorkRiskRateCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public decimal Rate { get; init; }
}

public class UpdateWorkRiskRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkRiskRateCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWorkRiskRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WorkRiskRates
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.Rate = request.Rate;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class UpdateWorkRiskRateCommandValidator : AbstractValidator<UpdateWorkRiskRateCommand>
{
    public UpdateWorkRiskRateCommandValidator()
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
