using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.DeleteDianReportFormat;

public record DeleteDianReportFormatCommand(Guid PublicId) : IRequest<Result>;

public class DeleteDianReportFormatCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteDianReportFormatCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDianReportFormatCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DianReportFormats
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
