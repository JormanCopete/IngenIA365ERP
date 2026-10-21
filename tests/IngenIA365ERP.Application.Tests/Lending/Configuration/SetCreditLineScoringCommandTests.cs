using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Scoring;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0336 (US1; api.md §3.3; FR-037): el bloque de puntaje de una línea por <see cref="SetCreditLineScoringCommandHandler"/>
/// (ruta con <c>Lending.Scoring.Manage</c>). Registra una versión nueva que copia todo lo demás de la vigente y cambia sólo el puntaje; pesos
/// fuera de 0..1 o que no suman 1 son <c>Lending.Scoring.WeightsInvalid</c> con su motivo; una fuente fuera de la lista que publica el motor
/// de puntaje (<see cref="FuentesDePuntaje"/>) es <c>Lending.Scoring.VariableUnknown</c>; un nivel de excepción que la matriz vigente no
/// tiene es <c>Lending.Attribution.LevelUnknown</c>. La consulta devuelve el bloque vigente y la lista de fuentes.
/// </summary>
public sealed class SetCreditLineScoringCommandTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public SetCreditLineScoringCommandTests() => _l.NormativaAsync(0.25m).GetAwaiter().GetResult();

    public void Dispose() => _l.Dispose();

    private static ScoringVariableDto Variable(string codigo, string fuente, decimal peso) =>
        new(codigo, codigo.ToLowerInvariant(), fuente, peso, [new ScoringRangeDto(null, 30m, 200m), new ScoringRangeDto(30.0001m, null, 800m)]);

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CreditLineVersionDto>> Puntuar(Guid linea, IReadOnlyList<ScoringVariableDto> variables,
        decimal? minimo = 550m, string? nivel = null, DateOnly? desde = null)
    {
        var r = await _l.Puntuar().Handle(new SetCreditLineScoringCommand(linea, desde ?? LineasDePrueba.Hoy, "Modelo aprobado por el comité de riesgos",
            minimo, nivel, variables) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Crea_una_version_que_copia_todo_y_cambia_solo_el_puntaje()
    {
        var linea = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.21m) with { Insurances = [_l.Seguro()] });

        var r = await Puntuar(linea.PublicId, [Variable("EDAD", FuentesDePuntaje.EdadDelSolicitante, 0.4m), Variable("CENTRAL", FuentesDePuntaje.PuntajeDeCentrales, 0.6m)]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.VersionNumber.Should().Be(2);
        r.Value.HasScoring.Should().BeTrue();
        r.Value.MinimumScore.Should().Be(550m);
        r.Value.EffectiveAnnual.Should().Be(0.21m);
        r.Value.Insurances.Should().ContainSingle();
        r.Value.Reason.Should().Be("Modelo aprobado por el comité de riesgos");
        var versiones = await _l.Db.CreditLineVersions.OrderBy(v => v.VersionNumber).ToListAsync();
        versiones[1].ContentSha256.Should().NotBe(versiones[0].ContentSha256, "el puntaje es contenido de la versión");
        versiones[1].ScoringModel.Should().Contain("Applicant.Age");

        var bloque = await new GetCreditLineScoringQueryHandler(_l.Db, _l.G.Alcance, _l.G.Auditoria, _l.G.Reloj)
            .Handle(new GetCreditLineScoringQuery(linea.PublicId), default);
        bloque.Value.Variables.Select(v => v.Code).Should().Equal("CENTRAL", "EDAD");
        bloque.Value.MinimumScore.Should().Be(550m);
        bloque.Value.AllowedSources.Should().Contain(FuentesDePuntaje.MoraMaximaEnCentrales);
    }

    [Theory]
    [InlineData(0.5, 0.4, "WeightsDoNotSumOne")]
    [InlineData(1.2, -0.2, "WeightOutOfRange")]
    [InlineData(0.5, 0.0000005, "WeightOutOfRange")]
    public async Task Pesos_que_no_suman_1_o_fuera_de_0_a_1_son_WeightsInvalid(double a, double b, string motivo)
    {
        var linea = await _l.LineaAsync("CONLIB");

        var r = await Puntuar(linea.PublicId, [Variable("EDAD", FuentesDePuntaje.EdadDelSolicitante, (decimal)a), Variable("CENTRAL", FuentesDePuntaje.PuntajeDeCentrales, (decimal)b)]);

        r.Error.Code.Should().Be("Lending.Scoring.WeightsInvalid");
        LineasDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be(motivo);
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Una_fuente_fuera_de_la_lista_del_motor_es_VariableUnknown()
    {
        var linea = await _l.LineaAsync("CONLIB");

        var r = await Puntuar(linea.PublicId, [Variable("SIGNO", "Applicant.Zodiac", 1m)]);

        r.Error.Code.Should().Be("Lending.Scoring.VariableUnknown");
        var datos = LineasDePrueba.Datos(r.Error);
        datos.GetProperty("variable").GetString().Should().Be("SIGNO");
        datos.GetProperty("allowed").GetArrayLength().Should().Be(FuentesDePuntaje.Todas.Count);
    }

    [Fact]
    public async Task Un_nivel_de_excepcion_que_la_matriz_vigente_no_tiene_es_LevelUnknown()
    {
        var linea = await _l.LineaAsync("CONLIB");
        _l.Db.AttributionMatrices.Add(new AttributionMatrix
        {
            VersionNumber = 1, ValidFrom = new DateOnly(2026, 1, 1), RegisteredAt = LineasDePrueba.Ahora.AddDays(-30), Reason = "Reglamento",
            LegalSource = "Acuerdo 12", ContentSha256 = new string('a', 64), RegisteredByUserId = _l.Jefe.Id,
        });
        await _l.Db.SaveChangesAsync();
        var matriz = await _l.Db.AttributionMatrices.SingleAsync();
        _l.Db.AttributionLevels.Add(new AttributionLevel
        {
            MatrixId = matriz.Id, Code = "EXC", Name = "Excepciones", Rank = 1, Kind = AttributionLevelKind.Individual,
            PermissionCode = "Lending.Decisions.Exceptions", IsExceptionLevel = true,
        });
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        var variables = new[] { Variable("EDAD", FuentesDePuntaje.EdadDelSolicitante, 1m) };

        (await Puntuar(linea.PublicId, variables, nivel: "GERENCIA")).Error.Code.Should().Be("Lending.Attribution.LevelUnknown");
        (await Puntuar(linea.PublicId, variables, nivel: "exc")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_variables_la_version_queda_sin_puntaje()
    {
        var linea = await _l.LineaAsync("CONLIB");
        await Puntuar(linea.PublicId, [Variable("EDAD", FuentesDePuntaje.EdadDelSolicitante, 1m)]);

        var r = await Puntuar(linea.PublicId, [], minimo: null, desde: new DateOnly(2027, 1, 1));

        r.Value.HasScoring.Should().BeFalse();
        r.Value.MinimumScore.Should().BeNull();
    }

    [Fact]
    public async Task Una_linea_cerrada_no_cambia_su_puntaje()
    {
        var linea = await _l.LineaAsync("CONLIB");
        await _l.Cerrar().Handle(new CloseCreditLineCommand(linea.PublicId, "Se reemplaza") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();

        (await Puntuar(linea.PublicId, [Variable("EDAD", FuentesDePuntaje.EdadDelSolicitante, 1m)])).Error.Code.Should().Be("Lending.CreditLine.Closed");
    }
}
