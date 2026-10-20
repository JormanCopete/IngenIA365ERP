using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Accrual;

/// <summary>
/// Feature 014, US5 (T131–T136, T138; FR-039, FR-060 a FR-065, SC-003, US5-1 a US5-5): la causación diaria por diferencia de acumulados en el paso de
/// captaciones, la liquidación mensual idempotente con la retención antes de capitalizar y sus acumulados por titular, el ajuste por fecha valor enlazado a
/// la operación, la simulación sin efectos, la suspensión por fallecimiento, el aviso de la causación atrasada y la cancelación de la cuenta. El escenario es
/// el mes de la investigación §3.3 llevado a diciembre de 2026 (31 días): 3 % EA real/365, 2.000.000 del 1 al 10, 5.000.000 del 11 al 20 y 1.500.000 del 21
/// al 31; la UVT de 2026 (52.374). (nuevo)
/// </summary>
public sealed class InteresesDeCuentaTests : IDisposable
{
    private const decimal Uvt = 52_374m;
    private static readonly DateOnly Apertura = new(2026, 11, 30);
    private static readonly DateOnly FinDeMes = new(2026, 12, 31);

    private readonly AportesDePrueba _a = new();

    public InteresesDeCuentaTests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Valor(CodigosDeValorNacional.Uvt, string.Empty, Uvt)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        Dia(Apertura);
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private static ValorNacionalVigente Valor(string codigo, string clave, decimal valor) => new(Guid.NewGuid(), Guid.NewGuid(), codigo, clave, valor, null, null,
        new DateOnly(2026, 1, 1), null, null, "Fuente de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada);

    private void Dia(DateOnly d)
    {
        _a.G.HoyLocal = d;
        _a.G.UtcNow = d.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
    }

    private static readonly decimal TasaDiaria = EquivalenciaDeTasas.TasaDiaria(0.03m, DayCountConvention.Actual365, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);

    private static DepositLineVersionInput AlTres(DepositInactivityInput? inactividad = null) => CuentasDePrueba.ALaVista() with
    {
        ValidFrom = Apertura,
        Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.03m },
        Inactivity = inactividad ?? new DepositInactivityInput(),
    };

    private async Task<MemberAccountDto> CuentaAsync(decimal deposito = 2_000_000m, DepositInactivityInput? inactividad = null, string codigo = "AHTRES",
        bool exenta = true)
    {
        var linea = await _a.C.LineaDeCaptacionAsync(codigo, AlTres(inactividad));
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana, exenta: exenta));
        if (deposito > 0m) await _a.ConsignadaAsync(Libre(cuenta, deposito));
        return cuenta;
    }

    private ConsignacionAAplicar Libre(MemberAccountDto cuenta, decimal valor, DateOnly? fechaValor = null) =>
        _a.Consignacion(cuenta, valor, ModalidadDeConsignacion.Free, fechaValor: fechaValor, medio: ClaseDeMedioDeRecaudo.Transfer);

    private async Task RetirarAsync(MemberAccountDto cuenta, decimal valor)
    {
        var r = await _a.Contrato().RetirarAsync(new SolicitudDeRetiro(AportesDePrueba.Prueba, AportesDePrueba.Clave(), cuenta.PublicId, valor, _a.G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId], cuenta.Holder.PersonPublicId,
            new ReferenciaDeRecibo("RC-9", _a.G.HoyLocal, null, null, "Recibo")), default);
        _a.Db.ChangeTracker.Clear();
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
    }

    /// <summary>El mes de la investigación: 2.000.000 desde la apertura, +3.000.000 el 11 y −3.500.000 el 21.</summary>
    private async Task<MemberAccountDto> MesDeLaInvestigacionAsync()
    {
        var cuenta = await CuentaAsync();
        Dia(new DateOnly(2026, 12, 11));
        await _a.ConsignadaAsync(Libre(cuenta, 3_000_000m));
        Dia(new DateOnly(2026, 12, 21));
        await RetirarAsync(cuenta, 3_500_000m);
        return cuenta;
    }

    private async Task<int> IdAsync(MemberAccountDto cuenta) =>
        await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();

    private async Task<IReadOnlyList<Application.Lending.Deposits.Accrual.FalloDeCuenta>> CausarAsync(MemberAccountDto cuenta, DateOnly d)
    {
        Dia(d);
        var r = await _a.Causacion().ProcesarAsync(new LendingProcessRun { Id = 1, Kind = LendingProcessKind.DailyCycle }, d, [await IdAsync(cuenta)], default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<IReadOnlyList<Application.Lending.Deposits.Accrual.FalloDeCuenta>> LiquidarPeriodosAsync(MemberAccountDto cuenta, DateOnly d)
    {
        Dia(d);
        var r = await new LiquidacionPeriodicaDeIntereses(_a.Db, _a.Liquidacion(), _a.C.Cerrojo)
            .ProcesarAsync(new LendingProcessRun { Id = 1, Kind = LendingProcessKind.DailyCycle }, d, [await IdAsync(cuenta)], default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<decimal> SaldoAsync(MemberAccountDto cuenta, MemberAccountConcept concepto)
    {
        var id = await IdAsync(cuenta);
        return await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == id && t.Concept == concepto).SumAsync(t => t.Amount);
    }

    // --------------------------------------------------------------------------------------------------- causación y liquidación --

    [Fact]
    public async Task US5_1_la_causacion_diaria_y_la_liquidacion_del_mes_dan_el_interes_de_cada_saldo_diario()
    {
        var cuenta = await MesDeLaInvestigacionAsync();

        (await CausarAsync(cuenta, FinDeMes)).Should().BeEmpty();
        var esperado = InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 11));
        (await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable)).Should().Be(esperado, "lo causado día a día suma el acumulado de la ventana");
        var id = await IdAsync(cuenta);
        (await _a.Db.MemberAccountTransactions.CountAsync(t => t.MemberAccountId == id && t.Kind == MemberAccountTransactionKind.InterestAccrued))
            .Should().Be(31, "una fila de causación por día");
        (await _a.Db.MemberAccountControls.SingleAsync(c => c.MemberAccountId == id)).LastAccruedDate.Should().Be(FinDeMes);
        (await _a.Db.AccountingFactParts.CountAsync(p => p.FactType == InteresDeCaptacionCausadoV1.Type)).Should().Be(31, "una parte CCA por día y sucursal");

        (await LiquidarPeriodosAsync(cuenta, FinDeMes)).Should().BeEmpty();
        var liquidacion = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        liquidacion.PeriodFrom.Should().Be(new DateOnly(2026, 12, 1));
        liquidacion.PeriodTo.Should().Be(FinDeMes);
        liquidacion.AccruedInterest.Should().Be(esperado);
        liquidacion.GrossInterest.Should().Be(Math.Round(esperado, 0, MidpointRounding.AwayFromZero));
        liquidacion.RoundingDifference.Should().Be(liquidacion.GrossInterest - esperado);
        liquidacion.WithholdingAmount.Should().Be(0m, "ningún día alcanza el umbral de 0,055 UVT");
        liquidacion.WithholdingExemptionReason.Should().Be(RetencionSobreRendimientos.MotivoBajoElUmbral);
        (await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable)).Should().Be(0m, "lo causado se abonó entero, con su redondeo");
        (await SaldoAsync(cuenta, MemberAccountConcept.Principal)).Should().Be(1_500_000m + liquidacion.GrossInterest);
        (await _a.Db.MemberAccountControls.SingleAsync(c => c.MemberAccountId == id)).LastSettledThrough.Should().Be(FinDeMes);
        (await _a.HechosAsync()).Should().Contain(InteresDeCaptacionLiquidadoV1.Type).And.NotContain(RetencionPracticadaV1.Type);
        (await _a.Db.WithholdingAccumulations.AsNoTracking().SumAsync(w => w.GrossInterest)).Should().Be(liquidacion.GrossInterest);
    }

    [Fact]
    public async Task US5_3_liquidar_otra_vez_el_mismo_periodo_responde_la_existente()
    {
        var cuenta = await MesDeLaInvestigacionAsync();
        await CausarAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);

        (await _a.Db.InterestSettlements.CountAsync()).Should().Be(1);
        var manual = await new SettleAccountInterestCommandHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, _a.Liquidacion(), _a.LectorDeInteres(), _a.G.Reloj)
            .Handle(new SettleAccountInterestCommand(cuenta.PublicId, FinDeMes, "Liquidación manual") { OperationKey = Guid.NewGuid() }, default);
        manual.IsSuccess.Should().BeTrue(manual.IsFailure ? manual.Error.Message : string.Empty);
        manual.Value.AlreadyExisted.Should().BeTrue("SC-003: idempotente por cuenta y período");
        (await _a.Db.InterestSettlements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Un_periodo_no_vencido_o_sin_causar_no_se_liquida_a_mano()
    {
        var cuenta = await MesDeLaInvestigacionAsync();
        var manejador = new SettleAccountInterestCommandHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, _a.Liquidacion(), _a.LectorDeInteres(), _a.G.Reloj);

        var abierto = await manejador.Handle(new SettleAccountInterestCommand(cuenta.PublicId, new DateOnly(2026, 12, 15), "No es fin de período")
            { OperationKey = Guid.NewGuid() }, default);
        abierto.Error.Code.Should().Be(LiquidacionDeIntereses.CodigoPeriodoAbierto);

        Dia(new DateOnly(2027, 1, 2));
        var sinCausar = await manejador.Handle(new SettleAccountInterestCommand(cuenta.PublicId, FinDeMes, "Sin causar") { OperationKey = Guid.NewGuid() }, default);
        sinCausar.Error.Code.Should().Be(LiquidacionDeIntereses.CodigoCausacionAtrasada);
    }

    [Fact]
    public async Task US5_4_con_un_dia_sobre_el_umbral_se_retiene_sobre_el_total_antes_de_capitalizar()
    {
        var cuenta = await CuentaAsync(400_000_000m);
        await CausarAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);

        var l = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        var bruto = Math.Round(InteresDeCaptacion.Seis(TasaDiaria * 400_000_000m * 31), 0, MidpointRounding.AwayFromZero);
        l.GrossInterest.Should().Be(bruto);
        l.ThresholdReached.Should().BeTrue();
        l.ThresholdDailyAmount.Should().Be(2_880.57m);
        l.InflationaryComponentAmount.Should().BeNull("sin componente publicado no se descuenta");
        l.WithholdingAmount.Should().Be(Math.Round(bruto * 0.07m, 0, MidpointRounding.AwayFromZero), "7 % sobre el total del período");
        (await SaldoAsync(cuenta, MemberAccountConcept.Withholding)).Should().Be(l.WithholdingAmount);
        (await SaldoAsync(cuenta, MemberAccountConcept.Principal)).Should().Be(400_000_000m + bruto - l.WithholdingAmount);
        var pareja = await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.Kind == MemberAccountTransactionKind.WithholdingApplied).ToListAsync();
        pareja.Select(t => (t.Concept, t.Amount)).Should().BeEquivalentTo(new[]
        {
            (MemberAccountConcept.Principal, -l.WithholdingAmount), (MemberAccountConcept.Withholding, l.WithholdingAmount),
        });
        var acumulado = await _a.Db.WithholdingAccumulations.AsNoTracking().SingleAsync();
        (acumulado.PersonId, acumulado.TaxYear, acumulado.WithholdingAmount, acumulado.Share).Should().Be((_a.C.Ana.Id, (short)2026, l.WithholdingAmount, 1m));
        (await _a.HechosAsync()).Should().Contain([InteresDeCaptacionLiquidadoV1.Type, RetencionPracticadaV1.Type]);
    }

    [Fact]
    public async Task P27_a_la_persona_natural_se_le_descuenta_el_componente_inflacionario_publicado()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.ComponenteInflacionario, "2025", Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Valor(CodigosDeValorNacional.ComponenteInflacionario, "2025", 0.5543m)));
        var cuenta = await CuentaAsync(400_000_000m);
        await CausarAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);

        var l = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        var componente = Math.Round(l.GrossInterest * 0.5543m, 0, MidpointRounding.AwayFromZero);
        l.InflationaryComponentFraction.Should().Be(0.5543m);
        l.InflationaryComponentAmount.Should().Be(componente);
        l.WithholdingBase.Should().Be(l.GrossInterest - componente);
        l.WithholdingAmount.Should().Be(Math.Round((l.GrossInterest - componente) * 0.07m, 0, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task Un_no_contribuyente_no_tiene_retencion()
    {
        var ana = await _a.Db.People.SingleAsync(p => p.Id == _a.C.Ana.Id);
        ana.WithholdingExempt = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        var cuenta = await CuentaAsync(400_000_000m);
        await CausarAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);

        var l = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        (l.WithholdingAmount, l.WithholdingExemptionReason).Should().Be((0m, RetencionSobreRendimientos.MotivoNoContribuyente));
    }

    // --------------------------------------------------------------------------------------------------------- fecha valor --

    [Fact]
    public async Task FR_062_una_consignacion_fechada_atras_ajusta_la_causacion_en_su_misma_operacion()
    {
        _a.ConAjusteDeCausacion = true;
        var cuenta = await MesDeLaInvestigacionAsync();
        await CausarAsync(cuenta, new DateOnly(2026, 12, 21));
        var antes = await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable);

        Dia(new DateOnly(2026, 12, 22));
        await _a.ConsignadaAsync(Libre(cuenta, 1_000_000m, new DateOnly(2026, 12, 18)));

        var ajuste = await _a.Db.MemberAccountTransactions.AsNoTracking().SingleAsync(t => t.Kind == MemberAccountTransactionKind.AccrualAdjustment);
        ajuste.OperationId.Should().NotBeNull("el ajuste va enlazado a la operación que lo provocó");
        ajuste.ValueDate.Should().Be(new DateOnly(2026, 12, 21), "fechado el último día causado");
        var recalculado = InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 1 + 1_000_000m * 4));
        (antes + ajuste.Amount).Should().Be(recalculado);
        (await _a.HechosAsync()).Should().Contain(AjusteDeCausacionDeCaptacionV1.Type);

        await CausarAsync(cuenta, FinDeMes);
        await LiquidarPeriodosAsync(cuenta, FinDeMes);
        var l = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        l.AccruedInterest.Should().Be(InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 11 + 1_000_000m * 14)),
            "después del ajuste la causación sigue con el saldo nuevo y la liquidación da lo de cada saldo diario");
        (await _a.Db.MemberAccountTransactions.CountAsync(t => t.Kind == MemberAccountTransactionKind.AccrualAdjustment && t.ProcessRunId != null))
            .Should().Be(0, "el proceso no tuvo nada que corregir");
    }

    [Fact]
    public async Task Sin_ajuste_enlazado_el_proceso_corrige_la_diferencia_antes_de_causar()
    {
        var cuenta = await MesDeLaInvestigacionAsync();
        await CausarAsync(cuenta, new DateOnly(2026, 12, 21));
        Dia(new DateOnly(2026, 12, 22));
        await _a.ConsignadaAsync(Libre(cuenta, 1_000_000m, new DateOnly(2026, 12, 18)));

        await CausarAsync(cuenta, FinDeMes);
        var correccion = await _a.Db.MemberAccountTransactions.AsNoTracking().SingleAsync(t => t.Kind == MemberAccountTransactionKind.AccrualAdjustment);
        correccion.ProcessRunId.Should().NotBeNull();
        (await _a.Db.AccountingFactParts.CountAsync(p => p.FactType == AjusteDeCausacionDeCaptacionV1.Type)).Should().Be(1);
        (await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable))
            .Should().Be(InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 11 + 1_000_000m * 14)));
    }

    // ---------------------------------------------------------------------------------------------------------- simulación --

    [Fact]
    public async Task FR_064_la_simulacion_a_una_fecha_futura_no_escribe_y_da_lo_que_se_liquidaria()
    {
        var cuenta = await MesDeLaInvestigacionAsync();
        await CausarAsync(cuenta, new DateOnly(2026, 12, 21));
        var filas = await _a.Db.MemberAccountTransactions.CountAsync();
        var mensajes = await _a.Db.IntegrationMessages.CountAsync();

        var r = await new SimulateInterestSettlementQueryHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Liquidacion(), _a.G.Reloj)
            .Handle(new SimulateInterestSettlementQuery(cuenta.PublicId, FinDeMes), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Accrued.Should().Be(InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 11)));
        r.Value.Explanation.Should().HaveCount(31);
        r.Value.Withholding.ExemptionReason.Should().Be(RetencionSobreRendimientos.MotivoBajoElUmbral);
        (await _a.Db.MemberAccountTransactions.CountAsync()).Should().Be(filas);
        (await _a.Db.IntegrationMessages.CountAsync()).Should().Be(mensajes);
    }

    // ---------------------------------------------------------------------------------------------- suspensión y atraso --

    [Fact]
    public async Task FR_065_con_el_titular_fallecido_y_la_linea_que_lo_dice_la_causacion_se_suspende()
    {
        var cuenta = await CuentaAsync(inactividad: new DepositInactivityInput(SuspendsOnDeceased: true));
        var ana = await _a.Db.People.SingleAsync(p => p.Id == _a.C.Ana.Id);
        ana.IsDeceased = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        (await CausarAsync(cuenta, new DateOnly(2026, 12, 10))).Should().BeEmpty();

        (await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable)).Should().Be(0m);
        var guardada = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == cuenta.PublicId);
        (guardada.AccrualSuspendedSince, guardada.AccrualSuspensionReason).Should().Be((new DateOnly(2026, 12, 1), "Deceased"));
        (await _a.Db.MemberAccountControls.SingleAsync()).LastAccruedDate.Should().Be(new DateOnly(2026, 12, 10), "la marca avanza aunque no cause");
    }

    [Fact]
    public async Task US5_5_la_causacion_atrasada_se_pone_al_dia_en_orden_y_avisa()
    {
        var cuenta = await CuentaAsync();
        (await CausarAsync(cuenta, new DateOnly(2026, 12, 5))).Should().BeEmpty();

        var id = await IdAsync(cuenta);
        (await _a.Db.MemberAccountTransactions.Where(t => t.MemberAccountId == id && t.Kind == MemberAccountTransactionKind.InterestAccrued)
            .Select(t => t.ValueDate).ToListAsync()).Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(d => new DateOnly(2026, 12, d)));
        await _a.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.CausacionDeCaptacionesAtrasada),
            Arg.Any<CancellationToken>());
        // Un día más: no está atrasada.
        _a.Alertas.ClearReceivedCalls();
        await CausarAsync(cuenta, new DateOnly(2026, 12, 6));
        await _a.Alertas.DidNotReceive().LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.CausacionDeCaptacionesAtrasada),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------------------------------------- cancelación --

    [Fact]
    public async Task FR_039_la_cancelacion_liquida_a_la_fecha_retiene_y_traslada_todo_el_saldo()
    {
        var destino = await CuentaAsync(deposito: 0m, codigo: "AHDEST", exenta: false);
        var cuenta = await MesDeLaInvestigacionAsync();
        await CausarAsync(cuenta, new DateOnly(2026, 12, 21));
        Dia(new DateOnly(2026, 12, 26));

        var previa = await new PreviewMemberAccountClosingQueryHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, Cancelacion(), _a.G.Reloj)
            .Handle(new PreviewMemberAccountClosingQuery(cuenta.PublicId), default);
        previa.IsSuccess.Should().BeTrue(previa.IsFailure ? previa.Error.Message : string.Empty);
        previa.Value.Blockers.Should().BeEmpty();
        var interes = InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 10 + 5_000_000m * 10 + 1_500_000m * 6));
        previa.Value.Settlement!.Accrued.Should().Be(interes);

        var r = await Cerrar().Handle(new CloseMemberAccountCommand(cuenta.PublicId, null,
            new CloseDeliveryInput(CloseDeliveryKind.TransferToAccount, destino.PublicId, null), [cuenta.Holder.PersonPublicId], "Cancelación pedida")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Closed);
        r.Value.Settlement!.Accrued.Should().Be(interes);
        r.Value.Delivered.Should().Be(1_500_000m + r.Value.Settlement.Net, "entre cuentas de ahorro del mismo titular el traslado no tiene GMF");
        (await SaldoAsync(cuenta, MemberAccountConcept.Principal)).Should().Be(0m);
        (await SaldoAsync(cuenta, MemberAccountConcept.InterestPayable)).Should().Be(0m);
        (await SaldoAsync(destino, MemberAccountConcept.Principal)).Should().Be(r.Value.Delivered);
        (await _a.Db.MemberAccountOperations.CountAsync(o => o.Kind == MemberAccountOperationKind.Cancellation)).Should().Be(1);
        (await _a.Db.AccountingFactParts.CountAsync(p => p.SourceKind == AccountingFactPartSource.AccountCatchUp)).Should().Be(1,
            "la puesta al día de la cancelación va en una parte ALD-");
    }

    [Fact]
    public async Task Una_cuenta_embargada_no_se_cancela_y_dice_por_que()
    {
        var cuenta = await CuentaAsync();
        var id = await IdAsync(cuenta);
        _a.Db.MemberAccounts.Single(a => a.Id == id).IsGarnished = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        var otra = await CuentaAsync(deposito: 0m, codigo: "AHDEST", exenta: false);
        var r = await Cerrar().Handle(new CloseMemberAccountCommand(cuenta.PublicId, null,
            new CloseDeliveryInput(CloseDeliveryKind.TransferToAccount, otra.PublicId, null), [], "Cancelación") { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be(ErroresDeMovimientos.CodigoCierreBloqueado);
        Sessions.GobiernoDePrueba.Datos(r.Error).ToString().Should().Contain("Garnishment");
    }

    // ------------------------------------------------------------------------------------ cargos por cobrar al cancelar (65 B) --

    private async Task<MemberAccountDto> CuentaConCargoPendienteAsync(decimal cargo, decimal deposito)
    {
        var version = AlTres() with
        {
            Minimums = new DepositMinimumsInput(0m, 3_000m, 0m),
            Charges =
            [
                new DepositChargeInput
                {
                    Code = "CUOTAMAN", Name = "Cuota de manejo", Kind = ChargeKind.MaintenanceFee, Trigger = ChargeTrigger.Periodic, Enabled = true,
                    Calculation = ChargeCalculation.FixedAmount, FixedAmount = cargo, Periodicity = RepaymentPeriodicity.Monthly,
                },
            ],
        };
        var linea = await _a.C.LineaDeCaptacionAsync("AHCARGO", version);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana, exenta: true));
        await _a.ConsignadaAsync(Libre(cuenta, deposito));
        var fin = new DateOnly(2026, 12, 31);
        Dia(fin);
        var id = await IdAsync(cuenta);
        await new Application.Lending.Deposits.Charges.CargosPeriodicos(_a.Db, _a.Movimientos(), _a.Cargos(), _a.C.Cerrojo)
            .ProcesarAsync(new LendingProcessRun { Id = 1 }, fin, [id], default);
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.AccountCharges.AsNoTracking().SingleAsync()).Status.Should().Be(AccountChargeStatus.Pending, "el mínimo de la línea impide cobrarlo del capital");
        return cuenta;
    }

    [Fact]
    public async Task R65_la_cancelacion_cobra_los_cargos_pendientes_del_saldo_y_entrega_el_resto()
    {
        var destino = await CuentaAsync(deposito: 0m, codigo: "AHDEST", exenta: false);
        var cuenta = await CuentaConCargoPendienteAsync(cargo: 2_000m, deposito: 4_000m);

        var previa = await new PreviewMemberAccountClosingQueryHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, Cancelacion(), _a.G.Reloj)
            .Handle(new PreviewMemberAccountClosingQuery(cuenta.PublicId), default);
        previa.IsSuccess.Should().BeTrue(previa.IsFailure ? previa.Error.Message : string.Empty);
        previa.Value.Blockers.Should().BeEmpty("el saldo cubre el cargo: ya no impide");
        previa.Value.PendingCharges.Should().Be(2_000m);

        var r = await Cerrar().Handle(new CloseMemberAccountCommand(cuenta.PublicId, null,
            new CloseDeliveryInput(CloseDeliveryKind.TransferToAccount, destino.PublicId, null), [cuenta.Holder.PersonPublicId], "Cancelación pedida")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Closed);
        r.Value.Delivered.Should().Be(4_000m - 2_000m + r.Value.Settlement!.Net, "se entrega lo que queda después de cobrar el cargo");
        var cargoCobrado = await _a.Db.AccountCharges.AsNoTracking().SingleAsync();
        cargoCobrado.Status.Should().Be(AccountChargeStatus.Collected);
        var cobro = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.Kind == MemberAccountOperationKind.ChargeCollection);
        cargoCobrado.CollectedOperationId.Should().Be(cobro.Id);
        cobro.Number.Should().StartWith("CGC-");
        (await SaldoAsync(cuenta, MemberAccountConcept.ChargeReceivable)).Should().Be(0m);
        (await SaldoAsync(cuenta, MemberAccountConcept.Principal)).Should().Be(0m);
        (await _a.HechosAsync()).Should().Contain(CargoPendienteCobradoV1.Type);
    }

    [Fact]
    public async Task R65_el_cargo_que_el_saldo_no_alcanza_a_cubrir_sigue_impidiendo_la_cancelacion()
    {
        var destino = await CuentaAsync(deposito: 0m, codigo: "AHDEST", exenta: false);
        var cuenta = await CuentaConCargoPendienteAsync(cargo: 5_000m, deposito: 4_000m);

        var previa = await new PreviewMemberAccountClosingQueryHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, Cancelacion(), _a.G.Reloj)
            .Handle(new PreviewMemberAccountClosingQuery(cuenta.PublicId), default);
        previa.Value.Blockers.Should().ContainSingle(b => b.Kind == "PendingCharges");

        var r = await Cerrar().Handle(new CloseMemberAccountCommand(cuenta.PublicId, null,
            new CloseDeliveryInput(CloseDeliveryKind.TransferToAccount, destino.PublicId, null), [cuenta.Holder.PersonPublicId], "Cancelación pedida")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeMovimientos.CodigoCierreBloqueado);
        Sessions.GobiernoDePrueba.Datos(r.Error).ToString().Should().Contain("PendingCharges");
    }

    private CancelacionDeCuenta Cancelacion() => new(_a.Db, _a.Movimientos(), _a.Liquidacion(), _a.G.Reloj, cargos: _a.Cargos());

    private CloseMemberAccountCommandHandler Cerrar() => new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.C.Cerrojo, Cancelacion(), _a.Causacion(), _a.Liquidacion(),
        _a.Movimientos(), _a.Retiro(), _a.Traslado(), Ordenes(), _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj,
        cargosDeCuentas: _a.Cargos());

    private OrdenesDeGiroDeCuentas Ordenes() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Reversion(), _a.C.Parametros(), _a.G.Actor,
        _a.G.Reloj);
}
