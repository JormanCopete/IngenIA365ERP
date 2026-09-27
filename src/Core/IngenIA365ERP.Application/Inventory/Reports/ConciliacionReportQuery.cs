using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>reconciliation</c> de <c>/api/reports/inventory</c> (feature 012, US7, T525; FR-081, SC-005; contracts/api.md §27;
/// <c>Inventory.Reconciliation.View</c>): el inventario valorizado contra el saldo contable, <b>por conjunto de cuentas</b>, a una
/// fecha (<c>asOf</c>). El saldo lo responde Contabilidad por <see cref="IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync"/>
/// (sin alcance de sucursal contable: la cifra se compara contra el valorizado de toda la cooperativa, G5); Inventario no lee los
/// libros. Cinco secciones en una tabla (<see cref="FilaExportable.Seccion"/>) sobre las mismas columnas:
/// <list type="number">
/// <item><b>Conjuntos</b>: grupos, cuentas, valorizado en bodegas (operativas, activas o no), en tránsito, el de las no activas con
/// cifras de referencia, total, saldo contable, diferencia (valorizado − saldo), y lo que la explica en pesos —el kardex de los documentos
/// cuyos mensajes siguen pendientes, en lote o rechazados— y lo que queda sin explicar (descontado también lo movido por tipos que no
/// pasan, sección 3);</item>
/// <item><b>Detalle por bodega</b> (informativo): el valorizado de cada bodega y grupo;</item>
/// <item><b>Lo movido por tipos que no pasan</b> (documentos sellados <c>NotPosted</c>), por tipo y grupo;</item>
/// <item><b>Ventas a crédito de esos tipos</b>: las ventas nacen en I3; hasta entonces no hay filas;</item>
/// <item><b>Bodegas no activas</b> que comparten cuentas, con sus cifras de referencia más recientes a la fecha: suman al valorizado del
/// conjunto y se muestran aparte; la diferencia no se les atribuye.</item>
/// </list>
/// Una bodega usa las cuentas de un grupo si el conjunto trae el par (grupo, su código) o (grupo, <c>*</c>). Si Contabilidad no
/// responde, la sección 1 va vacía y la nota lo dice. (nuevo)
/// </summary>
public sealed record ConciliacionReportQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ConciliacionReportQueryHandler(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    IContabilidadParaInventario? contabilidad = null)
    : IRequestHandler<ConciliacionReportQuery, Result<TablaExportable>>
{
    public const string PermisoDeConciliacion = "Inventory.Reconciliation.View";

    public const string SeccionConjuntos = "1. Conjuntos de cuentas";
    public const string SeccionBodegas = "2. Detalle por bodega (informativo)";
    public const string SeccionNoPasan = "3. Movido por tipos que no pasan";
    public const string SeccionCredito = "4. Ventas a crédito de tipos que no pasan";
    public const string SeccionReferencia = "5. Bodegas no activas con cifras de referencia";

    public static readonly VistaDeInformeDeInventario Vista = new(
        "reconciliation", "Conciliación con Contabilidad", "El valorizado por conjunto de cuentas contra el saldo contable, con lo que explica la diferencia.",
        "conciliacion-contable", ["asOf"], [], RequiredPermission: PermisoDeConciliacion);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Conjunto", TipoDeColumna.Texto),
        new("Grupos", TipoDeColumna.Texto),
        new("Cuentas", TipoDeColumna.Texto),
        new("Valorizado en bodegas", TipoDeColumna.Moneda),
        new("En tránsito", TipoDeColumna.Moneda),
        new("Valorizado total", TipoDeColumna.Moneda),
        new("Saldo contable", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
        new("Pendientes", TipoDeColumna.Moneda),
        new("En lote", TipoDeColumna.Moneda),
        new("Rechazados", TipoDeColumna.Moneda),
        new("Sin explicar", TipoDeColumna.Moneda),
        new("Mensaje", TipoDeColumna.Texto, "_mensaje"),
    ];

    private static bool Mismo(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public async Task<Result<TablaExportable>> Handle(ConciliacionReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var notas = new List<string>
        {
            "Diferencia = valorizado total − saldo contable. Pendientes, en lote y rechazados son el kardex de los documentos cuyos mensajes todavía no llegan al libro.",
            "Las bodegas no activas suman con sus cifras de referencia; la diferencia no se les atribuye.",
        };

        IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Accounting.ConjuntoDeCuentasDto>? conjuntos = null;
        if (contabilidad is not null)
        {
            try
            {
                var r = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, ct);
                if (r.IsSuccess) conjuntos = r.Value;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                conjuntos = null;
            }
        }
        if (conjuntos is null) notas.Add("Contabilidad no respondió la consulta de saldos: la sección 1 no se pudo calcular.");

        var valorizado = await ComparacionDeActivacion.ValorizadoPorGrupoYBodegaAsync(db, parametros, corte, ct);
        var bodegas = await db.Warehouses.AsNoTracking().ToListAsync(ct);
        var porId = bodegas.ToDictionary(w => w.Id);
        var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        // Las cifras de referencia más recientes a la fecha de cada bodega no activa.
        var cifras = (await db.LegacyFigures.AsNoTracking().Where(f => f.AsOfDate <= corte && f.WarehouseId != null)
                .Select(f => new { WarehouseId = f.WarehouseId!.Value, f.AccountingGroupId, Value = f.Value ?? 0m, f.AsOfDate }).ToListAsync(ct))
            .Where(f => porId.TryGetValue(f.WarehouseId, out var w) && !w.EstaActiva)
            .GroupBy(f => f.WarehouseId)
            .SelectMany(g => g.Where(f => f.AsOfDate == g.Max(x => x.AsOfDate)))
            .ToList();

        // Lo que explica la diferencia: el kardex hasta el corte de los documentos cuyos mensajes de negocio a Contabilidad siguen
        // pendientes, en lote o rechazados, o no pasan.
        var explicado = await ExplicadoAsync(corte, ct);

        var filas = new List<FilaExportable>();
        var numero = 0;
        foreach (var c in conjuntos ?? [])
        {
            numero++;
            var codigos = c.AccountingGroupCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool LaUsa(string bodega) => c.Pairs.Any(p => codigos.Contains(p.AccountingGroupCode) && (p.WarehouseCode == "*" || Mismo(p.WarehouseCode, bodega)));
            var delConjunto = valorizado.Where(v => codigos.Contains(v.Key.Grupo) && porId.TryGetValue(v.Key.WarehouseId, out var w) && LaUsa(w.Code)).ToList();
            var enBodegas = delConjunto.Where(v => !porId[v.Key.WarehouseId].EsTransito).Sum(v => v.Value.Valor);
            var enTransito = delConjunto.Where(v => porId[v.Key.WarehouseId].EsTransito).Sum(v => v.Value.Valor);
            var fueraDelModulo = cifras.Where(f => LaUsa(porId[f.WarehouseId].Code) && f.AccountingGroupId is int g && grupos.TryGetValue(g, out var gc) && codigos.Contains(gc))
                .Sum(f => f.Value);
            var total = enBodegas + enTransito + fueraDelModulo;
            var diferencia = total - c.Balance;
            decimal Suma(DeliveryStatus estado) => explicado
                .Where(x => x.Estado == estado && codigos.Contains(x.Grupo) && porId.TryGetValue(x.WarehouseId, out var w) && LaUsa(w.Code)).Sum(x => x.Valor);
            var pendiente = Suma(DeliveryStatus.Pending);
            var enLote = Suma(DeliveryStatus.InBatch);
            var rechazado = Suma(DeliveryStatus.Rejected);
            var noPasa = Suma(DeliveryStatus.NotApplicable);
            filas.Add(new FilaExportable(
            [
                $"Conjunto {numero}", string.Join(", ", c.AccountingGroupCodes), string.Join(", ", c.Accounts.Select(a => $"{a.AccountCode} {a.AccountName}")),
                enBodegas + fueraDelModulo, enTransito, total, c.Balance, diferencia, pendiente, enLote, rechazado,
                diferencia - pendiente - enLote - rechazado - noPasa, null,
            ], SeccionConjuntos, Resaltada: diferencia != 0m));
        }

        foreach (var v in valorizado.Where(v => v.Value.Valor != 0m || v.Value.Cantidad != 0m)
                     .OrderBy(v => porId.GetValueOrDefault(v.Key.WarehouseId)?.Code, StringComparer.Ordinal).ThenBy(v => v.Key.Grupo, StringComparer.Ordinal))
        {
            var w = porId.GetValueOrDefault(v.Key.WarehouseId);
            filas.Add(new FilaExportable(
            [
                w?.Code, v.Key.Grupo, null,
                w is { EsTransito: false } ? v.Value.Valor : null, w is { EsTransito: true } ? v.Value.Valor : null, v.Value.Valor,
                null, null, null, null, null, null, null,
            ], SeccionBodegas));
        }

        foreach (var g in explicado.Where(x => x.Estado == DeliveryStatus.NotApplicable)
                     .GroupBy(x => (x.TipoDeDocumento, x.Grupo)).OrderBy(g => g.Key.TipoDeDocumento, StringComparer.Ordinal).ThenBy(g => g.Key.Grupo, StringComparer.Ordinal))
        {
            filas.Add(new FilaExportable(
            [
                g.Key.TipoDeDocumento, g.Key.Grupo, null, null, null, g.Sum(x => x.Valor), null, null, null, null, null, null,
                g.Select(x => x.Mensaje).FirstOrDefault(),
            ], SeccionNoPasan));
        }
        notas.Add("Sección 4: las ventas a crédito nacen con las ventas (I3); hasta entonces no hay filas.");

        foreach (var g in cifras.GroupBy(f => (f.WarehouseId, f.AccountingGroupId)).OrderBy(g => porId[g.Key.WarehouseId].Code, StringComparer.Ordinal))
        {
            var usa = conjuntos?.Any(c => c.Pairs.Any(p => (p.WarehouseCode == "*" || Mismo(p.WarehouseCode, porId[g.Key.WarehouseId].Code))
                && g.Key.AccountingGroupId is int gid && grupos.TryGetValue(gid, out var gc) && Mismo(p.AccountingGroupCode, gc))) ?? false;
            if (!usa) continue;
            filas.Add(new FilaExportable(
            [
                porId[g.Key.WarehouseId].Code, g.Key.AccountingGroupId is int gid2 ? grupos.GetValueOrDefault(gid2) : null,
                $"Cifras al {g.Max(x => x.AsOfDate):yyyy-MM-dd}", g.Sum(x => x.Value), null, g.Sum(x => x.Value),
                null, null, null, null, null, null, null,
            ], SeccionReferencia));
        }

        return Result.Success(new TablaExportable("Conciliación con Contabilidad", $"Al {corte:yyyy-MM-dd}", Columnas, filas, null, notas));
    }

    /// <summary>Un importe del kardex de un documento cuyo mensaje a Contabilidad está en ese estado. (nuevo)</summary>
    private sealed record Explicado(DeliveryStatus Estado, string TipoDeDocumento, string Grupo, int WarehouseId, decimal Valor, string Mensaje);

    private async Task<IReadOnlyList<Explicado>> ExplicadoAsync(DateOnly corte, CancellationToken ct)
    {
        var estados = new[] { DeliveryStatus.Pending, DeliveryStatus.InBatch, DeliveryStatus.Rejected, DeliveryStatus.NotApplicable };
        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == IntegrationDestinations.Accounting && estados.Contains(d.Status)
                && d.Message!.Kind == IntegrationMessageKind.Business && d.Message.OriginKind == MessageOriginKind.Document
                && d.Message.OperationDate <= corte)
            .Select(d => new { d.Status, d.Message!.OriginPublicId, d.Message.PublicId })
            .ToListAsync(ct);
        if (entregas.Count == 0) return [];

        // Un documento, un estado: el más atrasado de sus mensajes (rechazado antes que en lote, en lote antes que pendiente).
        var estadoDe = entregas.GroupBy(e => e.OriginPublicId).ToDictionary(g => g.Key, g => (Estado: g.OrderBy(e => Orden(e.Status)).First().Status, Mensaje: g.First().PublicId));
        var publicos = estadoDe.Keys.ToList();
        var filas = await (from k in db.KardexEntries.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                           join t in db.InventoryDocumentTypes.AsNoTracking() on d.DocumentTypeId equals t.Id
                           where publicos.Contains(d.PublicId) && k.OperationDate <= corte
                           group k by new { d.PublicId, t.Code, k.ProductId, k.WarehouseId } into g
                           select new { g.Key.PublicId, g.Key.Code, g.Key.ProductId, g.Key.WarehouseId, Valor = g.Sum(x => x.TotalCost) })
            .ToListAsync(ct);
        var codigos = await GrupoContableALaFecha.CodigosAsync(db, filas.Select(f => f.ProductId).Distinct().ToList(), corte, ct);
        return filas
            .Where(f => codigos.GetValueOrDefault(f.ProductId) is not null)
            .Select(f => new Explicado(estadoDe[f.PublicId].Estado, f.Code, codigos[f.ProductId]!, f.WarehouseId, f.Valor, estadoDe[f.PublicId].Mensaje.ToString()))
            .ToList();

        static int Orden(DeliveryStatus s) => s switch
        {
            DeliveryStatus.Rejected => 0,
            DeliveryStatus.InBatch => 1,
            DeliveryStatus.Pending => 2,
            _ => 3,
        };
    }
}
