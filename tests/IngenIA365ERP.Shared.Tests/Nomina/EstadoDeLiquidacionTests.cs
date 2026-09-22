using FluentAssertions;
using IngenIA365ERP.Shared.Services.Nomina;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Nomina;

/// <summary>
/// Feature 010, revisión de N1 (pantallas #3): «Aprobar» sólo se ofrece en <c>Draft</c>. El servidor
/// rechaza <c>Stale</c> con <c>Payroll.Settlement.NotDraft</c> (<c>SettlementRunWorkflow.ApproveAsync</c>),
/// y a Stale se llega con cualquier vigencia nueva de política (D-19): un camino normal. Los DTOs de
/// las tres pantallas que lo ofrecían con <c>EsBorrador</c> (Prima, Vacaciones y Definitiva sobre
/// <see cref="ResumenCorridaDto"/>; la lista de Vacaciones) separan ahora «es borrador» de «se puede aprobar».
/// </summary>
public class EstadoDeLiquidacionTests
{
    private static ResumenCorridaDto Resumen(string status) => new(
        Guid.NewGuid(), null, 1, status, new DateTime(2026, 6, 30), "operador", null, null, null, null, null,
        3, new TotalesCorridaDto(0, 0, 0, 0, 0, 0), [], [], 0, "hash", null, null, null, false, [],
        Kind: "ServiceBonus", CutoffDate: new DateOnly(2026, 6, 30), Year: 2026, Semester: 1);

    private static LiquidacionVacacionesDto Vacaciones(string status) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ana Pérez", "1023", 0, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15),
        10m, 15, null, 1_500_000m, status, 1, new DateOnly(2026, 6, 30), null, new DateTime(2026, 6, 20), "operador", null, null, null, null, []);

    private static LiquidacionCesantiasDto Cesantias(string status) => new(
        Guid.NewGuid(), 2026, new DateOnly(2026, 12, 31), null, 1, status, 3, 0m, 0m, 0m, new DateTime(2027, 1, 5), "operador", null, null, 0, null, null, []);

    [Theory]
    [InlineData("Draft", true, true)]
    [InlineData("Stale", true, false)]
    [InlineData("Approved", false, false)]
    [InlineData("Superseded", false, false)]
    [InlineData("Reversed", false, false)]
    public void Un_borrador_desactualizado_sigue_siendo_borrador_pero_no_se_puede_aprobar(string status, bool esBorrador, bool sePuedeAprobar)
    {
        Resumen(status).EsBorrador.Should().Be(esBorrador);
        Resumen(status).SePuedeAprobar.Should().Be(sePuedeAprobar, "el servidor sólo aprueba Draft (Payroll.Settlement.NotDraft)");

        Vacaciones(status).EsBorrador.Should().Be(esBorrador);
        Vacaciones(status).SePuedeAprobar.Should().Be(sePuedeAprobar);

        Cesantias(status).EsBorrador.Should().Be(esBorrador);
        Cesantias(status).SePuedeAprobar.Should().Be(sePuedeAprobar);
    }

    [Fact]
    public void Stale_se_reconoce_como_desactualizado_para_pedir_el_recalculo()
    {
        Resumen("Stale").EstaDesactualizada.Should().BeTrue();
        Resumen("Draft").EstaDesactualizada.Should().BeFalse();
        Vacaciones("Stale").EstaDesactualizada.Should().BeTrue();
        Cesantias("Stale").EstaDesactualizada.Should().BeTrue();
        Resumen("Stale").EstadoTexto.Should().Be("Desactualizado", "la pantalla nunca muestra el nombre del enum en inglés");
    }
}
