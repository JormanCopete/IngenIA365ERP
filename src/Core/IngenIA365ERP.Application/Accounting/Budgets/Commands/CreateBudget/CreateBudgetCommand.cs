using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets.Commands.CreateBudget;

public record CreateBudgetCommand : IRequest<Result<Guid>>
{
    public Guid AccountPublicId { get; init; }
    public int Year { get; init; }
    public Guid? BranchPublicId { get; init; }
    public Guid? CostCenterPublicId { get; init; }
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

public class CreateBudgetCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateBudgetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBudgetCommand request, CancellationToken ct)
    {
        // 1. Resolve account
        var account = await context.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == request.AccountPublicId && !a.IsDeleted, ct);
        if (account is null)
            return Result.Failure<Guid>(new Error("Budget.AccountNotFound",
                "Cuenta contable no encontrada."));

        // 2. Resolve branch (default to first if not provided)
        int branchId = 0;
        if (request.BranchPublicId.HasValue)
        {
            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId.Value && !b.IsDeleted, ct);
            if (branch is not null) branchId = branch.Id;
        }
        else
        {
            var defaultBranch = await context.Branches.AsNoTracking()
                .Where(b => !b.IsDeleted).OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
            if (defaultBranch is not null) branchId = defaultBranch.Id;
        }

        // 3. Resolve cost center (default to first if not provided)
        int costCenterId = 0;
        if (request.CostCenterPublicId.HasValue)
        {
            var cc = await context.CostCenters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CostCenterPublicId.Value && !c.IsDeleted, ct);
            if (cc is not null) costCenterId = cc.Id;
        }
        else
        {
            var defaultCc = await context.CostCenters.AsNoTracking()
                .Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (defaultCc is not null) costCenterId = defaultCc.Id;
        }

        // 4. Check for duplicate budget
        var existing = await context.Budgets.FirstOrDefaultAsync(
            b => b.AccountId == account.Id
              && b.PeriodYear == request.Year
              && b.BranchId == branchId
              && b.CostCenterId == costCenterId
              && !b.IsDeleted, ct);
        if (existing is not null)
            return Result.Failure<Guid>(new Error("Budget.AlreadyExists",
                "Ya existe un presupuesto para esta cuenta, sucursal, centro de costo y anio."));

        // 5. Calculate total
        var total = request.JanBudget + request.FebBudget + request.MarBudget
                  + request.AprBudget + request.MayBudget + request.JunBudget
                  + request.JulBudget + request.AugBudget + request.SepBudget
                  + request.OctBudget + request.NovBudget + request.DecBudget;

        // 6. Create budget
        var budget = new Budget
        {
            AccountId = account.Id,
            PeriodYear = request.Year,
            BranchId = branchId,
            CostCenterId = costCenterId,
            JanBudget = request.JanBudget,
            FebBudget = request.FebBudget,
            MarBudget = request.MarBudget,
            AprBudget = request.AprBudget,
            MayBudget = request.MayBudget,
            JunBudget = request.JunBudget,
            JulBudget = request.JulBudget,
            AugBudget = request.AugBudget,
            SepBudget = request.SepBudget,
            OctBudget = request.OctBudget,
            NovBudget = request.NovBudget,
            DecBudget = request.DecBudget,
            TotalBudget = total,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Budgets.Add(budget);
        await context.SaveChangesAsync(ct);
        return Result.Success(budget.PublicId);
    }
}

public class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetCommandValidator()
    {
        RuleFor(x => x.AccountPublicId)
            .NotEmpty().WithMessage("Cuenta contable requerida.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Anio invalido.");

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
