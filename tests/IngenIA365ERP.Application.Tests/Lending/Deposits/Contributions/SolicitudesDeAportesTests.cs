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
}
