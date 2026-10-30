using FluentAssertions;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Treasury;

/// <summary>
/// Feature 015, US8, T0248 (R-22; FR-143, FR-144; contabilidad.md §4, §5): en la matriz de Tesorería el <c>PuenteDeGiroDelOrigen</c> se resuelve por
/// <c>OrderOrigin</c> y <c>OrderKind</c> (clase de orden 64, origen 32), para que la contadora lo lleve a la misma cuenta que el puente del origen; el rol
/// <c>Banco</c> sale del vínculo de la cuenta propia. Prueba escrita, sin correr.
/// </summary>
public class MatrizDeTesoreriaTests
{
    private static LineaDeHecho Puente(string origen, string clase) =>
        new(RolesDeTesoreria.PuenteDeGiroDelOrigen, true, 500m, null, null,
            new Dictionary<string, string> { ["OrderOrigin"] = origen, ["OrderKind"] = clase }, null);

    [Fact]
    public void El_puente_de_giro_se_resuelve_por_origen_y_clase_de_orden()
    {
        var desembolsos = Cuenta("249505");
        var devoluciones = Cuenta("212505");
        var devolucionDeRecaudo = Cuenta("259595");
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Tesoreria,
        [
            Regla<TreasuryPostingRule>(RolesDeTesoreria.PuenteDeGiroDelOrigen, desembolsos, dimensiones: r => r.OrderOrigin = "LendingDisbursement"),
            Regla<TreasuryPostingRule>(RolesDeTesoreria.PuenteDeGiroDelOrigen, devoluciones, dimensiones: r => { r.OrderOrigin = "DepositsPayout"; r.OrderKind = "DepositWithdrawal"; }),
            Regla<TreasuryPostingRule>(RolesDeTesoreria.PuenteDeGiroDelOrigen, devolucionDeRecaudo, dimensiones: r => r.OrderKind = "PayerCreditBalanceRefund"),
        ], [desembolsos, devoluciones, devolucionDeRecaudo]);

        matriz.Resolver(PagoDeOrdenGiradoV1.Type, Puente("LendingDisbursement", "LoanDisbursement"), Hoy).Value!.Cuenta.Code.Should().Be("249505");
        matriz.Resolver(PagoDeOrdenGiradoV1.Type, Puente("DepositsPayout", "DepositWithdrawal"), Hoy).Value!.Cuenta.Code.Should().Be("212505");
        matriz.Resolver(PagoDeOrdenGiradoV1.Type, Puente("Receipts", "PayerCreditBalanceRefund"), Hoy).Value!.Cuenta.Code.Should().Be("259595");
        matriz.Resolver(PagoDeOrdenGiradoV1.Type, Puente("Manual", "Otro"), Hoy).Error.Code.Should().Be("Accounting.TreasuryRule.Missing");
    }

    [Fact]
    public void El_banco_sale_del_vinculo_y_no_admite_regla()
    {
        var bancaria = Cuenta("111005");
        var propia = Guid.NewGuid();
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Tesoreria, [], [bancaria],
            [new OwnBankAccountLink { OwnBankAccountPublicId = propia, ChartOfAccountId = bancaria.Id, ValidFrom = Hoy.AddYears(-1) }]);

        matriz.Resolver(PagoDeOrdenGiradoV1.Type, new LineaDeHecho(RolesDeTesoreria.Banco, false, 500m, null, null, new Dictionary<string, string>(), propia), Hoy)
            .Value!.Cuenta.Code.Should().Be("111005");
        MatricesDeRecaudoYTesoreria.Tesoreria.Rol(RolesDeTesoreria.Banco)!.PorVinculo.Should().BeTrue();
        MatricesDeRecaudoYTesoreria.Tesoreria.Rol(RolesDeTesoreria.GmfGasto)!.DeResultado.Should().BeTrue("el hecho pesa en el gasto del GMF");
    }
}
