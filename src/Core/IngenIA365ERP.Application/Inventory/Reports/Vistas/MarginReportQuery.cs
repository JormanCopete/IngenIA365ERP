using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>margin</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T960; FR-086; contracts/api.md §27): ventas netas (ventas −
/// notas, sin impuestos) contra el costo de venta del kardex de las clases de venta, abierta por <c>by=product|category|salesperson|customer|point</c>
/// (producto por defecto), con Dimensión, Ventas netas, Costo de venta, Margen y Margen (%) y una fila de totales. Los cálculos son los de
/// <see cref="AnaliticaDeInventario.LineasDeVentaAsync"/>, los mismos de la ficha <c>grossMargin</c> del tablero. Exige
/// <c>Inventory.Costs.Read</c> (sin él, el 404 genérico); con <c>by=customer</c> trae datos de clientes, así que exportarla exige además
/// <c>Inventory.Reports.ExportPersonalData</c> (lo decide la ruta con <see cref="VistaDeInformeDeInventario.PersonalDataWhen"/>). Alcance por
/// bodega y punto de venta (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record MarginReportQuery(FiltrosDeInformeDeInventario Filtros, string? By = null) : IRequest<Result<TablaExportable>>;

public sealed class MarginReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    IDateTimeService reloj)
    : IRequestHandler<MarginReportQuery, Result<TablaExportable>>
{
    public const string PorProducto = "product";
    public const string PorCategoria = "category";
    public const string PorVendedor = "salesperson";
    public const string PorCliente = "customer";
    public const string PorPunto = "point";

    public static readonly IReadOnlyList<string> Dimensiones = [PorProducto, PorCategoria, PorVendedor, PorCliente, PorPunto];

    public const string SinCategoria = "Sin categoría";
    public const string SinVendedor = "Sin vendedor";
    public const string SinCliente = "Sin cliente";
    public const string SinPunto = "Sin punto de venta";

    /// <summary>Lo que la vista declara al publicarse (T966): exige costos; datos personales con <c>by=customer</c>.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "margin", "Margen", "Ventas netas contra el costo de venta por producto, categoría, vendedor, cliente o punto de venta.",
        "margen", ["from", "to", "branch", "warehouse", "pointOfSale", "product", "category"], ["by"],
        PersonalDataWhen: "by=customer", RequiredPermission: AnaliticaDeInventario.PermisoDeCostos);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión", TipoDeColumna.Texto),
        new("Ventas netas", TipoDeColumna.Moneda),
        new("Costo de venta", TipoDeColumna.Moneda),
        new("Margen", TipoDeColumna.Moneda),
        new("Margen (%)", TipoDeColumna.Porcentaje),
    ];

    public async Task<Result<TablaExportable>> Handle(MarginReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);
        var by = string.IsNullOrWhiteSpace(request.By) ? PorProducto : request.By.Trim();
        var dimension = Dimensiones.FirstOrDefault(d => string.Equals(d, by, StringComparison.OrdinalIgnoreCase));
        if (dimension is null) return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("by", request.By, Dimensiones));

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);

        var lineas = await analitica.LineasDeVentaAsync(ambito.Value, f.Desde(hoy), f.Hasta(hoy), ct);
        var nombres = await NombresAsync(dimension, lineas, alcanceDeLaPeticion, ct);

        var filas = lineas
            .GroupBy(nombres)
            .Select(g => Fila(g.Key, g.Sum(l => l.VentaNeta), g.Sum(l => l.CostoDeVenta)))
            .OrderBy(x => (string)x.Valores[0]!, StringComparer.CurrentCulture)
            .ToList();
        var totales = Fila("Total", lineas.Sum(l => l.VentaNeta), lineas.Sum(l => l.CostoDeVenta));
        return Result.Success(new TablaExportable("Margen", $"{DatosDeVentasYCaja.Rango(f, hoy)} · por {Nombre(dimension)} · {ambito.Value.Descripcion}",
            Columnas, filas, totales,
        [
            "Ventas netas = ventas confirmadas (sin impuestos, con sus descuentos) − notas crédito y de ajuste; costo de venta = lo que salió del kardex por ellas.",
            "La factura desde remisiones toma el costo de las remisiones que factura. Margen (%) = margen ÷ ventas netas.",
        ]));
    }

    private static FilaExportable Fila(string dimension, decimal venta, decimal costo) =>
        new([dimension, venta, costo, venta - costo, AnaliticaDeInventario.PorcentajeDeMargen(venta, costo)]);

    private static string Nombre(string dimension) => dimension switch
    {
        PorCategoria => "categoría",
        PorVendedor => "vendedor",
        PorCliente => "cliente",
        PorPunto => "punto de venta",
        _ => "producto",
    };

    /// <summary>El nombre de la dimensión de cada línea.</summary>
    private async Task<Func<LineaDeMargen, string>> NombresAsync(
        string dimension, IReadOnlyList<LineaDeMargen> lineas, IAlcanceDeInventario alcance, CancellationToken ct)
    {
        switch (dimension)
        {
            case PorCategoria:
            {
                var productos = await analitica.ProductosAsync(lineas.Select(l => l.ProductId), ct);
                var ids = productos.Values.Select(p => p.CategoryId).Distinct().ToList();
                var categorias = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => $"{c.Code} · {c.Name}", ct);
                return l => categorias.GetValueOrDefault(productos[l.ProductId].CategoryId) ?? SinCategoria;
            }
            case PorVendedor:
            {
                var ids = lineas.Select(l => l.SalespersonId).OfType<int>().Distinct().ToList();
                var vendedores = (await (from s in db.Salespeople.AsNoTracking().IgnoreQueryFilters()
                                         where ids.Contains(s.Id)
                                         join p in db.People.AsNoTracking().IgnoreQueryFilters() on s.PersonId equals p.Id
                                         select new { s.Id, p.BusinessName, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName }).ToListAsync(ct))
                    .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.BusinessName)
                        ? string.Join(' ', new[] { s.FirstName, s.OtherNames, s.LastName, s.SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
                        : s.BusinessName);
                return l => l.SalespersonId is int id && vendedores.TryGetValue(id, out var nombre) ? nombre : SinVendedor;
            }
            case PorCliente:
            {
                var clientes = await new DatosDeVentasYCaja(db, alcance).ClientesAsync(lineas.Select(l => (l.DocumentId, l.PersonId)).Distinct().ToList(), ct);
                return l => clientes.GetValueOrDefault(l.DocumentId) ?? SinCliente;
            }
            case PorPunto:
            {
                var ids = lineas.Select(l => l.PointOfSaleId).OfType<int>().Distinct().ToList();
                var puntos = await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => $"{p.Code} · {p.Name}", ct);
                return l => l.PointOfSaleId is int id && puntos.TryGetValue(id, out var nombre) ? nombre : SinPunto;
            }
            default:
            {
                var productos = await analitica.ProductosAsync(lineas.Select(l => l.ProductId), ct);
                return l => productos[l.ProductId].Texto;
            }
        }
    }
}
