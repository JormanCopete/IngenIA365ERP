using FluentAssertions;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Xunit;
using P = IngenIA365ERP.Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// Feature 015, T0016 (data-model §23; FR-009, FR-173, FR-178): cada clave de Recaudo (<c>RCD</c>) y Tesorería (<c>TES</c>) está registrada en el
/// catálogo cerrado con su módulo, su defecto y la marca «pendiente de validar» donde data-model dice P; las «sin valor» no se siembran (admiten
/// vacío y su defecto es vacío); <c>Conservacion.Anios</c> no admite menos de cinco años; la hora de corte tiene que caer antes del ciclo de
/// Cartera; y una versión nueva no es retroactiva sobre lo sellado, porque el lector lee a la fecha que se le pide.
/// </summary>
public class ParametrosDeRecaudoYTesoreriaTests
{
    private static DefinicionDeParametro Def(string modulo, string clave) =>
        CatalogoDeParametros.Buscar(modulo, clave) ?? throw new InvalidOperationException($"{modulo}.{clave} no está en el catálogo");

    public static TheoryData<string, string, string, bool> ClavesConDefecto => new()
    {
        { P.ModuloRecaudo, P.RecaudoHoraDeCorte, "18:00", false },
        { P.ModuloRecaudo, P.CajaArqueoCiego, "true", false },
        { P.ModuloRecaudo, P.CajaBaseModo, "DailyBase", false },
        { P.ModuloRecaudo, P.CajaTratamientoFaltante, "CashierReceivable", false },
        { P.ModuloRecaudo, P.CajaAdmiteCheque, "false", false },
        { P.ModuloRecaudo, P.CajaAdmiteChequeDeTercero, "true", false },
        { P.ModuloRecaudo, P.CajaFechaValorAnteriorMaxDias, "0", false },
        { P.ModuloRecaudo, P.ChequesPlazoPresentacionMismaPlaza, "15", true },
        { P.ModuloRecaudo, P.PartidasEscalonesDias, "30,60,90", true },
        { P.ModuloRecaudo, P.ExtractosDiasHabilesSinCarga, "2", false },
        { P.ModuloRecaudo, P.SarlaftUmbralDeclaracionOrigenFondos, "5000000", true },
        { P.ModuloRecaudo, P.SarlaftUmbralTransaccionIndividual, "5000000", true },
        { P.ModuloRecaudo, P.SarlaftUmbralTransaccionesMultiples, "30000000", true },
        { P.ModuloRecaudo, P.SarlaftReporteEstado, "Vigente", true },
        { P.ModuloRecaudo, P.SarlaftReporteAlcance, "SoloEfectivo", true },
        { P.ModuloRecaudo, P.SarlaftPagadorTerceroFrecuenteTitulares, "3", true },
        { P.ModuloRecaudo, P.SarlaftConsignanteRepetidoPartidas, "2", true },
        { P.ModuloRecaudo, P.SarlaftListasNoVinculantesEfecto, "Sigue", false },
        { P.ModuloRecaudo, P.ContabilidadFaltaDeRegla, "Bloquear", false },
        { P.ModuloRecaudo, P.ContabilidadModoDePaso, "PorLotes", false },
        { P.ModuloRecaudo, P.ConservacionAnios, "10", true },
        { P.ModuloTesoreria, P.ContabilidadFaltaDeRegla, "Bloquear", false },
        { P.ModuloTesoreria, P.ContabilidadModoDePaso, "EnLinea", false },
        { P.ModuloTesoreria, P.ConservacionAnios, "10", true },
        { P.ModuloTesoreria, P.GirosDiasSinResultado, "3", true },
        { P.ModuloTesoreria, P.ChequesDiasSinCobrar, "30", true },
        { P.ModuloTesoreria, P.ChequesDiasACuentaPorPagar, "180", true },
        { P.ModuloTesoreria, P.LotesTerceraPersonaParaResultado, "false", false },
        { P.ModuloTesoreria, P.RemesasDiasParaConfirmar, "2", false },
        { P.ModuloTesoreria, P.ConciliacionDiasDeHolgura, "3", false },
        { P.ModuloTesoreria, P.GmfTarifa, "0.004", true },
        { P.ModuloTesoreria, P.GmfExencionDesembolsos, "true", true },
    };

    [Theory]
    [MemberData(nameof(ClavesConDefecto))]
    public void Cada_clave_tiene_su_modulo_su_defecto_y_su_marca(string modulo, string clave, string defecto, bool pendiente)
    {
        var d = Def(modulo, clave);
        d.DefectoSeguro.Should().Be(defecto);
        (d.PendienteDeValidar is not null).Should().Be(pendiente);
        d.Interpretar(d.DefectoSeguro).Admitido.Should().BeTrue("el defecto se lee como valor válido");
        d.AdmiteAmbito(ParameterScopeKind.None).Should().BeTrue("toda clave admite el ámbito general");
        d.ExigeFuenteLegal.Should().BeFalse("la fuente va en la vigencia, como en Cartera");
    }

    [Theory]
    [InlineData(P.ModuloRecaudo, P.CajaSobrantesDiasAntesDeIngreso)]
    [InlineData(P.ModuloTesoreria, P.CuentasDestinoDiasDeEspera)]
    [InlineData(P.ModuloTesoreria, P.CuentasDestinoDiasDeMarca)]
    public void Las_claves_sin_valor_no_se_siembran_y_no_rigen(string modulo, string clave)
    {
        var d = Def(modulo, clave);
        d.AdmiteVacio.Should().BeTrue();
        d.DefectoSeguro.Should().BeEmpty();
        d.Interpretar(string.Empty).Valor.Should().BeNull("sin vigencia, el lector devuelve vacío: no rige");
    }

    [Fact]
    public void Conservacion_no_admite_menos_de_cinco_anios_en_ningun_modulo()
    {
        foreach (var modulo in new[] { P.ModuloRecaudo, P.ModuloTesoreria })
        {
            var d = Def(modulo, P.ConservacionAnios);
            d.Interpretar("4").Admitido.Should().BeFalse();
            d.Interpretar("5").Admitido.Should().BeTrue();
        }
    }

    [Fact]
    public void Las_claves_de_R1_y_T1_no_se_ofrecen_en_R0_T0()
    {
        Def(P.ModuloRecaudo, P.LibranzaDiasHabilesGiro).DisponibleEnLaEntrega().Should().BeFalse("es informativo de R1");
        Def(P.ModuloTesoreria, P.ContabilidadCierreExigeConciliaciones).DisponibleEnLaEntrega().Should().BeFalse("es de T1");
        Def(P.ModuloRecaudo, P.RecaudoHoraDeCorte).DisponibleEnLaEntrega().Should().BeTrue();
    }

    [Fact]
    public void Los_modulos_tienen_su_ruta_propia_y_no_aparecen_en_la_de_inventario()
    {
        CatalogoDeParametros.ModulosConRutaPropia.Should().Contain([P.ModuloRecaudo, P.ModuloTesoreria, "LND"]);
        CatalogoDeParametros.Todas.Count(d => d.Modulo is P.ModuloRecaudo or P.ModuloTesoreria).Should().Be(P.Definiciones.Count);
    }

    [Theory]
    [InlineData("18:00", "00:30", true)]
    [InlineData("23:59", "00:30", true)]
    [InlineData("18:00", "20:00", true)]
    [InlineData("21:00", "20:00", false)]
    public void La_hora_de_corte_cae_antes_del_ciclo_de_Cartera(string corte, string inicio, bool valida)
    {
        ReglasDeParametrosDeRecaudo.CorteValido(TimeOnly.Parse(corte), TimeOnly.Parse(inicio)).Should().Be(valida);
    }

    [Fact]
    public async Task Una_hora_de_corte_despues_del_ciclo_se_rechaza_con_su_codigo()
    {
        var reglas = new ReglasDeParametrosDeRecaudo(new TimeOnly(20, 0));
        var alta = new AltaDeParametro(Def(P.ModuloRecaudo, P.RecaudoHoraDeCorte), ParameterScopeKind.None, null, null, "21:00", new DateOnly(2026, 12, 1), false);

        var r = await reglas.EvaluarAsync(alta, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoCorteDespuesDelCicloDeCartera);
        (await reglas.EvaluarAsync(alta with { Valor = "19:00" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Una_version_nueva_no_es_retroactiva_porque_lo_sellado_se_lee_a_su_fecha()
    {
        var vieja = new VigenciaDeParametro(Guid.NewGuid(), P.ModuloRecaudo, P.CajaFechaValorAnteriorMaxDias, ParameterScopeKind.None, 0, "0",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 30), "inicial", null, "x", DateTime.UtcNow);
        var nueva = vieja with { PublicId = Guid.NewGuid(), Value = "3", ValidFrom = new DateOnly(2026, 12, 1), ValidTo = null };

        vieja.VigenteEn(new DateOnly(2026, 11, 15)).Should().BeTrue("lo que se selló el 15/11 sigue con la vigencia de ese día");
        nueva.VigenteEn(new DateOnly(2026, 11, 15)).Should().BeFalse();
        nueva.VigenteEn(new DateOnly(2026, 12, 2)).Should().BeTrue();
    }
}
