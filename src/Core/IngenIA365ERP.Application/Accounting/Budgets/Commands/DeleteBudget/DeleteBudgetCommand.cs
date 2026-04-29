using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets.Commands.DeleteBudget;

public record DeleteBudgetCommand(Guid PublicId) : IRequest<Result>;

public class DeleteBudgetCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteBudgetCommand, Result>
{
    public async Task<Result> Handle(DeleteBudgetCommand request, CancellationToken ct)
    {
        var budget = await context.Budgets.FirstOrDefaultAsync(
            b => b.PublicId == request.PublicId && !b.IsDeleted, ct);
        if (budget is null)
            return Result.Failure(new Error("Budget.NotFound",
                "Presupuesto no encontrado."));

        // Soft delete
        budget.IsDeleted = true;
        budget.DeletedAt = dateTime.UtcNow;
        budget.DeletedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class DeleteBudgetCommandValidator : AbstractValidator<DeleteBudgetCommand>
{
    public DeleteBudgetCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id del presupuesto requerido.");
    }
}
