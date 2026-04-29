using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.UpdateDianReportFormat;

public record UpdateDianReportFormatCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public string? FormatCode { get; init; }
    public string? Description { get; init; }
    public decimal Threshold { get; init; }
    public decimal BalanceThreshold { get; init; }
}

public class UpdateDianReportFormatCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDianReportFormatCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDianReportFormatCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DianReportFormats
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.FormatId = request.FormatId;
        entity.ConceptId = request.ConceptId;
        entity.FormatCode = request.FormatCode;
        entity.Description = request.Description;
        entity.Threshold = request.Threshold;
        entity.BalanceThreshold = request.BalanceThreshold;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
