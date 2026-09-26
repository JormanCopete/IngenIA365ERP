using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.AdjustmentCauses;

/// <summary>
/// Alta de una causa de baja o ajuste (feature 012, T216; contracts/api.md §3.7; FR-037, FR-039; data-model §5.10). El código
/// es inmutable: la matriz lo usa como <c>ReasonCode</c> en <c>Baja</c> y <c>AjusteNegativo</c> (T27). (nuevo)
/// </summary>
public sealed record CreateAdjustmentCauseCommand(
    string Code, string Name, bool AllowsPositive = false, bool AllowsNegative = true, bool AllowsTransitWriteOff = false, bool RequiresAttachment = false)
    : IRequest<Result<AdjustmentCauseDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateAdjustmentCauseCommandValidator : AbstractValidator<CreateAdjustmentCauseCommand>
{
    public CreateAdjustmentCauseCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x).Must(x => x.AllowsPositive || x.AllowsNegative).WithMessage("La causa vale al menos en un sentido (entrada o salida).");
    }
}

public sealed class CreateAdjustmentCauseCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateAdjustmentCauseCommand, Result<AdjustmentCauseDto>>
{
    public async Task<Result<AdjustmentCauseDto>> Handle(CreateAdjustmentCauseCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.AdjustmentCauses.Where(c => c.Code == codigo).Select(c => new { c.PublicId, c.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<AdjustmentCauseDto>(CodigoDeCatalogo.Duplicado("una causa de ajuste", codigo, existente.Name, existente.PublicId));

        var causa = new AdjustmentCause
        {
            Code = codigo,
            Name = request.Name.Trim(),
            AllowsPositive = request.AllowsPositive,
            AllowsNegative = request.AllowsNegative,
            AllowsTransitWriteOff = request.AllowsTransitWriteOff,
            RequiresAttachment = request.RequiresAttachment,
            IsActive = true,
        };
        db.AdjustmentCauses.Add(causa);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeCausas.Dto(causa));
    }
}

/// <summary>Edición de una causa (T216; §3.7, <c>PUT /{id}</c>): nombre y sentidos; el código no cambia. (nuevo)</summary>
public sealed record UpdateAdjustmentCauseCommand(
    Guid AdjustmentCausePublicId, string Name, bool AllowsPositive, bool AllowsNegative, bool AllowsTransitWriteOff, bool RequiresAttachment)
    : IRequest<Result<AdjustmentCauseDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateAdjustmentCauseCommandValidator : AbstractValidator<UpdateAdjustmentCauseCommand>
{
    public UpdateAdjustmentCauseCommandValidator()
    {
        RuleFor(x => x.AdjustmentCausePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x).Must(x => x.AllowsPositive || x.AllowsNegative).WithMessage("La causa vale al menos en un sentido (entrada o salida).");
    }
}

public sealed class UpdateAdjustmentCauseCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAdjustmentCauseCommand, Result<AdjustmentCauseDto>>
{
    public async Task<Result<AdjustmentCauseDto>> Handle(UpdateAdjustmentCauseCommand request, CancellationToken ct)
    {
        var causa = await db.AdjustmentCauses.FirstOrDefaultAsync(c => c.PublicId == request.AdjustmentCausePublicId, ct);
        if (causa is null) return Result.Failure<AdjustmentCauseDto>(CatalogErrors.AdjustmentCauseNotFound());
        causa.Name = request.Name.Trim();
        causa.AllowsPositive = request.AllowsPositive;
        causa.AllowsNegative = request.AllowsNegative;
        causa.AllowsTransitWriteOff = request.AllowsTransitWriteOff;
        causa.RequiresAttachment = request.RequiresAttachment;
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeCausas.Dto(causa));
    }
}

/// <summary>
/// Inactivar o reactivar una causa con motivo (T216; §3.7): las que usa el sistema («diferencia de conteo», «reclamación al
/// transportador») no se inactivan (<c>Inventory.AdjustmentCause.RequiredBySystem</c>). (nuevo)
/// </summary>
public sealed record SetAdjustmentCauseActiveCommand(Guid AdjustmentCausePublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetAdjustmentCauseActiveCommandValidator : ValidadorConMotivo<SetAdjustmentCauseActiveCommand>
{
    public SetAdjustmentCauseActiveCommandValidator() => RuleFor(x => x.AdjustmentCausePublicId).NotEmpty();
}

public sealed class SetAdjustmentCauseActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetAdjustmentCauseActiveCommand, Result>
{
    public async Task<Result> Handle(SetAdjustmentCauseActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.AdjustmentCauses.FirstOrDefaultAsync(c => c.PublicId == request.AdjustmentCausePublicId, ct),
            request.Active, c => c.IsActive, (c, a) => c.IsActive = a, CatalogErrors.AdjustmentCauseNotFound(),
            c => Task.FromResult<Error?>(c.IsRequiredBySystem ? CatalogErrors.AdjustmentCauseRequiredBySystem(c.Code) : null),
            db.SaveChangesAsync, ct);
}

/// <summary>Las causas de ajuste (T216; §3.7, <c>GET /?includeInactive=</c>), por código. (nuevo)</summary>
public sealed record ListAdjustmentCausesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<AdjustmentCauseDto>>>;

public sealed class ListAdjustmentCausesQueryValidator : AbstractValidator<ListAdjustmentCausesQuery>;

public sealed class ListAdjustmentCausesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListAdjustmentCausesQuery, Result<IReadOnlyList<AdjustmentCauseDto>>>
{
    public async Task<Result<IReadOnlyList<AdjustmentCauseDto>>> Handle(ListAdjustmentCausesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<AdjustmentCauseDto>>((await db.AdjustmentCauses.AsNoTracking()
            .Where(c => request.IncludeInactive || c.IsActive).OrderBy(c => c.Code).ToListAsync(ct))
            .Select(VistaDeCausas.Dto).ToList());
}

internal static class VistaDeCausas
{
    public static AdjustmentCauseDto Dto(AdjustmentCause c) => new(c.PublicId, c.Code, c.Name, c.AllowsPositive, c.AllowsNegative,
        c.AllowsTransitWriteOff, c.RequiresAttachment, c.IsSeeded, c.IsRequiredBySystem, c.IsActive);
}
