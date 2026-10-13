using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0680 (T45, R54, C52; api.md §12.1; data-model §23.10): registrar una versión de la política SARC rige desde hoy o después
/// (<c>RetroactiveValidFrom</c>), no sale del esquema de la norma (<c>SchemaInvalid</c> con la ruta) ni es menos estricta en ninguna celda
/// (<c>LessStrict</c> con <c>data.cells</c>), y guarda el JSON canónico con su huella; validarla exige el punto del acta del consejo
/// (<c>Lending.Session.ItemMismatch</c>), otra persona que quien la registró (segregación) y se hace una sola vez (<c>AlreadyValidated</c>).
/// </summary>
public class RiskPolicyCommandsTests : IAsyncLifetime
{
    private static readonly DateOnly Enero = new(2027, 1, 1);
    private const string Endurece = """{ "generalImpairment": { "minimumRate": 0.02 } }""";

    private CierreDePrueba _c = null!;
    private User _gerente = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        _gerente = _c.D.O.UsuarioConPersona("Gerente", "9007001");
        _c.G.ActuarComo(_c.Jefe);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private Task<Result<RiskPolicyVersionDto>> RegistrarAsync(string overrides, DateOnly? desde = null) =>
        new AddRiskPolicyVersionCommandHandler(_c.Db, new LectorDeDocumentoNormativo(_c.Db), _c.G.Intervenciones(), _c.G.Actor, _c.G.Reloj)
            .Handle(new AddRiskPolicyVersionCommand(desde ?? Enero, overrides, "Política del consejo 2027") { OperationKey = Guid.NewGuid() }, default);

    private Task<Result<RiskPolicyVersionDto>> ValidarAsync(Guid version, Guid punto) =>
        new ValidateRiskPolicyVersionCommandHandler(_c.Db, new PuntoDeActaCerrado(_c.Db), _c.G.Intervenciones(), _c.G.Actor, _c.G.Reloj)
            .Handle(new ValidateRiskPolicyVersionCommand(version, punto) { OperationKey = Guid.NewGuid() }, default);

    private Guid Acta(Guid version, string sujeto = LendingDecisionSubjects.RiskPolicyVersion)
    {
        var organo = new GoverningBody { Code = "CONSEJO" + _c.Db.GoverningBodies.Count(), Name = "Consejo de administración", Kind = GoverningBodyKind.BoardOfDirectors, MinutesPrefix = "CA-", CreatedBy = "test" };
        _c.Db.GoverningBodies.Add(organo);
        _c.Db.SaveChanges();
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = CierreDePrueba.Hoy, Status = SessionStatus.Closed, CreatedBy = "test" };
        _c.Db.LendingSessions.Add(sesion);
        _c.Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = sujeto, SubjectPublicId = version,
            Title = "Política SARC", Outcome = CreditDecisionOutcome.Approve, CreatedBy = "test",
        };
        _c.Db.SessionItems.Add(punto);
        _c.Db.SaveChanges();
        _c.Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    [Fact]
    public async Task Registrar_guarda_la_version_pendiente_con_el_json_canonico_y_su_huella()
    {
        var r = await RegistrarAsync("""{"generalImpairment":{   "minimumRate":0.02 }}""");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Should().BeEquivalentTo(new { VersionNumber = 1, ValidFrom = Enero, Status = nameof(RiskPolicyVersionStatus.PendingValidation) }, o => o.ExcludingMissingMembers());
        var guardada = await _c.Db.RiskPolicyVersions.AsNoTracking().SingleAsync();
        guardada.Overrides.Should().Be(ContenidoCanonico.Canonizar(Endurece), "se guarda el JSON canónico: mismo contenido, misma huella");
        guardada.OverridesSha256.Should().Be(ContenidoCanonico.Sha256(ContenidoCanonico.Canonizar(Endurece)));
        guardada.BaseDocumentSha256.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Una_celda_menos_estricta_responde_LessStrict_con_las_celdas()
    {
        var r = await RegistrarAsync("""{ "generalImpairment": { "minimumRate": 0.005 } }""");

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoPoliticaMenosEstricta);
        var celda = GobiernoDePrueba.Datos(r.Error).GetProperty("cells")[0];
        celda.GetProperty("path").GetString().Should().Be("generalImpairment.minimumRate");
        (celda.GetProperty("norm").GetDecimal(), celda.GetProperty("proposed").GetDecimal()).Should().Be((0.01m, 0.005m));
        (await _c.Db.RiskPolicyVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_segmento_que_la_norma_no_trae_responde_SchemaInvalid_con_la_ruta()
    {
        var r = await RegistrarAsync("""{ "segments": [ { "code": "INEXISTENTE", "impairmentRates": [ { "category": "C", "rate": 0.5 } ] } ] }""");

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoPoliticaFueraDeEsquema);
        GobiernoDePrueba.Datos(r.Error).GetProperty("errors")[0].GetProperty("path").GetString().Should().Be("$.segments[0].code", "el esquema de la política ya conoce los segmentos del anexo");
    }

    [Fact]
    public async Task Antes_de_hoy_responde_RetroactiveValidFrom()
    {
        (await RegistrarAsync(Endurece, CierreDePrueba.Hoy.AddDays(-1))).Error.Code.Should().Be(ErroresDeRiesgo.CodigoPoliticaRetroactiva);
    }

    [Fact]
    public async Task Validar_con_el_acta_y_otra_persona_la_deja_validada_una_sola_vez()
    {
        var version = (await RegistrarAsync(Endurece)).Value;
        var punto = Acta(version.PublicId);
        _c.G.ActuarComo(_gerente);

        var r = await ValidarAsync(version.PublicId, punto);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(nameof(RiskPolicyVersionStatus.Validated));
        r.Value.SessionItemPublicId.Should().Be(punto);
        (await ValidarAsync(version.PublicId, punto)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoPoliticaYaValidada);
    }

    [Fact]
    public async Task Quien_la_registro_no_la_valida()
    {
        var version = (await RegistrarAsync(Endurece)).Value;

        var r = await ValidarAsync(version.PublicId, Acta(version.PublicId));

        r.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);
        (await _c.Db.RiskPolicyVersions.AsNoTracking().SingleAsync()).ValidatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Un_punto_de_otro_sujeto_responde_ItemMismatch()
    {
        var version = (await RegistrarAsync(Endurece)).Value;
        _c.G.ActuarComo(_gerente);

        var r = await ValidarAsync(version.PublicId, Acta(Guid.NewGuid()));

        r.Error.Code.Should().Be(ErroresDeGobierno.CodigoPuntoNoCorresponde);
    }

    [Fact]
    public async Task Una_version_que_no_existe_responde_NotFound()
    {
        (await ValidarAsync(Guid.NewGuid(), Guid.NewGuid())).Error.Code.Should().Be(ErroresDeRiesgo.CodigoPoliticaInexistente);
    }
}
