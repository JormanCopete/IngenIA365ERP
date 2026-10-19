using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Plantilla;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using P = IngenIA365ERP.Application.Accounting.Lending.Reglas.PlantillaDeMatrizDeCartera;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T144, T148; contabilidad.md §4.2 y §4.3 de la 014; plantillas-y-archivos.md §6): la versión 2 de la plantilla de la matriz trae las
/// filas sugeridas sin cuenta de captaciones y aportes con su cuenta CUIF sugerida (informativa: no se aplica) y lee las cinco dimensiones nuevas; la
/// completitud dice, por naturaleza, qué combinaciones exigidas no tienen regla. Ninguna regla se siembra.
/// </summary>
public class PlantillaYCompletitudDeCaptacionesTests
{
    private const AccountingModules Car = AccountingModules.Accounting | AccountingModules.Lending;

    private static int Columna(string nombre) => P.Columnas.ToList().IndexOf(nombre);

    private static CatalogoDeDimensionesDeCarteraDto Catalogo(params LineaDeCarteraDto[] lineas) => new(
        [new("LIB01", "Libranza", "CONSUMO", "ConLibranza", true), .. lineas], [new("CONSUMO", "Consumo", ["ConLibranza"])], R.Conceptos, ["Receipts"],
        OperacionesDeCartera.Todas.Select(o => o.Codigo).ToList());

    private static readonly LineaDeCarteraDto AlaVista = new("AHVISTA", "Ahorro a la vista", string.Empty, null, true, "Deposit", "Demand");
    private static readonly LineaDeCarteraDto Cdat = new("CDAT", "CDAT", string.Empty, null, true, "Deposit", "TermDeposit");
    private static readonly LineaDeCarteraDto Aportes = new("APORTES", "Aportes sociales", string.Empty, null, true, "Contribution");

    // ---------------------------------------------------------------------------------------------- la plantilla --

    [Fact]
    public async Task La_plantilla_sugiere_las_filas_de_captaciones_sin_cuenta_y_con_la_cuenta_CUIF_como_texto_informativo()
    {
        var m = new MatrizDePruebaDeCartera();
        m.Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Catalogo(AlaVista, Cdat, Aportes)));
        m.D.Clock.HoyLocal.Returns(new DateOnly(2026, 11, 20));

        var filas = (await new GetLendingRulesTemplateQueryHandler(m.D.Db, m.Dimensiones, m.D.Clock).Handle(new GetLendingRulesTemplateQuery(), default)).Value.De("Datos");

        filas.Should().OnlyContain(f => f.Count == P.Columnas.Count);
        var deCaptaciones = filas.Where(f => R.EsDeCaptaciones((string?)f[Columna(P.Rol)])).ToList();
        deCaptaciones.Should().NotBeEmpty().And.OnlyContain(f => f[Columna(P.Cuenta)] == null && (string?)f[Columna(P.Notas)] == P.PendienteDeValidar,
            "ninguna regla se crea sola: la cuenta la elige la contadora (P1, C70)");
        deCaptaciones.Where(f => (string?)f[Columna(P.Rol)] == R.CapitalDeCaptacion && (string?)f[Columna(P.ClaseDeCaptacion)] == "Demand")
            .Select(f => ((string?)f[Columna(P.MarcaDeCuenta)], (string?)f[Columna(P.CuentaSugerida)]))
            .Should().BeEquivalentTo(new[] { ("Standard", "210505"), ("GmfExempt", "210505"), ("Garnished", "210505") });
        deCaptaciones.Where(f => (string?)f[Columna(P.Rol)] == R.CapitalDeCaptacion && (string?)f[Columna(P.ClaseDeCaptacion)] == "TermDeposit")
            .Select(f => ((string?)f[Columna(P.PlazoDeEmision)], (string?)f[Columna(P.CuentaSugerida)]))
            .Should().BeEquivalentTo(new[] { ("UnderSixMonths", "211005"), ("SixToTwelve", "211010"), ("TwelveToEighteen", "211015"), ("EighteenOrMore", "211020") });
        deCaptaciones.Should().Contain(f => (string?)f[Columna(P.Rol)] == R.AportesSociales && (string?)f[Columna(P.ClaseDeAporte)] == "Ordinary"
                                            && (string?)f[Columna(P.CuentaSugerida)] == "310505");
        deCaptaciones.Should().Contain(f => (string?)f[Columna(P.Rol)] == R.GmfPorPagar && (string?)f[Columna(P.ClaseDeCaptacion)] == "TermDeposit"
                                            && (string?)f[Columna(P.CuentaSugerida)] == "243010", "el GMF sobre CDAT (investigación §3.9)");
        filas.Where(f => (string?)f[Columna(P.Rol)] == R.Capital).Select(f => (string?)f[Columna(P.Modalidad)]).Distinct()
            .Should().Equal(["CONSUMO"], "las líneas de captación no tienen modalidad: no generan filas de crédito vacías");
        (await m.D.Db.LendingPostingRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Las_sugeridas_de_captaciones_pasan_la_forma_de_la_regla_y_no_se_repiten()
    {
        var sugeridas = SugerenciasDeCaptaciones.De(["Demand", "TermDeposit", "Contractual", "Permanent"], conAportes: true);

        foreach (var s in sugeridas)
        {
            var presentes = ReglasDeLaMatrizDeCartera.Presentes(null, null, null, null, null, null, null, null, null, null, s.ClaseDeRecaudador, s.Motivo, false,
                s.Naturaleza, s.Clase, s.Plazo, s.ClaseDeAporte, s.Marca);
            ReglasDeLaMatrizDeCartera.Forma(null, s.Rol, s.Motivo, presentes, s.Clase).Should().BeEmpty($"{s.Rol} {s.Clase} {s.Plazo} {s.Marca} {s.Motivo}");
        }
        sugeridas.Select(s => s.Clave).Should().OnlyHaveUniqueItems();
        SugerenciasDeCaptaciones.De([], conAportes: false).Should().BeEmpty("sin líneas de captación ni de aportes no hay qué sugerir");
    }

    // ---------------------------------------------------------------------------------------------- la importación --

    private sealed class Importacion
    {
        public MatrizDePruebaDeCartera M { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public ICurrentUserPermissions Permisos { get; } = Substitute.For<ICurrentUserPermissions>();

        public Importacion() => Permisos.EsMaestroGlobal.Returns(true);

        public void Archivo(params Dictionary<string, string?>[] filas)
        {
            var columnas = P.Columnas.ToArray();
            var tabla = new TablaLeida(columnas, filas.Select((f, i) => new FilaLeida(i + 2, columnas.Select(c => f.GetValueOrDefault(c)).ToArray())).ToList(), "xlsx");
            Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(tabla)));
        }

        public async Task<Result<ImportResultDto>> AplicarAsync()
        {
            var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
            var ejecutor = new EjecutorDeImportacion(M.D.Db, Lector, Permisos, servicios);
            var emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), M.D.User, M.D.Clock, NullLogger<AccountingAuditEmitter>.Instance,
                CooperativaDePrueba.Actual);
            var r = await new ImportLendingPostingRulesCommandHandler(M.D.Db, ejecutor, M.Reglas(), emisor)
                .Handle(new ImportLendingPostingRulesCommand(ModoDeImportacion.Apply, new ArchivoDeImportacion("matriz-cartera.xlsx", [1, 2, 3])), default);
            M.D.Db.ChangeTracker.Clear();
            return r;
        }
    }

    [Fact]
    public async Task La_importacion_lee_las_cinco_dimensiones_e_ignora_la_cuenta_sugerida()
    {
        var i = new Importacion();
        var ahorro = i.M.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true, cruce: true);
        var cdat = i.M.D.Cuenta("21101001", AccountNature.Credit, Car, tercero: true, cruce: true);
        i.M.D.Db.SaveChanges();
        i.Archivo(
            new() { [P.Rol] = R.CapitalDeCaptacion, [P.ClaseDeCaptacion] = "Demand", [P.MarcaDeCuenta] = "Standard", [P.Cuenta] = ahorro.Code,
                [P.CuentaSugerida] = "210505", [P.VigenteDesde] = "2026-01-01" },
            new() { [P.Rol] = R.CapitalDeCaptacion, [P.ClaseDeCaptacion] = "TermDeposit", [P.PlazoDeEmision] = "SixToTwelve", [P.MarcaDeCuenta] = "Standard",
                [P.Cuenta] = cdat.Code, [P.VigenteDesde] = "2026-01-01" },
            new() { [P.Rol] = R.AportesSociales, [P.ClaseDeAporte] = "Ordinary", [P.CuentaSugerida] = "310505", [P.Notas] = P.PendienteDeValidar,
                [P.VigenteDesde] = "2026-01-01" });

        var r = await i.AplicarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var reglas = await i.M.D.Db.LendingPostingRules.OrderBy(x => x.Id).ToListAsync();
        reglas.Should().HaveCount(2, "la fila sugerida sin cuenta se ignora");
        reglas[0].DepositClass.Should().Be("Demand");
        reglas[0].AccountFlag.Should().Be("Standard");
        reglas[1].IssueTermBand.Should().Be("SixToTwelve");
        reglas[0].DimensionKey.Should().EndWith("|*|Demand|*|*|Standard");
    }

    [Fact]
    public async Task Un_rol_de_captaciones_con_naturaleza_de_colocacion_falla_en_la_columna_naturaleza()
    {
        var i = new Importacion();
        var ahorro = i.M.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true, cruce: true);
        i.M.D.Db.SaveChanges();
        i.Archivo(new Dictionary<string, string?> { [P.Rol] = R.DevolucionPorPagar, [P.Naturaleza] = "Placement", [P.Motivo] = "DepositWithdrawal", [P.Cuenta] = ahorro.Code,
            [P.VigenteDesde] = "2026-01-01" });

        var r = await i.AplicarAsync();

        r.IsFailure.Should().BeTrue();
        System.Text.Json.JsonSerializer.Serialize(r.Error.Should().BeOfType<ErrorConDatos>().Which.Data).Should().Contain(P.Naturaleza).And.Contain("LineNatureMismatch");
    }

    // ---------------------------------------------------------------------------------------------- la completitud --

    [Fact]
    public void La_completitud_dice_por_naturaleza_las_combinaciones_exigidas_sin_regla_y_su_cuenta_sugerida()
    {
        var sinReglas = GetLendingRulesCompletenessQueryHandler.FaltantesDeCaptaciones([AlaVista, Aportes], []);

        sinReglas.Should().Contain(f => f.Role == R.CapitalDeCaptacion && f.Dimensions.DepositClass == "Demand" && f.Dimensions.AccountFlag == "Standard"
                                        && f.Nature == "Deposit" && f.CreditLines.SequenceEqual(new[] { "AHVISTA" }) && f.SuggestedAccount == "210505");
        sinReglas.Should().Contain(f => f.Role == R.AportesSociales && f.Dimensions.ContributionClass == "Ordinary" && f.Nature == "Contribution");
        sinReglas.Should().NotContain(f => f.Role == R.FondoDeRevalorizacion, "lo de la asamblea (K10) se sugiere pero no se exige todavía");

        var m = new MatrizDePruebaDeCartera();
        var cuenta = m.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();
        var regla = new Domain.Entities.Accounting.Lending.LendingPostingRule("*", R.CapitalDeCaptacion, cuenta.Id, MatrizDePruebaDeCartera.Enero1,
            creditLineCode: "AHVISTA", depositClass: "Demand", accountFlag: "Standard") { CreatedBy = "test" };
        var conRegla = GetLendingRulesCompletenessQueryHandler.FaltantesDeCaptaciones([AlaVista], [regla]);
        conRegla.Should().NotContain(f => f.Role == R.CapitalDeCaptacion && f.Dimensions.AccountFlag == "Standard",
            "una regla por línea también cubre la combinación");
        conRegla.Should().Contain(f => f.Role == R.CapitalDeCaptacion && f.Dimensions.AccountFlag == "GmfExempt");
    }
}
