using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.Inventory.Sales;

// ------------------------------------------------------------------------------------------------ los DTO --

/// <summary>Un pago en la lista (§18.1). (nuevo)</summary>
public sealed record SalesPaymentSummaryDto(string MeansCode, PaymentMeansClass MeansClass, decimal Amount);

/// <summary><c>SalesDocumentSummaryDto</c> (§18.1). (nuevo)</summary>
public sealed record SalesDocumentSummaryDto(
    Guid DocumentPublicId,
    DocumentClass Class,
    string DocumentTypeCode,
    string Prefix,
    long? Number,
    DocumentStatus Status,
    DateOnly OperationDate,
    string? PointOfSaleCode,
    string? CashRegisterCode,
    string CounterpartyName,
    string? SalespersonName,
    decimal Total,
    decimal AmountDue,
    IReadOnlyList<SalesPaymentSummaryDto> Payments,
    PostingMode? PostingMode,
    string? ElectronicStatus,
    bool PendingDelivery,
    bool PendingValidation);

/// <summary>La contraparte: la copia fiscal vigente en un confirmado, el maestro en un borrador (§18.1). (nuevo)</summary>
public sealed record SalesCounterpartyDto(Guid PersonPublicId, bool IsFinalConsumer, string Name, string? IdType, string? IdNumber, string? Address, string? Email);

/// <summary>La aprobación de un descuento (§18.1). (nuevo)</summary>
public sealed record SalesDiscountApprovalDto(Guid ApprovalRequestPublicId, string Status);

/// <summary>
/// Un descuento de la línea (§18.1; nuevo). I6 (T875): con <c>source = Promotion</c>, <see cref="PromotionPublicId"/> y el nombre de la
/// promoción (FR-055 «el documento muestra cuál se aplicó»).
/// </summary>
public sealed record SalesLineDiscountDto(DiscountSource Source, decimal? Percent, decimal Amount, bool FromDocumentDiscount, bool IsPriceOverride,
    SalesDiscountApprovalDto? Approval, Guid? PromotionPublicId = null, string? PromotionName = null);

/// <summary>Un impuesto de la línea o una retención del documento (§18.1). (nuevo)</summary>
public sealed record SalesTaxDto(string TaxRateCode, TaxKind Kind, decimal? Rate, decimal? AmountPerUnit, decimal Base, decimal Amount, TaxTreatment Treatment);

/// <summary>Una línea de la venta (§18.1). <see cref="BelowCost"/> no expone el costo. (nuevo)</summary>
public sealed record SalesLineDto(
    Guid LinePublicId,
    int LineNumber,
    ReferenciaDto Product,
    UnidadDto Unit,
    decimal Factor,
    decimal Quantity,
    decimal QuantityBase,
    decimal RoundingQuantity,
    decimal ListPrice,
    decimal UnitPrice,
    bool IncludesTaxes,
    ReferenciaDto? PriceList,
    IReadOnlyList<SalesLineDiscountDto> Discounts,
    IReadOnlyList<SalesTaxDto> Taxes,
    decimal Subtotal,
    decimal Total,
    bool BelowCost);

/// <summary>Los totales (§18.1, T26). (nuevo)</summary>
public sealed record SalesTotalsDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

/// <summary><c>DocumentPaymentDto</c> (§22.4): lo pedido más lo que el servidor deja. Nunca el número de la tarjeta. (nuevo)</summary>
public sealed record DocumentPaymentDto(
    Guid DocumentPaymentPublicId,
    int LineNumber,
    PaymentDirection Direction,
    decimal Amount,
    decimal? Tendered,
    decimal Change,
    string MeansCode,
    PaymentMeansClass MeansClass,
    string? NetworkCode,
    string? AcquirerCode,
    Guid? BankPublicId,
    string? Reference,
    string? AuthorizationCode,
    string? Last4,
    string? BatchNumber,
    Guid? CashSessionPublicId,
    bool PendingValidation,
    CreditOrigin? CreditOrigin,
    VoucherRedemptionStatus? VoucherRedemptionStatus);

/// <summary>Un mensaje del documento, para ver en qué va sin ir a la bandeja (§18.1). (nuevo)</summary>
public sealed record SalesMessageDto(Guid MessagePublicId, string Type, string Destination, string DeliveryStatus, long? BatchNumber);

/// <summary>Los vínculos (§18.1). (nuevo)</summary>
public sealed record SalesLinksDto(
    DocumentoReferidoDto? Origin,
    DocumentoReferidoDto? Voids,
    DocumentoReferidoDto? VoidedBy,
    IReadOnlyList<DocumentoReferidoDto> Notes,
    DocumentoReferidoDto? ReplacementOf,
    DocumentoReferidoDto? ReplacedBy);

/// <summary>Lo que impediría confirmar un borrador (§18.1). (nuevo)</summary>
public sealed record SalesIssueDto(string Code, string Message, int? LineNumber);

/// <summary><c>SalesDocumentDto</c> (contracts/api.md §18.1). <c>electronic</c> lo llena I4. (nuevo)</summary>
public sealed record SalesDocumentDto(
    Guid DocumentPublicId,
    DocumentClass Class,
    ReferenciaDto DocumentType,
    string Prefix,
    long? Number,
    DocumentStatus Status,
    DateOnly OperationDate,
    DateTime? ConfirmedAt,
    ReferenciaDto? Warehouse,
    ReferenciaDto Branch,
    ReferenciaDto? PointOfSale,
    ReferenciaDto? CashRegister,
    Guid? CashSessionPublicId,
    ReferenciaDto? SalesChannel,
    PostingMode? PostingMode,
    SalesCounterpartyDto? Counterparty,
    ReferenciaDto? Salesperson,
    IReadOnlyList<SalesLineDto> Lines,
    IReadOnlyList<SalesTaxDto> Withholdings,
    SalesTotalsDto Totals,
    IReadOnlyList<DocumentPaymentDto> Payments,
    IReadOnlyList<SalesMessageDto> Messages,
    object? Electronic,
    SalesLinksDto Links,
    UsuarioDto CreatedBy,
    UsuarioDto? ConfirmedBy,
    IReadOnlyList<SalesIssueDto> Issues,
    byte[] RowVersion);

// ---------------------------------------------------------------------------------------------- consultas --

/// <summary>
/// La lista de documentos de venta (<c>GET /api/inventory/sales/documents</c>, §18.1; feature 012, I3, T613) con los filtros de la
/// sección; <see cref="PendingDelivery"/> con <see cref="CashSession"/> es la lista «pendientes de entrega» de la caja (§20.3) y
/// <see cref="PendingValidation"/> las que tienen un pago de crédito provisional. Se filtra por alcance: bodega del documento o punto
/// de venta (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record ListSalesDocumentsQuery(
    DocumentClass? Class = null,
    Guid? DocumentType = null,
    DocumentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? PointOfSale = null,
    Guid? CashRegister = null,
    Guid? CashSession = null,
    Guid? Person = null,
    Guid? Salesperson = null,
    long? Number = null,
    string? ElectronicStatus = null,
    bool? PendingDelivery = null,
    bool? PendingValidation = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SalesDocumentSummaryDto>>>;

public sealed class ListSalesDocumentsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListSalesDocumentsQuery, Result<PagedResult<SalesDocumentSummaryDto>>>
{
    public async Task<Result<PagedResult<SalesDocumentSummaryDto>>> Handle(ListSalesDocumentsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ventas = ClasesDeDocumento.DelGrupo(DocumentClassGroup.Sales);
        var consulta = db.InventoryDocuments.AsNoTracking()
            .Where(d => ventas.Contains(d.Class) || (d.Class == DocumentClass.Voiding && db.InventoryDocuments.Any(o => o.Id == d.VoidsDocumentId && ventas.Contains(o.Class))))
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());

        if (request.Class is { } clase) consulta = consulta.Where(d => d.Class == clase);
        if (request.DocumentType is { } tipo) consulta = consulta.Where(d => d.DocumentType!.PublicId == tipo);
        if (request.Status is { } estado) consulta = consulta.Where(d => d.Status == estado);
        if (request.From is { } desde) consulta = consulta.Where(d => d.OperationDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(d => d.OperationDate <= hasta);
        if (request.PointOfSale is { } punto) consulta = consulta.Where(d => db.PointsOfSale.Any(p => p.Id == d.PointOfSaleId && p.PublicId == punto));
        if (request.CashRegister is { } caja) consulta = consulta.Where(d => db.CashRegisters.Any(c => c.Id == d.CashRegisterId && c.PublicId == caja));
        if (request.CashSession is { } sesion) consulta = consulta.Where(d => db.CashSessions.Any(s => s.Id == d.CashSessionId && s.PublicId == sesion));
        if (request.Person is { } persona) consulta = consulta.Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == persona));
        if (request.Salesperson is { } vendedor) consulta = consulta.Where(d => db.Salespeople.IgnoreQueryFilters().Any(s => s.Id == d.SalespersonId && s.PublicId == vendedor));
        if (request.Number is { } numero) consulta = consulta.Where(d => d.Number == numero);
        if (request.PendingValidation is { } pendiente)
            consulta = consulta.Where(d => db.DocumentPayments.Any(p => p.DocumentId == d.Id && !p.IsDeleted && p.PendingValidation) == pendiente);
        // I4 (T743): el estado electrónico por la fuente (SourceModule = INV); un estado que no existe no devuelve nada.
        if (!string.IsNullOrWhiteSpace(request.ElectronicStatus))
        {
            if (Enum.TryParse<ElectronicDocumentStatus>(request.ElectronicStatus, ignoreCase: true, out var estadoElectronico))
                consulta = consulta.Where(d => db.ElectronicDocuments.Any(e => e.SourceModule == FuenteDeEmisionDeInventario.Modulo
                    && e.SourceDocumentPublicId == d.PublicId && e.Status == estadoElectronico));
            else
                consulta = consulta.Where(_ => false);
        }

        var pagina = request.Page < 1 ? 1 : request.Page;
        var tamano = request.PageSize is < 1 or > PageRequest.MaxPageSize ? 20 : request.PageSize;
        List<InventoryDocument> filas;
        long total;
        HashSet<int> entregados;
        if (request.PendingDelivery is { } porEntregar)
        {
            // La primera entrega queda en la auditoría (§20.3): se filtra en memoria sobre los confirmados del POS.
            var candidatos = await consulta.Where(d => d.Status == DocumentStatus.Confirmed && d.PointOfSaleId != null)
                .OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Id).Take(2_000).ToListAsync(ct);
            entregados = await EntregadosAsync(candidatos, ct);
            var filtrados = candidatos.Where(d => entregados.Contains(d.Id) != porEntregar).ToList();
            total = filtrados.Count;
            filas = filtrados.Skip((pagina - 1) * tamano).Take(tamano).ToList();
        }
        else
        {
            total = await consulta.LongCountAsync(ct);
            filas = await consulta.OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
            entregados = await EntregadosAsync(filas, ct);
        }

        var ids = filas.Select(d => d.Id).ToList();
        var tipoIds = filas.Select(d => d.DocumentTypeId).Distinct().ToList();
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => tipoIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var pagos = (await db.DocumentPayments.AsNoTracking().Where(p => ids.Contains(p.DocumentId) && !p.IsDeleted).ToListAsync(ct)).ToLookup(p => p.DocumentId);
        var puntoIds = filas.Select(d => d.PointOfSaleId).OfType<int>().Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().Where(p => puntoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var cajaIds = filas.Select(d => d.CashRegisterId).OfType<int>().Distinct().ToList();
        var cajas = await db.CashRegisters.AsNoTracking().Where(c => cajaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var personaIds = filas.Select(d => d.CounterpartyPersonId).OfType<int>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => personaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, BorradorDelPos.Nombre, ct);
        var vendedorIds = filas.Select(d => d.SalespersonId).OfType<int>().Distinct().ToList();
        var vendedores = await db.Salespeople.IgnoreQueryFilters().AsNoTracking().Where(s => vendedorIds.Contains(s.Id)).Include(s => s.Person)
            .ToDictionaryAsync(s => s.Id, s => BorradorDelPos.Nombre(s.Person), ct);

        var publicos = filas.Select(d => d.PublicId).ToList();
        var estados = await db.ElectronicDocuments.AsNoTracking()
            .Where(e => e.SourceModule == FuenteDeEmisionDeInventario.Modulo && publicos.Contains(e.SourceDocumentPublicId))
            .Select(e => new { e.SourceDocumentPublicId, e.Status }).ToListAsync(ct);
        var estadoDe = estados.GroupBy(e => e.SourceDocumentPublicId).ToDictionary(g => g.Key, g => g.First().Status.ToString());

        var items = filas.Select(d => new SalesDocumentSummaryDto(
            d.PublicId, d.Class, tipos.GetValueOrDefault(d.DocumentTypeId) ?? string.Empty, d.Prefix, d.Number, d.Status, d.OperationDate,
            d.PointOfSaleId is int p ? puntos.GetValueOrDefault(p) : null,
            d.CashRegisterId is int c ? cajas.GetValueOrDefault(c) : null,
            d.CounterpartyPersonId is int per ? personas.GetValueOrDefault(per) ?? string.Empty : "Consumidor final",
            d.SalespersonId is int v ? vendedores.GetValueOrDefault(v) : null,
            d.Total, d.AmountDue,
            pagos[d.Id].Where(x => x.Direction == PaymentDirection.Received || d.Class != DocumentClass.Voiding).OrderBy(x => x.LineNumber)
                .Select(x => new SalesPaymentSummaryDto(x.MeansCode, x.MeansClass, x.Amount)).ToList(),
            d.PostingMode, estadoDe.GetValueOrDefault(d.PublicId),
            d.Status == DocumentStatus.Confirmed && d.PointOfSaleId is not null && !entregados.Contains(d.Id),
            pagos[d.Id].Any(x => x.PendingValidation))).ToList();
        return Result.Success(new PagedResult<SalesDocumentSummaryDto>(items, pagina, tamano, total));
    }

    /// <summary>
    /// Los documentos que ya tuvieron su primera entrega. Un documento electrónico (I4, T743) se entrega cuando su documento electrónico lo
    /// anota (<c>DeliveredAt</c>: el cobro que validó en línea o la primera entrega después); uno rechazado o cancelado no se entrega nunca y
    /// no queda pendiente. Los demás, por el evento <c>Inventory.Document.Delivered</c> de la auditoría.
    /// </summary>
    private async Task<HashSet<int>> EntregadosAsync(IReadOnlyList<InventoryDocument> documentos, CancellationToken ct)
    {
        if (documentos.Count == 0) return [];
        var publicos = documentos.Select(d => d.PublicId).ToList();
        var electronicos = await db.ElectronicDocuments.AsNoTracking()
            .Where(e => e.SourceModule == FuenteDeEmisionDeInventario.Modulo && publicos.Contains(e.SourceDocumentPublicId))
            .Select(e => new { e.SourceDocumentPublicId, e.DeliveredAt, e.Status }).ToListAsync(ct);
        var porElectronico = electronicos.GroupBy(e => e.SourceDocumentPublicId).ToDictionary(g => g.Key, g => g.First());
        var resueltos = documentos.Where(d => porElectronico.TryGetValue(d.PublicId, out var e)
                && (e.DeliveredAt is not null || e.Status is ElectronicDocumentStatus.Rejected or ElectronicDocumentStatus.CancelledWithoutReplacement))
            .Select(d => d.Id).ToHashSet();
        documentos = documentos.Where(d => !porElectronico.ContainsKey(d.PublicId)).ToList();
        if (documentos.Count == 0) return resueltos;
        var cargas = await db.AuditOutbox.AsNoTracking()
            .Where(e => e.Module == ModuloDeAuditoria.Inventory && e.PayloadJson != null && e.PayloadJson.Contains(AuditEventTypes.InventoryDocumentDelivered))
            .Select(e => e.PayloadJson!).ToListAsync(ct);
        var entregados = cargas.Select(AuditoriaEncadenada.LeerCarga).Where(e => e.Action == AuditEventTypes.InventoryDocumentDelivered)
            .Select(e => e.EntityPublicId).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        resueltos.UnionWith(documentos.Where(d => entregados.Contains(d.PublicId.ToString())).Select(d => d.Id));
        return resueltos;
    }
}

/// <summary>
/// El documento de venta (<c>GET /api/inventory/sales/documents/{id}</c>, §18.1; feature 012, I3, T613): cabecera, contraparte (la
/// copia fiscal vigente si está confirmado), vendedor, líneas con descuentos y su aprobación, impuestos, retenciones, pagos
/// (<see cref="DocumentPaymentDto"/>), el estado de cada mensaje leído de <c>COR_IntegrationMessageDeliveries</c>, vínculos e
/// <c>issues</c>. <c>belowCost</c> no expone el costo: el costo exige <c>Inventory.Costs.Read</c> y aquí no viaja. Fuera del alcance
/// (<see cref="IAlcanceDeInventario"/>) o de otro grupo, el mismo 404. (nuevo)
/// </summary>
public sealed record GetSalesDocumentQuery(Guid DocumentPublicId) : IRequest<Result<SalesDocumentDto>>;

public sealed class GetSalesDocumentQueryHandler(IApplicationDbContext db, VistaDeDocumentos vista, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<GetSalesDocumentQuery, Result<SalesDocumentDto>>
{
    public async Task<Result<SalesDocumentDto>> Handle(GetSalesDocumentQuery request, CancellationToken ct)
    {
        _ = alcanceDeLaPeticion;
        var d = await vista.BuscarAsync(request.DocumentPublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (d is null) return Result.Failure<SalesDocumentDto>(InventoryErrors.DocumentNotFound());

        var tipo = d.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == d.DocumentTypeId, ct);
        var bodega = d.WarehouseId is int w ? await db.Warehouses.AsNoTracking().Where(x => x.Id == w).Select(x => new ReferenciaDto(x.PublicId, x.Code, x.Name)).FirstOrDefaultAsync(ct) : null;
        var sucursal = await db.Branches.AsNoTracking().Where(b => b.Id == d.BranchId).Select(b => new ReferenciaDto(b.PublicId, b.LegacyCode ?? string.Empty, b.Name)).FirstOrDefaultAsync(ct)
            ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty);
        var punto = d.PointOfSaleId is int p ? await db.PointsOfSale.AsNoTracking().Where(x => x.Id == p).Select(x => new ReferenciaDto(x.PublicId, x.Code, x.Name)).FirstOrDefaultAsync(ct) : null;
        var caja = d.CashRegisterId is int c ? await db.CashRegisters.AsNoTracking().Where(x => x.Id == c).Select(x => new ReferenciaDto(x.PublicId, x.Code, x.Name)).FirstOrDefaultAsync(ct) : null;
        Guid? sesion = d.CashSessionId is int s ? await db.CashSessions.AsNoTracking().Where(x => x.Id == s).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        var canal = d.SalesChannelId is int sc ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == sc).Select(x => new ReferenciaDto(x.PublicId, x.Code, x.Name)).FirstOrDefaultAsync(ct) : null;
        var vendedor = d.SalespersonId is int v
            ? await db.Salespeople.IgnoreQueryFilters().AsNoTracking().Include(x => x.Person).Where(x => x.Id == v).FirstOrDefaultAsync(ct)
            : null;

        var contraparte = await ContraparteAsync(d, ct);
        var lineas = await LineasAsync(d, ct);
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == d.Id).ToListAsync(ct);
        var retenciones = impuestos.Where(t => t.Treatment is TaxTreatment.WithholdingSuffered or TaxTreatment.WithholdingApplied)
            .Select(t => new SalesTaxDto(t.TaxRateCode, t.Kind, t.Rate, t.AmountPerUnit, t.Base, t.Amount, t.Treatment)).ToList();
        var pagos = await PagosAsync(d, ct);
        var mensajes = (await vista.MensajesAsync(d.PublicId, ct))
            .Select(m => new SalesMessageDto(m.MessagePublicId, m.Type, m.Destination, m.DeliveryStatus.ToString(), null)).ToList();
        var creador = await Usuario(d.CreatedByUserId, ct);
        var confirmador = d.ConfirmedByUserId is int cu ? await Usuario(cu, ct) : null;

        return Result.Success(new SalesDocumentDto(
            d.PublicId, d.Class, new ReferenciaDto(tipo.PublicId, tipo.Code, tipo.Name), d.Prefix, d.Number, d.Status, d.OperationDate, d.ConfirmedAt,
            bodega, sucursal, punto, caja, sesion, canal, d.PostingMode, contraparte,
            vendedor is null ? null : new ReferenciaDto(vendedor.PublicId, string.Empty, BorradorDelPos.Nombre(vendedor.Person)),
            lineas, retenciones, new SalesTotalsDto(d.Subtotal, d.DiscountTotal, d.TaxTotal, d.WithholdingTotal, d.Total, d.AmountDue),
            pagos, mensajes,
            await EstadoElectronicoDeInventario.DeAsync(db, d.PublicId, ct) is { } electronico ? Pos.EsperaEnLineaDelPos.Bloque(electronico, 0, null) : null,
            await VinculosAsync(d, ct), creador, confirmador, await IssuesAsync(d, vendedor, pagos, ct), d.RowVersion ?? []));
    }

    private async Task<SalesCounterpartyDto?> ContraparteAsync(InventoryDocument d, CancellationToken ct)
    {
        if (d.CounterpartyPersonId is not int personaId) return null;
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct);
        if (persona is null) return null;
        var final = await BorradorDeVenta.EsConsumidorFinalAsync(db, d, ct);
        if (d.Status is DocumentStatus.Confirmed or DocumentStatus.Voided
            && await db.DocumentPartySnapshots.AsNoTracking().Where(x => x.DocumentId == d.Id).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) is { } foto)
            return new SalesCounterpartyDto(persona.PublicId, final, foto.LegalName, foto.DianIdTypeCode, foto.TaxId, foto.Address, foto.Email);
        return new SalesCounterpartyDto(persona.PublicId, final, BorradorDelPos.Nombre(persona), persona.IdType, persona.TaxId, persona.Address, persona.Email);
    }

    private async Task<IReadOnlyList<SalesLineDto>> LineasAsync(InventoryDocument d, CancellationToken ct)
    {
        var vivas = d.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var unidadIds = vivas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new UnidadDto(u.PublicId, u.Code), ct);
        var listaIds = vivas.Select(l => l.PriceListId).OfType<int>().Distinct().ToList();
        var listas = await db.PriceLists.AsNoTracking().Where(p => listaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var lineaIds = vivas.Select(l => l.Id).ToList();
        var descuentos = await db.DocumentLineDiscounts.AsNoTracking().Where(x => lineaIds.Contains(x.DocumentLineId) && !x.IsDeleted).ToListAsync(ct);
        var fuentes = descuentos.Where(x => x.RequiresApproval).Select(x => x.PublicId).ToList();
        var promocionIds = descuentos.Select(x => x.PromotionId).OfType<int>().Distinct().ToList();
        var promociones = await db.Promotions.AsNoTracking().Where(p => promocionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.PublicId, p.Name), ct);
        var solicitudes = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentLineDiscount && fuentes.Contains(r.SourcePublicId) && r.Status != ApprovalRequestStatus.Cancelled)
            .Select(r => new { r.SourcePublicId, r.PublicId, r.Status, r.Id }).ToListAsync(ct);
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == d.Id && t.DocumentLineId != null).ToListAsync(ct);

        // Bajo costo: confirmada, contra el costo con que salió; en borrador, contra el promedio vigente. Nunca se expone el costo.
        var costos = await db.CostStates.AsNoTracking().Where(x => productoIds.Contains(x.ProductId) && x.ScopeWarehouseId == 0)
            .ToDictionaryAsync(x => x.ProductId, x => x.AverageCost, ct);
        return vivas.Select(l =>
        {
            var costo = l.UnitCost ?? costos.GetValueOrDefault(l.ProductId);
            var bajo = costo > 0m && l.QuantityBase > 0m && l.NetAmount / l.QuantityBase < costo;
            var deLinea = impuestos.Where(t => t.DocumentLineId == l.Id).Select(t => new SalesTaxDto(t.TaxRateCode, t.Kind, t.Rate, t.AmountPerUnit, t.Base, t.Amount, t.Treatment)).ToList();
            return new SalesLineDto(l.PublicId, l.LineNumber, productos[l.ProductId], unidades[l.UnitId], l.Factor, l.Quantity, l.QuantityBase, l.RoundingQuantity,
                l.ListPrice ?? l.UnitPrice, l.UnitPrice, l.ListPriceIncludesTaxes, l.PriceListId is int li ? listas.GetValueOrDefault(li) : null,
                descuentos.Where(x => x.DocumentLineId == l.Id).OrderBy(x => x.Sequence).Select(x =>
                {
                    var solicitud = solicitudes.Where(r => r.SourcePublicId == x.PublicId).OrderByDescending(r => r.Id).FirstOrDefault();
                    var promocion = x.PromotionId is int pid ? promociones.GetValueOrDefault(pid) : default;
                    return new SalesLineDiscountDto(x.Source, x.Rate, x.Amount, x.FromDocumentDiscount, x.IsPriceOverride,
                        solicitud is null ? null : new SalesDiscountApprovalDto(solicitud.PublicId, solicitud.Status.ToString()),
                        promocion.PublicId == Guid.Empty ? null : promocion.PublicId, promocion.Name);
                }).ToList(),
                deLinea, l.NetAmount, l.NetAmount + deLinea.Where(t => t.Treatment == TaxTreatment.Generated).Sum(t => t.Amount), bajo);
        }).ToList();
    }

    private async Task<IReadOnlyList<DocumentPaymentDto>> PagosAsync(InventoryDocument d, CancellationToken ct)
    {
        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == d.Id && !p.IsDeleted).OrderBy(p => p.LineNumber).ToListAsync(ct);
        var ids = pagos.Select(p => p.Id).ToList();
        var bonos = (await db.VoucherRedemptions.AsNoTracking().Where(v => ids.Contains(v.DocumentPaymentId)).ToListAsync(ct))
            .GroupBy(v => v.DocumentPaymentId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Id).First().Status);
        var sesionIds = pagos.Select(p => p.CashSessionId).OfType<int>().Distinct().ToList();
        var sesiones = await db.CashSessions.AsNoTracking().Where(s => sesionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.PublicId, ct);
        var bancoIds = pagos.Select(p => p.BankId).OfType<int>().Distinct().ToList();
        var bancos = await db.Banks.AsNoTracking().Where(b => bancoIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        return pagos.Select(p => new DocumentPaymentDto(p.PublicId, p.LineNumber, p.Direction, p.Amount, p.AmountTendered, p.ChangeGiven ?? 0m, p.MeansCode,
            p.MeansClass, p.CardNetworkCode, p.CardAcquirerCode, p.BankId is int b ? bancos.GetValueOrDefault(b) : null, p.Reference, p.AuthorizationCode,
            p.Last4, p.TerminalBatchNumber, p.CashSessionId is int s ? sesiones.GetValueOrDefault(s) : null, p.PendingValidation, p.CreditOrigin,
            bonos.TryGetValue(p.Id, out var estado) ? estado : null)).ToList();
    }

    private async Task<SalesLinksDto> VinculosAsync(InventoryDocument d, CancellationToken ct)
    {
        var vinculos = await db.DocumentLinks.AsNoTracking().Where(l => !l.IsDeleted && (l.SourceDocumentId == d.Id || l.TargetDocumentId == d.Id))
            .Select(l => new { l.SourceDocumentId, l.TargetDocumentId, l.Kind }).ToListAsync(ct);
        var ids = vinculos.SelectMany(l => new[] { l.SourceDocumentId, l.TargetDocumentId }).Append(d.VoidsDocumentId ?? 0).Append(d.VoidedByDocumentId ?? 0)
            .Where(i => i != 0 && i != d.Id).Distinct().ToList();
        var otros = await db.InventoryDocuments.AsNoTracking().Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new DocumentoReferidoDto(o.PublicId, VistaDeDocumentos.NumeroVisible(o.Prefix, o.Number)), ct);
        DocumentoReferidoDto? De(int? id) => id is int i && otros.TryGetValue(i, out var r) ? r : null;
        return new SalesLinksDto(
            De(vinculos.FirstOrDefault(l => l.TargetDocumentId == d.Id && l.Kind is DocumentLinkKind.NoteOf or DocumentLinkKind.FromShipment or DocumentLinkKind.FromOrder)?.SourceDocumentId),
            De(d.VoidsDocumentId),
            De(d.VoidedByDocumentId),
            vinculos.Where(l => l.SourceDocumentId == d.Id && l.Kind == DocumentLinkKind.NoteOf).Select(l => De(l.TargetDocumentId)).OfType<DocumentoReferidoDto>().Distinct().ToList(),
            De(vinculos.FirstOrDefault(l => l.TargetDocumentId == d.Id && l.Kind == DocumentLinkKind.ReplacementOf)?.SourceDocumentId),
            De(vinculos.FirstOrDefault(l => l.SourceDocumentId == d.Id && l.Kind == DocumentLinkKind.ReplacementOf)?.TargetDocumentId));
    }

    /// <summary>Lo que hoy impediría confirmar un borrador: el vendedor, los pagos (sin su sesión) y lo común del tipo.</summary>
    private async Task<IReadOnlyList<SalesIssueDto>> IssuesAsync(InventoryDocument d, Domain.Entities.Inventory.Salesperson? vendedor,
        IReadOnlyList<DocumentPaymentDto> pagos, CancellationToken ct)
    {
        if (d.Status != DocumentStatus.Draft) return [];
        var issues = new List<SalesIssueDto>();
        if (vendedor is { } v && (v.IsDeleted || v.Person.IsDeleted))
            issues.Add(new SalesIssueDto(ErroresDelPos.SalespersonInvalidCode, ErroresDelPos.SalespersonInvalid().Message, null));
        if (d.Lines.All(l => l.IsDeleted)) issues.Add(new SalesIssueDto(InventoryErrors.Empty().Code, InventoryErrors.Empty().Message, null));
        var recibidos = pagos.Where(p => p.Direction == (NotasDeVenta.EsNota(d.Class) ? PaymentDirection.Refunded : PaymentDirection.Received)).Sum(p => p.Amount);
        if (recibidos != d.AmountDue)
            issues.Add(new SalesIssueDto(ValidadorDePagos.TotalMismatch, "Los pagos no suman exactamente el valor a pagar.", null));
        await Task.CompletedTask;
        return issues;
    }

    private async Task<UsuarioDto> Usuario(int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => new UsuarioDto(u.PublicId, u.Username)).FirstOrDefaultAsync(ct)
        ?? new UsuarioDto(null, string.Empty);
}

// ------------------------------------------------------------------------------------------ el crédito (US6) --

/// <summary>La aprobación del crédito de un pago (§23.2). (nuevo)</summary>
public sealed record CreditPaymentApprovalDto(Guid ApprovalRequestPublicId, string Status, string? ApprovedByName, int? Level, DateTime? DecidedAt);

/// <summary>Un pago de crédito con sus condiciones, su aprobación y su sello (§23.2). (nuevo)</summary>
public sealed record CreditPaymentDto(
    Guid DocumentPaymentPublicId,
    string MeansCode,
    PaymentMeansClass MeansClass,
    decimal Amount,
    short? Installments,
    short? TermDays,
    short? PeriodicityDays,
    DateOnly? FirstDueDate,
    DateOnly? FinalDueDate,
    string? SuggestedLineCode,
    bool PendingValidation,
    CreditOrigin? CreditOrigin,
    CreditPaymentApprovalDto? Approval);

/// <summary><c>validation</c> de un mensaje a Cartera: <c>Pending</c>, <c>Validated</c> o <c>Failed</c> (§23.2). (nuevo)</summary>
public sealed record LendingValidationDto(string Status, DateTime? EvaluatedAt, string? Reason);

/// <summary>Un mensaje a Cartera de la venta (el suyo y los ajustes de sus notas y anulación) con su entrega (§23.2). (nuevo)</summary>
public sealed record LendingMessageDto(Guid MessagePublicId, string Type, string DeliveryStatus, bool DestinationAvailable, LendingValidationDto Validation);

/// <summary>La pestaña «Crédito» de una venta (§23.2). Nunca trae saldos de Cartera (FR-062). (nuevo)</summary>
public sealed record SalesDocumentCreditDto(
    IReadOnlyList<CreditPaymentDto> Payments,
    string? AccountsReceivableRecordedBy,
    IReadOnlyList<LendingMessageDto> LendingMessages);

/// <summary>
/// El crédito de una venta (<c>GET /api/inventory/sales/documents/{id}/credit</c>; feature 012, I3, T657; contracts/api.md §23.2;
/// FR-062) (nuevo nombre): sus pagos de crédito con condiciones, aprobación y sello, y sus mensajes a Cartera —la
/// <c>VentaACreditoRegistrada</c> de cada pago y los <c>AjusteDeVentaACredito</c> de sus notas y anulación— con el estado de la entrega
/// (<c>COR_IntegrationMessageDeliveries</c>, destino <c>Lending</c>). Mientras IC esté pendiente, <c>destinationAvailable = false</c> y la
/// validación dice «Pendiente: el destino aún no está disponible (IC)» (<c>IConsultasDeCartera.EstadoDeValidacionAsync</c>).
/// </summary>
public sealed record GetSalesDocumentCreditQuery(Guid DocumentPublicId) : IRequest<Result<SalesDocumentCreditDto>>;

public sealed class GetSalesDocumentCreditQueryHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IngenIA365ERP.Application.Common.Integration.Lending.IConsultasDeCartera cartera,
    IEnumerable<IngenIA365ERP.Application.Common.Integration.IDestinoDeMensajes> destinos)
    : IRequestHandler<GetSalesDocumentCreditQuery, Result<SalesDocumentCreditDto>>
{
    public async Task<Result<SalesDocumentCreditDto>> Handle(GetSalesDocumentCreditQuery request, CancellationToken ct)
    {
        // El alcance lo aplica la vista del documento (bodega y punto de quien consulta).
        _ = alcanceDeLaPeticion;
        var d = await vista.BuscarAsync(request.DocumentPublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (d is null) return Result.Failure<SalesDocumentCreditDto>(InventoryErrors.DocumentNotFound());

        var pagos = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == d.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received
                && (p.MeansClass == PaymentMeansClass.AssociateCredit || p.MeansClass == PaymentMeansClass.CustomerCredit))
            .OrderBy(p => p.LineNumber).ToListAsync(ct);
        var fuentes = pagos.Select(p => p.PublicId).ToList();
        var solicitudes = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentPayment && fuentes.Contains(r.SourcePublicId) && r.Status != ApprovalRequestStatus.Cancelled)
            .ToListAsync(ct);

        var tipos = new[] { IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.VentaACreditoRegistradaV1.Type, IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.AjusteDeVentaACreditoV1.Type };
        var mensajes = await (from m in db.IntegrationMessages.AsNoTracking()
                              join e in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals e.MessageId
                              where tipos.Contains(m.Type) && e.Destination == IntegrationDestinations.Lending
                                    && (m.OriginPublicId == d.PublicId || m.RelatedPublicId == d.PublicId)
                              orderby m.Id
                              select new { m.PublicId, m.Type, e.Status })
            .ToListAsync(ct);
        var disponible = destinos.Any(x => x.Destino == IntegrationDestinations.Lending);

        var lendingMessages = new List<LendingMessageDto>(mensajes.Count);
        foreach (var m in mensajes)
        {
            var validacion = await cartera.EstadoDeValidacionAsync(m.PublicId, ct);
            lendingMessages.Add(new LendingMessageDto(m.PublicId, m.Type, m.Status.ToString(), disponible,
                new LendingValidationDto(validacion.Status, validacion.EvaluatedAt, validacion.Reason)));
        }

        return Result.Success(new SalesDocumentCreditDto(
            pagos.Select(p =>
            {
                var solicitud = solicitudes.Where(r => r.SourcePublicId == p.PublicId).OrderByDescending(r => r.Id).FirstOrDefault();
                var aprobo = solicitud?.Decisions.Where(x => x.Decision == ApprovalDecisionKind.Approve).OrderByDescending(x => x.Level).FirstOrDefault();
                return new CreditPaymentDto(p.PublicId, p.MeansCode, p.MeansClass, p.Amount, p.InstallmentCount, p.CreditTermDays, p.InstallmentPeriodDays,
                    p.FirstDueDate, p.FinalDueDate, p.SuggestedCreditLineCode, p.PendingValidation, p.CreditOrigin,
                    solicitud is null ? null : new CreditPaymentApprovalDto(solicitud.PublicId, solicitud.Status.ToString(), aprobo?.DecidedByName,
                        aprobo?.Level, aprobo?.DecidedAt));
            }).ToList(),
            pagos.Select(p => p.AccountsReceivableRecordedBy).FirstOrDefault(s => s is not null),
            lendingMessages));
    }
}
