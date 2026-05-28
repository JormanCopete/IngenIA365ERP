using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Companies.Commands.DeleteCompany;

public record DeleteCompanyCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCompanyCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCompanyCommand, Result>
{
    public async Task<Result> Handle(DeleteCompanyCommand request, CancellationToken ct)
    {
        var entity = await context.Companies
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Company.NotFound", "Empresa no encontrada."));

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
