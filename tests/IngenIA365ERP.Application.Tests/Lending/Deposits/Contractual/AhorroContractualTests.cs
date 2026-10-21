using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Contractual;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contractual;

/// <summary>
/// Feature 014, US7 (T182, T183, T184; api.md §4; FR-024, FR-074, FR-075; US7-1, US7-2): el ahorro contractual de punta a punta sobre el libro —la apertura que
/// sella el contrato (12 cuotas mensuales de 200.000 al 6 % EA en 30/360, bonificada al 8 %, castigo al 3 %, una cuota tolerada) con su primera cuota, las
/// cuotas que genera el paso diario, la consignación por cuotas (no libre), el retiro que no admite, el cumplimiento al vencer con la bonificación de 21.471 de la
/// investigación y la entrega a la cuenta a la vista; el incumplimiento por atrasos con sus tres efectos, el vencimiento con cuotas sin pagar que se reliquida a
/// la tasa de castigo (32.001 sobre diez aportes, el caso dorado <c>contractual-03</c>), la bonificación provisionada y su reversión, la cancelación anticipada
/// por la pantalla, la instrucción al vencimiento y la consulta del contrato—. Sobre <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K9. (nuevo)
/// </summary>
public sealed class AhorroContractualTests : IDisposable
{
    private const decimal Uvt = 49_799m;
    private const decimal Cuota = 200_000m;
    private static readonly DateOnly Hoy = CuentasDePrueba.Hoy;
    private static readonly DateOnly Vencimiento = Hoy.AddMonths(12);
    private readonly AportesDePrueba _a = new();

    public AhorroContractualTests()
    {
        _a.C.Entrega = EntregaDeCartera.K9;
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private Sessions.GobiernoDePrueba G => _a.G;

    private void Dia(DateOnly d)
    {
        G.HoyLocal = d;
        G.UtcNow = d.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
    }

    private LectorDeContractual Lector() => new(_a.Db, _a.C.Parametros(), _a.LectorDeInteres());

    private OrdenesDeGiroDeCuentas Ordenes() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Reversion(), _a.C.Parametros(), G.Actor, G.Reloj);

    private AperturaDeContractual AperturaDelContrato() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), Lector(), G.Alcance, G.Auditoria, Ordenes(), G.Actor,
        G.Reloj);

    private OpenMemberAccountCommandHandler Abrir() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj, AperturaDelContrato());

    private ReglasDeRetencion Retencion() => new(_a.C.Parametros(), G.Valores);

    private IncumplimientoDeContractual Incumplimiento() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), Lector(), G.Actor, G.Reloj);

    private BonificacionDeContractual Bonificacion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), Lector(), Retencion(), G.Actor, G.Reloj);

    private CancelacionDeCuenta Cancelacion() => new(_a.Db, _a.Movimientos(), _a.Liquidacion(), G.Reloj, Lector());

    private CierreDeContractual Cierre() => new(_a.Db, _a.LiquidacionALaFecha(), Cancelacion(), _a.Movimientos(), _a.Retiro(), _a.Traslado(), Ordenes(), G.Actor,
        G.Reloj);

    private CicloDelContractual Ciclo() => new(_a.Db, Lector(), Incumplimiento(), Bonificacion(), Cierre(), _a.C.Cerrojo);

    private GeneradorDeCuotas Generador() => new(_a.Db, _a.RegistroDeCuentas(), G.Valores, null,
        new GeneradorDeCuotasDeContractual(_a.Db, _a.RegistroDeCuentas(), Lector()));

    private CloseMemberAccountCommandHandler Cerrar() => new(_a.Db, G.Alcance, G.Auditoria, _a.C.Cerrojo, Cancelacion(), _a.Causacion(), _a.Liquidacion(),
        _a.Movimientos(), _a.Retiro(), _a.Traslado(), Ordenes(), _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), G.Actor, G.Reloj, null, Lector(),
        Incumplimiento(), Cierre());

    private static LendingProcessRun Corrida() => new() { Id = 1, Kind = LendingProcessKind.DailyCycle };

    /// <summary>La noche de <paramref name="d"/>: las cuotas, la causación, la liquidación periódica y el ciclo del contractual, como el paso de captaciones.</summary>
    private async Task NocheAsync(int cuenta, DateOnly d)
    {
        Dia(d);
        foreach (var subpaso in new ISubpasoDeCaptaciones[]
                 {
                     Generador(), _a.Causacion(), new LiquidacionPeriodicaDeIntereses(_a.Db, _a.Liquidacion(), _a.C.Cerrojo), Ciclo(),
                 })
        {
            var fallos = await subpaso.ProcesarAsync(Corrida(), d, [cuenta], default);
            fallos.Should().BeEmpty($"{subpaso.Nombre} del {d:yyyy-MM-dd}: " + string.Join("; ", fallos.Select(f => $"{f.Codigo}: {f.Mensaje}")));
            _a.Db.ChangeTracker.Clear();
        }
    }

    /// <summary>Las noches de fin de mes y de cada vencimiento de cuota, de <paramref name="desde"/> (excluido) a <paramref name="hasta"/>.</summary>
    private async Task NochesAsync(int cuenta, DateOnly desde, DateOnly hasta, Func<DateOnly, Task>? alAmanecer = null)
    {
        for (var d = desde.AddDays(1); d <= hasta; d = d.AddDays(1))
        {
            var finDeMes = d.AddDays(1).Day == 1;
            if (d.Day != 1 && !finDeMes && d != hasta) continue;
            if (alAmanecer is not null)
            {
                Dia(d);
                await alAmanecer(d);
            }
            await NocheAsync(cuenta, d);
        }
    }

    private async Task<LendingLineDto> LineaAsync(ContractualBreachEffect efecto = ContractualBreachEffect.BreachRate,
        BonusRecognition reconocimiento = BonusRecognition.AtFulfillment, int tolerancia = 1,
        ContractualMaturityDestinations destinos = ContractualMaturityDestinations.All, string codigo = "CONTRATO") =>
        await _a.C.LineaDeCaptacionAsync(codigo, new DepositLineVersionInput
        {
            ValidFrom = Hoy,
            Reason = "Reglamento del ahorro contractual, acta 31",
            Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.06m },
            DayCount = DayCountConvention.Thirty360,
            SettlementPeriodicity = RepaymentPeriodicity.Monthly,
            Contractual = new ContractualLineInput
            {
                InstallmentPeriodicity = RepaymentPeriodicity.Monthly, MinInstallment = 100_000m, TermMonthsFrom = 6, TermMonthsTo = 36, BonusRateEa = 0.08m,
                BreachRateEa = 0.03m, ToleranceInstallments = tolerancia, BreachEffect = efecto, BonusRecognition = reconocimiento, MaturityDestinations = destinos,
            },
        }, DepositClass.Contractual);

    private async Task<MemberAccountDto> ALaVistaAsync()
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        return await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
    }

    private async Task<Result<MemberAccountDto>> AbrirAsync(LendingLineDto linea, ContractualTermsInput? contrato)
    {
        var r = await Abrir().Handle(new OpenMemberAccountCommand(_a.C.Apertura(linea, _a.C.Ana) with { Contractual = contrato }) { OperationKey = Guid.NewGuid() },
            default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<MemberAccountDto> ContratoAsync(LendingLineDto linea, MemberAccountDto? destino = null, int meses = 12)
    {
        var r = await AbrirAsync(linea, new ContractualTermsInput(Cuota, meses,
            destino is null ? null : new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, destino.PublicId)));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<int> IdAsync(Guid cuenta) => await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta).Select(a => a.Id).SingleAsync();

    private async Task<decimal> SaldoAsync(Guid cuenta, MemberAccountConcept concepto)
    {
        var id = await IdAsync(cuenta);
        return await _a.Db.MemberAccountTransactions.Where(t => t.MemberAccountId == id && t.Concept == concepto).SumAsync(t => (decimal?)t.Amount) ?? 0m;
    }

    private Task PagarAsync(MemberAccountDto cuenta, DateOnly d) =>
        _a.ConsignadaAsync(_a.Consignacion(cuenta, Cuota, ModalidadDeConsignacion.Installments, fechaValor: d, medio: ClaseDeMedioDeRecaudo.Transfer));

    private async Task<Domain.Entities.Lending.Deposits.Accounts.MemberAccount> CuentaAsync(Guid cuenta) =>
        await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == cuenta);

    // ------------------------------------------------------------------------------------------------------- apertura --

    [Fact]
    public async Task La_apertura_sella_el_contrato_con_su_plan_y_la_primera_cuota()
    {
        var linea = await LineaAsync();
        var cuenta = await ContratoAsync(linea);

        cuenta.Status.Should().Be(MemberAccountStatus.Active);
        cuenta.Contractual.Should().NotBeNull();
        cuenta.Contractual!.Installment.Should().Be(Cuota);
        cuenta.Contractual.Installments.Should().Be(12);
        cuenta.Contractual.Status.Should().Be(ContractualStatus.Current);
        cuenta.Contractual.MaturityDate.Should().Be(Vencimiento);
        var id = await IdAsync(cuenta.PublicId);
        var version = await _a.Db.MemberAccountTermsVersions.SingleAsync(v => v.MemberAccountId == id);
        version.Cause.Should().Be(AccountTermsCause.ContractualSetup);
        version.PrincipalAmount.Should().Be(2_400_000m, "lo pactado: 12 cuotas de 200.000");
        version.AppliedAnnualRate.Should().Be(0.06m);
        version.BonusAnnualRate.Should().Be(0.08m);
        version.BreachAnnualRate.Should().Be(0.03m);
        version.ProjectedFinalAmount.Should().Be(2_486_777m, "investigación §3.4: cumpliendo, al 8 %");
        version.IssueTermBand.Should().Be(IssueTermBand.SixToTwelve);
        version.MaturityDate.Should().Be(Vencimiento);
        (await _a.Db.MemberAccountOperations.SingleAsync(o => o.Id == version.OperationId)).Kind.Should().Be(MemberAccountOperationKind.ContractualSetup);
        var cuota = await _a.Db.AccountInstallments.SingleAsync(c => c.MemberAccountId == id);
        cuota.DueDate.Should().Be(Hoy.AddMonths(1));
        cuota.Amount.Should().Be(Cuota);
        cuota.GeneratedByOperationId.Should().Be(version.OperationId);
    }

    [Fact]
    public async Task El_contrato_cabe_en_la_linea_y_va_solo_en_una_contractual()
    {
        var linea = await LineaAsync();
        (await AbrirAsync(linea, new ContractualTermsInput(50_000m, 12))).Error.Code.Should().Be(AhorroContractual.CodigoCuotaBajoElMinimo);
        (await AbrirAsync(linea, new ContractualTermsInput(Cuota, 48))).Error.Code.Should().Be(AhorroContractual.CodigoPlazoFueraDeRango);
        (await AbrirAsync(linea, null)).Error.Code.Should().Be(ErroresDeContractual.CodigoCondicionesRequeridas);
        var vista = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        (await AbrirAsync(vista, new ContractualTermsInput(Cuota, 12))).Error.Code.Should().Be(ErroresDeContractual.CodigoCondicionesNoAdmitidas);
        (await _a.Db.MemberAccounts.CountAsync()).Should().Be(0, "un contrato rechazado revierte la apertura entera");
    }

    [Fact]
    public async Task Sin_la_entrega_K9_el_contractual_no_se_abre()
    {
        var linea = await LineaAsync();
        _a.C.Entrega = EntregaDeCartera.K8;
        (await AbrirAsync(linea, new ContractualTermsInput(Cuota, 12))).Error.Code.Should().Be(ErroresDeAportes.CodigoEntregaNoDisponible);
    }

    // -------------------------------------------------------------------------------------------- cuotas y movimientos --

    [Fact]
    public async Task El_paso_diario_genera_cada_cuota_desde_el_vencimiento_de_la_anterior()
    {
        var cuenta = await ContratoAsync(await LineaAsync());
        var id = await IdAsync(cuenta.PublicId);
        await NocheAsync(id, Hoy.AddDays(15));
        (await _a.Db.AccountInstallments.CountAsync(c => c.MemberAccountId == id)).Should().Be(1);
        await NocheAsync(id, Hoy.AddMonths(1));
        var cuotas = await _a.Db.AccountInstallments.Where(c => c.MemberAccountId == id).OrderBy(c => c.DueDate).ToListAsync();
        cuotas.Select(c => c.DueDate).Should().Equal(Hoy.AddMonths(1), Hoy.AddMonths(2));
        cuotas[1].GeneratedByProcessRunId.Should().Be(1);
    }

    [Fact]
    public async Task Recibe_sus_cuotas_y_no_una_consignacion_libre_ni_un_retiro()
    {
        var cuenta = await ContratoAsync(await LineaAsync());
        Dia(Hoy.AddMonths(1));
        await PagarAsync(cuenta, Hoy.AddMonths(1));
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Principal)).Should().Be(Cuota);

        var libre = await _a.ConsignarAsync(_a.Consignacion(cuenta, 50_000m, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        libre.Code.Should().Be(ErroresDeContractual.CodigoConsignacionLibre);
        var excedente = await _a.ConsignarAsync(_a.Consignacion(cuenta, Cuota, ModalidadDeConsignacion.Installments, medio: ClaseDeMedioDeRecaudo.Transfer));
        excedente.Result.Should().NotBe(ResultadoDelContrato.Applied, "no hay cuota pendiente: lo que excede no se admite");

        var retiro = await _a.Retiro().RevisarAsync(await CuentaAsync(cuenta.PublicId), new RetiroPedido(null, null, null, cuenta.PublicId, 10_000m, G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId], null, null, null, PrevalidationOutcome.NotApplicable, [], null), pedirAprobacion: false, default);
        retiro.Error.Code.Should().Be(ErroresDeContractual.CodigoRetiroNoAdmitido);
    }

    // ----------------------------------------------------------------------------------------------------- vencimiento --

    [Fact]
    public async Task Cumplido_al_vencer_abona_la_bonificacion_de_21471_y_lo_traslada_a_la_vista()
    {
        // US7-1: todas las cuotas al día; al vencer se liquida con la tasa bonificada y el saldo se traslada según la instrucción.
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync(), vista);
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });

        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Fulfilled);
        contrato.Status.Should().Be(MemberAccountStatus.Closed);
        var vencimiento = await _a.Db.MemberAccountOperations.SingleAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualMaturity);
        vencimiento.Amount.Should().Be(21_471m, "investigación §3.4: 86.777 − 65.306");
        (await _a.Db.MemberAccountTransactions.Where(t => t.OperationId == vencimiento.Id && t.Concept == MemberAccountConcept.Principal).SumAsync(t => t.Amount))
            .Should().Be(21_471m, "bajo el umbral diario no se retiene");
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Bonus)).Should().Be(0m);
        var entregado = await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal);
        // Lo entregado es lo ahorrado, el interés base que causó el libro día a día en 30/360 (liquidado cada mes al peso; el 28 de febrero → 1 de marzo
        // cuenta tres días, research §19) y la bonificación: cerca del valor final de la investigación, nunca por debajo.
        entregado.Should().BeInRange(2_486_777m, 2_486_777m + 400m,
            "lo ahorrado con su interés a la tasa base más la bonificación da el valor final de la investigación");
        (await _a.Db.AccountInstallments.CountAsync(c => c.MemberAccountId == id)).Should().Be(12);
        var hechos = await _a.Db.IntegrationMessages.Where(m => m.Type == BonificacionReconocidaV1.Type).ToListAsync();
        hechos.Should().ContainSingle();
        hechos[0].PayloadJson.Should().Contain("21471");
    }

    [Fact]
    public async Task Vencido_con_cuotas_sin_pagar_espera_la_gracia_y_se_reliquida_a_la_tasa_de_castigo()
    {
        // US7 (Independent Test): pagando sólo 10 de 12 cuotas, la liquidación aplica la tasa de incumplimiento (caso dorado contractual-03: 32.001).
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync(tolerancia: 2), vista);
        var id = await IdAsync(cuenta.PublicId);
        var decima = Hoy.AddMonths(10);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1 && d <= decima) await PagarAsync(cuenta, d);
        });
        (await CuentaAsync(cuenta.PublicId)).Status.Should().Be(MemberAccountStatus.Active, "la última cuota tiene sus días de gracia");

        await NocheAsync(id, Vencimiento.AddDays(6));

        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Breached);
        contrato.Status.Should().Be(MemberAccountStatus.Closed);
        var incumplimiento = await _a.Db.MemberAccountOperations.SingleAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualBreach);
        incumplimiento.Amount.Should().Be(32_001m, "64.332 a la base menos 32.331 al castigo sobre los diez aportes");
        (await _a.Db.MemberAccountOperations.AnyAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualMaturity)).Should().BeFalse();
        (await _a.Db.AccountInstallments.Where(c => c.MemberAccountId == id && c.CancelledAt != null).CountAsync()).Should().Be(2,
            "las dos cuotas sin pagar ya no se exigen");
        (await _a.Db.IntegrationMessages.CountAsync(m => m.Type == PenalidadAplicadaV1.Type)).Should().Be(1);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().BeGreaterThan(2_000_000m);
    }

    // -------------------------------------------------------------------------------------------------- incumplimiento --

    [Fact]
    public async Task Mas_atrasos_que_los_tolerados_lo_dejan_incumplido_a_la_tasa_de_castigo()
    {
        // US7-2: más cuotas atrasadas que las toleradas → «incumplido» con el efecto de la línea (sigue a la tasa de castigo).
        var cuenta = await ContratoAsync(await LineaAsync());
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Hoy.AddMonths(3), async d =>
        {
            if (d == Hoy.AddMonths(1)) await PagarAsync(cuenta, d);
        });
        (await CuentaAsync(cuenta.PublicId)).ContractualStatus.Should().Be(ContractualStatus.Current, "la segunda está atrasada pero dentro de la tolerancia");

        await NocheAsync(id, Hoy.AddMonths(3).AddDays(6));

        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Breached);
        contrato.Status.Should().Be(MemberAccountStatus.Active, "con el efecto BreachRate el contrato sigue hasta su vencimiento");
        var versiones = await _a.Db.MemberAccountTermsVersions.Where(v => v.MemberAccountId == id).OrderBy(v => v.VersionNumber).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[1].Cause.Should().Be(AccountTermsCause.ContractualBreach);
        versiones[1].AppliedAnnualRate.Should().Be(0.03m);
        versiones[1].EffectiveValueDate.Should().Be(Hoy.AddMonths(3).AddDays(6));
        var aportes = new[] { new AporteDeContractual(Hoy.AddMonths(1), Cuota) };
        var esperada = AhorroContractual.Reliquidar(aportes, 0.06m, 0.03m, DayCountConvention.Thirty360, Hoy.AddMonths(3).AddDays(6),
            new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal)).Diferencia;
        (await _a.Db.MemberAccountOperations.SingleAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualBreach)).Amount
            .Should().Be(esperada);

        // Sigue generando cuotas y causando a la tasa de castigo.
        await NocheAsync(id, Hoy.AddMonths(4));
        (await _a.Db.AccountInstallments.CountAsync(c => c.MemberAccountId == id)).Should().Be(5);
    }

    [Fact]
    public async Task Incumplido_con_el_efecto_cancelar_se_liquida_y_se_entrega()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync(ContractualBreachEffect.Cancel), vista);
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Hoy.AddMonths(3), async d =>
        {
            if (d == Hoy.AddMonths(1)) await PagarAsync(cuenta, d);
        });
        await NocheAsync(id, Hoy.AddMonths(3).AddDays(6));

        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Breached);
        contrato.Status.Should().Be(MemberAccountStatus.Closed);
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().BeInRange(Cuota - 1m, Cuota + 2_000m);
        (await _a.Db.AccountInstallments.CountAsync(c => c.MemberAccountId == id && c.CancelledAt != null)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Incumplido_con_el_efecto_pasar_a_la_vista_va_a_la_cuenta_del_titular_sin_instruccion()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync(ContractualBreachEffect.MoveToDemand));
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Hoy.AddMonths(3), async d =>
        {
            if (d == Hoy.AddMonths(1)) await PagarAsync(cuenta, d);
        });
        await NocheAsync(id, Hoy.AddMonths(3).AddDays(6));

        (await CuentaAsync(cuenta.PublicId)).Status.Should().Be(MemberAccountStatus.Closed);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().BeGreaterThan(Cuota - 1m);
    }

    // ------------------------------------------------------------------------------------------------- bonificación --

    [Fact]
    public async Task Con_la_bonificacion_provisionada_se_reconoce_en_cada_cuota_y_se_reversa_al_incumplir()
    {
        var cuenta = await ContratoAsync(await LineaAsync(reconocimiento: BonusRecognition.Provisioned));
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Hoy.AddMonths(2), async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });
        var aportes = new[] { new AporteDeContractual(Hoy.AddMonths(1), Cuota), new AporteDeContractual(Hoy.AddMonths(2), Cuota) };
        var esperada = AhorroContractual.Bonificacion(aportes, 0.06m, 0.08m, DayCountConvention.Thirty360, Hoy.AddMonths(2),
            new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal)).Diferencia;
        esperada.Should().BeGreaterThan(0m);
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Bonus)).Should().Be(esperada);
        (await _a.Db.MemberAccountOperations.CountAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualBonus)).Should().BeGreaterThan(0);

        // Deja de pagar: incumple y la provisión se reversa.
        await NochesAsync(id, Hoy.AddMonths(2), Hoy.AddMonths(4).AddDays(6));
        (await CuentaAsync(cuenta.PublicId)).ContractualStatus.Should().Be(ContractualStatus.Breached);
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Bonus)).Should().Be(0m);
    }

    // ------------------------------------------------------------------------------------------------- por la pantalla --

    [Fact]
    public async Task Cancelarlo_antes_de_vencer_lo_reliquida_a_la_tasa_de_castigo_y_lo_entrega()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Hoy.AddMonths(3), async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });
        var fecha = Hoy.AddMonths(3).AddDays(10);
        await NocheAsync(id, fecha);

        var previa = await new PreviewMemberAccountClosingQueryHandler(_a.Db, G.Alcance, G.Auditoria, Cancelacion(), G.Reloj)
            .Handle(new PreviewMemberAccountClosingQuery(cuenta.PublicId), default);
        previa.IsSuccess.Should().BeTrue(previa.IsFailure ? previa.Error.Message : string.Empty);
        previa.Value.ContractualPenalty.Should().BeGreaterThan(0m);

        var r = await Cerrar().Handle(new CloseMemberAccountCommand(cuenta.PublicId, null,
            new CloseDeliveryInput(CloseDeliveryKind.TransferToAccount, vista.PublicId, null), [cuenta.Holder.PersonPublicId], "El asociado cancela su contrato")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Closed);
        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Breached);
        (await _a.Db.MemberAccountOperations.SingleAsync(o => o.MemberAccountId == id && o.Kind == MemberAccountOperationKind.ContractualBreach)).Amount
            .Should().Be(previa.Value.ContractualPenalty!.Value);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().Be(r.Value.Delivered);
    }

    [Fact]
    public async Task La_instruccion_al_vencimiento_se_cambia_y_la_consulta_muestra_el_contrato()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        var lectura = new GetContractualQueryHandler(_a.Db, Lector(), G.Alcance, G.Auditoria, G.Reloj);

        var antes = await lectura.Handle(new GetContractualQuery(cuenta.PublicId), default);
        antes.IsSuccess.Should().BeTrue(antes.IsFailure ? antes.Error.Message : string.Empty);
        antes.Value.Instruction.Should().BeNull();
        antes.Value.Installments.Should().HaveCount(12);
        antes.Value.Installments[0].Status.Should().Be(nameof(Domain.Entities.Lending.Deposits.Accounts.EstadoDeCuotaDeCuenta.Pending));
        antes.Value.Installments[1].Status.Should().Be("NotGenerated");
        antes.Value.Projection.ValueAtMaturityAtBase.Should().Be(2_465_306m);
        antes.Value.Projection.ValueAtMaturityIfFulfilled.Should().Be(2_486_777m);
        antes.Value.Projection.ProjectedBonus.Should().Be(21_471m);
        antes.Value.Terms.BaseRate.Should().Be(0.06m);

        var r = await new SetContractualMaturityInstructionCommandHandler(_a.Db, _a.C.Contexto, AperturaDelContrato(), Lector(), G.Alcance, G.Auditoria, G.Actor, G.Reloj)
            .Handle(new SetContractualMaturityInstructionCommand(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount,
                vista.PublicId), "El asociado lo quiere en su cuenta de ahorros") { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Instruction!.Kind.Should().Be(CloseDeliveryKind.TransferToAccount);
        r.Value.Instruction.TargetAccountNumber.Should().Be(vista.Number);

        var noEs = await new SetContractualMaturityInstructionCommandHandler(_a.Db, _a.C.Contexto, AperturaDelContrato(), Lector(), G.Alcance, G.Auditoria, G.Actor,
            G.Reloj).Handle(new SetContractualMaturityInstructionCommand(vista.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount,
            vista.PublicId), "x") { OperationKey = Guid.NewGuid() }, default);
        noEs.Error.Code.Should().Be(ErroresDeContractual.CodigoNoEsContractual);
    }

    [Fact]
    public async Task La_instruccion_no_admite_una_cuenta_de_otro_titular()
    {
        var cuenta = await ContratoAsync(await LineaAsync());
        var linea = await _a.C.LineaDeCaptacionAsync("AHBETO", CuentasDePrueba.ALaVista());
        var deBeto = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Beto));
        var r = await new SetContractualMaturityInstructionCommandHandler(_a.Db, _a.C.Contexto, AperturaDelContrato(), Lector(), G.Alcance, G.Auditoria, G.Actor,
            G.Reloj).Handle(new SetContractualMaturityInstructionCommand(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount,
            deBeto.PublicId), "x") { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be(ErroresDeContractual.CodigoDestinoInvalido);
    }

    // ------------------------------------------------------------ E248: lo que la línea admite y lo que decide el asociado --

    private async Task<Result<ContractualDto>> InstruirAsync(Guid cuenta, ContractualMaturityInstructionInput instruccion) =>
        await new SetContractualMaturityInstructionCommandHandler(_a.Db, _a.C.Contexto, AperturaDelContrato(), Lector(), G.Alcance, G.Auditoria, G.Actor, G.Reloj)
            .Handle(new SetContractualMaturityInstructionCommand(cuenta, instruccion, "Lo decidió el asociado") { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task La_linea_dice_que_destinos_admite_al_vencer_y_la_instruccion_se_valida_contra_ellos()
    {
        var vista = await ALaVistaAsync();
        var soloGiro = await LineaAsync(destinos: ContractualMaturityDestinations.DisbursementOrder);

        var alAbrir = await AbrirAsync(soloGiro, new ContractualTermsInput(Cuota, 12, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId)));
        alAbrir.IsFailure.Should().BeTrue();
        alAbrir.Error.Code.Should().Be(ErroresDeContractual.CodigoDestinoNoAdmitidoPorLaLinea);
        alAbrir.Error.Message.Should().Contain("no admite el traslado").And.Contain("giro");

        var cuenta = await ContratoAsync(soloGiro);
        var traslado = await InstruirAsync(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId));
        traslado.Error.Code.Should().Be(ErroresDeContractual.CodigoDestinoNoAdmitidoPorLaLinea);
        var giro = await InstruirAsync(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.DisbursementOrder));
        giro.IsSuccess.Should().BeTrue(giro.IsFailure ? giro.Error.Message : string.Empty);
        giro.Value.AllowedMaturityDestinations.Should().Be(ContractualMaturityDestinations.DisbursementOrder);

        var ninguno = await LineaAsync(destinos: ContractualMaturityDestinations.None, codigo: "CONTRATO2");
        var sinDestinos = await ContratoAsync(ninguno);
        (await InstruirAsync(sinDestinos.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.DisbursementOrder))).Error.Code
            .Should().Be(ErroresDeContractual.CodigoDestinoNoAdmitidoPorLaLinea);
    }

    [Fact]
    public async Task Cumplido_sin_instruccion_queda_a_disposicion_y_no_se_mueve_dinero()
    {
        // E248: el traslado a la vista es lo que decide el asociado; quien no dejó nada no lo tiene.
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });

        var contrato = await CuentaAsync(cuenta.PublicId);
        contrato.ContractualStatus.Should().Be(ContractualStatus.Fulfilled);
        contrato.Status.Should().Be(MemberAccountStatus.Matured, "vencido, a disposición del titular");
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Principal)).Should().BeGreaterThan(0m);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await _a.Db.IntegrationMessages.CountAsync(m => m.Type == TrasladoEntreCuentasAplicadoV1.Type)).Should().Be(0);
        // Ya vencido, la instrucción no se cambia: no hay nada que ejecutar.
        (await InstruirAsync(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId))).Error.Code
            .Should().Be(ErroresDeContractual.CodigoNoVigente);
    }

    [Fact]
    public async Task Cambiada_antes_de_vencer_el_proceso_ejecuta_la_ultima_y_solo_si_la_linea_la_admite()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        (await InstruirAsync(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId))).IsSuccess.Should().BeTrue();
        var id = await IdAsync(cuenta.PublicId);
        // La línea deja de admitir el traslado antes del vencimiento (la versión con la que se selló el contrato): el proceso no mueve dinero.
        var versionId = (await _a.Db.MemberAccountTermsVersions.AsNoTracking().FirstAsync(v => v.MemberAccountId == id)).DepositLineVersionId;
        var version = await _a.Db.DepositLineVersions.SingleAsync(v => v.Id == versionId);
        _a.Db.Entry(version).Property(v => v.MaturityDestinations).CurrentValue = ContractualMaturityDestinations.DisbursementOrder;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });

        (await CuentaAsync(cuenta.PublicId)).Status.Should().Be(MemberAccountStatus.Matured);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
    }

    private async Task<Result<ContractualDto>> RetirarAsync(Guid cuenta, string motivo = "El asociado ya no quiere moverlo") =>
        await new WithdrawContractualMaturityInstructionCommandHandler(_a.Db, _a.C.Contexto, Lector(), G.Alcance, G.Auditoria, G.Actor, G.Reloj)
            .Handle(new WithdrawContractualMaturityInstructionCommand(cuenta, motivo) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task E263_retirada_la_instruccion_el_contrato_vence_a_disposicion_sin_mover_dinero()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        (await InstruirAsync(cuenta.PublicId, new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId))).IsSuccess.Should().BeTrue();

        var retirada = await RetirarAsync(cuenta.PublicId);
        retirada.IsSuccess.Should().BeTrue(retirada.IsFailure ? $"{retirada.Error.Code}: {retirada.Error.Message}" : string.Empty);
        retirada.Value.Instruction.Should().BeNull("ya no rige ninguna instrucción");
        _a.Db.ChangeTracker.Clear();
        // Idempotente: retirar lo ya retirado responde el contrato sin tocar nada.
        (await RetirarAsync(cuenta.PublicId)).IsSuccess.Should().BeTrue();
        _a.Db.ChangeTracker.Clear();

        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });
        (await CuentaAsync(cuenta.PublicId)).Status.Should().Be(MemberAccountStatus.Matured, "a disposición del titular");
        (await SaldoAsync(cuenta.PublicId, MemberAccountConcept.Principal)).Should().BeGreaterThan(0m);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
        (await _a.Db.IntegrationMessages.CountAsync(m => m.Type == TrasladoEntreCuentasAplicadoV1.Type)).Should().Be(0);
    }

    [Fact]
    public async Task E263_retirar_tambien_deja_sin_efecto_la_instruccion_sellada_al_abrir()
    {
        var vista = await ALaVistaAsync();
        var cuenta = (await AbrirAsync(await LineaAsync(), new ContractualTermsInput(Cuota, 12,
            new ContractualMaturityInstructionInput(CloseDeliveryKind.TransferToAccount, vista.PublicId)))).Value;

        (await RetirarAsync(cuenta.PublicId)).Value.Instruction.Should().BeNull("la sellada tampoco rige ya");
        _a.Db.ChangeTracker.Clear();
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });
        (await CuentaAsync(cuenta.PublicId)).Status.Should().Be(MemberAccountStatus.Matured);
        (await SaldoAsync(vista.PublicId, MemberAccountConcept.Principal)).Should().Be(0m);
    }

    [Fact]
    public async Task E263_despues_del_vencimiento_o_en_una_cuenta_que_no_es_contractual_la_retirada_se_rechaza_con_su_motivo()
    {
        var vista = await ALaVistaAsync();
        var cuenta = await ContratoAsync(await LineaAsync());
        var id = await IdAsync(cuenta.PublicId);
        await NochesAsync(id, Hoy, Vencimiento, async d =>
        {
            if (d.Day == 1) await PagarAsync(cuenta, d);
        });

        var tarde = await RetirarAsync(cuenta.PublicId);
        tarde.IsFailure.Should().BeTrue();
        tarde.Error.Code.Should().Be(ErroresDeContractual.CodigoNoVigente);
        tarde.Error.Message.Should().Contain("no está vigente");
        (await RetirarAsync(vista.PublicId)).Error.Code.Should().Be(ErroresDeContractual.CodigoNoEsContractual);
    }

    [Fact]
    public async Task La_causacion_del_contractual_no_pasa_del_vencimiento()
    {
        var cuenta = await ContratoAsync(await LineaAsync(), meses: 6);
        var id = await IdAsync(cuenta.PublicId);
        var vence = Hoy.AddMonths(6);
        // Sin pagar nada: el saldo es cero y la cuenta no causa; con una cuota, causa hasta el vencimiento y nada después.
        Dia(Hoy.AddMonths(1));
        await PagarAsync(cuenta, Hoy.AddMonths(1));
        var lector = _a.LectorDeInteres();
        var contrato = await CuentaAsync(cuenta.PublicId);
        var ventana = await lector.VentanaAsync(contrato, vence.AddDays(-1), vence.AddDays(5), DateTime.MaxValue, false, default);
        ventana.IsSuccess.Should().BeTrue();
        ventana.Value.Interes.Dias.Where(x => x.Dia > vence).Should().OnlyContain(x => x.Causado == 0m);
        ventana.Value.Interes.Dias.Single(x => x.Dia == vence).Causado.Should().BeGreaterThan(0m);
        id.Should().BeGreaterThan(0);
    }
}
