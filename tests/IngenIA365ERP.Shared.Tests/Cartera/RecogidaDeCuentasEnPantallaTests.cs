using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 028, E3 (T081; HU6): las cuentas de aportes y ahorros en «Productos que recoge». El resumen de la pantalla suma lo pendiente a la fecha (por
/// cuotas) o el abono, sin liberar cuotas de crédito; lo marcado se pide como cuenta, con el valor del abono; la solicitud que se corrige vuelve a marcar la
/// cuenta; y los textos dicen el producto y «La cooperativa no los recoge». (nuevo)
/// </summary>
public class RecogidaDeCuentasEnPantallaTests
{
    private static readonly Guid Aportes = Guid.Parse("66666666-7777-8888-9999-000000000001");
    private static readonly Guid Vista = Guid.Parse("66666666-7777-8888-9999-000000000002");

    private static ProductoParaRecogerDto Cuenta(Guid id, string producto, decimal pendiente, params int[] modos) => new(TextosDeRecogida.ClaseCuenta, id, "AP-1",
        "APORTES · Aportes sociales", null, null, 0, 50_000m, true, null, modos, pendiente,
        new DesgloseDeRecogidaDto(pendiente, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m), null, null, null, null, null, "sha256:x", [])
    {
        AccountProduct = producto,
    };

    [Fact]
    public void El_resumen_suma_las_cuotas_pendientes_y_el_abono_sin_liberar_cuotas_de_credito()
    {
        var productos = new[]
        {
            Cuenta(Aportes, "Aportes", 100_000m, TextosDeRecogida.ModoCuotas, TextosDeRecogida.ModoAbono),
            Cuenta(Vista, "AhorroALaVista", 0m, TextosDeRecogida.ModoAbono),
        };
        IReadOnlyList<RecogidaMarcada> marcadas =
        [
            new(Aportes, "AP-1", TextosDeRecogida.ModoCuotas, null, 100_000m, Kind: TextosDeRecogida.ClaseCuenta),
            new(Vista, "AV-1", TextosDeRecogida.ModoAbono, 300_000m, 0m, Kind: TextosDeRecogida.ClaseCuenta),
        ];

        var r = SeleccionDeRecogida.Resumir(productos, marcadas, 5_000_000m, null);

        (r.SelectedTotal, r.NetToDisburse, r.SelectedCount, r.ReleasedInstallments).Should().Be((400_000m, 4_600_000m, 2, 0m));
    }

    [Fact]
    public void Lo_marcado_se_pide_como_cuenta_y_la_correccion_la_vuelve_a_marcar()
    {
        new RecogidaMarcada(Vista, "AV-1", TextosDeRecogida.ModoAbono, 300_000m, 0m, Kind: TextosDeRecogida.ClaseCuenta).ComoPedido()
            .Should().Be(new RecogidaElegidaRequest(TextosDeRecogida.ClaseCuenta, Vista, TextosDeRecogida.ModoAbono, 300_000m));
        new RecogidaMarcada(Aportes, "AP-1", TextosDeRecogida.ModoCuotas, 5m, 100_000m, Kind: TextosDeRecogida.ClaseCuenta).ComoPedido()
            .Should().Be(new RecogidaElegidaRequest(TextosDeRecogida.ClaseCuenta, Aportes, TextosDeRecogida.ModoCuotas), "por cuotas no lleva valor");

        var guardada = new RecogidaDeSolicitudDto(TextosDeRecogida.ClaseCuenta, Vista, null, "AV-1", TextosDeRecogida.ModoAbono, 300_000m, 300_000m,
            new DesgloseDeRecogidaDto(300_000m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m), null, null, null, new DateOnly(2027, 1, 10), DateTime.UtcNow, true, null, 1);
        guardada.ComoMarcada().Should().Be(new RecogidaMarcada(Vista, "AV-1", TextosDeRecogida.ModoAbono, 300_000m, 300_000m, Kind: TextosDeRecogida.ClaseCuenta));
    }

    [Fact]
    public void Los_textos_dicen_el_producto_y_que_la_cooperativa_no_los_recoge()
    {
        TextosDeRecogida.ProductoDeCuenta("AhorroContractual").Should().Be("Ahorro contractual");
        TextosDeRecogida.Motivo("NotAdmitted", TextosDeRecogida.ClaseCuenta).Should().Be("La cooperativa no los recoge");
        TextosDeRecogida.Motivo("NotAdmitted", TextosDeRecogida.ClaseCredito).Should().Be("La línea no lo admite");
        TextosDeRecogida.Motivo("DepositNotAllowed", TextosDeRecogida.ClaseCuenta).Should().Be("La línea no admite abonos");
        TextosDeDesembolso.EstadoDeRecogida(new RecogidaPropuestaDto(Guid.Empty, "AP-1", 100_000m, 0, false) { Kind = "Account" }).Should().Be("Se consigna");
    }
}
