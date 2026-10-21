using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US11 (T188; FR-055, D20, R23; research §20): el motor del límite inembargable contra sus casos dorados <c>Casos/embargo-*.json</c> —dos
/// cuentas sobre el límite que retienen sólo el exceso, alimentos sin límite, el orden de clases de la cooperativa, el coactivo sobre la cuenta más antigua,
/// el segundo embargo de la misma persona, el CDAT y la clase inembargable, la persona jurídica—, calculados por <c>Casos/calcular.py</c>. Más las reglas
/// que no son de un caso. (nuevo)
/// </summary>
public class LimiteInembargableTests
{
    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("embargo-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Estan_los_casos_de_la_historia()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["embargo-01-exceso-sobre-el-limite.json", "embargo-02-alimentos.json", "embargo-04-coactivo-cuenta-mas-antigua.json",
            "embargo-05-segundo-embargo.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);

        var r = LimiteInembargable.Calcular(new PedidoDeEmbargo(e.Tope, e.PorAlimentos, e.Limite,
            Enum.Parse<AplicacionDelLimiteInembargable>(e.Aplicacion), e.OrdenDeClases.Select(Enum.Parse<DepositClass>).ToList(),
            e.Cuentas.Select(c => new CuentaAnteElEmbargo(c.Id, c.Clase is null ? null : Enum.Parse<DepositClass>(c.Clase), DateOnly.Parse(c.AbiertaEl), c.Saldo,
                c.RetenidoPorOtros, c.Protegida, c.Embargable)).ToList()));

        r.LimiteAplicado.Should().Be(esperado.LimiteAplicado, archivo);
        r.Protegido.Should().Be(esperado.Protegido, archivo);
        r.TotalEmbargable.Should().Be(esperado.TotalEmbargable, archivo);
        r.Retenido.Should().Be(esperado.Retenido, archivo);
        r.Cuentas.Select(c => (c.CuentaId, c.Orden, c.Protegido, c.Embargable, c.Retenido))
            .Should().Equal(esperado.Cuentas.Select(c => (c.Id, c.Orden, c.Protegido, c.Embargable, c.Retenido)), archivo);
        r.Explicacion.Should().NotBeEmpty();
    }

    [Fact]
    public void Lo_retenido_nunca_supera_lo_embargable_ni_el_tope()
    {
        var cuentas = new[]
        {
            new CuentaAnteElEmbargo(1, DepositClass.Demand, new DateOnly(2020, 1, 1), 5_000m, 0m, true, true),
            new CuentaAnteElEmbargo(2, DepositClass.Demand, new DateOnly(2021, 1, 1), -300m, 0m, true, true),
        };
        var r = LimiteInembargable.Calcular(new PedidoDeEmbargo(1_000m, false, 2_000m, AplicacionDelLimiteInembargable.AllProtectedAccounts, [], cuentas));
        r.Retenido.Should().Be(1_000m);
        r.Cuentas.Should().OnlyContain(c => c.Retenido <= c.Embargable && c.Embargable >= 0m);
    }

    [Fact]
    public void Sin_cuentas_no_retiene_nada()
    {
        var r = LimiteInembargable.Calcular(new PedidoDeEmbargo(null, false, 1m, AplicacionDelLimiteInembargable.AllProtectedAccounts, [], []));
        r.Retenido.Should().Be(0m);
        r.Cuentas.Should().BeEmpty();
    }

    [Fact]
    public void Un_tope_o_un_limite_negativo_es_un_error_de_quien_llama()
    {
        var act = () => LimiteInembargable.Calcular(new PedidoDeEmbargo(-1m, false, 1m, AplicacionDelLimiteInembargable.AllProtectedAccounts, [], []));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class EntradaJson
    {
        public decimal? Tope { get; set; }
        public bool PorAlimentos { get; set; }
        public decimal? Limite { get; set; }
        public string Aplicacion { get; set; } = "AllProtectedAccounts";
        public List<string> OrdenDeClases { get; set; } = [];
        public List<CuentaJson> Cuentas { get; set; } = [];
    }

    private sealed class CuentaJson
    {
        public int Id { get; set; }
        public string? Clase { get; set; }
        public string AbiertaEl { get; set; } = string.Empty;
        public decimal Saldo { get; set; }
        public decimal RetenidoPorOtros { get; set; }
        public bool Protegida { get; set; }
        public bool Embargable { get; set; }
    }

    private sealed class EsperadoJson
    {
        public decimal LimiteAplicado { get; set; }
        public decimal Protegido { get; set; }
        public decimal TotalEmbargable { get; set; }
        public decimal Retenido { get; set; }
        public List<CuentaEsperadaJson> Cuentas { get; set; } = [];
    }

    private sealed class CuentaEsperadaJson
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        public decimal Protegido { get; set; }
        public decimal Embargable { get; set; }
        public decimal Retenido { get; set; }
    }
}
