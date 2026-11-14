using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US9 (T124; FR-055; contracts/erp-api-de-servicio.md, «documents»): los extractos y certificados que la cooperativa publica en el canal,
// generados por el ERP en el momento de pedirlos con el mismo motor de Cartera (el PDF es el mismo que emite la oficina para esa persona y fecha). Lo
// disponible sale de los conceptos publicados del asociado y de sus marcas: extracto (crédito o cuenta), certificado (aportes) y certificado tributario
// (de créditos, o de captaciones y aportes). Un documento no publicado, o un producto que no es del asociado, no existe para él.

/// <summary>
/// Un documento que el asociado puede pedir. <see cref="Kind"/> por nombre (<c>Statement</c>, <c>AccountStatement</c>, <c>ContributionCertificate</c>,
/// <c>TaxCertificate</c>, <c>DepositTaxCertificate</c>); <see cref="Parameter"/>: <c>period</c> (desde y hasta), <c>year</c> (un año terminado) o
/// <c>none</c>. Sin producto, el documento es de la persona.
/// </summary>
public sealed record ChannelDocumentDto(string Kind, Guid? ProductPublicId, string? ProductNumber, string Description, string Parameter);

/// <summary>El PDF emitido.</summary>
public sealed record ChannelDocumentFileDto(byte[] Content, string FileName, string Number);

/// <summary>Los errores de los documentos del canal.</summary>
public static class ErroresDeDocumentosDelCanal
{
    public const string NoDisponible = "Channel.Document.NotAvailable";

    public static Error NoEstaDisponible() => new(NoDisponible, "Ese documento no está disponible en el canal para esta persona.");
}

/// <summary><c>GET /tenants/{t}/people/{p}/documents</c>.</summary>
public sealed record ListChannelDocumentsQuery(Guid PersonPublicId) : IRequest<Result<IReadOnlyList<ChannelDocumentDto>>>;

public sealed class ListChannelDocumentsQueryHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<ListChannelDocumentsQuery, Result<IReadOnlyList<ChannelDocumentDto>>>
{
    public const string Periodo = "period";
    public const string Año = "year";
    public const string Ninguno = "none";

    public async Task<Result<IReadOnlyList<ChannelDocumentDto>>> Handle(ListChannelDocumentsQuery request, CancellationToken ct)
    {
        var conceptos = await db.ChannelPublishedConcepts.AsNoTracking()
            .Where(c => c.PublishStatement || c.PublishCertificate || c.PublishTaxCertificate).ToListAsync(ct);
        if (conceptos.Count == 0) return Result.Success<IReadOnlyList<ChannelDocumentDto>>([]);
        var productos = await sender.Send(new GetChannelPayablesQuery(request.PersonPublicId), ct);
        if (productos.IsFailure) return Result.Failure<IReadOnlyList<ChannelDocumentDto>>(productos.Error);

        var lista = new List<ChannelDocumentDto>();
        void Agregar(ChannelDocumentDto d)
        {
            if (!lista.Any(x => x.Kind == d.Kind && x.ProductPublicId == d.ProductPublicId)) lista.Add(d);
        }
        foreach (var p in productos.Value.Items)
        {
            if (!Enum.TryParse<ConceptKind>(p.Kind, out var clase)) continue;
            // El concepto de su línea manda sobre el de todas las líneas de ese tipo.
            var c = conceptos.FirstOrDefault(x => x.Kind == clase && x.LineCode == p.LineCode) ?? conceptos.FirstOrDefault(x => x.Kind == clase && x.LineCode == null);
            if (c is null) continue;
            switch (clase)
            {
                case ConceptKind.Loan:
                    if (c.PublishStatement)
                        Agregar(new(nameof(IssuedDocumentKind.Statement), p.ProductPublicId, p.ProductNumber, $"Extracto del crédito {p.ProductNumber}", Periodo));
                    if (c.PublishTaxCertificate)
                        Agregar(new(nameof(IssuedDocumentKind.TaxCertificate), null, null, "Certificado tributario de créditos", Año));
                    break;
                case ConceptKind.Contribution:
                    if (c.PublishCertificate)
                        Agregar(new(nameof(IssuedDocumentKind.ContributionCertificate), null, null, "Certificado de aportes", Ninguno));
                    if (c.PublishTaxCertificate)
                        Agregar(new(nameof(IssuedDocumentKind.DepositTaxCertificate), null, null, "Certificado tributario de ahorros y aportes", Año));
                    break;
                case ConceptKind.ContractualSaving or ConceptKind.ProgrammedSaving:
                    if (c.PublishStatement)
                        Agregar(new(nameof(IssuedDocumentKind.AccountStatement), p.ProductPublicId, p.ProductNumber, $"Extracto de la cuenta {p.ProductNumber}", Periodo));
                    if (c.PublishTaxCertificate)
                        Agregar(new(nameof(IssuedDocumentKind.DepositTaxCertificate), null, null, "Certificado tributario de ahorros y aportes", Año));
                    break;
            }
        }
        return Result.Success<IReadOnlyList<ChannelDocumentDto>>(lista);
    }
}

/// <summary>
/// <c>POST /tenants/{t}/people/{p}/documents</c>: emite el documento en modo descarga con <see cref="IssueLendingDocumentCommand"/> (marcado
/// <c>PorElCanal</c>) y devuelve el PDF. La descarga queda auditada (<c>Channel.Document.Downloaded</c>) además de la emisión de Cartera.
/// </summary>
public sealed record IssueChannelDocumentCommand(Guid PersonPublicId, string Kind, Guid? ProductPublicId, short? Year, DateOnly? From, DateOnly? To)
    : IRequest<Result<ChannelDocumentFileDto>>;

public sealed class IssueChannelDocumentCommandValidator : AbstractValidator<IssueChannelDocumentCommand>
{
    public IssueChannelDocumentCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(40);
    }
}

public sealed class IssueChannelDocumentCommandHandler(IApplicationDbContext db, ISender sender, IBlobStore store, IDateTimeService reloj, AuditoriaDelCanal auditoria)
    : IRequestHandler<IssueChannelDocumentCommand, Result<ChannelDocumentFileDto>>
{
    /// <summary>Sin período, el extracto cubre los últimos tres meses.</summary>
    public const int MesesPorDefecto = 3;

    public async Task<Result<ChannelDocumentFileDto>> Handle(IssueChannelDocumentCommand request, CancellationToken ct)
    {
        var disponibles = await sender.Send(new ListChannelDocumentsQuery(request.PersonPublicId), ct);
        if (disponibles.IsFailure) return Result.Failure<ChannelDocumentFileDto>(disponibles.Error);
        var publicado = disponibles.Value.FirstOrDefault(d => string.Equals(d.Kind, request.Kind, StringComparison.Ordinal) && d.ProductPublicId == request.ProductPublicId);
        if (publicado is null || !Enum.TryParse<IssuedDocumentKind>(publicado.Kind, out var clase))
            return Result.Failure<ChannelDocumentFileDto>(ErroresDeDocumentosDelCanal.NoEstaDisponible());

        var hoy = reloj.HoyLocal;
        PeriodoDeDocumento? periodo = publicado.Parameter == ListChannelDocumentsQueryHandler.Periodo
            ? new PeriodoDeDocumento(request.From ?? hoy.AddMonths(-MesesPorDefecto), request.To ?? hoy)
            : null;
        var emitido = await sender.Send(new IssueLendingDocumentCommand(clase, request.PersonPublicId,
            clase == IssuedDocumentKind.Statement ? request.ProductPublicId : null, null, periodo,
            publicado.Parameter == ListChannelDocumentsQueryHandler.Año ? request.Year : null, DocumentDeliveryMode.Download)
        {
            AccountPublicId = clase == IssuedDocumentKind.AccountStatement ? request.ProductPublicId : null,
            PorElCanal = true,
            OperationKey = Guid.NewGuid(),
        }, ct);
        if (emitido.IsFailure) return Result.Failure<ChannelDocumentFileDto>(emitido.Error);

        var documento = await db.IssuedLendingDocuments.AsNoTracking().FirstAsync(d => d.PublicId == emitido.Value.DocumentPublicId, ct);
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == documento.PdfAttachmentPublicId, ct);
        if (adjunto is null) return Result.Failure<ChannelDocumentFileDto>(ErroresDeDocumentosDelCanal.NoEstaDisponible());
        await using var flujo = await store.GetAsync(new BlobReference(adjunto.StoragePath), ct);
        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, ct);

        await auditoria.LecturaAsync(AuditoriaDelCanal.EventoDocumentoDescargado, "IssuedLendingDocument", documento.PublicId,
            new { kind = publicado.Kind, number = emitido.Value.Number, product = request.ProductPublicId, copy = emitido.Value.AlreadyIssued }, ct);
        return Result.Success(new ChannelDocumentFileDto(memoria.ToArray(), $"{publicado.Kind}-{emitido.Value.Number}.pdf", emitido.Value.Number));
    }
}
