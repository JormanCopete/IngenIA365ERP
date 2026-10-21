using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// La bandeja de mensajes vista desde Inventario (feature 013, T0075; T19): lo que la 012 tenía dentro de
/// <see cref="ListIntegrationMessagesQuery"/> y <see cref="SendNotApplicableMessagesCommand"/>, movido aquí sin cambiar
/// <c>/api/inventory/messages*</c> ni sus respuestas. El alcance es el de bodega de quien pregunta (<see cref="IAlcanceDeInventario"/>
/// sobre el documento de origen; las operaciones sin documento las ve sólo quien tiene todas las bodegas), los tipos son los de
/// documento (<c>INV_DocumentTypes</c>) y el modo de paso es <c>Contabilidad.ModoDePaso</c> por tipo de documento. Muestra los
/// mensajes de Inventario y los que le llegan a Comercial (destino <c>Inventory</c>). (nuevo)
/// </summary>
public sealed class VistaDeBandejaDeInventario(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeInventario,
    ILectorDeParametros parametros) : IVistaDeBandejaDelModulo
{
    public string Modulo => ModulosDeOrigen.Inventario;

    public async Task<IQueryable<IntegrationMessageDelivery>> VisiblesAsync(CancellationToken ct) =>
        Visibles(db, await alcanceDeInventario.ObtenerAsync(ct));

    /// <summary>
    /// Las entregas de Inventario que el alcance deja ver: con todas las bodegas, todas; si no, las de un documento de origen
    /// visible (<see cref="FiltroDeAlcance.DocumentosVisibles"/>). La usan también el tablero y las vistas de integración.
    /// </summary>
    public static IQueryable<IntegrationMessageDelivery> Visibles(IApplicationDbContext db, AlcanceDeInventario alcance)
    {
        var entregas = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Message!.OriginModule == ModulosDeOrigen.Inventario || d.Destination == IntegrationDestinations.Inventory);
        if (alcance.TodasLasBodegas) return entregas;
        var documentos = db.InventoryDocuments.AsNoTracking().DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        return entregas.Where(d => d.Message!.OriginKind == MessageOriginKind.Document && documentos.Any(doc => doc.PublicId == d.Message.OriginPublicId));
    }

    public async Task<IReadOnlyList<string>> CodigosDeTipoAsync(IReadOnlyCollection<Guid> tipoPublicIds, CancellationToken ct)
    {
        var publicos = tipoPublicIds.ToList();
        return await db.InventoryDocumentTypes.AsNoTracking().Where(t => publicos.Contains(t.PublicId)).Select(t => t.Code).ToListAsync(ct);
    }

    public IQueryable<IntegrationMessageDelivery> DeLosTipos(IQueryable<IntegrationMessageDelivery> entregas, IReadOnlyCollection<string> codigos)
    {
        var lista = codigos.ToList();
        return entregas.Where(d => d.Message!.OriginDocumentTypeCode != null && lista.Contains(d.Message.OriginDocumentTypeCode));
    }

    public string? CodigoDeTipo(string? originDocumentTypeCode, string type) => originDocumentTypeCode;

    public async Task<IReadOnlyList<string>> TiposQueSiguenSinPasoAsync(IReadOnlyCollection<string> codigos, DateOnly hoy, CancellationToken ct)
    {
        if (codigos.Count == 0) return [];
        var lista = codigos.ToList();
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => lista.Contains(t.Code))
            .Select(t => new { t.Id, t.Code }).ToListAsync(ct);

        var sinPaso = new List<string>();
        foreach (var tipo in tipos.OrderBy(t => t.Code, StringComparer.Ordinal))
        {
            var modo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, hoy,
                ParameterScopeKind.DocumentType, tipo.Id, ct);
            if (modo.IsSuccess && modo.Value.Texto == "NoPasa") sinPaso.Add(tipo.Code);
        }

        return sinPaso;
    }

    public string RutaDelLoteResumido(Guid batchPublicId) => $"/contabilidad/inventario/lotes/{batchPublicId:D}";
}
