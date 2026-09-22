using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// Feature 009 E2 (US9): la pestaña «Ejecución» de <c>/contabilidad/presupuesto</c> se abre con
/// <c>Accounting.Budget.View</c>, así que tiene que pedir la ejecución a una ruta que acepte ese
/// mismo permiso: <c>/api/accounting/budgets/execution</c>. Hasta el 2026-09-20 pedía la vista
/// <c>budget-execution</c> del centro de informes (<c>Accounting.Reports.View</c>) y un rol con sólo
/// el permiso del presupuesto veía la pestaña habilitada y «No se pudo consultar la ejecución».
/// </summary>
public class EjecucionPresupuestalBajoPresupuestoTests
{
    [Fact]
    public void La_ruta_de_la_ejecucion_va_bajo_el_presupuesto_con_los_mismos_parametros_que_el_centro_de_informes()
    {
        ContabilidadClient.RutaDeEjecucionPresupuestal("year=2026&month=3&branch=abc", null)
            .Should().Be("/api/accounting/budgets/execution?year=2026&month=3&branch=abc");
        ContabilidadClient.RutaDeEjecucionPresupuestal("?year=2026&month=3", "xlsx")
            .Should().Be("/api/accounting/budgets/execution?year=2026&month=3&format=xlsx", "el «?» sobrante no se duplica y el formato viaja como en los demás informes");
        ContabilidadClient.RutaDeEjecucionPresupuestal(string.Empty, null).Should().Be("/api/accounting/budgets/execution");
    }

    [Fact]
    public void La_pantalla_de_presupuesto_no_vuelve_a_pedir_la_ejecucion_al_centro_de_informes()
    {
        var pantalla = File.ReadAllText(Path.Combine(Repositorio.Shared(), "Pages", "Contabilidad", "Presupuesto.razor"));

        pantalla.Should().Contain("EjecucionPresupuestalAsync(").And.Contain("DescargarEjecucionPresupuestalAsync(");
        pantalla.Should().NotContain("Vistas.EjecucionPresupuestal", "esa vista exige Accounting.Reports.View, que la pantalla no pide")
            .And.NotContain("InformeAsync(ContabilidadClient.Vistas")
            .And.NotContain("/api/reports/accounting");
    }
}
