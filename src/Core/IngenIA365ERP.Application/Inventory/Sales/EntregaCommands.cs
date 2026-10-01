using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// La representación en carta (PDF) de un comprobante no electrónico (feature 012, I3, T607; §20.3). La implementa la API con
/// <c>SalesDocumentReport</c> (QuestPDF, T626): Application no conoce QuestPDF. Sin implementación registrada, la carta y el correo
/// responden <c>Inventory.Document.RepresentationUnavailable</c>. (nuevo)
/// </summary>
public interface IRepresentacionDeVentaEnPdf
{
    Task<byte[]> GenerarAsync(TicketDto modelo, CancellationToken ct);
}

/// <summary>Lo que devuelve la entrega o la reimpresión: la tirilla, o el PDF de la carta, y si salió el correo. (nuevo)</summary>
public sealed record EntregaDto(Guid DocumentPublicId, CashRegisterPrintFormat Format, bool Copy, TicketDto? Ticket, byte[]? Pdf, string? FileName, bool EmailSent,
    EnlaceDeDescargaDto? Link = null);

/// <summary>
/// La <b>primera</b> entrega de una venta confirmada (<c>POST /api/inventory/sales/documents/{id}/deliver</c>, §20.3), sin la marca
/// «COPIA» y auditada como <c>Inventory.Document.Delivered</c> con su formato. Tirilla → <see cref="TicketDto"/>; carta de un comprobante
/// no electrónico → el PDF de <see cref="IRepresentacionDeVentaEnPdf"/>; <c>sendEmail</c> manda el PDF por <see cref="IEmailSender"/> al
/// correo de la copia fiscal o a <c>email</c>. Ya entregada en ese formato → <c>Inventory.Document.AlreadyDelivered</c> (se reimprime);
/// no confirmada, o un documento electrónico antes de I4 → <c>ElectronicInvoicing.Document.NotDeliverable</c>. (nuevo)
/// </summary>
public sealed record DeliverSalesDocumentCommand(Guid DocumentPublicId, CashRegisterPrintFormat Format, bool SendEmail = false, string? Email = null)
    : IRequest<Result<EntregaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeliverSalesDocumentCommandValidator : AbstractValidator<DeliverSalesDocumentCommand>
{
    public DeliverSalesDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class DeliverSalesDocumentCommandHandler(
    IApplicationDbContext db, EntregaDeDocumentos entrega, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<DeliverSalesDocumentCommand, Result<EntregaDto>>
{
    public async Task<Result<EntregaDto>> Handle(DeliverSalesDocumentCommand request, CancellationToken ct)
    {
        var documento = await entrega.DocumentoAsync(request.DocumentPublicId, soloVentas: true, ct);
        if (documento.IsFailure) return Result.Failure<EntregaDto>(documento.Error);
        var d = documento.Value;
        if (d.Status != DocumentStatus.Confirmed) return Result.Failure<EntregaDto>(ErroresDelPos.NotDeliverable(d.Status));
        // I4 (T743): un documento electrónico se entrega después de validarse (o en contingencia); antes, NotDeliverable con su estado.
        var electronico = await entrega.ElectronicoAsync(d, ct);
        if (electronico.IsFailure) return Result.Failure<EntregaDto>(electronico.Error);
        if (await auditoria.ExisteAsync(AuditEventTypes.InventoryDocumentDelivered, d.PublicId, request.Format.ToString(), ct))
            return Result.Failure<EntregaDto>(ErroresDelPos.AlreadyDelivered(request.Format.ToString()));

        var r = electronico.Value is { } e
            ? await entrega.EntregarElectronicoAsync(d, e, request.Format, copia: false, request.SendEmail, ct)
            : await entrega.EntregarAsync(d, request.Format, copia: false, request.SendEmail, request.Email, ct);
        if (r.IsFailure) return r;
        await auditoria.AnotarAsync(AuditEventTypes.InventoryDocumentDelivered, request, d.PublicId,
            new { format = request.Format.ToString(), emailSent = r.Value.EmailSent }, ct,
            new Dictionary<string, string> { [AuditoriaDelPuntoDeVenta.ClaveDeFormato] = request.Format.ToString() });
        await db.SaveChangesAsync(ct);
        return r;
    }
}

/// <summary>
/// La reimpresión (<c>POST /api/inventory/documents/{id}/reprint</c>, permiso <c>Inventory.Documents.Reprint</c>, §20.3): lo mismo que la
/// entrega pero con <c>copy = true</c> y la marca «COPIA», desde la copia fiscal vigente (FR-011). Sirve para cualquier grupo; queda
/// auditada como <c>Inventory.Document.Reprinted</c>. (nuevo)
/// </summary>
public sealed record ReprintDocumentCommand(Guid DocumentPublicId, CashRegisterPrintFormat Format, string? Reason = null)
    : IRequest<Result<EntregaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReprintDocumentCommandValidator : AbstractValidator<ReprintDocumentCommand>
{
    public ReprintDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(250);
    }
}

public sealed class ReprintDocumentCommandHandler(IApplicationDbContext db, EntregaDeDocumentos entrega, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<ReprintDocumentCommand, Result<EntregaDto>>
{
    public async Task<Result<EntregaDto>> Handle(ReprintDocumentCommand request, CancellationToken ct)
    {
        var documento = await entrega.DocumentoAsync(request.DocumentPublicId, soloVentas: false, ct);
        if (documento.IsFailure) return Result.Failure<EntregaDto>(documento.Error);
        var d = documento.Value;
        if (d.Status is not (DocumentStatus.Confirmed or DocumentStatus.Voided)) return Result.Failure<EntregaDto>(InventoryErrors.NotConfirmed(d.Status));

        // I4 (T743): la copia de un electrónico sale del PDF guardado o, en tirilla, de su copia fiscal con el bloque electrónico y «COPIA».
        var electronico = d.Status == DocumentStatus.Confirmed
            ? await entrega.ElectronicoAsync(d, ct)
            : Result.Success<Domain.Entities.ElectronicInvoicing.ElectronicDocument?>(null);
        if (electronico.IsFailure) return Result.Failure<EntregaDto>(electronico.Error);
        var r = electronico.Value is { } e
            ? await entrega.EntregarElectronicoAsync(d, e, request.Format, copia: true, enviarCorreo: false, ct)
            : await entrega.EntregarAsync(d, request.Format, copia: true, enviarCorreo: false, null, ct);
        if (r.IsFailure) return r;
        await auditoria.AnotarAsync(AuditEventTypes.InventoryDocumentReprinted, request, d.PublicId,
            new { format = request.Format.ToString(), reason = request.Reason }, ct,
            new Dictionary<string, string> { [AuditoriaDelPuntoDeVenta.ClaveDeFormato] = request.Format.ToString() });
        await db.SaveChangesAsync(ct);
        return r;
    }
}

/// <summary>Lo que comparten la entrega y la reimpresión: el documento en el alcance, la tirilla, el PDF y el correo. (nuevo)</summary>
public sealed class EntregaDeDocumentos(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ConstructorDeTirilla tirilla,
    IEmailSender correo,
    IEnumerable<IRepresentacionDeVentaEnPdf> representaciones,
    ISender? sender = null,
    ElectronicInvoicing.Documents.EntregaAlComprador? entregaAlComprador = null,
    IDateTimeService? reloj = null)
{
    public const string RepresentationUnavailableCode = "Inventory.Document.RepresentationUnavailable";

    /// <summary>
    /// El documento electrónico (seguido) de una venta de clase electrónica, si está en un estado que se entrega; nulo si la clase no emite. Una
    /// clase electrónica sin documento o todavía sin validar → <c>ElectronicInvoicing.Document.NotDeliverable</c> con <c>data.status</c> (T743).
    /// </summary>
    public async Task<Result<ElectronicDocument?>> ElectronicoAsync(InventoryDocument d, CancellationToken ct)
    {
        if (GuardiaDeEmisionFiscal.TipoElectronicoDe(d.Class) is null) return Result.Success<ElectronicDocument?>(null);
        var e = await db.ElectronicDocuments
            .FirstOrDefaultAsync(x => x.SourceModule == FuenteDeEmisionDeInventario.Modulo && x.SourceDocumentPublicId == d.PublicId, ct);
        if (e is null) return Result.Failure<ElectronicDocument?>(ErroresDelPos.NotDeliverable(d.Status));
        if (!EntregaAlComprador.Entregable(e.Status)) return Result.Failure<ElectronicDocument?>(ErroresDeDocumentosElectronicos.NotDeliverable(e.Status));
        return Result.Success<ElectronicDocument?>(e);
    }

    /// <summary>
    /// La entrega de un documento electrónico (§20.3, T743): tirilla desde su copia fiscal con el bloque electrónico (código único, QR, leyenda y,
    /// en una copia, «COPIA»); carta → el enlace firmado de 60 s a la representación gráfica guardada (sin ella,
    /// <c>ElectronicInvoicing.Document.ArtifactNotFound</c>); <paramref name="enviarCorreo"/> reenvía por <see cref="EntregaAlComprador"/>. La
    /// primera entrega anota <c>DeliveredAt</c> (deja de estar pendiente de entrega).
    /// </summary>
    public async Task<Result<EntregaDto>> EntregarElectronicoAsync(InventoryDocument d, ElectronicDocument e, CashRegisterPrintFormat formato, bool copia,
        bool enviarCorreo, CancellationToken ct)
    {
        TicketDto? modelo = null;
        EnlaceDeDescargaDto? enlace = null;
        if (formato == CashRegisterPrintFormat.Letter)
        {
            if (sender is null)
                return Result.Failure<EntregaDto>(new Error(RepresentationUnavailableCode, "La representación en carta todavía no está disponible en este despliegue."));
            var link = await sender.Send(new GetElectronicArtifactLinkQuery(e.PublicId, ArtefactosElectronicos.GraphicRepresentation), ct);
            if (link.IsFailure) return Result.Failure<EntregaDto>(link.Error);
            enlace = link.Value;
        }
        else
        {
            modelo = await EsperaEnLineaDelPos.TirillaElectronicaAsync(db, tirilla, d, e, formato, copia, ct);
        }

        var enviado = false;
        if (enviarCorreo)
        {
            if (entregaAlComprador is null) return Result.Failure<EntregaDto>(ErroresDelPos.EmailRequired());
            var version = await db.ElectronicDocumentVersions.Where(v => v.ElectronicDocumentId == e.Id).OrderByDescending(v => v.VersionNumber).FirstAsync(ct);
            var r = await entregaAlComprador.EntregarAsync(e, version, null, null, forzar: true, ct);
            if (r.IsFailure) return Result.Failure<EntregaDto>(r.Error);
            enviado = r.Value;
        }
        if (!copia && e.DeliveredAt is null) e.DeliveredAt = reloj?.UtcNow ?? DateTime.UtcNow;
        return Result.Success(new EntregaDto(d.PublicId, formato, copia, modelo, null, null, enviado, enlace));
    }

    /// <summary>El documento, si está en el alcance del usuario (por su bodega o su punto); si no, el 404 del documento.</summary>
    public async Task<Result<InventoryDocument>> DocumentoAsync(Guid publicId, bool soloVentas, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (d is null) return Result.Failure<InventoryDocument>(InventoryErrors.DocumentNotFound());
        if (soloVentas && VistaDeDocumentos.GrupoDe(d.Class, null) != DocumentClassGroup.Sales) return Result.Failure<InventoryDocument>(InventoryErrors.DocumentNotFound());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var visible = d.PointOfSaleId is int punto ? alcance.IncluyePunto(punto) : FiltroDeAlcance.DocumentoVisible(alcance, d, []);
        return visible ? Result.Success(d) : Result.Failure<InventoryDocument>(InventoryErrors.DocumentNotFound());
    }

    public async Task<Result<EntregaDto>> EntregarAsync(InventoryDocument d, CashRegisterPrintFormat formato, bool copia, bool enviarCorreo, string? correoPedido,
        CancellationToken ct)
    {
        var modelo = await tirilla.ConstruirAsync(d, formato, copia, ct);
        byte[]? pdf = null;
        var archivo = $"{(string.IsNullOrEmpty(d.Prefix) ? "DOC" : d.Prefix)}-{d.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? d.PublicId.ToString("N")[..8]}{(copia ? "-copia" : string.Empty)}.pdf";
        if (formato == CashRegisterPrintFormat.Letter || enviarCorreo)
        {
            var generador = representaciones.FirstOrDefault();
            if (generador is null)
                return Result.Failure<EntregaDto>(new Error(RepresentationUnavailableCode, "La representación en carta todavía no está disponible en este despliegue."));
            pdf = await generador.GenerarAsync(modelo with { Format = CashRegisterPrintFormat.Letter }, ct);
        }

        var enviado = false;
        if (enviarCorreo)
        {
            var destino = !string.IsNullOrWhiteSpace(correoPedido)
                ? correoPedido.Trim()
                : await db.DocumentPartySnapshots.AsNoTracking().Where(s => s.DocumentId == d.Id).OrderByDescending(s => s.Version)
                    .Select(s => s.Email).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(destino)) return Result.Failure<EntregaDto>(ErroresDelPos.EmailRequired());
            var titulo = $"{modelo.Document.ClassLabel} {VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number)}";
            await correo.SendAsync(new EmailMessage(destino, titulo,
                $"<p>Adjuntamos su {System.Net.WebUtility.HtmlEncode(modelo.Document.ClassLabel.ToLowerInvariant())} de {System.Net.WebUtility.HtmlEncode(modelo.Header.CompanyName)}.</p>",
                Attachments: [new EmailAttachment(archivo, "application/pdf", pdf!)]), ct);
            enviado = true;
        }
        return Result.Success(new EntregaDto(d.PublicId, formato, copia,
            formato == CashRegisterPrintFormat.Letter ? null : modelo,
            formato == CashRegisterPrintFormat.Letter ? pdf : null,
            formato == CashRegisterPrintFormat.Letter ? archivo : null, enviado));
    }
}
