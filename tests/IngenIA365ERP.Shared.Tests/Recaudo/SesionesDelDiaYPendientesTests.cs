using FluentAssertions;
using IngenIA365ERP.Shared.Services.Recaudo;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Recaudo;

/// <summary>
/// 2026-11-14 (COOFLOPAL): la lista del supervisor en Recaudo › Sesiones traía sólo las sesiones de hoy, y una sesión que quedó abierta
/// después del corte de un día anterior («olvidada») no tenía cómo cerrarse: su cajero no puede y el supervisor no la veía. Ahora la
/// lista suma las olvidadas y las de diferencia por aprobar de cualquier día.
/// </summary>
public class SesionesDelDiaYPendientesTests
{
    private static SesionDeCajaDto Sesion(Guid id, string caja, DateOnly dia, int estado) =>
        new(id, Guid.NewGuid(), caja, Guid.NewGuid(), "PRINCIPAL", Guid.NewGuid(), "Cajero", dia, estado, dia.ToDateTime(TimeOnly.MinValue),
            null, 0m, estado == SesionesClient.EstadoOlvidada, false, false, null, null, []);

    [Fact]
    public void Suma_las_pendientes_de_otros_dias_sin_repetir_y_de_la_mas_reciente_a_la_mas_antigua()
    {
        var hoy = new DateOnly(2026, 11, 14);
        var olvidadaDeHoy = Sesion(Guid.NewGuid(), "CAJA4", hoy, SesionesClient.EstadoOlvidada);
        var abiertaDeHoy = Sesion(Guid.NewGuid(), "CAJA5", hoy, 1);
        var olvidadaDel13 = Sesion(Guid.NewGuid(), "CAJA3", hoy.AddDays(-1), SesionesClient.EstadoOlvidada);
        var olvidadaDel8 = Sesion(Guid.NewGuid(), "CAJA1", new DateOnly(2026, 11, 8), SesionesClient.EstadoOlvidada);
        var porAprobarDel13 = Sesion(Guid.NewGuid(), "CAJA2", hoy.AddDays(-1), SesionesClient.EstadoConDiferenciaPorAprobar);

        // La olvidada de hoy viene en la consulta de hoy y en la de olvidadas.
        var lista = SesionesClient.DelDiaYPendientes([abiertaDeHoy, olvidadaDeHoy, olvidadaDeHoy, olvidadaDel8, olvidadaDel13, porAprobarDel13]);

        lista.Select(s => s.CashBoxCode).Should().Equal("CAJA4", "CAJA5", "CAJA2", "CAJA3", "CAJA1");
    }

    [Fact]
    public void Los_estados_son_los_del_servidor()
    {
        SesionesClient.Estado(SesionesClient.EstadoOlvidada).Should().Be("Olvidada");
        SesionesClient.Estado(SesionesClient.EstadoConDiferenciaPorAprobar).Should().Be("Cerrada con diferencia por aprobar");
    }
}
