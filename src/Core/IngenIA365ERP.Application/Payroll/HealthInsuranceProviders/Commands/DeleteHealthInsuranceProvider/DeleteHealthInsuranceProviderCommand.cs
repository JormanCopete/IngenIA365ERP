using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.DeleteHealthInsuranceProvider;

public record DeleteHealthInsuranceProviderCommand(Guid PublicId) : IRequest<Result>;

public class DeleteHealthInsuranceProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteHealthInsuranceProviderCommand, Result>
{
    public async Task<Result> Handle(
        DeleteHealthInsuranceProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.HealthInsuranceProviders
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
