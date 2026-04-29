using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.DeletePayrollDeductionConcept;

public record DeletePayrollDeductionConceptCommand(Guid PublicId) : IRequest<Result>;

public class DeletePayrollDeductionConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePayrollDeductionConceptCommand, Result>
{
    public async Task<Result> Handle(
        DeletePayrollDeductionConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayrollDeductionConcepts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
