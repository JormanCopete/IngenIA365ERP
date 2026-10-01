using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Approvals;

namespace IngenIA365ERP.Domain.Tests.Approvals;

/// <summary>
/// T010 (feature 012; decisiones-transversales T33, T34; FR-009, FR-010; quickstart §1.1 «Aprobaciones»): los casos
/// dorados del motor puro de aprobaciones. Cada archivo de <c>Approvals/Casos</c> trae las políticas vigentes del
/// sujeto, los montos a evaluar con su monto máximo del permiso y lo esperado (sin aprobación, niveles en orden o
/// <c>AmountExceedsLimit</c>), o la segregación de un documento con los decisores y la razón por la que cada uno
/// queda excluido. Los valores se derivaron a mano; la derivación va en el caso.
/// </summary>
public class EvaluadorDePoliticaTests
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Approvals", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_trece_casos()
    {
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").Should().HaveCount(13);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_evaluador_coincide_con_el_caso(string archivo)
    {
        var caso = JsonSerializer.Deserialize<CasoDeAprobacion>(File.ReadAllText(Path.Combine(DirectorioDeCasos, archivo)), Opciones)!;
        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        caso.Derivacion.Should().NotBeNullOrWhiteSpace("todo caso dorado lleva su derivación a mano");
        (caso.Evaluaciones.Count + (caso.Segregacion?.Decisiones.Count ?? 0)).Should().BePositive("el caso comprueba algo");

        foreach (var ev in caso.Evaluaciones)
        {
            var sujeto = ev.Subject ?? caso.Subject ?? throw new InvalidOperationException("El caso no dice el sujeto.");
            var fecha = ev.Fecha ?? caso.Fecha ?? throw new InvalidOperationException("El caso no dice la fecha.");
            var politicas = (ev.Politicas ?? caso.Politicas).Select(p => p.ADominio()).ToList();

            var politica = EvaluadorDePolitica.ElegirPolitica(politicas, ev.DocumentType, fecha);
            var r = EvaluadorDePolitica.Evaluar(sujeto, ev.Monto, politica, ev.MontoMaximo);

            var porque = $"{sujeto} {ev.Monto} (máx. {ev.MontoMaximo?.ToString() ?? "sin límite"}) el {fecha:yyyy-MM-dd}";
            r.Resultado.Should().Be(ev.Esperado.Resultado, porque);
            r.Niveles.Select(n => n.Order).Should().Equal(ev.Esperado.Niveles, porque);
            if (ev.Esperado.Permisos is { } permisos)
                r.Niveles.Select(n => n.PermissionCode).Should().Equal(permisos, porque);
            if (ev.Esperado.NivelForzado is { } forzado)
                r.NivelForzado.Should().Be(forzado, porque);
            if (ev.Esperado.ReglaFija is { } fija)
                r.ReglaFija.Should().Be(fija, porque);
            if (ev.Esperado.Resultado == ResultadoDeEvaluacion.ExcedeLimite)
                r.MontoMaximo.Should().Be(ev.Esperado.MontoMaximo, porque);
        }

        if (caso.Segregacion is { } s)
        {
            var participantes = new ParticipantesDeAprobacion(s.Creador, s.Solicitante, s.Participantes, s.AprobadoresPrevios);
            foreach (var d in s.Decisiones)
                EvaluadorDePolitica.ValidarDecision(d.Decisor, participantes).Should().Be(d.Esperado, $"decisor {d.Decisor}");
        }
    }

    // ------------------------------------------------------------------------------------ fuera de los casos --

    [Fact]
    public void Si_el_creador_tambien_pidio_la_aprobacion_manda_Creator()
    {
        var p = new ParticipantesDeAprobacion(10, 10, [], []);

        EvaluadorDePolitica.ValidarDecision(10, p).Should().Be(MotivoDeExclusion.Creator);
    }

    [Theory]
    [InlineData("1:0,2:100", null)]
    [InlineData("1:100,2:100", null)]
    [InlineData("", null)]
    [InlineData("1:0,3:100", "orden")]
    [InlineData("2:0", "orden")]
    [InlineData("1:0,1:100", "orden")]
    [InlineData("1:100,2:50", "umbral")]
    [InlineData("1:-1", "umbral")]
    public void Los_niveles_van_1_a_n_con_umbrales_no_decrecientes_y_no_negativos(string niveles, string? falla)
    {
        var lista = niveles.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Split(':'))
            .Select(p => new NivelDeAprobacion(int.Parse(p[0]), decimal.Parse(p[1]), "Inventory.Approvals.Supervisor"))
            .ToList();

        var motivo = EvaluadorDePolitica.ValidarNiveles(lista);

        if (falla is null) motivo.Should().BeNull();
        else motivo.Should().NotBeNull().And.Contain(falla);
    }

    [Fact]
    public void Un_nivel_sin_permiso_no_es_valido()
    {
        EvaluadorDePolitica.ValidarNiveles([new NivelDeAprobacion(1, 0, " ")]).Should().Contain("permiso");
    }

    [Fact]
    public void Los_sujetos_son_los_de_la_tabla_de_data_model()
    {
        ApprovalSubjects.Todos.Should().Equal("DocumentConfirmation", "DiscountOverCap", "ProvisionalCredit", "TransferDiscrepancy", "PurchaseMatchException");
        ApprovalSubjects.EsValido("DocumentConfirmation").Should().BeTrue();
        ApprovalSubjects.EsValido("documentconfirmation").Should().BeFalse("el sujeto se escribe exacto");
        ApprovalSubjects.EsValido("Otro").Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ el formato --

    private sealed record CasoDeAprobacion(
        string Nombre,
        string Derivacion,
        string? Subject,
        DateOnly? Fecha,
        List<PoliticaDelCaso> Politicas,
        List<EvaluacionDelCaso> Evaluaciones,
        SegregacionDelCaso? Segregacion)
    {
        public List<PoliticaDelCaso> Politicas { get; init; } = Politicas ?? [];
        public List<EvaluacionDelCaso> Evaluaciones { get; init; } = Evaluaciones ?? [];
    }

    private sealed record PoliticaDelCaso(Guid? DocumentType, DateOnly ValidFrom, DateOnly? ValidTo, List<NivelDelCaso> Niveles)
    {
        public PoliticaDeAprobacion ADominio() =>
            new(DocumentType, ValidFrom, ValidTo, Niveles.Select(n => new NivelDeAprobacion(n.Order, n.Threshold, n.Permission)).ToList());
    }

    private sealed record NivelDelCaso(int Order, decimal Threshold, string Permission);

    private sealed record EvaluacionDelCaso(
        string? Subject,
        DateOnly? Fecha,
        Guid? DocumentType,
        decimal Monto,
        decimal? MontoMaximo,
        List<PoliticaDelCaso>? Politicas,
        EsperadoDelCaso Esperado);

    private sealed record EsperadoDelCaso(
        ResultadoDeEvaluacion Resultado,
        List<int> Niveles,
        List<string>? Permisos,
        bool? NivelForzado,
        bool? ReglaFija,
        decimal? MontoMaximo);

    private sealed record SegregacionDelCaso(int Creador, int Solicitante, List<int> Participantes, List<int> AprobadoresPrevios, List<DecisionDelCaso> Decisiones);

    private sealed record DecisionDelCaso(int Decisor, MotivoDeExclusion? Esperado);
}
