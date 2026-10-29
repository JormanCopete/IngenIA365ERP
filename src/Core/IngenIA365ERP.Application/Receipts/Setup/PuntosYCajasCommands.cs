using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, US1 (T0072; FR-021, FR-022, FR-028, FR-180, FR-181; contracts/api.md §2; data-model §1.2–§1.4): los puntos de recaudo y sus cajas. Son
// configuración: se admiten antes de activar Recaudo (no llevan IEscrituraDeRecaudo). El código es la nomenclatura de la cooperativa (CodigoDeCatalogo:
// 10 caracteres, mayúsculas, único entre vivos; Catalogo.CodigoDuplicado con el nombre del existente) y no cambia. Lo que está fuera del alcance de quien
// opera responde Generic.NotFound. Un cambio de una caja rige desde la próxima sesión: la abierta conserva lo que selló (US1-3). (nuevo)

/// <summary>Las reglas comunes del código de un catálogo de Recaudo. (nuevo)</summary>
internal static class ReglasDeCodigo
{
    public static void Codigo<T>(IRuleBuilder<T, string> regla, int largo = CodigoDeCatalogo.LargoCorto) => regla
        .NotEmpty().WithMessage("Indique el código.")
        .MaximumLength(largo).WithMessage($"El código admite hasta {largo} caracteres.")
        .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
}

/// <summary>El alcance aplicado a los puntos y las cajas de una consulta o un comando. (nuevo)</summary>
internal static class AlcanceDePuntos
{
    /// <summary>¿Ve quien opera el punto? Por el punto o por la oficina del punto.</summary>
    public static bool VePunto(AlcanceDeRecaudo alcance, CollectionPoint punto) => alcance.IncluyePunto(punto.Id) || alcance.IncluyeOficina(punto.BranchId);

    /// <summary>¿Ve quien opera la caja? Por la caja, su punto o la oficina del punto.</summary>
    public static bool VeCaja(AlcanceDeRecaudo alcance, CashBox caja, CollectionPoint punto) =>
        alcance.IncluyeCaja(caja.Id, caja.CollectionPointId) || alcance.IncluyeOficina(punto.BranchId);

    public static async Task<CollectionPoint?> PuntoVisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.CollectionPoints : db.CollectionPoints.AsNoTracking();
        var punto = await consulta.FirstOrDefaultAsync(p => p.PublicId == publicId, ct);
        return punto is not null && VePunto(await alcance.ObtenerAsync(ct), punto) ? punto : null;
    }

    public static async Task<(CashBox Caja, CollectionPoint Punto)?> CajaVisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir,
        CancellationToken ct)
    {
        var consulta = seguir ? db.CashBoxes : db.CashBoxes.AsNoTracking();
        var caja = await consulta.FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
        if (caja is null) return null;
        var punto = await db.CollectionPoints.AsNoTracking().FirstAsync(p => p.Id == caja.CollectionPointId, ct);
        return VeCaja(await alcance.ObtenerAsync(ct), caja, punto) ? (caja, punto) : null;
    }
}

// ---------------------------------------------------------------------------------------------------------------- puntos --

/// <summary><c>GET /api/receipts/collection-points</c>: los puntos al alcance, con la cantidad de cajas vivas. (nuevo)</summary>
public sealed record ListCollectionPointsQuery(Guid? BranchPublicId = null, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<CollectionPointDto>>>;

public sealed class ListCollectionPointsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCollectionPointsQuery, Result<IReadOnlyList<CollectionPointDto>>>
{
    public async Task<Result<IReadOnlyList<CollectionPointDto>>> Handle(ListCollectionPointsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var puntos = await (from p in db.CollectionPoints.AsNoTracking()
                            join b in db.Branches.AsNoTracking() on p.BranchId equals b.Id
                            where request.IncludeInactive || p.Status == ReceiptsCatalogStatus.Active
                            where request.BranchPublicId == null || b.PublicId == request.BranchPublicId
                            orderby p.Code
                            select new { Punto = p, b.PublicId, b.Name }).ToListAsync(ct);
        var visibles = puntos.Where(x => AlcanceDePuntos.VePunto(mio, x.Punto)).ToList();
        var ids = visibles.Select(x => x.Punto.Id).ToList();
        var cajas = await db.CashBoxes.AsNoTracking().Where(c => ids.Contains(c.CollectionPointId)).GroupBy(c => c.CollectionPointId)
            .Select(g => new { g.Key, Cantidad = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Cantidad, ct);
        return Result.Success<IReadOnlyList<CollectionPointDto>>(visibles
            .Select(x => SetupMapeos.Punto(x.Punto, x.PublicId, x.Name, cajas.GetValueOrDefault(x.Punto.Id))).ToList());
    }
}

/// <summary><c>POST /api/receipts/collection-points</c> (<c>Receipts.Setup.Manage</c>). (nuevo)</summary>
public sealed record CreateCollectionPointCommand(
    Guid BranchPublicId, string Code, string Name, DateOnly? ValidFrom = null, string? DocumentPrefix = null, bool IsMainCashBoxVault = false)
    : IRequest<Result<CollectionPointDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCollectionPointCommandValidator : AbstractValidator<CreateCollectionPointCommand>
{
    public CreateCollectionPointCommandValidator()
    {
        RuleFor(x => x.BranchPublicId).NotEmpty().WithMessage("Indique la oficina.");
        ReglasDeCodigo.Codigo(RuleFor(x => x.Code));
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.DocumentPrefix).MaximumLength(10).Matches(CodigoDeCatalogo.Patron).When(x => !string.IsNullOrWhiteSpace(x.DocumentPrefix));
    }
}

public sealed class CreateCollectionPointCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<CreateCollectionPointCommand, Result<CollectionPointDto>>
{
    public async Task<Result<CollectionPointDto>> Handle(CreateCollectionPointCommand request, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Id))
            return Result.Failure<CollectionPointDto>(ErroresDeRecaudo.NoEncontrado("La oficina"));
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.CollectionPoints.AsNoTracking().FirstOrDefaultAsync(p => p.Code == codigo, ct) is { } existente)
            return Result.Failure<CollectionPointDto>(CodigoDeCatalogo.Duplicado("un punto de recaudo", codigo, existente.Name, existente.PublicId));

        var punto = new CollectionPoint
        {
            BranchId = oficina.Id,
            Code = codigo,
            Name = request.Name.Trim(),
            ValidFrom = request.ValidFrom ?? reloj.HoyLocal,
            DocumentPrefix = CodigoDeCatalogo.Normalizar(request.DocumentPrefix),
            IsMainCashBoxVault = request.IsMainCashBoxVault,
        };
        db.CollectionPoints.Add(punto);
        await db.SaveChangesAsync(ct);
        return Result.Success(SetupMapeos.Punto(punto, oficina.PublicId, oficina.Name, 0));
    }
}

/// <summary><c>PUT /api/receipts/collection-points/{id}</c>: nombre, prefijo, caja principal como bóveda y fin de vigencia; el código no cambia. (nuevo)</summary>
public sealed record UpdateCollectionPointCommand(Guid PublicId, string Name, string? DocumentPrefix, bool IsMainCashBoxVault, DateOnly? ValidTo)
    : IRequest<Result<CollectionPointDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCollectionPointCommandValidator : AbstractValidator<UpdateCollectionPointCommand>
{
    public UpdateCollectionPointCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.DocumentPrefix).MaximumLength(10).Matches(CodigoDeCatalogo.Patron).When(x => !string.IsNullOrWhiteSpace(x.DocumentPrefix));
    }
}

public sealed class UpdateCollectionPointCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<UpdateCollectionPointCommand, Result<CollectionPointDto>>
{
    public async Task<Result<CollectionPointDto>> Handle(UpdateCollectionPointCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (punto is null) return Result.Failure<CollectionPointDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        if (request.ValidTo is { } hasta && hasta < punto.ValidFrom)
            return Result.Failure<CollectionPointDto>(new Error(Error.Validation.Code, "El fin de la vigencia no puede ser anterior a su inicio."));
        punto.Name = request.Name.Trim();
        punto.DocumentPrefix = CodigoDeCatalogo.Normalizar(request.DocumentPrefix);
        punto.IsMainCashBoxVault = request.IsMainCashBoxVault;
        punto.ValidTo = request.ValidTo;
        await db.SaveChangesAsync(ct);
        return Result.Success(await SetupMapeos.PuntoAsync(db, punto, ct));
    }
}

/// <summary><c>POST /api/receipts/collection-points/{id}/inactivate</c>: deja de ofrecerse; no se borra. (nuevo)</summary>
public sealed record InactivateCollectionPointCommand(Guid PublicId) : IRequest<Result<CollectionPointDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class InactivateCollectionPointCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<InactivateCollectionPointCommand, Result<CollectionPointDto>>
{
    public async Task<Result<CollectionPointDto>> Handle(InactivateCollectionPointCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (punto is null) return Result.Failure<CollectionPointDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        punto.Status = ReceiptsCatalogStatus.Inactive;
        await db.SaveChangesAsync(ct);
        return Result.Success(await SetupMapeos.PuntoAsync(db, punto, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------------- cajas --

/// <summary><c>GET /api/receipts/cash-boxes</c>: las cajas al alcance. (nuevo)</summary>
public sealed record ListCashBoxesQuery(Guid? CollectionPointPublicId = null, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<CashBoxDto>>>;

public sealed class ListCashBoxesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListCashBoxesQuery, Result<IReadOnlyList<CashBoxDto>>>
{
    public async Task<Result<IReadOnlyList<CashBoxDto>>> Handle(ListCashBoxesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from c in db.CashBoxes.AsNoTracking()
                           join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                           where request.IncludeInactive || c.Status == ReceiptsCatalogStatus.Active
                           where request.CollectionPointPublicId == null || p.PublicId == request.CollectionPointPublicId
                           orderby p.Code, c.Code
                           select new { Caja = c, Punto = p }).ToListAsync(ct);
        var visibles = filas.Where(f => AlcanceDePuntos.VeCaja(mio, f.Caja, f.Punto)).ToList();
        var hoy = reloj.HoyLocal;
        var ids = visibles.Select(f => f.Caja.Id).ToList();
        var cajeros = await db.CashBoxCashiers.AsNoTracking()
            .Where(c => ids.Contains(c.CashBoxId) && c.ValidFrom <= hoy && (c.ValidTo == null || c.ValidTo >= hoy))
            .GroupBy(c => c.CashBoxId).Select(g => new { g.Key, Cantidad = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Cantidad, ct);
        return Result.Success<IReadOnlyList<CashBoxDto>>(visibles
            .Select(f => SetupMapeos.Caja(f.Caja, f.Punto, cajeros.GetValueOrDefault(f.Caja.Id))).ToList());
    }
}

/// <summary><c>POST /api/receipts/cash-boxes</c> (<c>Receipts.Setup.Manage</c>): código único por punto entre vivas; a lo sumo una caja principal por oficina. (nuevo)</summary>
public sealed record CreateCashBoxCommand(
    Guid CollectionPointPublicId,
    string Code,
    string Name,
    CashBoxBaseMode BaseMode,
    decimal BaseAmount,
    decimal? CashWarnCap,
    decimal? CashBlockCap,
    decimal? DailyCashOutCap,
    bool IsMainCashBox,
    DateOnly? ValidFrom = null) : IRequest<Result<CashBoxDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCashBoxCommandValidator : AbstractValidator<CreateCashBoxCommand>
{
    public CreateCashBoxCommandValidator()
    {
        RuleFor(x => x.CollectionPointPublicId).NotEmpty().WithMessage("Indique el punto de recaudo.");
        ReglasDeCodigo.Codigo(RuleFor(x => x.Code));
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.BaseMode).IsInEnum();
        RuleFor(x => x.BaseAmount).GreaterThanOrEqualTo(0).WithMessage("La base no puede ser negativa.");
        RuleFor(x => x.CashWarnCap).GreaterThan(0).When(x => x.CashWarnCap is not null);
        RuleFor(x => x.CashBlockCap).GreaterThan(0).When(x => x.CashBlockCap is not null);
        RuleFor(x => x.DailyCashOutCap).GreaterThan(0).When(x => x.DailyCashOutCap is not null);
    }
}

public sealed class CreateCashBoxCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<CreateCashBoxCommand, Result<CashBoxDto>>
{
    public async Task<Result<CashBoxDto>> Handle(CreateCashBoxCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<CashBoxDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        if (request.CashWarnCap is { } aviso && request.CashBlockCap is { } bloqueo && bloqueo < aviso)
            return Result.Failure<CashBoxDto>(ErroresDeRecaudo.TopesInvalidos());
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.CashBoxes.AsNoTracking().FirstOrDefaultAsync(c => c.CollectionPointId == punto.Id && c.Code == codigo, ct) is { } existente)
            return Result.Failure<CashBoxDto>(CodigoDeCatalogo.Duplicado("una caja en este punto", codigo, existente.Name, existente.PublicId));
        if (request.IsMainCashBox && await SetupMapeos.CajaPrincipalDeLaOficinaAsync(db, punto.BranchId, null, ct) is { } principal)
            return Result.Failure<CashBoxDto>(ErroresDeRecaudo.CajaPrincipalRepetida(principal));

        var caja = new CashBox
        {
            CollectionPointId = punto.Id,
            Code = codigo,
            Name = request.Name.Trim(),
            BaseMode = request.BaseMode,
            BaseAmount = request.BaseAmount,
            CashWarnCap = request.CashWarnCap,
            CashBlockCap = request.CashBlockCap,
            DailyCashOutCap = request.DailyCashOutCap,
            IsMainCashBox = request.IsMainCashBox,
            ValidFrom = request.ValidFrom ?? reloj.HoyLocal,
        };
        db.CashBoxes.Add(caja);
        await db.SaveChangesAsync(ct);
        return Result.Success(SetupMapeos.Caja(caja, punto, 0));
    }
}

/// <summary>
/// <c>PUT /api/receipts/cash-boxes/{id}</c>: base, modo, topes y caja principal. Rige desde la próxima sesión: la abierta conserva lo que selló al abrir
/// (US1-3). El código y el punto no cambian. (nuevo)
/// </summary>
public sealed record UpdateCashBoxCommand(
    Guid PublicId,
    string Name,
    CashBoxBaseMode BaseMode,
    decimal BaseAmount,
    decimal? CashWarnCap,
    decimal? CashBlockCap,
    decimal? DailyCashOutCap,
    bool IsMainCashBox,
    DateOnly? ValidTo = null) : IRequest<Result<CashBoxDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCashBoxCommandValidator : AbstractValidator<UpdateCashBoxCommand>
{
    public UpdateCashBoxCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.BaseMode).IsInEnum();
        RuleFor(x => x.BaseAmount).GreaterThanOrEqualTo(0).WithMessage("La base no puede ser negativa.");
        RuleFor(x => x.CashWarnCap).GreaterThan(0).When(x => x.CashWarnCap is not null);
        RuleFor(x => x.CashBlockCap).GreaterThan(0).When(x => x.CashBlockCap is not null);
        RuleFor(x => x.DailyCashOutCap).GreaterThan(0).When(x => x.DailyCashOutCap is not null);
    }
}

public sealed class UpdateCashBoxCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<UpdateCashBoxCommand, Result<CashBoxDto>>
{
    public async Task<Result<CashBoxDto>> Handle(UpdateCashBoxCommand request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (hallada is null) return Result.Failure<CashBoxDto>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var (caja, punto) = hallada.Value;
        if (request.CashWarnCap is { } aviso && request.CashBlockCap is { } bloqueo && bloqueo < aviso)
            return Result.Failure<CashBoxDto>(ErroresDeRecaudo.TopesInvalidos());
        if (request.IsMainCashBox && await SetupMapeos.CajaPrincipalDeLaOficinaAsync(db, punto.BranchId, caja.Id, ct) is { } principal)
            return Result.Failure<CashBoxDto>(ErroresDeRecaudo.CajaPrincipalRepetida(principal));
        caja.Name = request.Name.Trim();
        caja.BaseMode = request.BaseMode;
        caja.BaseAmount = request.BaseAmount;
        caja.CashWarnCap = request.CashWarnCap;
        caja.CashBlockCap = request.CashBlockCap;
        caja.DailyCashOutCap = request.DailyCashOutCap;
        caja.IsMainCashBox = request.IsMainCashBox;
        caja.ValidTo = request.ValidTo;
        await db.SaveChangesAsync(ct);
        return Result.Success(SetupMapeos.Caja(caja, punto, await SetupMapeos.CajerosVigentesAsync(db, caja.Id, reloj.HoyLocal, ct)));
    }
}

/// <summary><c>POST /api/receipts/cash-boxes/{id}/inactivate</c>: la sesión abierta sigue con lo sellado; no se abre otra. (nuevo)</summary>
public sealed record InactivateCashBoxCommand(Guid PublicId) : IRequest<Result<CashBoxDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class InactivateCashBoxCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<InactivateCashBoxCommand, Result<CashBoxDto>>
{
    public async Task<Result<CashBoxDto>> Handle(InactivateCashBoxCommand request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (hallada is null) return Result.Failure<CashBoxDto>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var (caja, punto) = hallada.Value;
        caja.Status = ReceiptsCatalogStatus.Inactive;
        await db.SaveChangesAsync(ct);
        return Result.Success(SetupMapeos.Caja(caja, punto, await SetupMapeos.CajerosVigentesAsync(db, caja.Id, reloj.HoyLocal, ct)));
    }
}
