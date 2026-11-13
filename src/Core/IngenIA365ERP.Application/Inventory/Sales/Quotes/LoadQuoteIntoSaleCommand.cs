using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.Quotes;

/// <summary>Dónde se carga la cotización: la factura (o el comprobante) de oficina o la venta del punto de venta. (nuevo)</summary>
public enum DestinoDeCotizacion
{
    /// <summary>El borrador de <c>/ventas/facturas/nueva</c>: una factura de venta o un comprobante no electrónico de oficina.</summary>
    Invoice = 1,

    /// <summary>El borrador de la venta del POS (ligado a una sesión de caja abierta).</summary>
    Pos = 2,
}

/// <summary>
/// La regla de vigencia de la cotización en la venta (2026-11-12, decisión del dueño: «la cotización sólo es una base»): mientras la venta
/// tenga fecha hasta su «Vigente hasta», respeta sus precios y descuentos; vencida, la venta toma los precios del día. La usan la carga
/// (<see cref="LoadQuoteIntoSaleCommandHandler"/>) y cada guardado del borrador de oficina (<c>BorradorDeVenta</c>). Pura. (nuevo)
/// </summary>
public static class CotizacionEnLaVenta
{
    /// <summary>¿La cotización vence en <paramref name="validaHasta"/> y sigue vigente a <paramref name="fechaDeLaVenta"/>? Sin vigencia, no.</summary>
    public static bool Vigente(DateOnly? validaHasta, DateOnly fechaDeLaVenta) => validaHasta is { } vence && fechaDeLaVenta <= vence;
}

/// <summary>
/// «Cargar cotización» (2026-11-12, decisión del dueño; <c>POST /api/inventory/sales/quotes/{id}/to-invoice</c> y <c>/to-pos</c>): copia una
/// cotización <b>confirmada</b> —cliente, vendedor, notas y líneas con producto, unidad, cantidad, precio y descuentos— a un borrador de venta
/// que cobra: el de la factura de oficina (<see cref="DestinoDeCotizacion.Invoice"/>) o el de la venta del POS (<see cref="DestinoDeCotizacion.Pos"/>).
/// Sin <see cref="DraftPublicId"/> crea el borrador (la factura con <see cref="DocumentTypePublicId"/> y la bodega pedida o la de la cotización;
/// el POS en la sesión <see cref="CashSessionPublicId"/>); con él, agrega las líneas al final. Una cotización se carga cuantas veces se quiera:
/// es una base, no se consume. (nuevo)
/// </summary>
public sealed record LoadQuoteIntoSaleCommand(
    Guid QuotePublicId,
    DestinoDeCotizacion Target,
    Guid? DraftPublicId = null,
    Guid? CashSessionPublicId = null,
    Guid? DocumentTypePublicId = null,
    Guid? WarehousePublicId = null) : IRequest<Result<CotizacionCargadaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// Lo que respondió la carga: el borrador (nuevo o el mismo), su clase, la cotización, si estaba vencida a la fecha de la venta —entonces
/// <see cref="Repriced"/>: las líneas tomaron los precios del día—, cuántas líneas se agregaron, si el cliente de la venta cambió al de la
/// cotización y los avisos para la persona (una línea con serie por elegir, por ejemplo). (nuevo)
/// </summary>
public sealed record CotizacionCargadaDto(
    Guid DraftPublicId,
    DocumentClass DraftClass,
    Guid QuotePublicId,
    string? QuoteNumber,
    DateOnly? ValidUntil,
    bool Expired,
    bool Repriced,
    int LinesAdded,
    bool CustomerChanged,
    IReadOnlyList<string> Warnings);

public sealed class LoadQuoteIntoSaleCommandValidator : AbstractValidator<LoadQuoteIntoSaleCommand>
{
    public LoadQuoteIntoSaleCommandValidator()
    {
        RuleFor(x => x.QuotePublicId).NotEmpty();
        RuleFor(x => x.Target).IsInEnum();
        RuleFor(x => x.DocumentTypePublicId).NotEmpty().When(x => x.Target == DestinoDeCotizacion.Invoice && x.DraftPublicId is null)
            .WithMessage("Indique el tipo de la factura en la que se carga la cotización.");
        RuleFor(x => x.CashSessionPublicId).NotEmpty().When(x => x.Target == DestinoDeCotizacion.Pos && x.DraftPublicId is null)
            .WithMessage("Indique la sesión de caja en la que se abre la venta.");
    }
}

/// <summary>
/// En orden, en una transacción:
/// <list type="number">
/// <item>la cotización existe en el alcance y el grupo de ventas (404), es <c>SalesQuote</c> (<c>Inventory.Document.TypeNotForRoute</c>) y
/// está confirmada (<c>Inventory.Quote.NotConfirmed</c>);</item>
/// <item>el borrador destino: el que se nombra —en borrador (<c>Inventory.Document.NotDraft</c>), una factura o un comprobante de oficina
/// para <c>Invoice</c> o una venta del POS de la sesión del usuario para <c>Pos</c> (<c>Inventory.Quote.SaleClassInvalid</c>)— o uno nuevo
/// por el camino de siempre (<see cref="SaveInventoryDraftCommandHandler"/> para la factura, <see cref="CreatePosDraftCommandHandler"/> para
/// el POS);</item>
/// <item>el cliente: si la venta es del consumidor final toma el de la cotización (y vuelve a resolver las listas de lo que ya tenía); si ya
/// es de otro cliente identificado, <c>Inventory.Quote.CustomerMismatch</c>. Vendedor y notas, si la venta no los tiene;</item>
/// <item>las líneas, al final, cada una con su vínculo de línea al de la cotización (<c>FromOrder</c>, el mismo de cotización → pedido; uno
/// por par cotización–venta). <b>Vigente</b> a la fecha de la venta (<see cref="CotizacionEnLaVenta.Vigente"/>): la lista y el precio de lista
/// de la cotización quedan fijos y su precio digitado y su descuento manual se vuelven a pedir; vencida, se precifican al día y la
/// respuesta lo dice (<c>repriced</c>). Las promociones las vuelve a calcular el motor a la fecha de la venta. Todo lo precifica
/// <see cref="BorradorDelPos.PrecificarAsync"/> sobre el documento —el mismo motor que la factura—, sin tocar los pagos ya registrados;</item>
/// <item>después la venta es un borrador normal: se edita, se cobra y se confirma como siempre.</item>
/// </list>
/// Lo audita el <c>AuditBehavior</c> (es un comando). (nuevo)
/// </summary>
public sealed class LoadQuoteIntoSaleCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista,
    BorradorDelPos pos,
    SaveInventoryDraftCommandHandler guardar)
    : IRequestHandler<LoadQuoteIntoSaleCommand, Result<CotizacionCargadaDto>>
{
    private static readonly DocumentClass[] ClasesDeOficina = [DocumentClass.SalesInvoice, DocumentClass.NonElectronicSalesReceipt];

    public Task<Result<CotizacionCargadaDto>> Handle(LoadQuoteIntoSaleCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CargarAsync(request, ct), ct);

    private async Task<Result<CotizacionCargadaDto>> CargarAsync(LoadQuoteIntoSaleCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null) return Falla(ErroresDelDocumento.SinUsuario());

        var cotizacion = await vista.BuscarAsync(request.QuotePublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (cotizacion is null) return Falla(InventoryErrors.DocumentNotFound());
        if (cotizacion.Class != DocumentClass.SalesQuote) return Falla(InventoryErrors.TypeNotForRoute(cotizacion.Class, DocumentClassGroup.Sales));
        if (cotizacion.Status != DocumentStatus.Confirmed) return Falla(ErroresDelCicloComercial.QuoteNotConfirmed(cotizacion.Status));

        var destino = request.Target == DestinoDeCotizacion.Pos
            ? await VentaDelPosAsync(request, ct)
            : await FacturaAsync(request, cotizacion, ct);
        if (destino.IsFailure) return Falla(destino.Error);
        var venta = destino.Value;

        // El cliente: el de la cotización si la venta es del consumidor final; otro cliente identificado no se pisa.
        var clienteCambio = false;
        if (cotizacion.CounterpartyPersonId is int cliente && !await BorradorDeVenta.EsConsumidorFinalAsync(db, cotizacion, ct)
            && venta.CounterpartyPersonId != cliente)
        {
            if (!await BorradorDeVenta.EsConsumidorFinalAsync(db, venta, ct))
            {
                var nombre = await db.People.AsNoTracking().Where(p => p.Id == venta.CounterpartyPersonId).Select(p => BorradorDelPos.Nombre(p)).FirstOrDefaultAsync(ct);
                return Falla(ErroresDelCicloComercial.QuoteCustomerMismatch(nombre ?? string.Empty));
            }
            venta.CounterpartyPersonId = cliente;
            clienteCambio = true;
            // Lo que la venta ya tenía se vuelve a precificar con las listas del cliente (como al cambiarlo en el POS).
            if (venta.Lines.Any(l => !l.IsDeleted))
            {
                var repreciada = await pos.PrecificarAsync(venta, null, resolverListas: true, null, aplicar: true, ct);
                if (repreciada.IsFailure) return Falla(repreciada.Error);
            }
        }
        if (venta.SalespersonId is null && cotizacion.SalespersonId is int vendedor
            && await db.Salespeople.AsNoTracking().AnyAsync(s => s.Id == vendedor && !s.IsDeleted && !s.Person.IsDeleted, ct))
            venta.SalespersonId = vendedor;
        if (string.IsNullOrWhiteSpace(venta.Notes) && !string.IsNullOrWhiteSpace(cotizacion.Notes)) venta.Notes = cotizacion.Notes;

        // Las líneas, con su vínculo a la cotización.
        var vigente = CotizacionEnLaVenta.Vigente(cotizacion.ValidUntil, venta.OperationDate);
        var lineasDeLaCotizacion = cotizacion.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var ids = lineasDeLaCotizacion.Select(l => l.Id).ToList();
        var descuentos = (await db.DocumentLineDiscounts.AsNoTracking().Where(d => ids.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct))
            .ToLookup(d => d.DocumentLineId);
        var conSerie = (await db.Products.AsNoTracking().Where(p => lineasDeLaCotizacion.Select(l => l.ProductId).Contains(p.Id) && p.TracksSerial)
            .Select(p => new { p.Id, p.Code }).ToListAsync(ct)).ToDictionary(p => p.Id, p => p.Code);
        var ubicacion = venta.WarehouseId is int bodega
            ? await db.WarehouseLocations.AsNoTracking().Where(l => l.WarehouseId == bodega && l.IsDefault).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct)
            : null;

        var vinculo = await db.DocumentLinks.Include(l => l.LineLinks)
            .FirstOrDefaultAsync(l => l.SourceDocumentId == cotizacion.Id && l.TargetDocumentId == venta.Id && l.Kind == DocumentLinkKind.FromOrder && !l.IsDeleted, ct);
        if (vinculo is null)
        {
            vinculo = new DocumentLink { SourceDocumentId = cotizacion.Id, TargetDocument = venta, TargetDocumentId = venta.Id, Kind = DocumentLinkKind.FromOrder };
            db.DocumentLinks.Add(vinculo);
        }

        var avisos = new List<string>();
        var cambios = new Dictionary<int, CambioDeLinea>();
        var siguiente = venta.Lines.Where(l => !l.IsDeleted).Select(l => l.LineNumber).DefaultIfEmpty(0).Max();
        foreach (var origen in lineasDeLaCotizacion)
        {
            var linea = new InventoryDocumentLine
            {
                Document = venta,
                DocumentId = venta.Id,
                LineNumber = ++siguiente,
                ProductId = origen.ProductId,
                UnitId = origen.UnitId,
                Quantity = origen.Quantity,
                Factor = origen.Factor,
                QuantityBase = origen.QuantityBase,
                RoundingQuantity = origen.RoundingQuantity,
                LocationId = ubicacion,
                Description = origen.Description,
            };
            if (vigente && origen.ListPrice is > 0m)
            {
                // La lista y el precio de lista de la cotización quedan fijos (Pedida los toma de la línea); lo digitado y el descuento manual
                // se vuelven a pedir: el motor los mide contra el tope de quien vende ahora, como cualquier descuento de la venta.
                linea.PriceListId = origen.PriceListId;
                linea.ListPrice = origen.ListPrice;
                linea.ListPriceIncludesTaxes = origen.ListPriceIncludesTaxes;
                if (CambioDeLaCotizacion(origen, descuentos[origen.Id].ToList()) is { } cambio) cambios[linea.LineNumber] = cambio;
            }
            venta.Lines.Add(linea);
            vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = origen.Id, TargetLine = linea, QuantityBase = linea.QuantityBase });
            if (conSerie.TryGetValue(origen.ProductId, out var codigo))
                avisos.Add($"La línea {linea.LineNumber} ({codigo}) controla serie: elíjala antes de confirmar la venta.");
        }

        var precificada = await pos.PrecificarAsync(venta, cambios, resolverListas: false, null, aplicar: true, ct);
        if (precificada.IsFailure) return Falla(precificada.Error);
        await db.SaveChangesAsync(ct);
        await pos.EnlazarAprobacionesAsync(venta, ct);
        if (!vigente)
            avisos.Insert(0, $"La cotización venció el {cotizacion.ValidUntil:dd/MM/yyyy}: las líneas quedaron con los precios de hoy.");

        return Result.Success(new CotizacionCargadaDto(venta.PublicId, venta.Class, cotizacion.PublicId,
            VistaDeDocumentos.NumeroVisible(cotizacion.Prefix, cotizacion.Number), cotizacion.ValidUntil, !vigente, !vigente,
            lineasDeLaCotizacion.Count, clienteCambio, avisos));
    }

    /// <summary>
    /// El precio digitado (en la base de la lista) y el descuento manual de la línea de la cotización; nulo si vende al precio de lista sin
    /// descuento. El de una promoción no se pide: el motor lo vuelve a calcular.
    /// </summary>
    public static CambioDeLinea? CambioDeLaCotizacion(InventoryDocumentLine origen, IReadOnlyList<DocumentLineDiscount> filas)
    {
        var manuales = filas.Where(f => !f.FromDocumentDiscount && f.Source == DiscountSource.Manual).ToList();
        decimal? digitado = null;
        if (manuales.FirstOrDefault(f => f.IsPriceOverride) is { } porPrecio && origen.ListPrice is { } lista)
        {
            var divisor = origen.ListPriceIncludesTaxes && origen.UnitPrice > 0m ? lista / origen.UnitPrice : 1m;
            digitado = porPrecio.Rate is { } fraccion
                ? Math.Round(lista * (1m - fraccion), 2, MidpointRounding.AwayFromZero)
                : origen.Quantity > 0m ? Math.Round((origen.UnitPrice - porPrecio.Amount / origen.Quantity) * divisor, 2, MidpointRounding.AwayFromZero) : null;
        }
        var porcentaje = manuales.Where(f => !f.IsPriceOverride).Sum(f => f.Rate ?? 0m);
        if (digitado is null && porcentaje <= 0m) return null;
        return new CambioDeLinea(PrecioDigitado: digitado, Descuento: porcentaje > 0m ? new PosDiscountInput(Percent: porcentaje) : null);
    }

    /// <summary>La factura o el comprobante de oficina en borrador: el nombrado o uno nuevo (tipo pedido, bodega pedida o la de la cotización).</summary>
    private async Task<Result<InventoryDocument>> FacturaAsync(LoadQuoteIntoSaleCommand request, InventoryDocument cotizacion, CancellationToken ct)
    {
        Guid id;
        if (request.DraftPublicId is { } existente)
        {
            id = existente;
        }
        else
        {
            var bodega = request.WarehousePublicId
                ?? (cotizacion.WarehouseId is int w ? await db.Warehouses.AsNoTracking().Where(x => x.Id == w).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null);
            var entrada = new SalesDraftInput(request.DocumentTypePublicId!.Value, bodega ?? Guid.Empty, [], [], reloj.HoyLocal);
            var creada = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, entrada.ComoBorrador(ClasesDeOficina)), ct);
            if (creada.IsFailure) return Result.Failure<InventoryDocument>(creada.Error);
            id = creada.Value.PublicId;
        }

        var venta = await vista.BuscarAsync(id, DocumentClassGroup.Sales, seguir: true, ct);
        if (venta is null || venta.PointOfSaleId is not null) return Result.Failure<InventoryDocument>(InventoryErrors.DocumentNotFound());
        if (venta.Status != DocumentStatus.Draft) return Result.Failure<InventoryDocument>(InventoryErrors.NotDraft(venta.Status));
        if (!ClasesDeOficina.Contains(venta.Class)) return Result.Failure<InventoryDocument>(ErroresDelCicloComercial.QuoteSaleClassInvalid(venta.Class));
        return Result.Success(venta);
    }

    /// <summary>La venta del POS en borrador: la nombrada, en la sesión abierta del usuario, o una nueva en <c>CashSessionPublicId</c>.</summary>
    private async Task<Result<InventoryDocument>> VentaDelPosAsync(LoadQuoteIntoSaleCommand request, CancellationToken ct)
    {
        var id = request.DraftPublicId;
        if (id is null)
        {
            var abierta = await new CreatePosDraftCommandHandler(db, pos).Handle(new CreatePosDraftCommand(request.CashSessionPublicId!.Value), ct);
            if (abierta.IsFailure) return Result.Failure<InventoryDocument>(abierta.Error);
            id = abierta.Value.DraftPublicId;
        }
        var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, id.Value, ct);
        return cargada.IsFailure ? Result.Failure<InventoryDocument>(cargada.Error) : Result.Success(cargada.Value.Venta);
    }

    private static Result<CotizacionCargadaDto> Falla(Error error) => Result.Failure<CotizacionCargadaDto>(error);
}
