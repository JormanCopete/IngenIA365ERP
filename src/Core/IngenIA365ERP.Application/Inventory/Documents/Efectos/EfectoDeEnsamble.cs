using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>El cuerpo <c>assembly</c> de un ensamble (contracts/api.md §10): el kit y cuántos se arman. (nuevo)</summary>
public sealed record AssemblyRequest(Guid KitProductPublicId, decimal Quantity);

/// <summary>Los errores del ensamble (I6, T928; contracts/api.md §10). (nuevo)</summary>
public static class ErroresDeEnsamble
{
    /// <summary>El producto que se ensambla no es un kit (o el documento no tiene una sola línea de kit). (nuevo)</summary>
    public static Error NotAKit(string productCode) => new ErrorConDatos("Inventory.Assembly.NotAKit",
        $"{productCode} no es un kit: sólo se ensamblan productos de clase kit, uno por documento.", new { productCode });

    /// <summary>El kit no tiene componentes registrados. (nuevo)</summary>
    public static Error ComponentsMissing(string kitCode) => new ErrorConDatos("Inventory.Assembly.ComponentsMissing",
        $"El kit {kitCode} no tiene componentes: regístrelos en su ficha antes de ensamblarlo.", new { kitCode });

    /// <summary>Una línea del ensamble no es componente del kit. (nuevo)</summary>
    public static Error NotAComponent(int lineNumber, string productCode, string kitCode) => new ErrorConDatos("Inventory.Assembly.NotAComponent",
        $"Línea {lineNumber}: {productCode} no es componente del kit {kitCode}.", new { lineNumber, productCode, kitCode });
}

/// <summary>
/// Las líneas que propone un ensamble (I6, T928; contracts/api.md §10: «las líneas de componentes las propone el servidor desde
/// <c>INV_ProductComponents</c>»): la del kit, que entra, y una por componente vigente, cantidad × <c>ProductComponent.Quantity</c> en su
/// unidad base, que sale. De lo que la persona ya había digitado conserva, por producto, el lote, la serie y la ubicación. (nuevo)
/// </summary>
public static class PropuestaDeEnsamble
{
    public static async Task<Result<IReadOnlyList<SaveInventoryDraftLine>>> LineasAsync(IApplicationDbContext db, AssemblyRequest ensamble,
        IReadOnlyList<SaveInventoryDraftLine> digitadas, CancellationToken ct)
    {
        if (ensamble.Quantity <= 0m) return Result.Failure<IReadOnlyList<SaveInventoryDraftLine>>(new Error(Error.Validation.Code, "Se ensambla una cantidad positiva de kits."));
        var kit = await db.Products.AsNoTracking().Include(p => p.BaseUnit)
            .FirstOrDefaultAsync(p => p.PublicId == ensamble.KitProductPublicId, ct);
        if (kit is null) return Result.Failure<IReadOnlyList<SaveInventoryDraftLine>>(ErroresDelDocumento.ProductoInexistente());
        if (kit.Kind != ProductKind.Kit) return Result.Failure<IReadOnlyList<SaveInventoryDraftLine>>(ErroresDeEnsamble.NotAKit(kit.Code));
        var componentes = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == kit.Id)
            .Join(db.Products.AsNoTracking(), c => c.ComponentProductId, p => p.Id, (c, p) => new { p.PublicId, p.BaseUnitId, p.Code, c.Quantity })
            .OrderBy(x => x.Code).ToListAsync(ct);
        if (componentes.Count == 0) return Result.Failure<IReadOnlyList<SaveInventoryDraftLine>>(ErroresDeEnsamble.ComponentsMissing(kit.Code));
        var unidades = componentes.Select(c => c.BaseUnitId).Append(kit.BaseUnitId).Distinct().ToList();
        var publicas = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidades.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.PublicId, ct);

        SaveInventoryDraftLine Linea(Guid producto, Guid unidad, decimal cantidad)
        {
            var previa = digitadas.FirstOrDefault(d => d.ProductPublicId == producto);
            return new SaveInventoryDraftLine(previa?.LinePublicId, producto, unidad, cantidad, LocationPublicId: previa?.LocationPublicId,
                LotCode: previa?.LotCode, SerialNumber: previa?.SerialNumber, ExpiryDate: previa?.ExpiryDate, Notes: previa?.Notes);
        }

        var lineas = new List<SaveInventoryDraftLine> { Linea(kit.PublicId, publicas[kit.BaseUnitId], ensamble.Quantity) };
        lineas.AddRange(componentes.Select(c => Linea(c.PublicId, publicas[c.BaseUnitId],
            ExpansionDeCombos.Cantidad(ensamble.Quantity, new ComponenteDelCompuesto(0, c.Quantity)))));
        return Result.Success<IReadOnlyList<SaveInventoryDraftLine>>(lineas);
    }
}

/// <summary>
/// <c>Assembly</c>, el ensamble de kits (feature 012, I6, T928; FR-036 fila «Ensamble»; US15-3; contracts/api.md §10; mensajes.md §6.5), en
/// el grupo <c>Adjustments</c>:
/// <list type="bullet">
/// <item>antes de la aprobación: una sola línea de kit (<c>Inventory.Assembly.NotAKit</c>), el kit con componentes
/// (<c>.ComponentsMissing</c>) y las demás líneas componentes suyos (<c>.NotAComponent</c>), todos activos y no bloqueados; prepara el
/// cerrojo;</item>
/// <item>el monto que se compara con el límite de <c>Inventory.Adjustments.Confirm</c> es el <b>valor al costo</b> de lo que se consume;</item>
/// <item>dentro del cerrojo: salen los componentes al promedio de su ámbito (con su lote sugerido si lo controlan) y entra el kit por
/// <b>exactamente</b> lo consumido (<see cref="MovimientoDeKardex.AlCostoConsumidoDe"/> → <c>MotorDeCosteo.EntradaDeEnsamble</c>, con el
/// residuo visible);</item>
/// <item>emite <c>AjusteInventarioAprobado</c> con operación <c>Ensamble</c>: las salidas de los componentes y la entrada del kit por grupo
/// y bodega; su anulación revierte todo al costo del original.</item>
/// </list>
/// Scoped. (nuevo)
/// </summary>
public sealed class EfectoDeEnsamble(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, PreparacionDelRegistro> _preparados = [];
    private readonly Dictionary<Guid, decimal> _consumoEstimado = [];
    private readonly Dictionary<Guid, (RegistroHecho Hecho, int KitId)> _registrados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    public override DocumentClass Clase => DocumentClass.Assembly;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var reglas = await ReglasAsync(contexto.Documento, ct);
        return reglas.IsFailure ? [reglas.Error] : [];
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        IReadOnlyList<MovimientoDeKardex> movimientos;
        if (contexto.EsAnulacion)
        {
            movimientos = await reversion.MovimientosAsync(documento, contexto.Original!, ct);
        }
        else
        {
            var reglas = await ReglasAsync(documento, ct);
            if (reglas.IsFailure) return Result.Failure(reglas.Error);
            movimientos = Movimientos(documento, reglas.Value);
            var salidas = movimientos.Where(m => m.QuantityBase < 0m).ToList();
            var consumo = await registro.PrepararAsync(documento, salidas, ct);
            if (consumo.IsFailure) return Result.Failure(consumo.Error);
            _consumoEstimado[documento.PublicId] = consumo.Value.ValorEstimado;
        }
        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = preparado.Value;
        return Result.Success();
    }

    /// <summary>El valor al costo de lo que se consume (§10), no el total de la venta ni el del kit.</summary>
    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _consumoEstimado.TryGetValue(contexto.Documento.PublicId, out var v) ? v
        : _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.ValorEstimado
        : base.MontoParaAprobar(contexto);

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var reglas = await ReglasAsync(contexto.Documento, ct);
        if (reglas.IsFailure) return Result.Failure(reglas.Error);
        var registrado = await registro.RegistrarAsync(contexto.Documento, Movimientos(contexto.Documento, reglas.Value), ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[contexto.Documento.PublicId] = (registrado.Value, reglas.Value.ProductId);
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        List<KardexEntry> filas;
        int kit;
        if (_registrados.TryGetValue(documento.PublicId, out var hecho))
        {
            filas = hecho.Hecho.Lineas.ToList();
            kit = hecho.KitId;
        }
        else
        {
            var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id).ToList();
            filas = enMemoria.Count > 0 ? enMemoria : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).ToListAsync(ct);
            var lineaDelKit = (await ReglasAsync(documento, ct)).Value;
            kit = lineaDelKit.ProductId;
        }
        return filas.Count == 0 ? [] : [await AjusteAsync(documento, contexto.Tipo, filas, kit, ct)];
    }

    /// <summary>T520: el ajuste con los costos provisionales (el promedio leído sin bloqueo), para la validación previa contable.</summary>
    public override async Task<IReadOnlyList<object>> MensajesProvisionalesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await MensajesDeAnulacionAsync(contexto, ct);
        var reglas = await ReglasAsync(contexto.Documento, ct);
        if (reglas.IsFailure) return [];
        var filas = await registro.FilasProvisionalesDeAsync(contexto.Documento, Movimientos(contexto.Documento, reglas.Value), ct);
        return [await AjusteAsync(contexto.Documento, contexto.Tipo, filas, reglas.Value.ProductId, ct)];
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

    /// <summary><c>AjusteInventarioAprobado</c> (<c>Ensamble</c>): las salidas de los componentes y la entrada del kit (con su residuo).</summary>
    private async Task<object> AjusteAsync(InventoryDocument documento, InventoryDocumentType tipo, IReadOnlyList<KardexEntry> filas, int kit, CancellationToken ct)
    {
        var salidas = filas.Where(f => f.ProductId != kit).ToList();
        var entradas = filas.Where(f => f.ProductId == kit).ToList();
        var ajuste = await emision.AjusteAprobadoAsync(documento, tipo, salidas, ct);
        var lineasDelKit = await emision.LineasDeCostoAsync(documento, entradas, KardexEntryKind.Entry, ct);
        return ajuste with { Lines = [.. ajuste.Lines, .. lineasDelKit] };
    }

    /// <summary>Las reglas del ensamble: una línea de kit con componentes y las demás, componentes suyos activos. Devuelve la línea del kit.</summary>
    private async Task<Result<InventoryDocumentLine>> ReglasAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var ids = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Kind, p.Status }).ToDictionaryAsync(p => p.Id, ct);
        var kits = vivas.Where(l => productos.TryGetValue(l.ProductId, out var p) && p.Kind == ProductKind.Kit).ToList();
        if (kits.Count != 1)
        {
            var codigo = kits.Count > 1 ? productos[kits[1].ProductId].Code : vivas.Count > 0 && productos.TryGetValue(vivas[0].ProductId, out var p0) ? p0.Code : string.Empty;
            return Result.Failure<InventoryDocumentLine>(ErroresDeEnsamble.NotAKit(codigo));
        }
        var lineaDelKit = kits[0];
        var kit = productos[lineaDelKit.ProductId];
        var componentes = (await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == kit.Id).Select(c => c.ComponentProductId).ToListAsync(ct)).ToHashSet();
        if (componentes.Count == 0) return Result.Failure<InventoryDocumentLine>(ErroresDeEnsamble.ComponentsMissing(kit.Code));
        foreach (var linea in vivas.Where(l => !ReferenceEquals(l, lineaDelKit)))
        {
            var producto = productos[linea.ProductId];
            if (!componentes.Contains(linea.ProductId)) return Result.Failure<InventoryDocumentLine>(ErroresDeEnsamble.NotAComponent(linea.LineNumber, producto.Code, kit.Code));
            if (producto.Status == ProductStatus.Blocked) return Result.Failure<InventoryDocumentLine>(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            if (producto.Status == ProductStatus.Inactive) return Result.Failure<InventoryDocumentLine>(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
        }
        if (vivas.Count == 1) return Result.Failure<InventoryDocumentLine>(ErroresDeEnsamble.ComponentsMissing(kit.Code));
        return Result.Success(lineaDelKit);
    }

    /// <summary>Las salidas de los componentes, al promedio de su ámbito, y la entrada del kit por lo que consumieron.</summary>
    private static IReadOnlyList<MovimientoDeKardex> Movimientos(InventoryDocument documento, InventoryDocumentLine lineaDelKit)
    {
        if (documento.WarehouseId is not int bodega) return [];
        var salidas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m && !ReferenceEquals(l, lineaDelKit)).OrderBy(l => l.LineNumber)
            .Select(l => new MovimientoDeKardex(l, bodega, -l.QuantityBase, ValoracionDelMovimiento.AlCostoVigente, LocationId: l.LocationId))
            .ToList();
        var entrada = new MovimientoDeKardex(lineaDelKit, bodega, lineaDelKit.QuantityBase, ValoracionDelMovimiento.AlCostoIndicado, 0m,
            LocationId: lineaDelKit.LocationId)
        {
            AlCostoConsumidoDe = salidas,
        };
        return [.. salidas, entrada];
    }
}
