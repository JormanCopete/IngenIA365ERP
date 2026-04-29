using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.UpdateIcaTaxLine;

public record UpdateIcaTaxLineCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string LineCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public decimal TaxRate { get; init; }
    public string? BaseAccountCode { get; init; }
    public string? Sign { get; init; }
}

public class UpdateIcaTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateIcaTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        UpdateIcaTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.IcaTaxLines
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
