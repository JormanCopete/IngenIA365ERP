using FluentAssertions;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0248 (R-22; FR-143; contabilidad.md §5, §6): la matriz de Recaudo resuelve hecho × rol × dimensiones por código; gana la más específica (caja
/// 256, punto 128, medio 64, concepto 64, módulo y naturaleza 32, oficina 4, hecho 2 sólo en roles de resultado); el centro de costo se asigna sin pesar; un empate es
/// un error de configuración; la vigencia no es retroactiva; <c>BancoRecaudo</c> sale del vínculo de la cuenta propia y no admite regla. Prueba escrita, sin correr.
/// </summary>
public class MatrizDeRecaudoTests
{
    private static LineaDeHecho Linea(string rol, params (string Clave, string Valor)[] dimensiones) =>
        new(rol, true, 100m, null, null, dimensiones.ToDictionary(d => d.Clave, d => d.Valor), null);

    [Fact]
    public void Los_pesos_suman_las_dimensiones_fijadas_y_el_hecho_solo_en_los_roles_de_resultado()
    {
        var cuenta = Cuenta("110505");
        Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuenta, dimensiones: r => { r.CashBoxCode = "c1"; r.MeansCode = "EF"; r.BranchCode = "01"; })
            .SpecificityWeight.Should().Be(256 + 64 + 4);
        Regla<ReceiptsPostingRule>(RolesDeRecaudo.PuenteDelModulo, cuenta, dimensiones: r => { r.TargetModule = "CAR"; r.TargetNature = "Loan"; })
            .SpecificityWeight.Should().Be(32 + 32);
        Regla<ReceiptsPostingRule>(RolesDeRecaudo.GastoFaltante, cuenta, hecho: DiferenciaDeArqueoDeRecaudoAprobadaV1.Type, deResultado: true).SpecificityWeight.Should().Be(2);
        Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuenta, dimensiones: r => r.CostCenterId = 7).SpecificityWeight.Should().Be(0, "el centro de costo no pesa");
        Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuenta, dimensiones: r => r.CashBoxCode = "c1").CashBoxCode.Should().Be("C1", "los códigos se normalizan");
    }

    [Fact]
    public void Gana_la_mas_especifica_y_un_empate_es_error_de_configuracion()
    {
        var general = Cuenta("110505");
        var deLaCaja = Cuenta("110510");
        var reglas = new ReglaContableDeModulo[]
        {
            Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, general),
            Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, deLaCaja, dimensiones: r => r.CashBoxCode = "C1"),
        };
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Recaudo, reglas, [general, deLaCaja]);

        matriz.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.Caja, ("CashBoxCode", "C1")), Hoy).Value!.Cuenta.Code.Should().Be("110510");
        matriz.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.Caja, ("CashBoxCode", "C2")), Hoy).Value!.Cuenta.Code.Should().Be("110505");

        var empate = Matriz(MatricesDeRecaudoYTesoreria.Recaudo,
            [Regla<ReceiptsPostingRule>(RolesDeRecaudo.ConceptoVario, general, dimensiones: r => r.MiscConceptCode = "REI"),
             Regla<ReceiptsPostingRule>(RolesDeRecaudo.ConceptoVario, deLaCaja, dimensiones: r => r.MeansCode = "EF")], [general, deLaCaja]);
        empate.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.ConceptoVario, ("MiscConceptCode", "REI"), ("MeansCode", "EF")), Hoy)
            .Error.Code.Should().Be("Accounting.ReceiptsRule.Ambiguous");
    }

    [Fact]
    public void Sin_regla_vigente_falta_y_la_vigencia_no_se_extiende_hacia_atras()
    {
        var cuenta = Cuenta("110505");
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Recaudo, [Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuenta, desde: Hoy)], [cuenta]);

        matriz.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.Caja), Hoy.AddDays(-1)).Error.Code.Should().Be("Accounting.ReceiptsRule.Missing");
        matriz.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.Caja), Hoy).IsSuccess.Should().BeTrue();
        matriz.Resolver(ReciboEmitidoV1.Type, Linea(RolesDeRecaudo.PuenteDelModulo), Hoy).Error.Code.Should().Be("Accounting.ReceiptsRule.Missing");
    }

    [Fact]
    public void BancoRecaudo_sale_del_vinculo_de_la_cuenta_propia()
    {
        var bancaria = Cuenta("111005");
        var propia = Guid.NewGuid();
        var vinculo = new OwnBankAccountLink { OwnBankAccountPublicId = propia, ChartOfAccountId = bancaria.Id, ValidFrom = Hoy.AddYears(-1) };
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Recaudo, [], [bancaria], [vinculo]);

        matriz.Resolver(ReciboDeBancoEmitidoV1.Type, new LineaDeHecho(RolesDeRecaudo.BancoRecaudo, true, 10m, null, null, new Dictionary<string, string>(), propia), Hoy)
            .Value!.Cuenta.Code.Should().Be("111005");
        matriz.Resolver(ReciboDeBancoEmitidoV1.Type, new LineaDeHecho(RolesDeRecaudo.BancoRecaudo, true, 10m, null, null, new Dictionary<string, string>(), Guid.NewGuid()), Hoy)
            .Error.Code.Should().Be("Accounting.OwnBankAccount.NotLinked");
        MatricesDeRecaudoYTesoreria.Recaudo.Rol(RolesDeRecaudo.BancoRecaudo)!.PorVinculo.Should().BeTrue("no admite regla");
    }
}
