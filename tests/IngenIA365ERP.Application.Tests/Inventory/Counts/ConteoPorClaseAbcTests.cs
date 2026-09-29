using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, I6, T910 (FR-040; T930, T931): el conteo cíclico por clase ABC y por lote. <c>OpenPhysicalCountCommand</c> con
/// <c>scope = AbcClass</c> ya no responde <c>.ScopeNotAvailable</c>: clasifica con <c>ClasificacionAbc</c> sobre el valor de las salidas de la
/// bodega en los doce meses anteriores (base por defecto de T931) con <c>Informes.UmbralesAbc</c>, congela en la foto los productos de la
/// clase a esa fecha y guarda la clase y la lista resuelta en <c>CountScopeJson</c>; la foto y la captura van por lote cuando el producto
/// lo controla. Hoy es el 25 de septiembre de 2026.
/// </summary>
public class ConteoPorClaseAbcTests
{
    /// <summary>P1, P2 y P3 con 100 a 1.000 y salidas de 70, 20 y 10: con 80/15/5, P1 es A, P2 es B y P3 es C.</summary>
    private static async Task<ConteosDePrueba> ConSalidasAsync()
    {
        var c = await ConteosDePrueba.CrearAsync();
        foreach (var p in new[] { c.K.P1, c.K.P2, c.P3 }) await c.EntradaAsync(p, 100m, 1_000m);
        foreach (var (p, q) in new[] { (c.K.P1, 70m), (c.K.P2, 20m), (c.P3, 10m) })
            (await c.SalidaAsync(p, q)).Confirmacion.IsSuccess.Should().BeTrue();
        return c;
    }

    [Theory]
    [InlineData("A", "P1")]
    [InlineData("b", "P2")]
    [InlineData("C", "P3")]
    public async Task Abrir_por_clase_congela_en_la_foto_los_productos_de_esa_clase(string clase, string producto)
    {
        var c = await ConSalidasAsync();
        var definido = await c.DefinirAsync(c.Definicion(CountScope.AbcClass) with { AbcClass = clase });
        definido.IsSuccess.Should().BeTrue(definido.IsFailure ? definido.Error.Message : string.Empty);

        var abierto = await c.AbrirAsync(definido.Value.PublicId);

        abierto.IsSuccess.Should().BeTrue(abierto.IsFailure ? $"{abierto.Error.Code}: {abierto.Error.Message}" : string.Empty);
        var esperado = await c.Db.Products.Where(p => p.Code == producto).Select(p => p.Id).SingleAsync();
        c.Lineas(definido.Value.PublicId).Select(l => l.ProductId).Should().Equal(esperado);
        var criterio = CriterioDelConteo.De(c.Documento(definido.Value.PublicId).CountScopeJson);
        criterio.ClaseAbc.Should().Be(clase.ToUpperInvariant());
        criterio.ProductosDeLaClase.Should().Equal(esperado);
        criterio.BaseAbc.Should().Contain("salidas").And.Contain("2026-09-25");
    }

    [Fact]
    public async Task Los_umbrales_salen_de_Informes_UmbralesAbc()
    {
        var c = await ConSalidasAsync();
        c.K.Entrega = Domain.Common.Parametros.EntregaDelComercio.I6;
        c.Parametro(ParametrosDeInventario.InformesUmbralesAbc, "95/4/1");
        var definido = await c.DefinirAsync(c.Definicion(CountScope.AbcClass) with { AbcClass = "A" });

        (await c.AbrirAsync(definido.Value.PublicId)).IsSuccess.Should().BeTrue();

        c.Lineas(definido.Value.PublicId).Should().HaveCount(2, "con A = 95 %, P1 (70 %) y P2 (90 %) son A");
    }

    [Fact]
    public async Task Una_clase_que_no_es_A_B_ni_C_es_AbcClassInvalid()
    {
        var c = await ConSalidasAsync();
        var definido = await c.DefinirAsync(c.Definicion(CountScope.AbcClass) with { AbcClass = "Z" });

        ConteosDePrueba.Codigo(await c.AbrirAsync(definido.Value.PublicId)).Should().Be("Inventory.Count.AbcClassInvalid");
    }

    [Fact]
    public async Task La_foto_y_la_captura_van_por_lote_cuando_el_producto_lo_controla()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        var leche = (await c.K.C.ProductoAsync(c.K.C.Alta("LCH", "Leche") with { TracksLot = true })).PublicId;
        await c.EntradaAsync(leche, 5m, 1_000m, lote: "LA");
        await c.EntradaAsync(leche, 3m, 1_000m, lote: "LB");
        var conteo = await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [leche]));

        var lotes = await c.Db.Lots.ToDictionaryAsync(l => l.Id, l => l.Code);
        c.Lineas(conteo).Select(l => (lotes[l.LotId!.Value], l.TheoreticalQuantity)).Should().BeEquivalentTo(new[] { ("LA", 5m), ("LB", 3m) });

        c.ComoUsuario(ConteosDePrueba.ContadorA);
        var captura = await c.CapturarAsync(conteo, 1, c.Lectura(leche, 5m, lote: "la"), c.Lectura(leche, 2m, lote: "LB"), c.Lectura(leche, 1m));
        captura.IsSuccess.Should().BeTrue(captura.IsFailure ? captura.Error.Message : string.Empty);
        captura.Value.Accepted.Should().Be(2);
        captura.Value.Rejected.Should().ContainSingle().Which.Code.Should().Be("Inventory.Lot.Required", "sin lote no se sabe cuál se contó");

        c.ComoUsuario(ConteosDePrueba.Jefe);
        var cerrado = await c.CerrarAsync(conteo);
        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? $"{cerrado.Error.Code}: {cerrado.Error.Message}" : string.Empty);
        c.Lineas(conteo).Select(l => (lotes[l.LotId!.Value], l.Difference)).Should().BeEquivalentTo(new[] { ("LA", (decimal?)0m), ("LB", (decimal?)-1m) });
    }
}
