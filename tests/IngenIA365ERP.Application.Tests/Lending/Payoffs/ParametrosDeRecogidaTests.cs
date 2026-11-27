using FluentAssertions;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T004, R15): los siete parámetros de la recogida existen en el catálogo de Cartera con sus valores, sus ámbitos y un defecto que
/// reproduce lo de hoy —de fábrica nada cambia—: la cuota causada y la enviada se incluyen, los intereses como al cancelar, sólo se recogen créditos
/// total o parcial, se aplica lo de ahora, ninguna diferencia bloquea y lo que gira la pagaduría por un crédito recogido queda como saldo de ella.
/// </summary>
public class ParametrosDeRecogidaTests
{
    private const ParameterScopeKind N = ParameterScopeKind.None;
    private const ParameterScopeKind L = ParameterScopeKind.CreditLine;

    private static DefinicionDeParametro Definicion(string clave) => ParametrosDeCartera.Definiciones.Single(d => d.Clave == clave);

    public static TheoryData<string, string, string[], ParameterScopeKind[]> Elecciones => new()
    {
        { ParametrosDeCartera.RecogidaCuotaCausada, "Incluir", ["Incluir", "Excluir", "Preguntar"], [N, L] },
        { ParametrosDeCartera.PagosCuotaEnviadaAlCancelar, "Incluir", ["Incluir", "Excluir", "Preguntar"], [N, L] },
        { ParametrosDeCartera.RecogidaInteresesProporcionales, "ComoAlCancelar", ["ComoAlCancelar", "Si", "No"], [N, L] },
        { ParametrosDeCartera.RecogidaAlDesembolsar, "Recalcular", ["Recalcular", "MantenerLoProyectado"], [N, L] },
        { ParametrosDeCartera.RecogidaGiroPosteriorDeCreditoRecogido, "SaldoDeLaPagaduria", ["SaldoDeLaPagaduria", "AlCreditoNuevo", "AFavorDelAsociado"], [N] },
    };

    [Theory]
    [MemberData(nameof(Elecciones))]
    public void Cada_eleccion_tiene_sus_valores_su_defecto_de_hoy_y_sus_ambitos(string clave, string defecto, string[] valores, ParameterScopeKind[] ambitos)
    {
        var d = Definicion(clave);

        d.Modulo.Should().Be(ParametrosDeCartera.Modulo);
        d.Tipo.Should().Be(TipoDeParametro.Choice);
        d.ValoresAdmitidos.Should().Equal(valores);
        d.DefectoSeguro.Should().Be(defecto);
        d.AmbitosAdmitidos.Should().BeEquivalentTo(ambitos);
        d.DisponibleEnLaEntrega().Should().BeTrue("la recogida es de K1, la entrega que ya salió");
        d.PendienteDeValidar.Should().BeNull("el defecto es lo de hoy, no una propuesta por validar");
        d.Interpretar(defecto).Admitido.Should().BeTrue();
        d.Interpretar("Otro").Admitido.Should().BeFalse();
        d.Descripcion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Los_productos_admitidos_de_fabrica_son_solo_creditos_total_o_parcial()
    {
        var d = Definicion(ParametrosDeCartera.RecogidaProductosAdmitidos);

        d.Tipo.Should().Be(TipoDeParametro.Text);
        d.AmbitosAdmitidos.Should().BeEquivalentTo([N, L], "rige la línea del crédito nuevo");
        d.DefectoSeguro.Should().Be("Creditos:Total,Parcial");
        d.Interpretar(d.DefectoSeguro).Admitido.Should().BeTrue();
        d.Interpretar("Creditos:Total;Aportes:Cuotas,Abono;FacturasComerciales:Parcial").Admitido.Should().BeTrue();
        d.Interpretar("AhorroALaVista:Cuotas").Admitido.Should().BeFalse("el ahorro a la vista sólo admite abono");
        d.Interpretar("").Admitido.Should().BeFalse();

        var deFabrica = ProductosAdmitidos.Leer(d.DefectoSeguro);
        deFabrica.Modos.Keys.Should().Equal(ProductoDeRecogida.Creditos);
        deFabrica.ModosDe(ProductoDeRecogida.Creditos).Should().BeEquivalentTo(new[] { PayoffMode.Total, PayoffMode.Partial });
    }

    [Fact]
    public void La_tolerancia_de_fabrica_esta_vacia_y_nunca_bloquea()
    {
        var d = Definicion(ParametrosDeCartera.RecogidaToleranciaDeDiferencia);

        d.Tipo.Should().Be(TipoDeParametro.Decimal);
        d.AmbitosAdmitidos.Should().BeEquivalentTo([N]);
        d.DefectoSeguro.Should().BeEmpty();
        d.AdmiteVacio.Should().BeTrue();
        d.Interpretar("").Should().Be(new ValorInterpretado(true, string.Empty, null), "vacío: sólo se avisa");
        d.Interpretar("50000").Admitido.Should().BeTrue();
        d.Interpretar("-1").Admitido.Should().BeFalse("una tolerancia no es negativa");
    }

    [Fact]
    public void Los_siete_estan_en_el_catalogo_de_la_plataforma()
    {
        string[] claves =
        [
            ParametrosDeCartera.RecogidaCuotaCausada, ParametrosDeCartera.RecogidaInteresesProporcionales, ParametrosDeCartera.RecogidaProductosAdmitidos,
            ParametrosDeCartera.RecogidaAlDesembolsar, ParametrosDeCartera.RecogidaToleranciaDeDiferencia,
            ParametrosDeCartera.RecogidaGiroPosteriorDeCreditoRecogido, ParametrosDeCartera.PagosCuotaEnviadaAlCancelar,
        ];

        foreach (var clave in claves)
            CatalogoDeParametros.Buscar(ParametrosDeCartera.Modulo, clave).Should().NotBeNull(clave);
    }
}
