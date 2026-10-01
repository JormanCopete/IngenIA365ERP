using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Reports;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// T182 (contracts/api.md §27, «(PD)»): una vista trae datos personales siempre (<c>card-payments</c>) o sólo con un
/// filtro propio (<c>sales-by-register?groupBy=customer</c>, <c>margin?by=customer</c>); en ese caso exportarla exige
/// además <c>Inventory.Reports.ExportPersonalData</c>.
/// </summary>
public class VistaDeInformeDeInventarioTests
{
    private static Func<string, string?> Query(params (string Clave, string Valor)[] valores) =>
        clave => valores.FirstOrDefault(v => v.Clave == clave).Valor;

    [Fact]
    public void Una_vista_con_datos_personales_siempre_los_trae()
    {
        var vista = new VistaDeInformeDeInventario("card-payments", "Pagos con tarjeta", "…", "pagos-con-tarjeta", ["from", "to"], PersonalData: true);

        vista.TraeDatosPersonales(Query()).Should().BeTrue();
    }

    [Theory]
    [InlineData("customer", true)]
    [InlineData("Customer ", true)]
    [InlineData("day", false)]
    [InlineData(null, false)]
    public void Con_condicion_los_trae_solo_con_ese_valor_del_filtro(string? agrupacion, bool esperado)
    {
        var vista = new VistaDeInformeDeInventario("sales-by-register", "Ventas por caja", "…", "ventas-por-caja", ["from", "to"],
            OwnFilters: ["groupBy"], PersonalDataWhen: "groupBy=customer");

        vista.TraeDatosPersonales(Query(agrupacion is null ? [] : [("groupBy", agrupacion)])).Should().Be(esperado);
    }

    [Fact]
    public void Sin_marca_no_los_trae()
    {
        new VistaDeInformeDeInventario("stock", "Existencias", "…", "existencias", ["warehouse"])
            .TraeDatosPersonales(Query(("groupBy", "customer"))).Should().BeFalse();
    }
}
