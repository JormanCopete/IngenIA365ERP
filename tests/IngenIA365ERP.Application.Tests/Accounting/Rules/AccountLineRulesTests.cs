using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Rules;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Tests.Accounting.Rules;

/// <summary>
/// T032 — una prueba por regla de contracts/contabilizacion.md §2 (filas 4 a 10), con su código.
/// Las reglas son puras: la cuenta, la línea y el contexto entran ya resueltos.
/// </summary>
public class AccountLineRulesTests
{
    private const int Principal = 1;
    private const int Norte = 2;
    private const int Tercero = 10;
    private const int Centro = 20;

    private static CuentaParaReglas Cuenta(
        bool movimiento = true, bool activa = true, AccountingModules modulos = AccountingModules.Accounting | AccountingModules.Payroll,
        bool tercero = false, bool cruce = false, bool centro = false, bool sucursal = false, bool baseGravable = false, decimal? tarifa = null) =>
        new(1, "510505", movimiento, activa, modulos, tercero, cruce, centro, sucursal, baseGravable, tarifa);

    private static LineaParaReglas Linea(
        decimal debit = 100m, decimal credit = 0m, int? branch = Principal, bool explicita = false, int? person = null, int? centro = null,
        string? tipoCruce = null, string? numeroCruce = null, decimal? baseGravable = null) =>
        new(3, "510505", debit, credit, branch, explicita, person, centro, tipoCruce, numeroCruce, baseGravable);

    private static ContextoDeReglas Contexto(string modulo = ModuloContable.Contabilidad, AlcanceDeSucursales? alcance = null, decimal tolerancia = 5m) =>
        new(modulo, tolerancia, alcance ?? AlcanceDeSucursales.SinRestriccion,
            new HashSet<int> { Principal, Norte }, new HashSet<int> { Tercero }, new HashSet<int> { Centro },
            new HashSet<string>(["FV", "PG"], StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Codigos(CuentaParaReglas? cuenta, LineaParaReglas linea, ContextoDeReglas? ctx = null) =>
        AccountLineRules.Evaluar(cuenta, linea, ctx ?? Contexto()).Select(e => e.Code).ToList();

    [Fact]
    public void Una_linea_correcta_no_tiene_infracciones()
    {
        AccountLineRules.Evaluar(Cuenta(), Linea(), Contexto()).Should().BeEmpty();
    }

    // ---- regla 4: importe ----

    [Theory]
    [InlineData(100, 100)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(10.005, 0)]
    public void Regla4_debito_o_credito_mayor_que_cero_nunca_ambos_con_dos_decimales(double debit, double credit)
    {
        Codigos(Cuenta(), Linea((decimal)debit, (decimal)credit)).Should().Contain("Accounting.Line.AmountInvalid");
    }

    // ---- regla 5: cuenta ----

    [Fact]
    public void Regla5_cuenta_inexistente_corta_y_dice_la_referencia()
    {
        var errores = AccountLineRules.Evaluar(null, Linea(), Contexto());
        errores.Should().ContainSingle().Which.Code.Should().Be("Accounting.Line.AccountNotFound");
        errores[0].Field.Should().Be("Account");
        errores[0].LineNumber.Should().Be(3);
        errores[0].AccountCode.Should().Be("510505");
    }

    [Fact]
    public void Regla5_no_de_movimiento_inactiva_o_no_habilitada()
    {
        Codigos(Cuenta(movimiento: false), Linea()).Should().Contain("Accounting.Line.AccountNotMovement");
        Codigos(Cuenta(activa: false), Linea()).Should().Contain("Accounting.Line.AccountInactive");
        Codigos(Cuenta(modulos: AccountingModules.Accounting), Linea(), Contexto(ModuloContable.Nomina)).Should().Contain("Accounting.Line.AccountNotEnabledForModule");
        Codigos(Cuenta(modulos: AccountingModules.Accounting | AccountingModules.Payroll), Linea(), Contexto(ModuloContable.Nomina)).Should().BeEmpty();
    }

    // ---- regla 6: sucursal ----

    [Fact]
    public void Regla6_exige_sucursal_significa_elegirla_explicitamente()
    {
        Codigos(Cuenta(sucursal: true), Linea(branch: Principal, explicita: false)).Should().Contain("Accounting.Line.BranchRequired");
        Codigos(Cuenta(sucursal: true), Linea(branch: Principal, explicita: true)).Should().BeEmpty();
    }

    [Fact]
    public void Regla6_sucursal_inexistente_o_fuera_del_alcance_solo_al_digitar()
    {
        Codigos(Cuenta(), Linea(branch: 99)).Should().Contain("Accounting.Line.BranchInvalid");
        var soloNorte = AlcanceDeSucursales.Limitado([Norte], Norte);
        Codigos(Cuenta(), Linea(branch: Principal), Contexto(alcance: soloNorte)).Should().Contain("Accounting.Line.BranchOutOfScope");
        Codigos(Cuenta(), Linea(branch: Norte), Contexto(alcance: soloNorte)).Should().BeEmpty();
        // Un módulo manda la sucursal de la operación: el alcance de quien ejecuta no aplica (FR-035).
        Codigos(Cuenta(), Linea(branch: Principal), Contexto(ModuloContable.Nomina, soloNorte)).Should().BeEmpty();
    }

    // ---- regla 7: tercero ----

    [Fact]
    public void Regla7_tercero_obligatorio_y_vigente()
    {
        Codigos(Cuenta(tercero: true), Linea()).Should().Contain("Accounting.Line.ThirdPartyRequired");
        Codigos(Cuenta(tercero: true), Linea(person: Tercero)).Should().BeEmpty();
        Codigos(Cuenta(), Linea(person: 77)).Should().Contain("Accounting.Line.ThirdPartyInvalid");
    }

    // ---- regla 8: documento cruce ----

    [Fact]
    public void Regla8_documento_cruce_tipo_y_numero_de_un_tipo_vigente()
    {
        Codigos(Cuenta(cruce: true), Linea()).Should().Contain("Accounting.Line.CrossDocumentRequired");
        Codigos(Cuenta(cruce: true), Linea(tipoCruce: "FV")).Should().Contain("Accounting.Line.CrossDocumentRequired", "falta el número");
        Codigos(Cuenta(cruce: true), Linea(tipoCruce: "FV", numeroCruce: "123")).Should().BeEmpty();
        Codigos(Cuenta(), Linea(tipoCruce: "ZZ", numeroCruce: "1")).Should().Contain("Accounting.Line.CrossDocumentTypeInvalid");
    }

    // ---- regla 9: centro de costo ----

    [Fact]
    public void Regla9_centro_de_costo_presente_si_lo_exige_ausente_si_no_lo_maneja_y_vigente()
    {
        Codigos(Cuenta(centro: true), Linea()).Should().Contain("Accounting.Line.CostCenterRequired");
        Codigos(Cuenta(centro: false), Linea(centro: Centro)).Should().Contain("Accounting.Line.CostCenterNotAllowed");
        Codigos(Cuenta(centro: true), Linea(centro: 99)).Should().Contain("Accounting.Line.CostCenterInvalid");
        Codigos(Cuenta(centro: true), Linea(centro: Centro)).Should().BeEmpty();
    }

    // ---- regla 10: base gravable ----

    [Fact]
    public void Regla10_base_obligatoria_en_cuentas_de_impuesto_y_tarifa_vigente()
    {
        Codigos(Cuenta(baseGravable: true, tarifa: 0.04m), Linea()).Should().Contain("Accounting.Line.TaxBaseRequired");
        Codigos(Cuenta(baseGravable: true, tarifa: null), Linea(baseGravable: 1_000m)).Should().Contain("Accounting.Line.TaxRateMissing");
    }

    [Fact]
    public void Regla10_diferencia_dentro_de_la_tolerancia_es_aviso_y_fuera_es_error()
    {
        // base 2.500 × 4 % = 100 exacto: nada.
        AccountLineRules.Evaluar(Cuenta(baseGravable: true, tarifa: 0.04m), Linea(debit: 100m, baseGravable: 2_500m), Contexto()).Should().BeEmpty();

        // 103 contra 100: diferencia 3 ≤ tolerancia 5 → aviso que no bloquea.
        var aviso = AccountLineRules.Evaluar(Cuenta(baseGravable: true, tarifa: 0.04m), Linea(debit: 103m, baseGravable: 2_500m), Contexto());
        aviso.Should().ContainSingle();
        aviso[0].Code.Should().Be("Accounting.Line.TaxAmountDiffers");
        aviso[0].Severidad.Should().Be(Severidad.Aviso);
        aviso[0].Bloquea.Should().BeFalse();
        aviso[0].Field.Should().Be("TaxBase");

        // 110 contra 100: diferencia 10 > 5 → error.
        var error = AccountLineRules.Evaluar(Cuenta(baseGravable: true, tarifa: 0.04m), Linea(debit: 110m, baseGravable: 2_500m), Contexto());
        error.Should().ContainSingle().Which.Code.Should().Be("Accounting.Line.TaxAmountMismatch");
        error[0].Bloquea.Should().BeTrue();
    }

    [Fact]
    public void La_tarifa_vigente_es_la_mas_reciente_no_posterior_a_la_fecha()
    {
        var cuenta = new IngenIA365ERP.Domain.Entities.Accounting.ChartOfAccount { Code = "236505", RequiresTaxBase = true };
        cuenta.TaxRates.Add(new IngenIA365ERP.Domain.Entities.Accounting.AccountTaxRate { ValidFrom = new DateOnly(2025, 1, 1), Rate = 0.035m });
        cuenta.TaxRates.Add(new IngenIA365ERP.Domain.Entities.Accounting.AccountTaxRate { ValidFrom = new DateOnly(2026, 3, 1), Rate = 0.04m });
        cuenta.TaxRates.Add(new IngenIA365ERP.Domain.Entities.Accounting.AccountTaxRate { ValidFrom = new DateOnly(2026, 6, 1), Rate = 0.05m });

        CuentaParaReglas.TarifaVigenteDe(cuenta, new DateOnly(2026, 3, 15)).Should().Be(0.04m);
        CuentaParaReglas.TarifaVigenteDe(cuenta, new DateOnly(2026, 2, 15)).Should().Be(0.035m);
        CuentaParaReglas.TarifaVigenteDe(cuenta, new DateOnly(2024, 12, 31)).Should().BeNull();
    }

    [Fact]
    public void Un_error_de_linea_se_traduce_a_un_error_con_datos_para_la_pantalla()
    {
        var error = AccountLineRules.Evaluar(Cuenta(tercero: true), Linea(), Contexto()).Single().ComoError();

        error.Code.Should().Be("Accounting.Line.ThirdPartyRequired");
        error.Message.Should().StartWith("Línea 3:");
        error.Should().BeOfType<IngenIA365ERP.Application.Common.Models.ErrorConDatos>()
            .Which.Data.Should().BeEquivalentTo(new { lineNumber = 3, field = "Person", accountCode = "510505", rule = "Accounting.Line.ThirdPartyRequired", severity = "Error" });
    }
}
