using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>Un contenido de costo que no viaja con su documento: el contenido y el documento diferido cuyo costo es.</summary>
public sealed record CostoDiferido(object Contenido, InventoryDocument DocumentoDelCosto);

/// <summary>
/// El costo de la venta al cierre del mes (2026-11-18, pedido del dueño; <c>Contabilidad.CostoDeVenta</c>). La venta y la compra pasan a
/// Contabilidad en línea; el costo de las ventas con el parámetro en <c>AlCierreDelPeriodo</c> no: queda en <c>INV_DeferredCosts</c> y el
/// cierre del mes de inventario lo suma en un solo <see cref="CostoDelPeriodoReconocidoV1"/>. Lo que cambia es <b>cuándo</b> pasa, no el
/// asiento: cada parte del mensaje es el mismo contenido que habría viajado, con la sucursal y el centro de su sobre, y Contabilidad lo
/// desglosa con las mismas reglas de la matriz.
/// <list type="bullet">
///   <item>se difiere el costo propio de un documento que también lleva su parte comercial (la venta, el POS, la nota con devolución), así el
///         documento sigue teniendo un mensaje a Contabilidad del que heredan el modo sus notas, anulaciones y ajustes;</item>
///   <item>un <c>AjusteDeCostoReconocido</c> (compra con fecha atrasada, regularización de lo vendido sin existencia, diferencia de precio) sigue
///         a su documento afectado: si éste difirió su costo, el ajuste también;</item>
///   <item>la anulación copia la marca del anulado y deja el espejo de sus costos diferidos;</item>
///   <item>el mes de cada fila es el de la fecha de operación del documento que la dejó: nunca un mes ya cerrado.</item>
/// </list>
/// </summary>
public sealed class CostoAlCierre(IApplicationDbContext db, ILectorDeParametros? parametros = null)
{
    private static readonly JsonSerializerOptions Json = OpcionesDeMensajes.Opciones;

    // -------------------------------------------------------------------------------------------------- sellar --

    /// <summary>
    /// La marca que sella el documento al confirmar: la del original en una anulación; si no, el parámetro vigente a <paramref name="hoy"/>
    /// (excepción del tipo o general) cuando el documento pasa a Contabilidad. Con «no pasa» no hay nada que diferir.
    /// </summary>
    public async Task<Result<bool>> SellarAsync(InventoryDocument documento, InventoryDocument? original, PostingMode? modo, int tipoId, DateOnly hoy, CancellationToken ct)
    {
        if (original is not null) return Result.Success(original.CostAtPeriodClose);
        if (modo is null or PostingMode.NotPosted) return Result.Success(false);
        if (parametros is null) throw new InvalidOperationException("Sellar el costo al cierre necesita el lector de parámetros.");
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadCostoDeVenta, hoy,
            ParameterScopeKind.DocumentType, tipoId, ct);
        if (leido.IsFailure) return Result.Failure<bool>(leido.Error);
        return Result.Success(string.Equals(leido.Value.Texto, ParametrosDeInventario.CostoAlCierreDelPeriodo, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------ separar --

    /// <summary>¿Es el costo propio de una venta o de una devolución de cliente?</summary>
    public static bool EsCostoPropio(object contenido) =>
        contenido is CostoDeVentaReconocidoV1 || contenido is DevolucionRegistradaV1 { Operation: OperacionDevolucionDeCliente };

    public const string OperacionDevolucionDeCliente = "DevolucionDeCliente";

    /// <summary>¿Lleva el documento su parte comercial? Sólo entonces su costo propio se puede diferir.</summary>
    public static bool TieneParteComercial(IReadOnlyList<object> contenidos) =>
        contenidos.Any(c => c is VentaFacturadaV1 or VentaFacturadaV2 or NotaCreditoEmitidaV1 or NotaDebitoEmitidaV1);

    /// <summary>
    /// Parte los contenidos del documento en los que viajan ahora y los que esperan el cierre: su costo propio si el documento lo difiere
    /// (y lleva su parte comercial) y los ajustes de costo de documentos que lo difirieron.
    /// </summary>
    public async Task<(IReadOnlyList<object> Emitir, IReadOnlyList<CostoDiferido> Diferir)> SepararAsync(
        InventoryDocument documento, IReadOnlyList<object> contenidos, CancellationToken ct)
    {
        var propio = documento.CostAtPeriodClose && TieneParteComercial(contenidos);
        var afectados = contenidos.OfType<AjusteDeCostoReconocidoV1>().Select(a => a.AffectedDocument.PublicId)
            .Where(id => id != documento.PublicId).Distinct().ToList();
        var diferidosAfectados = afectados.Count == 0
            ? new Dictionary<Guid, InventoryDocument>()
            : await db.InventoryDocuments.Where(d => afectados.Contains(d.PublicId) && d.CostAtPeriodClose).ToDictionaryAsync(d => d.PublicId, ct);

        var emitir = new List<object>(contenidos.Count);
        var diferir = new List<CostoDiferido>();
        foreach (var c in contenidos)
        {
            if (propio && EsCostoPropio(c)) diferir.Add(new CostoDiferido(c, documento));
            else if (c is AjusteDeCostoReconocidoV1 ajuste
                     && (ajuste.AffectedDocument.PublicId == documento.PublicId ? documento.CostAtPeriodClose : diferidosAfectados.ContainsKey(ajuste.AffectedDocument.PublicId)))
                diferir.Add(new CostoDiferido(c, ajuste.AffectedDocument.PublicId == documento.PublicId ? documento : diferidosAfectados[ajuste.AffectedDocument.PublicId]));
            else emitir.Add(c);
        }
        return (emitir, diferir);
    }

    // ------------------------------------------------------------------------------------------------ guardar --

    /// <summary>Agrega a la unidad de trabajo una fila por contenido diferido, con la sucursal y el centro del sobre del documento. No guarda.</summary>
    public void Guardar(InventoryDocument fuente, OrigenDeEmision origen, IReadOnlyList<CostoDiferido> diferidos)
    {
        foreach (var d in diferidos)
        {
            var tipo = CatalogoDeMensajesV1.Buscar(d.Contenido.GetType())
                       ?? throw new InvalidOperationException($"{d.Contenido.GetType().Name} no está en el catálogo de mensajes.");
            db.DeferredCosts.Add(new DeferredCost
            {
                SourceDocument = fuente,
                SourceDocumentId = fuente.Id,
                CostDocument = d.DocumentoDelCosto,
                CostDocumentId = d.DocumentoDelCosto.Id,
                Year = (short)fuente.OperationDate.Year,
                Month = (byte)fuente.OperationDate.Month,
                OperationDate = fuente.OperationDate,
                ContentType = tipo.Type,
                ContentVersion = tipo.Version,
                ContentJson = JsonSerializer.Serialize(d.Contenido, d.Contenido.GetType(), Json),
                BranchPublicId = origen.BranchPublicId,
                CostCenterPublicId = origen.CostCenterPublicId,
            });
        }
    }

    /// <summary>
    /// El espejo de los costos propios que el anulado dejó para el cierre (los de su confirmación), con los importes con el signo contrario:
    /// la anulación los deja en su propio mes. Si el anulado no difirió nada, vacío.
    /// </summary>
    public async Task<IReadOnlyList<CostoDiferido>> EspejoDeAnulacionAsync(InventoryDocument anulado, CancellationToken ct)
    {
        if (!anulado.CostAtPeriodClose) return [];
        var filas = await db.DeferredCosts.AsNoTracking()
            .Where(d => d.SourceDocumentId == anulado.Id && d.CostDocumentId == anulado.Id)
            .OrderBy(d => d.Id).ToListAsync(ct);
        return filas.Select(f => Leer(f.ContentType, f.ContentVersion, f.ContentJson))
            .Where(c => c is not null)
            .Select(c => new CostoDiferido(Negar(c!), anulado))
            .ToList();
    }

    // -------------------------------------------------------------------------------------------- cierre del mes --

    /// <summary>
    /// Al cerrar el mes: suma lo que espera de ese mes en un envío y emite su <see cref="CostoDelPeriodoReconocidoV1"/> (en línea, origen el
    /// envío). Sin nada pendiente no crea nada y devuelve nulo. Agrega a la unidad de trabajo; no guarda.
    /// </summary>
    public async Task<PeriodCostPosting?> CerrarAsync(EmisorDeMensajes emisor, InventoryPeriod periodo, int version, DateOnly fin, Guid sucursalPrincipal, CancellationToken ct)
    {
        var pendientes = await db.DeferredCosts
            .Where(d => d.Year == periodo.Year && d.Month == periodo.Month && d.PeriodCostPostingId == null)
            .OrderBy(d => d.Id).ToListAsync(ct);
        if (pendientes.Count == 0) return null;

        var envio = new PeriodCostPosting
        {
            Period = periodo, PeriodId = periodo.Id, CloseVersion = version, IsReversal = false, OperationDate = fin, EntryCount = pendientes.Count,
        };
        db.PeriodCostPostings.Add(envio);
        foreach (var p in pendientes) p.PeriodCostPosting = envio;

        var contenido = new CostoDelPeriodoReconocidoV1
        {
            Year = periodo.Year, Month = periodo.Month, ClosingVersion = version, IsReversal = false,
            Parts = Sumar(pendientes.Select(Parte), fin, negar: false),
        };
        var emitidos = await emisor.EmitirAsync(new SolicitudDeEmision(
            Origen(envio, fin, sucursalPrincipal), ClavesDeEvento.Confirmacion, [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Online)), ct);
        envio.MessagePublicId = emitidos[0].PublicId;
        return envio;
    }

    /// <summary>
    /// Al reabrir el mes: reversa el último envío vigente (los mismos importes con el signo contrario, fechado igual, ligado al envío) y deja
    /// sus filas pendientes otra vez para el próximo cierre. Sin envío vigente no hace nada. Agrega a la unidad de trabajo; no guarda.
    /// </summary>
    public async Task<PeriodCostPosting?> ReabrirAsync(EmisorDeMensajes emisor, InventoryPeriod periodo, Guid sucursalPrincipal, CancellationToken ct)
    {
        var envio = await db.PeriodCostPostings
            .Where(e => e.PeriodId == periodo.Id && !e.IsReversal && !db.PeriodCostPostings.Any(r => r.ReversesId == e.Id))
            .OrderByDescending(e => e.CloseVersion).FirstOrDefaultAsync(ct);
        if (envio is null) return null;

        var incluidas = await db.DeferredCosts.Where(d => d.PeriodCostPostingId == envio.Id).OrderBy(d => d.Id).ToListAsync(ct);
        var reversion = new PeriodCostPosting
        {
            PeriodId = periodo.Id, CloseVersion = envio.CloseVersion, IsReversal = true, ReversesId = envio.Id, OperationDate = envio.OperationDate,
            EntryCount = incluidas.Count,
        };
        db.PeriodCostPostings.Add(reversion);
        foreach (var d in incluidas)
        {
            d.PeriodCostPostingId = null;
            d.PeriodCostPosting = null;
        }

        var contenido = new CostoDelPeriodoReconocidoV1
        {
            Year = periodo.Year, Month = periodo.Month, ClosingVersion = envio.CloseVersion, IsReversal = true,
            Parts = Sumar(incluidas.Select(Parte), envio.OperationDate, negar: true),
        };
        var numero = Numero(periodo.Year, periodo.Month);
        var emitidos = await emisor.EmitirAsync(new SolicitudDeEmision(
            Origen(reversion, envio.OperationDate, sucursalPrincipal), ClavesDeEvento.Confirmacion, [contenido],
            new ModoDeEntrega.Sellado(DeliveryMode.Online),
            Relacionado: new DocumentoRelacionado(envio.PublicId, ClaseDelEnvio, numero)), ct);
        reversion.MessagePublicId = emitidos[0].PublicId;
        return reversion;
    }

    /// <summary>La «clase» con que el envío aparece como relacionado de su reversión.</summary>
    public const string ClaseDelEnvio = "PeriodCost";

    private static string Numero(int anio, int mes) => $"{anio:0000}-{mes:00}";

    private static OrigenDeEmision Origen(PeriodCostPosting envio, DateOnly fecha, Guid sucursal) =>
        new(MessageOriginKind.Operation, envio.PublicId, null, null, Numero(fecha.Year, fecha.Month), fecha, sucursal);

    private static ParteDiferida Parte(DeferredCost d) =>
        new(d.ContentType, d.ContentVersion, d.BranchPublicId, d.CostCenterPublicId, Leer(d.ContentType, d.ContentVersion, d.ContentJson));

    // ------------------------------------------------------------------------------------------------- puros --

    /// <summary>Una fila leída: el tipo, la sucursal, el centro y el contenido tipado (nulo si el tipo ya no está en el catálogo).</summary>
    public sealed record ParteDiferida(string Tipo, int Version, Guid Sucursal, Guid? CentroDeCosto, object? Contenido);

    /// <summary>El contenido tipado de una fila.</summary>
    public static object? Leer(string tipo, int version, string json)
    {
        var registro = CatalogoDeMensajesV1.Todos.FirstOrDefault(t => t.Type == tipo && t.Version == version);
        return registro is null ? null : JsonSerializer.Deserialize(json, registro.Record, Json);
    }

    /// <summary>El mismo contenido con cantidades e importes con el signo contrario.</summary>
    public static object Negar(object contenido) => contenido switch
    {
        CostoDeVentaReconocidoV1 c => c with { Lines = c.Lines.Select(Negar).ToList() },
        DevolucionRegistradaV1 d => d with { Lines = d.Lines.Select(Negar).ToList() },
        AjusteDeCostoReconocidoV1 a => a with
        {
            Lines = a.Lines.Select(l => l with { InventoryAmount = -l.InventoryAmount, SoldAmount = -l.SoldAmount }).ToList(),
        },
        _ => throw new ArgumentException($"{contenido.GetType().Name} no es un contenido de costo.", nameof(contenido)),
    };

    private static CostLineV1 Negar(CostLineV1 l) => l with { QuantityBase = -l.QuantityBase, Cost = -l.Cost };

    /// <summary>
    /// Suma las filas en partes: una por tipo, operación (o motivo del ajuste), sucursal y centro de costo, con las líneas iguales sumadas
    /// (grupo contable, bodega, comportamiento, sucursal de la línea y movimiento) y sin las líneas del documento. Los ajustes quedan fechados
    /// al fin del mes. Con <paramref name="negar"/>, la reversión. Las partes en cero no salen.
    /// </summary>
    public static IReadOnlyList<DeferredCostPartV1> Sumar(IEnumerable<ParteDiferida> filas, DateOnly fin, bool negar)
    {
        var partes = new List<DeferredCostPartV1>();
        var leidas = filas.Where(f => f.Contenido is not null).ToList();

        foreach (var g in leidas.Where(f => f.Contenido is CostoDeVentaReconocidoV1)
                     .GroupBy(f => (f.Sucursal, f.CentroDeCosto, Operacion: ((CostoDeVentaReconocidoV1)f.Contenido!).Operation)))
        {
            var lineas = SumarLineas(g.SelectMany(f => ((CostoDeVentaReconocidoV1)f.Contenido!).Lines), negar);
            if (lineas.Count > 0)
                partes.Add(Parte(CostoDeVentaReconocidoV1.Type, g.Key.Sucursal, g.Key.CentroDeCosto,
                    new CostoDeVentaReconocidoV1 { Operation = g.Key.Operacion, Lines = lineas }));
        }

        foreach (var g in leidas.Where(f => f.Contenido is DevolucionRegistradaV1)
                     .GroupBy(f => (f.Sucursal, f.CentroDeCosto, Operacion: ((DevolucionRegistradaV1)f.Contenido!).Operation)))
        {
            var lineas = SumarLineas(g.SelectMany(f => ((DevolucionRegistradaV1)f.Contenido!).Lines), negar);
            if (lineas.Count > 0)
                partes.Add(Parte(DevolucionRegistradaV1.Type, g.Key.Sucursal, g.Key.CentroDeCosto,
                    new DevolucionRegistradaV1 { Operation = g.Key.Operacion, Lines = lineas }));
        }

        foreach (var g in leidas.Where(f => f.Contenido is AjusteDeCostoReconocidoV1)
                     .GroupBy(f => (f.Sucursal, f.CentroDeCosto, ((AjusteDeCostoReconocidoV1)f.Contenido!).Operation, ((AjusteDeCostoReconocidoV1)f.Contenido!).Reason)))
        {
            var signo = negar ? -1m : 1m;
            var lineas = g.SelectMany(f => ((AjusteDeCostoReconocidoV1)f.Contenido!).Lines)
                .GroupBy(l => (l.AccountingGroupCode, l.WarehouseCode, l.WarehouseBehavior))
                .Select(x => new CostDifferenceLineV1
                {
                    AccountingGroupCode = x.Key.AccountingGroupCode, WarehouseCode = x.Key.WarehouseCode, WarehouseBehavior = x.Key.WarehouseBehavior,
                    InventoryAmount = signo * x.Sum(l => l.InventoryAmount), SoldAmount = signo * x.Sum(l => l.SoldAmount),
                })
                .Where(l => l.InventoryAmount != 0m || l.SoldAmount != 0m)
                .OrderBy(l => l.AccountingGroupCode, StringComparer.Ordinal).ThenBy(l => l.WarehouseCode, StringComparer.Ordinal)
                .ToList();
            if (lineas.Count > 0)
                partes.Add(Parte(AjusteDeCostoReconocidoV1.Type, g.Key.Sucursal, g.Key.CentroDeCosto,
                    new AjusteDeCostoReconocidoV1 { Operation = g.Key.Operation, Reason = g.Key.Reason, EffectiveDate = fin, Lines = lineas }));
        }

        return partes;
    }

    private static DeferredCostPartV1 Parte(string tipo, Guid sucursal, Guid? centro, object contenido) =>
        new() { Type = tipo, Version = 1, BranchPublicId = sucursal, CostCenterPublicId = centro, Content = contenido };

    private static List<CostLineV1> SumarLineas(IEnumerable<CostLineV1> lineas, bool negar)
    {
        var signo = negar ? -1m : 1m;
        return lineas
            .GroupBy(l => (l.AccountingGroupCode, l.WarehouseCode, l.WarehouseBehavior, l.BranchPublicId, l.Movement))
            .Select(x => new CostLineV1
            {
                AccountingGroupCode = x.Key.AccountingGroupCode, WarehouseCode = x.Key.WarehouseCode, WarehouseBehavior = x.Key.WarehouseBehavior,
                BranchPublicId = x.Key.BranchPublicId, Movement = x.Key.Movement,
                QuantityBase = signo * x.Sum(l => l.QuantityBase), Cost = signo * x.Sum(l => l.Cost),
            })
            .Where(l => l.Cost != 0m)
            .OrderBy(l => l.AccountingGroupCode, StringComparer.Ordinal).ThenBy(l => l.WarehouseCode, StringComparer.Ordinal)
            .ToList();
    }
}
