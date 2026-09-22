using FluentAssertions;
using IngenIA365ERP.Shared.Models.Nomina;
using IngenIA365ERP.Shared.Services.Nomina;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Nomina;

/// <summary>
/// Feature 010, revisión de N1 (pantallas #4): habiendo llegado a Cesantías con <c>?corrida=</c>
/// (desde el comprobante, «Ver en Nómina»), calcular o recalcular deja elegida la corrida nueva, no
/// la del enlace. El enlace se consume una vez; después manda lo que la persona hizo.
/// </summary>
public class SeleccionDeCesantiasTests
{
    private static LiquidacionCesantiasDto Corrida(string status, int version = 1) => new(
        Guid.NewGuid(), 2026, new DateOnly(2026, 12, 31), null, version, status, 3, 0m, 0m, 0m,
        new DateTime(2027, 1, 5), "operador", null, null, 0, null, null, []);

    [Fact]
    public void La_recien_calculada_gana_a_la_del_enlace()
    {
        var reversada = Corrida("Reversed");
        var borrador = Corrida("Draft", version: 2);
        var lista = new[] { borrador, reversada };

        var elegida = SeleccionDeCesantias.Elegir(lista, recienCalculada: borrador.RunPublicId, elegidaAntes: null, delEnlace: reversada.RunPublicId);

        elegida.Should().BeSameAs(borrador, "los excluidos del cálculo nuevo se pintan bajo la corrida nueva");
    }

    [Fact]
    public void Al_llegar_por_el_enlace_se_elige_esa_corrida_aunque_haya_una_vigente()
    {
        var reversada = Corrida("Reversed");
        var aprobada = Corrida("Approved", version: 2);
        var lista = new[] { aprobada, reversada };

        SeleccionDeCesantias.Elegir(lista, null, null, delEnlace: reversada.RunPublicId).Should().BeSameAs(reversada);
    }

    [Fact]
    public void Sin_orden_explicita_se_mantiene_la_elegida_si_sigue_en_la_lista_y_si_no_la_vigente()
    {
        var reemplazada = Corrida("Superseded");
        var borrador = Corrida("Draft", version: 2);
        var lista = new[] { borrador, reemplazada };

        SeleccionDeCesantias.Elegir(lista, null, elegidaAntes: reemplazada.RunPublicId, null).Should().BeSameAs(reemplazada);
        SeleccionDeCesantias.Elegir(lista, null, elegidaAntes: Guid.NewGuid(), null).Should().BeSameAs(borrador, "la elegida ya no está: la vigente");
        SeleccionDeCesantias.Elegir([reemplazada], null, null, null).Should().BeSameAs(reemplazada, "sin vigente, la más reciente");
        SeleccionDeCesantias.Elegir([], null, null, null).Should().BeNull();
    }

    [Fact]
    public void Un_enlace_a_una_corrida_de_otro_anio_no_elige_nada_raro()
    {
        var borrador = Corrida("Draft");

        SeleccionDeCesantias.Elegir([borrador], null, null, delEnlace: Guid.NewGuid()).Should().BeSameAs(borrador);
    }
}
