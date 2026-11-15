using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// <c>/pos/cierre-del-dia</c> dice qué días faltan por cerrar y por qué no se puede cerrar (2026-11-11, capacitación de COOFLOPAL: con el
/// punto en «Todos» y una caja abierta, el botón quedaba gris sin explicación). (nuevo)
/// </summary>
public class DiasPorCerrarTests
{
    private static readonly Guid Almacen = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 11, 11);
    private static readonly DateOnly Ayer = new(2026, 11, 10);

    private static SesionDeCajaDto Sesion(DateOnly dia, int estado, string caja = "CAJA1", string cajero = "WILLIAN") =>
        new(Guid.NewGuid(), new PuntoDeLaSesionDto(Almacen, "ALMACEN", "Almacén", true), new ReferenciaDeInventarioDto(Guid.NewGuid(), caja, caja),
            new CajeroDto(null, cajero, null), dia, new DateTime(2026, 11, 11, 12, 48, 0, DateTimeKind.Utc), 0m, "Fixed", estado, null, null, 0, 0m, null);

    private static CierreDelDiaDto Cierre(DateOnly dia, int estado) =>
        new(Guid.NewGuid(), new ReferenciaDeInventarioDto(Almacen, "ALMACEN", "Almacén"), dia, 1, estado, DateTime.UtcNow, "X", null, null, 0m);

    [Fact]
    public void Un_dia_con_una_sesion_abierta_queda_pendiente_y_la_nombra()
    {
        var pendientes = DiasPorCerrar.Calcular([Sesion(Hoy, 2), Sesion(Hoy, 1)], []);

        pendientes.Should().ContainSingle();
        pendientes[0].SePuedeCerrar.Should().BeFalse();
        pendientes[0].OpenSessions.Should().ContainSingle().Which.CashRegisterCode.Should().Be("CAJA1");
        DiasPorCerrar.MotivoParaNoCerrar(Almacen, Hoy, pendientes, []).Should().Contain("caja CAJA1 de WILLIAN");
    }

    [Fact]
    public void Un_dia_cerrado_no_queda_pendiente_y_uno_reabierto_si()
    {
        var sesiones = new[] { Sesion(Ayer, 2), Sesion(Hoy, 2) };

        var pendientes = DiasPorCerrar.Calcular(sesiones, [Cierre(Ayer, 1), Cierre(Hoy, 2)]);

        pendientes.Should().ContainSingle().Which.OperatingDate.Should().Be(Hoy);
        pendientes[0].SePuedeCerrar.Should().BeTrue();
        DiasPorCerrar.MotivoParaNoCerrar(Almacen, Hoy, pendientes, []).Should().BeNull();
        DiasPorCerrar.MotivoParaNoCerrar(Almacen, Ayer, pendientes, [Cierre(Ayer, 1)]).Should().Contain("ya está cerrado");
    }

    [Fact]
    public void Sin_punto_o_sin_sesiones_dice_por_que()
    {
        DiasPorCerrar.MotivoParaNoCerrar(null, Hoy, [], []).Should().Contain("punto de venta");
        DiasPorCerrar.MotivoParaNoCerrar(Almacen, Hoy, [], []).Should().Contain("no tuvo sesiones");
    }
}
