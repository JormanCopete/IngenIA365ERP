using FluentAssertions;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.LegalParameters;

/// <summary>Feature 010 (T006, R4): qué le falta a cada proceso a una fecha, con la misma comprobación que hace el cálculo.</summary>
public class GetMissingLegalParametersQueryTests
{
    [Fact]
    public async Task Con_la_semilla_2026_no_falta_nada_a_ningun_proceso_en_2026()
    {
        var d = new NominaTestData();
        var h = new GetMissingLegalParametersQueryHandler(d.Db, d.Clock);

        foreach (var proceso in Enum.GetValues<LegalParameterProcess>())
        {
            var r = await h.Handle(new GetMissingLegalParametersQuery(proceso, new DateTime(2026, 12, 31)), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            r.Value.Process.Should().Be(proceso.ToString());
            r.Value.Missing.Should().BeEmpty($"{proceso} tiene todo lo suyo sembrado para 2026");
        }
    }

    [Fact]
    public async Task Un_codigo_de_prestaciones_sin_vigencia_falta_a_las_liquidaciones_pero_no_a_la_nomina_ordinaria()
    {
        var d = new NominaTestData();
        var prima = await d.Db.PayrollLegalParameters.SingleAsync(p => p.Code == SettlementParameterCodes.ServiceBonusDaysPerYear);
        prima.ValidTo = new DateTime(2026, 5, 31);
        var uvt2027 = await d.Db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt);
        await d.Db.SaveChangesAsync();
        var h = new GetMissingLegalParametersQueryHandler(d.Db, d.Clock);

        var liquidaciones = await h.Handle(new GetMissingLegalParametersQuery(LegalParameterProcess.Settlements, new DateTime(2026, 6, 30)), CancellationToken.None);
        var ordinaria = await h.Handle(new GetMissingLegalParametersQuery(LegalParameterProcess.Ordinary, new DateTime(2026, 6, 30)), CancellationToken.None);

        var faltante = liquidaciones.Value.Missing.Should().ContainSingle().Subject;
        faltante.Code.Should().Be(SettlementParameterCodes.ServiceBonusDaysPerYear);
        faltante.Description.Should().Contain(prima.Name).And.Contain("hasta 31/05/2026");
        faltante.Source.Should().Be(prima.Source);
        ordinaria.Value.Missing.Should().BeEmpty("la lista de la ordinaria no lleva los códigos de prestaciones (R4)");
        uvt2027.Should().NotBeNull();
    }

    [Fact]
    public async Task Un_codigo_que_nunca_se_sembro_se_dice_como_tal()
    {
        var d = new NominaTestData();
        var divisor = await d.Db.PayrollLegalParameters.SingleAsync(p => p.Code == "RETEFTE_P2_DIVISOR");
        d.Db.PayrollLegalParameters.Remove(divisor);
        await d.Db.SaveChangesAsync();

        var r = await new GetMissingLegalParametersQueryHandler(d.Db, d.Clock)
            .Handle(new GetMissingLegalParametersQuery(LegalParameterProcess.WithholdingRates), CancellationToken.None);

        r.Value.AsOf.Should().Be(NominaTestData.Ahora.Date, "sin fecha, hoy");
        var faltante = r.Value.Missing.Should().ContainSingle().Subject;
        faltante.Code.Should().Be("RETEFTE_P2_DIVISOR");
        faltante.Description.Should().Contain("Sin registrar");
        faltante.Source.Should().BeNull();
    }
}
