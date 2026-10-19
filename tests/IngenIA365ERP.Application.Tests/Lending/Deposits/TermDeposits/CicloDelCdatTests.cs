using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.TermDeposits;

/// <summary>
/// Feature 014, US6 (T172, T173, T175, T176, T177; api.md §5; FR-069 a FR-073; US6-2 a US6-5): el ciclo diario del CDAT —seis pagos mensuales de 79.741 con su
/// retención del 4 % y el GMF del abono en la cuenta; la prórroga tras la gracia con su versión y su título nuevos; la prórroga capitalizada neta de retención
/// (10.468.564) con la tasa nueva del tramo; sin prórroga por el máximo, vencido; embargado, ni se paga ni se prorroga—, la instrucción al vencimiento, la
/// renovación, la redención anticipada (no admitida, por reliquidación, por descuento), la cancelación en la gracia con el GMF de 41.952 de la investigación y
/// las vistas. Sobre <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K9 y la UVT de 2026. (nuevo)
/// </summary>
public sealed class CicloDelCdatTests : IDisposable
{
    private const decimal Uvt = 49_799m;
    private readonly AportesDePrueba _a = new();
    private static readonly DateOnly Hoy = CuentasDePrueba.Hoy;

    public CicloDelCdatTests()
    {
        _a.C.Entrega = EntregaDeCartera.K9;
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _a.Cdat = Constitucion;
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private Sessions.GobiernoDePrueba G => _a.G;

    private LectorDeCdat Lector() => new(_a.Db, _a.Calendario, _a.LectorDeInteres());

    private ReglasDeRetencion Retencion() => new(_a.C.Parametros(), G.Valores);

    private NegociacionDeTasa Negociacion() => new(_a.Db, new MatrizVigenteDeAtribuciones(_a.Db), new AtribucionDelDecisor(_a.Db, G.Permisos), G.Reloj);

    private CotizacionDeCdat Cotizacion() => new(_a.Db, Lector(), Negociacion(), Retencion());

    private ConstitucionDeCdat Constitucion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), _a.Retiro(), Lector(),
        _a.C.Cerrojo, G.Actor, G.Reloj);

    private AperturaDeCuentas Apertura() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    private PagoDeInteresesDeCdat Pagos() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), Retencion(), G.Actor, G.Reloj);

    private SalidaDeCdat Salidas() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), Retencion(), Lector(), G.Actor, G.Reloj);

    private ProrrogaDeCdat Prorrogas() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), Retencion(), Lector(), G.Actor, G.Reloj);

    private CicloDelCdat Ciclo() => new(_a.Db, Lector(), Pagos(), Prorrogas(), Salidas(), _a.C.Cerrojo, G.Reloj);

    private CausacionDeCaptaciones Causacion() => new(_a.Db, _a.RegistroDeCuentas(), _a.LectorDeInteres(),
        new PartesDeHechosDeCaptaciones(_a.Db, _a.Emision()), _a.Emision(), _a.FechaContable(), _a.C.Cerrojo, G.Reloj, _a.Alertas, null, null, Lector());

    private static LendingProcessRun Corrida() => new() { Id = 1, Kind = LendingProcessKind.DailyCycle };

    /// <summary>La noche del día <paramref name="d"/>: causa (poniéndose al día) y corre el ciclo del CDAT.</summary>
    private async Task NocheAsync(int cuenta, DateOnly d)
    {
        var causados = await Causacion().ProcesarAsync(Corrida(), d, [cuenta], default);
        causados.Should().BeEmpty(string.Join("; ", causados.Select(f => $"{f.Codigo}: {f.Mensaje}")));
        _a.Db.ChangeTracker.Clear();
        var ciclo = await Ciclo().ProcesarAsync(Corrida(), d, [cuenta], default);
        ciclo.Should().BeEmpty(string.Join("; ", ciclo.Select(f => $"{f.Codigo}: {f.Mensaje}")));
        _a.Db.ChangeTracker.Clear();
    }

    private async Task<TermDepositDto> ConstituidoAsync(LendingLineDto linea, MemberAccountDto ahorro, RepaymentPeriodicity periodicidad = RepaymentPeriodicity.SinglePayment,
        MaturityInstruction instruccion = MaturityInstruction.Rollover, int plazo = 180)
    {
        var r = await new ConstituteTermDepositCommandHandler(_a.Db, _a.C.Contexto, Apertura(), Cotizacion(), Constitucion(), Lector(), G.Intervenciones(),
            _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj).Handle(new ConstituteTermDepositCommand(new ConstituteTermDepositInput
        {
            LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Ana.PublicId, OfficePublicId = _a.C.Oficina.PublicId, Amount = 10_000_000m, TermDays = plazo,
            InterestPeriodicity = periodicidad, Timing = InterestTiming.Arrears, MaturityInstruction = instruccion,
            PaymentDestination = new TermDepositDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId),
            Funding = new TermDepositFundingInput(TermDepositFundingKind.TransferFromAccount, ahorro.PublicId, [_a.C.Ana.PublicId]),
        }) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<LendingLineDto> LineaAsync(TermDepositLineInput? cdat = null, DepositRateInput? tasa = null) =>
        await _a.C.LineaDeCaptacionAsync("CDAT", new DepositLineVersionInput
        {
            ValidFrom = Hoy,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = tasa ?? new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = cdat ?? Cdat(),
        }, DepositClass.TermDeposit);

    private static TermDepositLineInput Cdat() => new()
    {
        MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment, RepaymentPeriodicity.Monthly],
        Timings = [InterestTiming.Arrears],
    };

    private async Task<MemberAccountDto> AhorroAsync(decimal saldo = 20_000_000m)
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        return cuenta;
    }

    private async Task<int> IdAsync(Guid cuenta) => await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta).Select(a => a.Id).SingleAsync();

    private async Task<decimal> SaldoAsync(Guid cuenta, MemberAccountConcept concepto)
    {
        var id = await IdAsync(cuenta);
        return await _a.Db.MemberAccountTransactions.Where(t => t.MemberAccountId == id && t.Concept == concepto).SumAsync(t => t.Amount);
    }

    private async Task<Domain.Entities.Lending.Deposits.Accounts.MemberAccount> CuentaAsync(Guid cuenta) =>
        await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == cuenta);

    // ------------------------------------------------------------------------------------------------------ el ciclo --

    [Fact]
    public async Task US6_2_seis_pagos_mensuales_de_79741_netos_de_retencion_en_la_cuenta_y_la_prorroga_tras_la_gracia()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro, RepaymentPeriodicity.Monthly);
        var id = await IdAsync(cdat.Account.PublicId);
        var plan = await _a.Db.TermDepositScheduleItems.AsNoTracking().OrderBy(p => p.PeriodNumber).ToListAsync();
        plan.Should().HaveCount(6);

        foreach (var p in plan) await NocheAsync(id, p.PaymentDate);

        var pagos = await _a.Db.InterestSettlements.AsNoTracking().OrderBy(s => s.PeriodTo).ToListAsync();
        pagos.Should().HaveCount(6).And.OnlyContain(s => s.GrossInterest == 79_741m && s.WithholdingAmount == 3_190m && s.NetInterest == 76_551m);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.InterestPayable)).Should().Be(0m, "lo causado de cada período es exactamente su interés del plan");
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Withholding)).Should().Be(6 * 3_190m);
        // El abono en la cuenta de ahorro paga el GMF de la redención de CDAT abonada (D32): 4 por mil de 76.551 = 306.
        (await SaldoAsync(ahorro.PublicId, MemberAccountConcept.Principal)).Should().Be(20_000_000m - 10_000_000m - 40_000m + 6 * (76_551m - 306m));
        (await _a.HechosAsync()).Count(h => h == InteresesDeCdatPagadosV1.Type).Should().Be(6);

        // Vence el 30 de mayo de 2027 (domingo); tres días hábiles de gracia sin instrucción: se prorroga por el mismo plazo con la tasa vigente (D17).
        var finDeLaGracia = new DateOnly(2027, 6, 2);
        await NocheAsync(id, finDeLaGracia.AddDays(-1));
        (await _a.Db.MemberAccountTermsVersions.CountAsync()).Should().Be(1, "dentro de la gracia no se prorroga");
        await NocheAsync(id, finDeLaGracia);
        var versiones = await _a.Db.MemberAccountTermsVersions.AsNoTracking().OrderBy(v => v.VersionNumber).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[1].Cause.Should().Be(AccountTermsCause.Rollover);
        versiones[1].RolloverCount.Should().Be(1);
        versiones[1].PreviousVersionId.Should().Be(versiones[0].Id);
        versiones[1].StartDate.Should().Be(finDeLaGracia, "la gracia no causa: la prórroga empieza al terminarla");
        versiones[1].PrincipalAmount.Should().Be(10_000_000m);
        Domain.Entities.Lending.Deposits.Accounts.MemberAccountTermsVersion.NumeroDeTitulo(cdat.Account.Number, versiones[1].VersionNumber).Should().EndWith("-2");
        (await _a.HechosAsync()).Should().Contain(CdatProrrogadoV1.Type);
        (await CuentaAsync(cdat.Account.PublicId)).Status.Should().Be(MemberAccountStatus.Active);
    }

    [Fact]
    public async Task La_prorroga_capitaliza_el_interes_neto_de_retencion_y_toma_la_tasa_nueva_del_tramo()
    {
        var linea = await LineaAsync(Cdat() with { RolloverCapitalizes = true }, new DepositRateInput
        {
            Kind = RateKind.Fixed,
            Tiers =
            [
                new DepositRateTierInput(AmountFrom: 1_000_000m, AmountTo: 10_000_000m, EffectiveAnnual: 0.10m),
                new DepositRateTierInput(AmountFrom: 10_000_000.01m, EffectiveAnnual: 0.11m),
            ],
        });
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var vencimiento = cdat.CurrentTerms!.MaturityDate;

        await NocheAsync(id, vencimiento.AddDays(1));
        (await _a.Db.InterestSettlements.CountAsync()).Should().Be(0, "el último período se capitaliza en la prórroga: no se paga");
        await NocheAsync(id, new DateOnly(2027, 6, 2));

        var nueva = await _a.Db.MemberAccountTermsVersions.AsNoTracking().SingleAsync(v => v.VersionNumber == 2);
        nueva.PrincipalAmount.Should().Be(10_468_564m, "488.088 menos el 4 % (19.524) se capitaliza (FR-063)");
        nueva.TableAnnualRate.Should().Be(0.11m, "la regla de la línea es la tasa vigente de la tabla para el capital nuevo");
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Principal)).Should().Be(10_468_564m);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Withholding)).Should().Be(19_524m);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.InterestPayable)).Should().Be(0m);
        (await _a.Db.InterestSettlements.AsNoTracking().SingleAsync()).Destination.Should().Be(InterestDestination.Capitalize);
    }

    [Fact]
    public async Task Sin_prorroga_por_el_maximo_paga_el_vencimiento_y_queda_vencido()
    {
        // Una prórroga como máximo: la primera se hace y al segundo vencimiento ya no.
        var linea = await LineaAsync(Cdat() with { MaxRollovers = 1 });
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro, plazo: 30);
        var id = await IdAsync(cdat.Account.PublicId);
        for (var d = Hoy.AddDays(1); d <= Hoy.AddDays(75); d = d.AddDays(1)) await NocheAsync(id, d);

        (await _a.Db.MemberAccountTermsVersions.CountAsync()).Should().Be(2);
        (await _a.Db.InterestSettlements.CountAsync()).Should().Be(2, "cada vencimiento paga su período");
        var cuenta = await CuentaAsync(cdat.Account.PublicId);
        cuenta.Status.Should().Be(MemberAccountStatus.Matured, "el tratamiento por defecto es dejar de causar, vencido por pagar");
        cuenta.MaturedOn.Should().NotBeNull();
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Principal)).Should().Be(10_000_000m, "queda a disposición del titular");
    }

    [Fact]
    public async Task US6_5_embargado_no_se_paga_ni_se_prorroga_y_queda_a_disposicion()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var cuenta = await _a.Db.MemberAccounts.SingleAsync(a => a.Id == id);
        cuenta.IsGarnished = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        await NocheAsync(id, new DateOnly(2027, 5, 31));
        await NocheAsync(id, new DateOnly(2027, 6, 2));
        (await _a.Db.InterestSettlements.CountAsync()).Should().Be(0);
        (await CuentaAsync(cdat.Account.PublicId)).Status.Should().Be(MemberAccountStatus.Matured);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.InterestPayable)).Should().Be(488_088m, "lo causado queda a disposición");
    }

    // ------------------------------------------------------------------------------------------- instrucción y renovación --

    private SetTermDepositMaturityInstructionCommandHandler Instruccion() =>
        new(_a.Db, _a.C.Contexto, Lector(), G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    [Fact]
    public async Task La_instruccion_de_cancelar_paga_al_destino_al_terminar_la_gracia_y_pasada_la_gracia_no_cambia()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var r = await Instruccion().Handle(new SetTermDepositMaturityInstructionCommand(cdat.Account.PublicId, MaturityInstruction.CancelAndPay, null,
            "El titular pide el dinero al vencimiento") { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.EffectiveInstruction.Should().Be(MaturityInstruction.CancelAndPay);

        await NocheAsync(id, new DateOnly(2027, 5, 31));
        await NocheAsync(id, new DateOnly(2027, 6, 2));
        (await CuentaAsync(cdat.Account.PublicId)).Status.Should().Be(MemberAccountStatus.Closed);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await _a.HechosAsync()).Should().Contain(CdatCanceladoV1.Type);

        G.HoyLocal = new DateOnly(2027, 6, 3);
        var otro = await ConstituidoAsync(linea, ahorro, plazo: 30);
        G.HoyLocal = new DateOnly(2027, 7, 15);
        var tarde = await Instruccion().Handle(new SetTermDepositMaturityInstructionCommand(otro.Account.PublicId, MaturityInstruction.CancelAndPay, null, "Tarde")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        tarde.Error.Code.Should().Be(ErroresDeVencimientoDeCdat.CodigoGraciaVencida);
    }

    private RenewTermDepositCommandHandler Renovar() => new(_a.Db, _a.C.Contexto, Lector(), Pagos(), Negociacion(), _a.RegistroDeCuentas(), _a.Numerador(),
        _a.Emision(), _a.Movimientos(), _a.Retiro(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    [Fact]
    public async Task La_renovacion_fuera_de_la_ventana_se_rechaza_y_en_la_gracia_paga_lo_pendiente_y_sella_la_version_nueva_con_el_aumento()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var pedido = new RenewTermDepositInput(360, RepaymentPeriodicity.SinglePayment, InterestTiming.Arrears, "Renueva por un año con un millón más",
            new TermDepositAmountChangeInput(TermDepositAmountChangeInput.Aumento, 1_000_000m, ahorro.PublicId, [_a.C.Ana.PublicId]));

        G.HoyLocal = Hoy.AddDays(10);
        var temprano = await Renovar().Handle(new RenewTermDepositCommand(cdat.Account.PublicId, pedido) { OperationKey = Guid.NewGuid() }, default);
        temprano.Error.Code.Should().Be(ErroresDeVencimientoDeCdat.CodigoFueraDeVentana);
        _a.Db.ChangeTracker.Clear();

        await NocheAsync(id, new DateOnly(2027, 5, 30));
        G.HoyLocal = new DateOnly(2027, 5, 31);
        var r = await Renovar().Handle(new RenewTermDepositCommand(cdat.Account.PublicId, pedido) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.CurrentTerms!.Version.Should().Be(2);
        r.Value.CurrentTerms.Amount.Should().Be(11_000_000m);
        r.Value.CurrentTerms.TermDays.Should().Be(360);
        r.Value.CurrentTerms.Cause.Should().Be(AccountTermsCause.Renewal);
        (await _a.Db.InterestSettlements.AsNoTracking().SingleAsync()).GrossInterest.Should().Be(488_088m, "lo pendiente del plan se paga antes de renovar");
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Principal)).Should().Be(11_000_000m);
        (await _a.HechosAsync()).Should().Contain(CdatRenovadoV1.Type);
    }

    // -------------------------------------------------------------------------------------------------------- redención --

    private SimulateTermDepositRedemptionQueryHandler Simular() => new(_a.Db, _a.C.Contexto, Salidas(), Lector(), G.Alcance, G.Auditoria, G.Reloj);

    private RedeemTermDepositCommandHandler Redimir() => new(_a.Db, _a.C.Contexto, Salidas(), Lector(), _a.Movimientos(), _a.C.Cerrojo, G.Alcance, G.Auditoria,
        G.Reloj);

    [Fact]
    public async Task US6_4_una_linea_que_no_admite_redencion_anticipada_la_rechaza()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        G.HoyLocal = Hoy.AddDays(90);
        var r = await Simular().Handle(new SimulateTermDepositRedemptionQuery(cdat.Account.PublicId, Hoy.AddDays(90)), default);
        r.Error.Code.Should().Be(ErroresDeVencimientoDeCdat.CodigoRedencionNoAdmitida);
    }

    [Fact]
    public async Task La_redencion_anticipada_reliquida_a_la_tasa_de_penalidad_y_abona_capital_e_interes_en_la_cuenta()
    {
        var linea = await LineaAsync(Cdat() with { EarlyRedemption = new EarlyRedemptionInput(true, EarlyRedemptionPenaltyMode.PenaltyRate, 0.02m) });
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var dia = Hoy.AddDays(90);
        await NocheAsync(id, dia.AddDays(-1));
        G.HoyLocal = dia;

        var vista = await Simular().Handle(new SimulateTermDepositRedemptionQuery(cdat.Account.PublicId, dia), default);
        vista.IsSuccess.Should().BeTrue(vista.IsFailure ? $"{vista.Error.Code}: {vista.Error.Message}" : string.Empty);
        vista.Value.IsEarly.Should().BeTrue();
        vista.Value.AccruedInterest.Should().Be(241_136.890844m, "lo causado a los 90 días, también el día de hoy que el proceso no ha causado (caso dorado cdat-06)");
        vista.Value.Penalty.RepricedInterest.Should().Be(49_629m);
        vista.Value.Interest.Should().Be(49_629m);
        vista.Value.Withholding.Should().Be(1_985m);

        var r = await Redimir().Handle(new RedeemTermDepositCommand(cdat.Account.PublicId,
            new TermDepositRedemptionDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId), vista.Value.Fingerprint, "El titular necesita el dinero")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be("Applied");
        r.Value.Delivered.Should().Be(10_000_000m + 49_629m - 1_985m);
        var cuenta = await CuentaAsync(cdat.Account.PublicId);
        cuenta.Status.Should().Be(MemberAccountStatus.Closed);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await SaldoAsync(cdat.Account.PublicId, MemberAccountConcept.InterestPayable)).Should().Be(0m);
        (await _a.HechosAsync()).Should().Contain([CdatRedimidoV1.Type, AjusteDeCausacionDeCaptacionV1.Type, RetencionPracticadaV1.Type, GmfRetenidoV1.Type]);
    }

    [Fact]
    public async Task La_redencion_con_descuento_cobra_la_fraccion_de_lo_causado_como_penalidad()
    {
        var linea = await LineaAsync(Cdat() with { EarlyRedemption = new EarlyRedemptionInput(true, EarlyRedemptionPenaltyMode.Discount, PenaltyDiscount: 0.30m) });
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        var dia = Hoy.AddDays(90);
        await NocheAsync(id, dia);
        G.HoyLocal = dia;
        var vista = await Simular().Handle(new SimulateTermDepositRedemptionQuery(cdat.Account.PublicId, dia), default);
        vista.IsSuccess.Should().BeTrue(vista.IsFailure ? $"{vista.Error.Code}: {vista.Error.Message}" : string.Empty);
        vista.Value.Penalty.Amount.Should().Be(72_341m);
        vista.Value.Interest.Should().Be(168_796m);
    }

    [Fact]
    public async Task La_cancelacion_en_la_gracia_abonada_en_la_cuenta_paga_el_GMF_de_la_investigacion()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        // Sin retención (no contribuyente) el abono es 10.488.088 y su GMF, 41.952 (investigación §3.7, gmf-02).
        var ana = await _a.Db.People.SingleAsync(p => p.Id == _a.C.Ana.Id);
        ana.WithholdingExempt = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        var cdat = await ConstituidoAsync(linea, ahorro);
        var id = await IdAsync(cdat.Account.PublicId);
        await NocheAsync(id, new DateOnly(2027, 5, 30));
        var antes = await SaldoAsync(ahorro.PublicId, MemberAccountConcept.Principal);
        G.HoyLocal = new DateOnly(2027, 5, 31);

        var r = await Redimir().Handle(new RedeemTermDepositCommand(cdat.Account.PublicId,
            new TermDepositRedemptionDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId), null, "Cancelación en la gracia")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Delivered.Should().Be(10_488_088m);
        (await SaldoAsync(ahorro.PublicId, MemberAccountConcept.Gmf)).Should().Be(40_000m + 41_952m, "el fondeo y la cancelación, cada uno gravado (D32)");
        (await SaldoAsync(ahorro.PublicId, MemberAccountConcept.Principal)).Should().Be(antes + 10_488_088m - 41_952m);
        (await _a.HechosAsync()).Should().Contain(CdatCanceladoV1.Type);
    }

    // ------------------------------------------------------------------------------------------------------------ vistas --

    [Fact]
    public async Task Las_vistas_muestran_el_vencimiento_y_la_concentracion()
    {
        var linea = await LineaAsync();
        var ahorro = await AhorroAsync();
        var cdat = await ConstituidoAsync(linea, ahorro);
        G.Conceder(["Lending.Accounts.Read"]);
        var vencimientos = await new TermDepositMaturitiesReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new TermDepositMaturitiesReportQuery(new FiltrosDeInformeDeCartera { From = Hoy, To = Hoy.AddYears(1) }), default);
        vencimientos.IsSuccess.Should().BeTrue(vencimientos.IsFailure ? $"{vencimientos.Error.Code}: {vencimientos.Error.Message}" : string.Empty);
        vencimientos.Value.Filas.Should().ContainSingle().Which.Valores[1].Should().Be(cdat.Account.Number);

        var concentracion = await new DepositorConcentrationReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new DepositorConcentrationReportQuery(new FiltrosDeInformeDeCartera()), default);
        concentracion.IsSuccess.Should().BeTrue();
        concentracion.Value.Filas.Should().ContainSingle().Which.Valores[2].Should().Be(20_000_000m - 40_000m, "el ahorro y el CDAT de la misma persona");

        var negociadas = await new NegotiatedRatesReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new NegotiatedRatesReportQuery(new FiltrosDeInformeDeCartera { From = Hoy, To = Hoy }), default);
        negociadas.IsSuccess.Should().BeTrue();
        negociadas.Value.Filas.Should().BeEmpty("este CDAT no negoció tasa");
    }
}
