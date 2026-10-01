using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.SalesChannels;

/// <summary>Alta de un canal de venta (feature 012, T216; contracts/api.md §3.8). (nuevo)</summary>
public sealed record CreateSalesChannelCommand(string Code, string Name) : IRequest<Result<SalesChannelDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateSalesChannelCommandValidator : AbstractValidator<CreateSalesChannelCommand>
{
    public CreateSalesChannelCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class CreateSalesChannelCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateSalesChannelCommand, Result<SalesChannelDto>>
{
    public async Task<Result<SalesChannelDto>> Handle(CreateSalesChannelCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.SalesChannels.Where(c => c.Code == codigo).Select(c => new { c.PublicId, c.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<SalesChannelDto>(CodigoDeCatalogo.Duplicado("un canal de venta", codigo, existente.Name, existente.PublicId));

        var canal = new SalesChannel { Code = codigo, Name = request.Name.Trim(), IsActive = true };
        db.SalesChannels.Add(canal);
        await db.SaveChangesAsync(ct);
        return Result.Success(new SalesChannelDto(canal.PublicId, canal.Code, canal.Name, canal.IsActive));
    }
}

/// <summary>Renombrar un canal (T216; §3.8, <c>PUT /{id}</c>); el código no cambia. (nuevo)</summary>
public sealed record UpdateSalesChannelCommand(Guid SalesChannelPublicId, string Name) : IRequest<Result<SalesChannelDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateSalesChannelCommandValidator : AbstractValidator<UpdateSalesChannelCommand>
{
    public UpdateSalesChannelCommandValidator()
    {
        RuleFor(x => x.SalesChannelPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class UpdateSalesChannelCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalesChannelCommand, Result<SalesChannelDto>>
{
    public async Task<Result<SalesChannelDto>> Handle(UpdateSalesChannelCommand request, CancellationToken ct)
    {
        var canal = await db.SalesChannels.FirstOrDefaultAsync(c => c.PublicId == request.SalesChannelPublicId, ct);
        if (canal is null) return Result.Failure<SalesChannelDto>(CatalogErrors.SalesChannelNotFound());
        canal.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success(new SalesChannelDto(canal.PublicId, canal.Code, canal.Name, canal.IsActive));
    }
}

/// <summary>
/// Inactivar o reactivar un canal con motivo (T216; §3.8): en uso por un tipo de documento activo (y, desde I3, por un
/// punto de venta o una lista de precios vigentes), <c>Inventory.SalesChannel.InUse</c> nombrándolos. (nuevo)
/// </summary>
public sealed record SetSalesChannelActiveCommand(Guid SalesChannelPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetSalesChannelActiveCommandValidator : ValidadorConMotivo<SetSalesChannelActiveCommand>
{
    public SetSalesChannelActiveCommandValidator() => RuleFor(x => x.SalesChannelPublicId).NotEmpty();
}

public sealed class SetSalesChannelActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetSalesChannelActiveCommand, Result>
{
    public async Task<Result> Handle(SetSalesChannelActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.SalesChannels.FirstOrDefaultAsync(c => c.PublicId == request.SalesChannelPublicId, ct),
            request.Active, c => c.IsActive, (c, a) => c.IsActive = a, CatalogErrors.SalesChannelNotFound(),
            async c =>
            {
                var tipos = await db.InventoryDocumentTypes.Where(t => t.SalesChannelId == c.Id && t.IsActive)
                    .OrderBy(t => t.Code).Select(t => t.Code).ToListAsync(ct);
                return tipos.Count == 0 ? null : CatalogErrors.SalesChannelInUse(tipos.Select(t => $"el tipo de documento {t}").ToList());
            },
            db.SaveChangesAsync, ct);
}

/// <summary>Los canales de venta (T216; §3.8, <c>GET /?includeInactive=</c>), por código. (nuevo)</summary>
public sealed record ListSalesChannelsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<SalesChannelDto>>>;

public sealed class ListSalesChannelsQueryValidator : AbstractValidator<ListSalesChannelsQuery>;

public sealed class ListSalesChannelsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListSalesChannelsQuery, Result<IReadOnlyList<SalesChannelDto>>>
{
    public async Task<Result<IReadOnlyList<SalesChannelDto>>> Handle(ListSalesChannelsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<SalesChannelDto>>(await db.SalesChannels.AsNoTracking()
            .Where(c => request.IncludeInactive || c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new SalesChannelDto(c.PublicId, c.Code, c.Name, c.IsActive))
            .ToListAsync(ct));
}
