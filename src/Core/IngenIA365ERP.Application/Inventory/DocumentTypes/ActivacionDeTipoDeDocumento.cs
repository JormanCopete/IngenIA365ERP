using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// Inactivar un tipo (feature 012, T150; contracts/api.md §8, <c>POST /{id}/deactivate</c> con <c>{ reason }</c>): deja de
/// usarse en documentos nuevos. No con borradores ni documentos en aprobación
/// (<c>Inventory.DocumentType.HasOpenDocuments</c>), ni si es el último activo de una clase que el sistema genera solo
/// (<c>Inventory.DocumentType.RequiredBySystem</c>). (nuevo)
/// </summary>
public sealed record DeactivateInventoryDocumentTypeCommand(Guid DocumentTypePublicId, string Reason)
    : IRequest<Result<DocumentTypeDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeactivateInventoryDocumentTypeCommandValidator : ValidadorConMotivo<DeactivateInventoryDocumentTypeCommand>
{
    public DeactivateInventoryDocumentTypeCommandValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
    }
}

public sealed class DeactivateInventoryDocumentTypeCommandHandler(IApplicationDbContext db, VistaDeTiposDeDocumento vista)
    : IRequestHandler<DeactivateInventoryDocumentTypeCommand, Result<DocumentTypeDto>>
{
    /// <summary>Clases que el sistema genera solo (<see cref="ReglasDeTipoDeDocumento.ClasesDelSistema"/>).</summary>
    public static IReadOnlyList<DocumentClass> ClasesDelSistema => ReglasDeTipoDeDocumento.ClasesDelSistema;

    public async Task<Result<DocumentTypeDto>> Handle(DeactivateInventoryDocumentTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.PublicId == request.DocumentTypePublicId, ct);
        if (tipo is null) return Result.Failure<DocumentTypeDto>(InventoryErrors.DocumentTypeNotFound());
        if (!tipo.IsActive) return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);

        var abiertos = await db.InventoryDocuments.Where(d => d.DocumentTypeId == tipo.Id
                && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval))
            .GroupBy(d => d.Status).Select(g => new { Estado = g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        var quedaOtro = !ReglasDeTipoDeDocumento.ClasesDelSistema.Contains(tipo.Class)
            || await db.InventoryDocumentTypes.AnyAsync(t => t.Class == tipo.Class && t.IsActive && t.Id != tipo.Id, ct);
        var inactivacion = ReglasDeTipoDeDocumento.Inactivacion(tipo.Class,
            abiertos.Where(a => a.Estado == DocumentStatus.Draft).Sum(a => a.Cantidad),
            abiertos.Where(a => a.Estado == DocumentStatus.PendingApproval).Sum(a => a.Cantidad),
            quedaOtro);
        if (inactivacion.IsFailure) return Result.Failure<DocumentTypeDto>(inactivacion.Error);

        tipo.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }
}

/// <summary>Reactivar un tipo (T150; §8, <c>POST /{id}/reactivate</c> con <c>{ reason }</c>). (nuevo)</summary>
public sealed record ReactivateInventoryDocumentTypeCommand(Guid DocumentTypePublicId, string Reason)
    : IRequest<Result<DocumentTypeDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReactivateInventoryDocumentTypeCommandValidator : ValidadorConMotivo<ReactivateInventoryDocumentTypeCommand>
{
    public ReactivateInventoryDocumentTypeCommandValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
    }
}

public sealed class ReactivateInventoryDocumentTypeCommandHandler(IApplicationDbContext db, VistaDeTiposDeDocumento vista)
    : IRequestHandler<ReactivateInventoryDocumentTypeCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(ReactivateInventoryDocumentTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.PublicId == request.DocumentTypePublicId, ct);
        if (tipo is null) return Result.Failure<DocumentTypeDto>(InventoryErrors.DocumentTypeNotFound());
        var disponible = ReglasDeTipoDeDocumento.ClaseDisponible(tipo.Class);
        if (disponible.IsFailure) return Result.Failure<DocumentTypeDto>(disponible.Error);

        tipo.IsActive = true;
        await db.SaveChangesAsync(ct);
        return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }
}
