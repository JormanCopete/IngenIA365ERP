using FluentAssertions;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Accounting.Treasury;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Treasury;

/// <summary>
/// 2026-11-15 (pedido del dueño): el pago de una cuenta por pagar contable debita la cuenta donde está la deuda. Con varias cuentas marcadas para el rol
/// <c>CuentaPorPagarContable</c> la matriz no las distingue (todas son «AccountingPayable»); si se sabe en cuál está pendiente el documento cruce del tercero, el
/// pago la cancela allí; si no se sabe, la resuelve la matriz como antes.
/// </summary>
public class PagoDeCuentaPorPagarContableTests
{
    private static readonly Guid Sucursal = Guid.NewGuid();
    private static readonly Guid Tercero = Guid.NewGuid();
    private static readonly Guid Propia = Guid.NewGuid();

    private static (CatalogosDeModulo Sin, Func<ChartOfAccount, CatalogosDeModulo> Con, ChartOfAccount Costos, ChartOfAccount Salarios) Escenario()
    {
        var costos = Cuenta("233595", tercero: true, cruce: true);
        var salarios = Cuenta("250505", tercero: true, cruce: true);
        var banco = Cuenta("111005");
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Tesoreria,
            [Regla<TreasuryPostingRule>(ContabilidadParaTesoreria.RolCuentaPorPagarContable, costos)],
            [costos, salarios, banco],
            [new OwnBankAccountLink { OwnBankAccountPublicId = Propia, ChartOfAccountId = banco.Id, ValidFrom = Hoy.AddYears(-1) }]);
        var mapeos = new Dictionary<string, (string, string?)> { [PagoDeCuentaPorPagarGiradoV1.Type] = ("TP", "OT") };
        var personas = new Dictionary<Guid, int> { [Tercero] = 9 };
        var sucursales = new Dictionary<Guid, int> { [Sucursal] = 1 };
        return (new CatalogosDeModulo(matriz, mapeos, personas, sucursales),
            deuda => new CatalogosDeModulo(matriz, mapeos, personas, sucursales,
                new Dictionary<(Guid, string), ChartOfAccount> { [(Tercero, "CXP-77")] = deuda }),
            costos, salarios);
    }

    private static HechoContableDeModulo Pago() => HechoContableDeModulo.De(Mensaje("TES", new
    {
        paymentOrderPublicId = Guid.NewGuid(), orderNumber = "OP-1", crossDocument = "CXP-77", thirdPartyPersonPublicId = Tercero, amount = 500_000m,
        lines = new object[]
        {
            new { role = ContabilidadParaTesoreria.RolCuentaPorPagarContable, side = "Debit", amount = 500_000m, thirdPartyPersonPublicId = Tercero, crossDocument = "CXP-77",
                dimensions = new { orderOrigin = "AccountingPayable", orderKind = "AccountingPayable", ownBankAccountPublicId = Propia } },
            new { role = RolesDeTesoreria.Banco, side = "Credit", amount = 500_000m, crossDocument = "OP-1",
                dimensions = new { ownBankAccountPublicId = Propia } },
        },
    }, PagoDeCuentaPorPagarGiradoV1.Type, sucursal: Sucursal, persona: Tercero, numero: "OP-1"));

    [Fact]
    public void Debita_la_cuenta_donde_esta_pendiente_la_deuda_y_no_la_de_la_matriz()
    {
        var (_, con, _, salarios) = Escenario();

        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Tesoreria, [Pago()], con(salarios));

        r.EsValida.Should().BeTrue(string.Join("; ", r.Fallos.Select(f => f.Error.Message)));
        var debito = r.Request!.Lines.Single(l => l.Debit > 0m);
        debito.AccountCode.Should().Be("250505", "la deuda está en salarios por pagar");
        debito.PersonId.Should().Be(9);
        debito.CrossDocumentNumber.Should().Be("CXP-77");
        r.Request.Lines.Single(l => l.Credit > 0m).AccountCode.Should().Be("111005");
    }

    [Fact]
    public void Si_no_se_sabe_donde_esta_la_deuda_la_resuelve_la_matriz()
    {
        var (sin, _, _, _) = Escenario();

        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Tesoreria, [Pago()], sin);

        r.EsValida.Should().BeTrue(string.Join("; ", r.Fallos.Select(f => f.Error.Message)));
        r.Request!.Lines.Single(l => l.Debit > 0m).AccountCode.Should().Be("233595");
    }
}
