using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T456 (T516; contracts/contabilidad.md §7.2; FR-081, FR-090): los saldos de las cuentas mapeadas. Los conjuntos
/// son componentes conexos grupo ↔ cuenta de los roles <c>Inventario</c> y <c>Transito</c> (dos grupos que comparten cuenta van
/// juntos; un grupo con regla por bodega junta sus dos cuentas); el saldo por cuenta y sucursal sale de
/// <c>MovimientosContables</c> sin restricción de sucursal, hasta el corte inclusive, con la apertura <c>AP</c> dentro.
/// </summary>
public class InventoryAccountBalancesQueryTests
{
    private readonly EscenarioContable E = new();

    public InventoryAccountBalancesQueryTests()
    {
        // GRANOS comparte la cuenta de abarrotes: su conjunto es el mismo.
        E.Regla("Compra", "Inventario", "14350501", grupo: "GRANOS");
        var patrimonio = E.D.Cuenta("31050501", AccountNature.Credit);

        // La apertura (saldo inicial) y un movimiento del 10 de marzo.
        Contabilizar(new PostingRequest("AP", new DateOnly(2025, 12, 31), "Apertura", ContabilidadTestData.Manual(),
        [
            PostingLine.Debito("14350501", 10000m) with { BranchId = E.D.Principal.Id },
            PostingLine.Debito("14350503", 2000m) with { BranchId = E.D.Norte.Id },
            PostingLine.Debito("14350502", 3000m) with { BranchId = E.D.Principal.Id },
            PostingLine.Credito(patrimonio.Code, 15000m) with { BranchId = E.D.Principal.Id },
        ], DocumentKind.Opening));
        Contabilizar(new PostingRequest("CG", new DateOnly(2026, 3, 10), "Sobrante", ContabilidadTestData.Manual(),
            [PostingLine.Debito("14350501", 500m), PostingLine.Credito("42500501", 500m)]));
    }

    private void Contabilizar(PostingRequest request)
    {
        var r = E.D.Poster.PrepareAsync(request, default).GetAwaiter().GetResult();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        E.D.Db.SaveChanges();
    }

    private async Task<IReadOnlyList<ConjuntoDeCuentasDto>> ConsultarAsync(DateOnly corte)
    {
        var r = await new InventoryAccountBalancesQueryHandler(E.D.Db, E.D.Clock).Handle(new InventoryAccountBalancesQuery(corte), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    [Fact]
    public async Task Arma_los_conjuntos_como_componentes_conexos()
    {
        var conjuntos = await ConsultarAsync(new DateOnly(2026, 3, 31));

        var abarrotes = conjuntos.Single(c => c.AccountingGroupCodes.Contains("ABARROTES"));
        abarrotes.AccountingGroupCodes.Should().Equal("ABARROTES", "GRANOS");
        abarrotes.Accounts.Select(a => (a.AccountCode, a.Role)).Should().Equal(("14350501", "Inventario"), ("14350503", "Inventario"), ("14350590", "Transito"));
        abarrotes.Pairs.Should().Contain(new ParGrupoBodegaDto("ABARROTES", "B02")).And.Contain(new ParGrupoBodegaDto("ABARROTES", "*")).And.Contain(new ParGrupoBodegaDto("GRANOS", "*"));
        conjuntos.Single(c => c.AccountingGroupCodes.Contains("ASEO")).Accounts.Select(a => a.AccountCode).Should().Equal("14350502");
        conjuntos.Select(c => c.AccountingGroupCodes[0]).Should().Contain("KITS");
    }

    [Fact]
    public async Task El_saldo_incluye_la_apertura_y_se_lee_por_sucursal_hasta_el_corte()
    {
        var alNueve = await ConsultarAsync(new DateOnly(2026, 3, 9));
        var alTreintaYUno = await ConsultarAsync(new DateOnly(2026, 3, 31));

        alNueve.Single(c => c.AccountingGroupCodes.Contains("ABARROTES")).Balance.Should().Be(12000m, "la apertura es el saldo inicial");
        var abarrotes = alTreintaYUno.Single(c => c.AccountingGroupCodes.Contains("ABARROTES"));
        abarrotes.Balance.Should().Be(12500m);
        abarrotes.Accounts.Single(a => a.AccountCode == "14350503").BalanceByBranch.Should().Equal(new SaldoPorSucursalDto(EscenarioContable.Norte, 2000m));
        alTreintaYUno.Single(c => c.AccountingGroupCodes.Contains("ASEO")).Balance.Should().Be(3000m);
    }

    [Fact]
    public async Task Sin_reglas_de_inventario_no_hay_conjuntos()
    {
        E.D.Db.InventoryPostingRules.RemoveRange(E.D.Db.InventoryPostingRules);
        E.D.Db.SaveChanges();

        (await ConsultarAsync(new DateOnly(2026, 3, 31))).Should().BeEmpty();
    }
}
