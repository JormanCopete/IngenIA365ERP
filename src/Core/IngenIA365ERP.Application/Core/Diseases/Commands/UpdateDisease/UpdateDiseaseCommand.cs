using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Diseases.Commands.UpdateDisease;

public record UpdateDiseaseCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public class UpdateDiseaseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDiseaseCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDiseaseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Diseases
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Name = request.Name;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
