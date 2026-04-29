using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.UpdateTaxFormCode;

public record UpdateTaxFormCodeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string? FormCode { get; init; }
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public string? ConceptCode { get; init; }
    public string? TaxType { get; init; }
}

public class UpdateTaxFormCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTaxFormCodeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTaxFormCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TaxFormCodes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.FormCode = request.FormCode;
        entity.Description = request.Description;
        entity.AccountCode = request.AccountCode;
        entity.ConceptCode = request.ConceptCode;
        entity.TaxType = request.TaxType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
