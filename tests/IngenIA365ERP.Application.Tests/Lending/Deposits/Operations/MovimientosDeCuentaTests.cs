using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Operations;

/// <summary>
/// Feature 014, US4 (T122; contrato-de-cuentas §3.7–§3.11; FR-050 a FR-058, FR-086, FR-128, FR-129, SC-007): el contrato de cuentas real con el recaudador de
/// prueba sobre una cuenta de ahorro a la vista —consignar, consultar el disponible, retirar con el GMF, la misma clave, el canje, trasladar, reversar con
/// el sobregiro técnico—, los cargos periódicos y por evento con el cobro del pendiente, y la clave del débito automático. El escenario de
/// <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la UVT de 2026 (49.799) y los parámetros del GMF por defecto: 4 por mil, 350 UVT exentos al mes en
/// la cuenta marcada. (nuevo)
/// </summary>
public sealed class MovimientosDeCuentaTests : IDisposable
{
    private const decimal Uvt = 49_799m;

    private readonly AportesDePrueba _a = new();

    public MovimientosDeCuentaTests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        // El proceso salta las cuentas ocupadas: el cerrojo de prueba las concede todas.
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private async Task<MemberAccountDto> CuentaALaVistaAsync(bool exenta = false, DepositLineVersionInput? version = null, string codigo = "AHVISTA")
    {
        var linea = await _a.C.LineaDeCaptacionAsync(codigo, version ?? CuentasDePrueba.ALaVista());
        return await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana, exenta: exenta));
    }

    private ConsignacionAAplicar Libre(MemberAccountDto cuenta, decimal valor, ClaseDeMedioDeRecaudo medio = ClaseDeMedioDeRecaudo.Cash, string? clave = null) =>
        _a.Consignacion(cuenta, valor, ModalidadDeConsignacion.Free, clave: clave, medio: medio);

    private SolicitudDeRetiro Retiro(MemberAccountDto cuenta, decimal valor, string? clave = null) =>
        new(AportesDePrueba.Prueba, clave ?? AportesDePrueba.Clave(), cuenta.PublicId, valor, _a.G.HoyLocal, ClaseDeMedioDeRecaudo.Cash,
            [cuenta.Holder.PersonPublicId], cuenta.Holder.PersonPublicId, new ReferenciaDeRecibo("RC-9", _a.G.HoyLocal, null, null, "Recibo"));

    private async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(SolicitudDeRetiro s)
    {
        var r = await _a.Contrato().RetirarAsync(s, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<CuentaConsultadaDto> ConsultadaAsync(MemberAccountDto cuenta)
    {
        var r = await _a.Contrato().ConsultarAsync(new SolicitudDeConsultaDeCuentas(AportesDePrueba.Prueba, cuenta.Holder.PersonPublicId, _a.G.HoyLocal), default);
        r.Result.Should().Be(ResultadoDelContrato.Answered, $"{r.Code}: {r.Message}");
        return r.Value!.Accounts.Single(a => a.AccountPublicId == cuenta.PublicId);
    }

    private async Task<decimal> SaldoAsync(MemberAccountDto cuenta)
    {
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var filas = await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == id).ToListAsync();
        return filas.Where(t => t.Concept is MemberAccountConcept.Principal or MemberAccountConcept.InterestPayable or MemberAccountConcept.Bonus).Sum(t => t.Amount)
               - filas.Where(t => t.Concept == MemberAccountConcept.ChargeReceivable).Sum(t => t.Amount);
    }

    // ------------------------------------------------------------------------------------------------------- retiros --

    [Fact]
    public async Task US4_1_retirar_de_una_cuenta_no_marcada_cobra_el_4_por_mil_y_lo_deja_en_pareja_en_el_libro()
    {
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(Libre(cuenta, 1_000_000m));

        var r = await RetirarAsync(Retiro(cuenta, 400_000m));

        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        (await SaldoAsync(cuenta)).Should().Be(1_000_000m - 400_000m - 1_600m);
        var gmf = await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.Kind == MemberAccountTransactionKind.GmfCharged).ToListAsync();
        gmf.Select(t => (t.Concept, t.Amount)).Should().BeEquivalentTo(new[] { (MemberAccountConcept.Principal, -1_600m), (MemberAccountConcept.Gmf, 1_600m) });
        var uso = await _a.Db.GmfMonthlyUsages.AsNoTracking().SingleAsync();
        (uso.DisposedAmount, uso.ExemptAmount, uso.TaxableBase, uso.GmfAmount).Should().Be((400_000m, 0m, 400_000m, 1_600m));
        (await _a.HechosAsync()).Should().Contain([RetiroDeCuentaAplicadoV1.Type, GmfRetenidoV1.Type]);
    }

    [Fact]
    public async Task US4_1_retirar_de_la_cuenta_marcada_usa_el_tope_exento_del_mes_sin_GMF()
    {
        var cuenta = await CuentaALaVistaAsync(exenta: true);
        await _a.ConsignadaAsync(Libre(cuenta, 1_000_000m));

        var r = await RetirarAsync(Retiro(cuenta, 400_000m));

        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        (await SaldoAsync(cuenta)).Should().Be(600_000m);
        var uso = await _a.Db.GmfMonthlyUsages.AsNoTracking().SingleAsync();
        (uso.ExemptAmount, uso.GmfAmount).Should().Be((400_000m, 0m));
        (await _a.Db.MemberAccountTransactions.AnyAsync(t => t.Kind == MemberAccountTransactionKind.GmfCharged)).Should().BeFalse();
    }

    [Fact]
    public async Task SC_007_el_disponible_de_la_consulta_es_el_que_valida_el_retiro()
    {
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(Libre(cuenta, 500_000m));

        var consultada = await ConsultadaAsync(cuenta);
        var sobra = await RetirarAsync(Retiro(cuenta, consultada.Available.Available + 1m));

        sobra.Result.Should().Be(ResultadoDelContrato.Rejected);
        sobra.Code.Should().Be(ErroresDeMovimientos.CodigoDisponibleInsuficiente);
        (await _a.Db.MemberAccountOperations.CountAsync(o => o.Kind == MemberAccountOperationKind.Withdrawal)).Should().Be(0);
    }

    [Fact]
    public async Task La_misma_clave_del_retiro_devuelve_el_original_y_con_otro_valor_se_rechaza()
    {
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(Libre(cuenta, 1_000_000m));
        var clave = AportesDePrueba.Clave();

        var primera = await RetirarAsync(Retiro(cuenta, 100_000m, clave));
        var segunda = await RetirarAsync(Retiro(cuenta, 100_000m, clave));
        var otra = await RetirarAsync(Retiro(cuenta, 90_000m, clave));

        primera.Result.Should().Be(ResultadoDelContrato.Applied, $"{primera.Code}: {primera.Message}");
        segunda.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        segunda.Value!.OperationPublicId.Should().Be(primera.Value!.OperationPublicId);
        otra.Code.Should().Be("Lending.Contract.KeyReused");
        (await _a.Db.MemberAccountOperations.CountAsync(o => o.Kind == MemberAccountOperationKind.Withdrawal)).Should().Be(1);
    }

    [Fact]
    public async Task FR_050_lo_consignado_en_cheque_suma_al_saldo_pero_no_al_disponible_hasta_el_canje()
    {
        var cuenta = await CuentaALaVistaAsync();
        await _a.ConsignadaAsync(Libre(cuenta, 300_000m));
        await _a.ConsignadaAsync(Libre(cuenta, 200_000m, ClaseDeMedioDeRecaudo.Check));

        var consultada = await ConsultadaAsync(cuenta);

        consultada.Available.PendingClearance.Should().Be(200_000m);
        (await SaldoAsync(cuenta)).Should().Be(500_000m);
        var cheque = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.PaymentMeansClass == nameof(ClaseDeMedioDeRecaudo.Check));
        cheque.ClearingUntil.Should().Be(new DateOnly(2026, 12, 4), "tres días hábiles desde el martes 1 de diciembre");
        (await RetirarAsync(Retiro(cuenta, 400_000m))).Code.Should().Be(ErroresDeMovimientos.CodigoDisponibleInsuficiente);
    }

    // ------------------------------------------------------------------------------------------------------ traslado --

    [Fact]
    public async Task Trasladar_entre_dos_cuentas_del_mismo_titular_es_una_sola_operacion_exenta_de_GMF()
    {
        var origen = await CuentaALaVistaAsync();
        var linea = await _a.C.LineaDeCaptacionAsync("AHPROG", CuentasDePrueba.ALaVista());
        var destino = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(Libre(origen, 800_000m));

        var r = await _a.Contrato().TrasladarAsync(new SolicitudDeTraslado(AportesDePrueba.Prueba, AportesDePrueba.Clave(), origen.PublicId, destino.PublicId,
            300_000m, _a.G.HoyLocal, [origen.Holder.PersonPublicId], new ReferenciaDeRecibo("TR-1", _a.G.HoyLocal, null, null, "Traslado")), default);
        _a.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        (await SaldoAsync(origen)).Should().Be(500_000m);
        (await SaldoAsync(destino)).Should().Be(300_000m);
        (await _a.Db.MemberAccountOperations.CountAsync(o => o.Kind == MemberAccountOperationKind.Transfer)).Should().Be(1);
        (await _a.Db.GmfMonthlyUsages.AsNoTracking().SumAsync(u => u.GmfAmount)).Should().Be(0m, "entre cuentas de ahorro del mismo titular está exento");
    }

    // ------------------------------------------------------------------------------------------------------ reversos --

    [Fact]
    public async Task FR_056_reversar_por_el_recaudador_una_consignacion_ya_usada_deja_el_sobregiro_tecnico_con_alerta()
    {
        var cuenta = await CuentaALaVistaAsync();
        var clave = AportesDePrueba.Clave();
        await _a.ConsignadaAsync(Libre(cuenta, 500_000m, clave: clave));
        (await RetirarAsync(Retiro(cuenta, 400_000m))).Result.Should().Be(ResultadoDelContrato.Applied);

        var r = await _a.Contrato().ReversarAsync(new SolicitudDeReversoDeCuenta(AportesDePrueba.Prueba, clave, "Cheque devuelto"), default);
        _a.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        (await SaldoAsync(cuenta)).Should().BeNegative();
        await _a.Alertas.Received().LevantarAsync(
            Arg.Is<Application.Common.Alerts.AlertaALevantar>(s => s.TypeCode == Application.Common.Alerts.TiposDeAlerta.SobregiroTecnico),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task US4_6_desde_la_pantalla_nunca_queda_un_sobregiro_y_lo_de_un_recaudador_lo_reversa_el()
    {
        var cuenta = await CuentaALaVistaAsync();
        var consignacion = await _a.ConsignadaAsync(Libre(cuenta, 500_000m));
        var retiro = await RetirarAsync(Retiro(cuenta, 400_000m));
        var pantalla = new ReverseMemberAccountOperationCommandHandler(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.Reversion(), _a.Emision(), _a.G.Actor);

        var sobregira = await pantalla.Handle(new ReverseMemberAccountOperationCommand(cuenta.PublicId, consignacion.OperationPublicId, "Error de digitación")
            { OperationKey = Guid.NewGuid() }, default);
        var deOtro = await pantalla.Handle(new ReverseMemberAccountOperationCommand(cuenta.PublicId, retiro.Value!.OperationPublicId, "Error de digitación")
            { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();

        sobregira.Error.Code.Should().Be(ErroresDeMovimientos.CodigoReversoSobregira);
        deOtro.Error.Code.Should().Be(Application.Lending.Deposits.Contributions.ErroresDeAportes.CodigoReversoDeOtro);
        (await SaldoAsync(cuenta)).Should().Be(500_000m - 400_000m - 1_600m, "ningún reverso se aplicó");
    }

    // ------------------------------------------------------------------------------------------------------- cargos --

    private static DepositLineVersionInput ConCargo(DepositChargeInput cargo) => CuentasDePrueba.ALaVista() with { Charges = [cargo] };

    [Fact]
    public async Task FR_128_el_cargo_periodico_se_causa_el_ultimo_dia_del_mes_una_sola_vez()
    {
        var cuenta = await CuentaALaVistaAsync(version: ConCargo(new DepositChargeInput
        {
            Code = "CUOTAMAN", Name = "Cuota de manejo", Kind = ChargeKind.MaintenanceFee, Trigger = ChargeTrigger.Periodic, Enabled = true,
            Calculation = ChargeCalculation.FixedAmount, FixedAmount = 5_000m, Periodicity = RepaymentPeriodicity.Monthly,
        }));
        await _a.ConsignadaAsync(Libre(cuenta, 100_000m));
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var periodicos = new CargosPeriodicos(_a.Db, _a.Movimientos(), _a.Cargos(), _a.C.Cerrojo);
        var fin = new DateOnly(2026, 12, 31);

        (await periodicos.ProcesarAsync(new LendingProcessRun { Id = 1 }, new DateOnly(2026, 12, 15), [id], default)).Should().BeEmpty();
        (await _a.Db.AccountCharges.CountAsync()).Should().Be(0, "a mitad de mes no cierra el período");
        (await periodicos.ProcesarAsync(new LendingProcessRun { Id = 1 }, fin, [id], default)).Should().BeEmpty();
        (await periodicos.ProcesarAsync(new LendingProcessRun { Id = 1 }, fin, [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();

        var cargo = await _a.Db.AccountCharges.AsNoTracking().SingleAsync();
        (cargo.ChargeCode, cargo.Amount, cargo.Status, cargo.PeriodFrom).Should().Be(("CUOTAMAN", 5_000m, AccountChargeStatus.Collected, new DateOnly(2026, 12, 1)));
        (await SaldoAsync(cuenta)).Should().Be(95_000m);
    }

    [Fact]
    public async Task FR_128_el_cargo_que_no_alcanza_queda_pendiente_y_lo_cobra_la_siguiente_consignacion()
    {
        var cuenta = await CuentaALaVistaAsync(version: ConCargo(new DepositChargeInput
        {
            Code = "CUOTAMAN", Name = "Cuota de manejo", Kind = ChargeKind.MaintenanceFee, Trigger = ChargeTrigger.Periodic, Enabled = true,
            Calculation = ChargeCalculation.FixedAmount, FixedAmount = 5_000m, Periodicity = RepaymentPeriodicity.Monthly,
        }));
        await _a.ConsignadaAsync(Libre(cuenta, 2_000m));
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        (await new CargosPeriodicos(_a.Db, _a.Movimientos(), _a.Cargos(), _a.C.Cerrojo)
            .ProcesarAsync(new LendingProcessRun { Id = 1 }, new DateOnly(2026, 12, 31), [id], default)).Should().BeEmpty();
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.AccountCharges.AsNoTracking().SingleAsync()).Status.Should().Be(AccountChargeStatus.Pending);

        await _a.ConsignadaAsync(Libre(cuenta, 10_000m));

        var cargo = await _a.Db.AccountCharges.AsNoTracking().SingleAsync();
        cargo.Status.Should().Be(AccountChargeStatus.Collected);
        cargo.CollectedOperationId.Should().NotBeNull();
        (await SaldoAsync(cuenta)).Should().Be(2_000m + 10_000m - 5_000m);
        (await _a.HechosAsync()).Should().Contain(CargoPendienteCobradoV1.Type);
    }

    // ------------------------------------------------------------------------------------------------ débito automático --

    [Fact]
    public void La_clave_del_debito_automatico_es_una_por_credito_y_dia_y_su_pago_lo_admite_el_proceso()
    {
        var credito = Guid.Parse("0123456789abcdef0123456789abcdef");
        var clave = DebitoAutomatico.Clave(credito, new DateOnly(2026, 12, 5));

        clave.Should().Be("CAR:debito-automatico:0123456789abcdef0123456789abcdef:20261205");
        DebitoAutomatico.Clave(credito, new DateOnly(2026, 12, 5)).Should().Be(clave, "un débito repetido el mismo día no debita dos veces");
        var pago = new Application.Common.Integration.Contracts.Lending.PagoAplicadoV1
        {
            Collector = new Application.Common.Integration.Contracts.Lending.CollectorRefV1
            {
                CollectorCode = "CAR", CollectorClass = nameof(ClaseDeRecaudador.OwnLoanPayoff), IdempotencyKey = clave,
            },
        };
        Application.Common.Integration.Contracts.Lending.CatalogoDeMensajesDeCarteraV1.EsDebitoAutomatico(pago).Should().BeTrue();
        Application.Common.Integration.Contracts.Lending.CatalogoDeMensajesDeCarteraV1.EsDebitoAutomatico(
            pago with { Collector = pago.Collector with { IdempotencyKey = $"CAR:{credito:N}:{credito:N}" } }).Should().BeFalse();
    }
}
