using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>
/// <c>GuardiaDeCanalesExternos</c> (feature 013, T0030; T73; contracts/adaptadores-y-archivos.md §3.6, §10): con <c>AmbienteDePruebas</c> en
/// los dos valores y los cuatro <see cref="EfectoLegalDeCanal"/>. En producción un resultado simulado es <c>SinEfecto(Motivo,
/// QueConfigurar)</c> y en pruebas <c>ConEfecto</c>; <c>CARGA-PROPIA</c> no es simulado y vale en producción. (nuevo)
/// </summary>
public sealed class GuardiaDeCanalesExternosTests
{
    private static GuardiaDeCanalesExternos Guardia(bool ambienteDePruebas) =>
        new(Options.Create(new CanalesExternosOptions { AmbienteDePruebas = ambienteDePruebas }));

    private static ResultadoExterno<bool> Resultado(string canal, bool simulado) => ResultadoExterno<bool>.Ok(true, canal, simulado);

    public static TheoryData<EfectoLegalDeCanal> Efectos() =>
    [
        EfectoLegalDeCanal.PromissoryNotePerfection,
        EfectoLegalDeCanal.PriorNoticeEvidence,
        EfectoLegalDeCanal.BureauEvidenceForApproval,
        EfectoLegalDeCanal.RealDelivery,
    ];

    [Theory]
    [MemberData(nameof(Efectos))]
    public void En_produccion_un_resultado_simulado_no_tiene_efecto_legal(EfectoLegalDeCanal efecto)
    {
        var veredicto = Guardia(ambienteDePruebas: false).Evaluar(efecto, Resultado(CodigosDeCanalExterno.Simulado, simulado: true));

        Assert.False(veredicto.TieneEfecto);
        Assert.False(string.IsNullOrWhiteSpace(veredicto.Motivo));
        Assert.Contains("Canales externos", veredicto.QueConfigurar, StringComparison.Ordinal);
        var error = veredicto.ComoError();
        Assert.NotNull(error);
        Assert.Equal("Lending.Adapter.SimulatedInProduction", error!.Code);
    }

    [Theory]
    [MemberData(nameof(Efectos))]
    public void Fuera_de_produccion_un_resultado_simulado_tiene_efecto(EfectoLegalDeCanal efecto)
    {
        var veredicto = Guardia(ambienteDePruebas: true).Evaluar(efecto, Resultado(CodigosDeCanalExterno.Simulado, simulado: true));

        Assert.True(veredicto.TieneEfecto);
        Assert.Null(veredicto.ComoError());
    }

    [Theory]
    [MemberData(nameof(Efectos))]
    public void Un_canal_real_tiene_efecto_en_los_dos_ambientes(EfectoLegalDeCanal efecto)
    {
        Assert.True(Guardia(false).Evaluar(efecto, Resultado("PROVEEDOR-SMS", simulado: false)).TieneEfecto);
        Assert.True(Guardia(true).Evaluar(efecto, Resultado("PROVEEDOR-SMS", simulado: false)).TieneEfecto);
    }

    [Fact]
    public void La_carga_propia_no_es_simulada_y_vale_en_produccion()
    {
        var veredicto = Guardia(ambienteDePruebas: false).Evaluar(EfectoLegalDeCanal.BureauEvidenceForApproval,
            Resultado(CodigosDeCanalExterno.CargaPropia, simulado: false));

        Assert.True(veredicto.TieneEfecto);
    }

    [Fact]
    public void Un_resultado_del_canal_simulado_que_no_dice_que_es_simulado_igual_no_tiene_efecto_en_produccion()
    {
        // Un adaptador que se equivoca en la bandera no le da efecto legal al simulado.
        var veredicto = Guardia(false).Evaluar(EfectoLegalDeCanal.RealDelivery, Resultado(CodigosDeCanalExterno.Simulado, simulado: false));

        Assert.False(veredicto.TieneEfecto);
    }

    [Fact]
    public void Sin_configurar_el_ambiente_la_guardia_se_comporta_como_produccion()
    {
        var guardia = new GuardiaDeCanalesExternos(Options.Create(new CanalesExternosOptions()));

        Assert.False(guardia.Evaluar(EfectoLegalDeCanal.PromissoryNotePerfection, CodigosDeCanalExterno.Simulado, simulado: true).TieneEfecto);
    }

    [Fact]
    public void Un_rechazo_simulado_tampoco_tiene_efecto_en_produccion()
    {
        var rechazo = ResultadoExterno<bool>.Sin(ExternalOutcome.Rejected, CodigosDeCanalExterno.Simulado, true, "firma rechazada");

        Assert.False(Guardia(false).Evaluar(EfectoLegalDeCanal.PromissoryNotePerfection, rechazo).TieneEfecto);
    }
}
