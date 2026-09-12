using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Relationships.Commands.UpdateRelationship;

public record UpdateRelationshipCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class UpdateRelationshipCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateRelationshipCommand, Result>
{
    public async Task<Result> Handle(
        UpdateRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Relationships
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Relationships.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && e.Id != entity.Id && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure(CodigoDeCatalogo.Duplicado("un parentesco", codigo, repetido.Name));
        }
        entity.LegacyCode = codigo;

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
