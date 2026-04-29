using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Commands.UpdateWithholdingTaxLine;

public record UpdateWithholdingTaxLineCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string LineCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public decimal TaxRate { get; init; }
    public string? BaseAccountCode { get; init; }
    public string? Sign { get; init; }
}

public class UpdateWithholdingTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithholdingTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWithholdingTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingTaxLines
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.LineCode = request.LineCode;
        entity.Description = request.Description;
        entity.AccountCode = request.AccountCode;
        entity.TaxRate = request.TaxRate;
        entity.BaseAccountCode = request.BaseAccountCode;
        entity.Sign = request.Sign;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
