using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Loans.TermChange;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Loans;

/// <summary>
/// 2026-11-24 (pedido del dueño: «una opción que permita cambiar el plazo (las cuotas) y que esto recalcule el valor de la cuota y las cuotas en los
/// parámetros, siempre y cuando a este crédito no se le hayan realizado pagos y no tenga causación»): sobre el escenario de la causación —el crédito
/// de Ana, 3.000.000 a 24 cuotas al 20 % EA, desembolsado hoy— se simula y se registra el cambio, se acorta y se alarga, queda como versión nueva
/// del plan y de las condiciones (no es novedad), no deja filas en el libro, la reconstrucción y el ciclo diario siguen con el plan nuevo, y se
/// rechaza con causación, con pagos, con otras operaciones, fuera de la línea, con el parámetro apagado o, si la cooperativa lo pide, por quien lo
/// registró.
/// </summary>
public class CambioDePlazoTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private CambioDePlazo Cambio() => new(_c.Db, _c.Cargador(), _c.D.FechaContable(), _c.D.G.Parametros(), _c.D.G.Reloj);

    private SimulateLoanTermChangeQueryHandler Simular() => new(_c.Db, Cambio(), _c.D.G.Alcance, _c.D.G.Auditoria);

    private ChangeLoanTermCommandHandler Cambiar() => new(_c.Db, Cambio(), _c.Cargador(), _c.D.Registro(), _c.D.Numerador(), _c.D.O.Cerrojo,
        _c.D.G.Alcance, _c.D.G.Auditoria, _c.D.G.Actor, _c.D.G.Reloj);

    private Task<Result<LoanTermChangeResultDto>> CambiarAsync(int cuotas, DateOnly? primero = null) =>
        Cambiar().Handle(new ChangeLoanTermCommand(_c.Credito.PublicId, cuotas, primero, "La venta se registró con las cuotas equivocadas")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

    private async Task ParametroAsync(string clave, string valor)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = Hoy.AddDays(-1), Reason = "prueba",
        });
        await _c.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task La_simulacion_muestra_el_plan_nuevo_con_la_cuota_nueva_y_no_escribe()
    {
        var operaciones = await _c.Db.LoanOperations.CountAsync();

        var r = await Simular().Handle(new SimulateLoanTermChangeQuery(_c.Credito.PublicId, 12), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.CurrentInstallments.Should().Be(24);
        r.Value.NewInstallments.Should().Be(12);
        r.Value.Schedule.Should().HaveCount(12);
        r.Value.Schedule.Sum(q => q.Principal).Should().Be(3_000_000m);
        r.Value.NewInstallmentAmount.Should().BeGreaterThan(r.Value.CurrentInstallmentAmount, "a menos cuotas, cuota más alta");
        r.Value.NewFirstDueDate.Should().Be(r.Value.CurrentFirstDueDate, "sin primer vencimiento se conserva el de hoy");
        r.Value.ValueDate.Should().Be(Hoy);
        (await _c.Db.LoanOperations.CountAsync()).Should().Be(operaciones);
    }

    [Fact]
    public async Task Acortar_y_alargar_dejan_versiones_nuevas_sin_filas_en_el_libro_y_la_reconstruccion_las_sigue()
    {
        var filas = await _c.Db.LoanTransactions.CountAsync();

        var corto = await CambiarAsync(12);

        corto.IsSuccess.Should().BeTrue(corto.IsFailure ? corto.Error.Message : null);
        corto.Value.InstallmentsBefore.Should().Be(24);
        corto.Value.InstallmentsAfter.Should().Be(12);
        corto.Value.InstallmentAmountAfter.Should().BeGreaterThan(corto.Value.InstallmentAmountBefore);
        corto.Value.ScheduleVersion.Should().Be(2);
        corto.Value.OperationNumber.Should().StartWith(CambioDePlazo.PrefijoDeOperacion);

        var largo = await CambiarAsync(36);

        largo.IsSuccess.Should().BeTrue(largo.IsFailure ? largo.Error.Message : null);
        largo.Value.InstallmentsBefore.Should().Be(12, "un cambio de plazo anterior no impide otro");
        largo.Value.InstallmentsAfter.Should().Be(36);
        largo.Value.InstallmentAmountAfter.Should().BeLessThan(corto.Value.InstallmentAmountAfter);
        largo.Value.ScheduleVersion.Should().Be(3);

        (await _c.Db.LoanTransactions.CountAsync()).Should().Be(filas, "cambiar el plazo no mueve plata: no deja filas en el libro");
        var condiciones = await _c.Db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == _c.Credito.Id).OrderBy(t => t.VersionNumber).ToListAsync();
        condiciones.Select(t => (t.VersionNumber, t.Installments, t.Cause))
            .Should().Equal((1, 24, condiciones[0].Cause), (2, 12, TermsVersionCause.TermCorrection), (3, 36, TermsVersionCause.TermCorrection));
        condiciones.Should().OnlyContain(t => t.NoveltyId == null && t.RestructuringCount == 0, "no es novedad ni reestructuración");
        var operaciones = await _c.Db.LoanOperations.AsNoTracking().Where(o => o.LoanId == _c.Credito.Id && o.Kind == LoanOperationKind.TermsChange)
            .ToListAsync();
        operaciones.Should().HaveCount(2).And.OnlyContain(o => o.NoveltyId == null && o.ValueDate == Hoy && o.Amount == 0m);
        operaciones.Should().OnlyContain(o => ReglasDelCambioDePlazo.EsCambioDePlazo(o));

        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, Hoy, default);
        var plan = MotorDeCartera.Reconstruir(cargado!.Entrada!, Hoy).Plan!;
        var guardadas = await (from v in _c.Db.RepaymentScheduleVersions join i in _c.Db.RepaymentInstallments on v.Id equals i.ScheduleVersionId
                               where v.LoanId == _c.Credito.Id && v.VersionNumber == 3 orderby i.InstallmentNumber select i).ToListAsync();
        plan.Cuotas.Select(q => (q.Numero, q.Vencimiento, q.Capital, q.Interes))
            .Should().Equal(guardadas.Select(i => (i.InstallmentNumber, i.DueDate, i.Principal, i.Interest)), "lo guardado es la reconstrucción");
    }

    [Fact]
    public async Task Tras_el_cambio_el_ciclo_diario_causa_con_el_plan_nuevo_y_el_libro_cuadra()
    {
        (await CambiarAsync(12)).IsSuccess.Should().BeTrue();

        await _c.CausarHastaAsync(Hoy.AddDays(3));

        (await _c.ControlAsync()).LastAccruedDate.Should().Be(Hoy.AddDays(3));
        var integridad = await _c.Verificar().Handle(new IngenIA365ERP.Application.Lending.Ledger.VerifyLendingLedgerIntegrityQuery([_c.Credito.PublicId]), default);
        integridad.IsSuccess.Should().BeTrue(integridad.IsFailure ? integridad.Error.Message : null);
        integridad.Value.Mismatches.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_causacion_registrada_no_se_cambia()
    {
        await _c.CausarHastaAsync(Hoy.AddDays(1));

        var r = await Simular().Handle(new SimulateLoanTermChangeQuery(_c.Credito.PublicId, 12), default);

        r.Error.Code.Should().Be(ErroresDelCambioDePlazo.CodigoConCausacion);
        r.Error.Message.Should().Contain("novedad");
    }

    [Fact]
    public async Task Igual_que_hoy_no_hay_nada_que_cambiar()
    {
        var r = await Simular().Handle(new SimulateLoanTermChangeQuery(_c.Credito.PublicId, 24), default);

        r.Error.Code.Should().Be(ErroresDelCambioDePlazo.CodigoSinCambio);
    }

    [Fact]
    public async Task Fuera_del_rango_de_la_linea_se_rechaza()
    {
        var r = await CambiarAsync(120);

        r.Error.Code.Should().Be(ErroresDelCambioDePlazo.CodigoFueraDeLaLinea);
        (await _c.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.TermsChange)).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_parametro_apagado_no_se_cambia()
    {
        await ParametroAsync(ParametrosDeCartera.CreditosCambioDePlazo, "false");

        var r = await CambiarAsync(12);

        r.Error.Code.Should().Be(ErroresDelCambioDePlazo.CodigoDeshabilitado);
    }

    [Fact]
    public async Task Si_la_cooperativa_lo_pide_quien_registro_el_credito_no_cambia_su_plazo()
    {
        await ParametroAsync(ParametrosDeCartera.SegregacionCambioDePlazoPorOtraPersona, "true");

        var mismo = await CambiarAsync(12);
        _c.D.G.ActuarComo(_c.D.O.Analista);
        var otro = await CambiarAsync(12);

        mismo.Error.Code.Should().Be(ErroresDelCambioDePlazo.CodigoMismaPersona);
        otro.IsSuccess.Should().BeTrue(otro.IsFailure ? otro.Error.Message : null);
    }

    // ----------------------------------------------------------------------------------------------------- reglas puras --

    private static Loan Credito(LoanStatus estado = LoanStatus.Active, LoanOrigin origen = LoanOrigin.Commercial) =>
        new() { Number = "C-1", Status = estado, Origin = origen };

    private static LoanTermsVersion Condiciones(GraceKind? gracia = null, string? extraordinarias = null, DenominationUnit unidad = DenominationUnit.Pesos) =>
        new()
        {
            VersionNumber = 1, Periodicity = RepaymentPeriodicity.Monthly, Installments = 12, GraceKind = gracia, ExtraordinaryInstallments = extraordinarias,
            DenominationUnit = unidad,
        };

    private static LoanOperation Operacion(LoanOperationKind clase, int? novedad = null, string? payload = null) =>
        new() { Kind = clase, NoveltyId = novedad, Payload = payload ?? "{}" };

    private static Error? Evaluar(Loan? credito = null, LoanTermsVersion? condiciones = null, bool causado = false, params LoanOperation[] mas)
    {
        var c = condiciones ?? Condiciones();
        return ReglasDelCambioDePlazo.Evaluar(credito ?? Credito(), c, c, [Operacion(LoanOperationKind.Disbursement), .. mas], causado);
    }

    [Fact]
    public void Un_credito_recien_nacido_de_una_venta_lo_admite()
    {
        Evaluar().Should().BeNull();
        Evaluar(mas: Operacion(LoanOperationKind.PreferenceChange)).Should().BeNull("la preferencia de abono no mueve saldos");
    }

    [Theory]
    [InlineData(LoanOperationKind.Payment)]
    [InlineData(LoanOperationKind.ProvisionalCollection)]
    [InlineData(LoanOperationKind.PaymentReversal)]
    [InlineData(LoanOperationKind.CreditBalanceResolution)]
    public void Con_pagos_o_anticipos_no(LoanOperationKind clase)
    {
        Evaluar(mas: Operacion(clase))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoConPagos);
    }

    [Theory]
    [InlineData(LoanOperationKind.Charge)]
    [InlineData(LoanOperationKind.Waiver)]
    [InlineData(LoanOperationKind.CommercialAdjustment)]
    [InlineData(LoanOperationKind.RateReset)]
    [InlineData(LoanOperationKind.Disbursement)]
    public void Con_otras_operaciones_no(LoanOperationKind clase)
    {
        Evaluar(mas: Operacion(clase))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoConOperaciones);
    }

    [Fact]
    public void Una_novedad_no_es_un_cambio_de_plazo_pero_un_cambio_de_plazo_anterior_si()
    {
        var cambio = new DecisionDeNovedad { Mechanism = nameof(NoveltyMechanism.TermCorrection), Installments = 12 }.Serializar();
        var prorroga = new DecisionDeNovedad { Mechanism = nameof(NoveltyMechanism.Extension), Installments = 12 }.Serializar();

        Evaluar(mas: Operacion(LoanOperationKind.TermsChange, payload: cambio)).Should().BeNull();
        Evaluar(mas: Operacion(LoanOperationKind.TermsChange, novedad: 7, payload: prorroga))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoConOperaciones);
    }

    [Fact]
    public void Con_causacion_migrado_o_no_activo_no()
    {
        Evaluar(causado: true)!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoConCausacion);
        Evaluar(Credito(origen: LoanOrigin.Migration))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoMigrado);
        Evaluar(Credito(LoanStatus.PaidOff))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoNoActivo);
    }

    [Fact]
    public void Con_gracia_extraordinarias_o_en_UVR_no()
    {
        Evaluar(condiciones: Condiciones(gracia: GraceKind.Total))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoCondicionesEspeciales);
        Evaluar(condiciones: Condiciones(extraordinarias: "[{\"installment\":6,\"amount\":100000}]"))!.Code
            .Should().Be(ErroresDelCambioDePlazo.CodigoCondicionesEspeciales);
        Evaluar(condiciones: Condiciones(unidad: DenominationUnit.Uvr))!.Code.Should().Be(ErroresDelCambioDePlazo.CodigoCondicionesEspeciales);
        Evaluar(condiciones: Condiciones(gracia: GraceKind.None, extraordinarias: "[]")).Should().BeNull();
    }

    [Fact]
    public void La_causacion_que_mira_incluye_el_reconocimiento_y_no_el_desembolso()
    {
        ReglasDelCambioDePlazo.ClasesDeCausacion.Should().Contain([LoanTransactionKind.Accrual, LoanTransactionKind.PeriodRecognition])
            .And.NotContain(LoanTransactionKind.Disbursement);
    }
}
