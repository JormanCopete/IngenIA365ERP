using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 014, T050 (T044, T045, T049; contracts/contabilidad.md §1 y §4.1 de la 014; research C03, C10, C20): la matriz de Cartera con las
/// dimensiones de captaciones 14 a 18 (naturaleza de la línea, clase de captación, plazo de emisión, clase de aporte y marca de la cuenta).
/// Van al final de la clave posicional; se comparan por igualdad cuando la regla las fija y <b>no pesan</b>: los pesos de la 013 quedan
/// intactos y ninguna regla de crédito cambia de resolución. El capital de captación exige el plazo de emisión sólo en CDAT y contractual;
/// la inactividad cambia de rol, no de marca; un rol de captaciones no admite la naturaleza de colocación.
/// </summary>
public class ResolutorConDimensionesDeCaptacionTests
{
    private static readonly DateOnly Noviembre = new(2026, 11, 14);
    private const AccountingModules Car = AccountingModules.Accounting | AccountingModules.Lending;

    private static async Task<MatrizVigenteDeCartera> CargarAsync(MatrizDePruebaDeCartera m) =>
        await new ResolutorDeReglasDeCarteraTestsAyuda(m).CargarAsync();

    private static LendingPostingRule Regla(
        MatrizDePruebaDeCartera m, string rol, ChartOfAccount cuenta, string? naturaleza = null, string? clase = null, string? plazo = null,
        string? claseDeAporte = null, string? marca = null, string? linea = null, string? motivo = null, int? sucursal = null,
        string operacion = LendingPostingRule.Cualquiera)
    {
        var regla = new LendingPostingRule(operacion, rol, cuenta.Id, MatrizDePruebaDeCartera.Enero1, creditLineCode: linea, reasonCode: motivo,
            branchId: sucursal, notes: "prueba", lineNature: naturaleza, depositClass: clase, issueTermBand: plazo, contributionClass: claseDeAporte,
            accountFlag: marca) { CreatedBy = "test" };
        m.D.Db.LendingPostingRules.Add(regla);
        m.D.Db.SaveChanges();
        return regla;
    }

    private static PeticionDeReglaDeCartera Capital(string clase = "Demand", string marca = "Standard", string? plazo = null, string? linea = "AHVISTA",
        Guid? sucursal = null) =>
        new(ConsignacionEnCuentaAplicadaV1.Type, R.CapitalDeCaptacion,
            new ValoresDeCartera(CreditLineCode: linea, BranchPublicId: sucursal, LineNature: "Deposit", DepositClass: clase, IssueTermBand: plazo,
                AccountFlag: marca),
            Noviembre, 1_000_000m, []);

    // ------------------------------------------------------------------------------------------------ la clave --

    [Fact]
    public void Las_cinco_dimensiones_van_al_final_de_la_clave_y_no_pesan()
    {
        LendingPostingRule.ClaveDe("*", R.CapitalDeCaptacion, null, null, null, null, null, null, null, null, null, null, null, null,
                "Deposit", "TermDeposit", "SixToTwelve", null, "Standard")
            .Should().Be("*|CapitalDeCaptacion|*|*|*|*|*|*|*|*|*|*|*|*|Deposit|TermDeposit|SixToTwelve|*|Standard");
        LendingPostingRule.ClaveDe("*", R.AportesSociales, null, null, null, null, null, null, null, null, null, null, null, null,
                contributionClass: "Ordinary")
            .Should().Be("*|AportesSociales|*|*|*|*|*|*|*|*|*|*|*|*|*|*|*|Ordinary|*");
        LendingPostingRule.ClaveDe("*", R.Capital, null, "CONSUMO", null, "A", null, null, null, null, null, null, null, null)
            .Should().EndWith(LendingPostingRule.SufijoDeCaptaciones, "una regla de crédito lleva las cinco en «*» (la migración de datos de T044)");

        var m = new MatrizDePruebaDeCartera();
        var cuenta = m.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();
        Regla(m, R.CapitalDeCaptacion, cuenta, clase: "TermDeposit", plazo: "SixToTwelve", marca: "Standard").SpecificityWeight
            .Should().Be(0, "las dimensiones de captaciones se exigen por igualdad y no pesan (contabilidad.md §1.2)");
        Regla(m, R.CapitalDeCaptacion, cuenta, clase: "Demand", marca: "Standard", linea: "AHVISTA", sucursal: m.D.Norte.Id).SpecificityWeight
            .Should().Be(LendingPostingRule.PesoLinea + LendingPostingRule.PesoSucursal, "los pesos de la 013 quedan intactos");
    }

    // ------------------------------------------------------------------------------------------- la resolución --

    [Fact]
    public async Task La_clase_y_la_marca_se_comparan_por_igualdad_y_la_linea_sigue_pesando_512()
    {
        var m = new MatrizDePruebaDeCartera();
        var activas = m.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true);
        var exentas = m.D.Cuenta("21050502", AccountNature.Credit, Car, tercero: true);
        var deLaLinea = m.D.Cuenta("21050599", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();
        var estandar = Regla(m, R.CapitalDeCaptacion, activas, clase: "Demand", marca: "Standard");
        var exenta = Regla(m, R.CapitalDeCaptacion, exentas, clase: "Demand", marca: "GmfExempt");
        var linea = Regla(m, R.CapitalDeCaptacion, deLaLinea, clase: "Demand", marca: "Standard", linea: "AHVISTA");
        var matriz = await CargarAsync(m);

        matriz.Resolver(Capital(linea: "OTRA")).Value!.Regla.Id.Should().Be(estandar.Id);
        matriz.Resolver(Capital(marca: "GmfExempt", linea: "OTRA")).Value!.Regla.Id.Should().Be(exenta.Id, "la marca es exigida: GmfExempt no cae en Standard");
        matriz.Resolver(Capital()).Value!.Regla.Id.Should().Be(linea.Id, "la línea (512) le gana a la regla general de la misma clase y marca");
        var embargada = matriz.Resolver(Capital(marca: "Garnished"));
        embargada.IsFailure.Should().BeTrue("ninguna regla fija la marca Garnished");
        embargada.Error.Code.Should().Be("Accounting.LendingRule.Missing");
    }

    [Fact]
    public async Task El_plazo_de_emision_separa_los_CDAT()
    {
        var m = new MatrizDePruebaDeCartera();
        var corto = m.D.Cuenta("21100501", AccountNature.Credit, Car, tercero: true);
        var seisADoce = m.D.Cuenta("21101001", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();
        Regla(m, R.CapitalDeCaptacion, corto, clase: "TermDeposit", plazo: "UnderSixMonths", marca: "Standard");
        var regla = Regla(m, R.CapitalDeCaptacion, seisADoce, clase: "TermDeposit", plazo: "SixToTwelve", marca: "Standard");
        var matriz = await CargarAsync(m);

        matriz.Resolver(Capital("TermDeposit", plazo: "SixToTwelve")).Value!.Regla.Id.Should().Be(regla.Id);
        matriz.Resolver(Capital("TermDeposit", plazo: "EighteenOrMore")).IsFailure.Should().BeTrue("el plazo es exigido en CDAT: no hay comodín");
        matriz.Resolver(Capital("Demand")).IsFailure.Should().BeTrue("una regla de CDAT nunca sirve para el ahorro a la vista");
    }

    [Fact]
    public async Task Una_regla_de_credito_no_cambia_de_resolucion_por_las_dimensiones_nuevas()
    {
        var m = new MatrizDePruebaDeCartera();
        var credito = m.Regla(R.Capital, m.CapitalConsumoA, modalidad: "CONSUMO", categoria: "A");
        var matriz = await CargarAsync(m);

        var r = matriz.Resolver(new PeticionDeReglaDeCartera(PagoAplicadoV1.Type, R.Capital,
            new ValoresDeCartera("LIB01", "CONSUMO", "ConLibranza", "A", "Principal", false, false, false, false, "Receipts"), Noviembre, 100m, []));

        r.Value!.Regla.Id.Should().Be(credito.Id);
        credito.DimensionKey.Should().EndWith(LendingPostingRule.SufijoDeCaptaciones);
        credito.LineNature.Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------- la forma --

    [Fact]
    public void El_capital_exige_el_plazo_de_emision_solo_en_CDAT_y_contractual()
    {
        IReadOnlySet<DimensionDeCartera> Presentes(string clase, string? plazo, string? marca = "Standard") =>
            ReglasDeLaMatrizDeCartera.Presentes(null, null, null, null, null, null, null, null, null, null, null, null, false,
                claseDeCaptacion: clase, plazoDeEmision: plazo, marca: marca);

        ReglasDeLaMatrizDeCartera.Forma(null, R.CapitalDeCaptacion, null, Presentes("TermDeposit", null), "TermDeposit")
            .Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionRequired");
        ReglasDeLaMatrizDeCartera.Forma(null, R.CapitalDeCaptacion, null, Presentes("Contractual", "UnderSixMonths"), "Contractual")
            .Should().BeEmpty();
        ReglasDeLaMatrizDeCartera.Forma(null, R.CapitalDeCaptacion, null, Presentes("Demand", "SixToTwelve"), "Demand")
            .Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionNotAllowed");
        ReglasDeLaMatrizDeCartera.Forma(null, R.CapitalDeCaptacion, null, Presentes("Demand", null, marca: null), "Demand")
            .Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionRequired");
        ReglasDeLaMatrizDeCartera.Forma(null, R.CapitalDeCaptacionInactiva, null, Presentes("Demand", null, marca: null), "Demand")
            .Should().BeEmpty("la marca la exige sólo el capital activo; la inactividad es otro rol (C20)");
        ReglasDeLaMatrizDeCartera.Forma(null, R.Capital, null,
                ReglasDeLaMatrizDeCartera.Presentes(null, null, "CONSUMO", null, "A", null, null, null, null, null, null, null, false, naturaleza: "Deposit"))
            .Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionNotAllowed");
    }

    [Fact]
    public async Task Un_rol_de_captaciones_no_admite_la_naturaleza_de_colocacion_ni_valores_fuera_de_su_enum()
    {
        var m = new MatrizDePruebaDeCartera();
        var reglas = m.Reglas();
        var catalogos = await reglas.CargarAsync(default);
        var puente = m.D.Cuenta("24650501", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();

        var colocacion = await reglas.ValidarAsync(new ReglaPropuestaDeCartera("*", R.DevolucionPorPagar, puente.Id, MatrizDePruebaDeCartera.Enero1,
            ReasonCode: "MembershipRefund", LineNature: "Placement"), catalogos, [], esVersion: false, default);
        colocacion.Errores.Select(e => e.Code).Should().Equal("Accounting.LendingRule.LineNatureMismatch");

        var desconocida = await reglas.ValidarAsync(new ReglaPropuestaDeCartera("*", R.InteresPorPagar, puente.Id, MatrizDePruebaDeCartera.Enero1,
            DepositClass: "Lunar"), catalogos, [], esVersion: false, default);
        desconocida.Errores.Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionCodeUnknown");

        var buena = await reglas.ValidarAsync(new ReglaPropuestaDeCartera("*", R.DevolucionPorPagar, puente.Id, MatrizDePruebaDeCartera.Enero1,
            ReasonCode: "MembershipRefund", LineNature: "Contribution"), catalogos, [], esVersion: false, default);
        buena.EsValida.Should().BeTrue(string.Join("; ", buena.Errores.Select(e => e.Message)));
    }

    // ---------------------------------------------------------------------------------- de concepto a rol (§2) --

    [Fact]
    public void La_inactividad_cambia_el_rol_y_los_aportes_van_a_su_clase()
    {
        R.RolDelConceptoDeCuenta("Principal").Should().Be(R.CapitalDeCaptacion);
        R.RolDelConceptoDeCuenta("Principal", inactiva: true).Should().Be(R.CapitalDeCaptacionInactiva, "C20: la inactividad cambia de rol, no de marca");
        R.RolDelConceptoDeCuenta("Bonus").Should().Be(R.InteresPorPagar);
        R.RolDelConceptoDeCuenta("ContributionMinimum").Should().Be(R.AportesSociales);
        R.ClaseDeAporteDelConcepto("ContributionRevaluation").Should().Be("Revalued");
        R.RolDelConceptoDeCuenta("CurrentInterest").Should().BeNull("los conceptos de crédito no son del libro de cuentas");
        R.MarcasDeCuenta.Should().Equal("Standard", "GmfExempt", "Garnished");
        R.DeCaptaciones.Should().HaveCount(18);
        R.DeCaptaciones.Where(r => r.AdmiteHecho).Select(r => r.Codigo).Should().BeEquivalentTo(
            [R.GastoPorIntereses, R.GastoPorBonificacion, R.IngresoPorCargos, R.IngresoPorPenalidad], "el hecho sólo pesa en los roles de resultado");
        R.Buscar(R.CapitalDeCaptacion)!.Cruce.Should().Be(CruceDeRol.Cuenta);
        R.Buscar(R.CapitalDeCaptacion)!.Tercero.Should().Be(TerceroNaturalDeCartera.Titular);
    }

    // --------------------------------------------------------------------------------- retroactividad (§3.5) --

    [Fact]
    public async Task La_retroactividad_compara_tambien_las_dimensiones_de_captaciones()
    {
        var m = new MatrizDePruebaDeCartera();
        var cuenta = m.D.Cuenta("21050501", AccountNature.Credit, Car, tercero: true);
        m.D.Db.SaveChanges();
        m.Contabilizado(ConsignacionEnCuentaAplicadaV1.Type, new DateOnly(2026, 11, 10),
            """{"account":{"accountNumber":"AH-1"},"dimensions":{"creditLineCode":"AHVISTA","lineNature":"Deposit","depositClass":"Demand","accountFlag":"Standard"},"amounts":[{"concept":"Principal","amount":100}]}""");
        var resolutor = new ResolutorDeReglasDeCartera(m.D.Db);

        var aLaVista = new LendingPostingRule("*", R.CapitalDeCaptacion, cuenta.Id, MatrizDePruebaDeCartera.Enero1, depositClass: "Demand", accountFlag: "Standard");
        var deCdat = new LendingPostingRule("*", R.CapitalDeCaptacion, cuenta.Id, MatrizDePruebaDeCartera.Enero1, depositClass: "TermDeposit",
            issueTermBand: "SixToTwelve", accountFlag: "Standard");

        (await resolutor.UltimoQueCoincideAsync(aLaVista, MatrizDePruebaDeCartera.Enero1, null, default)).Should().NotBeNull();
        (await resolutor.UltimoQueCoincideAsync(deCdat, MatrizDePruebaDeCartera.Enero1, null, default))
            .Should().BeNull("el hecho contabilizado era de ahorro a la vista: una regla de CDAT no lo habría cambiado");
    }

    /// <summary>Carga la matriz de un escenario de 2026 a 2027.</summary>
    private sealed class ResolutorDeReglasDeCarteraTestsAyuda(MatrizDePruebaDeCartera m)
    {
        public Task<MatrizVigenteDeCartera> CargarAsync() =>
            new ResolutorDeReglasDeCartera(m.D.Db).CargarAsync(MatrizDePruebaDeCartera.Enero1, new DateOnly(2027, 12, 31), default);
    }
}
