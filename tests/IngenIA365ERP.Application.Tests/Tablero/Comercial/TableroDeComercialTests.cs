using FluentAssertions;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Comercial;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Sales;

namespace IngenIA365ERP.Application.Tests.Tablero.Comercial;

/// <summary>2026-11-17: las ventas del tablero de inicio salen de los documentos confirmados, y «Solo lo mío» es lo que confirmó el usuario.</summary>
public class TableroDeComercialTests
{
    [Fact]
    public async Task Las_ventas_del_dia_suman_lo_confirmado_hoy_y_solo_lo_mio_filtra_por_quien_confirmo()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 2m), v.Linea(v.P3, 1m)]);
        var neto = v.Documento(venta.PublicId).Lines.Where(l => !l.IsDeleted).Sum(l => l.NetAmount);
        var hoy = venta.OperationDate;
        var indicador = new VentasDelDia(v.Db, v.K.Alcance);

        var todo = (await indicador.CalcularAsync(new ContextoDeIndicador(false, null, null, hoy), default)).Value;
        var mio = (await indicador.CalcularAsync(new ContextoDeIndicador(true, KardexDePrueba.Usuario, null, hoy), default)).Value;
        var deOtro = (await indicador.CalcularAsync(new ContextoDeIndicador(true, KardexDePrueba.Usuario + 999, null, hoy), default)).Value;

        neto.Should().BeGreaterThan(0m);
        todo.Valor.Should().Be(neto);
        todo.Anterior.Should().Be(0m, "ayer no hubo ventas");
        mio.Valor.Should().Be(neto, "la confirmó el usuario de la prueba");
        deOtro.Valor.Should().Be(0m);
    }

    [Fact]
    public async Task Las_ventas_del_mes_y_la_serie_de_catorce_dias_dicen_lo_mismo()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 3m)]);
        var neto = v.Documento(venta.PublicId).Lines.Where(l => !l.IsDeleted).Sum(l => l.NetAmount);
        var contexto = new ContextoDeIndicador(false, null, null, venta.OperationDate);

        var mes = (await new VentasDelMes(v.Db, v.K.Alcance).CalcularAsync(contexto, default)).Value;
        var serie = (await new VentasUltimosDias(v.Db, v.K.Alcance).CalcularAsync(contexto, default)).Value;

        mes.Valor.Should().Be(neto);
        serie.Serie.Should().HaveCount(VentasUltimosDias.Dias);
        serie.Serie!.Last().Valor.Should().Be(neto, "la última barra es hoy");
        serie.Valor.Should().Be(neto);
    }
}
