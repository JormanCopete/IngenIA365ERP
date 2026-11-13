using System.Globalization;
using System.Net;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.Quotes;

// La cotización impresa para el cliente (2026-11-12, decisión del dueño). Application arma el modelo y la API lo dibuja con QuestPDF
// (SalesQuoteReport), como la orden de compra: Application no conoce la librería. (nuevos)

/// <summary>La cooperativa que cotiza: nombre, NIT con su dígito, dirección, teléfono y ciudad. (nuevo)</summary>
public sealed record EmpresaDeLaCotizacion(string Name, string TaxId, string? Address, string? Phone, string? City);

/// <summary>El cliente: su copia fiscal si la cotización ya se confirmó; si no, el maestro de personas. (nuevo)</summary>
public sealed record ClienteDeLaCotizacion(string Name, string? IdType, string? IdNumber, string? CheckDigit, string? Address, string? Email, string? Phone);

/// <summary>Una línea: código, descripción, unidad, cantidad, precio unitario (sin impuestos), descuento, impuestos y total. (nuevo)</summary>
public sealed record LineaDeLaCotizacion(int LineNumber, string ProductCode, string Description, string UnitCode, decimal Quantity, decimal UnitPrice,
    decimal DiscountAmount, decimal TaxAmount, decimal Total);

/// <summary>Un impuesto o una retención agrupados por tarifa, con su base. (nuevo)</summary>
public sealed record RenglonDeLaCotizacion(string Label, decimal? Rate, decimal Base, decimal Amount);

/// <summary>Todo lo que dibuja <c>SalesQuoteReport</c>. (nuevo)</summary>
public sealed record CotizacionImprimible(
    Guid PublicId,
    string? DisplayNumber,
    string DocumentTypeName,
    DocumentStatus Status,
    DateOnly OperationDate,
    DateOnly? ValidUntil,
    EmpresaDeLaCotizacion Empresa,
    ClienteDeLaCotizacion Cliente,
    string? Vendedor,
    IReadOnlyList<LineaDeLaCotizacion> Lineas,
    IReadOnlyList<RenglonDeLaCotizacion> Impuestos,
    IReadOnlyList<RenglonDeLaCotizacion> Retenciones,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal WithholdingTotal,
    decimal Total,
    decimal AmountDue,
    string? Notas,
    string? Condiciones,
    string? ElaboradaPor)
{
    /// <summary>El nombre del archivo: <c>cotizacion-{número}.pdf</c> o, sin número, <c>cotizacion-borrador-…</c>.</summary>
    public string NombreDelArchivo => DisplayNumber is { } n
        ? $"cotizacion-{n}.pdf"
        : $"cotizacion-borrador-{PublicId.ToString("N", CultureInfo.InvariantCulture)[..8]}.pdf";
}

/// <summary>
/// La cotización en PDF. La implementa la API con <c>SalesQuoteReport</c> (QuestPDF). Sin implementación registrada, el PDF y el envío responden
/// <c>Inventory.Document.RepresentationUnavailable</c>. (nuevo)
/// </summary>
public interface ICotizacionEnPdf
{
    byte[] Generar(CotizacionImprimible cotizacion);
}

/// <summary>El PDF de una cotización y su nombre de archivo. (nuevo)</summary>
public sealed record CotizacionEnPdfDto(byte[] Pdf, string FileName, CotizacionImprimible Cotizacion);

/// <summary>
/// Arma el modelo de la cotización para el PDF: encabezado de la cooperativa, número, fecha y «Vigente hasta», el cliente (copia fiscal o
/// maestro), el vendedor, las líneas con su impuesto, los impuestos y retenciones por tarifa calculados por <see cref="CalculoTributarioDeVenta"/>
/// —el mismo cálculo de la factura—, los totales guardados, las notas y las «Condiciones de la cotización» vigentes a su fecha
/// (<c>Comercial.CondicionesDeLaCotizacion</c>). Sin el cálculo, los totales guardados sin desglose. (nuevo)
/// </summary>
public sealed class ModeloDeCotizacion(IApplicationDbContext db, CalculoTributarioDeVenta calculo, ILectorDeParametros parametros)
{
    public async Task<CotizacionImprimible> ArmarAsync(InventoryDocument cotizacion, CancellationToken ct)
    {
        var tipo = cotizacion.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == cotizacion.DocumentTypeId, ct);
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var cooperativa = empresa is null
            ? new EmpresaDeLaCotizacion(string.Empty, string.Empty, null, null, null)
            : new EmpresaDeLaCotizacion(empresa.Name, empresa.TaxIdCheckDigit is { Length: > 0 } dv ? $"{empresa.TaxId}-{dv}" : empresa.TaxId,
                empresa.Address, empresa.Phone, empresa.City);

        var foto = await db.DocumentPartySnapshots.AsNoTracking().Where(s => s.DocumentId == cotizacion.Id).OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
        if (foto is null && cotizacion.CounterpartyPersonId is int id
            && await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) is { } persona)
            foto = FotoDeLaContraparte.De(cotizacion, persona);
        var cliente = foto is null
            ? new ClienteDeLaCotizacion(string.Empty, null, null, null, null, null, null)
            : new ClienteDeLaCotizacion(foto.LegalName, foto.DianIdTypeCode, foto.TaxId, foto.CheckDigit, foto.Address, foto.Email, foto.Phone);

        var vendedor = cotizacion.SalespersonId is int v
            ? await db.Salespeople.IgnoreQueryFilters().AsNoTracking().Include(s => s.Person).Where(s => s.Id == v).FirstOrDefaultAsync(ct)
            : null;

        var vivas = cotizacion.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var unidadIds = vivas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);

        IReadOnlyList<RenglonTributario> renglones = [];
        if (vivas.Count > 0)
        {
            var calculado = await calculo.CalcularAsync(cotizacion, ct);
            if (calculado.IsSuccess) renglones = calculado.Value.Renglones;
        }
        var impuestoDeLinea = renglones.Where(r => !r.EsRetencion && r.Treatment == TaxTreatment.Generated && r.Linea is not null)
            .GroupBy(r => r.Linea!.Value).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        var lineas = vivas.Select(l =>
        {
            var impuesto = impuestoDeLinea.GetValueOrDefault(l.LineNumber);
            var producto = productos.GetValueOrDefault(l.ProductId);
            return new LineaDeLaCotizacion(l.LineNumber, producto?.Code ?? string.Empty,
                string.IsNullOrWhiteSpace(l.Description) ? producto?.Name ?? string.Empty : l.Description!, unidades.GetValueOrDefault(l.UnitId) ?? string.Empty,
                l.Quantity, l.UnitPrice, l.DiscountAmount, impuesto, l.NetAmount + impuesto);
        }).ToList();

        var impuestos = renglones.Where(r => !r.EsRetencion && r.Treatment == TaxTreatment.Generated)
            .GroupBy(r => (r.Kind, r.Rate)).Select(g => new RenglonDeLaCotizacion(Etiqueta(g.Key.Kind), g.Key.Rate, g.Sum(r => r.Base), g.Sum(r => r.Amount)))
            .ToList();
        var retenciones = renglones.Where(r => r.EsRetencion)
            .GroupBy(r => (r.Kind, r.Rate)).Select(g => new RenglonDeLaCotizacion($"Retención {Etiqueta(g.Key.Kind)}", g.Key.Rate, g.Sum(r => r.Base), g.Sum(r => r.Amount)))
            .ToList();

        var condiciones = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComercialCondicionesDeLaCotizacion,
            cotizacion.OperationDate, ct: ct);
        var elaboro = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == cotizacion.CreatedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);

        return new CotizacionImprimible(cotizacion.PublicId, VistaDeDocumentos.NumeroVisible(cotizacion.Prefix, cotizacion.Number), tipo.Name,
            cotizacion.Status, cotizacion.OperationDate, cotizacion.ValidUntil, cooperativa, cliente,
            vendedor is null ? null : BorradorDelPos.Nombre(vendedor.Person), lineas, impuestos, retenciones,
            cotizacion.Subtotal, cotizacion.DiscountTotal, cotizacion.TaxTotal, cotizacion.WithholdingTotal, cotizacion.Total, cotizacion.AmountDue,
            string.IsNullOrWhiteSpace(cotizacion.Notes) ? null : cotizacion.Notes,
            condiciones.IsSuccess && !string.IsNullOrWhiteSpace(condiciones.Value.Texto) ? condiciones.Value.Texto.Trim() : null,
            elaboro);
    }

    private static string Etiqueta(TaxKind kind) => kind switch
    {
        TaxKind.Iva => "IVA",
        TaxKind.Inc => "INC",
        TaxKind.Ica => "ICA",
        _ => kind.ToString(),
    };
}

/// <summary>Lo que comparten el PDF y el envío. (nuevo)</summary>
public static class CotizacionesEnPdf
{
    public static async Task<Result<CotizacionEnPdfDto>> GenerarAsync(InventoryDocument cotizacion, ModeloDeCotizacion modelo,
        IEnumerable<ICotizacionEnPdf> generadores, CancellationToken ct)
    {
        var generador = generadores.FirstOrDefault();
        if (generador is null)
            return Result.Failure<CotizacionEnPdfDto>(new Error(Purchasing.OrdenesDeCompraEnPdf.RepresentationUnavailableCode,
                "La cotización en PDF todavía no está disponible en este despliegue."));
        var imprimible = await modelo.ArmarAsync(cotizacion, ct);
        return Result.Success(new CotizacionEnPdfDto(generador.Generar(imprimible), imprimible.NombreDelArchivo, imprimible));
    }
}

/// <summary>
/// La cotización en PDF (<c>GET /api/inventory/sales/quotes/{id}/pdf</c>, <c>Inventory.Sales.View</c>): para verla o imprimirla al cliente. Con
/// alcance: fuera de él, o si no es una cotización, el 404 del documento. Un borrador también se imprime (con la marca «BORRADOR», sin
/// número). (nuevo)
/// </summary>
public sealed record GetSalesQuotePdfQuery(Guid QuotePublicId) : IRequest<Result<CotizacionEnPdfDto>>;

public sealed class GetSalesQuotePdfQueryValidator : AbstractValidator<GetSalesQuotePdfQuery>
{
    public GetSalesQuotePdfQueryValidator() => RuleFor(x => x.QuotePublicId).NotEmpty();
}

public sealed class GetSalesQuotePdfQueryHandler(VistaDeDocumentos vista, ModeloDeCotizacion modelo, IEnumerable<ICotizacionEnPdf> generadores)
    : IRequestHandler<GetSalesQuotePdfQuery, Result<CotizacionEnPdfDto>>
{
    public async Task<Result<CotizacionEnPdfDto>> Handle(GetSalesQuotePdfQuery request, CancellationToken ct)
    {
        var cotizacion = await vista.BuscarAsync(request.QuotePublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (cotizacion is null || cotizacion.Class != DocumentClass.SalesQuote) return Result.Failure<CotizacionEnPdfDto>(InventoryErrors.DocumentNotFound());
        return await CotizacionesEnPdf.GenerarAsync(cotizacion, modelo, generadores, ct);
    }
}

/// <summary>
/// Envía la cotización al cliente en PDF (<c>POST /api/inventory/sales/quotes/{id}/send</c>, <c>Inventory.Sales.SendQuote</c> → 204), como
/// «Enviar al proveedor» de la orden de compra:
/// <list type="bullet">
/// <item>sólo una cotización confirmada (<c>Inventory.Quote.NotConfirmed</c>); fuera del alcance o de otra clase, el 404 del documento;</item>
/// <item>el correo es <see cref="Email"/> o, si no viene, el del cliente en el maestro (<c>Inventory.Quote.CustomerEmailMissing</c>);</item>
/// <item>el PDF lo dibuja <see cref="ICotizacionEnPdf"/> y sale por <see cref="IEmailSender"/> directo;</item>
/// <item>cada envío queda en la auditoría como <c>Inventory.SalesQuote.Sent</c> con el correo y el número. Enviar otra vez es otro envío.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record SendSalesQuoteCommand(Guid QuotePublicId, string? Email = null) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendSalesQuoteCommandValidator : AbstractValidator<SendSalesQuoteCommand>
{
    public SendSalesQuoteCommandValidator()
    {
        RuleFor(x => x.QuotePublicId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class SendSalesQuoteCommandHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    ModeloDeCotizacion modelo,
    IEnumerable<ICotizacionEnPdf> generadores,
    IEmailSender correo,
    InventoryAuditEmitter auditoria)
    : IRequestHandler<SendSalesQuoteCommand, Result>
{
    public async Task<Result> Handle(SendSalesQuoteCommand request, CancellationToken ct)
    {
        var cotizacion = await vista.BuscarAsync(request.QuotePublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (cotizacion is null || cotizacion.Class != DocumentClass.SalesQuote) return Result.Failure(InventoryErrors.DocumentNotFound());
        if (cotizacion.Status != DocumentStatus.Confirmed) return Result.Failure(ErroresDelCicloComercial.QuoteNotConfirmed(cotizacion.Status));

        var destino = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(destino))
        {
            var cliente = await db.People.AsNoTracking().Where(p => p.Id == cotizacion.CounterpartyPersonId)
                .Select(p => new { p.PublicId, p.Email }).FirstOrDefaultAsync(ct);
            destino = cliente?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(destino)) return Result.Failure(ErroresDelCicloComercial.QuoteCustomerEmailMissing(cliente?.PublicId ?? Guid.Empty));
        }

        var pdf = await CotizacionesEnPdf.GenerarAsync(cotizacion, modelo, generadores, ct);
        if (pdf.IsFailure) return Result.Failure(pdf.Error);
        var c = pdf.Value.Cotizacion;
        var numero = c.DisplayNumber ?? string.Empty;
        var vigencia = c.ValidUntil is { } vence ? $", vigente hasta el {vence:dd/MM/yyyy}" : string.Empty;
        await correo.SendAsync(new EmailMessage(destino,
            $"Cotización {numero} — {c.Empresa.Name}",
            $"<p>Señor(a) {WebUtility.HtmlEncode(c.Cliente.Name)}:</p><p>Adjuntamos la cotización {WebUtility.HtmlEncode(numero)} de "
            + $"{WebUtility.HtmlEncode(c.Empresa.Name)}{WebUtility.HtmlEncode(vigencia)}.</p>",
            Attachments: [new EmailAttachment(pdf.Value.FileName, "application/pdf", pdf.Value.Pdf)]), ct);

        await auditoria.EmitAsync(AuditEventTypes.InventorySalesQuoteSent, "InventoryDocument", cotizacion.PublicId, null,
            new { email = destino, displayNumber = numero, fileName = pdf.Value.FileName }, ct);
        return Result.Success();
    }
}
