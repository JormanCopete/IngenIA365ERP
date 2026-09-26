using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Inventory.GoLive;
using NSubstitute;
using F = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeCifrasDeSolido;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, T302 (US4-6; api.md §27): los comparativos con SOLIDO. <c>legacy-comparison-valuation</c> a una fecha da, por
/// grupo, bodega y producto, cantidad y valor de SOLIDO (lote más reciente del par fecha–bodega) contra el valorizado del módulo y
/// sus diferencias, con las cifras sin producto resuelto sumadas a su grupo y nunca por producto; <c>legacy-comparison-kardex</c>
/// exige la bodega y, sin <c>Inventory.Costs.Read</c>, deja vacías las columnas de valor.
/// </summary>
public class ComparativosConSolidoTests
{
    private static readonly string[] Encabezados = [F.Fecha, F.Bodega, F.Producto, F.Cantidad, F.Valor, F.GrupoContable];

    private static int Columna(TablaExportable tabla, string titulo) =>
        tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i;

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) => fila.Valores[Columna(tabla, titulo)];

    private static int Oculta(TablaExportable tabla, string clave) => tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Clave == clave).i;

    /// <summary>B3 con su saldo confirmado (P1: 10 a 1.500) y dos cargas de SOLIDO al corte; manda la más reciente.</summary>
    private static async Task<PuestaEnMarchaDePrueba> EscenarioAsync()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);
        await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados, ["2026-09-10", "B3", "P1", "99", "1", null]);
        var r = await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados,
            ["2026-09-10", "B3", "P1", "12", "18500", null],
            ["2026-09-10", "B3", "OBSOL-77", "3", "45000", "ABARR"]);
        r.IsSuccess.Should().BeTrue();
        return p;
    }

    private static FiltrosDeInformeDeInventario Filtros(PuestaEnMarchaDePrueba p) =>
        new() { AsOf = PuestaEnMarchaDePrueba.Corte, Warehouse = p.B3.PublicId };

    private static LegacyComparisonValuationQueryHandler Valorizado(PuestaEnMarchaDePrueba p) =>
        new(p.Db, p.K.Alcance, p.K.Permisos, new ValorizadoALaFecha(p.Db, p.K.Lector()), p.K.C.Reloj);

    private static LegacyComparisonKardexQueryHandler Kardex(PuestaEnMarchaDePrueba p) =>
        new(p.Db, p.K.Alcance, p.K.Permisos, new ValorizadoALaFecha(p.Db, p.K.Lector()), p.K.C.Reloj);

    [Fact]
    public async Task El_valorizado_compara_SOLIDO_del_lote_mas_reciente_con_el_modulo_y_lo_sin_producto_suma_al_grupo()
    {
        var p = await EscenarioAsync();

        var r = await Valorizado(p).Handle(new LegacyComparisonValuationQuery(Filtros(p)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var t = r.Value;
        t.Filas.Should().HaveCount(2);
        var p1 = t.Filas.Single(f => (string?)f.Valores[Oculta(t, "_producto")] is not null);
        Celda(t, p1, "Cantidad SOLIDO").Should().Be(12m, "el lote más reciente del par (fecha, bodega)");
        Celda(t, p1, "Valor SOLIDO").Should().Be(18_500m);
        Celda(t, p1, "Cantidad módulo").Should().Be(10m);
        Celda(t, p1, "Valor módulo").Should().Be(15_000m);
        Celda(t, p1, "Diferencia (cantidad)").Should().Be(2m);
        Celda(t, p1, "Diferencia (valor)").Should().Be(3_500m);

        var sinProducto = t.Filas.Single(f => f.Valores[Oculta(t, "_producto")] is null);
        ((string)Celda(t, sinProducto, "Producto")!).Should().StartWith("Sin producto en el catálogo");
        ((string)Celda(t, sinProducto, "Grupo")!).Should().StartWith("ABARR");
        Celda(t, sinProducto, "Valor SOLIDO").Should().Be(45_000m);
        t.Totales!.Valores[Columna(t, "Valor SOLIDO")].Should().Be(63_500m);
    }

    [Fact]
    public async Task El_valorizado_exige_ver_costos()
    {
        var p = await EscenarioAsync();
        p.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var r = await Valorizado(p).Handle(new LegacyComparisonValuationQuery(Filtros(p)), default);

        r.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task El_kardex_exige_la_bodega_y_sin_ver_costos_deja_vacios_los_valores()
    {
        var p = await EscenarioAsync();

        var sinBodega = await Kardex(p).Handle(new LegacyComparisonKardexQuery(new FiltrosDeInformeDeInventario()), default);
        sinBodega.IsFailure.Should().BeTrue();
        sinBodega.Error.Code.Should().Be(Error.Validation.Code);

        var conCostos = await Kardex(p).Handle(new LegacyComparisonKardexQuery(Filtros(p)), default);
        var fila = conCostos.Value.Filas.Should().ContainSingle().Subject;
        Celda(conCostos.Value, fila, "Fecha").Should().Be(PuestaEnMarchaDePrueba.Corte);
        Celda(conCostos.Value, fila, "Cantidad SOLIDO").Should().Be(12m);
        Celda(conCostos.Value, fila, "Cantidad módulo").Should().Be(10m);
        Celda(conCostos.Value, fila, "Diferencia (valor)").Should().Be(3_500m);

        p.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        var sinCostos = await Kardex(p).Handle(new LegacyComparisonKardexQuery(Filtros(p)), default);
        var vacia = sinCostos.Value.Filas.Single();
        Celda(sinCostos.Value, vacia, "Valor SOLIDO").Should().BeNull();
        Celda(sinCostos.Value, vacia, "Valor módulo").Should().BeNull();
        Celda(sinCostos.Value, vacia, "Cantidad SOLIDO").Should().Be(12m);
        sinCostos.Value.Notas.Should().Contain(n => n.Contains("Inventory.Costs.Read"));
    }
}
