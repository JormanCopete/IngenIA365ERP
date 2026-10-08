using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Domain.Tests.Lending.Governance;

/// <summary>
/// Feature 013, T0280 (data-model §3.1–§3.3, api.md §3.5.1; T63): la matriz de atribuciones se publica completa, con sus invariantes
/// —un nivel colegiado final sin techo, un solo nivel de excepción y un solo consejo, permisos de <c>Lending.Decisions.*</c> en los
/// individuales— y una huella canónica que no depende del orden en que llegan niveles y reglas.
/// </summary>
public class AttributionMatrixTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 8);
    private const int Comite = 3, Consejo = 1;

    private static readonly Dictionary<int, OrganoDeMatriz> Organos = new()
    {
        [Comite] = new OrganoDeMatriz(GoverningBodyKind.CreditCommittee, DecidesCredit: true, IsActive: true),
        [Consejo] = new OrganoDeMatriz(GoverningBodyKind.BoardOfDirectors, DecidesCredit: true, IsActive: true),
    };

    private static List<BorradorDeNivel> NivelesDelEnsayo() =>
    [
        new("ANALISTA", "Analista", 10, AttributionLevelKind.Individual, "Lending.Decisions.Analyst", null, 5_000_000m, false, false),
        new("EXCEP", "Jefe de cartera", 15, AttributionLevelKind.Individual, "Lending.Decisions.Exceptions", null, 5_000_000m, true, false),
        new("GERENCIA", "Gerencia", 20, AttributionLevelKind.Individual, "Lending.Decisions.Management", null, 20_000_000m, false, false),
        new("COMITE", "Comité de crédito", 30, AttributionLevelKind.Collegiate, null, Comite, 100_000_000m, false, false),
        new("CONSEJO", "Consejo de administración", 40, AttributionLevelKind.Collegiate, null, Consejo, null, false, true),
    ];

    private static List<BorradorDeRegla> ReglasDelEnsayo() =>
    [
        new(1, LendingDecisionSubjects.CreditApplication, "GERENCIA") { MinTermMonths = 61 },
        new(2, LendingDecisionSubjects.Condonation, "COMITE") { Concept = LoanConcept.DefaultInterest },
    ];

    private static BorradorDeMatriz Borrador(List<BorradorDeNivel>? niveles = null, List<BorradorDeRegla>? reglas = null, DateOnly? desde = null) =>
        new(desde ?? Hoy, "Reglamento de crédito 2026", "Acuerdo 12 del consejo", niveles ?? NivelesDelEnsayo(), reglas ?? ReglasDelEnsayo());

    private static string? Motivo(ProblemaDeCartera? p) => p?.Datos?["reason"] as string;

    [Fact]
    public void La_matriz_del_ensayo_se_publica_con_sus_niveles_y_reglas_y_su_huella()
    {
        AttributionMatrix.Validar(Borrador(), Hoy, Organos).Should().BeNull();

        var publicada = AttributionMatrix.Publicar(Borrador(), 2, Hoy, new DateTime(2026, 10, 8, 14, 0, 0), 7, Organos);

        publicada.Matriz.VersionNumber.Should().Be(2);
        publicada.Matriz.ContentSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        publicada.Matriz.Reason.Should().Be("Reglamento de crédito 2026");
        var niveles = publicada.Niveles(40);
        niveles.Should().HaveCount(5).And.OnlyContain(n => n.MatrixId == 40);
        var ids = niveles.Select((n, i) => (n.Code, Id: 100 + i)).ToDictionary(x => x.Code, x => x.Id);
        var reglas = publicada.Reglas(40, ids);
        reglas.Should().HaveCount(2);
        reglas[0].MinimumLevelId.Should().Be(ids["GERENCIA"]);
        reglas[1].MinimumLevelId.Should().Be(ids["COMITE"]);
    }

    [Fact]
    public void La_huella_no_depende_del_orden_de_llegada_y_cambia_con_el_contenido()
    {
        var a = AttributionMatrix.Huella(Borrador());
        var desordenada = Borrador(NivelesDelEnsayo().AsEnumerable().Reverse().ToList(), ReglasDelEnsayo().AsEnumerable().Reverse().ToList());

        AttributionMatrix.Huella(desordenada).Should().Be(a);
        var otroTecho = NivelesDelEnsayo();
        otroTecho[0] = otroTecho[0] with { MaxConsolidatedAmount = 6_000_000m };
        AttributionMatrix.Huella(Borrador(otroTecho)).Should().NotBe(a);
    }

    [Fact]
    public void La_huella_de_lo_guardado_es_la_misma_que_se_sello()
    {
        var publicada = AttributionMatrix.Publicar(Borrador(), 1, Hoy, new DateTime(2026, 10, 8), 7, Organos);
        publicada.Matriz.Id = 40;
        var niveles = publicada.Niveles(40).ToList();
        for (var i = 0; i < niveles.Count; i++) niveles[i].Id = 100 + i;
        var reglas = publicada.Reglas(40, niveles.ToDictionary(n => n.Code, n => n.Id));

        publicada.Matriz.HuellaDeLoGuardado(niveles, reglas).Should().Be(publicada.Matriz.ContentSha256);
    }

    [Fact]
    public void Sin_nivel_colegiado_final_sin_techo_es_MatrixWithoutTopLevel()
    {
        var niveles = NivelesDelEnsayo();
        niveles[4] = niveles[4] with { MaxConsolidatedAmount = 500_000_000m };
        AttributionMatrix.Validar(Borrador(niveles), Hoy)!.Codigo.Should().Be("Lending.Attribution.MatrixWithoutTopLevel");

        var individualArriba = NivelesDelEnsayo();
        individualArriba[2] = individualArriba[2] with { Rank = 50, MaxConsolidatedAmount = null };
        AttributionMatrix.Validar(Borrador(individualArriba), Hoy)!.Codigo.Should().Be("Lending.Attribution.MatrixWithoutTopLevel");

        AttributionMatrix.Validar(Borrador([]), Hoy)!.Codigo.Should().Be("Lending.Attribution.MatrixWithoutTopLevel");
    }

    [Theory]
    [InlineData("rango")]
    [InlineData("codigo")]
    [InlineData("permiso")]
    [InlineData("dos-excepciones")]
    [InlineData("dos-consejos")]
    [InlineData("sin-excepcion")]
    [InlineData("otro-sin-techo")]
    [InlineData("colegiado-con-permiso")]
    public void Las_invariantes_de_niveles_responden_MatrixInvalid_con_su_motivo(string caso)
    {
        var n = NivelesDelEnsayo();
        var (esperado, niveles) = caso switch
        {
            "rango" => ("DuplicateRank", Cambiar(n, 2, n[2] with { Rank = 10 })),
            "codigo" => ("DuplicateLevelCode", Cambiar(n, 2, n[2] with { Code = "analista" })),
            "permiso" => ("IndividualLevelPermission", Cambiar(n, 0, n[0] with { PermissionCode = "Lending.Applications.Create" })),
            "dos-excepciones" => ("ExceptionLevelCount", Cambiar(n, 0, n[0] with { IsExceptionLevel = true })),
            "dos-consejos" => ("BoardLevelCount", Cambiar(n, 3, n[3] with { IsBoardLevel = true })),
            "sin-excepcion" => ("ExceptionLevelCount", Cambiar(n, 1, n[1] with { IsExceptionLevel = false })),
            "otro-sin-techo" => ("CeilingMissing", Cambiar(n, 3, n[3] with { MaxConsolidatedAmount = null })),
            _ => ("CollegiateLevelBody", Cambiar(n, 3, n[3] with { PermissionCode = "Lending.Decisions.Management" })),
        };

        var problema = AttributionMatrix.Validar(Borrador(niveles), Hoy);

        problema!.Codigo.Should().Be("Lending.Attribution.MatrixInvalid");
        Motivo(problema).Should().Be(esperado);
    }

    [Fact]
    public void Las_reglas_nombran_un_nivel_de_la_matriz_con_ordenes_consecutivos()
    {
        AttributionMatrix.Validar(Borrador(reglas: [new(1, LendingDecisionSubjects.CreditLimit, "JUNTA")]), Hoy)!
            .Codigo.Should().Be("Lending.Attribution.LevelUnknown");
        Motivo(AttributionMatrix.Validar(Borrador(reglas: [new(1, "Prestamo", "COMITE")]), Hoy)).Should().Be("RuleSubjectUnknown");
        Motivo(AttributionMatrix.Validar(Borrador(reglas:
            [new(1, LendingDecisionSubjects.CreditApplication, "COMITE"), new(3, LendingDecisionSubjects.CreditLimit, "COMITE")]), Hoy))
            .Should().Be("RuleOrderNotConsecutive");
        Motivo(AttributionMatrix.Validar(Borrador(reglas:
            [new(1, LendingDecisionSubjects.CreditApplication, "COMITE") { MinTermMonths = 60, MaxTermMonths = 12 }]), Hoy))
            .Should().Be("RuleRangeInvalid");
    }

    [Fact]
    public void Desde_antes_de_hoy_no_se_publica()
    {
        Motivo(AttributionMatrix.Validar(Borrador(desde: Hoy.AddDays(-1)), Hoy)).Should().Be("ValidFromInPast");
        var publicar = () => AttributionMatrix.Publicar(Borrador(desde: Hoy.AddDays(-1)), 1, Hoy, new DateTime(2026, 10, 8), 7);
        publicar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Attribution.MatrixInvalid");
    }

    [Fact]
    public void Con_los_organos_un_nivel_colegiado_exige_un_organo_que_decide_y_el_consejo_es_el_de_administracion()
    {
        var sinCredito = new Dictionary<int, OrganoDeMatriz>(Organos) { [Comite] = new(GoverningBodyKind.CreditCommittee, false, true) };
        AttributionMatrix.Validar(Borrador(), Hoy, sinCredito)!.Codigo.Should().Be("Lending.GoverningBody.NotFound");
        AttributionMatrix.Validar(Borrador(), Hoy, new Dictionary<int, OrganoDeMatriz> { [Comite] = Organos[Comite] })!
            .Codigo.Should().Be("Lending.GoverningBody.NotFound");
        var consejoQueNoEs = new Dictionary<int, OrganoDeMatriz>(Organos) { [Consejo] = new(GoverningBodyKind.RiskCommittee, true, true) };
        Motivo(AttributionMatrix.Validar(Borrador(), Hoy, consejoQueNoEs)).Should().Be("BoardLevelNotBoard");
    }

    private static List<BorradorDeNivel> Cambiar(List<BorradorDeNivel> niveles, int i, BorradorDeNivel nuevo)
    {
        niveles[i] = nuevo;
        return niveles;
    }
}
