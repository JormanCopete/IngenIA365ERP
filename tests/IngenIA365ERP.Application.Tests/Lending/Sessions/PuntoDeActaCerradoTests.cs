using FluentAssertions;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Application.Tests.Lending.Sessions;

/// <summary>
/// Feature 013, T0249 (US17; T64; api.md §1.9): <see cref="PuntoDeActaCerrado"/>, la validación única del <c>sessionItemPublicId</c> de los
/// actos «con acta». Un punto de una sesión cerrada, que decide, sobre el mismo sujeto y con un resultado aprobatorio, pasa; otro sujeto, un
/// punto de una sesión abierta, uno que sólo recomienda o un resultado <c>Deny</c> → <c>Lending.Session.ItemMismatch</c> con
/// <c>{ expectedSubject, itemSubject, itemOutcome }</c>.
/// </summary>
public sealed class PuntoDeActaCerradoTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();
    private readonly GoverningBody _consejo;

    public PuntoDeActaCerradoTests() =>
        _consejo = _g.Organo("CONSEJO", "Consejo de administración", GoverningBodyKind.BoardOfDirectors, vinculadosN: 4, vinculadosD: 5);

    public void Dispose() => _g.Dispose();

    private SessionItem Punto(SessionStatus estado, string sujeto, Guid sujetoPublicId, CreditDecisionOutcome? resultado,
        SessionItemKind clase = SessionItemKind.Decision)
    {
        var sesion = new Session
        {
            GoverningBodyId = _consejo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = GobiernoDePrueba.Hoy, Status = estado,
            Number = estado == SessionStatus.Closed ? $"CONSEJO-{Guid.NewGuid():N}"[..20] : null,
        };
        _g.Db.LendingSessions.Add(sesion);
        _g.Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = clase, Rule = VotingRule.Majority, SubjectType = sujeto, SubjectPublicId = sujetoPublicId,
            Title = "Diferencias de la carga", Outcome = resultado,
        };
        _g.Db.SessionItems.Add(punto);
        _g.Db.SaveChanges();
        return punto;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<PuntoCerrado>> Validar(Guid punto, string sujeto, Guid? sujetoPublicId = null) =>
        new PuntoDeActaCerrado(_g.Db).ValidarAsync(punto, sujeto, sujetoPublicId);

    [Theory]
    [InlineData(CreditDecisionOutcome.Approve)]
    [InlineData(CreditDecisionOutcome.ApproveWithConditions)]
    public async Task Un_punto_cerrado_del_mismo_sujeto_con_resultado_aprobatorio_pasa(CreditDecisionOutcome resultado)
    {
        var carga = Guid.NewGuid();
        var punto = Punto(SessionStatus.Closed, LendingDecisionSubjects.ActivationDifferences, carga, resultado);

        var r = await Validar(punto.PublicId, LendingDecisionSubjects.ActivationDifferences, carga);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.ItemId.Should().Be(punto.Id);
        r.Value.Outcome.Should().Be(resultado);
    }

    [Fact]
    public async Task Otro_sujeto_es_ItemMismatch_con_lo_esperado_y_lo_del_punto()
    {
        var punto = Punto(SessionStatus.Closed, LendingDecisionSubjects.CreditApplication, Guid.NewGuid(), CreditDecisionOutcome.Approve);

        var r = await Validar(punto.PublicId, LendingDecisionSubjects.WriteOffBatch);

        r.Error.Code.Should().Be("Lending.Session.ItemMismatch");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("expectedSubject").GetString().Should().Be("WriteOffBatch");
        datos.GetProperty("itemSubject").GetString().Should().Be("CreditApplication");
        datos.GetProperty("itemOutcome").GetString().Should().Be("Approve");
    }

    [Fact]
    public async Task El_mismo_tipo_de_sujeto_con_otro_PublicId_es_ItemMismatch()
    {
        var punto = Punto(SessionStatus.Closed, LendingDecisionSubjects.ActivationDifferences, Guid.NewGuid(), CreditDecisionOutcome.Approve);

        (await Validar(punto.PublicId, LendingDecisionSubjects.ActivationDifferences, Guid.NewGuid())).Error.Code.Should().Be("Lending.Session.ItemMismatch");
    }

    [Fact]
    public async Task Un_punto_de_una_sesion_abierta_es_ItemMismatch()
    {
        var punto = Punto(SessionStatus.Open, LendingDecisionSubjects.CreditApplication, Guid.NewGuid(), CreditDecisionOutcome.Approve);

        (await Validar(punto.PublicId, LendingDecisionSubjects.CreditApplication)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
    }

    [Fact]
    public async Task Un_resultado_Deny_o_un_punto_que_solo_recomienda_es_ItemMismatch()
    {
        var negado = Punto(SessionStatus.Closed, LendingDecisionSubjects.CreditApplication, Guid.NewGuid(), CreditDecisionOutcome.Deny);
        var recomienda = Punto(SessionStatus.Closed, LendingDecisionSubjects.CreditApplication, Guid.NewGuid(), CreditDecisionOutcome.Approve,
            SessionItemKind.Recommendation);

        var r = await Validar(negado.PublicId, LendingDecisionSubjects.CreditApplication);

        r.Error.Code.Should().Be("Lending.Session.ItemMismatch");
        GobiernoDePrueba.Datos(r.Error).GetProperty("itemOutcome").GetString().Should().Be("Deny");
        (await Validar(recomienda.PublicId, LendingDecisionSubjects.CreditApplication)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
    }

    [Fact]
    public async Task Un_punto_inexistente_es_ItemMismatch_sin_datos_del_punto()
    {
        var r = await Validar(Guid.NewGuid(), LendingDecisionSubjects.ActivationDifferences);

        r.Error.Code.Should().Be("Lending.Session.ItemMismatch");
        GobiernoDePrueba.Datos(r.Error).GetProperty("itemSubject").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public void Dos_asuntos_que_declaran_el_mismo_sujeto_no_arman_el_registro()
    {
        var uno = new SessionCommandsTests.AsuntoFalso(LendingDecisionSubjects.CreditApplication);
        var otro = new SessionCommandsTests.AsuntoFalso(LendingDecisionSubjects.CreditApplication);

        var armar = () => new AsuntosDeActa([uno, otro]);

        armar.Should().Throw<InvalidOperationException>().WithMessage("*CreditApplication*");
        new AsuntosDeActa([uno]).Buscar(LendingDecisionSubjects.CreditApplication).Should().BeSameAs(uno);
        new AsuntosDeActa([uno]).Buscar(LendingDecisionSubjects.Novelty).Should().BeNull();
    }
}
