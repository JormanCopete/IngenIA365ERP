using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.UpdateConceptAccount;

public record UpdateConceptAccountCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ConceptId { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public string ExpenseAccountCode { get; init; } = string.Empty;
    public string CounterAccountCode { get; init; } = string.Empty;
    public string ProvisionAccountCode { get; init; } = string.Empty;
}

public class UpdateConceptAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateConceptAccountCommand, Result>
{
    public async Task<Result> Handle(
        UpdateConceptAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ConceptAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ConceptId = request.ConceptId;
        entity.CostCenterId = request.CostCenterId;
        entity.ExpenseAccountCode = request.ExpenseAccountCode;
        entity.CounterAccountCode = request.CounterAccountCode;
        entity.ProvisionAccountCode = request.ProvisionAccountCode;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
