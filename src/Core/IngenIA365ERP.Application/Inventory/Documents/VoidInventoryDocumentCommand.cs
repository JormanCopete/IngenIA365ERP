using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Anula un documento confirmado con un documento contrario (feature 012, T147; contracts/api.md §9.5, <c>POST /{id}/void</c>
/// con <c>{ reason, operationDate? }</c>; FR-006): crea el <c>Voiding</c> del tipo de anulación, con su propia fecha (hoy,
/// o la indicada; nunca la del original), vínculo <c>Voids</c> y referencias en los dos sentidos, y lo confirma por el
/// flujo canónico —si el tipo de anulación tiene política, pasa por aprobación como cualquier documento—. (nuevo)
/// </summary>
public sealed record VoidInventoryDocumentCommand(Guid DocumentPublicId, DocumentClassGroup ExpectedGroup, string Reason, DateOnly? OperationDate = null)
    : IRequest<Result<VoidResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidInventoryDocumentCommandValidator : ValidadorConMotivo<VoidInventoryDocumentCommand>
{
    public VoidInventoryDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.ExpectedGroup).IsInEnum();
    }
}

/// <summary>
/// En orden: el original existe en el alcance y el grupo (404); no es una anulación (<c>VoidingNotVoidable</c>); no está ya
/// anulado ni con una anulación en curso (<c>AlreadyVoided</c>); está confirmado (<c>NotConfirmed</c>); no es un fiscal
/// que emitió la cooperativa (<c>FiscalUseCorrection</c>: se corrige con su nota); no tiene dependientes vigentes
/// (<c>HasDependents</c>, nombrándolos). Crea el contrario y lo confirma en la misma transacción.
/// </summary>
public sealed class VoidInventoryDocumentCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista,
    ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<VoidInventoryDocumentCommand, Result<VoidResultDto>>
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    public Task<Result<VoidResultDto>> Handle(VoidInventoryDocumentCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => AnularAsync(request, ct), ct);

    private async Task<Result<VoidResultDto>> AnularAsync(VoidInventoryDocumentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var original = await vista.BuscarAsync(request.DocumentPublicId, request.ExpectedGroup, seguir: true, ct);
        if (original is null) return Falla(InventoryErrors.DocumentNotFound());
        if (original.Class == DocumentClass.Voiding) return Falla(InventoryErrors.VoidingNotVoidable());

        var anulacionPrevia = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.VoidsDocumentId == original.Id && d.Status != DocumentStatus.Discarded)
            .Select(d => new { d.PublicId, d.Prefix, d.Number })
            .FirstOrDefaultAsync(ct);
        if (original.Status == DocumentStatus.Voided || anulacionPrevia is not null)
        {
            return Falla(InventoryErrors.AlreadyVoided(anulacionPrevia?.PublicId ?? Guid.Empty,
                anulacionPrevia is null ? null : VistaDeDocumentos.NumeroVisible(anulacionPrevia.Prefix, anulacionPrevia.Number)));
        }
        if (original.Status != DocumentStatus.Confirmed) return Falla(InventoryErrors.NotConfirmed(original.Status));
        if (ClasesDeDocumento.De(original.Class).FiscalDirection == FiscalDirection.Emitted) return Falla(InventoryErrors.FiscalUseCorrection());

        var dependientes = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == original.Id && l.Kind != DocumentLinkKind.Voids)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => Vigentes.Contains(d.Status))
            .Select(d => new { d.PublicId, d.Class, d.Prefix, d.Number, d.Status })
            .ToListAsync(ct);
        if (dependientes.Count > 0)
        {
            return Falla(InventoryErrors.HasDependents(dependientes
                .Select(d => new InventoryErrors.Dependiente(d.PublicId, d.Class.ToString(), VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), d.Status.ToString()))
                .ToList()));
        }

        var tipo = await db.InventoryDocumentTypes.Where(t => t.Class == DocumentClass.Voiding && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return Falla(InventoryErrors.DocumentClassNotAvailable(DocumentClass.Voiding));

        // El contrario: su propia fecha, las bodegas, la contraparte y las líneas del original; el costo lo pone la reversión.
        var anulacion = new InventoryDocument
        {
            Class = DocumentClass.Voiding,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = request.OperationDate ?? reloj.HoyLocal,
            CreatedByUserId = usuario,
            WarehouseId = original.WarehouseId,
            DestinationWarehouseId = original.DestinationWarehouseId,
            TransitWarehouseId = original.TransitWarehouseId,
            BranchId = original.BranchId,
            CostCenterId = original.CostCenterId,
            CounterpartyPersonId = original.CounterpartyPersonId,
            SalespersonId = original.SalespersonId,
            SalesChannelId = original.SalesChannelId,
            Currency = original.Currency,
            ExchangeRate = original.ExchangeRate,
            Reason = request.Reason.Trim(),
            VoidsDocumentId = original.Id,
            Subtotal = original.Subtotal,
            DiscountTotal = original.DiscountTotal,
            TaxTotal = original.TaxTotal,
            WithholdingTotal = original.WithholdingTotal,
            Total = original.Total,
            AmountDue = original.AmountDue,
            CostTotal = original.CostTotal,
        };
        foreach (var linea in original.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
        {
            anulacion.Lines.Add(new InventoryDocumentLine
            {
                Document = anulacion,
                LineNumber = linea.LineNumber,
                ProductId = linea.ProductId,
                UnitId = linea.UnitId,
                Quantity = linea.Quantity,
                Factor = linea.Factor,
                QuantityBase = linea.QuantityBase,
                RoundingQuantity = linea.RoundingQuantity,
                UnitPrice = linea.UnitPrice,
                GrossAmount = linea.GrossAmount,
                DiscountAmount = linea.DiscountAmount,
                NetAmount = linea.NetAmount,
                UnitCost = linea.UnitCost,
                TotalCost = linea.TotalCost,
                LocationId = linea.LocationId,
                ToLocationId = linea.ToLocationId,
                LotId = linea.LotId,
                SerialId = linea.SerialId,
                AdjustmentCauseId = linea.AdjustmentCauseId,
                Description = linea.Description,
            });
        }
        db.InventoryDocuments.Add(anulacion);
        db.DocumentLinks.Add(new DocumentLink { SourceDocument = original, TargetDocument = anulacion, Kind = DocumentLinkKind.Voids });

        // Guardar el borrador del contrario da su Id (lo necesita la referencia del original); la transacción es la misma.
        await db.SaveChangesAsync(ct);

        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(anulacion.PublicId, GrupoEsperado: null), ct);
        if (confirmada.IsFailure) return Falla(confirmada.Error);

        var r = confirmada.Value;
        return Result.Success(new VoidResultDto(anulacion.PublicId, r.DisplayNumber, r.Status, anulacion.OperationDate,
            r.Status == DocumentStatus.Confirmed ? await AjustesDeCostoAsync(anulacion.Id, ct) : null,
            r.Messages?.ToList()));
    }

    /// <summary>
    /// <c>costAdjustments</c> (§9.5, US2 T252): la diferencia de costo que dejó la reversión por producto (líneas
    /// <c>VoidDifference</c> del kardex de la anulación). Es un valor: sólo con <c>Inventory.Costs.Read</c>.
    /// </summary>
    private async Task<IReadOnlyList<AjusteDeCostoDeAnulacionDto>?> AjustesDeCostoAsync(int anulacionId, CancellationToken ct)
    {
        if (!await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct)) return null;
        var diferencias = await db.KardexEntries.AsNoTracking()
            .Where(k => k.DocumentId == anulacionId && k.Kind == KardexEntryKind.CostAdjustment && k.Reason == KardexReason.VoidDifference)
            .GroupBy(k => k.ProductId)
            .Select(g => new { ProductId = g.Key, Diferencia = g.Sum(k => k.TotalCost) })
            .ToListAsync(ct);
        if (diferencias.Count == 0) return [];
        var ids = diferencias.Select(d => d.ProductId).ToList();
        var productos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        return diferencias.Where(d => productos.ContainsKey(d.ProductId))
            .Select(d => new AjusteDeCostoDeAnulacionDto(productos[d.ProductId], d.Diferencia))
            .OrderBy(d => d.Product.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static Result<VoidResultDto> Falla(Error error) => Result.Failure<VoidResultDto>(error);
}
