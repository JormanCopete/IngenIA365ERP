using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.CostCenters.Commands.CreateCostCenter;

public record CreateCostCenterCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 20); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? CompanyTaxId { get; init; }
    public short PayrollType { get; init; }
    public short Period { get; init; }
    public short PayrollPeriodicity { get; init; }
    public string? PayrollStatus { get; init; }
}

public class CreateCostCenterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCostCenterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCostCenterCommand request,
        CancellationToken cancellationToken)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.CostCenters.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un centro de costo", codigo, repetido.Name));
        }

        var entity = new CostCenter
        {
            LegacyCode = codigo,
            Name = request.Name,
            CompanyName = request.CompanyName,
            CompanyTaxId = request.CompanyTaxId,
            PayrollType = request.PayrollType,
            Period = request.Period,
            PayrollPeriodicity = request.PayrollPeriodicity,
            PayrollStatus = request.PayrollStatus,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CostCenters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCostCenterCommandValidator : AbstractValidator<CreateCostCenterCommand>
{
    public CreateCostCenterCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoLargo).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoLargo} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.CompanyName)
            .MaximumLength(100).WithMessage("Company name must not exceed 100 characters.");

        RuleFor(x => x.CompanyTaxId)
            .MaximumLength(20).WithMessage("Company tax ID must not exceed 20 characters.");

        RuleFor(x => x.PayrollStatus)
            .MaximumLength(30).WithMessage("Payroll status must not exceed 30 characters.");
    }
}
