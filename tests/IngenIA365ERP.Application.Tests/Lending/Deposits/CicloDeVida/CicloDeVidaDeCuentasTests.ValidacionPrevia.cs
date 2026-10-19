using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Application.Lending.Deposits.Unclaimed;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.CicloDeVida;

/// <summary>
/// Pendiente de la fase 17 (D23; research E206): la aprobación de la entrega por fallecimiento, el traslado a mano a no reclamados y el pago de un saldo no
/// reclamado preguntan a Contabilidad antes de mover nada, como las demás pantallas de captaciones. Con <c>Contabilidad.FaltaDeRegla</c> en su defecto
/// (<c>Bloquear</c>) y una regla faltante responden <c>Lending.Prevalidation.NotPostable</c> sin tocar el libro; con <c>Advertir</c>, siguen.
/// </summary>
public sealed partial class CicloDeVidaDeCuentasTests
{
    private const string NoContabilizable = "Lending.Prevalidation.NotPostable";

    private ValidacionPreviaDeCaptaciones ValidacionPreviaDeCaptaciones() => new(_a.Db, _a.ValidacionPrevia());

    /// <summary>Contabilidad responde que a la matriz le falta la regla de <paramref name="hecho"/> (o que todo se contabiliza, si es nulo).</summary>
    private void Matriz(string? hecho)
    {
        _a.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(hecho is null
                ? Result.Success(new ResultadoDeEvaluacionContableDto(true, [], []))
                : Result.Success(new ResultadoDeEvaluacionContableDto(false,
                    [new HallazgoDeCarteraDto(hecho, "NoReclamadosPorPagar", null, null, "Accounting.LendingRule.Missing", "Falta la regla",
                        new QuienCorrigeDto("Accounting", null, null))], [])));
    }

    private async Task<(MemberAccountDtoRef Cuenta, int Id)> InactivaParaNoReclamadosAsync()
    {
        Parametro(ParametrosDeCartera.CuentasAbandonadasTrasladoHabilitado, "true");
        Parametro(ParametrosDeCartera.CuentasAbandonadasDiasSinMovimiento, "30");
        var dto = await AhorroAsync(500_000m);
        await InactivaAsync(dto);
        return (new MemberAccountDtoRef(dto.PublicId), (await CuentaAsync(dto)).Id);
    }

    private sealed record MemberAccountDtoRef(Guid PublicId);

    private TransferToUnclaimedCommandHandler Trasladar() =>
        new(_a.Db, NoReclamados(), _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, _a.C.Contexto, _a.G.Reloj, ValidacionPreviaDeCaptaciones());

    [Fact]
    public async Task E206_sin_la_regla_de_no_reclamados_el_traslado_a_mano_se_bloquea_y_no_mueve_nada()
    {
        var (cuenta, id) = await InactivaParaNoReclamadosAsync();
        Matriz(SaldoTrasladadoANoReclamadosV1Type);

        var r = await Trasladar().Handle(new TransferToUnclaimedCommand([cuenta.PublicId], null, "Reglamento, art. 30") { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(NoContabilizable, r.Error.Message);
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.UnclaimedBalances.AsNoTracking().AnyAsync()).Should().BeFalse();
        (await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Id == id)).Status.Should().Be(MemberAccountStatus.Inactive);
        await _a.Contabilidad.Received().EvaluarAsync(Arg.Is<IReadOnlyList<MensajeContableDto>>(h => h.Any(m => m.Envelope.Type == SaldoTrasladadoANoReclamadosV1Type)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task E206_con_Advertir_el_traslado_sigue_aunque_falte_la_regla()
    {
        Parametro(ParametrosDeCartera.ContabilidadFaltaDeRegla, "Advertir");
        var (cuenta, _) = await InactivaParaNoReclamadosAsync();
        Matriz(SaldoTrasladadoANoReclamadosV1Type);

        var r = await Trasladar().Handle(new TransferToUnclaimedCommand([cuenta.PublicId], null, "Reglamento, art. 30") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (await _a.Db.UnclaimedBalances.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task E206_sin_la_regla_de_la_devolucion_el_pago_de_un_no_reclamado_se_bloquea()
    {
        var (cuenta, _) = await InactivaParaNoReclamadosAsync();
        Matriz(null);
        (await Trasladar().Handle(new TransferToUnclaimedCommand([cuenta.PublicId], null, "Reglamento") { OperationKey = Guid.NewGuid() }, default)).IsSuccess
            .Should().BeTrue();
        _a.Db.ChangeTracker.Clear();
        var saldo = await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync();
        Matriz("DevolucionOrdenada");

        var pago = await new PayUnclaimedBalanceCommandHandler(_a.Db, NoReclamados(), _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, _a.G.Reloj,
                ValidacionPreviaDeCaptaciones())
            .Handle(new PayUnclaimedBalanceCommand(saldo.PublicId, FormaDePagoDeNoReclamado.DisbursementOrder, null, null, null, null, "El titular se presentó")
            {
                OperationKey = Guid.NewGuid(),
            }, default);

        pago.IsFailure.Should().BeTrue();
        pago.Error.Code.Should().Be(NoContabilizable);
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync()).Status.Should().Be(UnclaimedStatus.Held);
        (await _a.Db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.Purpose == PayoutPurpose.UnclaimedPayment)).Should().BeFalse();
        await _a.Contabilidad.Received().EvaluarAsync(Arg.Is<IReadOnlyList<MensajeContableDto>>(h => h.Any(m => m.Envelope.Type == "DevolucionOrdenada")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task E206_sin_la_regla_de_la_devolucion_la_entrega_por_fallecimiento_no_se_aprueba_y_con_la_matriz_completa_si()
    {
        await AhorroAsync(1_000_000m, [new(_a.C.Carla.PublicId, 50m), new(_a.C.Dario.PublicId, 50m)]);
        var caso = await RegistradoAsync();
        caso = await EntregaRegistradaAsync(caso, DeliveryRoute.Beneficiaries, null);
        _a.G.ActuarComo(_a.C.Jefe);
        Matriz("DevolucionOrdenada");
        ApproveDeceasedDeliveryCommandHandler Aprobar() =>
            new(_a.Db, Vista(), Fallecimientos(), _a.G.Intervenciones(), _a.C.Cerrojo, _a.G.Reloj, ValidacionPreviaDeCaptaciones());

        var bloqueada = await Aprobar().Handle(new ApproveDeceasedDeliveryCommand(caso.PublicId, "Aprobada") { OperationKey = Guid.NewGuid() }, default);

        bloqueada.IsFailure.Should().BeTrue();
        bloqueada.Error.Code.Should().Be(NoContabilizable);
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync()).Status.Should().Be(DeceasedCaseStatus.DeliveryPending);
        (await _a.Db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.Purpose == PayoutPurpose.DeceasedDelivery)).Should().BeFalse();

        Matriz(null);
        var aprobada = await Aprobar().Handle(new ApproveDeceasedDeliveryCommand(caso.PublicId, "Aprobada") { OperationKey = Guid.NewGuid() }, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        (await _a.Db.AccountPayoutOrders.AsNoTracking().CountAsync(o => o.Purpose == PayoutPurpose.DeceasedDelivery)).Should().Be(2);
    }

    [Fact]
    public void E206_los_representativos_del_ciclo_de_vida_se_desglosan_en_roles_de_su_hecho()
    {
        var d = HechosRepresentativosDeCaptaciones.Dimensiones("AHVISTA", LineNature.Deposit, DepositClass.Demand, Guid.NewGuid());
        var c = HechosRepresentativosDeCaptaciones.Cuenta(d, Guid.NewGuid(), "AH-1");
        var otra = HechosRepresentativosDeCaptaciones.Cuenta(d, Guid.NewGuid(), "AH-2");
        var dia = new DateOnly(2026, 12, 1);
        foreach (var unidad in new[]
                 {
                     HechosRepresentativosDeCaptaciones.Unidad(dia, c, HechosRepresentativosDeCaptaciones.TrasladoANoReclamados(c, "InactiveBalance")),
                     HechosRepresentativosDeCaptaciones.Unidad(dia, c, HechosRepresentativosDeCaptaciones.PagoDeNoReclamadoPorGiro(c)),
                     HechosRepresentativosDeCaptaciones.Unidad(dia, c, HechosRepresentativosDeCaptaciones.PagoDeNoReclamadoPorTraslado(c, otra)),
                     HechosRepresentativosDeCaptaciones.Unidad(dia, c, HechosRepresentativosDeCaptaciones.Devolucion(c, nameof(PayoutPurpose.DeceasedDelivery))),
                 })
        {
            var desglose = Application.Accounting.Lending.Contabilizacion.ConstructorDeLineasDeCartera.Desglosar(
                unidad.Select(Application.Accounting.Lending.Contabilizacion.HechoDeCartera.De).ToList());
            desglose.Fallos.Select(f => $"{f.FactType}: {f.Error.Code} {f.Error.Message}").Should().BeEmpty();
            desglose.Partidas.Should().NotBeEmpty();
        }
    }

    private const string SaldoTrasladadoANoReclamadosV1Type = Application.Common.Integration.Contracts.Lending.Deposits.SaldoTrasladadoANoReclamadosV1.Type;
}
