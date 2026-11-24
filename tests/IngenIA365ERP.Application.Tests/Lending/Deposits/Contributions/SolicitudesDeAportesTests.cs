using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// Feature 014, US3 (T086, T092; api.md §7; FR-042, FR-043, FR-044, FR-048): la devolución de aportes sin retiro y la cesión entre asociados, radicadas
/// por una persona y aprobadas por otra; la devolución debita los aportes (primero los extraordinarios) y nace su orden de giro con
/// <c>DevolucionOrdenada</c> y <c>OrdenDeGiroEmitida</c> v2; la cesión es un traslado entre las dos cuentas de aportes. Nunca perforan el mínimo ni se
/// hacen con créditos vivos. (nuevo)
/// </summary>
public sealed class SolicitudesDeAportesTests : IDisposable
{
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    private SolicitudesDeAportes Solicitudes() => new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Reglas());

    private RequestContributionRefundCommandHandler PedirDevolucion() =>
        new(_a.Db, Solicitudes(), _a.Reglas(), _a.G.Intervenciones(), _a.G.Actor, _a.G.Reloj);

    private ApproveContributionRefundCommandHandler AprobarDevolucion() =>
        new(_a.Db, Solicitudes(), _a.Reglas(), _a.G.Intervenciones(), _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private RequestContributionAssignmentCommandHandler PedirCesion() =>
        new(_a.Db, Solicitudes(), _a.Reglas(), _a.G.Intervenciones(), _a.G.Actor, _a.G.Reloj);

    private ApproveContributionAssignmentCommandHandler AprobarCesion() =>
        new(_a.Db, Solicitudes(), _a.Reglas(), _a.G.Intervenciones(), _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private static T Ok<T>(Result<T> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Ana con 1.000.000 de aportes: el mínimo (500.000) primero y el resto extraordinario.</summary>
    private async Task<MemberAccountDto> AnaConAportesAsync(bool cesion = false)
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version() with { AllowsTransferBetweenMembers = cesion });
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 1_000_000m, ModalidadDeConsignacion.Free));
        return cuenta;
    }

    [Fact]
    public async Task La_devolucion_la_radica_una_persona_la_aprueba_otra_y_sale_por_orden_de_giro()
    {
        var cuenta = await AnaConAportesAsync();

        var pedida = Ok(await PedirDevolucion().Handle(new RequestContributionRefundCommand(cuenta.PublicId, 150_000m, ContributionRefundCause.Other, null,
            "Decisión del consejo, acta 8"), default));
        pedida.Status.Should().Be(ContributionRequestStatus.PendingApproval);

        var mismaPersona = await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.DisbursementOrder,
            null, null, "Aprobada"), default);
        mismaPersona.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto, "quien radica no aprueba");

        _a.G.ActuarComo(_a.C.Jefe);
        _a.Db.ChangeTracker.Clear();
        var aprobada = Ok(await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.DisbursementOrder,
            null, null, "Aprobada por el gerente"), default));
        _a.Db.ChangeTracker.Clear();

        aprobada.Status.Should().Be(ContributionRequestStatus.Approved);
        aprobada.OperationNumber.Should().StartWith("DVC-");
        aprobada.PayoutOrderNumber.Should().StartWith("OG-");
        var filas = await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.Kind == MemberAccountTransactionKind.Refund).ToListAsync();
        filas.Select(f => (f.Concept, f.Amount)).Should().BeEquivalentTo(new[] { (MemberAccountConcept.ContributionExtraordinary, -150_000m) });
        (await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync()).Purpose.Should().Be(PayoutPurpose.ContributionRefund);
        (await _a.HechosAsync()).Should().Contain([DevolucionOrdenadaV1.Type, OrdenDeGiroEmitidaV2.Type]);

        var otraVez = await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.DisbursementOrder,
            null, null, "Otra vez"), default);
        otraVez.Error.Code.Should().Be(ErroresDeAportes.CodigoSolicitudDecidida);
    }

    [Fact]
    public async Task La_devolucion_no_perfora_el_minimo_ni_se_pide_con_creditos_vivos()
    {
        var cuenta = await AnaConAportesAsync();

        (await PedirDevolucion().Handle(new RequestContributionRefundCommand(cuenta.PublicId, 600_000m, ContributionRefundCause.Other, null, "Más del mínimo"),
            default)).Error.Code.Should().Be(ErroresDeAportes.CodigoMinimo);

        _a.Db.Loans.Add(new Loan { Number = "CR-0001", DebtorPersonId = _a.C.Ana.Id, Status = LoanStatus.Active, CreditLineId = 1, BranchId = _a.C.Oficina.Id });
        await _a.Db.SaveChangesAsync();
        var conCredito = await PedirDevolucion().Handle(new RequestContributionRefundCommand(cuenta.PublicId, 10_000m, ContributionRefundCause.Other, null,
            "Con crédito"), default);
        conCredito.Error.Code.Should().Be(ErroresDeAportes.CodigoConCreditosVivos);
    }

    [Fact]
    public async Task La_cesion_solo_si_los_estatutos_la_permiten()
    {
        var sinPermiso = await AnaConAportesAsync();
        (await PedirCesion().Handle(new RequestContributionAssignmentCommand(sinPermiso.PublicId, _a.C.Beto.PublicId, 50_000m, null, "Cesión"), default))
            .Error.Code.Should().Be(ErroresDeAportes.CodigoCesionNoAdmitida);
    }

    [Fact]
    public async Task La_cesion_aprobada_es_un_traslado_entre_las_dos_cuentas_de_aportes_con_su_hecho()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version() with { AllowsTransferBetweenMembers = true });
        var ana = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        var beto = await _a.CuentaDeAportesAsync(_a.C.Beto, linea);
        await _a.ConsignadaAsync(_a.Consignacion(ana, 700_000m, ModalidadDeConsignacion.Free));

        var pedida = Ok(await PedirCesion().Handle(new RequestContributionAssignmentCommand(ana.PublicId, _a.C.Beto.PublicId, 150_000m, null,
            "Cesión aprobada por el consejo"), default));
        _a.G.ActuarComo(_a.C.Jefe);
        _a.Db.ChangeTracker.Clear();
        var aprobada = Ok(await AprobarCesion().Handle(new ApproveContributionAssignmentCommand(pedida.PublicId, "Conforme a estatutos"), default));
        _a.Db.ChangeTracker.Clear();

        aprobada.Status.Should().Be(ContributionRequestStatus.Approved);
        var porCuenta = await _a.Db.MemberAccountTransactions.AsNoTracking().GroupBy(t => t.MemberAccountId)
            .Select(g => new { g.Key, Total = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total);
        var idAna = await _a.Db.MemberAccounts.Where(a => a.PublicId == ana.PublicId).Select(a => a.Id).SingleAsync();
        var idBeto = await _a.Db.MemberAccounts.Where(a => a.PublicId == beto.PublicId).Select(a => a.Id).SingleAsync();
        (porCuenta[idAna], porCuenta[idBeto]).Should().Be((550_000m, 150_000m));
        (await _a.HechosAsync()).Should().Contain(TrasladoEntreCuentasAplicadoV1.Type);
    }

    // ---------------------------------------------------------------------------------- cerrar una cuenta (2026-11-24) --

    /// <summary>Ana con dos cuentas de aportes en la misma línea: 1.000.000 en la primera y <paramref name="enLaSegunda"/> en la segunda.</summary>
    private async Task<(MemberAccountDto Primera, MemberAccountDto Segunda)> AnaConDosCuentasAsync(decimal enLaSegunda)
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());
        var primera = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        var segunda = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        await _a.ConsignadaAsync(_a.Consignacion(primera, 1_000_000m, ModalidadDeConsignacion.Free));
        await _a.ConsignadaAsync(_a.Consignacion(segunda, enLaSegunda, ModalidadDeConsignacion.Free));
        return (primera, segunda);
    }

    private async Task<(MemberAccountStatus Estado, decimal Saldo)> CuentaAsync(MemberAccountDto cuenta)
    {
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var estado = await _a.Db.MemberAccounts.Where(a => a.Id == id).Select(a => a.Status).SingleAsync();
        var saldo = await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == id).SumAsync(t => t.Amount);
        return (estado, saldo);
    }

    [Fact]
    public async Task Cerrar_una_cuenta_de_aportes_con_traslado_pasa_todo_su_saldo_a_otra_del_asociado_y_la_cancela()
    {
        var (primera, segunda) = await AnaConDosCuentasAsync(200_000m);

        (await PedirDevolucion().Handle(new RequestContributionRefundCommand(segunda.PublicId, 150_000m, ContributionRefundCause.AccountClosure, null,
            "Vendió el vehículo"), default)).Error.Code.Should().Be(ErroresDeAportes.CodigoMontoDelCierre, "el cierre pide todo el saldo");
        var pedida = Ok(await PedirDevolucion().Handle(new RequestContributionRefundCommand(segunda.PublicId, 200_000m, ContributionRefundCause.AccountClosure,
            null, "Vendió el vehículo"), default));

        _a.G.ActuarComo(_a.C.Jefe);
        _a.Db.ChangeTracker.Clear();
        (await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.TransferToAccount, null,
            segunda.PublicId, "Aprobada"), default)).Error.Code.Should().Be(ErroresDeAportes.CodigoDestinoDelCierre, "no a la misma cuenta");
        _a.Db.ChangeTracker.Clear();
        var aprobada = Ok(await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.TransferToAccount,
            null, primera.PublicId, "Aprobada"), default));
        _a.Db.ChangeTracker.Clear();

        aprobada.Status.Should().Be(ContributionRequestStatus.Approved);
        aprobada.PayoutOrderNumber.Should().BeNull("el saldo no sale de los aportes");
        (await CuentaAsync(segunda)).Should().Be((MemberAccountStatus.Closed, 0m));
        (await CuentaAsync(primera)).Should().Be((MemberAccountStatus.Active, 1_200_000m));
        (await _a.HechosAsync()).Should().Contain(TrasladoEntreCuentasAplicadoV1.Type);
    }

    [Fact]
    public async Task Cerrar_con_devolucion_deja_la_cuenta_en_cierre_con_su_orden_y_la_ultima_no_se_cierra_asi()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());
        var sola = await _a.CuentaDeAportesAsync(_a.C.Beto, linea);
        await _a.ConsignadaAsync(_a.Consignacion(sola, 100_000m, ModalidadDeConsignacion.Free));
        (await PedirDevolucion().Handle(new RequestContributionRefundCommand(sola.PublicId, 100_000m, ContributionRefundCause.AccountClosure, null,
            "Desafilió el vehículo"), default)).Error.Code.Should().Be(ErroresDeAportes.CodigoUltimaCuenta, "la última se cierra con el retiro");

        var primera = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        var segunda = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        await _a.ConsignadaAsync(_a.Consignacion(primera, 1_000_000m, ModalidadDeConsignacion.Free));
        await _a.ConsignadaAsync(_a.Consignacion(segunda, 600_000m, ModalidadDeConsignacion.Free));
        var pedida = Ok(await PedirDevolucion().Handle(new RequestContributionRefundCommand(primera.PublicId, 1_000_000m,
            ContributionRefundCause.AccountClosure, null, "Desafilió el vehículo"), default));

        _a.G.ActuarComo(_a.C.Jefe);
        _a.Db.ChangeTracker.Clear();
        var aprobada = Ok(await AprobarDevolucion().Handle(new ApproveContributionRefundCommand(pedida.PublicId, FormaDeDevolucionDeAportes.DisbursementOrder,
            null, null, "Aprobada"), default));
        _a.Db.ChangeTracker.Clear();

        aprobada.PayoutOrderNumber.Should().StartWith("OG-");
        (await CuentaAsync(primera)).Should().Be((MemberAccountStatus.Closing, 0m), "queda en cierre hasta que Tesorería gire la orden");
        (await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync()).Amount.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task El_minimo_es_del_asociado_lo_que_tiene_en_sus_otras_cuentas_lo_cubre()
    {
        // Mínimo 500.000: de la primera (1.000.000) salen 700.000 porque la segunda (600.000) sostiene el mínimo del asociado.
        var (primera, _) = await AnaConDosCuentasAsync(600_000m);

        var pedida = await PedirDevolucion().Handle(new RequestContributionRefundCommand(primera.PublicId, 700_000m, ContributionRefundCause.Other, null,
            "Decisión del consejo"), default);

        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        (await PedirDevolucion().Handle(new RequestContributionRefundCommand(primera.PublicId, 1_000_000m, ContributionRefundCause.Other, null,
            "Todo"), default)).IsSuccess.Should().BeTrue("quedan 600.000 en la otra, sobre el mínimo");
    }
}
