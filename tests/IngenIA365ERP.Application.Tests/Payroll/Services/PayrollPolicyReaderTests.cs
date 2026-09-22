using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// Feature 010 (T019, R4): las políticas por empresa se leen de <c>PAY_CompanyPolicies</c> por
/// vigencia a una fecha, caen a <c>COR_SystemSettings</c> sólo si la clave no existe, y un valor
/// que no se reconoce no se tapa con el defecto: se nombra la clave.
/// </summary>
public class PayrollPolicyReaderTests
{
    private static readonly DateOnly Enero = new(2026, 1, 15);
    private static readonly DateOnly Agosto = new(2026, 8, 15);

    [Fact]
    public async Task Sin_ninguna_fila_rigen_los_defectos_del_catalogo()
    {
        var d = new NominaTestData();

        var p = await d.Policies.ReadAsync(Enero, CancellationToken.None);

        p.Exonerada114_1.Should().BeFalse();
        p.AllowSameUserApproval.Should().BeFalse();
        p.SemanaLaboral.Should().Be(SemanaLaboral.LunesASabado);
        p.VacacionesPagoAnticipado.Should().BeTrue("D-01");
        p.CotizaArlEnVacaciones.Should().BeFalse();
        p.RetefteTopesAnualesModo.Should().Be(ModoDeTopesAnuales.Mensualizado);
        p.P2SecuenciaDepuracion.Should().Be(SecuenciaDepuracionP2.DepurarLuegoDividir);
        p.DianPlazoComputo.Should().Be(ComputoDePlazo.Calendario);
        p.DeduccionAlRetiroModo.Should().Be(DeduccionAlRetiro.SaldoTotal);
        p.DianMedioPagoMapa.Should().Contain("Transfer", "47").And.Contain("Check", "20").And.Contain("Cash", "10");
        p.CodigoDianDeMedioDePago("Transfer").Should().Be("47");
        p.ArranqueNominaFecha.Should().BeNull("no tiene defecto: lo pone la semilla con el primer período");
        p.ForSettlement().PayrollStartDate.Should().BeNull();
    }

    [Fact]
    public async Task La_vigencia_que_cubre_la_fecha_manda_y_un_cambio_posterior_no_reescribe_el_pasado()
    {
        var d = new NominaTestData();
        d.Politica(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        d.Politica(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesASabado, new DateOnly(2026, 7, 1));
        d.Politica(CompanyPolicyKeys.Exonerada114_1, CompanyPolicyKeys.Verdadero, new DateOnly(2026, 7, 1));

        var enero = await d.Policies.ReadAsync(Enero, CancellationToken.None);
        var agosto = await d.Policies.ReadAsync(Agosto, CancellationToken.None);

        enero.SemanaLaboral.Should().Be(SemanaLaboral.LunesAViernes);
        agosto.SemanaLaboral.Should().Be(SemanaLaboral.LunesASabado);
        enero.Exonerada114_1.Should().BeFalse("la clave existe pero no tiene vigencia en enero: rige el defecto, no la de julio");
        agosto.Exonerada114_1.Should().BeTrue();
        agosto.ForCalculation().ApplyEmployerExemption.Should().BeTrue("la nómina ordinaria lee la exoneración por aquí");
    }

    [Fact]
    public async Task Cae_a_COR_SystemSettings_solo_si_la_clave_no_existe_en_politicas()
    {
        var d = new NominaTestData();
        d.Db.SystemSettings.Add(new SystemSetting { SettingKey = PayrollPolicyReader.ApplyEmployerExemptionKey, SettingValue = "true", ValueType = "Bool", ModulePrefix = "PAY", CreatedBy = "test" });
        d.PermitirAprobarMismoUsuario();
        // AllowSameUserApproval sí está en políticas (y dice false): el setting heredado no cuenta.
        d.Politica(CompanyPolicyKeys.AllowSameUserApproval, CompanyPolicyKeys.Falso, new DateOnly(2020, 1, 1));

        var p = await d.Policies.ReadAsync(Enero, CancellationToken.None);

        p.Exonerada114_1.Should().BeTrue("la clave no existe en PAY_CompanyPolicies: se lee Payroll.ApplyEmployerExemption");
        p.AllowSameUserApproval.Should().BeFalse("la clave existe en políticas: COR_SystemSettings ya no se mira");
    }

    [Fact]
    public async Task Redondeo_y_umbral_siguen_en_COR_SystemSettings()
    {
        var d = new NominaTestData();
        d.Db.SystemSettings.Add(new SystemSetting { SettingKey = PayrollPolicyReader.VariationThresholdKey, SettingValue = "25", ValueType = "Decimal", ModulePrefix = "PAY", CreatedBy = "test" });
        d.Db.SystemSettings.Add(new SystemSetting { SettingKey = PayrollPolicyReader.RoundingKey, SettingValue = "Centavo", ValueType = "String", ModulePrefix = "PAY", CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        var p = await d.Policies.ReadAsync(CancellationToken.None);

        p.VariationThresholdPercent.Should().Be(25m);
        p.Rounding.Should().Be(Domain.Enums.Payroll.PayrollRounding.Centavo);
    }

    [Fact]
    public async Task Un_valor_que_no_se_reconoce_es_error_y_nombra_la_clave()
    {
        var d = new NominaTestData();
        d.Politica(CompanyPolicyKeys.DeduccionAlRetiroModo, "Todo", new DateOnly(2020, 1, 1));
        d.Politica(CompanyPolicyKeys.ArranqueNominaFecha, "01/12/2026", new DateOnly(2020, 1, 1));

        var r = await d.Policies.LeerAsync(Enero, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(PayrollPolicyReader.ValueInvalidCode);
        r.Error.Message.Should().Contain(CompanyPolicyKeys.DeduccionAlRetiroModo).And.Contain("Todo").And.Contain("SaldoTotal")
            .And.Contain(CompanyPolicyKeys.ArranqueNominaFecha).And.Contain("yyyy-MM-dd");

        var acto = () => d.Policies.ReadAsync(Enero, CancellationToken.None);
        (await acto.Should().ThrowAsync<PoliticaDeNominaInvalidaException>()).Which.Message.Should().Contain(CompanyPolicyKeys.DeduccionAlRetiroModo);
    }

    [Fact]
    public async Task El_arranque_y_el_mapa_dian_se_tipan()
    {
        var d = new NominaTestData();
        d.Politica(CompanyPolicyKeys.ArranqueNominaFecha, "2026-12-01", new DateOnly(2020, 1, 1));
        d.Politica(CompanyPolicyKeys.DianMedioPagoMapa, """{ "Transfer": "42", "Check": "20", "Cash": "10" }""", new DateOnly(2020, 1, 1));
        d.Politica(CompanyPolicyKeys.RetefteTopesAnualesModo, CompanyPolicyKeys.RetefteTopesAnualesModoValores.Acumulado, new DateOnly(2020, 1, 1));

        var p = await d.Policies.ReadAsync(Enero, CancellationToken.None);

        p.ArranqueNominaFecha.Should().Be(new DateOnly(2026, 12, 1));
        p.ForSettlement().PayrollStartDate.Should().Be(new DateTime(2026, 12, 1));
        p.CodigoDianDeMedioDePago("transfer").Should().Be("42", "el mapa no distingue mayúsculas");
        p.ForSettlement().RetefteTopesAnualesModo.Should().Be(ModoDeTopesAnuales.Acumulado);
    }
}
