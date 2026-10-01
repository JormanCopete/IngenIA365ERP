using FluentAssertions;
using IngenIA365ERP.Domain.Inventory.Tracking;

namespace IngenIA365ERP.Domain.Tests.Inventory.Tracking;

/// <summary>
/// Feature 012, I6, T905 (FR-026, US15-4; data-model §1.11): <see cref="SelectorDeLotes"/> sugiere el lote con existencia que vence
/// primero (FEFO), luego por código, con los lotes sin vencimiento al final; reparte una salida entre varios lotes cuando uno no
/// alcanza; y con <c>Ventas.LoteVencido = Bloquear</c> excluye los vencidos a <c>HoyLocal</c>, con <c>Advertir</c> los incluye
/// marcados.
/// </summary>
public class SelectorDeLotesTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 29);

    private static LoteDisponible Lote(int id, string codigo, int? venceEnDias, decimal disponible) =>
        new(id, codigo, venceEnDias is { } d ? Hoy.AddDays(d) : null, disponible);

    private static PedidoDeLotes Pedido(decimal cantidad, PoliticaDeLoteVencido politica, params LoteDisponible[] lotes) =>
        new(lotes, cantidad, Hoy, politica);

    [Fact]
    public void Sugiere_el_que_vence_primero()
    {
        var r = SelectorDeLotes.Repartir(Pedido(3m, PoliticaDeLoteVencido.Bloquear,
            Lote(1, "L-60", 60, 10m), Lote(2, "L-10", 10, 10m)));

        r.Sugerido!.Lote.Codigo.Should().Be("L-10", "US15-4: dos lotes que vencen a 10 y a 60 días, la venta sugiere el de 10");
        r.Asignaciones.Should().ContainSingle().Which.Cantidad.Should().Be(3m);
        r.Faltante.Should().Be(0m);
        r.Completo.Should().BeTrue();
    }

    [Fact]
    public void Con_el_mismo_vencimiento_ordena_por_codigo_y_los_sin_vencimiento_van_al_final()
    {
        var orden = SelectorDeLotes.Ordenar(
            [Lote(1, "SIN-B", null, 1m), Lote(2, "B", 5, 1m), Lote(3, "A", 5, 1m), Lote(4, "SIN-A", null, 1m), Lote(5, "C", 1, 1m)],
            Hoy, PoliticaDeLoteVencido.Bloquear);

        orden.Select(o => o.Lote.Codigo).Should().Equal("C", "A", "B", "SIN-A", "SIN-B");
    }

    [Fact]
    public void Un_lote_sin_existencia_no_se_sugiere()
    {
        var r = SelectorDeLotes.Repartir(Pedido(2m, PoliticaDeLoteVencido.Bloquear,
            Lote(1, "L-5", 5, 0m), Lote(2, "L-9", 9, -1m), Lote(3, "L-30", 30, 4m)));

        r.Sugerido!.Lote.Codigo.Should().Be("L-30");
    }

    [Fact]
    public void Reparte_la_salida_entre_varios_lotes_cuando_uno_no_alcanza()
    {
        var r = SelectorDeLotes.Repartir(Pedido(12m, PoliticaDeLoteVencido.Bloquear,
            Lote(1, "L-60", 60, 10m), Lote(2, "L-10", 10, 5m), Lote(3, "L-20", 20, 4m)));

        r.Asignaciones.Select(a => (a.Lote.Codigo, a.Cantidad)).Should().Equal(("L-10", 5m), ("L-20", 4m), ("L-60", 3m));
        r.Faltante.Should().Be(0m);
    }

    [Fact]
    public void Si_entre_todos_no_alcanza_informa_lo_que_falta()
    {
        var r = SelectorDeLotes.Repartir(Pedido(12m, PoliticaDeLoteVencido.Bloquear, Lote(1, "L-10", 10, 5m)));

        r.Asignaciones.Sum(a => a.Cantidad).Should().Be(5m);
        r.Faltante.Should().Be(7m);
        r.Completo.Should().BeFalse();
    }

    [Fact]
    public void Con_Bloquear_excluye_los_vencidos_a_hoy()
    {
        var r = SelectorDeLotes.Repartir(Pedido(3m, PoliticaDeLoteVencido.Bloquear,
            Lote(1, "VENCIDO", -1, 10m), Lote(2, "VIGENTE", 10, 10m)));

        r.Sugerido!.Lote.Codigo.Should().Be("VIGENTE");
        r.Asignaciones.Should().OnlyContain(a => !a.Vencido);
        r.Excluidos.Select(l => l.Codigo).Should().Equal("VENCIDO");
    }

    [Fact]
    public void El_lote_que_vence_hoy_todavia_se_vende()
    {
        SelectorDeLotes.EstaVencido(Hoy, Hoy).Should().BeFalse("la fecha de vencimiento es el último día en que el lote sirve");
        SelectorDeLotes.EstaVencido(Hoy.AddDays(-1), Hoy).Should().BeTrue();
        SelectorDeLotes.EstaVencido(null, Hoy).Should().BeFalse("sin vencimiento nunca vence");
    }

    [Fact]
    public void Con_Advertir_incluye_los_vencidos_marcados()
    {
        var r = SelectorDeLotes.Repartir(Pedido(12m, PoliticaDeLoteVencido.Advertir,
            Lote(1, "VENCIDO", -1, 10m), Lote(2, "VIGENTE", 10, 10m)));

        r.Asignaciones.Select(a => (a.Lote.Codigo, a.Cantidad, a.Vencido)).Should().Equal(("VENCIDO", 10m, true), ("VIGENTE", 2m, false));
        r.Excluidos.Should().BeEmpty();
        r.HayVencidos.Should().BeTrue("la aplicación lo devuelve como aviso en warnings[]");
    }

    [Theory]
    [InlineData(PoliticaDeLoteVencido.Bloquear, VeredictoDeLote.Bloqueado)]
    [InlineData(PoliticaDeLoteVencido.Advertir, VeredictoDeLote.Advertido)]
    public void El_veredicto_de_un_lote_elegido_a_mano_depende_de_la_politica(PoliticaDeLoteVencido politica, VeredictoDeLote esperado)
    {
        SelectorDeLotes.Veredicto(Hoy.AddDays(-3), Hoy, politica).Should().Be(esperado);
        SelectorDeLotes.Veredicto(Hoy.AddDays(3), Hoy, politica).Should().Be(VeredictoDeLote.Vigente);
    }

    [Fact]
    public void Una_baja_por_vencimiento_si_saca_un_lote_vencido()
    {
        var r = SelectorDeLotes.Repartir(Pedido(2m, PoliticaDeLoteVencido.Bloquear, Lote(1, "VENCIDO", -5, 3m)) with { AdmiteVencidos = true });

        r.Asignaciones.Should().ContainSingle().Which.Should().Match<AsignacionDeLote>(a => a.Lote.Codigo == "VENCIDO" && a.Vencido);
        r.Faltante.Should().Be(0m);
    }

    [Theory]
    [InlineData("Bloquear", PoliticaDeLoteVencido.Bloquear)]
    [InlineData("advertir", PoliticaDeLoteVencido.Advertir)]
    public void La_politica_se_lee_del_valor_del_parametro(string valor, PoliticaDeLoteVencido esperada)
    {
        SelectorDeLotes.PoliticaDesde(valor).Should().Be(esperada);
    }

    [Fact]
    public void Un_valor_de_parametro_desconocido_es_un_error_visible()
    {
        var act = () => SelectorDeLotes.PoliticaDesde("Ignorar");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void La_cantidad_pedida_es_positiva()
    {
        var act = () => SelectorDeLotes.Repartir(Pedido(0m, PoliticaDeLoteVencido.Bloquear, Lote(1, "L", 1, 1m)));
        act.Should().Throw<ArgumentException>();
    }
}
