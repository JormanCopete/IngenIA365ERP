using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Define un conteo físico (feature 012, US11, T402; contracts/api.md §12, <c>POST /counts</c>, permiso <c>Inventory.Counts.Open</c>):
/// el borrador <c>PhysicalCount</c> con su tipo, su bodega, total o cíclico, el alcance (todo, categorías, ubicaciones o selección;
/// la clase ABC se admite en la definición y se rechaza al abrir hasta I6), ciego o no, los contadores y las notas. No tiene foto ni
/// número: la foto se toma al abrir y el número se asigna al cerrar. (nuevo)
/// </summary>
public sealed record CreatePhysicalCountCommand(PhysicalCountRequest Count) : IRequest<Result<PhysicalCountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// Cambia la definición de un conteo que todavía no se abrió (<c>PUT /counts/{id}</c>, permiso <c>Inventory.Counts.Open</c>): uno
/// abierto tiene su foto fija (<c>Inventory.Count.AlreadyOpen</c>). (nuevo)
/// </summary>
public sealed record UpdatePhysicalCountCommand(Guid CountPublicId, PhysicalCountRequest Count) : IRequest<Result<PhysicalCountDto>>, IOperacionIdempotente
{
    public byte[]? RowVersion { get; init; }

    public Guid OperationKey { get; init; }
}

/// <summary>Las reglas de forma de la definición (las que no miran la base). (nuevo)</summary>
public sealed class ValidadorDeDefinicionDeConteo : AbstractValidator<PhysicalCountRequest>
{
    /// <summary>Topes de la definición: el criterio cabe en <c>CountScopeJson</c> (4.000 caracteres).</summary>
    public const int MaxProductos = 300;
    public const int MaxUbicaciones = 100;
    public const int MaxCategorias = 100;
    public const int MaxContadores = 30;

    public ValidadorDeDefinicionDeConteo()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x).Must(x => x.Kind != CountKind.Total || x.Scope == CountScope.All)
            .WithMessage("Un conteo total cuenta toda la bodega: su alcance es «todo».");
        RuleFor(x => x).Must(x => x.Kind != CountKind.Cyclic || x.Scope != CountScope.All)
            .WithMessage("Un conteo cíclico se limita a categorías, ubicaciones o una selección de productos.");
        RuleFor(x => x.CategoryPublicIds).Must(l => l is { Count: > 0 }).When(x => x.Scope == CountScope.Category)
            .WithMessage("Elija al menos una categoría.");
        RuleFor(x => x.LocationPublicIds).Must(l => l is { Count: > 0 }).When(x => x.Scope == CountScope.Location)
            .WithMessage("Elija al menos una ubicación.");
        RuleFor(x => x.ProductPublicIds).Must(l => l is { Count: > 0 }).When(x => x.Scope == CountScope.Selection)
            .WithMessage("Elija al menos un producto.");
        RuleFor(x => x.CategoryPublicIds).Must(l => l is null || l.Count <= MaxCategorias).WithMessage($"Hasta {MaxCategorias} categorías.");
        RuleFor(x => x.LocationPublicIds).Must(l => l is null || l.Count <= MaxUbicaciones).WithMessage($"Hasta {MaxUbicaciones} ubicaciones.");
        RuleFor(x => x.ProductPublicIds).Must(l => l is null || l.Count <= MaxProductos).WithMessage($"Hasta {MaxProductos} productos por conteo.");
        RuleFor(x => x.CounterUserPublicIds).Must(l => l is null || l.Count <= MaxContadores).WithMessage($"Hasta {MaxContadores} contadores.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CreatePhysicalCountCommandValidator : AbstractValidator<CreatePhysicalCountCommand>
{
    public CreatePhysicalCountCommandValidator() => RuleFor(x => x.Count).NotNull().SetValidator(new ValidadorDeDefinicionDeConteo());
}

public sealed class UpdatePhysicalCountCommandValidator : AbstractValidator<UpdatePhysicalCountCommand>
{
    public UpdatePhysicalCountCommandValidator()
    {
        RuleFor(x => x.CountPublicId).NotEmpty();
        RuleFor(x => x.Count).NotNull().SetValidator(new ValidadorDeDefinicionDeConteo());
    }
}

/// <summary>
/// Lo que comparten crear y editar la definición: el tipo existe, es de conteo y está activo; la bodega existe en el alcance de quien
/// define (si no, 404), es operativa, está activa y la admite el tipo; las categorías, ubicaciones (de esa bodega), productos y
/// contadores existen. Escribe las columnas <c>Count*</c> y el criterio en <c>CountScopeJson</c>. (nuevo)
/// </summary>
public sealed class DefinicionDeConteo(IApplicationDbContext db, IMaestrosDelDocumento maestros, IAlcanceDeInventario alcanceDeLaPeticion)
{
    public async Task<Result> AplicarAsync(InventoryDocument conteo, PhysicalCountRequest pedido, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstOrDefaultAsync(t => t.PublicId == pedido.DocumentTypePublicId, ct);
        if (tipo is null) return Result.Failure(InventoryErrors.DocumentTypeNotFound());
        if (tipo.Class != DocumentClass.PhysicalCount) return Result.Failure(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Counts));
        if (!tipo.IsActive) return Result.Failure(InventoryErrors.DocumentTypeInactive(tipo.Code));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodega = (await maestros.BodegasAsync([pedido.WarehousePublicId], ct)).FirstOrDefault();
        if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure(ErroresDeAlcance.BodegaInexistente());
        if (bodega.EsTransito) return Result.Failure(InventoryErrors.TransitNotAllowed(bodega.Code));
        if (bodega.Inactiva) return Result.Failure(InventoryErrors.WarehouseInactive(bodega.PublicId, bodega.Code));
        if (!bodega.Activa) return Result.Failure(InventoryErrors.WarehouseNotActive(bodega.PublicId, bodega.Code));
        if (!tipo.AllWarehouses && tipo.Warehouses.Where(w => !w.IsDeleted).All(w => w.WarehouseId != bodega.Id))
            return Result.Failure(InventoryErrors.WarehouseNotAllowedForType(bodega.Code, tipo.Code));

        var categorias = new List<int>();
        if (pedido.Scope == CountScope.Category)
        {
            var pedidas = pedido.CategoryPublicIds!.Distinct().ToList();
            categorias = await db.ProductCategories.AsNoTracking().Where(c => pedidas.Contains(c.PublicId)).Select(c => c.Id).ToListAsync(ct);
            if (categorias.Count != pedidas.Count) return Result.Failure(Catalog.CatalogErrors.CategoryNotFound());
        }
        var ubicaciones = new List<int>();
        if (pedido.Scope == CountScope.Location)
        {
            var pedidas = pedido.LocationPublicIds!.Distinct().ToList();
            var halladas = await maestros.UbicacionesAsync(pedidas, ct);
            if (halladas.Count != pedidas.Count) return Result.Failure(ErroresDelDocumento.UbicacionInexistente());
            if (halladas.Any(u => u.WarehouseId != bodega.Id)) return Result.Failure(InventoryErrors.LocationNotInWarehouse(0, string.Empty));
            ubicaciones = halladas.Select(u => u.Id).ToList();
        }
        var productos = new List<int>();
        if (pedido.Scope == CountScope.Selection)
        {
            var pedidos = pedido.ProductPublicIds!.Distinct().ToList();
            var hallados = await maestros.ProductosAsync(pedidos, ct);
            if (hallados.Count != pedidos.Count) return Result.Failure(ErroresDelDocumento.ProductoInexistente());
            if (hallados.FirstOrDefault(p => !p.Inventariable) is { } servicio) return Result.Failure(InventoryErrors.ProductNotInventoriable(0, servicio.Code));
            productos = hallados.Select(p => p.Id).ToList();
        }
        var contadores = new List<int>();
        if (pedido.CounterUserPublicIds is { Count: > 0 } pedidosContadores)
        {
            var distintos = pedidosContadores.Distinct().ToList();
            contadores = await db.Users.AsNoTracking().Where(u => distintos.Contains(u.PublicId) && u.IsActive).Select(u => u.Id).ToListAsync(ct);
            if (contadores.Count != distintos.Count)
                return Result.Failure(new ErrorConDatos("Validation.Invalid", "Uno de los contadores no existe o no está activo.", new { field = "counterUserPublicIds" }));
        }

        conteo.DocumentTypeId = tipo.Id;
        conteo.DocumentType = tipo;
        conteo.WarehouseId = bodega.Id;
        conteo.BranchId = bodega.BranchId;
        conteo.CountKind = pedido.Kind;
        conteo.CountScope = pedido.Scope;
        conteo.IsBlindCount = pedido.Blind;
        conteo.Notes = string.IsNullOrWhiteSpace(pedido.Notes) ? null : pedido.Notes.Trim();
        conteo.CountScopeJson = new CriterioDelConteo
        {
            Categorias = categorias,
            Ubicaciones = ubicaciones,
            Productos = productos,
            ClaseAbc = pedido.Scope == CountScope.AbcClass ? pedido.AbcClass?.Trim() : null,
            Contadores = contadores,
        }.ComoJson();
        return Result.Success();
    }
}

public sealed class CreatePhysicalCountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    DefinicionDeConteo definicion,
    DetalleDeConteo detalle)
    : IRequestHandler<CreatePhysicalCountCommand, Result<PhysicalCountDto>>
{
    public async Task<Result<PhysicalCountDto>> Handle(CreatePhysicalCountCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PhysicalCountDto>(ErroresDelDocumento.SinUsuario());

        var conteo = new InventoryDocument { Class = DocumentClass.PhysicalCount, OperationDate = reloj.HoyLocal, CreatedByUserId = usuario };
        var aplicada = await definicion.AplicarAsync(conteo, request.Count, ct);
        if (aplicada.IsFailure) return Result.Failure<PhysicalCountDto>(aplicada.Error);

        db.InventoryDocuments.Add(conteo);
        await db.SaveChangesAsync(ct);
        return Result.Success(await detalle.DetalleAsync(conteo, ct));
    }
}

public sealed class UpdatePhysicalCountCommandHandler(
    IApplicationDbContext db,
    IDateTimeService reloj,
    VistaDeConteos conteos,
    DefinicionDeConteo definicion,
    DetalleDeConteo detalle)
    : IRequestHandler<UpdatePhysicalCountCommand, Result<PhysicalCountDto>>
{
    public async Task<Result<PhysicalCountDto>> Handle(UpdatePhysicalCountCommand request, CancellationToken ct)
    {
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: true, ct);
        if (conteo is null) return Result.Failure<PhysicalCountDto>(ErroresDeConteos.NotFound());
        if (conteo.Status != DocumentStatus.Draft) return Result.Failure<PhysicalCountDto>(InventoryErrors.NotDraft(conteo.Status));
        if (conteo.CountSnapshotAt is { } foto) return Result.Failure<PhysicalCountDto>(ErroresDeConteos.AlreadyOpen(foto));
        if (request.RowVersion is { Length: > 0 } leida && conteo.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
            return Result.Failure<PhysicalCountDto>(Error.StaleRowVersion);

        var aplicada = await definicion.AplicarAsync(conteo, request.Count, ct);
        if (aplicada.IsFailure) return Result.Failure<PhysicalCountDto>(aplicada.Error);
        conteo.OperationDate = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        return Result.Success(await detalle.DetalleAsync(conteo, ct));
    }
}
