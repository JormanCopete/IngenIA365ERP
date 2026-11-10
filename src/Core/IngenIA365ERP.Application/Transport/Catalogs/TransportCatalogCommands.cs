using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Catalogs;

// Feature 020 (US2; R6; T050; FR-024; contracts/api.md §3, «Catálogos»): un solo CRUD para los nueve catálogos de Transporte. El código es
// la nomenclatura de la cooperativa (CodigoDeCatalogo, 10 caracteres), único por catálogo entre vivos. Se inactiva; nunca se borra.

/// <summary>Los nombres de los catálogos en las rutas y en <c>GET /api/catalogos/transporte-{kind}/codigo/{codigo}</c>.</summary>
public static class CatalogosDeTransporte
{
    public static IReadOnlyDictionary<string, TransportCatalogKind> PorNombre { get; } =
        new Dictionary<string, TransportCatalogKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["marca"] = TransportCatalogKind.Brand,
            ["clase"] = TransportCatalogKind.VehicleClass,
            ["carroceria"] = TransportCatalogKind.BodyType,
            ["combustible"] = TransportCatalogKind.Fuel,
            ["modalidad"] = TransportCatalogKind.Modality,
            ["clase-de-servicio"] = TransportCatalogKind.ServiceClass,
            ["organismo-de-transito"] = TransportCatalogKind.TransitAuthority,
            ["grupo-de-rodamiento"] = TransportCatalogKind.RollingGroup,
            ["tipo-de-equipo"] = TransportCatalogKind.EquipmentType,
        };

    /// <summary>El catálogo de un nombre de ruta (<c>marca</c>) o de consulta de código (<c>transporte-marca</c>); también admite el enum.</summary>
    public static TransportCatalogKind? DelNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        var limpio = nombre.Trim();
        if (limpio.StartsWith("transporte-", StringComparison.OrdinalIgnoreCase)) limpio = limpio["transporte-".Length..];
        if (PorNombre.TryGetValue(limpio, out var tipo)) return tipo;
        return Enum.TryParse<TransportCatalogKind>(limpio, ignoreCase: true, out var e) && Enum.IsDefined(e) ? e : null;
    }
}

public sealed record TransportCatalogItemDto(Guid PublicId, TransportCatalogKind Kind, string Code, string Name, bool IsActive, bool IsSystem);

/// <summary>Los valores de un catálogo; sin <paramref name="IncludeInactive"/>, sólo los activos.</summary>
public sealed record ListTransportCatalogQuery(TransportCatalogKind Kind, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<TransportCatalogItemDto>>>;

public sealed class ListTransportCatalogQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListTransportCatalogQuery, Result<IReadOnlyList<TransportCatalogItemDto>>>
{
    public async Task<Result<IReadOnlyList<TransportCatalogItemDto>>> Handle(ListTransportCatalogQuery request, CancellationToken ct)
    {
        var consulta = db.TransportCatalogItems.AsNoTracking().Where(c => c.Kind == request.Kind);
        if (!request.IncludeInactive) consulta = consulta.Where(c => c.IsActive);
        var filas = await consulta.OrderBy(c => c.Name)
            .Select(c => new TransportCatalogItemDto(c.PublicId, c.Kind, c.Code, c.Name, c.IsActive, c.IsSystem))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<TransportCatalogItemDto>>(filas);
    }
}

/// <summary>Crea un valor de catálogo.</summary>
public sealed record CreateTransportCatalogItemCommand(TransportCatalogKind Kind, string Code, string Name) : IRequest<Result<TransportCatalogItemDto>>;

public sealed class CreateTransportCatalogItemCommandValidator : AbstractValidator<CreateTransportCatalogItemCommand>
{
    public CreateTransportCatalogItemCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código.").MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(120);
    }
}

public sealed class CreateTransportCatalogItemCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateTransportCatalogItemCommand, Result<TransportCatalogItemDto>>
{
    public async Task<Result<TransportCatalogItemDto>> Handle(CreateTransportCatalogItemCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.TransportCatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Kind == request.Kind && c.Code == codigo, ct);
        if (existente is not null)
            return Result.Failure<TransportCatalogItemDto>(ErroresDeTransporte.CodigoDeCatalogoDuplicado(request.Kind, codigo, existente.Name));

        var item = new TransportCatalogItem { Kind = request.Kind, Code = codigo, Name = request.Name.Trim(), IsActive = true };
        db.TransportCatalogItems.Add(item);
        await db.SaveChangesAsync(ct);
        return Result.Success(new TransportCatalogItemDto(item.PublicId, item.Kind, item.Code, item.Name, item.IsActive, item.IsSystem));
    }
}

/// <summary>Renombra o inactiva un valor de catálogo. El código no cambia.</summary>
public sealed record UpdateTransportCatalogItemCommand(Guid PublicId, string Name, bool IsActive) : IRequest<Result<TransportCatalogItemDto>>;

public sealed class UpdateTransportCatalogItemCommandValidator : AbstractValidator<UpdateTransportCatalogItemCommand>
{
    public UpdateTransportCatalogItemCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(120);
    }
}

public sealed class UpdateTransportCatalogItemCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateTransportCatalogItemCommand, Result<TransportCatalogItemDto>>
{
    public async Task<Result<TransportCatalogItemDto>> Handle(UpdateTransportCatalogItemCommand request, CancellationToken ct)
    {
        var item = await db.TransportCatalogItems.FirstOrDefaultAsync(c => c.PublicId == request.PublicId, ct);
        if (item is null) return Result.Failure<TransportCatalogItemDto>(ErroresDeTransporte.CatalogoNoEncontradoPorId(request.PublicId));
        item.Name = request.Name.Trim();
        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(new TransportCatalogItemDto(item.PublicId, item.Kind, item.Code, item.Name, item.IsActive, item.IsSystem));
    }
}
