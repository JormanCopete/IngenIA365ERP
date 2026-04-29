using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.UpdatePayrollConcept;

public record UpdatePayrollConceptCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ConceptCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int ConceptClass { get; init; }
    public int Nature { get; init; }
    public decimal Value { get; init; }
    public decimal Factor { get; init; }
    public int Base { get; init; }
    public int AffectsSalary { get; init; }
    public string? DaysComputed { get; init; }
    public int LiquidationBase { get; init; }
    public decimal TopSalary { get; init; }
    public int AffectsBenefits { get; init; }
    public int AffectsWithholding { get; init; }
    public int IsBenefit { get; init; }
    public decimal ProvisionRate { get; init; }
    public int ProvisionBase { get; init; }
    public int AffectsSeverance { get; init; }
    public int AffectsBonus { get; init; }
    public int AffectsVacation { get; init; }
    public int AffectsIndemnity { get; init; }
    public int ConceptSubClass { get; init; }
}

public class UpdatePayrollConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePayrollConceptCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePayrollConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayrollConcepts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ConceptCode = request.ConceptCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.ConceptClass = request.ConceptClass;
        entity.Nature = request.Nature;
        entity.Value = request.Value;
        entity.Factor = request.Factor;
        entity.Base = request.Base;
        entity.AffectsSalary = request.AffectsSalary;
        entity.DaysComputed = request.DaysComputed ?? string.Empty;
        entity.LiquidationBase = request.LiquidationBase;
        entity.TopSalary = request.TopSalary;
        entity.AffectsBenefits = request.AffectsBenefits;
        entity.AffectsWithholding = request.AffectsWithholding;
        entity.IsBenefit = request.IsBenefit;
        entity.ProvisionRate = request.ProvisionRate;
        entity.ProvisionBase = request.ProvisionBase;
        entity.AffectsSeverance = request.AffectsSeverance;
        entity.AffectsBonus = request.AffectsBonus;
        entity.AffectsVacation = request.AffectsVacation;
        entity.AffectsIndemnity = request.AffectsIndemnity;
        entity.ConceptSubClass = request.ConceptSubClass;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
