using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US4 (T108, T109; research R07, D32; FR-022, FR-053): el motor del GMF contra sus casos dorados <c>Casos/gmf-*.json</c> —el tope
/// cruzado (6.676), la redención de CDAT abonada (41.952), el traslado del mismo titular, el reverso, la tarifa cero desde una fecha y el tope por
/// titular—, calculados con <c>Casos/calcular.py</c>. Las disposiciones de cada caso van en orden y el uso exento se acumula por mes, como lo lee el
/// contrato bajo el cerrojo de la persona. (nuevo)
/// </summary>
public class GravamenMovimientosFinancierosTests
{
    private sealed record PoliticaJson(DateOnly Desde, decimal Tarifa, decimal TopeExencionUvt, decimal Uvt, GmfExemptionPolicy Politica,
        List<string> Gravados, List<string> Exentos);

    private sealed record MovimientoJson(string Id, DateOnly Fecha, string? Hecho, decimal Monto, bool CuentaSujeta, bool EsLaCuentaMarcada, string? Reversa);

    private sealed record EntradaJson(List<PoliticaJson> Politicas, List<MovimientoJson> Movimientos);

    private sealed record ResultadoJson(string Id, decimal ExentoAplicado, decimal BaseGravada, decimal Valor, decimal ExentoUsadoDespues);

    private sealed record EsperadoJson(List<ResultadoJson> Movimientos, decimal TotalGmf);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("gmf-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Los_casos_de_GMF_de_la_investigacion_estan()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["gmf-01-tope-cruzado.json", "gmf-02-redencion-cdat-abonada.json", "gmf-03-traslado-mismo-titular.json",
            "gmf-04-reverso.json", "gmf-05-tarifa-cero-desde-una-fecha.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_con_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var entrada = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var vigencias = entrada.Politicas.Select(p => new VigenciaDeGmf(p.Desde,
            new PoliticaDeGmf(p.Tarifa, p.TopeExencionUvt, p.Uvt, p.Politica, p.Gravados, p.Exentos))).ToList();

        var usadoPorMes = new Dictionary<(int, int), decimal>();
        var calculados = new Dictionary<string, GmfCalculado>();
        var total = 0m;
        for (var i = 0; i < entrada.Movimientos.Count; i++)
        {
            var m = entrada.Movimientos[i];
            var mes = (m.Fecha.Year, m.Fecha.Month);
            var usado = usadoPorMes.GetValueOrDefault(mes);
            ResultadoJson obtenido;
            if (m.Reversa is { } id)
            {
                var original = calculados[id];
                usado = GravamenMovimientosFinancieros.UsoTrasReverso(usado, original.ExentoAplicado);
                obtenido = new ResultadoJson(m.Id, -original.ExentoAplicado, -original.BaseGravada, -original.Valor, usado);
            }
            else
            {
                var politica = GravamenMovimientosFinancieros.Vigente(vigencias, m.Fecha);
                politica.Should().NotBeNull($"{archivo}: hay política el {m.Fecha}");
                var g = GravamenMovimientosFinancieros.Calcular(politica!, new DisposicionDeGmf(m.Hecho!, m.Monto, m.CuentaSujeta, m.EsLaCuentaMarcada, usado));
                calculados[m.Id] = g;
                usado = g.ExentoUsadoDespues;
                obtenido = new ResultadoJson(m.Id, g.ExentoAplicado, g.BaseGravada, g.Valor, g.ExentoUsadoDespues);
                g.Explicacion.Should().NotBeNullOrWhiteSpace();
            }
            usadoPorMes[mes] = usado;
            total += obtenido.Valor;
            obtenido.Should().BeEquivalentTo(esperado.Movimientos[i], $"{archivo}, movimiento {m.Id}");
        }
        total.Should().Be(esperado.TotalGmf, archivo);
    }

    [Fact]
    public void Un_hecho_que_el_parametro_no_nombra_no_genera_GMF()
    {
        var politica = new PoliticaDeGmf(0.004m, 350m, 52374m, GmfExemptionPolicy.SingleMarkedAccount, [HechosGeneradoresDeGmf.Retiro], []);
        var g = GravamenMovimientosFinancieros.Calcular(politica, new DisposicionDeGmf(HechosGeneradoresDeGmf.Cancelacion, 1_000_000m, true, false, 0m));

        g.Valor.Should().Be(0m);
        g.DejaHuella.Should().BeFalse();
    }

    [Fact]
    public void El_redondeo_y_el_debito_automatico_son_de_la_cooperativa()
    {
        // Decisión del dueño del 2026-10-18 (D54): al peso y el débito automático gravado son defectos, no constantes.
        var politica = new PoliticaDeGmf(0.004m, 0m, 52374m, GmfExemptionPolicy.SingleMarkedAccount,
            [HechosGeneradoresDeGmf.Retiro, HechosGeneradoresDeGmf.DebitoAutomatico], []);
        var retiro = new DisposicionDeGmf(HechosGeneradoresDeGmf.Retiro, 1_669_100m, true, false, 0m);

        GravamenMovimientosFinancieros.Calcular(politica, retiro).Valor.Should().Be(6_676m, "al peso por defecto");
        GravamenMovimientosFinancieros.Calcular(politica with { AlPeso = false }, retiro).Valor.Should().Be(6_676.40m, "al centavo si la cooperativa lo fija");
        GravamenMovimientosFinancieros.Calcular(politica, retiro with { HechoGenerador = HechosGeneradoresDeGmf.DebitoAutomatico }).Valor.Should().Be(6_676m);
        GravamenMovimientosFinancieros.Calcular(politica with { Exentos = [HechosGeneradoresDeGmf.DebitoAutomatico] },
            retiro with { HechoGenerador = HechosGeneradoresDeGmf.DebitoAutomatico }).Valor.Should().Be(0m, "la cooperativa lo puede declarar exento");
        HechosGeneradoresDeGmf.Existe(HechosGeneradoresDeGmf.DebitoAutomatico).Should().BeTrue();
    }

    [Fact]
    public void Una_cuenta_de_una_linea_no_sujeta_no_paga_GMF()
    {
        var politica = new PoliticaDeGmf(0.004m, 350m, 52374m, GmfExemptionPolicy.SingleMarkedAccount, [HechosGeneradoresDeGmf.Retiro], []);
        GravamenMovimientosFinancieros.Calcular(politica, new DisposicionDeGmf(HechosGeneradoresDeGmf.Retiro, 1_000_000m, false, false, 0m)).Valor
            .Should().Be(0m);
    }

    [Theory]
    [InlineData("{\"gravados\": [\"Withdrawal\"], \"exentos\": [\"TransferBetweenSameHolderSavings\"]}", true)]
    [InlineData("{\"gravados\": [\"Lunar\"]}", false)]
    [InlineData("no es json", false)]
    [InlineData("", false)]
    public void Los_hechos_generadores_se_leen_del_parametro_o_fallan_cerrado(string json, bool legible)
    {
        var (gravados, _, problema) = PoliticaDeGmf.LeerHechos(json);
        (problema is null).Should().Be(legible);
        if (legible) gravados.Should().Equal("Withdrawal");
    }

    [Theory]
    [InlineData(HechosGeneradoresDeGmf.EntregaPorFallecimiento)]
    [InlineData(HechosGeneradoresDeGmf.PagoDeNoReclamado)]
    public void La_entrega_por_fallecimiento_y_el_pago_de_no_reclamados_cobran_GMF_como_un_retiro_por_defecto(string hecho)
    {
        // Respuesta 97 del dueño (E253): gravados por defecto, como cualquier retiro; los reconoce el parámetro Gmf.HechosGeneradores.
        var politica = new PoliticaDeGmf(0.004m, 350m, 52374m, GmfExemptionPolicy.SingleMarkedAccount, [HechosGeneradoresDeGmf.Retiro, hecho], []);
        HechosGeneradoresDeGmf.Existe(hecho).Should().BeTrue();
        HechosGeneradoresDeGmf.EsEntregaDeLaRespuesta97(hecho).Should().BeTrue();
        HechosGeneradoresDeGmf.EsEntregaDeLaRespuesta97(HechosGeneradoresDeGmf.Retiro).Should().BeFalse();
        PoliticaDeGmf.LeerHechos($"{{\"gravados\": [\"{hecho}\"], \"exentos\": []}}").Problema.Should().BeNull();
        var g = GravamenMovimientosFinancieros.Calcular(politica, new DisposicionDeGmf(hecho, 500_000m, true, false, 0m));
        g.Valor.Should().Be(2_000m, "500.000 × 0,004 sin exención en una cuenta que no es la marcada");
        GravamenMovimientosFinancieros.SeCobra(g).Should().BeTrue();
    }

    [Fact]
    public void Una_linea_que_apaga_el_GMF_de_la_entrega_no_lo_cobra_y_un_hecho_exento_tampoco()
    {
        var politica = new PoliticaDeGmf(0.004m, 350m, 52374m, GmfExemptionPolicy.SingleMarkedAccount, [HechosGeneradoresDeGmf.EntregaPorFallecimiento], []);
        // La línea «no cobra» llega al motor como cuenta no sujeta.
        GravamenMovimientosFinancieros.Calcular(politica, new DisposicionDeGmf(HechosGeneradoresDeGmf.EntregaPorFallecimiento, 500_000m, false, false, 0m))
            .Valor.Should().Be(0m);
        // El parámetro también puede declararlo exento.
        GravamenMovimientosFinancieros.Calcular(politica with { Exentos = [HechosGeneradoresDeGmf.EntregaPorFallecimiento] },
            new DisposicionDeGmf(HechosGeneradoresDeGmf.EntregaPorFallecimiento, 500_000m, true, false, 0m)).Valor.Should().Be(0m);
        // La cuenta marcada exenta usa el tope mensual como en cualquier retiro (350 UVT = 18.330.900).
        GravamenMovimientosFinancieros.Calcular(politica, new DisposicionDeGmf(HechosGeneradoresDeGmf.EntregaPorFallecimiento, 500_000m, true, true, 0m))
            .Valor.Should().Be(0m);
    }

    [Theory]
    [InlineData(DepositClass.Demand, DepositClass.Demand, true, HechosGeneradoresDeGmf.TrasladoEntreAhorrosDelMismoTitular)]
    [InlineData(DepositClass.Demand, DepositClass.Demand, false, HechosGeneradoresDeGmf.TrasladoAOtroTitular)]
    [InlineData(DepositClass.Demand, DepositClass.TermDeposit, true, HechosGeneradoresDeGmf.TrasladoEntreAhorroYCdat)]
    [InlineData(DepositClass.TermDeposit, DepositClass.Permanent, true, HechosGeneradoresDeGmf.TrasladoEntreAhorroYCdat)]
    public void El_hecho_de_un_traslado_sale_de_las_clases_y_del_titular(DepositClass origen, DepositClass destino, bool mismo, string hecho) =>
        HechosGeneradoresDeGmf.DeTraslado(origen, destino, mismo).Should().Be(hecho);
}
