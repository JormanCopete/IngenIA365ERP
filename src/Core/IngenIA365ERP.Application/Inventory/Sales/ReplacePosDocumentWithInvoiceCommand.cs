using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>Lo que devuelve la factura después del documento equivalente (api.md §18.3.1). (nuevo)</summary>
public sealed record FacturaEnLugarDelDocumentoEquivalenteDto(
    Guid AdjustmentNotePublicId,
    string? AdjustmentNoteNumber,
    Guid InvoicePublicId,
    string? InvoiceNumber,
    IReadOnlyList<MensajeEmitidoDto>? Messages);

/// <summary>
/// La factura que pide el comprador cuando el documento equivalente POS ya se expidió (feature 012, I4, T739; contracts/api.md §18.3.1;
/// FR-063; <c>POST /api/inventory/sales/documents/{id}/invoice-instead</c>, permiso <c>Inventory.Sales.Confirm</c>). Una sola acción del
/// cajero. (nuevo)
/// </summary>
public sealed record ReplacePosDocumentWithInvoiceCommand(Guid DocumentPublicId, Guid BuyerPersonPublicId, string Reason, decimal ExpectedAmountDue)
    : IRequest<Result<FacturaEnLugarDelDocumentoEquivalenteDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReplacePosDocumentWithInvoiceCommandValidator : ValidadorConMotivo<ReplacePosDocumentWithInvoiceCommand>
{
    public ReplacePosDocumentWithInvoiceCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.BuyerPersonPublicId).NotEmpty().WithMessage("Indique el comprador a cuyo nombre sale la factura.");
        RuleFor(x => x.ExpectedAmountDue).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// En <b>una</b> transacción (api.md §18.3.1):
/// <list type="number">
/// <item>el original es un <c>PosEquivalentDocument</c> confirmado, expedido y sin notas (<c>Inventory.Sales.InvoiceInsteadNotApplicable</c>);
/// enviado sin respuesta → <c>ElectronicInvoicing.Document.AwaitingResponse</c>; <c>expectedAmountDue</c> igual a lo que dice
/// (<c>Payments.TotalMismatch</c>);</item>
/// <item>la <c>PosAdjustmentNote</c> de anulación total, sin devolución y con el concepto de anulación del catálogo DIAN, por el flujo canónico;
/// no reintegra (<see cref="TrasladoDeVentaEnCurso"/>);</item>
/// <item>la <c>SalesInvoice</c> con las mismas líneas, precios, impuestos, vendedor y bodega, a nombre de <see cref="ReplacePosDocumentWithInvoiceCommand.BuyerPersonPublicId"/>,
/// del tipo del rol <c>InvoiceOnRequest</c> de la caja del original (o el de contingencia si hay 03 abierta), con los pagos del original
/// trasladados sin sesión de caja, <c>ReplacementOf</c> hacia el original, sin kardex, y que se transmite <b>después</b> de la nota
/// (<c>WaitsForDocumentId</c>).</item>
/// </list>
/// Un error deja todo como estaba. (nuevo)
/// </summary>
public sealed class ReplacePosDocumentWithInvoiceCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    SaveCreditNoteDraftCommandHandler notas,
    ConfirmacionDeDocumento confirmacion,
    TrasladoDeVentaEnCurso traslado,
    EmisionFiscalDeLaConfirmacion? emisionFiscal = null)
    : IRequestHandler<ReplacePosDocumentWithInvoiceCommand, Result<FacturaEnLugarDelDocumentoEquivalenteDto>>
{
    public const string InvoiceInsteadNotApplicableCode = "Inventory.Sales.InvoiceInsteadNotApplicable";

    public Task<Result<FacturaEnLugarDelDocumentoEquivalenteDto>> Handle(ReplacePosDocumentWithInvoiceCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await ReemplazarAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<FacturaEnLugarDelDocumentoEquivalenteDto>> ReemplazarAsync(ReplacePosDocumentWithInvoiceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) El original.
        var dee = await db.InventoryDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.PublicId == request.DocumentPublicId && !d.IsDeleted, ct);
        if (dee is null) return Falla(InventoryErrors.DocumentNotFound());
        if (dee.Class != DocumentClass.PosEquivalentDocument || dee.Status != DocumentStatus.Confirmed)
            return Falla(NoAplica("Sólo se pide factura sobre un documento equivalente POS expedido."));
        var electronico = await EstadoElectronicoDeInventario.DeAsync(db, dee.PublicId, ct);
        if (electronico?.Status == ElectronicDocumentStatus.Sent) return Falla(ErroresDeVentas.AwaitingResponse(electronico.PublicId));
        if (electronico?.Status is ElectronicDocumentStatus.Rejected or ElectronicDocumentStatus.CancelledWithoutReplacement)
            return Falla(NoAplica("La DIAN rechazó el documento equivalente: no está expedido y se resuelve desde Documentos electrónicos."));
        var conNotas = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == dee.Id && !l.IsDeleted && (l.Kind == DocumentLinkKind.NoteOf || l.Kind == DocumentLinkKind.ReplacementOf))
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d.Status)
            .AnyAsync(s => s != DocumentStatus.Discarded, ct);
        if (conNotas) return Falla(NoAplica("El documento equivalente ya tiene notas o un reemplazo: no se pide factura sobre él."));
        if (request.ExpectedAmountDue != dee.AmountDue)
            return Falla(new ErrorConDatos(ValidadorDePagos.TotalMismatch, "El valor esperado no coincide con el del documento equivalente.",
                new { amountDue = dee.AmountDue, expected = request.ExpectedAmountDue }));

        var comprador = await db.People.AsNoTracking().Where(p => p.PublicId == request.BuyerPersonPublicId && !p.IsDeleted)
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (comprador is null) return Falla(ErroresDelDocumento.PersonaInexistente());

        // (2) El tipo de la factura: el del rol InvoiceOnRequest de la caja del original.
        var caja = dee.CashRegisterId is int cajaId
            ? await db.CashRegisters.Include(c => c.DocumentTypes.Where(t => !t.IsDeleted)).FirstOrDefaultAsync(c => c.Id == cajaId, ct)
            : null;
        var rolFactura = caja?.DocumentTypes.FirstOrDefault(t => t.Role == CashRegisterDocumentRole.InvoiceOnRequest);
        if (caja is null || rolFactura is null) return Falla(NoAplica("La caja del documento equivalente no tiene tipo de factura a solicitud del comprador."));
        var tipoFactura = await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstAsync(t => t.Id == rolFactura.DocumentTypeId, ct);

        // (3) La nota de ajuste de anulación total, sin devolución y sin reintegros.
        var hoy = reloj.HoyLocal;
        var anulacion = CatalogoDian.Embebido.ConceptosDeCorreccion(ClaseDeNotaDian.NotaDeAjustePos, hoy).FirstOrDefault(c => c.EsAnulacion);
        if (anulacion is null) return Falla(ErroresDeVentas.CorrectionConceptRequired());
        var borrador = await notas.Handle(new SaveCreditNoteDraftCommand(null, new CreditNoteDraftInput(
            dee.PublicId, request.Reason, TotalVoid: true, WithReturn: false, [], [], OperationDate: hoy, CorrectionConceptCode: anulacion.Codigo)), ct);
        if (borrador.IsFailure) return Falla(borrador.Error);
        var nota = await db.InventoryDocuments.FirstAsync(d => d.PublicId == borrador.Value.PublicId, ct);
        foreach (var reintegro in await db.DocumentPayments.Where(p => p.DocumentId == nota.Id && !p.IsDeleted).ToListAsync(ct))
        {
            reintegro.IsDeleted = true;
            reintegro.DeletedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        traslado.Nota(nota.PublicId, dee.PublicId);
        var notaConfirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(nota.PublicId, DocumentClassGroup.Sales), ct);
        if (notaConfirmada.IsFailure) return Falla(notaConfirmada.Error);
        if (notaConfirmada.Value.Status != DocumentStatus.Confirmed)
            return Falla(NoAplica("La nota de ajuste requiere aprobación: la factura en lugar del documento equivalente se hace en una sola acción."));

        // (4) La factura: las mismas líneas, impuestos y totales, los pagos trasladados sin sesión, ReplacementOf al original.
        var factura = new InventoryDocument
        {
            Class = DocumentClass.SalesInvoice,
            DocumentTypeId = tipoFactura.Id,
            DocumentType = tipoFactura,
            OperationDate = hoy,
            CreatedByUserId = usuario,
            WarehouseId = dee.WarehouseId,
            BranchId = dee.BranchId,
            CostCenterId = dee.CostCenterId,
            CounterpartyPersonId = comprador,
            SalespersonId = dee.SalespersonId,
            SalesChannelId = dee.SalesChannelId,
            PointOfSaleId = dee.PointOfSaleId,
            CashRegisterId = dee.CashRegisterId,
            Currency = dee.Currency,
            ExchangeRate = dee.ExchangeRate,
            DueDate = dee.DueDate,
            Reason = request.Reason.Trim(),
            Notes = dee.Notes,
            Subtotal = dee.Subtotal,
            DiscountTotal = dee.DiscountTotal,
            TaxTotal = dee.TaxTotal,
            WithholdingTotal = dee.WithholdingTotal,
            Total = dee.Total,
            AmountDue = dee.AmountDue,
        };
        foreach (var l in dee.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
        {
            factura.Lines.Add(new InventoryDocumentLine
            {
                Document = factura, LineNumber = l.LineNumber, ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity, Factor = l.Factor,
                QuantityBase = l.QuantityBase, RoundingQuantity = l.RoundingQuantity, UnitPrice = l.UnitPrice, ListPrice = l.ListPrice,
                PriceListId = l.PriceListId, ListPriceIncludesTaxes = l.ListPriceIncludesTaxes, GrossAmount = l.GrossAmount,
                DiscountAmount = l.DiscountAmount, NetAmount = l.NetAmount, LocationId = l.LocationId, Description = l.Description,
            });
        }
        db.InventoryDocuments.Add(factura);
        db.DocumentLinks.Add(new DocumentLink { SourceDocument = dee, TargetDocument = factura, Kind = DocumentLinkKind.ReplacementOf });
        var pagos = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == dee.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received).OrderBy(p => p.LineNumber).ToListAsync(ct);
        foreach (var p in pagos)
        {
            db.DocumentPayments.Add(new DocumentPayment
            {
                Document = factura, LineNumber = p.LineNumber, PaymentMeansId = p.PaymentMeansId, Direction = PaymentDirection.Received, Amount = p.Amount,
                Reference = p.Reference, NormalizedReference = p.NormalizedReference, AuthorizationCode = p.AuthorizationCode, CardTerminalId = p.CardTerminalId,
                TerminalBatchNumber = p.TerminalBatchNumber, Last4 = p.Last4, CashSessionId = null, MeansCode = p.MeansCode, MeansName = p.MeansName,
                MeansClass = p.MeansClass, CardNetworkCode = p.CardNetworkCode, CardAcquirerCode = p.CardAcquirerCode,
                CardAcquirerPersonId = p.CardAcquirerPersonId, BankId = p.BankId, DianPaymentMeansCode = p.DianPaymentMeansCode,
                ExpectedCommissionAmount = p.ExpectedCommissionAmount, CreditTermDays = p.CreditTermDays, InstallmentCount = p.InstallmentCount,
                InstallmentPeriodDays = p.InstallmentPeriodDays, FirstDueDate = p.FirstDueDate, FinalDueDate = p.FinalDueDate,
                SuggestedCreditLineCode = p.SuggestedCreditLineCode, CreditOrigin = p.CreditOrigin,
                AccountsReceivableRecordedBy = p.AccountsReceivableRecordedBy,
            });
        }
        await TipoDeVentaEnContingencia.AplicarAsync(db, factura, caja, ct);
        await db.SaveChangesAsync(ct);

        traslado.Factura(factura.PublicId, dee.PublicId);
        emisionFiscal?.EsperarA(factura.PublicId, nota.PublicId);
        var facturaConfirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura.PublicId, DocumentClassGroup.Sales), ct);
        if (facturaConfirmada.IsFailure) return Falla(facturaConfirmada.Error);
        if (facturaConfirmada.Value.Status != DocumentStatus.Confirmed)
            return Falla(NoAplica("La factura requiere aprobación: la factura en lugar del documento equivalente se hace en una sola acción."));

        var mensajes = notaConfirmada.Value.Messages is null && facturaConfirmada.Value.Messages is null
            ? null
            : (notaConfirmada.Value.Messages ?? []).Concat(facturaConfirmada.Value.Messages ?? []).ToList();
        return Result.Success(new FacturaEnLugarDelDocumentoEquivalenteDto(nota.PublicId, notaConfirmada.Value.DisplayNumber,
            factura.PublicId, facturaConfirmada.Value.DisplayNumber, mensajes));
    }

    private static Error NoAplica(string motivo) => new(InvoiceInsteadNotApplicableCode, motivo);

    private static Result<FacturaEnLugarDelDocumentoEquivalenteDto> Falla(Error error) => Result.Failure<FacturaEnLugarDelDocumentoEquivalenteDto>(error);
}
