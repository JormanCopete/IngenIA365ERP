using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtParameters.Commands.UpdateCdtParameter;

public record UpdateCdtParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int CreditLineId { get; init; }
    public string? Description { get; init; }
    public decimal? MinimumRate { get; init; }
    public decimal? AnnualRate { get; init; }
    public decimal? WithholdingRate { get; init; }
    public decimal? MinWithholdingAmount { get; init; }
    public int? InterestConceptId { get; init; }
    public int? WithholdingConceptId { get; init; }
    public decimal? MonthlyIncrement { get; init; }
    public decimal? InterestRate { get; init; }
    public int? Term { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int InterestPaymentType { get; init; }
    public string InterestType { get; init; } = "S";
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public int SourceId { get; init; }
    public string? TreasuryAccount { get; init; }
}

public class UpdateCdtParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCdtParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCdtParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CdtParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CreditLineId = request.CreditLineId;
        entity.Description = request.Description;
        entity.MinimumRate = request.MinimumRate;
        entity.AnnualRate = request.AnnualRate;
        entity.WithholdingRate = request.WithholdingRate;
        entity.MinWithholdingAmount = request.MinWithholdingAmount;
        entity.InterestConceptId = request.InterestConceptId;
        entity.WithholdingConceptId = request.WithholdingConceptId;
        entity.MonthlyIncrement = request.MonthlyIncrement;
        entity.InterestRate = request.InterestRate;
        entity.Term = request.Term;
        entity.MinAmount = request.MinAmount;
        entity.MaxAmount = request.MaxAmount;
        entity.InterestPaymentType = request.InterestPaymentType;
        entity.InterestType = request.InterestType;
        entity.FormatId = request.FormatId;
        entity.ConceptId = request.ConceptId;
        entity.SourceId = request.SourceId;
        entity.TreasuryAccount = request.TreasuryAccount;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
