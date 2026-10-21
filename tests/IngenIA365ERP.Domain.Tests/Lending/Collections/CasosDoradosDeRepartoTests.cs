using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Feature 013, K4, T0920 (FR-118, T77; data-model §26.6): <see cref="RepartoDeGestores"/> coincide con los repartos de
/// <c>Casos/reparto-*.json</c>, calculados con la hoja independiente <c>Casos/reparto.py</c> (no con el motor): la primera regla que casa
/// por prioridad (oficina, etapa, línea, pagaduría, rango de saldo), el reparto por número y por saldo con pesos, el deudor completo a un
/// solo gestor y el gestor vigente que se conserva; el resultado es el mismo en cualquier orden de llegada.
/// </summary>
public class CasosDoradosDeRepartoTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(CasoDoradoDeVentanaLegal.DirectorioDeCasos, "reparto-*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_cinco_casos_del_reparto()
    {
        Casos().Select(c => (string)c[0]).Should().Equal(
            "reparto-01-primera-regla-por-prioridad.json", "reparto-02-por-numero-con-pesos.json", "reparto-03-por-saldo-con-pesos.json",
            "reparto-04-deudor-completo.json", "reparto-05-conserva-el-gestor-vigente.json");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_reparto_coincide_con_la_hoja(string archivo)
    {
        var caso = Cargar(archivo);
        var reglas = caso.Reglas.Select(r => r.DelMotor()).ToList();
        var creditos = caso.Creditos.Select(c => c.DelMotor()).ToList();

        var resultado = RepartoDeGestores.Repartir(reglas, creditos);

        using (new AssertionScope(caso.Caso))
        {
            resultado.SinRegla.Should().Equal(caso.Esperado.SinRegla);
            resultado.Asignaciones.Should().HaveCount(caso.Esperado.Asignaciones.Count);
            for (var i = 0; i < resultado.Asignaciones.Count && i < caso.Esperado.Asignaciones.Count; i++)
            {
                var a = resultado.Asignaciones[i];
                var e = caso.Esperado.Asignaciones[i];
                a.CreditoPublicId.Should().Be(e.Credito, $"asignación {i + 1}");
                a.DeudorPublicId.Should().Be(e.Deudor, $"asignación {i + 1}");
                a.Creditos.Should().Equal(e.Creditos, $"asignación {i + 1}");
                a.GestorPublicId.Should().Be(e.Gestor, $"asignación {i + 1}");
                a.ReglaPublicId.Should().Be(e.Regla, $"asignación {i + 1}");
                a.SeMantiene.Should().Be(e.SeMantiene, $"asignación {i + 1}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_orden_de_llegada_no_cambia_el_reparto(string archivo)
    {
        var caso = Cargar(archivo);
        var reglas = caso.Reglas.Select(r => r.DelMotor()).ToList();
        var creditos = caso.Creditos.Select(c => c.DelMotor()).ToList();

        var directo = RepartoDeGestores.Repartir(reglas, creditos);
        var alReves = RepartoDeGestores.Repartir(Enumerable.Reverse(reglas).ToList(), Enumerable.Reverse(creditos).ToList());

        alReves.SinRegla.Should().Equal(directo.SinRegla);
        alReves.Asignaciones.Select(Clave).Should().Equal(directo.Asignaciones.Select(Clave));
    }

    [Fact]
    public void Una_regla_sin_gestores_o_con_peso_cero_no_se_admite()
    {
        var sinGestores = new ReglaDeReparto(Guid.NewGuid(), 1, AssignmentDistribution.ByCount, []);
        var pesoCero = new ReglaDeReparto(Guid.NewGuid(), 1, AssignmentDistribution.ByBalance, [new GestorDeRegla(Guid.NewGuid(), 0m)]);
        var invertida = new ReglaDeReparto(Guid.NewGuid(), 1, AssignmentDistribution.ByCount, [new GestorDeRegla(Guid.NewGuid(), 1m)])
        {
            SaldoMinimo = 10m, SaldoMaximo = 5m,
        };

        RepartoDeGestores.Validar(sinGestores)!.Codigo.Should().Be(RepartoDeGestores.CodigoReglaInvalida);
        RepartoDeGestores.Validar(pesoCero)!.Codigo.Should().Be(RepartoDeGestores.CodigoReglaInvalida);
        RepartoDeGestores.Validar(invertida)!.Codigo.Should().Be(RepartoDeGestores.CodigoReglaInvalida);
        var repartir = () => RepartoDeGestores.Repartir([pesoCero], []);
        repartir.Should().Throw<ReglaDeCarteraException>();
    }

    private static string Clave(AsignacionPropuesta a) =>
        $"{a.CreditoPublicId}|{a.DeudorPublicId}|{string.Join(',', a.Creditos)}|{a.GestorPublicId}|{a.ReglaPublicId}|{a.SeMantiene}";

    private static CasoJson Cargar(string archivo) =>
        JsonSerializer.Deserialize<CasoJson>(File.ReadAllText(Path.Combine(CasoDoradoDeVentanaLegal.DirectorioDeCasos, archivo)), Opciones)
        ?? throw new InvalidOperationException($"El caso {archivo} está vacío.");

    private sealed class CasoJson
    {
        public string Caso { get; set; } = string.Empty;
        public List<ReglaJson> Reglas { get; set; } = [];
        public List<CreditoJson> Creditos { get; set; } = [];
        public EsperadoJson Esperado { get; set; } = new();
    }

    private sealed class ReglaJson
    {
        public Guid PublicId { get; set; }
        public int Prioridad { get; set; }
        public AssignmentDistribution Distribucion { get; set; }
        public bool DeudorCompleto { get; set; }
        public Guid? Oficina { get; set; }
        public string? Etapa { get; set; }
        public int? Linea { get; set; }
        public int? Pagaduria { get; set; }
        public decimal? SaldoMinimo { get; set; }
        public decimal? SaldoMaximo { get; set; }
        public List<GestorJson> Gestores { get; set; } = [];

        public ReglaDeReparto DelMotor() => new(PublicId, Prioridad, Distribucion, Gestores.Select(g => new GestorDeRegla(g.Usuario, g.Peso)).ToList())
        {
            DeudorCompleto = DeudorCompleto,
            Oficina = Oficina,
            Etapa = Etapa,
            Linea = Linea,
            Pagaduria = Pagaduria,
            SaldoMinimo = SaldoMinimo,
            SaldoMaximo = SaldoMaximo,
        };
    }

    private sealed class GestorJson
    {
        public Guid Usuario { get; set; }
        public decimal Peso { get; set; }
    }

    private sealed class CreditoJson
    {
        public Guid Credito { get; set; }
        public Guid Deudor { get; set; }
        public decimal Saldo { get; set; }
        public Guid? Oficina { get; set; }
        public string? Etapa { get; set; }
        public int? Linea { get; set; }
        public int? Pagaduria { get; set; }
        public Guid? GestorActual { get; set; }

        public CreditoParaReparto DelMotor() => new(Credito, Deudor, Saldo)
        {
            Oficina = Oficina, Etapa = Etapa, Linea = Linea, Pagaduria = Pagaduria, GestorActual = GestorActual,
        };
    }

    private sealed class EsperadoJson
    {
        public List<AsignacionJson> Asignaciones { get; set; } = [];
        public List<Guid> SinRegla { get; set; } = [];
    }

    private sealed class AsignacionJson
    {
        public Guid? Credito { get; set; }
        public Guid Deudor { get; set; }
        public List<Guid> Creditos { get; set; } = [];
        public Guid Gestor { get; set; }
        public Guid Regla { get; set; }
        public bool SeMantiene { get; set; }
    }
}
