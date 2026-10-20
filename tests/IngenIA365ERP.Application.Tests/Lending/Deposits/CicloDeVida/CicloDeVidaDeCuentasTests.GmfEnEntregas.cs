using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Unclaimed;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.CicloDeVida;

/// <summary>
/// Respuesta 97 del dueño (2026-10-21, research E253): el GMF se cobra en la entrega por fallecimiento y en el pago de los saldos no reclamados como en
/// cualquier retiro, retenido de lo que se entrega (neto = valor − GMF), y cada línea puede apagarlo (<c>GmfOnDeceasedAndUnclaimed</c>).
/// </summary>
public sealed partial class CicloDeVidaDeCuentasTests
{
    private const decimal GmfDeMedioMillon = 2_000m;

    private async Task<(MemberAccountDto Cuenta, List<AccountPayoutOrder> Ordenes)> FallecidoConDosBeneficiariosAsync(bool gmfEnEntregas)
    {
        _conGmf = true;
        var dto = await AhorroAsync(1_000_000m, [new(_a.C.Carla.PublicId, 50m), new(_a.C.Dario.PublicId, 50m)], gmfEnEntregas);
        var caso = await RegistradoAsync();
        caso = await EntregaRegistradaAsync(caso, DeliveryRoute.Beneficiaries, null);
        _a.G.ActuarComo(_a.C.Jefe);
        var aprobada = await Aprobar().Handle(new ApproveDeceasedDeliveryCommand(caso.PublicId, "Aprobada por el jefe") { OperationKey = Guid.NewGuid() }, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        _a.Db.ChangeTracker.Clear();
        var ordenes = await _a.Db.AccountPayoutOrders.Where(o => o.Purpose == PayoutPurpose.DeceasedDelivery).OrderBy(o => o.Id).ToListAsync();
        return (dto, ordenes);
    }

    [Fact]
    public async Task E253_fallecido_con_dos_beneficiarios_al_50_cobra_el_GMF_de_cada_porcion_y_gira_el_neto()
    {
        var (dto, ordenes) = await FallecidoConDosBeneficiariosAsync(gmfEnEntregas: true);
        var id = (await CuentaAsync(dto)).Id;

        ordenes.Should().HaveCount(2);
        ordenes.Select(o => o.Amount).Should().AllBeEquivalentTo(500_000m - GmfDeMedioMillon, "cada beneficiario recibe su porción menos el GMF de esa porción");
        var saldos = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        saldos.GetValueOrDefault(MemberAccountConcept.Principal).Should().Be(0m, "lo girado más el GMF vacían el capital");
        saldos.GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(2 * GmfDeMedioMillon);

        var usos = await _a.Db.GmfMonthlyUsages.AsNoTracking().Where(u => u.MemberAccountId == id).ToListAsync();
        usos.Should().HaveCount(2);
        usos.Should().OnlyContain(u => u.TaxableEventCode == HechosGeneradoresDeGmf.EntregaPorFallecimiento && u.GmfAmount == GmfDeMedioMillon);
        var hechos = await _a.HechosAsync();
        hechos.Count(h => h == GmfRetenidoV1.Type).Should().Be(2, "un GmfRetenido por porción, en la misma unidad de la entrega");
        hechos.Count(h => h == DevolucionOrdenadaV1.Type).Should().Be(2);

        // Tesorería gira las dos y la cuenta se cancela como siempre.
        foreach (var o in ordenes)
        {
            var viva = await _a.Db.AccountPayoutOrders.SingleAsync(x => x.Id == o.Id);
            (await Ordenes().AplicarResultadoAsync(viva, DisbursementOrderStatus.Disbursed, $"G-{o.Id}", _a.G.HoyLocal, null, default)).IsSuccess.Should().BeTrue();
            await _a.Db.SaveChangesAsync();
            _a.Db.ChangeTracker.Clear();
        }
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closed);
    }

    [Fact]
    public async Task E253_con_la_linea_en_no_cobra_la_entrega_por_fallecimiento_gira_el_valor_entero_sin_GMF()
    {
        var (dto, ordenes) = await FallecidoConDosBeneficiariosAsync(gmfEnEntregas: false);
        var id = (await CuentaAsync(dto)).Id;

        ordenes.Select(o => o.Amount).Should().AllBeEquivalentTo(500_000m);
        var saldos = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        saldos.GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(0m);
        (await _a.Db.MemberAccountTransactions.AsNoTracking().AnyAsync(t => t.MemberAccountId == id && t.Kind == MemberAccountTransactionKind.GmfCharged)).Should().BeFalse();
        (await _a.HechosAsync()).Should().NotContain(GmfRetenidoV1.Type);
    }

    private async Task<(int Id, decimal Saldo)> NoReclamadoAsync(bool gmfEnEntregas)
    {
        _conGmf = true;
        Parametro(ParametrosDeCartera.CuentasAbandonadasTrasladoHabilitado, "true");
        Parametro(ParametrosDeCartera.CuentasAbandonadasDiasSinMovimiento, "60");
        var dto = await AhorroAsync(500_000m, gmfEnEntregas: gmfEnEntregas);
        await InactivaAsync(dto);
        var id = (await CuentaAsync(dto)).Id;
        var d = CuentasDePrueba.Hoy.AddDays(60);
        Dia(d);
        (await new NoReclamadosDelCiclo(_a.Db, NoReclamados(), _a.C.Cerrojo, _a.C.Contexto).ProcesarAsync(Corrida, d, [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();
        return (id, (await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync()).Amount);
    }

    private async Task<Application.Lending.Deposits.Unclaimed.UnclaimedBalanceDto> PagarNoReclamadoAsync(FormaDePagoDeNoReclamado forma, Guid? destino = null)
    {
        var saldo = await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync();
        var pago = await new PayUnclaimedBalanceCommandHandler(_a.Db, NoReclamados(), _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo)
            .Handle(new PayUnclaimedBalanceCommand(saldo.PublicId, forma, destino, null, null, null, "El titular se presentó") { OperationKey = Guid.NewGuid() }, default);
        pago.IsSuccess.Should().BeTrue(pago.IsFailure ? $"{pago.Error.Code}: {pago.Error.Message}" : string.Empty);
        _a.Db.ChangeTracker.Clear();
        return pago.Value;
    }

    [Fact]
    public async Task E253_el_pago_de_un_saldo_no_reclamado_retiene_el_GMF_del_pago_y_un_giro_devuelto_lo_restituye()
    {
        var (id, saldo) = await NoReclamadoAsync(gmfEnEntregas: true);
        await PagarNoReclamadoAsync(FormaDePagoDeNoReclamado.DisbursementOrder);

        var gmf = Math.Round(saldo * 0.004m, 0, MidpointRounding.AwayFromZero);
        gmf.Should().BeGreaterThan(0m);
        var orden = await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Purpose == PayoutPurpose.UnclaimedPayment);
        orden.Amount.Should().Be(saldo - gmf, "neto = lo no reclamado menos el GMF");
        var saldos = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        saldos.GetValueOrDefault(MemberAccountConcept.Unclaimed).Should().Be(0m, "el giro y el GMF vacían los no reclamados");
        saldos.GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(gmf);
        var uso = await _a.Db.GmfMonthlyUsages.AsNoTracking().SingleAsync(u => u.MemberAccountId == id);
        uso.TaxableEventCode.Should().Be(HechosGeneradoresDeGmf.PagoDeNoReclamado);
        uso.GmfAmount.Should().Be(gmf);
        (await _a.HechosAsync()).Should().Contain(GmfRetenidoV1.Type);

        // Tesorería rechaza el giro: el reverso devuelve todo —lo girado y el GMF— a no reclamados y el saldo vuelve a retenido.
        (await Ordenes().AplicarResultadoAsync(orden, DisbursementOrderStatus.Rejected, "R-1", _a.G.HoyLocal, "Cuenta errada", default)).IsSuccess.Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync()).Status.Should().Be(UnclaimedStatus.Held);
        var despues = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        despues.GetValueOrDefault(MemberAccountConcept.Unclaimed).Should().Be(saldo);
        despues.GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(0m);
    }

    [Fact]
    public async Task E253_con_la_linea_en_no_cobra_el_pago_de_un_no_reclamado_gira_el_saldo_entero()
    {
        var (id, saldo) = await NoReclamadoAsync(gmfEnEntregas: false);
        await PagarNoReclamadoAsync(FormaDePagoDeNoReclamado.DisbursementOrder);

        (await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Purpose == PayoutPurpose.UnclaimedPayment)).Amount.Should().Be(saldo);
        (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id].GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(0m);
        (await _a.HechosAsync()).Should().NotContain(GmfRetenidoV1.Type);
    }

    [Fact]
    public void E253_el_GMF_de_una_porcion_sale_del_capital_y_si_no_alcanza_del_interes_por_pagar()
    {
        var porcion = new List<(MemberAccountConcept, decimal)> { (MemberAccountConcept.Principal, 100m), (MemberAccountConcept.InterestPayable, 50m), (MemberAccountConcept.Bonus, 10m) };

        var (netos, deGmf) = Application.Lending.Deposits.Deceased.Fallecimientos.DescontarGmf(porcion, 40m)!.Value;
        netos.Should().Equal([(MemberAccountConcept.Principal, 60m), (MemberAccountConcept.InterestPayable, 50m), (MemberAccountConcept.Bonus, 10m)]);
        deGmf.Should().Equal([(MemberAccountConcept.Principal, 40m)]);

        var (netos2, deGmf2) = Application.Lending.Deposits.Deceased.Fallecimientos.DescontarGmf(porcion, 120m)!.Value;
        netos2.Should().Equal([(MemberAccountConcept.InterestPayable, 30m), (MemberAccountConcept.Bonus, 10m)]);
        deGmf2.Should().Equal([(MemberAccountConcept.Principal, 100m), (MemberAccountConcept.InterestPayable, 20m)]);

        Application.Lending.Deposits.Deceased.Fallecimientos.DescontarGmf(porcion, 200m).Should().BeNull("la bonificación no cubre GMF");
        Application.Lending.Deposits.Deceased.Fallecimientos.DescontarGmf(porcion, 0m)!.Value.Netos.Should().Equal(porcion);
    }
}
