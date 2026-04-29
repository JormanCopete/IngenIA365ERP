using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.UpdateWithholdingParameter;

public record UpdateWithholdingParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int PayrollCompanyId { get; init; }
    public int UvtRangeStart { get; init; }
    public int UvtRangeEnd { get; init; }
    public decimal Rate { get; init; }
    public int AdditionalUvt { get; init; }
}

public class UpdateWithholdingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithholdingParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWithholdingParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.PayrollCompanyId = request.PayrollCompanyId;
        entity.UvtRangeStart = request.UvtRangeStart;
        entity.UvtRangeEnd = request.UvtRangeEnd;
        entity.Rate = request.Rate;
        entity.AdditionalUvt = request.AdditionalUvt;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
