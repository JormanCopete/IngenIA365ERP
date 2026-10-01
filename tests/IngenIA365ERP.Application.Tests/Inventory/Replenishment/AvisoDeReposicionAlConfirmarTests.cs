using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Domain.Enums.Alerts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Replenishment;

/// <summary>
/// Feature 012, US17, T945 (FR-035, FR-022; contracts/api.md §4.4, §9.3): al confirmar una <b>salida</b> que deja la posición igual o
/// menor que el punto de reorden, la respuesta lleva el aviso <c>Inventory.Stock.BelowReorderPoint</c> con
/// <c>{ productCode, warehouseCode, position, reorderPoint }</c> y se levanta <c>Inventario.Reorden</c> con la condición
/// <c>Inventario.Reorden:{producto}:{bodega}</c> y la bodega como alcance; si además el disponible quedó bajo el mínimo, también
/// <c>Inventario.Quiebre</c>. Una entrada no levanta nada; sin política no hay aviso; una segunda salida con la alerta pendiente suma
/// la ocurrencia. Por el ciclo común real (<c>ConfirmacionDeDocumento</c>) con ajustes negativos, consumo interno y bajas.
/// </summary>
public class AvisoDeReposicionAlConfirmarTests
{
    [Fact]
    public async Task Una_salida_que_deja_la_posicion_en_o_bajo_el_punto_avisa_y_levanta_reorden_y_quiebre()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 12m, 1000m);
        r.Politica(k.P1, k.Principal);

        var (_, confirmacion) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]));

        confirmacion.IsSuccess.Should().BeTrue(confirmacion.IsFailure ? confirmacion.Error.Message : string.Empty);
        var aviso = confirmacion.Value.Warnings.Should().ContainSingle().Which;
        aviso.Code.Should().Be(AvisoDeReposicionAlConfirmar.CodigoDelAviso).And.Be("Inventory.Stock.BelowReorderPoint");
        aviso.Data.Should().BeEquivalentTo(new { productCode = "P1", warehouseCode = "PRIN", position = 8m, reorderPoint = 15m });

        var producto = k.C.Producto(k.P1).PublicId;
        var alertas = await r.AlertasAsync();
        alertas.Select(a => a.TypeCode).Should().BeEquivalentTo([TiposDeAlerta.Reorden, TiposDeAlerta.Quiebre], "8 ≤ 15 pide reorden y 8 < 10 es quiebre");
        alertas.Single(a => a.TypeCode == TiposDeAlerta.Reorden).DedupKey.Should().Be($"Inventario.Reorden:{producto:D}:{k.Principal.PublicId:D}");
        alertas.Single(a => a.TypeCode == TiposDeAlerta.Quiebre).DedupKey.Should().Be($"Inventario.Quiebre:{producto:D}:{k.Principal.PublicId:D}");
        alertas.Should().OnlyContain(a => a.ScopeWarehousePublicId == k.Principal.PublicId && a.EntityPublicId == producto);
    }

    [Fact]
    public async Task En_el_punto_pide_reorden_pero_sin_quiebre_si_el_disponible_no_baja_del_minimo()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 20m, 1000m);
        r.Politica(k.P1, k.Principal);

        var (_, confirmacion) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 5m)]));

        confirmacion.Value.Warnings.Select(w => w.Code).Should().Equal(AvisoDeReposicionAlConfirmar.CodigoDelAviso);
        (await r.AlertasAsync()).Select(a => a.TypeCode).Should().Equal(TiposDeAlerta.Reorden);
    }

    [Fact]
    public async Task Lo_que_sigue_en_transito_hacia_la_bodega_cuenta_en_la_posicion()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 13m, 1000m);
        r.Politica(k.P1, k.Principal);
        r.EnTransito(k.P1, k.Segunda, k.Principal, 5m);

        var (_, confirmacion) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 1m)]));

        confirmacion.Value.Warnings.Should().BeEmpty("12 disponibles y 5 en tránsito dan posición 17, sobre el punto 15 (US17-2)");
        (await r.AlertasAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task El_consumo_interno_y_la_baja_tambien_avisan()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 20m, 1000m);
        r.Politica(k.P1, k.Principal);

        var (_, consumo) = await k.AjusteAsync(k.Borrador("CI", centro: k.Centro.PublicId, lineas: [k.Linea(k.P1, 3m)]));
        consumo.Value.Warnings.Should().BeEmpty("17 sigue sobre el punto");

        var (_, baja) = await k.AjusteAsync(k.Borrador("BAJ", causa: k.Causa(), lineas: [k.Linea(k.P1, 3m)]));
        baja.Value.Warnings.Should().ContainSingle(w => w.Code == AvisoDeReposicionAlConfirmar.CodigoDelAviso);
    }

    [Fact]
    public async Task Una_entrada_no_levanta_nada()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        r.Politica(k.P1, k.Principal);

        var (_, entrada) = await k.AjusteAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 5m, 1000m)]));

        entrada.IsSuccess.Should().BeTrue();
        entrada.Value.Warnings.Should().BeEmpty("5 ≤ 15, pero una entrada no deja la posición peor de lo que estaba");
        (await r.AlertasAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_politica_para_el_producto_en_la_bodega_no_hay_aviso()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 12m, 1000m);
        r.Politica(k.P1, k.Segunda);
        r.Politica(k.P2, k.Principal);

        var (_, confirmacion) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 10m)]));

        confirmacion.Value.Warnings.Should().BeEmpty();
        (await r.AlertasAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Una_segunda_salida_con_la_alerta_pendiente_suma_la_ocurrencia()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 12m, 1000m);
        r.Politica(k.P1, k.Principal);

        await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]));
        var (_, segunda) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 1m)]));

        segunda.Value.Warnings.Should().ContainSingle().Which.Data.Should().BeEquivalentTo(new { position = 7m });
        var alertas = await r.AlertasAsync();
        alertas.Should().HaveCount(2, "una pendiente por tipo, producto y bodega");
        alertas.Should().OnlyContain(a => a.OccurrenceCount == 2 && a.Status == AlertStatus.Pending);
    }

    [Fact]
    public async Task Un_tipo_de_alerta_apagado_no_bloquea_la_confirmacion()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 12m, 1000m);
        r.Politica(k.P1, k.Principal);
        foreach (var tipo in await k.C.Db.AlertTypes.ToListAsync()) tipo.IsEnabled = false;
        await k.C.Db.SaveChangesAsync();

        var (_, confirmacion) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]));

        confirmacion.IsSuccess.Should().BeTrue("el aviso nunca bloquea");
        confirmacion.Value.Warnings.Should().ContainSingle();
        (await r.AlertasAsync()).Should().BeEmpty();
    }
}
