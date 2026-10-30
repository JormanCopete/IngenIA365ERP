using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Feature 015, R1, T0347 (contratos-v2.md §3.1, §3.4; E-02 a, E-02 d; FR-165 a, FR-165 d): la versión 2 del contrato de cuentas.
/// <list type="bullet">
/// <item><c>ConsignacionAAplicar.BankItem</c> con la misma regla que Cartera: v1 → <c>VersionNotSupported</c>; con la fecha del movimiento se exime la ventana de
/// <c>Captaciones.DiasMaximosFechaValor</c>; con otra fecha <c>Lending.Payment.BankItemDateMismatch</c>; la operación guarda la partida;</item>
/// <item>la consignación que cita la línea de pagaduría ya verificada también se exime de la ventana (como Cartera);</item>
/// <item><c>RetirarAsync</c> con <c>CloseAccount = true</c> deja de responder <c>Lending.Savings.DeliveryNotAvailable</c> cuando el recaudador es <c>Receipts</c> real:
/// liquida, cobra GMF y cargos, autoriza el retiro de todo el disponible y cancela la cuenta; otro recaudador sigue sin cancelar por el contrato.</item>
/// </list>
/// Prueba escrita, sin correr (estrategia de R1).
/// </summary>
public sealed class ContratoDeCuentasV2Tests : IDisposable
{
    private static readonly Recaudador Recaudo = new("REC", ClaseDeRecaudador.Receipts);
    private readonly AportesDePrueba _a = new();

    public ContratoDeCuentasV2Tests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, 49_799m, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _a.Recaudadores["REC"] = IntegrationParticipantClass.Receipts;
    }

    public void Dispose() => _a.Dispose();

    private async Task<MemberAccountDto> CuentaALaVistaAsync()
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        return await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
    }

    private void AdoptarV2(string codigo) =>
        _a.Adopciones[codigo] = [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 1), new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 2)];

    private static PartidaBancariaCitada Partida(DateOnly movimiento) => new(Guid.NewGuid(), movimiento, Guid.NewGuid());

    [Fact]
    public async Task Con_solo_la_v1_la_partida_citada_responde_VersionNotSupported()
    {
        var cuenta = await CuentaALaVistaAsync();
        var r = await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer)
            with { BankItem = Partida(_a.G.HoyLocal) });

        r.Code.Should().Be(ErroresDePagos.CodigoVersionNoSoportada);
    }

    [Fact]
    public async Task Con_la_v2_y_la_fecha_del_movimiento_se_exime_la_ventana_y_la_operacion_guarda_la_partida()
    {
        AdoptarV2(ClavesDeRecaudador.Prueba);
        var cuenta = await CuentaALaVistaAsync();
        var hoy = _a.G.HoyLocal;
        _a.C.G.HoyLocal = hoy.AddDays(20);
        var movimiento = hoy.AddDays(2);

        var sin = await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, movimiento, medio: ClaseDeMedioDeRecaudo.Transfer));
        sin.Code.Should().Be(ErroresDeAportes.CodigoFechaValor, "dieciocho días atrás pasan la ventana de Captaciones");

        var partida = Partida(movimiento);
        var con = await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, movimiento, medio: ClaseDeMedioDeRecaudo.Transfer)
            with { BankItem = partida });
        con.Result.Should().Be(ResultadoDelContrato.Applied, $"{con.Code}: {con.Message}");
        var op = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.PublicId == con.Value!.OperationPublicId);
        (op.BankItemPublicId, op.ValueDate).Should().Be((partida.BankItemPublicId, movimiento));

        var otraFecha = await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, movimiento.AddDays(1),
            medio: ClaseDeMedioDeRecaudo.Transfer) with { BankItem = partida });
        otraFecha.Code.Should().Be(ErroresDePagos.CodigoPartidaConOtraFecha);
    }

    [Fact]
    public async Task La_simulacion_de_la_cancelacion_dice_lo_que_entrega_y_sin_Recaudo_real_no_se_cancela_por_el_contrato()
    {
        _a.ConCancelacionConEntrega = true;
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 400_000m, ModalidadDeConsignacion.Free));

        var ajeno = await _a.Contrato().RetirarAsync(Retiro(AportesDePrueba.Prueba, cuenta, 400_000m, cerrar: true), default);
        ajeno.Code.Should().Be(ErroresDeAportes.CodigoEntregaNoDisponible, "sólo Recaudo real cancela con entrega (E-02 d)");

        var simulada = await _a.Contrato().SimularAsync(new SolicitudDeSimulacionDeCuenta(Recaudo, "Close", cuenta.PublicId, 1m, _a.G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId]), default);
        simulada.Value!.Allowed.Should().BeTrue(simulada.Message);
        simulada.Value.AmountToDeliver.Should().BeGreaterThan(0m).And.BeLessThan(400_000m, "el GMF de la cancelación sale del saldo");
    }

    [Fact]
    public async Task Recaudo_real_cancela_con_entrega_debita_todo_el_disponible_y_la_cuenta_queda_cancelada()
    {
        _a.ConCancelacionConEntrega = true;
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 400_000m, ModalidadDeConsignacion.Free));
        var simulada = await _a.Contrato().SimularAsync(new SolicitudDeSimulacionDeCuenta(Recaudo, "Close", cuenta.PublicId, 1m, _a.G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId]), default);
        var entrega = simulada.Value!.AmountToDeliver!.Value;

        var cambiado = await _a.Contrato().RetirarAsync(Retiro(Recaudo, cuenta, entrega + 1m, cerrar: true), default);
        cambiado.Code.Should().Be(CancelacionConEntregaPorElContrato.CodigoValorCambiado);
        _a.Db.ChangeTracker.Clear();

        var r = await _a.Contrato().RetirarAsync(Retiro(Recaudo, cuenta, entrega, cerrar: true), default);
        _a.Db.ChangeTracker.Clear();
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        r.Value!.Amount.Should().Be(entrega);
        var final = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == cuenta.PublicId);
        final.Status.Should().Be(MemberAccountStatus.Closed, "debita todo el disponible y la cuenta queda cancelada");
        (await _a.Db.MemberAccountOperations.AsNoTracking().CountAsync(o => o.Kind == MemberAccountOperationKind.Cancellation)).Should().Be(1);
    }

    private static SolicitudDeRetiro Retiro(Recaudador recaudador, MemberAccountDto cuenta, decimal valor, bool cerrar, string? clave = null) =>
        new(recaudador, clave ?? $"{recaudador.Code}:{Guid.NewGuid():N}:{cuenta.PublicId:N}", cuenta.PublicId, valor, Sessions.GobiernoDePrueba.Hoy,
            ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId], cuenta.Holder.PersonPublicId,
            new ReferenciaDeRecibo("EG-1", Sessions.GobiernoDePrueba.Hoy, null, null, "CashDisbursement"), CloseAccount: cerrar);
}
