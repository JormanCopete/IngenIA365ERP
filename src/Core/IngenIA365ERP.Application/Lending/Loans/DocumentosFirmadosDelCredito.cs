using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>Un documento firmado del crédito que se puede volver a imprimir: su clase, el nombre y el adjunto que lo guarda.</summary>
public sealed record DocumentoFirmadoDelCreditoDto(string Clase, string Nombre, Guid AttachmentPublicId, string? Archivo, DateOnly? Fecha);

/// <summary>
/// <c>GET /api/lending/loans/{id}/signed-documents</c> (2026-11-16, pedido del dueño; <c>Lending.Loans.Read</c>): los documentos firmados del crédito
/// para reimprimirlos desde el estado de cuenta —la copia del pagaré y la carta de instrucciones que se adjuntaron al registrarlo, y los soportes de la
/// solicitud (allí va la libranza o autorización de descuento firmada)—. Reimprimir es descargar la copia firmada (decisión del dueño del 2026-11-16);
/// desde el 2026-11-18 el sistema genera los documentos para firmar en la solicitud aprobada (<c>GenerateSignatureDocumentsCommand</c>). Sólo lista; bajar cada adjunto pasa por <c>/api/attachments/{id}/download-link</c>, que aplica el permiso del
/// dueño de cada uno. Fuera del alcance de oficina o línea, el mismo 404 que el crédito. (nuevo)
/// </summary>
public sealed record GetLoanSignedDocumentsQuery(Guid LoanPublicId) : IRequest<Result<IReadOnlyList<DocumentoFirmadoDelCreditoDto>>>;

public sealed class GetLoanSignedDocumentsQueryValidator : AbstractValidator<GetLoanSignedDocumentsQuery>
{
    public GetLoanSignedDocumentsQueryValidator() => RuleFor(x => x.LoanPublicId).NotEmpty();
}

public sealed class GetLoanSignedDocumentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetLoanSignedDocumentsQuery, Result<IReadOnlyList<DocumentoFirmadoDelCreditoDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentoFirmadoDelCreditoDto>>> Handle(GetLoanSignedDocumentsQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<IReadOnlyList<DocumentoFirmadoDelCreditoDto>>(leido.Error);
        var credito = leido.Value;

        var documentos = new List<DocumentoFirmadoDelCreditoDto>();
        var pagares = await db.PromissoryNotes.AsNoTracking()
            .Where(p => !p.IsDeleted && (p.LoanId == credito.Id || (credito.ApplicationId != null && p.ApplicationId == credito.ApplicationId)))
            .OrderByDescending(p => p.IssuedOn)
            .Select(p => new { p.Number, p.IssuedOn, p.Status, p.DocumentAttachmentPublicId, p.InstructionsAttachmentPublicId })
            .ToListAsync(ct);
        foreach (var p in pagares)
        {
            var anulado = p.Status == PromissoryNoteStatus.Voided ? " (anulado)" : "";
            if (p.DocumentAttachmentPublicId is { } copia)
                documentos.Add(new("Pagaré", $"Pagaré {p.Number}{anulado}".Trim(), copia, null, p.IssuedOn));
            if (p.InstructionsAttachmentPublicId is { } carta)
                documentos.Add(new("Carta de instrucciones", $"Carta de instrucciones del pagaré {p.Number}{anulado}".Trim(), carta, null, p.IssuedOn));
        }

        if (credito.ApplicationId is { } solicitud)
        {
            var soportes = await (
                    from d in db.CreditApplicationDocuments.AsNoTracking()
                    where d.ApplicationId == solicitud && !d.IsDeleted && d.RejectionReason == null
                    join r in db.CreditLineDocumentRequirements.AsNoTracking() on d.RequirementId equals r.Id into requisitos
                    from r in requisitos.DefaultIfEmpty()
                    orderby r == null ? int.MaxValue : r.Order
                    select new { Requisito = r == null ? null : r.Name, d.AttachmentPublicId, d.DocumentDate })
                .ToListAsync(ct);
            foreach (var s in soportes)
            {
                var nombre = s.Requisito ?? "Documento adicional de la solicitud";
                documentos.Add(new(EsLibranza(nombre) ? "Libranza" : "Soporte de la solicitud", nombre, s.AttachmentPublicId, null, s.DocumentDate));
            }
        }

        // El nombre del archivo, para que la persona sepa qué va a bajar; un adjunto que ya no está disponible no se ofrece.
        var ids = documentos.Select(d => d.AttachmentPublicId).ToList();
        var archivos = await db.Attachments.AsNoTracking().Where(a => ids.Contains(a.PublicId) && !a.IsDeleted && a.Status == EstadoDeAdjunto.Available)
            .ToDictionaryAsync(a => a.PublicId, a => a.FileName, ct);
        return Result.Success<IReadOnlyList<DocumentoFirmadoDelCreditoDto>>(documentos
            .Where(d => archivos.ContainsKey(d.AttachmentPublicId))
            .Select(d => d with { Archivo = archivos[d.AttachmentPublicId] })
            .ToList());
    }

    /// <summary>Un requisito cuyo nombre habla de libranza o de autorización de descuento: se rotula «Libranza» para encontrarlo rápido.</summary>
    public static bool EsLibranza(string nombre) =>
        nombre.Contains("libranza", StringComparison.OrdinalIgnoreCase) || nombre.Contains("autorización de descuento", StringComparison.OrdinalIgnoreCase)
        || nombre.Contains("autorizacion de descuento", StringComparison.OrdinalIgnoreCase);
}
