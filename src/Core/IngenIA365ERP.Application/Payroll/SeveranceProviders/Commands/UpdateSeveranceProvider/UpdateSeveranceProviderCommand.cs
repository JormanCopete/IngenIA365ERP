using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.SeveranceProviders.Commands.UpdateSeveranceProvider;

public record UpdateSeveranceProviderCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
}

public class UpdateSeveranceProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSeveranceProviderCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSeveranceProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SeveranceProviders
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await context.SeveranceProviders.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == codigo && e.Id != entity.Id, cancellationToken);
        if (repetido is not null)
            return Result.Failure(CodigoDeCatalogo.Duplicado("un fondo de cesantías", codigo, repetido.Name));

        entity.Code = codigo;
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
