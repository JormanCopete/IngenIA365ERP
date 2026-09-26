using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>OpeningBalance</c> (feature 012, T309; FR-044, FR-089, FR-091; contracts/api.md §13.1; mensajes.md §7.1):
/// <list type="bullet">
/// <item>sólo en una bodega operativa y todavía <c>NotActivated</c> (la regla común de <see cref="ReglasDelDocumento"/> dice
/// <c>Inventory.OpeningBalance.WarehouseActive</c>; aquí se repite para la anulación, que las reglas comunes admiten en cualquier
/// bodega); nunca en tránsito (<c>Inventory.OpeningBalance.TransitNotAllowed</c>);</item>
/// <item>fecha = la <c>CutoffDate</c> de la bodega (la pone <c>ImportOpeningBalanceCommand</c>); entrada al <b>costo cargado</b>
/// de cada línea (<see cref="ValoracionDelMovimiento.AlCostoIndicado"/>, FR-044);</item>
/// <item>exento de <c>Costeo.RetroactivosPermitidos</c> (T18, D8): <see cref="RegistroDeKardex.EsExentoAsync"/> lo reconoce por su
/// clase y la bodega no activa, y el retroactivo mínimo de I1 recalcula las salidas posteriores del ámbito; sus
/// <c>AjusteDeCostoReconocido</c> (negocio) salen con él;</item>
/// <item><c>PostingMode</c> nulo (sólo emite informativos) y <c>SaldoInicialCargado</c> v1 con <c>Kind = Informational</c>; su
/// anulación emite <c>DocumentoAnulado</c> con el <c>Kind</c> del original (§7.1).</item>
/// </list>
/// Es <c>Scoped</c>: lo preparado y lo registrado se recuerdan por documento dentro de la petición. (nuevo)
/// </summary>
public sealed class EfectoSaldoInicial(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, PreparacionDelRegistro> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    public override DocumentClass Clase => DocumentClass.OpeningBalance;

    // ----------------------------------------------------------------------------------------------- borrador --

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        await ReglasAsync(contexto, ct);

    // ------------------------------------------------------------------------------------------- confirmar --

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        IReadOnlyList<MovimientoDeKardex> movimientos;
        if (contexto.EsAnulacion)
        {
            // La anulación sólo mientras la bodega no opera (FR-091): con la bodega activa, lo que sobre o falte se ajusta.
            if (await BodegaAsync(documento, ct) is { Activa: true } activa)
                return Result.Failure(GoLiveErrors.OpeningBalanceWarehouseActive(activa.PublicId, activa.Code));
            movimientos = await reversion.MovimientosAsync(documento, contexto.Original!, ct);
        }
        else
        {
            var errores = await ReglasAsync(contexto, ct);
            if (errores.Count > 0) return Result.Failure(errores[0]);
            movimientos = Movimientos(documento);
        }

        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = preparado.Value;
        return Result.Success();
    }

    /// <summary>El valor al costo cargado: Σ cantidad × costo de las líneas (§1.3: saldo inicial, al costo).</summary>
    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.ValorEstimado : base.MontoParaAprobar(contexto);

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var registrado = await registro.RegistrarAsync(contexto.Documento, Movimientos(contexto.Documento), ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[contexto.Documento.PublicId] = registrado.Value;
        return Result.Success();
    }

    /// <summary>
    /// <c>SaldoInicialCargado</c> (informativo) y, si el saldo cambió el promedio de salidas posteriores del ámbito, un
    /// <c>AjusteDeCostoReconocido</c> por documento afectado (US3, T285: ésos sí se contabilizan).
    /// </summary>
    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var hecho = _registrados.GetValueOrDefault(contexto.Documento.PublicId);
        var filas = hecho is not null ? hecho.Lineas.ToList() : await KardexDelDocumentoAsync(contexto.Documento, ct);
        if (filas.Count == 0) return [];
        IReadOnlyList<object> retroactivos = hecho is null ? [] : await emision.AjustesRetroactivosAsync(hecho, ct);
        return [await emision.SaldoInicialCargadoAsync(contexto.Documento, filas, ct), .. retroactivos];
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var revertido = await reversion.RevertirAsync(contexto.Documento, contexto.Original!, ct);
        if (revertido.IsFailure) return Result.Failure(revertido.Error);
        _revertidos[contexto.Documento.PublicId] = revertido.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var diferencias = _revertidos.TryGetValue(contexto.Documento.PublicId, out var hecha) ? hecha.Diferencias : [];
        return await emision.AnulacionAsync(contexto.Documento, contexto.Original!, diferencias, ct);
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Una entrada por línea viva, en su orden, en la bodega del documento, al costo cargado.</summary>
    private static IReadOnlyList<MovimientoDeKardex> Movimientos(InventoryDocument documento)
    {
        if (documento.WarehouseId is not int bodega) return [];
        return documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber)
            .Select(l => new MovimientoDeKardex(l, bodega, l.QuantityBase, ValoracionDelMovimiento.AlCostoIndicado, l.UnitCost ?? 0m,
                LocationId: l.LocationId))
            .ToList();
    }

    /// <summary>
    /// Las reglas de la clase, en el orden de §13.1: bodega no de tránsito; producto inventariable, con grupo contable, activo y
    /// no bloqueado; costo cargado en cada línea (cero se admite, la plantilla ya avisó).
    /// </summary>
    private async Task<IReadOnlyList<Error>> ReglasAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var errores = new List<Error>();
        var documento = contexto.Documento;
        if (await BodegaAsync(documento, ct) is { EsTransito: true } transito)
            errores.Add(GoLiveErrors.OpeningBalanceTransitNotAllowed(transito.Code));

        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var ids = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = (await maestros.ProductosPorIdAsync(ids, ct)).ToDictionary(p => p.Id);
        var conGrupo = (await db.Products.AsNoTracking().IgnoreQueryFilters()
                .Where(p => ids.Contains(p.Id) && p.AccountingGroupId != null).Select(p => p.Id).ToListAsync(ct))
            .ToHashSet();
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            if (!producto.Inventariable) errores.Add(InventoryErrors.ProductNotInventoriable(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Blocked) errores.Add(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Inactive) errores.Add(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
            else if (!conGrupo.Contains(producto.Id)) errores.Add(Catalog.CatalogErrors.ProductAccountingGroupRequired());
            if (linea.UnitCost is null) errores.Add(InventoryErrors.FieldRequired("unitCost"));
        }
        return errores;
    }

    private async Task<BodegaDelDocumento?> BodegaAsync(InventoryDocument documento, CancellationToken ct) =>
        documento.WarehouseId is int id ? (await maestros.BodegasPorIdAsync([id], ct)).FirstOrDefault() : null;

    private async Task<List<KardexEntry>> KardexDelDocumentoAsync(InventoryDocument documento, CancellationToken ct)
    {
        var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id && k.Kind != KardexEntryKind.CostAdjustment).ToList();
        return enMemoria.Count > 0
            ? enMemoria
            : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id && k.Kind != KardexEntryKind.CostAdjustment).ToListAsync(ct);
    }
}
