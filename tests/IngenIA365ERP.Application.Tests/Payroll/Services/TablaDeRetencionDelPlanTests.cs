using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.CreateWithholdingParameter;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// Pedido del dueño (2026-09-19): los tramos de /nomina/parametros-retencion son de un plan de nómina
/// y la liquidación los usa en lugar de RETEFTE_TABLA_UVT para ese plan; un plan sin tramos sigue con
/// la tabla legal. Hasta entonces la pantalla escribía sobre una tabla del legado que nadie leía.
/// </summary>
public class TablaDeRetencionDelPlanTests
{
    private static WithholdingParameter Tramo(int planId, int desde, int hasta, decimal tarifa, int fijas) => new()
    {
        PayrollPlanId = planId, PayrollCompanyId = 1, UvtRangeStart = desde, UvtRangeEnd = hasta, Rate = tarifa, AdditionalUvt = fijas, CreatedBy = "test",
    };

    [Fact]
    public void Sin_tramos_del_plan_los_parametros_quedan_iguales()
    {
        var d = new NominaTestData();
        var legales = d.Db.PayrollLegalParameters.Include(p => p.Ranges).ToList();

        var r = TablaDeRetencionDelPlan.Aplicar(legales, [], d.Plan, d.Marzo.EndDate);

        r.Should().BeSameAs(legales);
    }

    [Fact]
    public async Task Con_tramos_del_plan_la_tabla_legal_se_reemplaza_solo_para_esa_corrida_y_el_motor_la_usa()
    {
        var d = new NominaTestData();
        // Un plan «generoso»: exento hasta 200 UVT y 10 % marginal de ahí en adelante, sin UVT fijas.
        d.Db.WithholdingParameters.AddRange(Tramo(d.Plan.Id, 0, 200, 0m, 0), Tramo(d.Plan.Id, 200, 0, 10m, 0));
        await d.Db.SaveChangesAsync();

        var batch = await d.Loader.LoadAsync(d.Marzo, CancellationToken.None);

        var tabla = new ParameterSet(batch.Parameters, d.Marzo.EndDate).Table(LegalParameterCodes.WithholdingTableUvt);
        tabla.Ranges.Should().HaveCount(2);
        tabla.Ranges.OrderBy(t => t.Order).Select(t => (t.FromValue, t.ToValue, t.Rate)).Should().Equal([(0m, 200m, 0m), (200m, null, 10m)]);
        tabla.RangeIsMarginal.Should().BeTrue();
        tabla.RangeUnitParameterCode.Should().Be(LegalParameterCodes.Uvt, "los tramos del plan van en UVT como los legales");
        tabla.Source.Should().Contain(d.Plan.Code);
        batch.Parameters.Count(p => p.Code == LegalParameterCodes.WithholdingTableUvt).Should().Be(1, "las vigencias legales salen del lote");
        d.Db.PayrollLegalParameters.Count(p => p.Code == LegalParameterCodes.WithholdingTableUvt).Should().BeGreaterThan(0, "la tabla legal en la base no se toca");
    }

    [Fact]
    public void Los_tramos_de_otro_plan_no_cuentan_y_los_retirados_tampoco()
    {
        var d = new NominaTestData();
        var legales = d.Db.PayrollLegalParameters.Include(p => p.Ranges).ToList();
        var retirado = Tramo(d.Plan.Id, 0, 95, 0m, 0); retirado.IsDeleted = true;

        var r = TablaDeRetencionDelPlan.Aplicar(legales, [retirado, Tramo(d.Plan.Id + 1, 0, 95, 5m, 0)], d.Plan, d.Marzo.EndDate);

        r.Should().BeSameAs(legales, "sólo cuentan los tramos vivos del plan de la corrida");
    }

    [Fact]
    public void El_validador_exige_plan_rango_coherente_y_tarifa_en_porcentaje()
    {
        var v = new CreateWithholdingParameterCommandValidator();
        v.TestValidate(new CreateWithholdingParameterCommand { PayrollPlanPublicId = Guid.Empty, UvtRangeStart = 0, UvtRangeEnd = 95 }).ShouldHaveValidationErrorFor(x => x.PayrollPlanPublicId);
        v.TestValidate(new CreateWithholdingParameterCommand { PayrollPlanPublicId = Guid.NewGuid(), UvtRangeStart = 150, UvtRangeEnd = 95 }).ShouldHaveValidationErrorFor(x => x.UvtRangeEnd);
        v.TestValidate(new CreateWithholdingParameterCommand { PayrollPlanPublicId = Guid.NewGuid(), UvtRangeStart = 2300, UvtRangeEnd = 0, Rate = 39m, AdditionalUvt = 770 }).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new CreateWithholdingParameterCommand { PayrollPlanPublicId = Guid.NewGuid(), UvtRangeStart = 0, UvtRangeEnd = 95, Rate = 0.19m * 1000 }).ShouldHaveValidationErrorFor(x => x.Rate);
    }

    [Fact]
    public async Task Crear_rechaza_el_tramo_que_se_cruza_pero_admite_el_contiguo()
    {
        var d = new NominaTestData();
        var handler = new CreateWithholdingParameterCommandHandler(d.Db, d.Clock, d.User);
        var primero = await handler.Handle(new CreateWithholdingParameterCommand { PayrollPlanPublicId = d.Plan.PublicId, UvtRangeStart = 0, UvtRangeEnd = 95 }, CancellationToken.None);
        primero.IsSuccess.Should().BeTrue(primero.Error.Message);

        var contiguo = await handler.Handle(new CreateWithholdingParameterCommand { PayrollPlanPublicId = d.Plan.PublicId, UvtRangeStart = 95, UvtRangeEnd = 150, Rate = 19m }, CancellationToken.None);
        contiguo.IsSuccess.Should().BeTrue("95–150 empieza donde termina 0–95: los tramos son semiabiertos");

        var cruzado = await handler.Handle(new CreateWithholdingParameterCommand { PayrollPlanPublicId = d.Plan.PublicId, UvtRangeStart = 100, UvtRangeEnd = 0, Rate = 28m }, CancellationToken.None);
        cruzado.IsSuccess.Should().BeFalse();
        cruzado.Error.Code.Should().Be("Payroll.WithholdingRange.Overlap");
        cruzado.Error.Message.Should().Contain("95 a 150");

        var otroPlan = await handler.Handle(new CreateWithholdingParameterCommand { PayrollPlanPublicId = Guid.NewGuid(), UvtRangeStart = 0, UvtRangeEnd = 95 }, CancellationToken.None);
        otroPlan.Error.Code.Should().Be("Payroll.PlanNotFound");
    }
}
