using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Lo que comparten las estrategias del grupo <c>Adjustments</c> de I1 (feature 012, T253; contracts/api.md §10; FR-036, FR-037):
/// el ajuste positivo (<see cref="EfectoDeAjustePositivo"/>) y las salidas por ajuste (<see cref="EfectoDeSalidaPorAjuste"/>:
/// negativo, consumo interno, baja). (nuevo)
/// <list type="bullet">
/// <item>reglas de la clase antes de la aprobación: producto inventariable, activo y no bloqueado; ningún ajuste sale del
/// tránsito ni entra a él (las diferencias de un traslado se resuelven por US10); las de cada clase
/// (<see cref="ReglasDeLaClaseAsync"/>);</item>
/// <item>el monto que se aprueba es el valor al costo estimado con el costo vigente (§10);</item>
/// <item>bloquea bodega, estados de costo, existencias y detalles que va a tocar (lo prepara <see cref="RegistroDeKardex"/> en
/// <see cref="ValidarAsync"/>, que el ciclo común siempre llama antes de <see cref="Cerrojo"/>);</item>
/// <item>aplica con <see cref="RegistroDeKardex"/>, revierte con <see cref="ReversionDeKardex"/> y arma los mensajes con
/// <see cref="EmisionDeInventario"/>.</item>
/// </list>
/// Es <c>Scoped</c>: lo preparado y lo registrado se recuerdan por documento dentro de la petición.
/// </summary>
public abstract class EfectoDeAjuste(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IPermissionChecker permisos,
    IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, PreparacionDelRegistro> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    protected IPermissionChecker Permisos { get; } = permisos;

    /// <summary>Entrada (+1) o salida (−1).</summary>
    protected abstract decimal Signo { get; }

    /// <summary>Las reglas propias de la clase sobre el documento (causa, centro de costo, costo digitado…), en el orden de §10.</summary>
    protected abstract Task<IReadOnlyList<Error>> ReglasDeLaClaseAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>Cómo se valora cada línea (el positivo admite costo indicado; las salidas, siempre al vigente).</summary>
    protected abstract (ValoracionDelMovimiento Valoracion, decimal? Costo) Valoracion(InventoryDocumentLine linea);

    // ----------------------------------------------------------------------------------------------- borrador --

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var avisos = new List<Error>(await ReglasComunesAsync(contexto, ct));
        avisos.AddRange(await ReglasDeLaClaseAsync(contexto, ct));
        if (Signo < 0m && await ExistenciaInsuficienteAsync(contexto.Documento, ct) is { } faltante) avisos.Add(faltante);
        return avisos;
    }

    // ------------------------------------------------------------------------------------------- confirmar --

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
            var errores = await ReglasComunesAsync(contexto, ct);
            if (errores.Count > 0) return Result.Failure(errores[0]);
            var deLaClase = await ReglasDeLaClaseAsync(contexto, ct);
            if (deLaClase.Count > 0) return Result.Failure(deLaClase[0]);
            movimientos = Movimientos(documento);
        }

        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = preparado.Value;
        return Result.Success();
    }

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

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var filas = _registrados.TryGetValue(contexto.Documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await KardexDelDocumentoAsync(contexto.Documento, ct);
        if (filas.Count == 0) return [];
        return [await emision.AjusteAprobadoAsync(contexto.Documento, contexto.Tipo, filas, ct)];
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

    /// <summary>Un movimiento por línea viva, en orden, en la bodega del documento y en unidad base.</summary>
    protected IReadOnlyList<MovimientoDeKardex> Movimientos(InventoryDocument documento)
    {
        if (documento.WarehouseId is not int bodega) return [];
        return documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                var (valoracion, costo) = Valoracion(l);
                return new MovimientoDeKardex(l, bodega, Signo * l.QuantityBase, valoracion, costo, LocationId: l.LocationId);
            })
            .ToList();
    }

    /// <summary>Producto inventariable, activo y no bloqueado; ningún ajuste toca el tránsito.</summary>
    private async Task<IReadOnlyList<Error>> ReglasComunesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var errores = new List<Error>();
        var documento = contexto.Documento;
        if (documento.WarehouseId is int bodegaId
            && (await maestros.BodegasPorIdAsync([bodegaId], ct)).FirstOrDefault() is { EsTransito: true } transito)
        {
            errores.Add(InventoryErrors.TransitNotAllowed(transito.Code));
        }

        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            if (!producto.Inventariable) errores.Add(InventoryErrors.ProductNotInventoriable(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Blocked) errores.Add(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Inactive) errores.Add(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
        }
        return errores;
    }

    /// <summary>El aviso de existencia del borrador: lo que hoy no cabría, por bodega (la confirmación lo vuelve a mirar en el cerrojo).</summary>
    private async Task<Error?> ExistenciaInsuficienteAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.WarehouseId is not int bodega) return null;
        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Count == 0) return null;

        var productos = vivas.Select(l => l.ProductId).Distinct().ToList();
        var disponibles = await db.StockBalances.AsNoTracking()
            .Where(s => s.WarehouseId == bodega && productos.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, s => s.Physical - s.Reserved, ct);
        var maestrosDeProducto = (await maestros.ProductosPorIdAsync(productos, ct)).ToDictionary(p => p.Id);
        var bodegaPublica = (await maestros.BodegasPorIdAsync([bodega], ct)).FirstOrDefault()?.PublicId ?? Guid.Empty;

        var faltantes = new List<InventoryErrors.LineaSinExistencia>();
        foreach (var linea in vivas)
        {
            var disponible = disponibles.GetValueOrDefault(linea.ProductId);
            if (disponible < linea.QuantityBase)
            {
                var producto = maestrosDeProducto.GetValueOrDefault(linea.ProductId);
                faltantes.Add(new InventoryErrors.LineaSinExistencia(linea.LineNumber, producto?.PublicId ?? Guid.Empty, producto?.Code ?? string.Empty,
                    bodegaPublica, null, linea.QuantityBase, Math.Max(0m, disponible)));
            }
            disponibles[linea.ProductId] = disponible - linea.QuantityBase;
        }
        return faltantes.Count == 0 ? null : InventoryErrors.StockInsufficient(faltantes);
    }

    private async Task<List<KardexEntry>> KardexDelDocumentoAsync(InventoryDocument documento, CancellationToken ct)
    {
        var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id).ToList();
        return enMemoria.Count > 0 ? enMemoria : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).ToListAsync(ct);
    }
}
