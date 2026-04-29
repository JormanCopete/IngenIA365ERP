using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.DeletePayrollConcept;

public record DeletePayrollConceptCommand(Guid PublicId) : IRequest<Result>;

public class DeletePayrollConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePayrollConceptCommand, Result>
{
    public async Task<Result> Handle(
        DeletePayrollConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayrollConcepts
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
