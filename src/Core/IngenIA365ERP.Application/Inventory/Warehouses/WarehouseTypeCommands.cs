using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Alta de un tipo de bodega (feature 012, T222; contracts/api.md §4.1; FR-032). El comportamiento es fijo del sistema y
/// <see cref="WarehouseBehavior.Transit"/> sólo lo tiene el tipo sembrado: pedirlo responde
/// <c>Inventory.WarehouseType.TransitIsSystem</c>. (nuevo)
/// </summary>
public sealed record CreateWarehouseTypeCommand(string Code, string Name, WarehouseBehavior Behavior = WarehouseBehavior.Operational)
    : IRequest<Result<WarehouseTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateWarehouseTypeCommandValidator : AbstractValidator<CreateWarehouseTypeCommand>
{
    public CreateWarehouseTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Behavior).IsInEnum();
    }
}

public sealed class CreateWarehouseTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateWarehouseTypeCommand, Result<WarehouseTypeDto>>
{
    public async Task<Result<WarehouseTypeDto>> Handle(CreateWarehouseTypeCommand request, CancellationToken ct)
    {
        if (request.Behavior == WarehouseBehavior.Transit) return Result.Failure<WarehouseTypeDto>(WarehouseErrors.WarehouseTypeTransitIsSystem());
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.WarehouseTypes.Where(t => t.Code == codigo).Select(t => new { t.PublicId, t.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<WarehouseTypeDto>(CodigoDeCatalogo.Duplicado("un tipo de bodega", codigo, existente.Name, existente.PublicId));

        var tipo = new WarehouseType { Code = codigo, Name = request.Name.Trim(), Behavior = request.Behavior, IsActive = true };
        db.WarehouseTypes.Add(tipo);
        await db.SaveChangesAsync(ct);
        return Result.Success(Dto(tipo));
    }

    internal static WarehouseTypeDto Dto(WarehouseType t) => new(t.PublicId, t.Code, t.Name, t.Behavior, t.IsSeeded, t.IsActive);
}

/// <summary>Renombrar un tipo de bodega (T222; §4.1, <c>PUT /{id}</c>): código y comportamiento no cambian. (nuevo)</summary>
public sealed record UpdateWarehouseTypeCommand(Guid WarehouseTypePublicId, string Name) : IRequest<Result<WarehouseTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateWarehouseTypeCommandValidator : AbstractValidator<UpdateWarehouseTypeCommand>
{
    public UpdateWarehouseTypeCommandValidator()
    {
        RuleFor(x => x.WarehouseTypePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
    }
}

public sealed class UpdateWarehouseTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWarehouseTypeCommand, Result<WarehouseTypeDto>>
{
    public async Task<Result<WarehouseTypeDto>> Handle(UpdateWarehouseTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.WarehouseTypes.FirstOrDefaultAsync(t => t.PublicId == request.WarehouseTypePublicId, ct);
        if (tipo is null) return Result.Failure<WarehouseTypeDto>(WarehouseErrors.WarehouseTypeNotFound());
        tipo.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success(CreateWarehouseTypeCommandHandler.Dto(tipo));
    }
}

/// <summary>
/// Inactivar o reactivar un tipo de bodega con motivo (T222; §4.1): con bodegas activas, <c>Inventory.WarehouseType.InUse</c>.
/// (nuevo)
/// </summary>
public sealed record SetWarehouseTypeActiveCommand(Guid WarehouseTypePublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetWarehouseTypeActiveCommandValidator : ValidadorConMotivo<SetWarehouseTypeActiveCommand>
{
    public SetWarehouseTypeActiveCommandValidator() => RuleFor(x => x.WarehouseTypePublicId).NotEmpty();
}

public sealed class SetWarehouseTypeActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetWarehouseTypeActiveCommand, Result>
{
    public async Task<Result> Handle(SetWarehouseTypeActiveCommand request, CancellationToken ct)
    {
        var tipo = await db.WarehouseTypes.FirstOrDefaultAsync(t => t.PublicId == request.WarehouseTypePublicId, ct);
        if (tipo is null) return Result.Failure(WarehouseErrors.WarehouseTypeNotFound());
        if (tipo.IsActive == request.Active) return Result.Success();
        if (!request.Active)
        {
            var bodegas = await db.Warehouses.CountAsync(w => w.WarehouseTypeId == tipo.Id && w.IsActive, ct);
            if (bodegas > 0) return Result.Failure(WarehouseErrors.WarehouseTypeInUse(bodegas));
        }
        tipo.IsActive = request.Active;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
