using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// Alta de un tipo de documento (feature 012, T150; contracts/api.md §8, <c>POST /api/inventory/document-types</c>;
/// FR-036 a FR-038): elige su clase (fija, e inmutable después) y su código (inmutable), campos obligatorios, bodegas,
/// canal, marcas y su primer consecutivo. Las clases que numeran con resolución DIAN declaran el prefijo de su
/// resolución y no tienen consecutivo propio. (nuevo)
/// </summary>
public sealed record CreateInventoryDocumentTypeCommand(
    string Code,
    string Name,
    DocumentClass Class,
    bool RequiresCounterparty,
    bool RequiresCostCenter,
    bool RequiresReason,
    bool RequiresExternalReference,
    IReadOnlyList<Guid>? WarehousePublicIds,
    Guid? SalesChannelPublicId,
    bool IsTaxableWithdrawal,
    bool VatNonDeductible,
    bool AllowsFutureDate,
    string? Prefix,
    long? FirstNumber,
    DateOnly? ValidFrom)
    : IRequest<Result<DocumentTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateInventoryDocumentTypeCommandValidator : AbstractValidator<CreateInventoryDocumentTypeCommand>
{
    public CreateInventoryDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Class).IsInEnum();
        RuleFor(x => x.Prefix).Matches(ReglasDeTipoDeDocumento.PatronDePrefijo).WithMessage(ReglasDeTipoDeDocumento.MensajeDePrefijo);
        RuleFor(x => x.FirstNumber).GreaterThanOrEqualTo(1).When(x => x.FirstNumber is not null);
        RuleForEach(x => x.WarehousePublicIds).NotEmpty();
    }
}

public sealed class CreateInventoryDocumentTypeCommandHandler(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IDateTimeService reloj,
    VistaDeTiposDeDocumento vista)
    : IRequestHandler<CreateInventoryDocumentTypeCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(CreateInventoryDocumentTypeCommand request, CancellationToken ct)
    {
        var disponible = ReglasDeTipoDeDocumento.ClaseDisponible(request.Class);
        if (disponible.IsFailure) return Falla(disponible.Error);
        var marcas = ReglasDeTipoDeDocumento.Marcas(request.Class, new MarcasDelTipo(request.IsTaxableWithdrawal, request.VatNonDeductible, request.AllowsFutureDate));
        if (marcas.IsFailure) return Falla(marcas.Error);

        var clase = ClasesDeDocumento.De(request.Class);
        var porResolucion = clase.NumberedBy == NumberedBy.DianResolution;
        if (porResolucion && request.FirstNumber is not null) return Falla(InventoryErrors.NumberedByResolution(request.Class));

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.InventoryDocumentTypes.Where(t => t.Code == codigo).Select(t => t.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Falla(CodigoDeCatalogo.Duplicado("un tipo de documento", codigo, existente));

        var bodegas = await ReglasDeTipoDeDocumento.BodegasAsync(maestros, request.WarehousePublicIds ?? [], ct);
        if (bodegas.IsFailure) return Falla(bodegas.Error);
        var canal = await ReglasDeTipoDeDocumento.CanalAsync(maestros, request.SalesChannelPublicId, ct);
        if (canal.IsFailure) return Falla(canal.Error);

        var prefijo = ReglasDeTipoDeDocumento.Prefijo(request.Prefix);
        var tipo = new InventoryDocumentType
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Class = request.Class,
            FiscalPrefix = porResolucion && prefijo.Length > 0 ? prefijo : null,
            RequiresCounterparty = request.RequiresCounterparty,
            RequiresCostCenter = request.RequiresCostCenter,
            RequiresReason = request.RequiresReason,
            RequiresExternalReference = request.RequiresExternalReference,
            SalesChannelId = canal.Value,
            IsTaxableWithdrawal = request.IsTaxableWithdrawal,
            VatNonDeductible = request.VatNonDeductible,
            AllowsFutureDate = request.AllowsFutureDate,
            AllWarehouses = bodegas.Value.Count == 0,
            IsActive = true,
        };
        foreach (var bodega in bodegas.Value)
            tipo.Warehouses.Add(new DocumentTypeWarehouse { DocumentType = tipo, WarehouseId = bodega.Id });
        if (!porResolucion)
        {
            tipo.Sequences.Add(new DocumentSequence
            {
                DocumentType = tipo,
                Prefix = prefijo,
                NextValue = request.FirstNumber ?? 1,
                ValidFrom = request.ValidFrom ?? reloj.HoyLocal,
            });
        }

        db.InventoryDocumentTypes.Add(tipo);
        await db.SaveChangesAsync(ct);
        return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }

    private static Result<DocumentTypeDto> Falla(Error error) => Result.Failure<DocumentTypeDto>(error);
}

/// <summary>
/// Edición de un tipo (T150; §8, <c>PUT /{id}</c>): lo mismo que el alta sin código, clase, prefijo, primer número ni
/// vigencia —el código y la clase no cambian; el consecutivo se cambia con <c>AddDocumentSequenceCommand</c>—. (nuevo)
/// </summary>
public sealed record UpdateInventoryDocumentTypeCommand(
    Guid DocumentTypePublicId,
    string Name,
    bool RequiresCounterparty,
    bool RequiresCostCenter,
    bool RequiresReason,
    bool RequiresExternalReference,
    IReadOnlyList<Guid>? WarehousePublicIds,
    Guid? SalesChannelPublicId,
    bool IsTaxableWithdrawal,
    bool VatNonDeductible,
    bool AllowsFutureDate)
    : IRequest<Result<DocumentTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateInventoryDocumentTypeCommandValidator : AbstractValidator<UpdateInventoryDocumentTypeCommand>
{
    public UpdateInventoryDocumentTypeCommandValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.WarehousePublicIds).NotEmpty();
    }
}

public sealed class UpdateInventoryDocumentTypeCommandHandler(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IDateTimeService reloj,
    VistaDeTiposDeDocumento vista)
    : IRequestHandler<UpdateInventoryDocumentTypeCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(UpdateInventoryDocumentTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstOrDefaultAsync(t => t.PublicId == request.DocumentTypePublicId, ct);
        if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());

        var marcas = ReglasDeTipoDeDocumento.Marcas(tipo.Class, new MarcasDelTipo(request.IsTaxableWithdrawal, request.VatNonDeductible, request.AllowsFutureDate));
        if (marcas.IsFailure) return Falla(marcas.Error);
        var bodegas = await ReglasDeTipoDeDocumento.BodegasAsync(maestros, request.WarehousePublicIds ?? [], ct);
        if (bodegas.IsFailure) return Falla(bodegas.Error);
        var canal = await ReglasDeTipoDeDocumento.CanalAsync(maestros, request.SalesChannelPublicId, ct);
        if (canal.IsFailure) return Falla(canal.Error);

        tipo.Name = request.Name.Trim();
        tipo.RequiresCounterparty = request.RequiresCounterparty;
        tipo.RequiresCostCenter = request.RequiresCostCenter;
        tipo.RequiresReason = request.RequiresReason;
        tipo.RequiresExternalReference = request.RequiresExternalReference;
        tipo.SalesChannelId = canal.Value;
        tipo.IsTaxableWithdrawal = request.IsTaxableWithdrawal;
        tipo.VatNonDeductible = request.VatNonDeductible;
        tipo.AllowsFutureDate = request.AllowsFutureDate;
        tipo.AllWarehouses = bodegas.Value.Count == 0;

        // Bodegas permitidas: las que ya no van quedan de baja lógica; las nuevas se agregan.
        var pedidas = bodegas.Value.Select(b => b.Id).ToHashSet();
        foreach (var vieja in tipo.Warehouses.Where(w => !w.IsDeleted && !pedidas.Contains(w.WarehouseId)))
        {
            vieja.IsDeleted = true;
            vieja.DeletedAt = reloj.UtcNow;
        }
        foreach (var id in pedidas.Where(id => tipo.Warehouses.All(w => w.IsDeleted || w.WarehouseId != id)))
            tipo.Warehouses.Add(new DocumentTypeWarehouse { DocumentType = tipo, WarehouseId = id });

        await db.SaveChangesAsync(ct);
        return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }

    private static Result<DocumentTypeDto> Falla(Error error) => Result.Failure<DocumentTypeDto>(error);
}
