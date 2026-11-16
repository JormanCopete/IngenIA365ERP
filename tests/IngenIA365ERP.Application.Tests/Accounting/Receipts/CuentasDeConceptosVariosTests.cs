using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Receipts;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using MediatR;
using NSubstitute;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// 2026-11-16 (pedido del dueño): la cuenta a la que va cada concepto vario según la matriz de Recaudo, que la configuración de los conceptos muestra
/// (<c>IContabilidadParaRecaudo.CuentasDeConceptosVariosAsync</c>): la regla del rol <c>ConceptoVario</c> con el código del concepto gana a la general; sin la de su
/// código, la general; una regla de una sola oficina no casa sin la oficina; sin regla, el concepto no viene.
/// </summary>
public class CuentasDeConceptosVariosTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private ContabilidadParaRecaudo Contabilidad => new(Substitute.For<ISender>(), _db, new ResolutorDeLaMatriz(_db));

    [Fact]
    public async Task La_regla_del_concepto_gana_a_la_general_y_la_de_una_oficina_no_casa_sin_ella()
    {
        var soat = Cuenta("24459501");
        var general = Cuenta("42950501");
        var deOficina = Cuenta("42950502");
        _db.ChartOfAccounts.AddRange(soat, general, deOficina);
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.ConceptoVario, soat, dimensiones: r => r.MiscConceptCode = "SOAT"));
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.ConceptoVario, general));
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.ConceptoVario, deOficina, dimensiones: r =>
        {
            r.MiscConceptCode = "CERT";
            r.BranchCode = "NORTE";
        }));
        await _db.SaveChangesAsync();

        var cuentas = await Contabilidad.CuentasDeConceptosVariosAsync(["SOAT", "CERT"], Hoy, default);

        cuentas["SOAT"].AccountCode.Should().Be("24459501");
        cuentas["SOAT"].AccountName.Should().Be("Cuenta 24459501");
        cuentas["CERT"].AccountCode.Should().Be("42950501", "la de la oficina NORTE no casa sin la oficina: se informa la general");
    }

    [Fact]
    public async Task Sin_regla_ni_resolutor_el_concepto_no_viene()
    {
        (await Contabilidad.CuentasDeConceptosVariosAsync(["SOAT"], Hoy, default)).Should().BeEmpty();
        (await new ContabilidadParaRecaudo(Substitute.For<ISender>()).CuentasDeConceptosVariosAsync(["SOAT"], Hoy, default)).Should().BeEmpty();
    }
}
