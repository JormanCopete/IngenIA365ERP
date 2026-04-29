using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.UpdateDebitAgreementParameter;

public record UpdateDebitAgreementParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string AgreementCode { get; init; } = string.Empty;
    public int TokenRS { get; init; }
    public string? Name { get; init; }
}

public class UpdateDebitAgreementParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDebitAgreementParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDebitAgreementParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DebitAgreementParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.AgreementCode = request.AgreementCode;
        entity.TokenRS = request.TokenRS;
        entity.Name = request.Name;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
