using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// Resuelve un faltante o un sobrante de traslado (feature 012, US10, T371; FR-039; contracts/api.md §11,
/// <c>POST /transfers/discrepancies/{id}/resolve</c>, permiso <c>Inventory.Transfers.Receive</c>): crea el documento que la resuelve
/// —recepción al origen (<c>ReturnToOrigin</c>, <c>ReturnOf</c>), baja desde el tránsito (<c>WriteOffFromTransit</c>), recepción
/// tardía al destino (<c>LateReceipt</c>, <c>ReceiptOf</c>) o ajuste positivo en el destino al costo vigente
/// (<c>SurplusAdjustment</c>)— y lo deja <b>en aprobación</b>: siempre la decide otra persona. (nuevo)
/// </summary>
public sealed record ResolveTransferDiscrepancyCommand(
    Guid DiscrepancyPublicId,
    TransferDiscrepancyResolution Resolution,
    string Reason,
    decimal? Quantity = null,
    Guid? AdjustmentCausePublicId = null,
    DateOnly? OperationDate = null,
    Guid? ToLocationPublicId = null) : IRequest<Result<ResolveTransferDiscrepancyResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ResolveTransferDiscrepancyCommandValidator : ValidadorConMotivo<ResolveTransferDiscrepancyCommand>
{
    public ResolveTransferDiscrepancyCommandValidator()
    {
        RuleFor(x => x.DiscrepancyPublicId).NotEmpty();
        RuleFor(x => x.Resolution).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity is not null).WithMessage("La cantidad va positiva.");
        RuleFor(x => x.Quantity).Must(q => q is null || SaveInventoryDraftCommandValidator.Decimales(q.Value) <= 4)
            .WithMessage("La cantidad admite hasta 4 decimales.");
    }
}

/// <summary>
/// En orden, dentro de una <see cref="TransaccionExplicita"/>: la diferencia existe y su traslado está al alcance de quien la
/// resuelve —destino u origen— (si no, 404); está pendiente (<c>Inventory.TransferDiscrepancy.NotPending</c>); la salida corresponde a
/// su tipo (<c>.ResolutionNotAllowed</c>); la cantidad no pasa de lo pendiente (<c>.QuantityExceeds</c>); la causa, obligatoria en la
/// baja y el sobrante (<c>Inventory.Document.FieldRequired</c>), sirve para esa salida (<c>.CauseNotAllowed</c>). Crea el documento,
/// lo deja en aprobación y abre la solicitud por <see cref="IMotorDeAprobaciones.SolicitarAsync"/> con <c>Subject = TransferDiscrepancy</c>,
/// la política del sujeto para el tipo de la <b>recepción</b> de traslado (o, sin política, un nivel con
/// <c>Inventory.Transfers.Approve</c>) y excluidos quien despachó, quien recibió y quien propone. Si la política vigente no pide
/// niveles para ese monto, el documento se confirma y la diferencia se resuelve en la misma transacción.
/// </summary>
public sealed class ResolveTransferDiscrepancyCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IDateTimeService reloj,
    IMotorDeAprobaciones motor,
    CierreDeDiferencias cierre)
    : IRequestHandler<ResolveTransferDiscrepancyCommand, Result<ResolveTransferDiscrepancyResultDto>>
{
    public Task<Result<ResolveTransferDiscrepancyResultDto>> Handle(ResolveTransferDiscrepancyCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => ResolverAsync(request, ct), ct);

    private async Task<Result<ResolveTransferDiscrepancyResultDto>> ResolverAsync(ResolveTransferDiscrepancyCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) La diferencia y su traslado, al alcance.
        var diferencia = await cierre.BuscarAsync(request.DiscrepancyPublicId, ct);
        if (diferencia is null) return Falla(ErroresDeTraslados.DiscrepancyNotFound());
        var despacho = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == diferencia.DispatchDocumentId, ct);
        var recepcion = await db.InventoryDocuments.AsNoTracking().Include(d => d.DocumentType).FirstAsync(d => d.Id == diferencia.ReceiptDocumentId, ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!((despacho.DestinationWarehouseId is int d && alcance.IncluyeBodega(d)) || (despacho.WarehouseId is int o && alcance.IncluyeBodega(o))))
            return Falla(ErroresDeTraslados.DiscrepancyNotFound());

        // (2) Pendiente, salida admitida, cantidad y causa.
        if (diferencia.Estado != TransferDiscrepancy.EstadoPendiente) return Falla(ErroresDeTraslados.DiscrepancyNotPending(diferencia.Estado));
        if (!diferencia.Admite(request.Resolution))
            return Falla(ErroresDeTraslados.ResolutionNotAllowed(diferencia.Kind, TransferDiscrepancy.ResolucionesAdmitidas(diferencia.Kind)));
        var pendiente = diferencia.Pendiente();
        var cantidad = request.Quantity ?? pendiente;
        if (cantidad > pendiente) return Falla(ErroresDeTraslados.QuantityExceeds(pendiente));

        int? causaId = null;
        if (TransferDiscrepancy.ExigeCausa(request.Resolution))
        {
            if (request.AdjustmentCausePublicId is not { } cp) return Falla(InventoryErrors.FieldRequired(InventoryErrors.CampoCausa));
            var causa = await db.AdjustmentCauses.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == cp, ct);
            if (causa is null) return Falla(ErroresDelDocumento.CausaInexistente());
            var sirve = request.Resolution == TransferDiscrepancyResolution.WriteOffFromTransit ? causa.AllowsTransitWriteOff : causa.AllowsPositive;
            if (!sirve || !causa.IsActive) return Falla(ErroresDeTraslados.CauseNotAllowed(causa.Code, request.Resolution));
            causaId = causa.Id;
        }

        // (3) El documento que la resuelve.
        var armado = await ArmarAsync(request, diferencia, despacho, recepcion, cantidad, causaId, usuario, ct);
        if (armado.IsFailure) return Falla(armado.Error);
        var documento = armado.Value;
        await db.SaveChangesAsync(ct);

        diferencia.PedirResolucion(request.Resolution, cantidad, usuario, reloj.UtcNow, request.Reason, causaId, documento.Id);
        documento.EnviarAAprobacion();

        // (4) La solicitud: la política de la diferencia para el tipo de la recepción de traslado.
        var costo = diferencia.UnitCost ?? await CostoVigenteAsync(diferencia.ProductId, ct);
        var participantes = new[] { despacho.CreatedByUserId, despacho.ConfirmedByUserId, recepcion.CreatedByUserId, recepcion.ConfirmedByUserId }
            .OfType<int>().Distinct().ToList();
        var bodegaDeAlcance = await db.Warehouses.AsNoTracking().Where(w => w.Id == despacho.DestinationWarehouseId).Select(w => (Guid?)w.PublicId).FirstOrDefaultAsync(ct);
        var solicitud = await motor.SolicitarAsync(new SolicitudDeAprobacion(
            ApprovalSubjects.TransferDiscrepancy,
            ApprovalSourceTypes.TransferDiscrepancy,
            diferencia.PublicId,
            $"{(diferencia.Kind == TransferDiscrepancyKind.Shortage ? "Faltante" : "Sobrante")} del traslado {VistaDeDocumentos.NumeroVisible(despacho.Prefix, despacho.Number)}",
            recepcion.DocumentType!.PublicId,
            bodegaDeAlcance,
            null,
            Math.Round(cantidad * costo, 2, MidpointRounding.AwayFromZero),
            documento.OperationDate,
            usuario,
            participantes,
            ConfirmacionDeDocumento.Huella(documento),
            null), ct);
        if (solicitud.IsFailure) return Falla(solicitud.Error);

        Guid? solicitudId = solicitud.Value?.PublicId;
        if (solicitud.Value is null)
        {
            // La política vigente no pide niveles para este monto: lo decide ella; se confirma ya.
            await db.SaveChangesAsync(ct);
            var confirmada = await cierre.AlAprobarAsync(diferencia, ct);
            if (confirmada.IsFailure) return Falla(confirmada.Error);
        }
        await db.SaveChangesAsync(ct);

        return Result.Success(new ResolveTransferDiscrepancyResultDto(diferencia.PublicId, request.Resolution, documento.PublicId, documento.Class,
            documento.Status, solicitudId));
    }

    /// <summary>El documento de cada salida (api.md §11), en borrador, con su vínculo al despacho cuando saca del tránsito.</summary>
    private async Task<Result<InventoryDocument>> ArmarAsync(ResolveTransferDiscrepancyCommand request, TransferDiscrepancy diferencia,
        InventoryDocument despacho, InventoryDocument recepcion, decimal cantidad, int? causaId, int usuario, CancellationToken ct)
    {
        var lineaDelDespacho = despacho.Lines.First(l => l.Id == diferencia.DispatchLineId);
        var baseDelProducto = await db.Products.AsNoTracking().Where(p => p.Id == diferencia.ProductId).Select(p => p.BaseUnitId).FirstAsync(ct);
        var (clase, bodega, kind) = request.Resolution switch
        {
            TransferDiscrepancyResolution.ReturnToOrigin => (DocumentClass.TransferReceipt, despacho.WarehouseId!.Value, (DocumentLinkKind?)DocumentLinkKind.ReturnOf),
            TransferDiscrepancyResolution.LateReceipt => (DocumentClass.TransferReceipt, despacho.DestinationWarehouseId!.Value, DocumentLinkKind.ReceiptOf),
            // La baja consume la línea de despacho como lo que sale del tránsito (así deja de contar «en tránsito»).
            TransferDiscrepancyResolution.WriteOffFromTransit => (DocumentClass.WriteOff, despacho.TransitWarehouseId!.Value, DocumentLinkKind.ReceiptOf),
            _ => (DocumentClass.PositiveAdjustment, despacho.DestinationWarehouseId!.Value, (DocumentLinkKind?)null),
        };

        var tipo = clase == DocumentClass.TransferReceipt
            ? await db.InventoryDocumentTypes.FirstAsync(t => t.Id == recepcion.DocumentTypeId, ct)
            : await db.InventoryDocumentTypes.Where(t => t.Class == clase && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return Result.Failure<InventoryDocument>(InventoryErrors.DocumentClassNotAvailable(clase));

        int? ubicacion = null;
        if (request.ToLocationPublicId is { } lu)
        {
            var u = await db.WarehouseLocations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == lu, ct);
            if (u is null) return Result.Failure<InventoryDocument>(ErroresDelDocumento.UbicacionInexistente());
            if (u.WarehouseId != bodega || clase == DocumentClass.WriteOff)
                return Result.Failure<InventoryDocument>(InventoryErrors.LocationNotInWarehouse(1, string.Empty));
            ubicacion = u.Id;
        }
        int? ubicacionDelTransito = null;
        if (clase == DocumentClass.WriteOff)
        {
            ubicacionDelTransito = await db.KardexEntries.AsNoTracking()
                .Where(k => k.DocumentLineId == lineaDelDespacho.Id && k.WarehouseId == bodega && k.Kind == KardexEntryKind.Entry)
                .Select(k => (int?)k.LocationId).FirstOrDefaultAsync(ct);
        }

        var sucursal = await db.Warehouses.AsNoTracking().Where(w => w.Id == bodega).Select(w => w.BranchId).FirstAsync(ct);
        var documento = new InventoryDocument
        {
            Class = clase,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = request.OperationDate ?? reloj.HoyLocal,
            CreatedByUserId = usuario,
            WarehouseId = clase == DocumentClass.TransferReceipt ? despacho.WarehouseId : bodega,
            DestinationWarehouseId = clase == DocumentClass.TransferReceipt ? despacho.DestinationWarehouseId : null,
            TransitWarehouseId = clase == DocumentClass.TransferReceipt ? despacho.TransitWarehouseId : null,
            BranchId = sucursal,
            Reason = request.Reason.Trim(),
            ExternalReference = despacho.ExternalReference,
            Currency = despacho.Currency,
            ExchangeRate = despacho.ExchangeRate,
        };
        var linea = new InventoryDocumentLine
        {
            Document = documento,
            LineNumber = 1,
            ProductId = diferencia.ProductId,
            UnitId = baseDelProducto,
            Quantity = cantidad,
            Factor = 1m,
            QuantityBase = cantidad,
            LotId = diferencia.LotId,
            AdjustmentCauseId = causaId,
            // La recepción entra por ToLocationId; el ajuste positivo, por LocationId; la baja sale de donde entró al tránsito.
            ToLocationId = clase == DocumentClass.TransferReceipt ? ubicacion : null,
            LocationId = clase == DocumentClass.PositiveAdjustment ? ubicacion : ubicacionDelTransito,
        };
        documento.Lines.Add(linea);
        db.InventoryDocuments.Add(documento);

        if (kind is { } k2)
        {
            var vinculo = new DocumentLink { SourceDocumentId = despacho.Id, TargetDocument = documento, Kind = k2 };
            vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = lineaDelDespacho.Id, TargetLine = linea, QuantityBase = cantidad });
            db.DocumentLinks.Add(vinculo);
        }
        return Result.Success(documento);
    }

    /// <summary>El costo vigente del producto (el promedio de la cooperativa, o el último costo) para valorar un sobrante que se pide.</summary>
    private async Task<decimal> CostoVigenteAsync(int productoId, CancellationToken ct)
    {
        var estado = await db.CostStates.AsNoTracking().Where(c => c.ProductId == productoId).OrderBy(c => c.ScopeWarehouseId).FirstOrDefaultAsync(ct);
        return estado is null ? 0m : estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost;
    }

    private static Result<ResolveTransferDiscrepancyResultDto> Falla(Error error) => Result.Failure<ResolveTransferDiscrepancyResultDto>(error);
}
