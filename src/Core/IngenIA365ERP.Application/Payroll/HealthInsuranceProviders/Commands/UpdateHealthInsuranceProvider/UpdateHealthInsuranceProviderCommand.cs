using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.UpdateHealthInsuranceProvider;

public record UpdateHealthInsuranceProviderCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
}

public class UpdateHealthInsuranceProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateHealthInsuranceProviderCommand, Result>
{
    public async Task<Result> Handle(
        UpdateHealthInsuranceProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.HealthInsuranceProviders
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.TaxId = request.TaxId;
        entity.CheckDigit = request.CheckDigit;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
