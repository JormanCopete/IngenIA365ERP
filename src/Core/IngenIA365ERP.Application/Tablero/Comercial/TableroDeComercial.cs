using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Application.Common.Approvals.ListMyPendingApprovals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Comercial;

// 2026-11-17 (pedido del dueño): las cifras de Comercial e inventario del tablero de inicio. Las ventas son las mismas del tablero de
// Inventario (/inventario/tablero): líneas de los documentos de venta confirmados menos las devoluciones, a su valor neto, de los
// documentos que el usuario ve por su bodega o su punto de venta. «Solo lo mío» = los que confirmó el usuario (el cajero en el POS).

/// <summary>La consulta común de ventas del tablero: venta neta por día en un rango, con el alcance y, si se pide, sólo lo del usuario.</summary>
public sealed class VentasDelTablero(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
{
    public const string Permiso = "Inventory.Dashboard.View";
    public const string Grupo = "Comercial";

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> PorDiaAsync(ContextoDeIndicador contexto, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var clases = DatosDeVentasYCaja.ClasesDeVenta.Concat(DatosDeVentasYCaja.ClasesDeDevolucion).ToList();
        var devoluciones = DatosDeVentasYCaja.ClasesDeDevolucion.ToList();
        var documentos = db.InventoryDocuments.AsNoTracking()
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking())
            .Where(d => d.Status == DocumentStatus.Confirmed && clases.Contains(d.Class) && d.OperationDate >= desde && d.OperationDate <= hasta);
        if (contexto.SoloLoMio)
        {
            if (contexto.UsuarioId is not int usuario) return new Dictionary<DateOnly, decimal>();
            documentos = documentos.Where(d => d.ConfirmedByUserId == usuario);
        }

        var filas = await (from l in db.InventoryDocumentLines.AsNoTracking()
                           where !l.IsDeleted
                           join d in documentos on l.DocumentId equals d.Id
                           group l.NetAmount by new { d.OperationDate, d.Class } into g
                           select new { g.Key.OperationDate, g.Key.Class, Neto = g.Sum() })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.OperationDate)
            .ToDictionary(g => g.Key, g => g.Sum(f => devoluciones.Contains(f.Class) ? -f.Neto : f.Neto));
    }
}

/// <summary>Ventas del día contra ayer.</summary>
public sealed class VentasDelDia(IApplicationDbContext db, IAlcanceDeInventario alcance) : IIndicadorDelTablero
{
    private readonly VentasDelTablero ventas = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "ventas.del-dia", "Ventas del día", VentasDelTablero.Grupo, VentasDelTablero.Permiso, AdmiteSoloLoMio: true, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Moneda, "/inventario/informes?vista=sales-by-register",
        "Venta neta de hoy (facturas, POS y notas débito menos devoluciones), comparada con la de ayer.", PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var hoy = contexto.Hoy;
        var porDia = await ventas.PorDiaAsync(contexto, hoy.AddDays(-1), hoy, ct);
        return Result.Success(new ResultadoDeIndicador(porDia.GetValueOrDefault(hoy), porDia.GetValueOrDefault(hoy.AddDays(-1)), "ayer"));
    }
}

/// <summary>Ventas del mes en curso contra el mismo tramo del mes anterior.</summary>
public sealed class VentasDelMes(IApplicationDbContext db, IAlcanceDeInventario alcance) : IIndicadorDelTablero
{
    private readonly VentasDelTablero ventas = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "ventas.del-mes", "Ventas del mes", VentasDelTablero.Grupo, VentasDelTablero.Permiso, AdmiteSoloLoMio: true, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Moneda, "/inventario/informes?vista=sales-by-register",
        "Venta neta del mes hasta hoy, comparada con el mes anterior hasta el mismo día.", PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var hoy = contexto.Hoy;
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1);
        var anteriorInicio = inicio.AddMonths(-1);
        var anteriorFin = hoy.AddMonths(-1);
        var esteMes = (await ventas.PorDiaAsync(contexto, inicio, hoy, ct)).Values.Sum();
        var mesAnterior = (await ventas.PorDiaAsync(contexto, anteriorInicio, anteriorFin, ct)).Values.Sum();
        return Result.Success(new ResultadoDeIndicador(esteMes, mesAnterior, "mes anterior"));
    }
}

/// <summary>Ventas de los últimos 14 días, en barras.</summary>
public sealed class VentasUltimosDias(IApplicationDbContext db, IAlcanceDeInventario alcance) : IIndicadorDelTablero
{
    private readonly VentasDelTablero ventas = new(db, alcance);

    public const int Dias = 14;

    public DefinicionDeIndicador Definicion { get; } = new(
        "ventas.ultimos-14-dias", "Ventas de los últimos 14 días", VentasDelTablero.Grupo, VentasDelTablero.Permiso, AdmiteSoloLoMio: true,
        TipoDeIndicador.Serie, FormatoDeIndicador.Moneda, "/inventario/informes?vista=sales-by-register",
        "Venta neta por día de las últimas dos semanas; la cifra es el total.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var desde = contexto.Hoy.AddDays(-(Dias - 1));
        var porDia = await ventas.PorDiaAsync(contexto, desde, contexto.Hoy, ct);
        var serie = Enumerable.Range(0, Dias).Select(i => desde.AddDays(i))
            .Select(d => new PuntoDeSerie(d.ToString("dd/MM"), porDia.GetValueOrDefault(d))).ToList();
        return Result.Success(new ResultadoDeIndicador(serie.Sum(p => p.Valor), Serie: serie));
    }
}

/// <summary>Productos bajo su punto de reorden y en quiebre, con la misma evaluación del informe <c>reorder-alerts</c>.</summary>
public sealed class ProductosBajoElPuntoDeReorden(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, EvaluacionDeReposicion evaluacion)
    : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "inventario.bajo-reorden", "Productos bajo el punto de reorden", "Inventario", VentasDelTablero.Permiso, AdmiteSoloLoMio: false,
        TipoDeIndicador.Cifra, FormatoDeIndicador.Numero, "/inventario/informes?vista=reorder-alerts",
        "Productos y bodegas cuya existencia está en o bajo el punto de reorden; la nota dice cuántos están en quiebre.", PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var politicas = db.ReorderPolicies.AsNoTracking().PorBodega(alcance, r => r.WarehouseId);
        var reposicion = await evaluacion.EvaluarAsync(politicas, ct);
        var bajo = reposicion.Count(x => x.Resultado.RequiereReorden);
        var quiebres = reposicion.Count(x => x.Resultado.Quiebre);
        return Result.Success(new ResultadoDeIndicador(bajo, Nota: quiebres == 0 ? "Ninguno en quiebre" : $"{quiebres} en quiebre (sin existencia)"));
    }
}

/// <summary>Valor del inventario a hoy (con tránsito), el mismo del informe «Valorizado». Exige ver costos.</summary>
public sealed class ValorDelInventario(IAlcanceDeInventario alcanceDeLaPeticion, AnaliticaDeInventario analitica) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "inventario.valor", "Valor del inventario", "Inventario", AnaliticaDeInventario.PermisoDeCostos, AdmiteSoloLoMio: false,
        TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/inventario/informes?vista=valuation",
        "Existencias valorizadas al costo promedio a hoy, incluida la mercancía en tránsito, en las bodegas que usted ve.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(new FiltrosDeInformeDeInventario(), alcance, ct);
        if (ambito.IsFailure) return Result.Failure<ResultadoDeIndicador>(ambito.Error);
        var valor = (await analitica.ExistenciasAsync(ambito.Value, contexto.Hoy, sinTransito: false, ct)).Sum(x => x.Value);
        return Result.Success(new ResultadoDeIndicador(valor));
    }
}

/// <summary>
/// Las aprobaciones de Comercial e inventario (compras, ajustes, traslados, descuentos) que el usuario puede decidir ahora: la misma bandeja de
/// <c>/inventario/aprobaciones</c>, con su nivel, su permiso y su alcance.
/// </summary>
public sealed class AprobacionesDeInventarioParaMi(ISender sender) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "inventario.aprobaciones-para-mi", "Aprobaciones de inventario esperando por usted", "Inventario", "Inventory.Approvals.View",
        "/inventario/aprobaciones");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var r = await sender.Send(new ListMyPendingApprovalsQuery(Mine: true, PageSize: 1), ct);
        return r.IsSuccess ? (int)r.Value.TotalCount : 0;
    }
}
