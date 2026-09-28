using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>Un documento soporte semanal propuesto: su borrador, el proveedor y las recepciones que reúne. (nuevo)</summary>
public sealed record DocumentoSoporteSemanalDto(Guid SupportDocumentPublicId, Guid SupplierPersonPublicId, IReadOnlyList<Guid> ReceiptPublicIds);

/// <summary>
/// La generación semanal del documento soporte (feature 012, I4, T742; Res. 167/2021 art. 10 par. 2; contracts/dian.md §14.2): con
/// <c>DocumentoSoporte.Generacion = Semanal</c>, reúne por proveedor no obligado a facturar las recepciones confirmadas de la semana (lunes a
/// <see cref="Hasta"/>) que todavía no tienen factura ni documento soporte, y deja <b>un borrador</b> de documento soporte por proveedor con sus
/// líneas enlazadas (<c>InvoiceOfReceipt</c>). Lo confirma una persona (numera con la resolución y se emite). Con <c>PorOperacion</c> no hace
/// nada: cada recepción propone el suyo (<see cref="PropuestaDeDocumentoSoporte"/>). (nuevo)
/// </summary>
public sealed record GenerateWeeklySupportDocumentsCommand(DateOnly? Hasta = null)
    : IRequest<Result<IReadOnlyList<DocumentoSoporteSemanalDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateWeeklySupportDocumentsCommandHandler(
    IApplicationDbContext db,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    SaveInventoryDraftCommandHandler guardar)
    : IRequestHandler<GenerateWeeklySupportDocumentsCommand, Result<IReadOnlyList<DocumentoSoporteSemanalDto>>>
{
    public Task<Result<IReadOnlyList<DocumentoSoporteSemanalDto>>> Handle(GenerateWeeklySupportDocumentsCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => GenerarAsync(request, ct), ct);

    private async Task<Result<IReadOnlyList<DocumentoSoporteSemanalDto>>> GenerarAsync(GenerateWeeklySupportDocumentsCommand request, CancellationToken ct)
    {
        var hasta = request.Hasta ?? reloj.HoyLocal;
        var leido = await parametros.LeerComoAsync<string>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.DocumentoSoporteGeneracion, hasta, ct: ct);
        if (!leido.IsSuccess || !string.Equals(leido.Value, PropuestaDeDocumentoSoporte.GeneracionSemanal, StringComparison.OrdinalIgnoreCase))
            return Result.Success<IReadOnlyList<DocumentoSoporteSemanalDto>>([]);

        var tipo = await db.InventoryDocumentTypes.AsNoTracking()
            .Where(t => t.Class == DocumentClass.SupportDocument && t.IsActive && !t.IsContingency).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return Result.Failure<IReadOnlyList<DocumentoSoporteSemanalDto>>(InventoryErrors.DocumentClassNotAvailable(DocumentClass.SupportDocument));

        var desde = hasta.AddDays(-(((int)hasta.DayOfWeek + 6) % 7));
        var noObligados = db.People.AsNoTracking().Where(p => !p.IsObligatedToInvoice).Select(p => p.Id);
        var facturadas = db.DocumentLinks.AsNoTracking()
            .Where(l => l.Kind == DocumentLinkKind.InvoiceOfReceipt && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => new { l.SourceDocumentId, d.Status })
            .Where(x => x.Status != DocumentStatus.Discarded)
            .Select(x => x.SourceDocumentId);
        var recepciones = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.PurchaseReceipt && d.Status == DocumentStatus.Confirmed
                        && d.OperationDate >= desde && d.OperationDate <= hasta
                        && d.CounterpartyPersonId != null && noObligados.Contains(d.CounterpartyPersonId.Value)
                        && !facturadas.Contains(d.Id))
            .OrderBy(d => d.OperationDate).ThenBy(d => d.Id)
            .Select(d => new { d.Id, d.PublicId, Proveedor = d.CounterpartyPersonId!.Value, d.OperationMunicipalityDaneCode })
            .ToListAsync(ct);

        var propuestos = new List<DocumentoSoporteSemanalDto>();
        foreach (var grupo in recepciones.GroupBy(r => r.Proveedor))
        {
            var ids = grupo.Select(r => r.Id).ToList();
            var lineas = await db.InventoryDocumentLines.AsNoTracking()
                .Where(l => ids.Contains(l.DocumentId) && !l.IsDeleted).OrderBy(l => l.DocumentId).ThenBy(l => l.LineNumber)
                .Select(l => new { l.PublicId, l.Quantity, l.UnitPrice, l.DiscountAmount, l.ProductId, l.UnitId })
                .ToListAsync(ct);
            var productos = await db.Products.AsNoTracking().Where(p => lineas.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
            var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => lineas.Select(l => l.UnitId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.PublicId, ct);
            var proveedor = await db.People.AsNoTracking().Where(p => p.Id == grupo.Key).Select(p => p.PublicId).FirstAsync(ct);

            var pedido = new SaveInventoryDraftRequest(tipo.PublicId, hasta, null, null, null, proveedor, null, null, null,
                $"Documento soporte semanal del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}.", null, null, null,
                lineas.Select(l => new SaveInventoryDraftLine(null, productos[l.ProductId], unidades[l.UnitId], l.Quantity, l.UnitPrice,
                    DiscountAmount: l.DiscountAmount > 0m ? l.DiscountAmount : null, ReceiptLinePublicId: l.PublicId)).ToList(),
                OperationMunicipalityDaneCode: grupo.First().OperationMunicipalityDaneCode);
            var r = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, pedido), ct);
            if (r.IsFailure) return Result.Failure<IReadOnlyList<DocumentoSoporteSemanalDto>>(r.Error);
            propuestos.Add(new DocumentoSoporteSemanalDto(r.Value.PublicId, proveedor, grupo.Select(x => x.PublicId).ToList()));
        }
        return Result.Success<IReadOnlyList<DocumentoSoporteSemanalDto>>(propuestos);
    }
}
