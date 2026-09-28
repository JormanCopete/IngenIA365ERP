using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

/// <summary>
/// La aprobación de los descuentos sobre el tope (feature 012, I3, T602; contracts/api.md §19.3; FR-054, T33, T51; data-model §14
/// <c>INV_DocumentLineDiscounts</c>). Un descuento sobre el tope no se rechaza: queda <c>RequiresApproval</c> y este servicio pide
/// su solicitud al motor (<see cref="IMotorDeAprobaciones.SolicitarAsync"/>) con <c>Subject = DiscountOverCap</c>,
/// <c>SourceType = DocumentLineDiscount</c> y la huella de (línea, producto, cantidad, <c>ListPrice</c>, <c>UnitPrice</c>,
/// <c>Amount</c>). Sin política registrada rige la regla fija del motor: un nivel con <c>Inventory.Discounts.Authorize</c>
/// (<c>EvaluadorDePolitica.ReglaFija</c>; el dueño confirma que no se exige registrar política, data-model §27 duda 10).
/// <list type="bullet">
/// <item>si la línea cambia, la huella deja de coincidir: la solicitud pendiente se invalida (<c>Cancelled</c>) y se pide otra;</item>
/// <item>quién aprueba: permiso de nivel, alcance sobre el punto y que no sea quien pidió (el motor), <b>y un tope propio
/// suficiente</b> (<see cref="FuenteDeAprobacionDeDescuento"/>, <c>Inventory.Discount.CapInsufficient</c>);</item>
/// <item>al aprobar, la fila copia aprobador, método (<c>OwnSession</c>, <c>InPersonPasskey</c>, <c>InPersonTotp</c>) y la
/// solicitud;</item>
/// <item>cobrar o confirmar con un descuento sin aprobación vigente → <c>Inventory.Discount.ApprovalPending</c> con las líneas
/// (<see cref="ExigirAprobadosAsync"/>).</item>
/// </list>
/// Nunca guarda: la solicitud se guarda con el documento que la pide. (nuevo)
/// </summary>
public sealed class AprobacionDeDescuentos(IApplicationDbContext db, IMotorDeAprobaciones motor, IActorActual actorActual)
{
    /// <summary>La huella de lo que se aprueba de un descuento (T33).</summary>
    public static string Huella(InventoryDocumentLine linea, DocumentLineDiscount descuento) => HuellaDeOperacion.Calcular("DocumentLineDiscount", new
    {
        Linea = linea.PublicId,
        linea.LineNumber,
        linea.ProductId,
        linea.UnitId,
        linea.Quantity,
        linea.ListPrice,
        linea.UnitPrice,
        descuento.Sequence,
        descuento.Amount,
    });

    /// <summary>
    /// Pide (o conserva) la aprobación de cada descuento que la exige y retira la de los que ya no. Con una solicitud pendiente de
    /// la misma huella no pide otra; con otra huella la invalida y pide una nueva. Devuelve las solicitudes nuevas.
    /// </summary>
    public async Task<Result<IReadOnlyList<ApprovalRequest>>> SolicitarAsync(
        InventoryDocument documento,
        IReadOnlyCollection<(InventoryDocumentLine Linea, DocumentLineDiscount Descuento)> descuentos,
        Guid? puntoPublicId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } solicitante)
            throw new InvalidOperationException("Pedir la aprobación de un descuento exige una persona resuelta en SEC_Users.");

        var tipo = documento.DocumentType?.PublicId
            ?? await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Id == documento.DocumentTypeId).Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync(ct);
        var ids = descuentos.Select(d => d.Descuento.PublicId).ToList();
        var pendientes = await db.ApprovalRequests
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentLineDiscount && r.Subject == ApprovalSubjects.DiscountOverCap
                && ids.Contains(r.SourcePublicId) && r.Status == ApprovalRequestStatus.Pending)
            .ToListAsync(ct);

        var nuevas = new List<ApprovalRequest>();
        foreach (var (linea, descuento) in descuentos)
        {
            var huella = Huella(linea, descuento);
            var pendiente = pendientes.FirstOrDefault(p => p.SourcePublicId == descuento.PublicId);
            if (!descuento.RequiresApproval || descuento.IsDeleted)
            {
                if (pendiente is not null) await motor.InvalidarAsync(ApprovalSourceTypes.DocumentLineDiscount, descuento.PublicId, ApprovalSubjects.DiscountOverCap, ct);
                continue;
            }
            if (pendiente is not null && string.Equals(pendiente.ContentSha256, huella, StringComparison.OrdinalIgnoreCase)) continue;
            if (pendiente is not null) await motor.InvalidarAsync(ApprovalSourceTypes.DocumentLineDiscount, descuento.PublicId, ApprovalSubjects.DiscountOverCap, ct);

            var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                ApprovalSubjects.DiscountOverCap,
                ApprovalSourceTypes.DocumentLineDiscount,
                descuento.PublicId,
                Etiqueta(documento, linea),
                tipo,
                null,
                puntoPublicId,
                descuento.Amount,
                documento.OperationDate,
                documento.CreatedByUserId != 0 ? documento.CreatedByUserId : solicitante,
                [],
                huella,
                null), ct);
            if (solicitada.IsFailure) return Result.Failure<IReadOnlyList<ApprovalRequest>>(solicitada.Error);
            if (solicitada.Value is { } solicitud) nuevas.Add(solicitud);
        }
        return Result.Success<IReadOnlyList<ApprovalRequest>>(nuevas);
    }

    /// <summary>
    /// Los números de línea con un descuento que exige aprobación y no tiene una aprobada con su huella vigente. Vacío = se puede
    /// cobrar o confirmar.
    /// </summary>
    public async Task<IReadOnlyList<int>> LineasSinAprobarAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var descuentos = await db.DocumentLineDiscounts.AsNoTracking()
            .Where(d => d.DocumentId == documento.Id && !d.IsDeleted && d.RequiresApproval).ToListAsync(ct);
        descuentos = descuentos.Where(d => vivas.ContainsKey(d.DocumentLineId)).ToList();
        if (descuentos.Count == 0) return [];

        var ids = descuentos.Select(d => d.PublicId).ToList();
        var aprobadas = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentLineDiscount && r.Subject == ApprovalSubjects.DiscountOverCap
                && ids.Contains(r.SourcePublicId) && r.Status == ApprovalRequestStatus.Approved)
            .Select(r => new { r.SourcePublicId, r.ContentSha256 })
            .ToListAsync(ct);

        return descuentos
            .Where(d => !aprobadas.Any(a => a.SourcePublicId == d.PublicId
                && string.Equals(a.ContentSha256, Huella(vivas[d.DocumentLineId], d), StringComparison.OrdinalIgnoreCase)))
            .Select(d => vivas[d.DocumentLineId].LineNumber)
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>Falla con <c>Inventory.Discount.ApprovalPending</c> si alguna línea tiene un descuento sin aprobación vigente.</summary>
    public async Task<Result> ExigirAprobadosAsync(InventoryDocument documento, CancellationToken ct)
    {
        var lineas = await LineasSinAprobarAsync(documento, ct);
        return lineas.Count == 0 ? Result.Success() : Result.Failure(ErroresDePrecios.ApprovalPending(lineas));
    }

    /// <summary>
    /// Anota en cada descuento la solicitud pendiente que lo cubre (<c>ApprovalRequestId</c>), después del guardado que les dio
    /// Id (el descuento no tiene navegación a la solicitud). La aprobada la anota la fuente al aprobar.
    /// </summary>
    public async Task EnlazarSolicitudesAsync(IReadOnlyCollection<DocumentLineDiscount> descuentos, CancellationToken ct)
    {
        var ids = descuentos.Where(d => d.RequiresApproval).Select(d => d.PublicId).ToList();
        if (ids.Count == 0) return;
        var solicitudes = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentLineDiscount && ids.Contains(r.SourcePublicId) && r.Status == ApprovalRequestStatus.Pending)
            .Select(r => new { r.SourcePublicId, r.Id }).ToListAsync(ct);
        foreach (var d in descuentos)
            if (solicitudes.FirstOrDefault(s => s.SourcePublicId == d.PublicId) is { } s) d.ApprovalRequestId = s.Id;
    }

    private static string Etiqueta(InventoryDocument documento, InventoryDocumentLine linea)
    {
        var numero = documento.Number is null ? "Borrador" : VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number);
        var texto = $"{numero} · línea {linea.LineNumber}";
        return texto.Length > 80 ? texto[..80] : texto;
    }
}

/// <summary>
/// La fuente de aprobación de los descuentos sobre el tope (feature 012, I3, T602; <c>SourceType = DocumentLineDiscount</c>). Aprobar
/// no confirma nada —la venta se cobra después—: exige que quien aprueba tenga un tope suficiente para la fracción pedida (por
/// línea o por total), y en el último nivel copia a la fila la solicitud, el aprobador y el método. El rechazo deja el descuento
/// como está: la venta no se cobra hasta quitarlo o pedir otra aprobación. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeDescuento(IApplicationDbContext db) : IFuenteDeAprobacion, IFuenteConAprobador
{
    public string SourceType => ApprovalSourceTypes.DocumentLineDiscount;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        var descuento = await db.DocumentLineDiscounts.FirstOrDefaultAsync(d => d.PublicId == solicitud.SourcePublicId, ct);
        if (descuento is null) return Result.Failure(Error.NotFound);
        var linea = await db.InventoryDocumentLines.AsNoTracking().FirstAsync(l => l.Id == descuento.DocumentLineId, ct);

        var pedida = await FraccionPedidaAsync(descuento, linea, ct);
        var tope = await TopesDeDescuento.DelUsuarioAsync(db, aprobadorUserId, solicitud.OperationDate, ct);
        var suyo = descuento.FromDocumentDiscount ? tope.MaxDocumentRate : tope.MaxLineRate;
        if (suyo < pedida) return Result.Failure(ErroresDePrecios.CapInsufficient(pedida, suyo));

        if (esElUltimo)
        {
            descuento.ApprovalRequestId = solicitud.Id;
            descuento.ApprovedByUserId = aprobadorUserId;
            descuento.ApprovalMethod = metodo;
        }
        return Result.Success();
    }

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var descuento = await db.DocumentLineDiscounts.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == solicitud.SourcePublicId, ct);
        return descuento is null
            ? Result.Failure<EstadoDeFuenteDto>(Error.NotFound)
            : Result.Success(new EstadoDeFuenteDto(descuento.PublicId, SourceType, ApprovalRequestStatus.Approved.ToString(), solicitud.SourceLabel));
    }

    public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, "RequiresApproval", solicitud.SourceLabel)));

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        var documento = await (from d in db.DocumentLineDiscounts.AsNoTracking()
                               join doc in db.InventoryDocuments.AsNoTracking() on d.DocumentId equals doc.Id
                               where d.PublicId == solicitud.SourcePublicId
                               select doc).FirstOrDefaultAsync(ct);
        return documento is not null && FiltroDeAlcance.DocumentoVisible(alcance, documento, []);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await (from d in db.DocumentLineDiscounts.AsNoTracking()
                           join l in db.InventoryDocumentLines.AsNoTracking() on d.DocumentLineId equals l.Id
                           join doc in db.InventoryDocuments.AsNoTracking() on d.DocumentId equals doc.Id
                           join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                           where sourcePublicIds.Contains(d.PublicId)
                           select new { d.PublicId, d.Amount, d.Rate, d.IsPriceOverride, d.FromDocumentDiscount, l.LineNumber, ProductCode = p.Code, ProductName = p.Name,
                               doc.Class, doc.Prefix, doc.Number, doc.OperationDate, doc.PointOfSaleId })
            .ToListAsync(ct);
        var puntoIds = filas.Select(f => f.PointOfSaleId).OfType<int>().Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().Where(p => puntoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        return filas.ToDictionary(f => f.PublicId, f => new OrigenDeAprobacionDto(
            f.PublicId,
            f.Class.ToString(),
            null,
            f.Number is null ? null : VistaDeDocumentos.NumeroVisible(f.Prefix, f.Number),
            f.OperationDate,
            null,
            f.PointOfSaleId is int punto ? puntos.GetValueOrDefault(punto) : null,
            $"Descuento {(f.FromDocumentDiscount ? "por total" : f.IsPriceOverride ? "por precio digitado" : "manual")} de {f.Amount:N2} en la línea {f.LineNumber} ({f.ProductCode} {f.ProductName})",
            null));
    }

    /// <summary>La fracción que se pide aprobar: la del total, o la suma de los descuentos manuales de la línea sobre su bruto.</summary>
    private async Task<decimal> FraccionPedidaAsync(DocumentLineDiscount descuento, InventoryDocumentLine linea, CancellationToken ct)
    {
        if (descuento.FromDocumentDiscount) return descuento.Rate ?? 0m;
        var manuales = await db.DocumentLineDiscounts.AsNoTracking()
            .Where(d => d.DocumentLineId == linea.Id && !d.IsDeleted && !d.FromDocumentDiscount).SumAsync(d => d.Amount, ct);
        return linea.GrossAmount > 0m ? manuales / linea.GrossAmount : 0m;
    }
}
