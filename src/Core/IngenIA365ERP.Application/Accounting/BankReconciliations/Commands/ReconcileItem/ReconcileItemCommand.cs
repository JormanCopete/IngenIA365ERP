using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.BankReconciliations.Commands.ReconcileItem;

public record ReconcileItemCommand(Guid PublicId, bool IsReconciled) : IRequest<Result>;

public class ReconcileItemCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ReconcileItemCommand, Result>
{
    public async Task<Result> Handle(ReconcileItemCommand request, CancellationToken ct)
    {
        // 1. Find the reconciliation item
        var item = await context.BankReconciliations.FirstOrDefaultAsync(
            r => r.PublicId == request.PublicId && !r.IsDeleted, ct);
        if (item is null)
            return Result.Failure(new Error("BankReconciliation.ItemNotFound",
                "Partida de conciliacion no encontrada."));

        // 2. Validate item is not in a closed reconciliation
        if (item.IsClosed)
            return Result.Failure(new Error("BankReconciliation.ItemClosed",
                "La partida pertenece a una conciliacion cerrada."));

        // 3. Validate the master reconciliation is still open
        var master = await context.BankReconciliationMasters.FirstOrDefaultAsync(
            m => m.AccountId == item.AccountId
              && m.PeriodCode == (item.PeriodCode.HasValue ? item.PeriodCode.Value.ToString() : "")
              && !m.IsDeleted, ct);
        if (master is not null && master.IsClosed)
            return Result.Failure(new Error("BankReconciliation.MasterClosed",
                "La conciliacion bancaria esta cerrada."));

        // 4. Toggle reconciliation status
        var wasReconciled = item.IsReconciled;
        item.IsReconciled = request.IsReconciled;
        item.ReconciliationDate = request.IsReconciled ? DateOnly.FromDateTime(dateTime.UtcNow) : null;
        item.UpdatedAt = dateTime.UtcNow;
        item.UpdatedBy = currentUser.UserName;

        // 5. Update master final balance based on reconciliation change
        if (master is not null)
        {
            var netAmount = item.DebitAmount - item.CreditAmount;
            if (request.IsReconciled && !wasReconciled)
            {
                // Newly reconciled: add to final balance
                master.FinalBalance += netAmount;
            }
            else if (!request.IsReconciled && wasReconciled)
            {
                // Un-reconciled: subtract from final balance
                master.FinalBalance -= netAmount;
            }
            master.UpdatedAt = dateTime.UtcNow;
            master.UpdatedBy = currentUser.UserName;
        }

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class ReconcileItemCommandValidator : AbstractValidator<ReconcileItemCommand>
{
    public ReconcileItemCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id de la partida requerido.");
    }
}
