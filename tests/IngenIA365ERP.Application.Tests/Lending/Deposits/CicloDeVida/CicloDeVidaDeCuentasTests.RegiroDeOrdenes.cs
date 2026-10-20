using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.CicloDeVida;

/// <summary>
/// Respuesta 96 del dueño (2026-10-21, research E258): si Tesorería rechaza el giro de la entrega por fallecimiento a un beneficiario, el valor <b>no vuelve</b> a
/// la cuenta del fallecido: sigue respaldado en la cuenta por pagar a favor del beneficiario, la orden queda pendiente de regiro —la cuenta sigue en cierre y el
/// caso sin entregar— y se vuelve a girar, sin cobrar el GMF otra vez. Lo de no reclamados (E253) no cambia: ése sí reintegra.
/// </summary>
public sealed partial class CicloDeVidaDeCuentasTests
{
    private RegiroDeOrdenesDeCuentas Regiro() => new(_a.Db, Ordenes(), _a.C.Cerrojo);

    private async Task<Result<PayoutOrderReissueDto>> RegirarEntregaAsync(Guid caso, Guid orden)
    {
        var r = await new ReissueDeceasedPayoutOrderCommandHandler(_a.Db, Vista(), Regiro())
            .Handle(new ReissueDeceasedPayoutOrderCommand(caso, orden, null, null, null, "Cuenta del beneficiario corregida") { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private static T Ok<T>(Result<T> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task ResultadoAsync(int ordenId, DisbursementOrderStatus resultado, string referencia)
    {
        var viva = await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Id == ordenId);
        (await Ordenes().AplicarResultadoAsync(viva, resultado, referencia, _a.G.HoyLocal, "Resultado de Tesorería", default)).IsSuccess.Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task E258_el_giro_rechazado_de_una_entrega_por_fallecimiento_queda_por_pagar_al_beneficiario_y_se_regira_sin_cobrar_otro_GMF()
    {
        var (dto, ordenes) = await FallecidoConDosBeneficiariosAsync(gmfEnEntregas: true);
        var id = (await CuentaAsync(dto)).Id;
        var caso = await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync();
        ordenes.Should().HaveCount(2);

        // Tesorería rechaza la primera y gira la segunda.
        await ResultadoAsync(ordenes[0].Id, DisbursementOrderStatus.Rejected, "R-1");
        await ResultadoAsync(ordenes[1].Id, DisbursementOrderStatus.Disbursed, "G-2");

        // El valor no volvió a la cuenta del fallecido (y el GMF ya retenido tampoco se devuelve): sigue por pagar a favor del beneficiario.
        var saldos = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        saldos.GetValueOrDefault(MemberAccountConcept.Principal).Should().Be(0m);
        saldos.GetValueOrDefault(MemberAccountConcept.Gmf).Should().Be(2 * GmfDeMedioMillon);
        (await OrdenesDeGiroDeCuentas.PendientesDeRegiro(_a.Db).AsNoTracking().Select(o => o.Id).ToListAsync()).Should().BeEquivalentTo([ordenes[0].Id]);
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closing, "una orden pendiente de regiro mantiene la cuenta en cierre");
        (await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync()).Status.Should().Be(DeceasedCaseStatus.Approved, "el caso sigue sin entregarse");
        (await _a.HechosAsync()).Should().NotContain(OperacionDeCuentaReversadaV1.Type, "no hay reintegro: el rechazo no reversa nada");

        // La que se pagó no se regira; la rechazada sí, una sola vez, con el mismo valor y la misma operación.
        (await RegirarEntregaAsync(caso.PublicId, ordenes[1].PublicId)).Error.Code.Should().Be(OrdenesDeGiroDeCuentas.CodigoNoSeRegira);
        var nueva = Ok(await RegirarEntregaAsync(caso.PublicId, ordenes[0].PublicId));
        nueva.Amount.Should().Be(498_000m);
        nueva.ReissuesOrderNumber.Should().Be(ordenes[0].Number);
        (await RegirarEntregaAsync(caso.PublicId, ordenes[0].PublicId)).Error.Code.Should().Be(OrdenesDeGiroDeCuentas.CodigoNoSeRegira);
        var guardada = await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync(o => o.PublicId == nueva.OrderPublicId);
        guardada.OperationId.Should().Be(ordenes[0].OperationId);
        guardada.BeneficiaryPersonId.Should().Be(ordenes[0].BeneficiaryPersonId);
        guardada.DeceasedHolderCaseId.Should().Be(caso.Id);
        guardada.Status.Should().Be(DisbursementOrderStatus.Issued);
        (await _a.Db.GmfMonthlyUsages.AsNoTracking().CountAsync(u => u.MemberAccountId == id)).Should().Be(2, "el regiro no vuelve a causar el GMF");
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closing);

        // Tesorería gira el regiro: la cuenta se cancela y el caso queda entregado.
        await ResultadoAsync(guardada.Id, DisbursementOrderStatus.Disbursed, "G-3");
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closed);
        (await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync()).Status.Should().Be(DeceasedCaseStatus.Delivered);
    }

    [Fact]
    public async Task E258_un_regiro_rechazado_otra_vez_vuelve_a_quedar_pendiente_y_admite_otro_regiro_a_otra_cuenta()
    {
        var (dto, ordenes) = await FallecidoConDosBeneficiariosAsync(gmfEnEntregas: false);
        var caso = await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync();
        await ResultadoAsync(ordenes[0].Id, DisbursementOrderStatus.Rejected, "R-1");
        var primera = Ok(await RegirarEntregaAsync(caso.PublicId, ordenes[0].PublicId));
        var idPrimera = (await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync(o => o.PublicId == primera.OrderPublicId)).Id;

        await ResultadoAsync(idPrimera, DisbursementOrderStatus.Voided, "A-1");
        (await OrdenesDeGiroDeCuentas.PendientesDeRegiro(_a.Db).AsNoTracking().Select(o => o.Id).ToListAsync()).Should().BeEquivalentTo([idPrimera],
            "la rechazada original ya tiene su regiro; el pendiente es el último");
        var segunda = Ok(await RegirarEntregaAsync(caso.PublicId, primera.OrderPublicId));
        segunda.ReissuesOrderNumber.Should().Be(primera.OrderNumber);
        segunda.Amount.Should().Be(500_000m, "sin GMF en la línea: el valor entero");
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closing);
    }
}
