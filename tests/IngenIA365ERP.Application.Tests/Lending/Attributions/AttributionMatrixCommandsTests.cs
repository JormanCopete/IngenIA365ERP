using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Attributions;

/// <summary>
/// Feature 013, T0246 (US17; T63, R132; api.md §3.5.1; data-model §3.1–§3.3): la matriz de atribuciones por
/// <see cref="AddAttributionMatrixVersionCommand"/>, <see cref="ListAttributionMatricesQuery"/>, <see cref="GetAttributionMatrixQuery"/> y
/// <see cref="PreviewAttributionRoutingQuery"/>. Se publica completa: sin nivel colegiado final sin techo → <c>MatrixWithoutTopLevel</c>;
/// rango repetido, permiso fuera de <c>Lending.Decisions.*</c>, dos niveles de excepción o dos consejos → <c>MatrixInvalid</c> con
/// <c>data.reason</c>; un órgano que no decide crédito o inexistente → <c>Lending.GoverningBody.NotFound</c>; <c>ValidFrom</c> anterior a
/// hoy → rechazo; la anterior rige hasta la víspera de la siguiente (lectura bitemporal a D y R); <c>ContentSha256</c> sellado; la vista
/// previa no escribe. Matriz del ensayo (quickstart §2.7): analista hasta 5.000.000, gerencia hasta 20.000.000, comité hasta 100.000.000 y
/// consejo sin techo, con un nivel de excepción.
/// </summary>
public sealed class AttributionMatrixCommandsTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();
    private readonly GoverningBody _comite;
    private readonly GoverningBody _consejo;
    private readonly GoverningBody _junta;

    public AttributionMatrixCommandsTests()
    {
        _comite = _g.Organo("COMITE", "Comité de crédito", GoverningBodyKind.CreditCommittee);
        _consejo = _g.Organo("CONSEJO", "Consejo de administración", GoverningBodyKind.BoardOfDirectors, vinculadosN: 4, vinculadosD: 5);
        _junta = _g.Organo("JUNTA", "Junta de vigilancia", GoverningBodyKind.OversightBoard, decideCredito: false);
        _g.ActuarComo(_g.Usuario(_g.Persona("Ana", "Jefa", "1001"), "jefa@coop.test"));
    }

    public void Dispose() => _g.Dispose();

    private List<AttributionLevelInput> Niveles() =>
    [
        new("ANALISTA", "Analista", 10, AttributionLevelKind.Individual, "Lending.Decisions.Analyst", ConsolidatedAmountCeiling: 5_000_000m),
        new("EXCEP", "Jefe de cartera (excepción)", 15, AttributionLevelKind.Individual, "Lending.Decisions.Exceptions",
            ConsolidatedAmountCeiling: 5_000_000m, IsExceptionLevel: true),
        new("GERENCIA", "Gerencia", 20, AttributionLevelKind.Individual, "Lending.Decisions.Management", ConsolidatedAmountCeiling: 20_000_000m),
        new("COMITE", "Comité de crédito", 30, AttributionLevelKind.Collegiate, GoverningBodyPublicId: _comite.PublicId,
            ConsolidatedAmountCeiling: 100_000_000m),
        new("CONSEJO", "Consejo de administración", 40, AttributionLevelKind.Collegiate, GoverningBodyPublicId: _consejo.PublicId, IsBoardLevel: true),
    ];

    private static List<AttributionRuleInput> Reglas() =>
        [new(1, LendingDecisionSubjects.CreditApplication, "COMITE", new AttributionRuleConditionsDto(PolicyException: true))];

    private AddAttributionMatrixVersionCommand Matriz(DateOnly? desde = null, List<AttributionLevelInput>? niveles = null) =>
        new(desde ?? GobiernoDePrueba.Hoy, "Reglamento de crédito aprobado por el consejo", "Acuerdo 12 de 2026", niveles ?? Niveles(), Reglas())
        {
            OperationKey = Guid.NewGuid(),
        };

    private Task<Result<AttributionMatrixDto>> Publicar(AddAttributionMatrixVersionCommand c) =>
        new AddAttributionMatrixVersionCommandHandler(_g.Db, _g.Actor, _g.Reloj).Handle(c, CancellationToken.None);

    private MatrizVigenteDeAtribuciones Lector() => new(_g.Db);

    private Task<Result<AttributionRoutingPreviewDto>> Vista(PreviewAttributionRoutingQuery q) =>
        new PreviewAttributionRoutingQueryHandler(_g.Db, Lector(), new VinculadosVigentes(_g.Db), _g.Alcance, _g.Auditoria, _g.Reloj)
            .Handle(q, CancellationToken.None);

    private static string Motivo(Error e) => GobiernoDePrueba.Datos(e).GetProperty("reason").GetString()!;

    // ------------------------------------------------------------------------------------------------------- publicar --

    [Fact]
    public async Task Publica_la_matriz_completa_con_sus_niveles_y_reglas_y_sella_la_huella()
    {
        var r = await Publicar(Matriz());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Version.Should().Be(1);
        r.Value.Levels.Select(l => l.Code).Should().Equal("ANALISTA", "EXCEP", "GERENCIA", "COMITE", "CONSEJO");
        r.Value.Levels.Single(l => l.Code == "COMITE").GoverningBody!.PublicId.Should().Be(_comite.PublicId);
        r.Value.Rules.Should().ContainSingle().Which.MinimumLevelCode.Should().Be("COMITE");
        r.Value.ContentSha256.Should().MatchRegex("^[0-9a-f]{64}$");

        var guardada = await _g.Db.AttributionMatrices.SingleAsync();
        guardada.ContentSha256.Should().Be(r.Value.ContentSha256);
        guardada.HuellaDeLoGuardado(await _g.Db.AttributionLevels.ToListAsync(), await _g.Db.AttributionRules.ToListAsync())
            .Should().Be(guardada.ContentSha256, "la huella sellada es la de lo que quedó guardado");
        guardada.RegisteredByUserId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Sin_nivel_colegiado_final_sin_techo_es_MatrixWithoutTopLevel()
    {
        var niveles = Niveles();
        niveles[^1] = niveles[^1] with { ConsolidatedAmountCeiling = 500_000_000m };

        var r = await Publicar(Matriz(niveles: niveles));

        r.Error.Code.Should().Be("Lending.Attribution.MatrixWithoutTopLevel");
        (await _g.Db.AttributionMatrices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rango_repetido_es_MatrixInvalid_DuplicateRank()
    {
        var niveles = Niveles();
        niveles[2] = niveles[2] with { Order = 10 };

        var r = await Publicar(Matriz(niveles: niveles));

        r.Error.Code.Should().Be("Lending.Attribution.MatrixInvalid");
        Motivo(r.Error).Should().Be("DuplicateRank");
    }

    [Fact]
    public async Task Permiso_de_nivel_fuera_de_Lending_Decisions_es_MatrixInvalid()
    {
        var niveles = Niveles();
        niveles[0] = niveles[0] with { PermissionCode = "Lending.Applications.Analyze" };

        var r = await Publicar(Matriz(niveles: niveles));

        r.Error.Code.Should().Be("Lending.Attribution.MatrixInvalid");
        Motivo(r.Error).Should().Be("IndividualLevelPermission");
    }

    [Fact]
    public async Task Dos_niveles_de_excepcion_o_dos_consejos_es_MatrixInvalid()
    {
        var dosExcepciones = Niveles();
        dosExcepciones[0] = dosExcepciones[0] with { IsExceptionLevel = true };
        var dosConsejos = Niveles();
        dosConsejos[3] = dosConsejos[3] with { IsBoardLevel = true };

        var r1 = await Publicar(Matriz(niveles: dosExcepciones));
        var r2 = await Publicar(Matriz(niveles: dosConsejos));

        Motivo(r1.Error).Should().Be("ExceptionLevelCount");
        Motivo(r2.Error).Should().Be("BoardLevelCount");
    }

    [Fact]
    public async Task Un_organo_que_no_decide_credito_o_inexistente_es_GoverningBody_NotFound()
    {
        var conJunta = Niveles();
        conJunta[3] = conJunta[3] with { GoverningBodyPublicId = _junta.PublicId };
        var inexistente = Niveles();
        inexistente[3] = inexistente[3] with { GoverningBodyPublicId = Guid.NewGuid() };

        var r1 = await Publicar(Matriz(niveles: conJunta));
        var r2 = await Publicar(Matriz(niveles: inexistente));

        r1.Error.Code.Should().Be("Lending.GoverningBody.NotFound");
        r2.Error.Code.Should().Be("Lending.GoverningBody.NotFound");
    }

    [Fact]
    public async Task ValidFrom_anterior_a_hoy_se_rechaza()
    {
        var r = await Publicar(Matriz(GobiernoDePrueba.Hoy.AddDays(-1)));

        r.Error.Code.Should().Be("Lending.Attribution.MatrixInvalid");
        Motivo(r.Error).Should().Be("ValidFromInPast");
    }

    [Fact]
    public async Task La_validacion_de_forma_exige_motivo()
    {
        var v = new AddAttributionMatrixVersionCommandValidator();

        v.Validate(Matriz() with { Reason = " " }).IsValid.Should().BeFalse();
        v.Validate(Matriz()).IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------ vigencia --

    [Fact]
    public async Task La_version_anterior_rige_hasta_la_vispera_de_la_siguiente_a_D_y_R()
    {
        (await Publicar(Matriz())).IsSuccess.Should().BeTrue();
        var registroDeLaPrimera = _g.UtcNow;
        _g.UtcNow = GobiernoDePrueba.Ahora.AddHours(2);
        var niveles = Niveles();
        niveles[0] = niveles[0] with { ConsolidatedAmountCeiling = 3_000_000m };
        var segunda = await Publicar(Matriz(GobiernoDePrueba.Hoy.AddDays(10), niveles));
        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? segunda.Error.Message : string.Empty);

        (await Lector().LeerAsync(GobiernoDePrueba.Hoy.AddDays(9), _g.UtcNow))!.Matriz.VersionNumber.Should().Be(1);
        (await Lector().LeerAsync(GobiernoDePrueba.Hoy.AddDays(10), _g.UtcNow))!.Matriz.VersionNumber.Should().Be(2);
        (await Lector().LeerAsync(GobiernoDePrueba.Hoy.AddDays(10), registroDeLaPrimera))!.Matriz.VersionNumber
            .Should().Be(1, "con lo que se sabía antes de registrar la segunda, regía la primera");

        var historia = await new ListAttributionMatricesQueryHandler(_g.Db, _g.Reloj).Handle(new ListAttributionMatricesQuery(IncludeHistory: true), default);
        historia.Value.Select(m => m.Version).Should().Equal(2, 1);
        historia.Value.Single(m => m.Version == 1).ValidTo.Should().Be(GobiernoDePrueba.Hoy.AddDays(9));
        historia.Value.Single(m => m.Version == 2).ValidTo.Should().BeNull();

        var vigentes = await new ListAttributionMatricesQueryHandler(_g.Db, _g.Reloj).Handle(new ListAttributionMatricesQuery(), default);
        vigentes.Value.Select(m => m.Version).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Get_devuelve_la_version_y_un_PublicId_desconocido_es_NotFound()
    {
        var publicada = await Publicar(Matriz());
        var handler = new GetAttributionMatrixQueryHandler(_g.Db);

        (await handler.Handle(new GetAttributionMatrixQuery(publicada.Value.PublicId), default)).Value.ContentSha256
            .Should().Be(publicada.Value.ContentSha256);
        (await handler.Handle(new GetAttributionMatrixQuery(Guid.NewGuid()), default)).Error.Code.Should().Be("Lending.AttributionMatrix.NotFound");
    }

    // ------------------------------------------------------------------------------------------------- vista previa --

    [Theory]
    [InlineData(3_000_000, "ANALISTA")]
    [InlineData(15_000_000, "GERENCIA")]
    [InlineData(60_000_000, "COMITE")]
    [InlineData(150_000_000, "CONSEJO")]
    public async Task La_vista_previa_enruta_por_el_monto_consolidado_y_no_escribe(decimal monto, string nivel)
    {
        await Publicar(Matriz());
        var antes = await _g.Db.AttributionMatrices.CountAsync();

        var r = await Vista(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, monto));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.EffectiveLevel.Code.Should().Be(nivel);
        r.Value.ConsolidatedAmount.Should().Be(monto);
        r.Value.Reasons.Should().Contain(x => x.Kind == RazonDeEnrutamiento.AmountCeiling);
        r.Value.MatrixVersion.Should().Be(1);
        (await _g.Db.AttributionMatrices.CountAsync()).Should().Be(antes);
        _g.Db.ChangeTracker.Entries().Should().NotContain(e => e.State != Microsoft.EntityFrameworkCore.EntityState.Unchanged);
    }

    [Fact]
    public async Task Un_vinculado_va_al_consejo_y_una_alerta_al_menos_a_excepcion()
    {
        await Publicar(Matriz());
        var vinculada = _g.Persona("Carla", "Consejera", "2002");
        _g.Db.RelatedParties.Add(new RelatedParty { PersonId = vinculada.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2026, 1, 1) });
        await _g.Db.SaveChangesAsync();

        var conVinculado = await Vista(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 1_000_000m, PersonPublicId: vinculada.PublicId));
        var conAlerta = await Vista(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 1_000_000m, Alerts: ["Concentracion"]));
        var excepcionAPolitica = await Vista(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 1_000_000m, PolicyException: true));

        conVinculado.Value.EffectiveLevel.Code.Should().Be("CONSEJO");
        conVinculado.Value.Reasons.Should().Contain(x => x.Kind == RazonDeEnrutamiento.RelatedParty);
        conAlerta.Value.EffectiveLevel.Code.Should().Be("EXCEP");
        excepcionAPolitica.Value.EffectiveLevel.Code.Should().Be("COMITE");
        excepcionAPolitica.Value.Reasons.Should().Contain(x => x.Kind == RazonDeEnrutamiento.Rule);
    }

    [Fact]
    public async Task Sin_matriz_vigente_la_vista_previa_es_NotFound()
    {
        var r = await Vista(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 1_000_000m));

        r.Error.Code.Should().Be("Lending.AttributionMatrix.NotFound");
    }
}
