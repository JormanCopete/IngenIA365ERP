using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Deposits.Unclaimed;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.CicloDeVida;

/// <summary>
/// Feature 014, US14 (T211; FR-058, FR-065, FR-072, FR-081 a FR-084; research §23): el ciclo de vida de las cuentas sin movimiento y de los titulares
/// fallecidos sobre el libro real —la cuenta que pasa a inactiva a los días de la línea con su reclasificación, el interés que no la reactiva, la
/// consignación que sí (con su reclasificación de vuelta), la marca del GMF que rige desde una fecha futura, el traslado de una inactiva a no reclamados
/// (sólo con la regla habilitada) y su pago por giro devuelto por Tesorería, y el fallecimiento con dos beneficiarios al 50 % que genera dos órdenes de giro
/// y cancela la cuenta al pagarse ambas, aprobado por otra persona—. El escenario de <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K10. (nuevo)
/// </summary>
public sealed class CicloDeVidaDeCuentasTests : IDisposable
{
    private const int DiasDeInactividad = 30;

    private readonly AportesDePrueba _a = new();

    public CicloDeVidaDeCuentasTests()
    {
        _a.C.Entrega = EntregaDeCartera.K10;
        _a.C.EntregaDeParametros = EntregaDeCartera.K10;
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, 49_799m, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private static readonly LendingProcessRun Corrida = new() { Id = 1, Kind = LendingProcessKind.DailyCycle };

    private ReclasificacionDeCuenta Reclasificacion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj);

    private Application.Lending.Deposits.Accrual.Inactividad Subpaso() => new(_a.Db, Reclasificacion(), _a.C.Parametros(), _a.C.Cerrojo, _a.C.Contexto, _a.G.Reloj);

    private OrdenesDeGiroDeCuentas Ordenes() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Reversion(), _a.C.Parametros(), _a.G.Actor,
        _a.G.Reloj);

    private NoReclamados NoReclamados() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.LiquidacionALaFecha(), Ordenes(),
        _a.C.Parametros(), _a.G.Actor, _a.G.Reloj, reactivacion: _a.Reactivacion());

    private Fallecimientos Fallecimientos() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.LiquidacionALaFecha(), Ordenes(),
        _a.C.Parametros(), _a.G.Valores, _a.G.Actor, _a.G.Reloj, _a.Reactivacion());

    private VistaDeFallecimientos Vista() => new(_a.Db, _a.G.Alcance, _a.G.Auditoria, Fallecimientos(), _a.G.Reloj);

    private void Dia(DateOnly d)
    {
        _a.G.HoyLocal = d;
        _a.G.UtcNow = d.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
    }

    private void Parametro(string clave, string valor)
    {
        _a.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = _a.G.UtcNow.AddDays(-10),
        });
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
    }

    private async Task<MemberAccountDto> AhorroAsync(decimal saldo, IReadOnlyList<MemberAccountBeneficiaryInput>? beneficiarios = null)
    {
        var version = CuentasDePrueba.ALaVista() with { Inactivity = new DepositInactivityInput(DiasDeInactividad) };
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", version);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana, beneficiarios: beneficiarios));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        return cuenta;
    }

    private async Task<MemberAccount> CuentaAsync(MemberAccountDto dto) => await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == dto.PublicId);

    private async Task<int> ReclasificacionesAsync(int cuenta, string causa) =>
        await _a.Db.MemberAccountOperations.AsNoTracking()
            .CountAsync(o => o.MemberAccountId == cuenta && o.Kind == MemberAccountOperationKind.Reclassification && o.Payload.Contains($"\"cause\":\"{causa}\""));

    private async Task InactivaAsync(MemberAccountDto dto)
    {
        var id = (await CuentaAsync(dto)).Id;
        var d = CuentasDePrueba.Hoy.AddDays(DiasDeInactividad);
        Dia(d);
        (await Subpaso().ProcesarAsync(Corrida, d, [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();
    }

    // --------------------------------------------------------------------------------------------------- inactividad --

    [Fact]
    public async Task US14_a_los_dias_de_la_linea_la_cuenta_pasa_a_inactiva_y_reclasifica_su_saldo()
    {
        var dto = await AhorroAsync(1_000_000m);
        var id = (await CuentaAsync(dto)).Id;

        var vispera = CuentasDePrueba.Hoy.AddDays(DiasDeInactividad - 1);
        Dia(vispera);
        (await Subpaso().ProcesarAsync(Corrida, vispera, [id], default)).Should().BeEmpty();
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Active, "la víspera del plazo sigue activa");

        await InactivaAsync(dto);
        var cuenta = await CuentaAsync(dto);
        cuenta.Status.Should().Be(MemberAccountStatus.Inactive);
        cuenta.InactiveSince.Should().Be(CuentasDePrueba.Hoy.AddDays(DiasDeInactividad));
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaInactividad)).Should().Be(1, "el capital pasa al de captación inactiva");
        (await _a.HechosAsync()).Should().Contain(CaptacionReclasificadaV1.Type);

        // Correr otra vez el mismo día no hace nada: ya está inactiva.
        (await Subpaso().ProcesarAsync(Corrida, _a.G.HoyLocal, [id], default)).Should().BeEmpty();
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaInactividad)).Should().Be(1);
    }

    [Fact]
    public void US14_1_el_abono_de_intereses_los_cargos_y_la_asamblea_no_son_movimientos_del_titular()
    {
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.InterestCredited).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.InterestAccrued).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.ChargeApplied).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.AssemblyCredit).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.Deposit).Should().BeTrue();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.Withdrawal).Should().BeTrue();
    }

    [Fact]
    public async Task US14_una_consignacion_del_titular_la_reactiva_y_reclasifica_de_vuelta()
    {
        var dto = await AhorroAsync(1_000_000m);
        await InactivaAsync(dto);
        var id = (await CuentaAsync(dto)).Id;

        var comprobante = await _a.ConsignadaAsync(_a.Consignacion(dto, 50_000m, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));

        comprobante.Reactivated.Should().BeTrue();
        var cuenta = await CuentaAsync(dto);
        cuenta.Status.Should().Be(MemberAccountStatus.Active);
        cuenta.InactiveSince.Should().BeNull();
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaReactivacion)).Should().Be(1,
            "la consignación se contabilizó con la cuenta inactiva y la reactivación lleva todo el saldo al capital activo");
    }

    [Fact]
    public void US14_la_inactividad_de_una_cuenta_migrada_cuenta_desde_el_corte()
    {
        var migrada = MemberAccount.Abrir(1, LineNature.Deposit, DepositClass.Demand, 1, 1, HandlingMode.Indistinct, new DateOnly(2015, 1, 1), null,
            DateTime.UtcNow, MemberAccountOrigin.Migration, cargaId: 1);
        migrada.Activar(new DateOnly(2026, 11, 1), DateTime.UtcNow);

        Application.Lending.Deposits.Accrual.Inactividad.DesdeCuando(migrada, new DateOnly(2020, 5, 5)).Should().Be(new DateOnly(2026, 10, 31), "el último movimiento real es anterior al corte");
        Application.Lending.Deposits.Accrual.Inactividad.DesdeCuando(migrada, new DateOnly(2026, 11, 20)).Should().Be(new DateOnly(2026, 11, 20));
    }

    [Fact]
    public async Task US14_la_exencion_del_GMF_que_rige_desde_una_fecha_futura_reclasifica_el_dia_que_rige()
    {
        var dto = await AhorroAsync(1_000_000m);
        var id = (await CuentaAsync(dto)).Id;
        var rige = CuentasDePrueba.Hoy.AddDays(5);
        var marcada = await _a.C.Marcar().Handle(new MarkGmfExemptAccountCommand(dto.PublicId, rige, "Elige su cuenta exenta") { OperationKey = Guid.NewGuid() },
            default);
        marcada.IsSuccess.Should().BeTrue(marcada.IsFailure ? marcada.Error.Message : string.Empty);
        _a.Db.ChangeTracker.Clear();
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaExencionDeGmf)).Should().Be(0, "la marca rige desde una fecha futura");

        Dia(rige);
        var marcas = new MarcasQueRigen(_a.Db, Reclasificacion(), _a.C.Cerrojo);
        (await marcas.ProcesarAsync(Corrida, rige, [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaExencionDeGmf)).Should().Be(1);

        (await marcas.ProcesarAsync(Corrida, rige, [id], default)).Should().BeEmpty();
        (await ReclasificacionesAsync(id, EmisionDeCartera.CausaExencionDeGmf)).Should().Be(1, "no se repite el mismo día");
    }

    // -------------------------------------------------------------------------------------------------- no reclamados --

    [Fact]
    public async Task US14_sin_la_regla_habilitada_una_inactiva_no_pasa_a_no_reclamados()
    {
        var dto = await AhorroAsync(500_000m);
        await InactivaAsync(dto);

        var r = await new TransferToUnclaimedCommandHandler(_a.Db, NoReclamados(), _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, _a.C.Contexto, _a.G.Reloj)
            .Handle(new TransferToUnclaimedCommand([dto.PublicId], null, "Reglamento de ahorro, art. 30") { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeNoReclamados.CodigoReglaNoHabilitada);
    }

    [Fact]
    public async Task US14_la_inactiva_pasa_a_no_reclamados_y_se_paga_al_titular_y_un_giro_devuelto_la_retiene_otra_vez()
    {
        Parametro(ParametrosDeCartera.CuentasAbandonadasTrasladoHabilitado, "true");
        Parametro(ParametrosDeCartera.CuentasAbandonadasDiasSinMovimiento, "60");
        var dto = await AhorroAsync(500_000m);
        await InactivaAsync(dto);
        var id = (await CuentaAsync(dto)).Id;

        // A los 59 días sin movimiento todavía no.
        var antes = CuentasDePrueba.Hoy.AddDays(59);
        Dia(antes);
        var ciclo = new NoReclamadosDelCiclo(_a.Db, NoReclamados(), _a.C.Cerrojo, _a.C.Contexto);
        (await ciclo.ProcesarAsync(Corrida, antes, [id], default)).Should().BeEmpty();
        (await _a.Db.UnclaimedBalances.AsNoTracking().AnyAsync()).Should().BeFalse();

        var d = CuentasDePrueba.Hoy.AddDays(60);
        Dia(d);
        (await ciclo.ProcesarAsync(Corrida, d, [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();

        var saldo = await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync();
        saldo.Source.Should().Be(UnclaimedSource.InactiveBalance);
        saldo.Status.Should().Be(UnclaimedStatus.Held);
        saldo.Amount.Should().BeGreaterThanOrEqualTo(500_000m, "lo consignado más los intereses liquidados a la fecha");
        var cuenta = await CuentaAsync(dto);
        cuenta.Status.Should().Be(MemberAccountStatus.Closed);
        var saldos = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        saldos.GetValueOrDefault(MemberAccountConcept.Principal).Should().Be(0m);
        saldos.GetValueOrDefault(MemberAccountConcept.Unclaimed).Should().Be(saldo.Amount);
        (await _a.HechosAsync()).Should().Contain(SaldoTrasladadoANoReclamadosV1.Type);

        // Aparece el titular: se le paga por orden de giro sobre la cuenta cancelada.
        var pago = await new PayUnclaimedBalanceCommandHandler(_a.Db, NoReclamados(), _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo)
            .Handle(new PayUnclaimedBalanceCommand(saldo.PublicId, FormaDePagoDeNoReclamado.DisbursementOrder, null, null, null, null, "El titular se presentó")
            {
                OperationKey = Guid.NewGuid(),
            }, default);
        pago.IsSuccess.Should().BeTrue(pago.IsFailure ? $"{pago.Error.Code}: {pago.Error.Message}" : string.Empty);
        pago.Value.Status.Should().Be(UnclaimedStatus.Claimed);
        _a.Db.ChangeTracker.Clear();
        var orden = await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Purpose == PayoutPurpose.UnclaimedPayment);
        orden.Amount.Should().Be(saldo.Amount);

        // Tesorería rechaza el giro: el reverso devuelve el saldo a no reclamados y queda retenido otra vez.
        (await Ordenes().AplicarResultadoAsync(orden, Domain.Enums.Lending.DisbursementOrderStatus.Rejected, "R-1", d, "Cuenta errada", default)).IsSuccess
            .Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.UnclaimedBalances.AsNoTracking().SingleAsync()).Status.Should().Be(UnclaimedStatus.Held);
        (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id].GetValueOrDefault(MemberAccountConcept.Unclaimed).Should().Be(saldo.Amount);
    }

    // --------------------------------------------------------------------------------------------------- fallecimiento --

    private Guid Soporte(Guid persona, string nombre = "registro-civil.pdf")
    {
        var adjunto = new Domain.Entities.Core.Attachment
        {
            TenantId = 1, OwnerEntityType = AdjuntosDeModulo.SoporteDeFallecimiento, OwnerEntityPublicId = persona, FileName = nombre,
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x/" + Guid.NewGuid().ToString("N"),
        };
        _a.Db.Attachments.Add(adjunto);
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
        return adjunto.PublicId;
    }

    private async Task<DeceasedHolderCaseDto> RegistradoAsync()
    {
        _a.G.ActuarComo(_a.C.Asesora);
        var r = await new RegisterDeceasedHolderCommandHandler(_a.Db, Vista(), _a.C.Contexto, _a.G.Alcance, _a.G.Auditoria, _a.Numerador(), _a.C.Cerrojo,
                _a.G.Actor, _a.G.Reloj)
            .Handle(new RegisterDeceasedHolderCommand(_a.C.Ana.PublicId, CuentasDePrueba.Hoy, Soporte(_a.C.Ana.PublicId), "Informa la hija") { OperationKey = Guid.NewGuid() },
                default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<DeceasedHolderCaseDto> EntregaRegistradaAsync(DeceasedHolderCaseDto caso, DeliveryRoute ruta, IReadOnlyList<DeliveryRecipientInput>? destinatarios,
        IReadOnlyList<Guid>? soportes = null)
    {
        var previa = await new PreviewDeceasedDeliveryQueryHandler(Vista(), Fallecimientos(), _a.G.Reloj)
            .Handle(new PreviewDeceasedDeliveryQuery(caso.PublicId, ruta, destinatarios), default);
        previa.IsSuccess.Should().BeTrue(previa.IsFailure ? $"{previa.Error.Code}: {previa.Error.Message}" : string.Empty);
        var r = await new RegisterDeceasedDeliveryCommandHandler(_a.Db, Vista(), Fallecimientos(), _a.G.Intervenciones(), _a.G.Actor, _a.G.Reloj)
            .Handle(new RegisterDeceasedDeliveryCommand(caso.PublicId, ruta, destinatarios, soportes, previa.Value.Sha256, "Entrega a los beneficiarios")
            {
                OperationKey = Guid.NewGuid(),
            }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private ApproveDeceasedDeliveryCommandHandler Aprobar() => new(_a.Db, Vista(), Fallecimientos(), _a.G.Intervenciones(), _a.C.Cerrojo, _a.G.Reloj);

    [Fact]
    public async Task US14_2_fallecido_con_dos_beneficiarios_al_50_genera_dos_ordenes_y_la_cuenta_se_cancela_al_pagarse_ambas()
    {
        var dto = await AhorroAsync(1_000_000m, [new(_a.C.Carla.PublicId, 50m), new(_a.C.Dario.PublicId, 50m)]);
        var id = (await CuentaAsync(dto)).Id;

        var caso = await RegistradoAsync();
        caso.Status.Should().Be(DeceasedCaseStatus.Registered);
        (await _a.Db.AccountHolds.AsNoTracking().SingleAsync(h => h.MemberAccountId == id && h.Reason == HoldReason.Deceased)).Amount
            .Should().BeNull("el fallecimiento bloquea la cuenta entera");
        caso.Beneficiaries.Should().HaveCount(2);

        caso = await EntregaRegistradaAsync(caso, DeliveryRoute.Beneficiaries, null);
        caso.Status.Should().Be(DeceasedCaseStatus.DeliveryPending);

        // Quien registró la entrega no la aprueba (FR-083).
        var propia = await Aprobar().Handle(new ApproveDeceasedDeliveryCommand(caso.PublicId, "Aprobada") { OperationKey = Guid.NewGuid() }, default);
        propia.IsFailure.Should().BeTrue();
        propia.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);
        _a.Db.ChangeTracker.Clear();

        _a.G.ActuarComo(_a.C.Jefe);
        var aprobada = await Aprobar().Handle(new ApproveDeceasedDeliveryCommand(caso.PublicId, "Aprobada por el jefe") { OperationKey = Guid.NewGuid() }, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        aprobada.Value.Status.Should().Be(DeceasedCaseStatus.Approved);
        _a.Db.ChangeTracker.Clear();

        var ordenes = await _a.Db.AccountPayoutOrders.Where(o => o.Purpose == PayoutPurpose.DeceasedDelivery).OrderBy(o => o.Id).ToListAsync();
        ordenes.Should().HaveCount(2, "una orden de giro por beneficiario");
        ordenes.Select(o => o.Amount).Should().AllBeEquivalentTo(500_000m);
        ordenes.Select(o => o.BeneficiaryPersonId).Should().BeEquivalentTo([_a.C.Carla.Id, _a.C.Dario.Id]);
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closing);

        // Tesorería gira la primera: la cuenta sigue en cierre; gira la segunda: se cancela y el caso queda entregado.
        (await Ordenes().AplicarResultadoAsync(ordenes[0], Domain.Enums.Lending.DisbursementOrderStatus.Disbursed, "G-1", _a.G.HoyLocal, null, default)).IsSuccess
            .Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closing);

        var segunda = await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Id == ordenes[1].Id);
        (await Ordenes().AplicarResultadoAsync(segunda, Domain.Enums.Lending.DisbursementOrderStatus.Disbursed, "G-2", _a.G.HoyLocal, null, default)).IsSuccess
            .Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await CuentaAsync(dto)).Status.Should().Be(MemberAccountStatus.Closed);
        (await _a.Db.DeceasedHolderCases.AsNoTracking().SingleAsync()).Status.Should().Be(DeceasedCaseStatus.Delivered);
        (await _a.HechosAsync()).Should().Contain(DevolucionOrdenadaV1.Type);
    }

    [Fact]
    public async Task US14_el_fallecimiento_se_registra_una_vez_y_sin_beneficiarios_la_entrega_por_beneficiarios_se_rechaza()
    {
        await AhorroAsync(1_000_000m);
        var caso = await RegistradoAsync();

        var otra = await new RegisterDeceasedHolderCommandHandler(_a.Db, Vista(), _a.C.Contexto, _a.G.Alcance, _a.G.Auditoria, _a.Numerador(), _a.C.Cerrojo,
                _a.G.Actor, _a.G.Reloj)
            .Handle(new RegisterDeceasedHolderCommand(_a.C.Ana.PublicId, CuentasDePrueba.Hoy, Soporte(_a.C.Ana.PublicId), "Otra vez") { OperationKey = Guid.NewGuid() },
                default);
        otra.Error.Code.Should().Be(ErroresDeFallecimiento.CodigoYaRegistrado);

        var previa = await new PreviewDeceasedDeliveryQueryHandler(Vista(), Fallecimientos(), _a.G.Reloj)
            .Handle(new PreviewDeceasedDeliveryQuery(caso.PublicId, DeliveryRoute.Beneficiaries, null), default);
        previa.Error.Code.Should().Be(ErroresDeFallecimiento.CodigoSinBeneficiarios);
    }

    [Fact]
    public async Task US14_sin_juicio_de_sucesion_no_supera_el_tope_vigente()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.TopeEntregaSinSucesion, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.TopeEntregaSinSucesion, string.Empty, 800_000m,
                "Pesos", null, new DateOnly(2026, 1, 1), null, null, "EOSF art. 127 (prueba)", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1),
                EstadoDeRevision.Validada)));
        await AhorroAsync(1_000_000m);
        var caso = await RegistradoAsync();

        var previa = await new PreviewDeceasedDeliveryQueryHandler(Vista(), Fallecimientos(), _a.G.Reloj)
            .Handle(new PreviewDeceasedDeliveryQuery(caso.PublicId, DeliveryRoute.WithoutProbate,
                [new DeliveryRecipientInput(_a.C.Beto.PublicId, 1m, FormaDeEntrega.DisbursementOrder, null, null, null, null)]), default);

        previa.Error.Code.Should().Be(ErroresDeFallecimiento.CodigoTopeSuperado);
    }

    [Fact]
    public void El_reparto_por_porcentajes_suma_exactamente_el_saldo()
    {
        var r = Application.Lending.Deposits.Deceased.Fallecimientos.Repartir(1, 100_000.01m, [(1, 1m / 3m), (2, 1m / 3m), (3, 1m / 3m)]);
        r.Sum(p => p.Valor).Should().Be(100_000.01m);
        r.Select(p => p.Valor).Should().OnlyContain(v => v == 33_333.33m || v == 33_333.34m || v == 33_333.35m);
    }
}
