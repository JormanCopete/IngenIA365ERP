using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.TransactionCodes.Commands.UpdateTransactionCode;

public record UpdateTransactionCodeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string AccountCode { get; init; } = string.Empty;
    public string AdjustAccrual { get; init; } = string.Empty;
    public string DebitCreditFlag { get; init; } = string.Empty;
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public int SourceId { get; init; }
}

public class UpdateTransactionCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTransactionCodeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTransactionCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TransactionCodes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.TransactionType = request.TransactionType;
        entity.AccountCode = request.AccountCode;
        entity.AdjustAccrual = request.AdjustAccrual;
        entity.DebitCreditFlag = request.DebitCreditFlag;
        entity.FormatId = request.FormatId;
        entity.ConceptId = request.ConceptId;
        entity.SourceId = request.SourceId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
