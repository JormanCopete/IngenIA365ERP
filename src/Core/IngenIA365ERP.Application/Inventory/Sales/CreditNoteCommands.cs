using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Payments;
using IngenIA365ERP.Domain.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

// ------------------------------------------------------------------------------------------------ entrada --

/// <summary>Una línea de la nota (§18.3): la línea del original y, opcionales, la cantidad (en su unidad) y el valor neto que se acredita. (nuevo)</summary>
public sealed record CreditNoteLineInput(Guid OriginLinePublicId, decimal? Quantity = null, decimal? Amount = null);

/// <summary>
/// <c>CreditNoteDraftInput</c> (contracts/api.md §18.3, feature 012, I3, T612): el cuerpo de <c>POST</c> y <c>PUT
/// /api/inventory/sales/credit-notes</c>. La clase la fija el original (<see cref="NotasDeVenta.ClaseDeNota"/>); sin tipo, el primero activo
/// de esa clase. <see cref="TotalVoid"/> acredita todo lo que queda; <see cref="WithReturn"/> devuelve la mercancía al costo con que salió a
/// <see cref="ReturnWarehousePublicId"/> (por defecto, la bodega del original). Sin <see cref="Refunds"/>, los reintegros van por los medios
/// de la venta y en proporción. (nuevo)
/// </summary>
public sealed record CreditNoteDraftInput(
    Guid OriginDocumentPublicId,
    string Reason,
    bool TotalVoid,
    bool WithReturn,
    IReadOnlyList<CreditNoteLineInput> Lines,
    IReadOnlyList<DocumentPaymentInput> Refunds,
    Guid? DocumentTypePublicId = null,
    DateOnly? OperationDate = null,
    string? CorrectionConceptCode = null,
    Guid? ReturnWarehousePublicId = null,
    byte[]? RowVersion = null);

/// <summary>Crea (<see cref="DocumentPublicId"/> nulo) o reemplaza el borrador de una nota de venta (§18.3). No consume número. (nuevo)</summary>
public sealed record SaveCreditNoteDraftCommand(Guid? DocumentPublicId, CreditNoteDraftInput Draft)
    : IRequest<Result<InventoryDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveCreditNoteDraftCommandValidator : AbstractValidator<SaveCreditNoteDraftCommand>
{
    public SaveCreditNoteDraftCommandValidator()
    {
        RuleFor(x => x.Draft).NotNull();
        RuleFor(x => x.Draft.OriginDocumentPublicId).NotEmpty();
        RuleFor(x => x.Draft.Reason).NotEmpty().MaximumLength(500).WithMessage("Indique el motivo de la nota.");
        RuleFor(x => x.Draft.Lines).NotNull();
        RuleFor(x => x.Draft.Refunds).NotNull();
        RuleFor(x => x.Draft.Lines).Must(l => l.Count > 0).When(x => !x.Draft.TotalVoid).WithMessage("Indique las líneas que acredita la nota.");
        RuleForEach(x => x.Draft.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.OriginLinePublicId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity is not null);
            l.RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null);
        });
        RuleForEach(x => x.Draft.Refunds).ChildRules(p =>
        {
            p.RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
            p.RuleFor(x => x.Amount).GreaterThan(0);
        });
    }
}

// ---------------------------------------------------------------------------------------------- el comando --

/// <summary>
/// El borrador de una nota de venta (feature 012, I3, T612; contracts/api.md §18.3; FR-066, FR-097, T22, T50):
/// <list type="bullet">
/// <item>el original es una venta confirmada del alcance (404 si no); la clase de la nota la fija su clase —<c>CreditNote</c> de una
/// factura, <c>PosAdjustmentNote</c> de un documento equivalente, <c>NonElectronicSalesNote</c> de un comprobante— y un tipo de otra es
/// <c>Inventory.CreditNote.ClassMismatch</c>; una clase ajena a la ruta, <c>Inventory.Document.TypeNotForRoute</c>;</item>
/// <item>en las electrónicas, el concepto de corrección del catálogo DIAN (<see cref="CatalogoDian"/>);</item>
/// <item>cada línea acredita a lo sumo lo que queda (original − notas vivas, calculado por <c>INV_DocumentLineLinks</c>): si se pasa,
/// <c>Inventory.CreditNote.ExceedsRemaining</c> con <c>data.lines</c>; vínculos <c>NoteOf</c> y, con devolución, <c>ReturnOf</c>;</item>
/// <item>impuestos y retenciones con la foto del original en proporción (<see cref="CalculoTributarioDeVenta"/>, E9);</item>
/// <item>reintegros <c>Refunded</c> con <c>RefundsPaymentId</c>: por defecto el mismo medio y en proporción; otro medio exige
/// <c>Inventory.Sales.RefundOtherMeans</c> (sin él <c>Payments.RefundMeansNotAllowed</c>; con él queda auditado); lo que se arquea sale
/// de una sesión abierta del usuario, aunque la venta haya sido en otra caja (T50).</item>
/// </list>
/// Lo que impediría confirmar sin impedir guardar (una sesión que falta) vuelve en <c>warnings[]</c>. El bono reintegrado se libera
/// al confirmar. (nuevo)
/// </summary>
public sealed class SaveCreditNoteDraftCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IMaestrosDelDocumento maestros,
    IPermissionChecker permisos,
    CalculoTributarioDeVenta calculo,
    VistaDeDocumentos vista,
    InventoryAuditEmitter? auditoria = null)
    : IRequestHandler<SaveCreditNoteDraftCommand, Result<InventoryDocumentDto>>
{
    public async Task<Result<InventoryDocumentDto>> Handle(SaveCreditNoteDraftCommand request, CancellationToken ct)
    {
        var entrada = request.Draft;
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) La nota que se reemplaza, si la hay.
        InventoryDocument? nota = null;
        if (request.DocumentPublicId is { } id)
        {
            nota = await vista.BuscarAsync(id, DocumentClassGroup.Sales, seguir: true, ct);
            if (nota is null || !NotasDeVenta.EsNota(nota.Class)) return Falla(InventoryErrors.DocumentNotFound());
            if (nota.Status != DocumentStatus.Draft) return Falla(InventoryErrors.NotDraft(nota.Status));
            if (entrada.RowVersion is { Length: > 0 } leida && nota.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
                return Falla(Error.StaleRowVersion);
        }

        // (2) El original: una venta confirmada del alcance; su clase fija la de la nota.
        var original = await vista.BuscarAsync(entrada.OriginDocumentPublicId, DocumentClassGroup.Sales, seguir: true, ct);
        if (original is null) return Falla(InventoryErrors.DocumentNotFound());
        if (NotasDeVenta.ClaseDeNota(original.Class) is not { } esperada || original.Status != DocumentStatus.Confirmed)
            return Falla(ErroresDeVentas.CreditNoteOriginInvalid());

        // (3) El tipo.
        InventoryDocumentType? tipo;
        if (entrada.DocumentTypePublicId is { } t)
        {
            tipo = await db.InventoryDocumentTypes.Include(x => x.Warehouses).FirstOrDefaultAsync(x => x.PublicId == t, ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
            if (!NotasDeVenta.EsNota(tipo.Class)) return Falla(InventoryErrors.TypeNotForRoute(tipo.Class, ClasesDeDocumento.De(tipo.Class).Group ?? DocumentClassGroup.Sales));
            if (tipo.Class != esperada) return Falla(ErroresDeVentas.CreditNoteClassMismatch(original.Class, esperada));
            if (!tipo.IsActive && nota?.DocumentTypeId != tipo.Id) return Falla(InventoryErrors.DocumentTypeInactive(tipo.Code));
        }
        else
        {
            tipo = await db.InventoryDocumentTypes.Include(x => x.Warehouses).Where(x => x.Class == esperada && x.IsActive).OrderBy(x => x.Code).FirstOrDefaultAsync(ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        }

        // (4) El concepto de corrección de la DIAN en las electrónicas.
        var fecha = entrada.OperationDate ?? nota?.OperationDate ?? reloj.HoyLocal;
        string? concepto = null;
        if (ClasesDeDocumento.De(esperada).IsFiscal)
        {
            var clase = esperada == DocumentClass.PosAdjustmentNote ? ClaseDeNotaDian.NotaDeAjustePos : ClaseDeNotaDian.NotaCredito;
            var hallado = CatalogoDian.Embebido.ConceptoDeCorreccion(clase, entrada.CorrectionConceptCode, fecha);
            if (hallado is null) return Falla(ErroresDeVentas.CorrectionConceptRequired());
            concepto = hallado.Codigo;
        }
        else if (!string.IsNullOrWhiteSpace(entrada.CorrectionConceptCode))
        {
            concepto = entrada.CorrectionConceptCode.Trim();
        }

        // (5) La bodega a la que vuelve la mercancía (por defecto, la del original) y su alcance.
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        int? bodegaId = original.WarehouseId;
        if (entrada.ReturnWarehousePublicId is { } rb)
        {
            var bodega = (await maestros.BodegasAsync([rb], ct)).FirstOrDefault();
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Falla(ErroresDeAlcance.BodegaInexistente());
            bodegaId = bodega.Id;
        }
        var sucursal = bodegaId is int bid ? (await maestros.BodegasPorIdAsync([bid], ct)).FirstOrDefault()?.BranchId ?? original.BranchId : original.BranchId;

        // (6) Las líneas, contra lo que queda por acreditar.
        var restantes = await NotasDeVenta.RestantesAsync(db, original, nota?.Id, ct);
        var vivasDelOriginal = original.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var pedidas = entrada.TotalVoid && entrada.Lines.Count == 0
            ? vivasDelOriginal.Select(l => new CreditNoteLineInput(l.PublicId)).ToList()
            : entrada.Lines.ToList();
        var planeadas = new List<(InventoryDocumentLine Origen, decimal Cantidad, decimal CantidadBase, decimal Neto)>();
        var excedidas = false;
        foreach (var pedida in pedidas)
        {
            var origen = vivasDelOriginal.FirstOrDefault(l => l.PublicId == pedida.OriginLinePublicId);
            if (origen is null) return Falla(ErroresDelDocumento.ProductoInexistente());
            var queda = restantes[origen.Id];
            decimal cantidadBase, neto;
            if (entrada.TotalVoid)
            {
                cantidadBase = entrada.WithReturn ? queda.RemainingQuantity : origen.QuantityBase;
                neto = queda.RemainingAmount;
            }
            else
            {
                var cantidad = pedida.Quantity ?? (entrada.WithReturn ? queda.RemainingQuantity / (origen.Factor == 0m ? 1m : origen.Factor) : origen.Quantity);
                cantidadBase = Math.Round(cantidad * origen.Factor, 4, MidpointRounding.AwayFromZero);
                neto = pedida.Amount ?? (origen.QuantityBase > 0m
                    ? Math.Round(origen.NetAmount * cantidadBase / origen.QuantityBase, 2, MidpointRounding.AwayFromZero)
                    : origen.NetAmount);
            }
            if ((entrada.WithReturn && cantidadBase > queda.RemainingQuantity) || neto > queda.RemainingAmount) excedidas = true;
            if (cantidadBase <= 0m || neto <= 0m) continue;
            planeadas.Add((origen, Math.Round(cantidadBase / (origen.Factor == 0m ? 1m : origen.Factor), 4, MidpointRounding.AwayFromZero), cantidadBase, neto));
        }
        if (excedidas) return Falla(ErroresDeVentas.ExceedsRemaining(restantes.Values.ToList()));
        if (planeadas.Count == 0) return Falla(InventoryErrors.Empty());

        // (7) La cabecera, nueva o reemplazada, y sus líneas y vínculos.
        var ahora = reloj.UtcNow;
        if (nota is null)
        {
            nota = new InventoryDocument { CreatedByUserId = usuario, Currency = InventoryDocument.MonedaPorDefecto, ExchangeRate = 1m };
            db.InventoryDocuments.Add(nota);
        }
        else
        {
            foreach (var vieja in nota.Lines.Where(l => !l.IsDeleted))
            {
                vieja.IsDeleted = true;
                vieja.DeletedAt = ahora;
                vieja.DeletedBy = actor.Name;
            }
            foreach (var vinculo in await db.DocumentLinks.Include(l => l.LineLinks).Where(l => l.TargetDocumentId == nota.Id && !l.IsDeleted).ToListAsync(ct))
            {
                vinculo.IsDeleted = true;
                vinculo.DeletedAt = ahora;
                foreach (var deLinea in vinculo.LineLinks.Where(x => !x.IsDeleted))
                {
                    deLinea.IsDeleted = true;
                    deLinea.DeletedAt = ahora;
                }
            }
            foreach (var pago in await db.DocumentPayments.Where(p => p.DocumentId == nota.Id && !p.IsDeleted).ToListAsync(ct))
            {
                pago.IsDeleted = true;
                pago.DeletedAt = ahora;
            }
        }
        nota.Class = esperada;
        nota.DocumentTypeId = tipo.Id;
        nota.DocumentType = tipo;
        nota.OperationDate = fecha;
        nota.WarehouseId = bodegaId;
        nota.BranchId = sucursal;
        nota.CounterpartyPersonId = original.CounterpartyPersonId;
        nota.SalespersonId = original.SalespersonId;
        nota.SalesChannelId = original.SalesChannelId;
        nota.CostCenterId = original.CostCenterId;
        nota.Reason = entrada.Reason.Trim();
        nota.ReturnsGoods = entrada.WithReturn;
        nota.IsFullReversal = entrada.TotalVoid;
        nota.CorrectionConceptCode = concepto;

        var deNota = new DocumentLink { SourceDocument = original, SourceDocumentId = original.Id, TargetDocument = nota, Kind = DocumentLinkKind.NoteOf };
        db.DocumentLinks.Add(deNota);
        DocumentLink? deDevolucion = null;
        if (entrada.WithReturn)
        {
            deDevolucion = new DocumentLink { SourceDocument = original, SourceDocumentId = original.Id, TargetDocument = nota, Kind = DocumentLinkKind.ReturnOf };
            db.DocumentLinks.Add(deDevolucion);
        }
        var ubicacion = bodegaId is int b2
            ? await db.WarehouseLocations.AsNoTracking().Where(l => l.WarehouseId == b2 && l.IsDefault).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct)
            : null;
        var numero = 0;
        var lineaOriginal = new Dictionary<int, int>();
        foreach (var (origen, cantidad, cantidadBase, neto) in planeadas)
        {
            numero++;
            var bruto = origen.NetAmount > 0m ? Math.Round(origen.GrossAmount * neto / origen.NetAmount, 2, MidpointRounding.AwayFromZero) : neto;
            if (bruto < neto) bruto = neto;
            var linea = new InventoryDocumentLine
            {
                Document = nota,
                LineNumber = numero,
                ProductId = origen.ProductId,
                UnitId = origen.UnitId,
                Quantity = cantidad,
                Factor = origen.Factor,
                QuantityBase = cantidadBase,
                UnitPrice = origen.UnitPrice,
                ListPrice = origen.ListPrice,
                PriceListId = origen.PriceListId,
                ListPriceIncludesTaxes = origen.ListPriceIncludesTaxes,
                GrossAmount = bruto,
                DiscountAmount = bruto - neto,
                NetAmount = neto,
                LocationId = entrada.WithReturn && bodegaId == original.WarehouseId ? origen.LocationId ?? ubicacion : ubicacion,
            };
            nota.Lines.Add(linea);
            lineaOriginal[numero] = origen.LineNumber;
            deNota.LineLinks.Add(new DocumentLineLink { DocumentLink = deNota, SourceLine = origen, SourceLineId = origen.Id, TargetLine = linea, QuantityBase = cantidadBase });
            deDevolucion?.LineLinks.Add(new DocumentLineLink { DocumentLink = deDevolucion, SourceLine = origen, SourceLineId = origen.Id, TargetLine = linea, QuantityBase = cantidadBase });
        }

        // (8) Impuestos y retenciones con la foto del original en proporción (E9), y totales (T26).
        var impuestos = await NotasDeVenta.ImpuestosAsync(db, calculo, original, nota, lineaOriginal, ct);
        if (impuestos.IsFailure)
        {
            db.DescartarCambios();
            return Falla(impuestos.Error);
        }
        CalculoTributarioDeVenta.AplicarTotales(nota, impuestos.Value.Totales);
        nota.CostTotal = 0m;

        // (9) Los reintegros.
        var reintegros = await ReintegrosAsync(nota, original, entrada.Refunds, usuario, ct);
        if (reintegros.IsFailure)
        {
            db.DescartarCambios();
            return Falla(reintegros.Error);
        }

        await db.SaveChangesAsync(ct);
        if (reintegros.Value.OtroMedio.Count > 0 && auditoria is not null)
            await auditoria.EmitAsync(AuditEventTypes.InventorySalesRefundOtherMeans, "InventoryDocument", nota.PublicId, null,
                new { origin = original.PublicId, means = reintegros.Value.OtroMedio, reason = nota.Reason }, ct);

        return Result.Success(await vista.DetalleAsync(nota, reintegros.Value.Avisos.Select(ReglasDelDocumento.ComoAviso).ToList(), ct));
    }

    /// <summary>
    /// Los reintegros de la nota: los pedidos, o —sin ninguno— los medios de la venta en proporción a lo que acredita la nota (el residuo
    /// del redondeo al mayor). Cada uno referencia el pago de la venta que reintegra; otro medio exige permiso.
    /// </summary>
    private async Task<Result<(IReadOnlyList<Error> Avisos, IReadOnlyList<string> OtroMedio)>> ReintegrosAsync(
        InventoryDocument nota, InventoryDocument original, IReadOnlyList<DocumentPaymentInput> pedidos, int usuario, CancellationToken ct)
    {
        var avisos = new List<Error>();
        var otros = new List<string>();
        var deLaVenta = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == original.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received)
            .OrderBy(p => p.LineNumber).ToListAsync(ct);
        var abiertas = await db.CashSessions.AsNoTracking().Where(s => s.CashierUserId == usuario && s.Status == CashSessionStatus.Open)
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.PublicId, s.PointOfSaleId, s.CashRegisterId }).ToListAsync(ct);

        var entradas = new List<(DocumentPaymentInput Entrada, int? Reintegra)>();
        if (pedidos.Count == 0)
        {
            var base_ = deLaVenta.Sum(p => p.Amount);
            if (base_ > 0m && nota.AmountDue > 0m)
            {
                var partes = deLaVenta.Select(p => Math.Round(p.Amount * nota.AmountDue / base_, 2, MidpointRounding.ToZero)).ToList();
                var residuo = nota.AmountDue - partes.Sum();
                if (residuo != 0m) partes[partes.IndexOf(partes.Max())] += residuo;
                var idsDeLaVenta = deLaVenta.Select(p => p.PaymentMeansId).Distinct().ToList();
                var medios = await db.PaymentMeans.AsNoTracking().Where(m => idsDeLaVenta.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, m => m.PublicId, ct);
                for (var i = 0; i < deLaVenta.Count; i++)
                    if (partes[i] > 0m) entradas.Add((new DocumentPaymentInput(medios[deLaVenta[i].PaymentMeansId], partes[i]), deLaVenta[i].Id));
            }
        }
        else
        {
            var ids = pedidos.Select(p => p.PaymentMeansPublicId).Distinct().ToList();
            var porPublico = await db.PaymentMeans.AsNoTracking().Where(m => ids.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, m => m.Id, ct);
            var puedeOtro = await permisos.HasPermissionAsync(ReglasDeConfirmacionDeVenta.PermisoOtroMedio, ct);
            foreach (var p in pedidos)
            {
                if (!porPublico.TryGetValue(p.PaymentMeansPublicId, out var medioId))
                    return Result.Failure<(IReadOnlyList<Error>, IReadOnlyList<string>)>(RegistroDePagos.ErrorDePago(new ErrorDePago(DisponibilidadDeMedio.MeansNotAvailable, null,
                        new Dictionary<string, object?> { ["paymentMeansCode"] = null })));
                var reintegra = deLaVenta.FirstOrDefault(v => v.PaymentMeansId == medioId);
                if (reintegra is null)
                {
                    var codigo = await db.PaymentMeans.AsNoTracking().Where(m => m.Id == medioId).Select(m => m.Code).FirstAsync(ct);
                    if (!puedeOtro) return Result.Failure<(IReadOnlyList<Error>, IReadOnlyList<string>)>(ErroresDeVentas.RefundMeansNotAllowed(codigo));
                    otros.Add(codigo);
                }
                entradas.Add((p, reintegra?.Id));
            }
        }

        var publicos = entradas.Select(e => e.Entrada.PaymentMeansPublicId).Distinct().ToList();
        var todos = await db.PaymentMeans.AsNoTracking().Include(m => m.CardNetwork).Include(m => m.CardAcquirer)
            .Where(m => publicos.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, ct);
        short numero = 0;
        foreach (var (e, reintegra) in entradas)
        {
            var m = todos[e.PaymentMeansPublicId];
            if (new[] { e.Reference, e.AuthorizationCode, e.BatchNumber, e.Last4 }.Any(ValidadorDePagos.PareceNumeroDeTarjeta))
                return Result.Failure<(IReadOnlyList<Error>, IReadOnlyList<string>)>(RegistroDePagos.ErrorDePago(new ErrorDePago(ValidadorDePagos.CardNumberNotAllowed, numero,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = m.Code })));
            int? sesion = null;
            if (m.CountMethod != CashCountMethod.None)
            {
                var elegida = e.CashSessionPublicId is { } s ? abiertas.FirstOrDefault(a => a.PublicId == s) : abiertas.FirstOrDefault();
                sesion = elegida?.Id;
                if (elegida is null) avisos.Add(RegistroDePagos.ErrorDePago(new ErrorDePago(ValidadorDePagos.CashSessionRequired, numero,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = m.Code })));
                else if (nota.CashSessionId is null)
                {
                    nota.CashSessionId = elegida.Id;
                    nota.CashRegisterId = elegida.CashRegisterId;
                    nota.PointOfSaleId = elegida.PointOfSaleId;
                }
            }
            var pago = new DocumentPayment
            {
                Document = nota,
                DocumentId = nota.Id,
                LineNumber = ++numero,
                Direction = PaymentDirection.Refunded,
                Amount = e.Amount,
                Reference = string.IsNullOrWhiteSpace(e.Reference) ? null : e.Reference.Trim(),
                AuthorizationCode = string.IsNullOrWhiteSpace(e.AuthorizationCode) ? null : e.AuthorizationCode.Trim(),
                Last4 = e.Last4,
                CashSessionId = sesion,
                RefundsPaymentId = reintegra,
            };
            pago.CopiarDelMedio(m);
            pago.ExpectedCommissionAmount = null;
            db.DocumentPayments.Add(pago);
        }
        if (entradas.Sum(e => e.Entrada.Amount) != nota.AmountDue)
            avisos.Add(new ErrorConDatos(ValidadorDePagos.TotalMismatch, "Los reintegros no suman exactamente el valor de la nota.",
                new { amountDue = nota.AmountDue, paid = entradas.Sum(e => e.Entrada.Amount) }));
        return Result.Success<(IReadOnlyList<Error>, IReadOnlyList<string>)>((avisos, otros));
    }

    private static Result<InventoryDocumentDto> Falla(Error error) => Result.Failure<InventoryDocumentDto>(error);
}

// --------------------------------------------------------------------------------------------- las reglas --

/// <summary>
/// Lo que comparten el borrador y la confirmación de una nota de venta (feature 012, I3, T612; §18.3): la clase de la nota según su
/// original, el original de una nota (<c>NoteOf</c>), lo que queda por acreditar de cada línea y los impuestos con la foto del original.
/// Puro salvo las lecturas. (nuevo)
/// </summary>
public static class NotasDeVenta
{
    /// <summary>La clase de la nota de un original; nula si el original no admite nota (§18.3).</summary>
    public static DocumentClass? ClaseDeNota(DocumentClass original) => original switch
    {
        DocumentClass.SalesInvoice or DocumentClass.SalesInvoiceFromShipments => DocumentClass.CreditNote,
        DocumentClass.PosEquivalentDocument => DocumentClass.PosAdjustmentNote,
        DocumentClass.NonElectronicSalesReceipt => DocumentClass.NonElectronicSalesNote,
        _ => null,
    };

    /// <summary>¿Es una de las tres clases de nota de venta?</summary>
    public static bool EsNota(DocumentClass clase) =>
        clase is DocumentClass.CreditNote or DocumentClass.PosAdjustmentNote or DocumentClass.NonElectronicSalesNote;

    /// <summary>El original de la nota (vínculo <c>NoteOf</c> vivo), con sus líneas.</summary>
    public static async Task<InventoryDocument?> OriginalDeAsync(IApplicationDbContext db, InventoryDocument nota, CancellationToken ct)
    {
        var enMemoria = db.DocumentLinks.Local.FirstOrDefault(l => ReferenceEquals(l.TargetDocument, nota) && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted);
        var origenId = enMemoria?.SourceDocumentId
            ?? await db.DocumentLinks.AsNoTracking().Where(l => l.TargetDocumentId == nota.Id && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted)
                .Select(l => (int?)l.SourceDocumentId).FirstOrDefaultAsync(ct);
        return origenId is int id ? await db.InventoryDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct) : null;
    }

    /// <summary>
    /// Lo que queda por acreditar de cada línea viva del original, sin contar la nota <paramref name="excluir"/>: cantidad = original −
    /// devuelto por notas vivas (<c>ReturnOf</c>); valor = neto original − neto acreditado por notas vivas (<c>NoteOf</c>). Vivas = en
    /// borrador, en aprobación o confirmadas y no anuladas ni descartadas.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, ErroresDeVentas.Restante>> RestantesAsync(IApplicationDbContext db, InventoryDocument original, int? excluir,
        CancellationToken ct)
    {
        var vivasDelOriginal = original.Lines.Where(l => !l.IsDeleted).ToList();
        var ids = vivasDelOriginal.Select(l => l.Id).ToList();
        var consumos = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && ids.Contains(x.SourceLineId) && !x.DocumentLink!.IsDeleted
                && (x.DocumentLink.Kind == DocumentLinkKind.NoteOf || x.DocumentLink.Kind == DocumentLinkKind.ReturnOf)
                && x.DocumentLink.TargetDocumentId != (excluir ?? 0)
                && (x.DocumentLink.TargetDocument!.Status == DocumentStatus.Draft || x.DocumentLink.TargetDocument.Status == DocumentStatus.PendingApproval
                    || x.DocumentLink.TargetDocument.Status == DocumentStatus.Confirmed)
                && !x.TargetLine!.IsDeleted)
            .Select(x => new { x.SourceLineId, x.DocumentLink!.Kind, x.QuantityBase, x.TargetLine!.NetAmount })
            .ToListAsync(ct);
        return vivasDelOriginal.ToDictionary(l => l.Id, l => new ErroresDeVentas.Restante(l.PublicId,
            l.QuantityBase - consumos.Where(c => c.SourceLineId == l.Id && c.Kind == DocumentLinkKind.ReturnOf).Sum(c => c.QuantityBase),
            l.NetAmount - consumos.Where(c => c.SourceLineId == l.Id && c.Kind == DocumentLinkKind.NoteOf).Sum(c => c.NetAmount)));
    }

    /// <summary><c>Inventory.CreditNote.ExceedsRemaining</c> si alguna línea de la nota acredita (o devuelve) más de lo que queda; nulo si no.</summary>
    public static async Task<Error?> ExcesoAsync(IApplicationDbContext db, InventoryDocument original, InventoryDocument nota, CancellationToken ct)
    {
        var restantes = await RestantesAsync(db, original, nota.Id, ct);
        var vinculos = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DocumentLink!.TargetDocumentId == nota.Id && x.DocumentLink.Kind == DocumentLinkKind.NoteOf && !x.DocumentLink.IsDeleted)
            .Select(x => new { x.SourceLineId, x.TargetLineId }).ToListAsync(ct);
        foreach (var linea in nota.Lines.Where(l => !l.IsDeleted))
        {
            var origen = vinculos.FirstOrDefault(v => v.TargetLineId == linea.Id)?.SourceLineId;
            if (origen is not int o || !restantes.TryGetValue(o, out var queda)) continue;
            if ((nota.ReturnsGoods && linea.QuantityBase > queda.RemainingQuantity) || linea.NetAmount > queda.RemainingAmount)
                return ErroresDeVentas.ExceedsRemaining(restantes.Values.ToList());
        }
        return null;
    }

    /// <summary>Los impuestos de la nota con la foto del original (sus renglones guardados), en proporción y sin volver a probar la base mínima.</summary>
    public static async Task<Result<(IReadOnlyList<RenglonTributario> Renglones, TotalesDeVenta Totales)>> ImpuestosAsync(IApplicationDbContext db,
        CalculoTributarioDeVenta calculo, InventoryDocument original, InventoryDocument nota, IReadOnlyDictionary<int, int>? lineaOriginal, CancellationToken ct)
    {
        var foto = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == original.Id).ToListAsync(ct);
        var numeros = original.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var renglones = CalculoTributarioDeCompra.DesdeLaFoto(foto, numeros);
        if (lineaOriginal is null)
        {
            var vinculos = await db.DocumentLineLinks.AsNoTracking()
                .Where(x => !x.IsDeleted && x.DocumentLink!.TargetDocumentId == nota.Id && x.DocumentLink.Kind == DocumentLinkKind.NoteOf && !x.DocumentLink.IsDeleted)
                .Select(x => new { x.SourceLineId, x.TargetLineId }).ToListAsync(ct);
            var mapa = new Dictionary<int, int>();
            foreach (var l in nota.Lines.Where(l => !l.IsDeleted))
                if (vinculos.FirstOrDefault(v => v.TargetLineId == l.Id) is { } v && numeros.TryGetValue(v.SourceLineId, out var n)) mapa[l.LineNumber] = n;
            lineaOriginal = mapa;
        }
        return await calculo.CalcularAsync(nota, ct, renglones, lineaOriginal);
    }

    /// <summary>Overload de la confirmación: el mapa de líneas sale de los vínculos guardados.</summary>
    public static Task<Result<(IReadOnlyList<RenglonTributario> Renglones, TotalesDeVenta Totales)>> ImpuestosAsync(IApplicationDbContext db,
        CalculoTributarioDeVenta calculo, InventoryDocument original, InventoryDocument nota, CancellationToken ct) =>
        ImpuestosAsync(db, calculo, original, nota, null, ct);
}
