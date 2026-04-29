using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets.Commands.UpdateBudget;

public record UpdateBudgetCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public decimal JanBudget { get; init; }
    public decimal FebBudget { get; init; }
    public decimal MarBudget { get; init; }
    public decimal AprBudget { get; init; }
    public decimal MayBudget { get; init; }
    public decimal JunBudget { get; init; }
    public decimal JulBudget { get; init; }
    public decimal AugBudget { get; init; }
    public decimal SepBudget { get; init; }
    public decimal OctBudget { get; init; }
    public decimal NovBudget { get; init; }
    public decimal DecBudget { get; init; }
}

public class UpdateBudgetCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateBudgetCommand, Result>
{
    public async Task<Result> Handle(UpdateBudgetCommand request, CancellationToken ct)
    {
        var budget = await context.Budgets.FirstOrDefaultAsync(
            b => b.PublicId == request.PublicId && !b.IsDeleted, ct);
        if (budget is null)
            return Result.Failure(new Error("Budget.NotFound",
                "Presupuesto no encontrado."));

        // Validate the fiscal year is not completely closed
        var period = await context.AccountingPeriods.AsNoTracking()
            .Where(p => p.Year == budget.PeriodYear && p.ModuleCode == "CNT" && !p.IsDeleted)
            .AnyAsync(p => p.Status != "C", ct);
        if (!period)
            return Result.Failure(new Error("Budget.YearClosed",
                "Todos los periodos del anio estan cerrados. No se puede modificar el presupuesto."));

        budget.JanBudget = request.JanBudget;
        budget.FebBudget = request.FebBudget;
        budget.MarBudget = request.MarBudget;
        budget.AprBudget = request.AprBudget;
        budget.MayBudget = request.MayBudget;
        budget.JunBudget = request.JunBudget;
        budget.JulBudget = request.JulBudget;
        budget.AugBudget = request.AugBudget;
        budget.SepBudget = request.SepBudget;
        budget.OctBudget = request.OctBudget;
        budget.NovBudget = request.NovBudget;
        budget.DecBudget = request.DecBudget;

        budget.TotalBudget = request.JanBudget + request.FebBudget + request.MarBudget
                           + request.AprBudget + request.MayBudget + request.JunBudget
                           + request.JulBudget + request.AugBudget + request.SepBudget
                           + request.OctBudget + request.NovBudget + request.DecBudget;

        budget.UpdatedAt = dateTime.UtcNow;
        budget.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateBudgetCommandValidator : AbstractValidator<UpdateBudgetCommand>
{
    public UpdateBudgetCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id del presupuesto requerido.");

        RuleFor(x => x.JanBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FebBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MarBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AprBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MayBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.JunBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.JulBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AugBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SepBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OctBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NovBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DecBudget).GreaterThanOrEqualTo(0);
    }
}
