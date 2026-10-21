using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K3 (T0857; FR-090): <see cref="ClasificadorDeNovedad"/>. La causa la da el motivo y no el mecanismo: mora, dificultades de
/// pago, acuerdo de insolvencia o recogida en mora → reestructuración con cualquier mecanismo; a petición del deudor sin deterioro →
/// modificación sólo si la mora de los últimos meses que fija el Anexo 1 no pasa del límite del segmento. La norma de prueba es la de los
/// casos de riesgo con una regla de modificaciones de prueba (6 meses; 60 días en consumo; 90 en comercial; recaída a 30), no la semilla.
/// </summary>
public class ClasificadorDeNovedadTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 15);

    private static Anexo1 NormaConModificaciones()
    {
        var nodo = JsonNode.Parse(File.ReadAllText(Path.Combine(CasoDoradoDeRiesgo.Directorio, "Normas", "anexo1-prueba.json")),
            documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        nodo["modifications"] = JsonNode.Parse("""
            { "lookbackMonths": 6, "relapseDaysPastDue": 30,
              "limits": [ { "segment": "CONSUMO", "maxDaysPastDue": 60 }, { "segment": "COMERCIAL_PN", "maxDaysPastDue": 90 } ] }
            """);
        var r = Anexo1.Leer(nodo.ToJsonString(), 1);
        return r.Documento ?? throw new InvalidOperationException(string.Join("; ", r.Errores.Select(e => $"{e.Ruta} {e.Mensaje}")));
    }

    private static readonly Anexo1 Norma = NormaConModificaciones();

    private static NovedadParaClasificar Pedido(NoveltyMechanism mecanismo = NoveltyMechanism.Extension, NoveltyRequestMotive motivo = NoveltyRequestMotive.DebtorRequest,
        int diasHoy = 0, string segmento = "CONSUMO", params MoraDelMes[] historial) =>
        new(mecanismo, motivo, segmento, diasHoy, Hoy) { Historial = historial };

    [Theory]
    [InlineData(NoveltyMechanism.Extension)]
    [InlineData(NoveltyMechanism.PaymentDateChange)]
    [InlineData(NoveltyMechanism.RateChange)]
    [InlineData(NoveltyMechanism.Novation)]
    [InlineData(NoveltyMechanism.Refinancing)]
    public void Un_credito_en_mora_es_reestructuracion_cualquiera_sea_el_mecanismo(NoveltyMechanism mecanismo)
    {
        var c = ClasificadorDeNovedad.Clasificar(Norma, Pedido(mecanismo, diasHoy: 5));

        c.Causa.Should().Be(NoveltyCause.Restructuring);
        c.Admitida.Should().BeTrue();
        c.Explicacion.Texto.Should().Contain("el crédito está en mora");
    }

    [Theory]
    [InlineData(NoveltyMechanism.Extension)]
    [InlineData(NoveltyMechanism.RateChange)]
    [InlineData(NoveltyMechanism.Novation)]
    public void Pedida_por_dificultades_de_pago_es_reestructuracion_aunque_este_al_dia(NoveltyMechanism mecanismo)
    {
        var c = ClasificadorDeNovedad.Clasificar(Norma, Pedido(mecanismo, NoveltyRequestMotive.Hardship));

        c.Causa.Should().Be(NoveltyCause.Restructuring);
        c.Problema.Should().BeNull();
    }

    [Fact]
    public void El_acuerdo_de_insolvencia_y_la_recogida_en_mora_son_reestructuracion()
    {
        ClasificadorDeNovedad.Clasificar(Norma, Pedido() with { AcuerdoDeInsolvencia = true }).Causa.Should().Be(NoveltyCause.Restructuring);
        var recogida = ClasificadorDeNovedad.Clasificar(Norma, Pedido(NoveltyMechanism.Refinancing) with { RecogeCreditosEnMora = true });
        recogida.Causa.Should().Be(NoveltyCause.Restructuring);
        recogida.DeterminacionJson.Should().Contain("\"restructuringReasons\":[\"CollectsLoansInArrears\"]");
    }

    [Fact]
    public void A_peticion_del_deudor_sin_deterioro_y_dentro_del_limite_es_modificacion()
    {
        // Mora de 45 días en abril (dentro de los 6 meses: abril a septiembre) y límite de consumo 60.
        var c = ClasificadorDeNovedad.Clasificar(Norma, Pedido(NoveltyMechanism.PaymentDateChange, historial:
            [new MoraDelMes(2026, 4, 45), new MoraDelMes(2026, 7, 10)]));

        c.Causa.Should().Be(NoveltyCause.Modification);
        c.Admitida.Should().BeTrue();
        (c.MoraReciente, c.Limite).Should().Be((45, 60));
        c.Explicacion.Texto.Should().Contain("dentro del límite de CONSUMO (60)").And.Contain("vuelve a ser reestructuración");
    }

    [Fact]
    public void Con_la_mora_reciente_sobre_el_limite_no_se_admite_como_modificacion_y_dice_cuanto()
    {
        var c = ClasificadorDeNovedad.Clasificar(Norma, Pedido(historial: [new MoraDelMes(2026, 5, 75)]));

        c.Causa.Should().Be(NoveltyCause.Modification);
        c.Admitida.Should().BeFalse();
        c.Problema!.Codigo.Should().Be("Lending.Novelty.ModificationNotAllowed");
        c.Problema.Datos.Should().Contain("recentDaysPastDue", 75).And.Contain("limit", 60);
        c.Explicacion.Texto.Should().Contain("se tramita como reestructuración");
    }

    [Fact]
    public void Solo_cuentan_los_meses_de_la_ventana_de_la_norma()
    {
        // Marzo queda fuera de los 6 meses que terminan en septiembre (abril a septiembre): su mora de 200 días no cuenta.
        var c = ClasificadorDeNovedad.Clasificar(Norma, Pedido(historial: [new MoraDelMes(2026, 3, 200), new MoraDelMes(2026, 4, 20)]));

        c.Admitida.Should().BeTrue();
        c.MoraReciente.Should().Be(20);
    }

    [Fact]
    public void El_limite_es_el_del_segmento_del_credito()
    {
        var historial = new[] { new MoraDelMes(2026, 6, 80) };
        ClasificadorDeNovedad.Clasificar(Norma, Pedido(segmento: "COMERCIAL_PN", historial: historial)).Admitida.Should().BeTrue("80 ≤ 90 en comercial");
        ClasificadorDeNovedad.Clasificar(Norma, Pedido(segmento: "CONSUMO", historial: historial)).Admitida.Should().BeFalse("80 > 60 en consumo");
    }

    [Fact]
    public void Sin_regla_o_sin_limite_del_segmento_no_hay_modificacion()
    {
        var sinRegla = CasoDoradoDeRiesgo.LeerNorma("anexo1-prueba");
        sinRegla.Modificaciones.Should().BeNull("la norma de K1 no trae la regla");
        var c = ClasificadorDeNovedad.Clasificar(sinRegla, Pedido());
        c.Problema!.Codigo.Should().Be(ClasificadorDeNovedad.CodigoModificacionNoAdmitida);
        c.Problema.Datos.Should().Contain("limit", null);

        ClasificadorDeNovedad.Clasificar(Norma, Pedido(segmento: "MICROCREDITO")).Admitida.Should().BeFalse("la regla de prueba no fija microcrédito");
    }

    [Fact]
    public void La_explicacion_canonica_va_a_CauseDeterminationJson_y_se_repite()
    {
        var pedido = Pedido(NoveltyMechanism.RateChange, historial: [new MoraDelMes(2026, 8, 12)]);
        var a = ClasificadorDeNovedad.Clasificar(Norma, pedido);
        var b = ClasificadorDeNovedad.Clasificar(Norma, pedido);

        a.DeterminacionJson.Should().Be(b.DeterminacionJson);
        using var json = JsonDocument.Parse(a.DeterminacionJson);
        var raiz = json.RootElement;
        raiz.GetProperty("cause").GetInt32().Should().Be((int)NoveltyCause.Modification);
        raiz.GetProperty("mechanism").GetInt32().Should().Be((int)NoveltyMechanism.RateChange);
        raiz.GetProperty("recentDaysPastDue").GetInt32().Should().Be(12);
        raiz.GetProperty("limit").GetInt32().Should().Be(60);
        raiz.GetProperty("lookbackMonths").GetInt32().Should().Be(6);
        raiz.GetProperty("admitted").GetBoolean().Should().BeTrue();
        raiz.GetProperty("explanation").GetString().Should().Contain("Modificación");
    }

    [Fact]
    public void La_regla_de_modificaciones_se_valida_al_leer_la_norma()
    {
        var nodo = JsonNode.Parse(File.ReadAllText(Path.Combine(CasoDoradoDeRiesgo.Directorio, "Normas", "anexo1-prueba.json")),
            documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        nodo["modifications"] = JsonNode.Parse("""
            { "lookbackMonths": 0, "limits": [ { "segment": "CONSUMO", "maxDaysPastDue": 60 }, { "segment": "CONSUMO", "maxDaysPastDue": 30 },
              { "segment": "OTRO", "maxDaysPastDue": 10 } ] }
            """);

        var r = Anexo1.Leer(nodo.ToJsonString(), 1);

        r.Documento.Should().BeNull();
        r.Errores.Select(e => e.Ruta).Should().Contain(["$.modifications.lookbackMonths", "$.modifications.limits[1].segment", "$.modifications.limits[2].segment"]);
    }
}
