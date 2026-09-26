using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, T277 (US17-1, SC-017; contracts/api.md §27): la vista <c>valuation</c>. A una fecha pasada es el último cierre
/// vigente anterior más el kardex posterior, igual al valorizado fijado al cerrar; el tránsito sólo con <c>includeTransit</c>; el
/// grupo contable es el de la fecha, no el de hoy; el valor por bodega es cantidad × promedio del ámbito con el residuo por
/// <c>Redondeo.Residuo</c>; y sin <c>Inventory.Costs.Read</c> la vista responde el 404 genérico.
/// </summary>
public class ValuationReportQueryTests
{
    private static readonly DateOnly Julio10 = new(2026, 7, 10);
    private static readonly DateOnly Julio31 = new(2026, 7, 31);
    private static readonly DateOnly Agosto5 = new(2026, 8, 5);

    private static int Columna(TablaExportable tabla, string titulo) =>
        tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i;

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) => fila.Valores[Columna(tabla, titulo)];

    [Fact]
    public async Task A_la_fecha_de_un_cierre_coincide_con_el_valorizado_fijado_y_despues_suma_el_kardex_posterior()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 5m, 1300m, p.K.Segunda);
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();
        await p.AjusteAsync("AJN", Agosto5, p.K.P1, 4m);

        var alCierre = (await p.ValorizadoAsync(Julio31)).Value;
        var hoy = (await p.ValorizadoAsync()).Value;

        // Las filas van por grupo, bodega (B2 antes que PRIN) y producto.
        var fijado = await p.K.C.Db.PeriodClosingBalances.OrderByDescending(b => b.WarehouseId).Select(b => b.Value).ToListAsync();
        alCierre.Filas.Select(f => (decimal)Celda(alCierre, f, "Valor")!).Should().Equal(fijado);
        alCierre.Filas.Select(f => (decimal)Celda(alCierre, f, "Cantidad")!).Should().Equal(5m, 10m);

        // Después: la salida de 4 al promedio 1.100 deja 11 × 1.100 = 12.100 en toda la cooperativa.
        hoy.Filas.Select(f => (decimal)Celda(hoy, f, "Cantidad")!).Should().Equal(5m, 6m);
        hoy.Filas.Sum(f => (decimal)Celda(hoy, f, "Valor")!).Should().Be(12100m);
        var costo = await p.K.C.Db.CostStates.SingleAsync(c => c.ProductId == p.K.ProductoId(p.K.P1));
        hoy.Filas.Sum(f => (decimal)Celda(hoy, f, "Valor")!).Should().Be(costo.Value);
        hoy.Filas.Should().OnlyContain(f => (decimal)Celda(hoy, f, "Costo promedio")! == 1100m);
    }

    [Fact]
    public async Task El_residuo_del_valor_por_bodega_va_segun_Redondeo_Residuo()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 1m, 100m);
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 1m, 100.01m, p.K.Segunda);

        // 200,01 / 2 = 100,005: cada bodega redondea a 100,01 y sobra un centavo que se descuenta donde diga la regla: en la
        // de mayor valor (empatan: la primera, PRIN) o en la última (B2). Las filas salen B2, PRIN.
        var mayorValor = (await p.ValorizadoAsync()).Value;
        mayorValor.Filas.Select(f => ((string)Celda(mayorValor, f, "Bodega")!, (decimal)Celda(mayorValor, f, "Valor")!))
            .Should().Equal(("B2", 100.01m), ("PRIN", 100.00m));

        p.K.Parametro(ParametrosDeInventario.RedondeoResiduo, "UltimaLinea");
        var ultimaLinea = (await p.ValorizadoAsync()).Value;
        ultimaLinea.Filas.Select(f => ((string)Celda(ultimaLinea, f, "Bodega")!, (decimal)Celda(ultimaLinea, f, "Valor")!))
            .Should().Equal(("B2", 100.00m), ("PRIN", 100.01m));
    }

    [Fact]
    public async Task El_grupo_contable_es_el_de_la_fecha_y_no_el_de_hoy()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var aseo = new AccountingGroup { Code = "ASEO", Name = "Aseo", IsActive = true };
        p.K.C.Db.AccountingGroups.Add(aseo);
        await p.K.C.Db.SaveChangesAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        (await p.ReclasificarAsync(p.K.P1, aseo.PublicId)).IsSuccess.Should().BeTrue();

        var antes = (await p.ValorizadoAsync(Julio31)).Value;
        var hoy = (await p.ValorizadoAsync()).Value;
        var porGrupo = (await p.ValorizadoAsync(Julio31, grupo: p.K.C.GrupoAbarrotes.PublicId)).Value;

        ((string)Celda(antes, antes.Filas.Single(), "Grupo contable")!).Should().StartWith("ABARR");
        ((string)Celda(hoy, hoy.Filas.Single(), "Grupo contable")!).Should().StartWith("ASEO");
        porGrupo.Filas.Should().ContainSingle("al 31 de julio el producto estaba en abarrotes");
    }

    [Fact]
    public async Task El_transito_sale_solo_con_includeTransit_y_en_sus_columnas()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        // Una unidad viajando: lo que dejaría un despacho (US10), escrito aquí directo para la prueba.
        var producto = p.K.ProductoId(p.K.P1);
        var ubicacion = p.K.Transito.Locations.Single().Id;
        var documento = await p.K.C.Db.KardexEntries.Select(e => e.DocumentId).FirstAsync();
        var linea = await p.K.C.Db.KardexEntries.Select(e => e.DocumentLineId).FirstAsync();
        p.K.C.Db.KardexEntries.AddRange(
            new KardexEntry { DocumentId = documento, DocumentLineId = linea, ProductId = producto, WarehouseId = p.K.Principal.Id, LocationId = p.K.Principal.Locations.Single().Id,
                OperationDate = Julio10, Kind = KardexEntryKind.Exit, Reason = KardexReason.Normal, QuantityBase = -1m, UnitCost = 1000m, TotalCost = -1000m },
            new KardexEntry { DocumentId = documento, DocumentLineId = linea, ProductId = producto, WarehouseId = p.K.Transito.Id, LocationId = ubicacion,
                OperationDate = Julio10, Kind = KardexEntryKind.Entry, Reason = KardexReason.Normal, QuantityBase = 1m, UnitCost = 1000m, TotalCost = 1000m });
        await p.K.C.Db.SaveChangesAsync();

        var sin = (await p.ValorizadoAsync()).Value;
        var con = (await p.ValorizadoAsync(conTransito: true)).Value;

        sin.Filas.Select(f => (string)Celda(sin, f, "Bodega")!).Should().Equal("PRIN");
        con.Filas.Should().HaveCount(2);
        var transito = con.Filas.Single(f => (string)Celda(con, f, "Bodega")! == "TR01");
        Celda(con, transito, "Cantidad").Should().BeNull();
        Celda(con, transito, "En tránsito (cantidad)").Should().Be(1m);
        Celda(con, transito, "En tránsito (valor)").Should().Be(1000m);
    }

    [Fact]
    public async Task Sin_permiso_de_costos_la_vista_es_404()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        p.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var r = await p.ValorizadoAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(Error.NotFound);
    }
}
