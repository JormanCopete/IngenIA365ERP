using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>Lo recibido de una línea del despacho: cantidad (en su unidad o la del despacho), ubicación de llegada y sobrante declarado.</summary>
public sealed record LineaRecibidaRequest(
    Guid DispatchLinePublicId,
    decimal ReceivedQuantity,
    Guid? UnitPublicId = null,
    Guid? ToLocationPublicId = null,
    decimal? SurplusQuantity = null);

/// <summary>
/// Recibe un traslado (feature 012, US10, T370; FR-019, FR-039, US10-2; contracts/api.md §11, <c>POST /transfers/{id}/receive</c>,
/// permiso <c>Inventory.Transfers.Receive</c> y alcance sobre el <b>destino</b>): crea <b>y</b> confirma la recepción en una
/// operación. Lo que no llega queda en tránsito como faltante pendiente; lo que llega de más, fuera de la existencia, como sobrante
/// pendiente (<c>INV_TransferDiscrepancies</c>). Una línea del despacho que no se nombra se recibe en cero. (nuevo)
/// </summary>
public sealed record ReceiveTransferCommand(
    Guid DispatchPublicId,
    IReadOnlyList<LineaRecibidaRequest> Lines,
    DateOnly? OperationDate = null,
    Guid? DocumentTypePublicId = null,
    string? Notes = null) : IRequest<Result<ReceiveTransferResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReceiveTransferCommandValidator : AbstractValidator<ReceiveTransferCommand>
{
    public ReceiveTransferCommandValidator()
    {
        RuleFor(x => x.DispatchPublicId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().WithMessage("Indicá lo recibido de cada línea.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.DispatchLinePublicId).NotEmpty();
            l.RuleFor(x => x.ReceivedQuantity).GreaterThanOrEqualTo(0).WithMessage("La cantidad recibida no puede ser negativa.");
            l.RuleFor(x => x.SurplusQuantity).GreaterThanOrEqualTo(0).When(x => x.SurplusQuantity is not null)
                .WithMessage("El sobrante no puede ser negativo.");
            l.RuleFor(x => x.ReceivedQuantity).Must(q => SaveInventoryDraftCommandValidator.Decimales(q) <= 4).WithMessage("La cantidad admite hasta 4 decimales.");
        });
        RuleFor(x => x.Lines).Must(ls => ls is null || ls.Select(l => l.DispatchLinePublicId).Distinct().Count() == ls.Count)
            .WithMessage("Cada línea del despacho va una sola vez.");
    }
}

/// <summary>
/// En orden, dentro de una <see cref="TransaccionExplicita"/>: el despacho existe, es del grupo y el <b>destino</b> está en el alcance
/// (si no, 404); está en tránsito (<c>Inventory.Transfer.NotInTransit</c>) y sin recepción (<c>.AlreadyReceived</c>); la fecha (hoy por
/// defecto, en el período abierto aunque el despacho sea de un mes cerrado) no es anterior al despacho (<c>.ReceiptBeforeDispatch</c>);
/// cada línea en unidad base no pasa de lo despachado (<c>.ReceiveExceedsDispatched</c>) y su ubicación es del destino. Arma la
/// recepción con las tres bodegas del despacho y vínculos <c>ReceiptOf</c> por línea, la confirma por el flujo canónico
/// (<c>EfectoRecepcionDeTraslado</c>: al costo de la línea de despacho, derivada de él) y registra las diferencias.
/// </summary>
public sealed class ReceiveTransferCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IDateTimeService reloj,
    IMaestrosDelDocumento maestros,
    VistaDeDocumentos vista,
    ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<ReceiveTransferCommand, Result<ReceiveTransferResultDto>>
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    public Task<Result<ReceiveTransferResultDto>> Handle(ReceiveTransferCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => RecibirAsync(request, ct), ct);

    private async Task<Result<ReceiveTransferResultDto>> RecibirAsync(ReceiveTransferCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) El despacho, del grupo y visible; la recepción exige el destino en el alcance.
        var despacho = await vista.BuscarAsync(request.DispatchPublicId, DocumentClassGroup.Transfers, seguir: true, ct);
        if (despacho is null || despacho.Class != DocumentClass.TransferDispatch || despacho.DestinationWarehouseId is not int destinoId)
            return Falla(InventoryErrors.DocumentNotFound());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.IncluyeBodega(destinoId)) return Falla(InventoryErrors.DocumentNotFound());

        // (2) En tránsito y sin recepción.
        if (despacho.Status != DocumentStatus.Confirmed) return Falla(ErroresDeTraslados.NotInTransit(despacho.Status));
        var previa = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == despacho.Id && l.Kind == DocumentLinkKind.ReceiptOf)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => d.Class == DocumentClass.TransferReceipt && Vigentes.Contains(d.Status))
            .OrderBy(d => d.Id)
            .Select(d => new { d.PublicId, d.Prefix, d.Number })
            .FirstOrDefaultAsync(ct);
        if (previa is not null) return Falla(ErroresDeTraslados.AlreadyReceived(previa.PublicId, VistaDeDocumentos.NumeroVisible(previa.Prefix, previa.Number)));

        // (3) El tipo y la fecha.
        var tipo = request.DocumentTypePublicId is { } tp
            ? await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.PublicId == tp, ct)
            : await db.InventoryDocumentTypes.Where(t => t.Class == DocumentClass.TransferReceipt && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return Falla(request.DocumentTypePublicId is null
            ? InventoryErrors.DocumentClassNotAvailable(DocumentClass.TransferReceipt)
            : InventoryErrors.DocumentTypeNotFound());
        if (tipo.Class != DocumentClass.TransferReceipt) return Falla(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Transfers));
        if (!tipo.IsActive) return Falla(InventoryErrors.DocumentTypeInactive(tipo.Code));
        var fecha = request.OperationDate ?? reloj.HoyLocal;
        if (fecha < despacho.OperationDate) return Falla(ErroresDeTraslados.ReceiptBeforeDispatch(despacho.OperationDate));

        // (4) Las líneas: cada una de su despacho, en unidad base, sin pasar de lo despachado; la ubicación, del destino.
        var destino = (await maestros.BodegasPorIdAsync([destinoId], ct)).First();
        var lineasDelDespacho = despacho.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var pedidas = request.Lines.ToDictionary(l => l.DispatchLinePublicId);
        foreach (var pedida in request.Lines)
            if (lineasDelDespacho.All(l => l.PublicId != pedida.DispatchLinePublicId)) return Falla(ErroresDeTraslados.DispatchLineUnknown(pedida.DispatchLinePublicId));

        var ubicacionesPedidas = (await maestros.UbicacionesAsync(request.Lines.Select(l => l.ToLocationPublicId).OfType<Guid>().Distinct().ToList(), ct))
            .ToDictionary(u => u.PublicId);
        var unidadesDelDespacho = (await maestros.UnidadesPorIdAsync(lineasDelDespacho.Select(l => l.UnitId).Distinct().ToList(), ct)).ToDictionary(u => u.Id);
        var productos = (await maestros.ProductosPorIdAsync(lineasDelDespacho.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);

        var calculadas = new List<(InventoryDocumentLine Despachada, decimal Cantidad, int UnidadId, decimal Factor, decimal Recibida, decimal RecibidaResiduo, decimal Sobrante, int? Ubicacion)>();
        foreach (var despachada in lineasDelDespacho)
        {
            var codigo = productos.GetValueOrDefault(despachada.ProductId)?.Code ?? string.Empty;
            pedidas.TryGetValue(despachada.PublicId, out var pedida);
            var unidadPedida = pedida?.UnitPublicId ?? unidadesDelDespacho.GetValueOrDefault(despachada.UnitId)?.PublicId;
            var unidad = unidadPedida is { } up ? await maestros.UnidadAsync(despachada.ProductId, up, ct) : null;
            if (unidad is null) return Falla(InventoryErrors.UnitNotForProduct(despachada.LineNumber, codigo, string.Empty));

            var recibida = Convertir(despachada.LineNumber, pedida?.ReceivedQuantity ?? 0m, unidad);
            if (recibida.Rechazo is { } rechazo)
                return Falla(InventoryErrors.UnitDecimalsNotAllowed(despachada.LineNumber, codigo, rechazo.UnitCode, rechazo.AllowedDecimals, rechazo.QuantityBase));
            if (recibida.QuantityBase > despachada.QuantityBase)
                return Falla(ErroresDeTraslados.ReceiveExceedsDispatched(despachada.LineNumber, despachada.QuantityBase));
            var sobrante = Convertir(despachada.LineNumber, pedida?.SurplusQuantity ?? 0m, unidad);
            if (sobrante.Rechazo is { } rechazoSobrante)
                return Falla(InventoryErrors.UnitDecimalsNotAllowed(despachada.LineNumber, codigo, rechazoSobrante.UnitCode, rechazoSobrante.AllowedDecimals, rechazoSobrante.QuantityBase));

            int? ubicacion = null;
            if (pedida?.ToLocationPublicId is { } lu)
            {
                if (!ubicacionesPedidas.TryGetValue(lu, out var u)) return Falla(ErroresDelDocumento.UbicacionInexistente());
                if (u.WarehouseId != destinoId) return Falla(InventoryErrors.LocationNotInWarehouse(despachada.LineNumber, codigo));
                ubicacion = u.Id;
            }
            calculadas.Add((despachada, pedida?.ReceivedQuantity ?? 0m, unidad.Id, unidad.Factor, recibida.QuantityBase, recibida.RoundingQuantity, sobrante.QuantityBase, ubicacion));
        }
        if (calculadas.All(c => c.Recibida == 0m)) return Falla(InventoryErrors.Empty());

        // (5) La recepción: las tres bodegas del despacho, la sucursal del destino, vínculos ReceiptOf por línea.
        var recepcion = new InventoryDocument
        {
            Class = DocumentClass.TransferReceipt,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = fecha,
            CreatedByUserId = usuario,
            WarehouseId = despacho.WarehouseId,
            DestinationWarehouseId = despacho.DestinationWarehouseId,
            TransitWarehouseId = despacho.TransitWarehouseId,
            BranchId = destino.BranchId,
            ExternalReference = despacho.ExternalReference,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Currency = despacho.Currency,
            ExchangeRate = despacho.ExchangeRate,
        };
        var vinculo = new DocumentLink { SourceDocumentId = despacho.Id, TargetDocument = recepcion, Kind = DocumentLinkKind.ReceiptOf };
        var numero = 0;
        foreach (var c in calculadas.Where(c => c.Recibida > 0m))
        {
            var linea = new InventoryDocumentLine
            {
                Document = recepcion,
                LineNumber = ++numero,
                ProductId = c.Despachada.ProductId,
                UnitId = c.UnidadId,
                Quantity = c.Cantidad,
                Factor = c.Factor,
                QuantityBase = c.Recibida,
                RoundingQuantity = c.RecibidaResiduo,
                ToLocationId = c.Ubicacion,
                LotId = c.Despachada.LotId,
            };
            recepcion.Lines.Add(linea);
            vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = c.Despachada.Id, TargetLine = linea, QuantityBase = c.Recibida });
        }
        db.InventoryDocuments.Add(recepcion);
        db.DocumentLinks.Add(vinculo);
        await db.SaveChangesAsync(ct);

        // (6) Confirmar por el flujo canónico (o quedar en aprobación, si el tipo tiene política).
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(recepcion.PublicId, DocumentClassGroup.Transfers), ct);
        if (confirmada.IsFailure) return Falla(confirmada.Error);

        // (7) Las diferencias: lo que no llegó sigue en tránsito; lo que llegó de más, fuera de la existencia.
        var faltantes = new List<(TransferDiscrepancy Fila, int ProductoId)>();
        var sobrantes = new List<(TransferDiscrepancy Fila, int ProductoId)>();
        foreach (var c in calculadas)
        {
            var falta = c.Despachada.QuantityBase - c.Recibida;
            if (falta > 0m)
            {
                var fila = Diferencia(despacho, recepcion, c.Despachada, TransferDiscrepancyKind.Shortage, falta, c.Despachada.UnitCost);
                db.TransferDiscrepancies.Add(fila);
                faltantes.Add((fila, c.Despachada.ProductId));
            }
            if (c.Sobrante > 0m)
            {
                var fila = Diferencia(despacho, recepcion, c.Despachada, TransferDiscrepancyKind.Surplus, c.Sobrante, null);
                db.TransferDiscrepancies.Add(fila);
                sobrantes.Add((fila, c.Despachada.ProductId));
            }
        }
        if (faltantes.Count + sobrantes.Count > 0) await db.SaveChangesAsync(ct);

        var r = confirmada.Value;
        return Result.Success(new ReceiveTransferResultDto(recepcion.PublicId, r.DisplayNumber, recepcion.OperationDate, r.Status,
            faltantes.Select(f => Creada(f.Fila, f.ProductoId)).ToList(),
            sobrantes.Select(s => Creada(s.Fila, s.ProductoId)).ToList(),
            r.Messages));

        DiferenciaCreadaDto Creada(TransferDiscrepancy fila, int productoId)
        {
            var p = productos.GetValueOrDefault(productoId);
            return new DiferenciaCreadaDto(fila.PublicId, new ReferenciaDto(p?.PublicId ?? Guid.Empty, p?.Code ?? string.Empty, p?.Name ?? string.Empty), fila.QuantityBase);
        }
    }

    private static TransferDiscrepancy Diferencia(InventoryDocument despacho, InventoryDocument recepcion, InventoryDocumentLine linea,
        TransferDiscrepancyKind tipo, decimal cantidad, decimal? costo) => new()
    {
        DispatchDocumentId = despacho.Id,
        ReceiptDocumentId = recepcion.Id,
        DispatchLineId = linea.Id,
        ProductId = linea.ProductId,
        LotId = linea.LotId,
        Kind = tipo,
        QuantityBase = cantidad,
        UnitCost = costo,
    };

    private static ResultadoDeConversion Convertir(int numero, decimal cantidad, UnidadDelDocumento unidad) =>
        cantidad == 0m
            ? new ResultadoDeConversion(0m, 0m, null)
            : ConversionDeUnidades.Convertir(new PedidoDeConversion(numero, unidad.Code, cantidad, unidad.Factor,
                unidad.DecimalesPermitidos, unidad.BaseUnitCode ?? unidad.Code, unidad.DecimalesDeLaBase));

    private static Result<ReceiveTransferResultDto> Falla(Error error) => Result.Failure<ReceiveTransferResultDto>(error);
}
