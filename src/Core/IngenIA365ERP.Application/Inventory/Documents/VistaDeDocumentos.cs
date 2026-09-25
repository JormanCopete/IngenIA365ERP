using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Lo que comparten los comandos y las consultas del ciclo común para leer un documento (feature 012, T143, T148):
/// buscarlo con su alcance y su grupo (fuera = el mismo 404 que inexistente, contracts/api.md §2.2), saber su grupo
/// (la anulación toma el de su original), qué grupos ve quien pregunta, y armar <see cref="DocumentSummaryDto"/> e
/// <see cref="InventoryDocumentDto"/> con lo que su permiso deja ver —costos sólo con <c>Inventory.Costs.Read</c>,
/// mensajes sólo con <c>Inventory.Messages.View</c>— y <c>allowedActions</c> calculado aquí, nunca en la pantalla.
/// Aplica <see cref="IAlcanceDeInventario"/> por <see cref="FiltroDeAlcance"/>. (nuevo)
/// </summary>
public sealed class VistaDeDocumentos(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IPermissionChecker permisos,
    IAlcanceDeInventario alcanceDeLaPeticion)
{
    public const string AccionEditar = "Edit";
    public const string AccionDescartar = "Discard";
    public const string AccionConfirmar = "Confirm";
    public const string AccionAnular = "Void";

    private readonly Dictionary<string, bool> _permisos = new(StringComparer.Ordinal);

    /// <summary>¿Tiene quien pregunta este permiso? (memorizado por petición).</summary>
    public async Task<bool> TieneAsync(string? permiso, CancellationToken ct)
    {
        if (permiso is null) return false;
        if (_permisos.TryGetValue(permiso, out var tiene)) return tiene;
        tiene = await permisos.HasPermissionAsync(permiso, ct);
        _permisos[permiso] = tiene;
        return tiene;
    }

    /// <summary>El grupo del documento: el de su clase, o el del original si es una anulación.</summary>
    public static DocumentClassGroup GrupoDe(DocumentClass clase, DocumentClass? claseDelOriginal) =>
        ClasesDeDocumento.GrupoDe(clase, clase == DocumentClass.Voiding ? claseDelOriginal : null);

    /// <summary>La clase del original de una anulación (nula si el documento no es una).</summary>
    public async Task<DocumentClass?> ClaseDelOriginalAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.Class != DocumentClass.Voiding || documento.VoidsDocumentId is not int original) return null;
        return await db.InventoryDocuments.AsNoTracking().Where(d => d.Id == original).Select(d => (DocumentClass?)d.Class).FirstOrDefaultAsync(ct);
    }

    /// <summary>¿Puede ver documentos de este grupo? (<c>Costing</c> exige además <c>Inventory.Documents.View</c>).</summary>
    public async Task<bool> VeGrupoAsync(DocumentClassGroup grupo, CancellationToken ct)
    {
        var ver = await TieneAsync(PermisosDeGrupo.De(grupo).View, ct);
        return grupo == DocumentClassGroup.Costing ? ver && await TieneAsync(PermisosDeGrupo.VerDocumentos, ct) : ver;
    }

    /// <summary>
    /// El documento <paramref name="publicId"/> con líneas y tipo, sólo si existe, está en el alcance de quien pregunta y
    /// es de <paramref name="grupoEsperado"/> (nulo = cualquiera). Si no, nulo: el llamador responde
    /// <c>Inventory.Document.NotFound</c> igual en los tres casos.
    /// </summary>
    public async Task<InventoryDocument?> BuscarAsync(Guid publicId, DocumentClassGroup? grupoEsperado, bool seguir, CancellationToken ct)
    {
        var consulta = db.InventoryDocuments.Include(d => d.Lines).Include(d => d.DocumentType!).ThenInclude(t => t.Warehouses).AsQueryable();
        if (!seguir) consulta = consulta.AsNoTracking();
        var documento = await consulta.FirstOrDefaultAsync(d => d.PublicId == publicId, ct);
        if (documento is null) return null;

        if (grupoEsperado is { } esperado && GrupoDe(documento.Class, await ClaseDelOriginalAsync(documento, ct)) != esperado) return null;

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var origenes = documento.WarehouseId is null && documento.DestinationWarehouseId is null
            ? await FiltroDeAlcance.BodegasDeSusOrigenesAsync(db, documento.Id, ct)
            : [];
        return FiltroDeAlcance.DocumentoVisible(alcance, documento, origenes) ? documento : null;
    }

    /// <summary>¿Puede operar (editar, confirmar, anular) el documento, además de verlo? Por su bodega de origen o, sin bodega, por todos sus orígenes.</summary>
    private async Task<bool> OperaAsync(InventoryDocument documento, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (alcance.TodasLasBodegas) return true;
        if (documento.WarehouseId is int origen) return alcance.IncluyeBodega(origen);
        if (documento.DestinationWarehouseId is int destino) return alcance.IncluyeBodega(destino);
        return FiltroDeAlcance.DocumentoSinBodegaOperable(alcance, await FiltroDeAlcance.BodegasDeSusOrigenesAsync(db, documento.Id, ct));
    }

    /// <summary>
    /// Las acciones que el servidor permite (§9.2): en borrador, editar y descartar con <c>Create</c> y confirmar con
    /// <c>Confirm</c> del grupo; confirmado, anular con <c>Void</c> salvo una anulación, un documento ya anulado o un fiscal
    /// emitido (se corrige con su nota). Fuera del alcance operable, ninguna.
    /// </summary>
    public async Task<IReadOnlyList<string>> AccionesAsync(InventoryDocument documento, DocumentClassGroup grupo, CancellationToken ct)
    {
        if (!await OperaAsync(documento, ct)) return [];
        var p = PermisosDeGrupo.De(grupo);
        var acciones = new List<string>();
        switch (documento.Status)
        {
            case DocumentStatus.Draft:
                if (await TieneAsync(p.Create, ct)) { acciones.Add(AccionEditar); acciones.Add(AccionDescartar); }
                if (await TieneAsync(p.Confirm, ct) && ClasesDeDocumento.De(documento.Class).ManualCreation) acciones.Add(AccionConfirmar);
                break;
            case DocumentStatus.Confirmed:
                var clase = ClasesDeDocumento.De(documento.Class);
                if (documento.Class != DocumentClass.Voiding && documento.VoidedByDocumentId is null
                    && clase.FiscalDirection != FiscalDirection.Emitted && await TieneAsync(p.Void, ct))
                {
                    acciones.Add(AccionAnular);
                }
                break;
        }
        return acciones;
    }

    public static string? NumeroVisible(string prefijo, long? numero) => numero is null ? null : $"{prefijo}{numero}";

    // --------------------------------------------------------------------------------------------- lista --

    /// <summary>Los resúmenes de <paramref name="documentos"/>, en el mismo orden.</summary>
    public async Task<IReadOnlyList<DocumentSummaryDto>> ResumenesAsync(IReadOnlyList<InventoryDocument> documentos, CancellationToken ct)
    {
        if (documentos.Count == 0) return [];
        var costos = await TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var ids = documentos.Select(d => d.Id).ToList();
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().IgnoreQueryFilters()
            .Where(t => documentos.Select(d => d.DocumentTypeId).Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);
        var referidos = documentos.SelectMany(d => new[] { d.VoidsDocumentId, d.VoidedByDocumentId }).OfType<int>().Distinct().ToList();
        var otros = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => referidos.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number, d.Class }).ToDictionaryAsync(d => d.Id, ct);
        var bodegas = (await maestros.BodegasPorIdAsync(
                documentos.SelectMany(d => new[] { d.WarehouseId, d.DestinationWarehouseId }).OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(b => b.Id);
        var usuarios = await UsuariosAsync(documentos.SelectMany(d => new[] { (int?)d.CreatedByUserId, d.ConfirmedByUserId }).OfType<int>(), ct);
        var personas = await PersonasAsync(documentos.Select(d => d.CounterpartyPersonId).OfType<int>(), ct);
        var pendientes = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.InventoryDocument && r.Status == ApprovalRequestStatus.Pending
                        && documentos.Select(d => d.PublicId).Contains(r.SourcePublicId))
            .Select(r => new { r.SourcePublicId, r.PublicId, r.CurrentLevel })
            .ToListAsync(ct);

        return documentos.Select(d =>
        {
            var tipo = tipos.GetValueOrDefault(d.DocumentTypeId);
            var claseOriginal = d.VoidsDocumentId is int v && otros.TryGetValue(v, out var o) ? o.Class : (DocumentClass?)null;
            var pendiente = pendientes.FirstOrDefault(p => p.SourcePublicId == d.PublicId);
            return new DocumentSummaryDto(
                d.PublicId, d.Class, GrupoSeguro(d.Class, claseOriginal),
                tipo is null ? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty) : new ReferenciaDto(tipo.PublicId, tipo.Code, tipo.Name),
                d.Prefix, d.Number, NumeroVisible(d.Prefix, d.Number), d.Status, d.OperationDate,
                Bodega(d.WarehouseId), Bodega(d.DestinationWarehouseId),
                d.CounterpartyPersonId is int pid ? personas.GetValueOrDefault(pid) : null,
                d.Total, costos ? d.CostTotal : null,
                usuarios.GetValueOrDefault(d.CreatedByUserId) ?? new UsuarioDto(null, d.CreatedBy ?? string.Empty),
                d.CreatedAt, d.ConfirmedAt,
                d.ConfirmedByUserId is int c ? usuarios.GetValueOrDefault(c) : null,
                d.PostingMode,
                Referido(d.VoidsDocumentId), Referido(d.VoidedByDocumentId),
                pendiente is null ? null : new AprobacionPendienteDto(pendiente.PublicId, pendiente.CurrentLevel));
        }).ToList();

        ReferenciaDto? Bodega(int? id) => id is int b && bodegas.TryGetValue(b, out var x) ? new ReferenciaDto(x.PublicId, x.Code, x.Name) : null;
        DocumentoReferidoDto? Referido(int? id) => id is int r && otros.TryGetValue(r, out var x) ? new DocumentoReferidoDto(x.PublicId, NumeroVisible(x.Prefix, x.Number)) : null;
    }

    // ------------------------------------------------------------------------------------------- detalle --

    /// <summary>El detalle completo de <paramref name="documento"/> (cargado con líneas y tipo).</summary>
    public async Task<InventoryDocumentDto> DetalleAsync(InventoryDocument documento, IReadOnlyList<AvisoDto> avisos, CancellationToken ct)
    {
        var costos = await TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var claseOriginal = await ClaseDelOriginalAsync(documento, ct);
        var grupo = GrupoSeguro(documento.Class, claseOriginal);
        var tipo = documento.DocumentType
            ?? await db.InventoryDocumentTypes.AsNoTracking().IgnoreQueryFilters().FirstAsync(t => t.Id == documento.DocumentTypeId, ct);
        var lineas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();

        var bodegas = (await maestros.BodegasPorIdAsync(
                new[] { documento.WarehouseId, documento.DestinationWarehouseId, documento.TransitWarehouseId }.OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(b => b.Id);
        var productos = (await maestros.ProductosPorIdAsync(lineas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var unidades = (await maestros.UnidadesPorIdAsync(lineas.Select(l => l.UnitId).Distinct().ToList(), ct)).ToDictionary(u => u.Id);
        var ubicaciones = (await maestros.UbicacionesPorIdAsync(
                lineas.SelectMany(l => new[] { l.LocationId, l.ToLocationId }).OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(u => u.Id);
        var causaId = lineas.Select(l => l.AdjustmentCauseId).OfType<int>().FirstOrDefault();
        var causa = causaId == 0 ? null : (await maestros.CausasDeAjustePorIdAsync([causaId], ct)).FirstOrDefault();

        var sucursal = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == documento.BranchId)
            .Select(b => new ReferenciaDto(b.PublicId, b.LegacyCode ?? string.Empty, b.Name)).FirstOrDefaultAsync(ct)
            ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty);
        var centro = documento.CostCenterId is int cc
            ? await db.CostCenters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == cc)
                .Select(c => new ReferenciaDto(c.PublicId, c.LegacyCode ?? string.Empty, c.Name)).FirstOrDefaultAsync(ct)
            : null;
        var personas = await PersonasAsync(documento.CounterpartyPersonId is int p ? [p] : [], ct);
        var vendedor = documento.SalespersonId is int sp
            ? await db.Salespeople.AsNoTracking().IgnoreQueryFilters().Where(s => s.Id == sp)
                .Select(s => new ReferenciaDto(s.PublicId, s.Person.TaxId, s.Person.BusinessName ?? (s.Person.FirstName + " " + s.Person.LastName)))
                .FirstOrDefaultAsync(ct)
            : null;
        var usuarios = await UsuariosAsync(new[] { (int?)documento.CreatedByUserId, documento.ConfirmedByUserId }.OfType<int>(), ct);

        // Vínculos del documento con otros, en los dos sentidos.
        var vinculos = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == documento.Id || l.TargetDocumentId == documento.Id)
            .Select(l => new { l.Id, l.Kind, Otro = l.SourceDocumentId == documento.Id ? l.TargetDocumentId : l.SourceDocumentId })
            .ToListAsync(ct);
        var otrosIds = vinculos.Select(v => v.Otro)
            .Concat(new[] { documento.VoidsDocumentId, documento.VoidedByDocumentId }.OfType<int>()).Distinct().ToList();
        var otros = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => otrosIds.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Class, d.Prefix, d.Number, d.Status }).ToDictionaryAsync(d => d.Id, ct);
        var lineIds = lineas.Select(l => l.Id).ToList();
        var vinculosDeLinea = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => lineIds.Contains(x.SourceLineId) || lineIds.Contains(x.TargetLineId))
            .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x.SourceLineId, x.TargetLineId, x.QuantityBase, l.Kind, l.SourceDocumentId, l.TargetDocumentId })
            .ToListAsync(ct);
        var lineasOtras = vinculosDeLinea.SelectMany(x => new[] { x.SourceLineId, x.TargetLineId }).Where(id => !lineIds.Contains(id)).Distinct().ToList();
        var publicasDeLinea = await db.InventoryDocumentLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineasOtras.Contains(l.Id))
            .Select(l => new { l.Id, l.PublicId }).ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);

        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == documento.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var foto = await db.DocumentPartySnapshots.AsNoTracking().Where(s => s.DocumentId == documento.Id)
            .OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
        var solicitud = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .Where(r => r.SourceType == ApprovalSourceTypes.InventoryDocument && r.SourcePublicId == documento.PublicId)
            .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(ct);
        var adjuntos = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityPublicId == documento.PublicId && !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new DocumentAttachmentDto(a.PublicId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedBy, a.CreatedAt, false))
            .ToListAsync(ct);
        var mensajes = await TieneAsync(PermisosDeGrupo.VerMensajes, ct) ? await MensajesAsync(documento.PublicId, ct) : null;

        var dtoLineas = lineas.Select(l => new DocumentLineDto(
            l.PublicId, l.LineNumber,
            productos.TryGetValue(l.ProductId, out var pr) ? new ReferenciaDto(pr.PublicId, pr.Code, pr.Name) : new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
            unidades.TryGetValue(l.UnitId, out var u) ? new UnidadDto(u.PublicId, u.Code) : new UnidadDto(Guid.Empty, string.Empty),
            l.Quantity, l.Factor, l.QuantityBase, l.RoundingQuantity,
            l.UnitPrice, l.DiscountAmount, l.NetAmount,
            costos ? l.UnitCost : null, costos ? l.TotalCost : null,
            Ubicacion(l.LocationId), Ubicacion(l.ToLocationId),
            null, null, null,
            vinculosDeLinea.Where(x => x.SourceLineId == l.Id || x.TargetLineId == l.Id).Select(x =>
            {
                var esOrigen = x.SourceLineId == l.Id;
                var otroDoc = esOrigen ? x.TargetDocumentId : x.SourceDocumentId;
                var otraLinea = esOrigen ? x.TargetLineId : x.SourceLineId;
                return new VinculoDeLineaDto(x.Kind,
                    otros.TryGetValue(otroDoc, out var od) ? od.PublicId : Guid.Empty,
                    otros.TryGetValue(otroDoc, out var od2) ? NumeroVisible(od2.Prefix, od2.Number) : null,
                    publicasDeLinea.GetValueOrDefault(otraLinea), x.QuantityBase);
            }).ToList(),
            l.Description)).ToList();

        var numerosDeLinea = lineas.ToDictionary(l => l.Id, l => l.LineNumber);
        return new InventoryDocumentDto(
            documento.PublicId, documento.Class, grupo,
            new ReferenciaDto(tipo.PublicId, tipo.Code, tipo.Name),
            documento.Prefix, documento.Number, NumeroVisible(documento.Prefix, documento.Number),
            documento.Status, documento.OperationDate,
            usuarios.GetValueOrDefault(documento.CreatedByUserId) ?? new UsuarioDto(null, documento.CreatedBy ?? string.Empty),
            documento.CreatedAt, documento.ConfirmedAt,
            documento.ConfirmedByUserId is int cb ? usuarios.GetValueOrDefault(cb) : null,
            Bodega(documento.WarehouseId), Bodega(documento.DestinationWarehouseId), Bodega(documento.TransitWarehouseId),
            sucursal, centro,
            documento.CounterpartyPersonId is int cp ? personas.GetValueOrDefault(cp) : null,
            vendedor,
            documento.ExternalReference, documento.Reason,
            causa is null ? null : new ReferenciaDto(causa.PublicId, causa.Code, causa.Name),
            documento.Notes, documento.Currency, documento.ExchangeRate, documento.PostingMode,
            Referido(documento.VoidsDocumentId), Referido(documento.VoidedByDocumentId),
            documento.RowVersion,
            dtoLineas,
            vinculos.Where(v => otros.ContainsKey(v.Otro)).Select(v =>
            {
                var o = otros[v.Otro];
                return new DocumentLinkDto(v.Kind, o.PublicId, o.Class, NumeroVisible(o.Prefix, o.Number), o.Status);
            }).ToList(),
            impuestos.Select(t => new DocumentTaxLineDto(
                t.DocumentLineId is int li && numerosDeLinea.TryGetValue(li, out var n) ? n : null,
                t.Kind, t.TaxRateCode, t.Rate, t.AmountPerUnit, t.Base, t.Amount, t.Treatment, t.MunicipalityDaneCode, t.ExplanationJson)).ToList(),
            new DocumentTotalsDto(documento.Subtotal, documento.DiscountTotal, documento.TaxTotal, documento.WithholdingTotal,
                documento.Total, documento.AmountDue, costos ? documento.CostTotal : null),
            foto is null || documento.Status is not (DocumentStatus.Confirmed or DocumentStatus.Voided) ? null : new DocumentPartyDto(
                foto.Version, foto.LegalName, foto.DianIdTypeCode, foto.TaxId, foto.CheckDigit, foto.Address, foto.MunicipalityDaneCode,
                foto.DianResponsibilities, foto.IsVatResponsible, foto.IsLargeContributor, foto.IsSelfWithholder),
            solicitud is null ? null : Aprobacion(solicitud),
            mensajes,
            adjuntos,
            await AccionesAsync(documento, grupo, ct),
            avisos);

        ReferenciaDto? Bodega(int? id) => id is int b && bodegas.TryGetValue(b, out var x) ? new ReferenciaDto(x.PublicId, x.Code, x.Name) : null;
        ReferenciaDto? Ubicacion(int? id) => id is int b && ubicaciones.TryGetValue(b, out var x) ? new ReferenciaDto(x.PublicId, x.Code, x.Name) : null;
        DocumentoReferidoDto? Referido(int? id) => id is int r && otros.TryGetValue(r, out var x) ? new DocumentoReferidoDto(x.PublicId, NumeroVisible(x.Prefix, x.Number)) : null;
    }

    /// <summary>Los mensajes de integración del documento con su entrega (sólo con <c>Inventory.Messages.View</c>).</summary>
    public async Task<IReadOnlyList<DocumentMessageDto>> MensajesAsync(Guid documentoPublicId, CancellationToken ct) =>
        await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == documentoPublicId)
            .Join(db.IntegrationMessageDeliveries.AsNoTracking(), m => m.Id, e => e.MessageId,
                (m, e) => new { m.Id, m.PublicId, m.Type, e.Destination, e.Status, e.Mode })
            .OrderBy(x => x.Id)
            .Select(x => new DocumentMessageDto(x.PublicId, x.Type, x.Destination, x.Status, x.Mode))
            .ToListAsync(ct);

    private static DocumentApprovalDto Aprobacion(Domain.Entities.Approvals.ApprovalRequest solicitud)
    {
        var aprobados = solicitud.Decisions.Where(d => d.Decision == ApprovalDecisionKind.Approve).Select(d => (int)d.Level).ToHashSet();
        var rechazado = solicitud.Decisions.Where(d => d.Decision == ApprovalDecisionKind.Reject).Select(d => (int)d.Level).ToHashSet();
        var niveles = solicitud.NivelesRequeridos().Select(n => new NivelDeAprobacionDelDocumentoDto(
            n.Order, n.Threshold, n.PermissionCode,
            aprobados.Contains(n.Order) ? "Approved"
            : rechazado.Contains(n.Order) ? "Rejected"
            : solicitud.Status == ApprovalRequestStatus.Pending && n.Order == solicitud.CurrentLevel ? "Pending" : "Waiting")).ToList();
        var decisiones = solicitud.Decisions.OrderBy(d => d.DecidedAt).Select(d => new DecisionDeAprobacionDelDocumentoDto(
            d.Level, d.Decision.ToString(), d.DecidedByName, d.DecidedAt, d.Reason)).ToList();
        return new DocumentApprovalDto(solicitud.PublicId, solicitud.Status.ToString(), solicitud.CurrentLevel, niveles, decisiones);
    }

    /// <summary>El grupo sin excepción: una anulación huérfana (no debería existir) cae en el grupo de ajustes.</summary>
    private static DocumentClassGroup GrupoSeguro(DocumentClass clase, DocumentClass? claseDelOriginal) =>
        clase == DocumentClass.Voiding && claseDelOriginal is null or DocumentClass.Voiding
            ? DocumentClassGroup.Adjustments
            : GrupoDe(clase, claseDelOriginal);

    private async Task<Dictionary<int, UsuarioDto>> UsuariosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UsuarioDto(u.PublicId, u.Username), ct);
    }

    private async Task<Dictionary<int, ContraparteDto>> PersonasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ContraparteDto(p.PublicId, p.BusinessName ?? (p.FirstName + " " + p.LastName), p.TaxId), ct);
    }
}
