using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0620 (T0649; contracts/contabilidad.md §14.1; FR-130, FR-141): el saldo contable de las combinaciones de Cartera por conjunto
/// de cuentas. Cada combinación se resuelve con la matriz al corte (hecho <c>*</c>); los conjuntos son componentes conexos combinación ↔ cuenta
/// por familia de rol (dos categorías en una auxiliar; una combinación con reglas por sucursal); el saldo sale sólo de los movimientos
/// contabilizados hasta el corte; lo que no resuelve va en <c>UnresolvedCombinations</c>; sin contabilidad iniciada, <c>Accounting.NotInitialized</c>.
/// </summary>
public class GetLendingAccountBalancesQueryTests
{
    private static readonly DateOnly Corte = new(2026, 11, 30);
    private static readonly Guid Principal = Guid.Parse("b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f");
    private static readonly Guid Norte = Guid.Parse("c8e2f3a4-1b5c-4d6e-9f70-2b3c4d5e6f71");
    private readonly EscenarioContableDeCartera E = new();

    private static CombinacionDeSaldo Capital(string categoria, Guid? sucursal = null, string subsegmento = "ConLibranza") =>
        new(R.Capital, "LIB01", "CONSUMO", subsegmento, categoria, R.Principal, false, false, false, false, null, null, sucursal ?? Principal);

    private async Task<SaldosMapeadosDeCarteraDto> SaldosAsync(params CombinacionDeSaldo[] combinaciones)
    {
        var r = await new GetLendingAccountBalancesQueryHandler(E.D.Db, new ResolutorDeReglasDeCartera(E.D.Db), E.D.Clock)
            .Handle(new GetLendingAccountBalancesQuery(Corte, combinaciones), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private async Task ContabilizarAsync(string caso)
    {
        var unidad = EscenarioContableDeCartera.UnidadDe(caso);
        E.Emitir(unidad);
        var r = await E.PorUnidad().Handle(new PostLendingFactsCommand(unidad.Select(m => m.MessageId).ToList(), null), default);
        r.Value.Should().BeOfType<IngenIA365ERP.Application.Common.Integration.ResultadoDeConsumo.Processed>();
        E.D.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task El_saldo_de_cada_conjunto_sale_de_los_movimientos_hasta_el_corte()
    {
        await ContabilizarAsync("01-desembolso-por-transferencia");
        await ContabilizarAsync("03-causacion-del-dia");

        var s = await SaldosAsync(
            Capital("A"),
            new CombinacionDeSaldo(R.InteresPorCobrar, "LIB01", "CONSUMO", "ConLibranza", "A", R.CurrentInterest, false, false, false, false, null, null, Principal),
            Capital("C"),
            Capital("B"));

        s.UnresolvedCombinations.Should().Equal(3);
        var capitalA = s.Sets.Single(c => c.Combinations.Contains(0));
        capitalA.RoleFamily.Should().Be(nameof(FamiliaDeRol.Saldo));
        capitalA.Accounts.Should().ContainSingle().Which.AccountCode.Should().Be("14410501");
        capitalA.Balance.Should().Be(10_000_000m);
        capitalA.Accounts[0].BalanceByBranch.Should().ContainSingle().Which.BranchPublicId.Should().Be(Principal);
        s.Sets.Single(c => c.Combinations.Contains(1)).Balance.Should().Be(6452.48m);
        s.Sets.Single(c => c.Combinations.Contains(2)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task Dos_categorias_en_una_auxiliar_son_un_conjunto()
    {
        E.AgregarReglas(JsonNode.Parse("""[{ "rol": "Capital", "cuenta": "14410501", "modalidad": "CONSUMO", "subsegmento": "ConLibranza", "categoria": "B" }]""")!.AsArray());

        var s = await SaldosAsync(Capital("A"), Capital("B"));

        s.Sets.Should().ContainSingle().Which.Combinations.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Una_combinacion_con_reglas_por_sucursal_junta_sus_cuentas()
    {
        E.AgregarReglas(JsonNode.Parse("""[{ "rol": "Capital", "cuenta": "14420501", "modalidad": "CONSUMO", "subsegmento": "ConLibranza", "categoria": "A", "sucursal": "norte" }]""")!.AsArray());

        var s = await SaldosAsync(Capital("A"), Capital("A", Norte));

        var conjunto = s.Sets.Should().ContainSingle().Subject;
        conjunto.Combinations.Should().Equal(0, 1);
        conjunto.Accounts.Select(a => a.AccountCode).Should().Equal("14410501", "14420501");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_responde_NotInitialized()
    {
        var setup = await E.D.Db.AccountingSetups.SingleAsync();
        setup.IsDeleted = true;
        await E.D.Db.SaveChangesAsync();

        var r = await new GetLendingAccountBalancesQueryHandler(E.D.Db, new ResolutorDeReglasDeCartera(E.D.Db), E.D.Clock)
            .Handle(new GetLendingAccountBalancesQuery(Corte, [Capital("A")]), default);

        r.Error.Code.Should().Be("Accounting.NotInitialized");
    }

    [Theory]
    [InlineData(R.Ingreso, false)]
    [InlineData(R.GastoDeterioro, false)]
    [InlineData("NoExiste", false)]
    [InlineData(R.Capital, true)]
    [InlineData(R.RecaudoPorAplicar, true)]
    [InlineData(R.ContingenteAcreedora, true)]
    public void El_validador_solo_admite_roles_de_saldo(string rol, bool valido)
    {
        var combinacion = new CombinacionDeSaldo(rol, null, "CONSUMO", null, "A", null, null, null, null, null, null, null, Principal);
        new GetLendingAccountBalancesQueryValidator().Validate(new GetLendingAccountBalancesQuery(Corte, [combinacion])).IsValid.Should().Be(valido, "C29");
    }
}
