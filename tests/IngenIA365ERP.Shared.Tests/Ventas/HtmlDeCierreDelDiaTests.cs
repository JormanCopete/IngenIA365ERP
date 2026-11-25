using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// 2026-11-25 (pedido del dueño: «ver el cierre del día bien detallado»): la hoja impresa del cierre del día lleva cada sesión con su arqueo y, debajo,
/// los medios que difieren con su tratamiento y el motivo, para que a quién corresponde cada descuadre quede en la hoja; la sesión de la caja única
/// dice que se arquea en Recaudo; el texto que viene de la base se codifica.
/// </summary>
public class HtmlDeCierreDelDiaTests
{
    private static DetalleDelCierreDelDiaDto Detalle(params SesionDelCierreDelDiaDto[] sesiones) => new(
        new CierreDelDiaDto(Guid.NewGuid(), new ReferenciaDeInventarioDto(Guid.NewGuid(), "PUNTO1", "Punto de prueba"), new DateOnly(2026, 11, 25), 3, 1,
            new DateTime(2026, 11, 25, 14, 53, 0, DateTimeKind.Utc), "supervisor", null, null, 1_000_000m),
        [new LineaDelCierreDelDiaDto("EFECTIVO", 900_000m, 900_000m, 0m, 12), new LineaDelCierreDelDiaDto("TDVISA", 100_700m, 100_000m, -700m, 4)],
        [],
        sesiones);

    [Fact]
    public void Cada_sesion_lleva_su_arqueo_y_los_medios_que_difieren_con_su_motivo()
    {
        var html = HtmlDeCierreDelDia.Documento(Detalle(
            new SesionDelCierreDelDiaDto(Guid.NewGuid(), "CAJA1", "CAJERO DE PRUEBA", new DateTime(2026, 11, 25, 11, 53, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 25, 12, 9, 0, DateTimeKind.Utc), 100_000m, 250_700m, 250_000m, -700m, "cajero.prueba", false,
                [new DiferenciaDeSesionDelCierreDto("TDVISA", 40_700m, 40_000m, -700m, "ERROR <EN> COBRO", 3)])));

        html.Should().Contain("CAJERO DE PRUEBA").And.Contain("250.700").And.Contain("250.000").And.Contain("-700");
        html.Should().Contain("Faltante al gasto").And.Contain("«ERROR &lt;EN&gt; COBRO»", "el motivo va codificado");
    }

    [Fact]
    public void La_sesion_de_la_caja_unica_dice_que_se_arquea_en_Recaudo()
    {
        var html = HtmlDeCierreDelDia.Documento(Detalle(
            new SesionDelCierreDelDiaDto(Guid.NewGuid(), "CAJA4", "CAJERA DE PRUEBA", new DateTime(2026, 11, 25, 11, 24, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 25, 12, 28, 0, DateTimeKind.Utc), LinkedToCollection: true)));

        html.Should().Contain("CAJERA DE PRUEBA").And.Contain("Se arquea en Recaudo");
    }
}
