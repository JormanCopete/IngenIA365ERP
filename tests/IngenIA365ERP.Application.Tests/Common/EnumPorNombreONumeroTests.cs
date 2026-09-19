using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Json;
using IngenIA365ERP.Application.Payroll.Concepts;
using IngenIA365ERP.Application.Payroll.Plans;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// QA, 2026-09-18: crear un concepto de nómina respondía «Error HTTP 400» porque la pantalla
/// manda los enums por nombre («Earning») y la API sólo aceptaba números. El convertidor que
/// registra la API acepta las dos formas y sigue escribiendo números, que es lo que leen los
/// DTOs de Shared.
/// </summary>
public class EnumPorNombreONumeroTests
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new EnumPorNombreONumero() },
    };

    [Fact]
    public void Un_concepto_mandado_por_nombre_como_lo_manda_la_pantalla_se_lee()
    {
        const string json = """
            {"code":"BONO","name":"Bono","nature":"Earning","calculationKind":"PercentOfBase",
             "baseKind":"BasicSalary","percent":10,"unitKind":null,"validFrom":"2026-10-01T00:00:00"}
            """;

        var input = JsonSerializer.Deserialize<ConceptDefinitionInput>(json, Opciones)!;

        input.Nature.Should().Be(ConceptNature.Earning);
        input.CalculationKind.Should().Be(CalculationKind.PercentOfBase);
        input.BaseKind.Should().Be(CalculationBase.BasicSalary);
        input.UnitKind.Should().BeNull();
    }

    [Fact]
    public void Tambien_se_lee_por_numero_como_lo_mandan_las_pruebas_y_sin_distinguir_mayusculas()
    {
        JsonSerializer.Deserialize<CreatePayrollPlanCommand>("""{"code":"Q","name":"Quincenal","periodicity":15}""", Opciones)!
            .Periodicity.Should().Be(PayrollPeriodicity.Biweekly);
        JsonSerializer.Deserialize<CreatePayrollPlanCommand>("""{"code":"Q","name":"Quincenal","periodicity":"monthly"}""", Opciones)!
            .Periodicity.Should().Be(PayrollPeriodicity.Monthly);
    }

    [Fact]
    public void Un_nombre_que_no_existe_falla_diciendo_cuales_admite()
    {
        var acto = () => JsonSerializer.Deserialize<CreatePayrollPlanCommand>("""{"code":"Q","name":"Q","periodicity":"Lunar"}""", Opciones);

        acto.Should().Throw<JsonException>().WithMessage("*Lunar*PayrollPeriodicity*Monthly*");
    }

    [Fact]
    public void Se_escribe_como_numero_para_no_cambiar_lo_que_ya_leen_los_clientes()
    {
        JsonSerializer.Serialize(new { status = MembershipStatus.Active, canales = NotificationChannels.InApp | NotificationChannels.Email }, Opciones)
            .Should().Be($$"""{"status":{{(int)MembershipStatus.Active}},"canales":{{(int)(NotificationChannels.InApp | NotificationChannels.Email)}}}""");
    }
}
