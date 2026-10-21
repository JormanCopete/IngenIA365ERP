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
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.TermDeposits;

/// <summary>
/// Feature 014, US6 (T170, T171, T177; api.md §5, §6; FR-067, FR-127): la constitución del CDAT —la simulación de la investigación (seis pagos de 79.741), el
/// fondeo desde una cuenta de ahorro con su GMF y la versión 1 sellada, el fondeo por el recaudador del contrato con el monto exacto, el plazo fuera de rango,
/// la periodicidad no admitida, la línea sin tasa para el plazo, la entrega K9— y la tasa negociada —se aplica sola dentro de la atribución, espera fuera de
/// ella, se rechaza sobre el máximo de la regla o si la línea no la admite, se aprueba o se niega por otra persona, y no se fondea mientras espera—. Sobre
/// <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K9, la UVT de 2026 y el contrato de cuentas real. (nuevo)
/// </summary>
public sealed class CdatTests : IDisposable
{
    private const decimal Uvt = 49_799m;
    private readonly AportesDePrueba _a = new();

    public CdatTests()
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

    private NegociacionDeTasa Negociacion() => new(_a.Db, new MatrizVigenteDeAtribuciones(_a.Db), new AtribucionDelDecisor(_a.Db, G.Permisos), G.Reloj);

    private CotizacionDeCdat Cotizacion() => new(_a.Db, Lector(), Negociacion(), new ReglasDeRetencion(_a.C.Parametros(), G.Valores));

    private ConstitucionDeCdat Constitucion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), _a.Retiro(), Lector(),
        _a.C.Cerrojo, G.Actor, G.Reloj);

    private AperturaDeCuentas Apertura() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    private async Task<Result<TermDepositDto>> ConstituirAsync(ConstituteTermDepositInput e)
    {
        var r = await new ConstituteTermDepositCommandHandler(_a.Db, _a.C.Contexto, Apertura(), Cotizacion(), Constitucion(), Lector(), G.Intervenciones(),
            _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj).Handle(new ConstituteTermDepositCommand(e) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<NegotiatedRateRequestDto>> DecidirAsync(Guid solicitud, NegotiatedRateDecision decision)
    {
        var r = await new DecideNegotiatedRateCommandHandler(_a.Db, Negociacion(), new AtribucionDelDecisor(_a.Db, G.Permisos), new PuntoDeActaCerrado(_a.Db),
            G.Intervenciones(), G.Alcance, G.Auditoria, G.Reloj).Handle(new DecideNegotiatedRateCommand(solicitud, decision, "Asociado de cinco años") { OperationKey = Guid.NewGuid() },
            default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Una línea de CDAT al 10 % EA en base 360 de 30 a 720 días, al vencimiento o mensual, con o sin tasa negociada.</summary>
    private async Task<LendingLineDto> LineaDeCdatAsync(bool negocia = false, DepositRateInput? tasa = null, string codigo = "CDAT") =>
        await _a.C.LineaDeCaptacionAsync(codigo, new DepositLineVersionInput
        {
            ValidFrom = CuentasDePrueba.Hoy,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = tasa ?? new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            NegotiatedRateAllowed = negocia,
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = new TermDepositLineInput
            {
                MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment, RepaymentPeriodicity.Monthly],
                Timings = [InterestTiming.Arrears, InterestTiming.Advance],
            },
        }, DepositClass.TermDeposit);

    private async Task<MemberAccountDto> AhorroConSaldoAsync(decimal saldo)
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        return cuenta;
    }

    private ConstituteTermDepositInput Pedido(LendingLineDto linea, MemberAccountDto ahorro, decimal monto = 10_000_000m, int plazo = 180,
        RepaymentPeriodicity periodicidad = RepaymentPeriodicity.SinglePayment, TermDepositFundingKind fondeo = TermDepositFundingKind.TransferFromAccount,
        TermDepositNegotiationInput? negociacion = null) => new()
    {
        LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Ana.PublicId, OfficePublicId = _a.C.Oficina.PublicId, Amount = monto, TermDays = plazo,
        InterestPeriodicity = periodicidad, Timing = InterestTiming.Arrears,
        PaymentDestination = new TermDepositDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId),
        Funding = new TermDepositFundingInput(fondeo, fondeo == TermDepositFundingKind.TransferFromAccount ? ahorro.PublicId : null, [_a.C.Ana.PublicId]),
        Negotiation = negociacion,
    };

    private async Task<decimal> CapitalAsync(Guid cuenta)
    {
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta).Select(a => a.Id).SingleAsync();
        return await _a.Db.MemberAccountTransactions.Where(t => t.MemberAccountId == id && t.Concept == MemberAccountConcept.Principal).SumAsync(t => t.Amount);
    }

    private void ConPermisos(params string[] extra) =>
        G.Conceder([PermisosDeLineas.VerCaptacion, PermisosDeLineas.AdministrarCaptacion, PermisosDeLineas.VerAportes, PermisosDeLineas.AdministrarAportes, .. extra]);

    // ------------------------------------------------------------------------------------------------------ constitución --

    [Fact]
    public async Task La_simulacion_da_los_seis_pagos_de_la_investigacion()
    {
        var linea = await LineaDeCdatAsync();
        var r = await new SimulateTermDepositQueryHandler(_a.Db, Cotizacion(), _a.C.Contexto, G.Alcance, G.Auditoria, G.Reloj)
            .Handle(new SimulateTermDepositQuery(linea.PublicId, 10_000_000m, 180, RepaymentPeriodicity.Monthly, InterestTiming.Arrears), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Schedule.Should().HaveCount(6).And.OnlyContain(p => p.Interest == 79_741m);
        r.Value.TotalInterest.Should().Be(478_446m);
        r.Value.TableRate.Should().Be(0.10m);
        r.Value.IssueTermBand.Should().Be(IssueTermBand.SixToTwelve);
        r.Value.WithholdingEstimate.AppliesThreshold.Should().BeFalse("el umbral diario no alcanza al CDAT por defecto (D10)");
        r.Value.Fingerprint.Should().HaveLength(64);
    }

    [Fact]
    public async Task Fondeado_desde_una_cuenta_de_ahorro_nace_activo_con_su_version_y_su_plan_y_el_GMF_del_traslado()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);

        var r = await ConstituirAsync(Pedido(linea, ahorro));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Active);
        r.Value.CurrentTerms!.Version.Should().Be(1);
        r.Value.CurrentTerms.Amount.Should().Be(10_000_000m);
        r.Value.CurrentTerms.MaturityDate.Should().Be(CuentasDePrueba.Hoy.AddDays(180));
        r.Value.CurrentTerms.TitleNumber.Should().EndWith("-1");
        r.Value.Proposal.Should().BeNull();
        (await CapitalAsync(r.Value.Account.PublicId)).Should().Be(10_000_000m);
        // El traslado de ahorro a CDAT está gravado (D32): 4 por mil sobre 10.000.000.
        (await CapitalAsync(ahorro.PublicId)).Should().Be(20_000_000m - 10_000_000m - 40_000m);
        var plan = await _a.Db.TermDepositScheduleItems.ToListAsync();
        plan.Should().ContainSingle().Which.Interest.Should().Be(488_088m);
        (await _a.HechosAsync()).Should().Contain([CdatConstituidoV1.Type, GmfRetenidoV1.Type]);
    }

    [Fact]
    public async Task Fondeado_por_el_recaudador_exige_el_monto_exacto()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(1_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, fondeo: TermDepositFundingKind.Collector));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Opening);
        r.Value.Proposal!.Amount.Should().Be(10_000_000m);
        var cdat = r.Value.Account;

        var corta = await _a.ConsignarAsync(_a.Consignacion(cdat, 9_000_000m, ModalidadDeConsignacion.TermDepositFunding, medio: ClaseDeMedioDeRecaudo.Transfer));
        corta.Result.Should().NotBe(ResultadoDelContrato.Applied);
        corta.Code.Should().Be(ErroresDeCdat.CodigoMontoDistinto);

        var exacta = await _a.ConsignarAsync(_a.Consignacion(cdat, 10_000_000m, ModalidadDeConsignacion.TermDepositFunding, medio: ClaseDeMedioDeRecaudo.Transfer));
        exacta.Result.Should().Be(ResultadoDelContrato.Applied, $"{exacta.Code}: {exacta.Message}");
        (await _a.Db.MemberAccounts.SingleAsync(a => a.PublicId == cdat.PublicId)).Status.Should().Be(MemberAccountStatus.Active);
        (await CapitalAsync(cdat.PublicId)).Should().Be(10_000_000m);

        var otra = await _a.ConsignarAsync(_a.Consignacion(cdat, 1_000m, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        otra.Code.Should().Be(ErroresDeCdat.CodigoSinConsignaciones);
    }

    [Fact]
    public async Task Un_plazo_fuera_del_rango_se_rechaza_con_el_rango_vigente()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(1_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, plazo: 1_000));
        r.Error.Code.Should().Be("Lending.TermDeposit.TermOutOfRange");
        var datos = Sessions.GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("minTermDays").GetInt32().Should().Be(30);
        datos.GetProperty("maxTermDays").GetInt32().Should().Be(720);
    }

    [Fact]
    public async Task Una_periodicidad_que_la_linea_no_admite_se_rechaza()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(1_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, periodicidad: RepaymentPeriodicity.Quarterly));
        r.Error.Code.Should().Be("Lending.TermDeposit.PeriodicityNotAllowed");
    }

    [Fact]
    public async Task Sin_tasa_para_el_plazo_no_se_constituye()
    {
        var linea = await LineaDeCdatAsync(tasa: new DepositRateInput
        {
            Kind = RateKind.Fixed, Tiers = [new DepositRateTierInput(TermDaysFrom: 30, TermDaysTo: 90, EffectiveAnnual: 0.08m)],
        });
        var ahorro = await AhorroConSaldoAsync(1_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, plazo: 180));
        r.Error.Code.Should().Be(ErroresDeCdat.CodigoSinTasa);
    }

    [Fact]
    public async Task Sin_la_entrega_K9_el_CDAT_no_esta_disponible()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(1_000_000m);
        _a.C.Entrega = EntregaDeCartera.K8;
        var r = await ConstituirAsync(Pedido(linea, ahorro));
        r.Error.Code.Should().Be("Lending.Savings.DeliveryNotAvailable");
        Sessions.GobiernoDePrueba.Datos(r.Error).GetProperty("availableFrom").GetString().Should().Be("K9");
    }

    // -------------------------------------------------------------------------------------------------- tasa negociada --

    private async Task<LendingLineDto> LineaConReglaAsync()
    {
        var linea = await LineaDeCdatAsync(negocia: true);
        var regla = await new AddNegotiatedRateRuleCommandHandler(_a.Db, _a.C.Contexto, G.Alcance, G.Auditoria, G.Reloj).Handle(new AddNegotiatedRateRuleCommand(
            linea.PublicId, new NegotiatedRateRuleInput("ASOC", 1, new NegotiatedRateCriteriaDto(AmountFrom: 5_000_000m), 0.015m, null, CuentasDePrueba.Hoy,
                "Asociados con depósitos grandes")) { OperationKey = Guid.NewGuid() }, default);
        regla.IsSuccess.Should().BeTrue(regla.IsFailure ? $"{regla.Error.Code}: {regla.Error.Message}" : string.Empty);
        _a.Db.ChangeTracker.Clear();
        return linea;
    }

    [Fact]
    public async Task La_tasa_negociada_dentro_de_la_atribucion_se_sella_sola()
    {
        var linea = await LineaConReglaAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);
        ConPermisos(NegociacionDeTasa.PermisoDeAprobacion);

        var r = await ConstituirAsync(Pedido(linea, ahorro, negociacion: new TermDepositNegotiationInput(0.012m, "Asociado de cinco años")));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Active);
        r.Value.CurrentTerms!.EffectiveAnnual.Should().Be(0.112m);
        r.Value.CurrentTerms.TableRate.Should().Be(0.10m);
        r.Value.CurrentTerms.Negotiated!.MarginEa.Should().Be(0.012m);
        (await _a.Db.NegotiatedRateRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Fuera_de_la_atribucion_espera_y_no_se_fondea_hasta_decidirla_por_otra_persona()
    {
        var linea = await LineaConReglaAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);

        var r = await ConstituirAsync(Pedido(linea, ahorro, fondeo: TermDepositFundingKind.Collector,
            negociacion: new TermDepositNegotiationInput(0.012m, "Asociado de cinco años")));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Opening);
        r.Value.PendingNegotiation!.Status.Should().Be(NegotiatedRateRequestStatus.PendingDecision);
        r.Value.PendingNegotiation.MaxMarginEa.Should().Be(0.015m);

        var antes = await _a.ConsignarAsync(_a.Consignacion(r.Value.Account, 10_000_000m, ModalidadDeConsignacion.TermDepositFunding,
            medio: ClaseDeMedioDeRecaudo.Transfer));
        antes.Code.Should().Be(ErroresDeCdat.CodigoNegociacionPendiente);

        // Quien la pidió no la decide (segregación por persona) aunque tenga el permiso.
        ConPermisos(NegociacionDeTasa.PermisoDeAprobacion);
        (await DecidirAsync(r.Value.PendingNegotiation.PublicId, NegotiatedRateDecision.Approve)).Error.Code.Should().Be("Lending.Segregation.Conflict");

        G.ActuarComo(_a.C.Jefe);
        var aprobada = await DecidirAsync(r.Value.PendingNegotiation.PublicId, NegotiatedRateDecision.Approve);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        aprobada.Value.Status.Should().Be(NegotiatedRateRequestStatus.Approved);

        var fondeo = await _a.ConsignarAsync(_a.Consignacion(r.Value.Account, 10_000_000m, ModalidadDeConsignacion.TermDepositFunding,
            medio: ClaseDeMedioDeRecaudo.Transfer));
        fondeo.Result.Should().Be(ResultadoDelContrato.Applied, $"{fondeo.Code}: {fondeo.Message}");
        var version = await _a.Db.MemberAccountTermsVersions.SingleAsync();
        version.AppliedAnnualRate.Should().Be(0.112m);
        version.NegotiationApprovedByUserId.Should().Be(_a.C.Jefe.Id);
        version.NegotiatedRateRequestId.Should().NotBeNull();
    }

    [Fact]
    public async Task Negada_la_tasa_el_CDAT_queda_con_la_tabla()
    {
        var linea = await LineaConReglaAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, fondeo: TermDepositFundingKind.Collector,
            negociacion: new TermDepositNegotiationInput(0.012m, "Asociado de cinco años")));
        G.ActuarComo(_a.C.Jefe);
        ConPermisos(NegociacionDeTasa.PermisoDeAprobacion);
        var negada = await DecidirAsync(r.Value.PendingNegotiation!.PublicId, NegotiatedRateDecision.Reject);
        negada.Value.Status.Should().Be(NegotiatedRateRequestStatus.Rejected);
        var leido = await _a.Db.MemberAccounts.SingleAsync(a => a.PublicId == r.Value.Account.PublicId);
        PropuestaDeCdat.Leer(leido.PendingTermsJson)!.Negociacion.Should().BeNull();
        PropuestaDeCdat.Leer(leido.PendingTermsJson)!.TasaAplicada.Should().Be(0.10m);
    }

    [Fact]
    public async Task Sobre_el_maximo_de_la_regla_se_rechaza_con_el_maximo()
    {
        var linea = await LineaConReglaAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, negociacion: new TermDepositNegotiationInput(0.02m, "Pide dos puntos")));
        r.Error.Code.Should().Be("Lending.NegotiatedRate.MarginAboveRule");
        Sessions.GobiernoDePrueba.Datos(r.Error).GetProperty("maxMarginEa").GetDecimal().Should().Be(0.015m);
    }

    [Fact]
    public async Task Una_linea_que_no_admite_tasa_negociada_la_rechaza()
    {
        var linea = await LineaDeCdatAsync();
        var ahorro = await AhorroConSaldoAsync(20_000_000m);
        var r = await ConstituirAsync(Pedido(linea, ahorro, negociacion: new TermDepositNegotiationInput(0.005m, "Medio punto")));
        r.Error.Code.Should().Be(ErroresDeCdat.CodigoLineaNoNegocia);
        var regla = await new AddNegotiatedRateRuleCommandHandler(_a.Db, _a.C.Contexto, G.Alcance, G.Auditoria, G.Reloj).Handle(new AddNegotiatedRateRuleCommand(
            linea.PublicId, new NegotiatedRateRuleInput("GEN", 1, new NegotiatedRateCriteriaDto(), 0.01m, null, CuentasDePrueba.Hoy, "General"))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        regla.Error.Code.Should().Be(ErroresDeCdat.CodigoLineaNoNegocia);
    }
}
