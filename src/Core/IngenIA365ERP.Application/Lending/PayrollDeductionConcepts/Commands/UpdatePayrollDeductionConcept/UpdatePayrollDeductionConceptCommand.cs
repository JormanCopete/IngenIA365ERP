using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.UpdatePayrollDeductionConcept;

public record UpdatePayrollDeductionConceptCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string CompanyCode { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
    public string CostCenterId { get; init; } = string.Empty;
    public int CreditLineId { get; init; }
    public string PayrollConceptCode { get; init; } = string.Empty;
    public string InterestConceptCode { get; init; } = string.Empty;
    public string ExtraConceptCode { get; init; } = string.Empty;
}

public class UpdatePayrollDeductionConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePayrollDeductionConceptCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePayrollDeductionConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayrollDeductionConcepts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CompanyCode = request.CompanyCode;
        entity.BranchId = request.BranchId;
        entity.CostCenterId = request.CostCenterId;
        entity.CreditLineId = request.CreditLineId;
        entity.PayrollConceptCode = request.PayrollConceptCode;
        entity.InterestConceptCode = request.InterestConceptCode;
        entity.ExtraConceptCode = request.ExtraConceptCode;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
