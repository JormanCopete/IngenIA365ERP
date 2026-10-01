using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T385 (FR-040, US11-2; contracts/api.md §12, <c>POST /counts/{id}/captures</c>): tres lecturas del mismo código
/// cuentan 3; el código de una caja suma su factor en unidad base; una lectura negativa corrige; cada lectura se acepta o se rechaza
/// sola (<c>Inventory.Barcode.NotFound</c>, <c>Inventory.Count.ProductNotInScope</c>); en un conteo total o por ubicación un producto
/// sin teórico entra con teórico 0 (<c>AddedDuringCapture</c>); un contador no declarado es <c>.CounterNotAssigned</c>; un conteo no
/// abierto <c>.NotOpen</c>; y en un conteo ciego quien sólo captura no ve teórico, diferencia ni valor (valores con
/// <c>Inventory.Costs.Read</c>).
/// </summary>
public class CapturarConteoTests
{
    private static CountReadRequest Codigo(string barras, decimal? cantidad = null) => new(barras, null, null, cantidad, null);

    [Fact]
    public async Task Tres_lecturas_del_mismo_codigo_cuentan_tres_y_la_caja_suma_su_factor()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.P3, 20m, 800m);
        var conteo = await c.AbiertoAsync();
        c.ComoUsuario(ConteosDePrueba.ContadorA);

        var r = await c.CapturarAsync(conteo, 1, Codigo("7700000000031"), Codigo("7700000000031"), Codigo(" 7700000000031 "), Codigo("17700000000038"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.Accepted.Should().Be(4);
        r.Value.Rejected.Should().BeEmpty();
        r.Value.Lines.Should().ContainSingle().Which.Counted.Should().Be(15m, "3 unidades + una docena");
        var capturas = await c.Db.CountCaptures.AsNoTracking().ToListAsync();
        capturas.Should().ContainSingle("una tanda por línea y contador").Which.Should().BeEquivalentTo(new
        {
            Round = (byte)1, CounterUserId = ConteosDePrueba.ContadorA, Quantity = 15m, Reads = 4, IsCorrection = false,
        }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Una_lectura_negativa_corrige_y_varios_contadores_suman()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync();

        c.ComoUsuario(ConteosDePrueba.ContadorA);
        await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 6m));
        c.ComoUsuario(ConteosDePrueba.ContadorB);
        await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 5m));
        var r = await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, -1m));

        r.Value.Lines.Single().Counted.Should().Be(10m, "6 + 5 − 1");
        (await c.Db.CountCaptures.CountAsync(x => x.IsCorrection && x.Quantity == -1m)).Should().Be(1, "la corrección es otra captura: nada se reescribe");
        var detalle = await c.DetalleAsync(conteo);
        detalle.Value.Lines.Single().Should().BeEquivalentTo(new { CountedRound1 = (decimal?)10m, Counted = 10m, Difference = (decimal?)0m },
            o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Cada_lectura_se_acepta_o_rechaza_sola()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        var conteo = await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [c.K.P1]));
        c.ComoUsuario(ConteosDePrueba.ContadorA);

        var r = await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 2m), Codigo("999"), c.Lectura(c.K.P2, 1m), c.Lectura(c.K.P1, 1m));

        r.Value.Accepted.Should().Be(2);
        r.Value.Rejected.Select(x => (x.Index, x.Code)).Should().Equal((1, "Inventory.Barcode.NotFound"), (2, "Inventory.Count.ProductNotInScope"));
        r.Value.Lines.Single().Counted.Should().Be(3m);
    }

    [Theory]
    [InlineData(CountScope.All)]
    [InlineData(CountScope.Location)]
    public async Task En_un_conteo_total_o_por_ubicacion_un_producto_sin_teorico_entra_con_teorico_cero(CountScope alcance)
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync(c.Definicion(alcance));
        c.ComoUsuario(ConteosDePrueba.ContadorA);

        var r = await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P2, 2m));

        r.Value.Accepted.Should().Be(1);
        r.Value.Lines.Single().AddedDuringCapture.Should().BeTrue();
        c.Lineas(conteo).Single(l => l.ProductId == c.K.ProductoId(c.K.P2)).Should().BeEquivalentTo(new
        {
            TheoreticalQuantity = 0m, AddedDuringCapture = true, LocationId = c.General.Id, RecountRequired = true,
        }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Contador_no_declarado_y_conteo_no_abierto()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync(c.Definicion(contadores: [c.UsuarioContadorA.PublicId]));

        c.ComoUsuario(ConteosDePrueba.ContadorB);
        (await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1))).Error.Code.Should().Be("Inventory.Count.CounterNotAssigned");
        c.ComoUsuario(ConteosDePrueba.ContadorA);
        (await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1))).IsSuccess.Should().BeTrue();

        var sinAbrir = await c.DefinirAsync(c.Definicion(CountScope.All));
        (await c.CapturarAsync(sinAbrir.Value.PublicId, 1, c.Lectura(c.K.P1))).Error.Code.Should().Be("Inventory.Count.NotOpen");
    }

    [Fact]
    public async Task La_ronda_dos_solo_admite_las_lineas_con_reconteo()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        var conteo = await c.AbiertoAsync();
        c.ComoUsuario(ConteosDePrueba.ContadorA);

        (await c.CapturarAsync(conteo, 2, c.Lectura(c.K.P1, 10m))).Error.Code.Should().Be("Inventory.Count.RoundNotOpen", "sin diferencias no hay ronda 2");
        await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 7m), c.Lectura(c.K.P2, 4m));
        var r = await c.CapturarAsync(conteo, 2, c.Lectura(c.K.P1, 9m), c.Lectura(c.K.P2, 4m));

        r.Value.Accepted.Should().Be(1);
        r.Value.Rejected.Should().ContainSingle().Which.Code.Should().Be("Inventory.Count.RoundNotOpen", "P2 no tiene reconteo pendiente");
        c.Documento(conteo).CountRound.Should().Be(2);
    }

    [Fact]
    public async Task En_un_conteo_ciego_quien_solo_captura_no_ve_el_teorico_y_los_valores_piden_Costs_Read()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync(c.Definicion(ciego: true));
        c.ComoUsuario(ConteosDePrueba.ContadorA);
        await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 8m));

        c.SinPermisos(DetalleDeConteo.PermisoDeAbrir, DetalleDeConteo.PermisoDeCerrar);
        var ciego = (await c.DetalleAsync(conteo)).Value;
        ciego.ShowsTheoretical.Should().BeFalse();
        ciego.Lines.Single().Should().BeEquivalentTo(new
        {
            Theoretical = (decimal?)null, Difference = (decimal?)null, DifferenceValue = (decimal?)null, Counted = 8m,
        }, o => o.ExcludingMissingMembers());

        c.SinPermisos("Inventory.Costs.Read");
        var sinCostos = (await c.DetalleAsync(conteo)).Value.Lines.Single();
        sinCostos.Theoretical.Should().Be(10m, "quien abre o cierra ve el teórico");
        sinCostos.Difference.Should().Be(-2m);
        sinCostos.DifferenceValue.Should().BeNull("los valores exigen Inventory.Costs.Read");

        c.SinPermisos();
        (await c.DetalleAsync(conteo)).Value.Lines.Single().DifferenceValue.Should().Be(-2_000m);
    }
}
