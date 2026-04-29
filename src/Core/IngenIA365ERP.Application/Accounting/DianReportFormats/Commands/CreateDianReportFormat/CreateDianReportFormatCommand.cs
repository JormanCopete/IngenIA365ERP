using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.CreateDianReportFormat;

public record CreateDianReportFormatCommand : IRequest<Result<Guid>>
{
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public string? FormatCode { get; init; }
    public string? Description { get; init; }
    public decimal Threshold { get; init; }
    public decimal BalanceThreshold { get; init; }
}

public class CreateDianReportFormatCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDianReportFormatCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDianReportFormatCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new DianReportFormat
        {
            FormatId = request.FormatId,
            ConceptId = request.ConceptId,
            FormatCode = request.FormatCode,
            Description = request.Description,
            Threshold = request.Threshold,
            BalanceThreshold = request.BalanceThreshold,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DianReportFormats.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDianReportFormatCommandValidator : AbstractValidator<CreateDianReportFormatCommand>
{
    public CreateDianReportFormatCommandValidator()
    {
        RuleFor(x => x.FormatCode)
            .MaximumLength(10).WithMessage("Format code must not exceed 10 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");
    }
}
