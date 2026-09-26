using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Counts;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Lo que el conteo guarda en <c>INV_Documents.CountScopeJson</c> (data-model §5.1: «Ids de categorías, ubicaciones o productos; en
/// I6 la clase ABC») más lo que se sella al abrir y no tiene columna propia: los contadores declarados, si bloquea los movimientos
/// (<c>Conteo.BloquearMovimientos</c> de la bodega a la fecha de la foto) y quién lo abrió. Todo por <c>Id</c> interno: no sale de la
/// base; hacia afuera va por <c>PublicId</c>. (nuevo)
/// </summary>
public sealed record CriterioDelConteo
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<int> Categorias { get; init; } = [];
    public IReadOnlyList<int> Ubicaciones { get; init; } = [];
    public IReadOnlyList<int> Productos { get; init; } = [];
    public string? ClaseAbc { get; init; }
    public IReadOnlyList<int> Contadores { get; init; } = [];

    /// <summary>Sellado al abrir; nulo mientras el conteo no tiene foto.</summary>
    public bool? BloqueaMovimientos { get; init; }

    /// <summary><c>SEC_Users.Id</c> de quien abrió (sellado al abrir).</summary>
    public int? AbiertoPor { get; init; }

    public static CriterioDelConteo De(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new CriterioDelConteo() : JsonSerializer.Deserialize<CriterioDelConteo>(json, Opciones) ?? new CriterioDelConteo();

    public string ComoJson() => JsonSerializer.Serialize(this, Opciones);
}

/// <summary>Los parámetros del conteo a una fecha para su bodega. (nuevo)</summary>
public sealed record ParametrosDelConteo(bool BloquearMovimientos, ToleranciaDeReconteo Tolerancia, string FechaDelAjuste);

/// <summary>
/// Lo que comparten los comandos y consultas de conteos (feature 012, US11): buscar un conteo al alcance, su estado derivado, sus
/// parámetros, qué productos cubre su alcance, lo movido después de la foto, la comparación de sus líneas con
/// <see cref="ComparacionDeConteo"/> y quiénes participaron (para excluirlos de la aprobación del ajuste). (nuevo)
/// </summary>
public sealed class VistaDeConteos(IApplicationDbContext db, VistaDeDocumentos vista, ILectorDeParametros parametros)
{
    private static readonly DocumentStatus[] AjustesVivos = [DocumentStatus.Draft, DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>El conteo al alcance de quien pregunta, o nulo (el 404 de <c>Inventory.Count.NotFound</c>).</summary>
    public async Task<InventoryDocument?> BuscarAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        var documento = await vista.BuscarAsync(publicId, DocumentClassGroup.Counts, seguir, ct);
        return documento is { Class: DocumentClass.PhysicalCount } ? documento : null;
    }

    /// <summary>¿Abierto? En borrador, con foto (data-model §8).</summary>
    public static bool EstaAbierto(InventoryDocument conteo) => conteo.Status == DocumentStatus.Draft && conteo.CountSnapshotAt is not null;

    /// <summary>Los ajustes del conteo (vínculo <c>CountAdjustmentOf</c>) que no se descartaron.</summary>
    public async Task<IReadOnlyList<InventoryDocument>> AjustesAsync(int conteoId, CancellationToken ct) =>
        await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == conteoId && l.Kind == DocumentLinkKind.CountAdjustmentOf)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => d.Status != DocumentStatus.Discarded)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

    /// <summary>Los ajustes en curso o vigentes (borrador, en aprobación o confirmados): los que impiden generar otro.</summary>
    public static IReadOnlyList<InventoryDocument> AjustesVigentes(IReadOnlyList<InventoryDocument> ajustes) =>
        ajustes.Where(a => AjustesVivos.Contains(a.Status)).ToList();

    /// <summary>
    /// El estado derivado (§12): descartado, anulado, borrador (sin foto), abierto (con foto), y un conteo cerrado según sus ajustes:
    /// en aprobación si alguno sigue en borrador o en aprobación, ajustado si todos los vivos se confirmaron o si no tenía
    /// diferencias, cerrado si todavía no se generó el ajuste.
    /// </summary>
    public static string EstadoDe(InventoryDocument conteo, IReadOnlyList<InventoryDocument> ajustes, bool conDiferencias) => conteo.Status switch
    {
        DocumentStatus.Discarded => EstadosDeConteo.Descartado,
        DocumentStatus.Voided => EstadosDeConteo.Anulado,
        DocumentStatus.Draft or DocumentStatus.PendingApproval => conteo.CountSnapshotAt is null ? EstadosDeConteo.Borrador : EstadosDeConteo.Abierto,
        _ when ajustes.Any(a => a.Status is DocumentStatus.Draft or DocumentStatus.PendingApproval) => EstadosDeConteo.AjusteEnAprobacion,
        _ when ajustes.Any(a => a.Status == DocumentStatus.Confirmed) || !conDiferencias => EstadosDeConteo.Ajustado,
        _ => EstadosDeConteo.Cerrado,
    };

    /// <summary>Los parámetros del conteo a <paramref name="fecha"/> para su bodega.</summary>
    public async Task<Result<ParametrosDelConteo>> ParametrosAsync(int bodegaId, DateOnly fecha, CancellationToken ct)
    {
        var bloquea = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ConteoBloquearMovimientos, fecha,
            ParameterScopeKind.Warehouse, bodegaId, ct);
        if (bloquea.IsFailure) return Result.Failure<ParametrosDelConteo>(bloquea.Error);
        var porcentaje = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ConteoToleranciaReconteoPorcentaje, fecha,
            ParameterScopeKind.Warehouse, bodegaId, ct);
        if (porcentaje.IsFailure) return Result.Failure<ParametrosDelConteo>(porcentaje.Error);
        var unidades = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ConteoToleranciaReconteoUnidades, fecha,
            ParameterScopeKind.Warehouse, bodegaId, ct);
        if (unidades.IsFailure) return Result.Failure<ParametrosDelConteo>(unidades.Error);
        var fechaDelAjuste = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ConteoFechaDelAjuste, fecha, ct: ct);
        if (fechaDelAjuste.IsFailure) return Result.Failure<ParametrosDelConteo>(fechaDelAjuste.Error);

        return Result.Success(new ParametrosDelConteo(
            bloquea.Value.Como<bool>(),
            new ToleranciaDeReconteo(porcentaje.Value.Como<decimal>(), unidades.Value.Como<decimal>()),
            fechaDelAjuste.Value.Texto == ReglasDeFechaDelAjuste.Aprobacion ? ReglasDeFechaDelAjuste.Aprobacion : ReglasDeFechaDelAjuste.Foto));
    }

    /// <summary>Las categorías pedidas con todas sus descendientes (el árbol por <c>Path</c>).</summary>
    public async Task<HashSet<int>> CategoriasConDescendientesAsync(IReadOnlyCollection<int> categorias, CancellationToken ct)
    {
        if (categorias.Count == 0) return [];
        var rutas = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Where(c => categorias.Contains(c.Id)).Select(c => c.Path).ToListAsync(ct);
        var todas = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Select(c => new { c.Id, c.Path }).ToListAsync(ct);
        return todas.Where(c => categorias.Contains(c.Id) || rutas.Any(r => !string.IsNullOrEmpty(r) && c.Path.StartsWith(r, StringComparison.Ordinal)))
            .Select(c => c.Id).ToHashSet();
    }

    /// <summary>
    /// De <paramref name="productos"/>, los que están en el alcance del conteo: todos en <c>All</c>; los de la selección; los de las
    /// categorías (con sus descendientes); y siempre los que están en la foto (en el conteo por ubicación, el alcance es lo que la foto
    /// encontró allí).
    /// </summary>
    public async Task<HashSet<int>> ProductosEnElAlcanceAsync(InventoryDocument conteo, IReadOnlyCollection<int> productos, CancellationToken ct)
    {
        if (productos.Count == 0) return [];
        if (conteo.CountScope == CountScope.All) return productos.ToHashSet();

        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        var enLaFoto = conteo.Id == 0
            ? []
            : await db.CountSnapshotLines.AsNoTracking().Where(l => l.DocumentId == conteo.Id && productos.Contains(l.ProductId))
                .Select(l => l.ProductId).Distinct().ToListAsync(ct);
        var resultado = enLaFoto.ToHashSet();

        switch (conteo.CountScope)
        {
            case CountScope.Selection:
                resultado.UnionWith(productos.Where(criterio.Productos.Contains));
                break;
            case CountScope.Category:
                var categorias = await CategoriasConDescendientesAsync(criterio.Categorias, ct);
                var deLasCategorias = await db.Products.AsNoTracking().IgnoreQueryFilters()
                    .Where(p => productos.Contains(p.Id) && categorias.Contains(p.CategoryId)).Select(p => p.Id).ToListAsync(ct);
                resultado.UnionWith(deLasCategorias);
                break;
        }
        return resultado;
    }

    /// <summary>
    /// Lo movido después de la foto por línea (entradas positivas, salidas negativas; sin los ajustes de costo): el kardex de la bodega
    /// con <c>Id</c> mayor que <c>CountSnapshotKardexEntryId</c>, por producto, ubicación y lote.
    /// </summary>
    public async Task<IReadOnlyList<MovimientoPosteriorALaFoto<int>>> MovimientosPosterioresAsync(InventoryDocument conteo,
        IReadOnlyList<CountSnapshotLine> lineas, CancellationToken ct)
    {
        if (lineas.Count == 0 || conteo.WarehouseId is not int bodega) return [];
        var desde = conteo.CountSnapshotKardexEntryId ?? 0L;
        var productos = lineas.Select(l => l.ProductId).Distinct().ToList();
        var movidos = await db.KardexEntries.AsNoTracking()
            .Where(k => k.Id > desde && k.WarehouseId == bodega && productos.Contains(k.ProductId) && k.Kind != KardexEntryKind.CostAdjustment)
            .GroupBy(k => new { k.ProductId, k.LocationId, k.LotId })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, g.Key.LotId, Cantidad = g.Sum(k => k.QuantityBase) })
            .ToListAsync(ct);
        return lineas.Select(l => new MovimientoPosteriorALaFoto<int>(l.Id,
                movidos.Where(m => m.ProductId == l.ProductId && m.LocationId == l.LocationId && m.LotId == l.LotId).Sum(m => m.Cantidad)))
            .Where(m => m.Cantidad != 0m)
            .ToList();
    }

    /// <summary>
    /// La comparación de las líneas del conteo con sus capturas (<see cref="ComparacionDeConteo"/>): con las tolerancias vigentes a la
    /// fecha de la foto para su bodega y, si el conteo admitía movimientos, lo movido después de la foto. Lee las capturas guardadas:
    /// quien acaba de agregar una, guarda antes de comparar.
    /// </summary>
    public async Task<Result<IReadOnlyList<ComparacionDeLinea<int>>>> CompararAsync(InventoryDocument conteo, IReadOnlyList<CountSnapshotLine> lineas,
        CancellationToken ct)
    {
        var leidos = await ParametrosAsync(conteo.WarehouseId ?? 0, conteo.OperationDate, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<ComparacionDeLinea<int>>>(leidos.Error);

        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        var sumaMovimientos = criterio.BloqueaMovimientos == false;
        var ids = lineas.Select(l => l.Id).ToList();
        var capturas = await db.CountCaptures.AsNoTracking().Where(c => c.DocumentId == conteo.Id && ids.Contains(c.SnapshotLineId))
            .Select(c => new { c.SnapshotLineId, c.Round, c.CounterUserId, c.Quantity }).ToListAsync(ct);
        var movimientos = sumaMovimientos ? await MovimientosPosterioresAsync(conteo, lineas, ct) : [];

        return Result.Success(ComparacionDeConteo.Comparar(new PedidoDeComparacion<int>(
            lineas.Select(l => new LineaDeFoto<int>(l.Id, l.TheoreticalQuantity)).ToList(),
            capturas.Select(c => new CapturaDeConteo<int>(c.SnapshotLineId, c.Round, c.CounterUserId, c.Quantity)).ToList(),
            movimientos,
            leidos.Value.Tolerancia,
            sumaMovimientos)));
    }

    /// <summary>
    /// Quiénes participaron en el conteo: quien lo creó, quien lo abrió, los contadores declarados y todos los que capturaron. Ninguno
    /// aprueba su ajuste (FR-010, US11-4).
    /// </summary>
    public async Task<IReadOnlyList<int>> ParticipantesAsync(InventoryDocument conteo, CancellationToken ct)
    {
        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        var capturaron = await db.CountCaptures.AsNoTracking().Where(c => c.DocumentId == conteo.Id).Select(c => c.CounterUserId).Distinct().ToListAsync(ct);
        return new[] { conteo.CreatedByUserId }
            .Concat(criterio.AbiertoPor is int abrio ? [abrio] : [])
            .Concat(criterio.Contadores)
            .Concat(capturaron)
            .Distinct()
            .ToList();
    }
}
