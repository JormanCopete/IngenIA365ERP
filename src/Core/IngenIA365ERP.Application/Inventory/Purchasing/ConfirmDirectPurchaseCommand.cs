using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// La compra directa en un paso (feature 012, T344; FR-019; contracts/api.md §14.3, <c>POST /purchases/direct</c>): la recepción
/// y la factura del proveedor con las mismas líneas y precios, la misma cadena y el mismo modo sellado, en <b>una</b>
/// transacción. (nuevo)
/// <para>
/// 2026-11-13: la compra directa se crea y se confirma de una vez, así que el aviso del borrador llegaba tarde. Si el cálculo deja el
/// IVA por fuera porque el proveedor no es responsable de IVA, no confirma sin <paramref name="AcknowledgeWithoutVat"/>: responde
/// <c>Inventory.Purchase.SupplierNotVatResponsible</c> sin guardar nada. El documento soporte no lo pide (ahí lo normal es sin IVA).
/// </para>
/// <para>
/// 2026-11-14 (compra directa desde el XML, decisiones del dueño): <paramref name="Xml"/> trae lo que la pantalla leyó del XML del proveedor.
/// La compra se confirma aunque no cuadre —un descuento o cargo de la cabecera, el IVA o el total del ERP fuera de
/// <c>Compras.ToleranciaFrenteAlXml</c>, o un producto que no tiene la unidad del proveedor—, pero con una <b>nota aclaratoria</b>: sin ella
/// responde <c>Inventory.Purchase.XmlClarificationRequired</c> sin guardar nada. La nota queda en las notas de la recepción y de la factura.
/// Las líneas con «recordar» guardan la equivalencia del proveedor (<see cref="EquivalenciasDelProveedor"/>) y cada producto sin la unidad del
/// proveedor levanta <c>Compras.ProductoPorRevisar</c> para quien administra el catálogo, todo en la misma transacción.
/// <paramref name="ServiceLines"/> son los servicios del XML (un flete): van sólo a la factura, porque la recepción no recibe servicios.
/// </para>
/// </summary>
public sealed record ConfirmDirectPurchaseCommand(
    SaveInventoryDraftRequest Receipt,
    DirectPurchaseInvoiceRequest Invoice,
    bool AcknowledgeWithoutVat = false,
    DirectPurchaseXmlRequest? Xml = null,
    IReadOnlyList<SaveInventoryDraftLine>? ServiceLines = null)
    : IRequest<Result<DirectPurchaseResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>El largo de la nota aclaratoria (cabe dos veces en las notas del documento, de 1.000).</summary>
    public const int LargoDeLaNota = 450;
}

public sealed class ConfirmDirectPurchaseCommandValidator : AbstractValidator<ConfirmDirectPurchaseCommand>
{
    public ConfirmDirectPurchaseCommandValidator()
    {
        RuleFor(x => x.Receipt).NotNull();
        RuleFor(x => x.Receipt.DocumentTypePublicId).NotEmpty().WithMessage("Indicá el tipo de la recepción.");
        RuleFor(x => x.Receipt.WarehousePublicId).NotEmpty().WithMessage("Indicá la bodega que recibe.");
        RuleFor(x => x.Receipt.Contraparte).NotEmpty().WithMessage("Indicá el proveedor.");
        RuleFor(x => x.Receipt.Lines).NotEmpty().WithMessage("La compra lleva al menos una línea.");
        RuleForEach(x => x.Receipt.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductPublicId).NotEmpty();
            l.RuleFor(x => x.UnitPublicId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).NotNull().GreaterThanOrEqualTo(0).WithMessage("Cada línea de la compra lleva su precio.");
        });
        RuleFor(x => x.Invoice).NotNull();
        RuleFor(x => x.Invoice.DocumentTypePublicId).NotEmpty().WithMessage("Indicá el tipo de la factura del proveedor.");
        // I4 (T741): con un tipo de documento soporte no hay documento del proveedor; la factura lo exige en el handler.
        RuleFor(x => x.Invoice.Supplier.Number).NotEmpty().MaximumLength(SupplierInvoiceDetail.LargoDelNumero).WithMessage("Indicá el número de la factura.")
            .When(x => x.Invoice?.Supplier is not null);
        RuleFor(x => x.Invoice.Supplier.Prefix).MaximumLength(SupplierInvoiceDetail.LargoDelPrefijo).When(x => x.Invoice?.Supplier is not null);
        RuleForEach(x => x.ServiceLines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductPublicId).NotEmpty();
            l.RuleFor(x => x.UnitPublicId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).NotNull().GreaterThanOrEqualTo(0).WithMessage("Cada servicio de la compra lleva su precio.");
        });
        RuleFor(x => x.Xml!.ClarificationNote).MaximumLength(ConfirmDirectPurchaseCommand.LargoDeLaNota).When(x => x.Xml is not null);
        RuleForEach(x => x.Xml!.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductPublicId).NotEmpty();
            l.RuleFor(x => x.SupplierItemCode).MaximumLength(SupplierItemCode.LargoDelCodigo);
        }).When(x => x.Xml is not null);
    }
}

/// <summary>
/// En orden, dentro de una <see cref="TransaccionExplicita"/>: la factura del proveedor no está ya registrada (antes de
/// guardar nada); el borrador de la recepción (el ciclo común con las reglas de compras); su confirmación por el flujo canónico
/// —la política del tipo de la recepción y el monto máximo de <c>Purchases.Confirm</c> se miden con el total de la compra, que
/// es el de la factura—; el borrador de la factura con las mismas líneas enlazadas a la recepción; y, si la recepción quedó
/// confirmada, la confirmación de la factura (copia el modo de la recepción). Si la recepción quedó en aprobación, la factura
/// queda en borrador enlazada y <see cref="CompraDirectaEncadenada"/> la confirma con la última aprobación, en la transacción
/// del aprobador. Un error de la factura no deja nada guardado.
/// </summary>
public sealed class ConfirmDirectPurchaseCommandHandler(
    IApplicationDbContext db,
    SaveInventoryDraftCommandHandler guardar,
    ConfirmacionDeDocumento confirmacion,
    ContextoDeCompraDirecta contexto,
    VistaDeDocumentos vista,
    ILectorDeParametros? parametros = null,
    IAlertas? alertas = null)
    : IRequestHandler<ConfirmDirectPurchaseCommand, Result<DirectPurchaseResultDto>>
{
    /// <summary>La tolerancia frente al XML cuando el parámetro no se puede leer (su defecto).</summary>
    public const decimal ToleranciaPorDefecto = 1m;

    public Task<Result<DirectPurchaseResultDto>> Handle(ConfirmDirectPurchaseCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => ComprarAsync(request, ct), ct);

    private async Task<Result<DirectPurchaseResultDto>> ComprarAsync(ConfirmDirectPurchaseCommand request, CancellationToken ct)
    {
        // (0) Los tipos y la factura del proveedor, antes de guardar nada.
        var tipoRecepcion = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == request.Receipt.DocumentTypePublicId, ct);
        if (tipoRecepcion is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        if (tipoRecepcion.Class != DocumentClass.PurchaseReceipt) return Falla(InventoryErrors.TypeNotForRoute(tipoRecepcion.Class, DocumentClassGroup.Purchases));
        var tipoFactura = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == request.Invoice.DocumentTypePublicId, ct);
        if (tipoFactura is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        // I4 (T741): a un vendedor no obligado a facturar, la compra directa se soporta con el documento soporte (lo numera la cooperativa).
        var esSoporte = tipoFactura.Class == DocumentClass.SupportDocument;
        if (tipoFactura.Class != DocumentClass.SupplierInvoice && !esSoporte) return Falla(InventoryErrors.TypeNotForRoute(tipoFactura.Class, DocumentClassGroup.Purchases));
        if (!esSoporte && request.Invoice.Supplier is null) return Falla(InventoryErrors.FieldRequired("supplier"));

        var proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == request.Receipt.Contraparte).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (proveedor is null) return Falla(ErroresDelDocumento.PersonaInexistente());
        if (!esSoporte)
        {
            var previo = await ColisionDeFacturaDeProveedor.BuscarAsync(db, new SupplierInvoiceDetail
            {
                DocumentClass = DocumentClass.SupplierInvoice,
                SupplierPersonId = proveedor.Value,
                SupplierPrefix = SupplierInvoiceDetail.NormalizarNumero(request.Invoice.Supplier!.Prefix),
                SupplierNumber = SupplierInvoiceDetail.NormalizarNumero(request.Invoice.Supplier.Number),
                Cufe = SupplierInvoiceDetail.NormalizarCufe(request.Invoice.Supplier.Cufe),
            }, ct);
            if (previo is not null) return Falla(previo);
        }
        contexto.EnCurso = true;

        // 2026-11-14: la nota aclaratoria del XML queda en las notas de la recepción y de la factura.
        var nota = request.Xml?.ClarificationNote?.Trim();
        var recepcionPedida = string.IsNullOrEmpty(nota)
            ? request.Receipt
            : request.Receipt with { Notes = string.IsNullOrWhiteSpace(request.Receipt.Notes) ? $"Nota aclaratoria: {nota}" : $"{request.Receipt.Notes.Trim()} · Nota aclaratoria: {nota}" };

        // (1) La recepción: su borrador y su confirmación (o su solicitud de aprobación).
        var recepcion = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, recepcionPedida), ct);
        if (recepcion.IsFailure) return Falla(recepcion.Error);
        if (!esSoporte && !request.AcknowledgeWithoutVat
            && recepcion.Value.Warnings.FirstOrDefault(a => a.Code == ErroresDeCompras.SupplierNotVatResponsibleCode) is { } sinIva)
            return Falla(new ErrorConDatos(sinIva.Code, sinIva.Message, sinIva.Data ?? new { }));
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(recepcion.Value.PublicId, DocumentClassGroup.Purchases), ct);
        if (confirmada.IsFailure) return Falla(confirmada.Error);

        // (2) La factura: las mismas líneas, enlazadas a las de la recepción, con el documento del proveedor.
        var lineas = recepcion.Value.Lines.OrderBy(l => l.LineNumber).Select(l =>
        {
            var pedida = request.Receipt.Lines[l.LineNumber - 1];
            return new SaveInventoryDraftLine(null, l.Product.PublicId, l.Unit.PublicId, l.Quantity, pedida.UnitPrice, pedida.DiscountPercent,
                pedida.DiscountAmount, ReceiptLinePublicId: l.LinePublicId);
        }).ToList();
        // 2026-11-14: los servicios del XML (un flete) van sólo a la factura: la recepción no recibe servicios.
        lineas.AddRange(request.ServiceLines ?? []);
        var borradorFactura = new SaveInventoryDraftRequest(tipoFactura.PublicId, request.Receipt.OperationDate, null, null,
            request.Receipt.CostCenterPublicId, request.Receipt.Contraparte, null, null, null, recepcionPedida.Notes, null, null, null, lineas,
            OperationMunicipalityDaneCode: request.Receipt.OperationMunicipalityDaneCode,
            Supplier: esSoporte ? null : request.Invoice.Supplier);

        contexto.PermiteRecepcionPendiente = true;
        Result<InventoryDocumentDto> factura;
        try
        {
            factura = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, borradorFactura), ct);
        }
        finally
        {
            contexto.PermiteRecepcionPendiente = false;
        }
        if (factura.IsFailure) return Falla(factura.Error);

        // 2026-11-14: el XML del proveedor es control. Lo que no cuadra exige la nota aclaratoria; sin ella no se guarda nada.
        if (request.Xml is { } xml)
        {
            var revision = await RevisarContraElXmlAsync(xml, factura.Value.Totals, request.Receipt.OperationDate, ct);
            if (revision is not null && string.IsNullOrEmpty(nota)) return Falla(revision);
        }

        // (3) Con la recepción confirmada, la factura se confirma en la misma transacción.
        ConfirmationResultDto? facturaConfirmada = null;
        if (confirmada.Value.Status == DocumentStatus.Confirmed)
        {
            var r = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura.Value.PublicId, DocumentClassGroup.Purchases), ct);
            if (r.IsFailure) return Falla(r.Error);
            facturaConfirmada = r.Value;
        }

        // 2026-11-14: las equivalencias que la persona pidió recordar y el aviso al supervisor por la unidad que el producto no tiene.
        if (request.Xml?.Lines is { Count: > 0 } lineasDelXml)
            await DespuesDelXmlAsync(proveedor.Value, lineasDelXml, nota, request.Invoice.Supplier, ct);

        var documentoFactura = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.PublicId == factura.Value.PublicId, ct);
        var detalle = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == documentoFactura.Id, ct);
        var eventos = await ConsultasDeCompras.EventosAsync(db, documentoFactura.Id, ct);
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == documentoFactura.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var numeros = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.DocumentId == documentoFactura.Id).ToDictionaryAsync(l => l.Id, l => l.LineNumber, ct);
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);

        var mensajes = (confirmada.Value.Messages ?? []).Concat(facturaConfirmada?.Messages ?? []).ToList();
        return Result.Success(new DirectPurchaseResultDto(
            confirmada.Value.Status,
            new DocumentoCreadoDto(confirmada.Value.PublicId, confirmada.Value.DisplayNumber, confirmada.Value.Status),
            new FacturaCreadaDto(documentoFactura.PublicId, VistaDeDocumentos.NumeroVisible(documentoFactura.Prefix, documentoFactura.Number),
                documentoFactura.Status, detalle?.NumeroVisible),
            confirmada.Value.Approval,
            new DocumentTotalsDto(documentoFactura.Subtotal, documentoFactura.DiscountTotal, documentoFactura.TaxTotal, documentoFactura.WithholdingTotal,
                documentoFactura.Total, documentoFactura.AmountDue, costos ? documentoFactura.CostTotal : null),
            impuestos.Count > 0
                ? impuestos.Select(t => new DocumentTaxLineDto(t.DocumentLineId is int li && numeros.TryGetValue(li, out var n) ? n : null,
                    t.Kind, t.TaxRateCode, t.Rate, t.AmountPerUnit, t.Base, t.Amount, t.Treatment, t.MunicipalityDaneCode, t.ExplanationJson)).ToList()
                : factura.Value.TaxLines,
            eventos,
            confirmada.Value.Messages is null && facturaConfirmada?.Messages is null ? null : mensajes,
            recepcion.Value.Warnings.Concat(factura.Value.Warnings).ToList()));
    }

    /// <summary>
    /// Lo que no cuadra con el XML (nulo si todo cuadra): un descuento o cargo de la cabecera, el IVA o el total antes de retenciones del ERP
    /// a más de <c>Compras.ToleranciaFrenteAlXml</c> de los del XML, o productos que no tienen la unidad en que factura el proveedor.
    /// </summary>
    private async Task<Error?> RevisarContraElXmlAsync(DirectPurchaseXmlRequest xml, DocumentTotalsDto totales, DateOnly? fecha, CancellationToken ct)
    {
        var tolerancia = await ToleranciaAsync(fecha ?? DateOnly.FromDateTime(DateTime.Today), ct);
        var motivos = new List<string>();
        if (xml.HeaderDiscount > 0m || xml.HeaderCharges > 0m)
            motivos.Add($"El XML trae {(xml.HeaderDiscount > 0m ? $"un descuento de {xml.HeaderDiscount:N2}" : $"cargos de {xml.HeaderCharges:N2}")} a nivel de la factura, que no se reparte a las líneas.");
        if (Math.Abs(totales.TaxTotal - xml.TaxTotal) > tolerancia)
            motivos.Add($"El IVA que calcula el ERP ({totales.TaxTotal:N2}) difiere del XML ({xml.TaxTotal:N2}).");
        if (Math.Abs(totales.Total - xml.TaxInclusive) > tolerancia)
            motivos.Add($"El total antes de retenciones que calcula el ERP ({totales.Total:N2}) difiere del XML ({xml.TaxInclusive:N2}).");
        var sinUnidad = (xml.Lines ?? []).Count(l => l.UnitNotInProduct);
        if (sinUnidad > 0)
            motivos.Add(sinUnidad == 1 ? "Un producto no tiene la unidad en que factura el proveedor." : $"{sinUnidad} productos no tienen la unidad en que factura el proveedor.");
        return motivos.Count == 0 ? null : ErroresDeCompras.XmlClarificationRequired(motivos, totales.TaxTotal, xml.TaxTotal, totales.Total, xml.TaxInclusive);
    }

    private async Task<decimal> ToleranciaAsync(DateOnly fecha, CancellationToken ct)
    {
        if (parametros is null) return ToleranciaPorDefecto;
        var leido = await parametros.LeerComoAsync<decimal>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasToleranciaFrenteAlXml, fecha, ct: ct);
        return leido.IsSuccess && leido.Value >= 0m ? leido.Value : ToleranciaPorDefecto;
    }

    /// <summary>
    /// Recuerda las equivalencias marcadas (código del proveedor → producto y empaque) y levanta <c>Compras.ProductoPorRevisar</c> por cada
    /// producto que no tiene la unidad del proveedor. En la transacción de la compra.
    /// </summary>
    private async Task DespuesDelXmlAsync(int proveedorId, IReadOnlyList<DirectPurchaseXmlLineRequest> lineas, string? nota, SupplierDocumentRequest? documento,
        CancellationToken ct)
    {
        var publicos = lineas.Select(l => l.ProductPublicId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => publicos.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId, p.Code, p.Name }).ToDictionaryAsync(p => p.PublicId, ct);
        var ids = productos.Values.Select(p => p.Id).ToList();
        var empaques = await db.ProductUnits.AsNoTracking().Where(u => ids.Contains(u.ProductId))
            .Select(u => new { u.Id, u.ProductId, UnitPublicId = u.Unit!.PublicId, UnitCode = u.Unit.Code }).ToListAsync(ct);

        var recordar = lineas.Where(l => l.Remember && !string.IsNullOrWhiteSpace(l.SupplierItemCode) && productos.ContainsKey(l.ProductPublicId))
            .Select(l =>
            {
                var p = productos[l.ProductPublicId];
                var empaque = empaques.FirstOrDefault(u => u.ProductId == p.Id && u.UnitPublicId == l.UnitPublicId);
                return new EquivalenciaARecordar(l.SupplierItemCode!, p.Id, empaque?.Id);
            }).ToList();
        if (recordar.Count > 0)
        {
            await EquivalenciasDelProveedor.RecordarAsync(db, proveedorId, recordar, ct);
            await db.SaveChangesAsync(ct);
        }

        if (alertas is null) return;
        var proveedor = await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == proveedorId, ct);
        var nombre = proveedor is null ? "El proveedor" : Pos.BorradorDelPos.Nombre(proveedor);
        var factura = documento is null ? string.Empty : $" en la factura {documento.Prefix}{documento.Number}";
        foreach (var l in lineas.Where(l => l.UnitNotInProduct && productos.ContainsKey(l.ProductPublicId))
                     .GroupBy(l => (l.ProductPublicId, l.XmlUnitCode)).Select(g => g.First()))
        {
            var p = productos[l.ProductPublicId];
            var unidadDelXml = string.IsNullOrWhiteSpace(l.XmlUnitCode) ? "(sin unidad)" : l.XmlUnitCode.Trim();
            var elegida = empaques.FirstOrDefault(u => u.ProductId == p.Id && u.UnitPublicId == l.UnitPublicId)?.UnitCode ?? "su unidad base";
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ProductoPorRevisar,
                $"Revisar el producto {p.Code}: el proveedor lo factura en «{unidadDelXml}»",
                $"{nombre} facturó {p.Code} · {p.Name}{factura} en la unidad «{unidadDelXml}», que el producto no tiene. Quien compró la registró en "
                + $"{elegida} y dejó esta nota: «{nota}». Revise el producto y agregue la unidad con su factor si corresponde.",
                "InventoryProduct", p.PublicId, DedupKey: $"{TiposDeAlerta.ProductoPorRevisar}:{p.PublicId:N}:{unidadDelXml}", ModuloDeOrigen: "Inventory"), ct);
        }
    }

    private static Result<DirectPurchaseResultDto> Falla(Error error) => Result.Failure<DirectPurchaseResultDto>(error);
}

/// <summary>
/// La última aprobación de la recepción de una compra directa confirma también su factura (T344): la que está en borrador
/// enlazada a ella (<c>InvoiceOfReceipt</c>). Sólo la compra directa deja una factura así (el borrador común no admite una
/// recepción sin confirmar). <see cref="ConfirmacionDeDocumento"/> se resuelve al usarla: depende del motor de aprobaciones,
/// que depende de las fuentes. (nuevo)
/// </summary>
public sealed class CompraDirectaEncadenada(IApplicationDbContext db, IServiceProvider servicios) : IConfirmacionEncadenada
{
    public async Task<Result> AlConfirmarPorAprobacionAsync(Guid documentoPublicId, CancellationToken ct)
    {
        var recepcion = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.PublicId == documentoPublicId && d.Class == DocumentClass.PurchaseReceipt)
            .Select(d => new { d.Id, d.Status }).FirstOrDefaultAsync(ct);
        if (recepcion is null || recepcion.Status != DocumentStatus.Confirmed) return Result.Success();

        var facturas = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == recepcion.Id && l.Kind == DocumentLinkKind.InvoiceOfReceipt)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => d.Status == DocumentStatus.Draft && d.Class == DocumentClass.SupplierInvoice)
            .Select(d => d.PublicId).Distinct().ToListAsync(ct);
        if (facturas.Count == 0) return Result.Success();

        var confirmacion = servicios.GetRequiredService<ConfirmacionDeDocumento>();
        foreach (var factura in facturas)
        {
            var r = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura, DocumentClassGroup.Purchases), ct);
            if (r.IsFailure) return Result.Failure(r.Error);
        }
        return Result.Success();
    }
}
