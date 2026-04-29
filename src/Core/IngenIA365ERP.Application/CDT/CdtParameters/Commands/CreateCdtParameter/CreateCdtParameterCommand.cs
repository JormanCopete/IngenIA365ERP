using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;

namespace IngenIA365ERP.Application.CDT.CdtParameters.Commands.CreateCdtParameter;

public record CreateCdtParameterCommand : IRequest<Result<Guid>>
{
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

public class CreateCdtParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCdtParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCdtParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new CdtParameter
        {
            CreditLineId = request.CreditLineId,
            Description = request.Description,
            MinimumRate = request.MinimumRate,
            AnnualRate = request.AnnualRate,
            WithholdingRate = request.WithholdingRate,
            MinWithholdingAmount = request.MinWithholdingAmount,
            InterestConceptId = request.InterestConceptId,
            WithholdingConceptId = request.WithholdingConceptId,
            MonthlyIncrement = request.MonthlyIncrement,
            InterestRate = request.InterestRate,
            Term = request.Term,
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            InterestPaymentType = request.InterestPaymentType,
            InterestType = request.InterestType,
            FormatId = request.FormatId,
            ConceptId = request.ConceptId,
            SourceId = request.SourceId,
            TreasuryAccount = request.TreasuryAccount,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CdtParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCdtParameterCommandValidator : AbstractValidator<CreateCdtParameterCommand>
{
    public CreateCdtParameterCommandValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");

        RuleFor(x => x.InterestType)
            .MaximumLength(2).WithMessage("Interest type must not exceed 2 characters.");

        RuleFor(x => x.TreasuryAccount)
            .MaximumLength(15).WithMessage("Treasury account must not exceed 15 characters.");
    }
}
