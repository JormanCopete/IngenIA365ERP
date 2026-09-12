using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.UpdateWithholdingCause;

public record UpdateWithholdingCauseCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int IndemnityType { get; init; }
    public int AutoDeductions { get; init; }
}

public class UpdateWithholdingCauseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithholdingCauseCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWithholdingCauseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingCauses
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await context.WithholdingCauses.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == codigo && e.Id != entity.Id, cancellationToken);
        if (repetido is not null)
            return Result.Failure(CodigoDeCatalogo.Duplicado("una causa de retención", codigo, repetido.Name));

        entity.Code = codigo;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.IndemnityType = request.IndemnityType;
        entity.AutoDeductions = request.AutoDeductions;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
