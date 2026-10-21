using FluentAssertions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, T030 y T032 (data-model §0.2–§0.4, §0.7, §4): el único escritor del libro de cuentas y la numeración de las cuentas y sus operaciones.
/// El sello monótono por cuenta, el origen único y la precisión de cada fila, la retención y el GMF en pareja, el espejo que no se espeja, la
/// causación de a un día, la marca de liquidación, la posición versionada y el número por línea que salta lo que ya existe.
/// </summary>
public class RegistroDeCuentasTests
{
    private static readonly DateTime Ahora = new(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 11, 3);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private RegistroDeCuentas Registro() => new(_db);

    private static MemberAccount Cuenta(LineNature naturaleza = LineNature.Deposit, DepositClass? clase = DepositClass.Demand) =>
        MemberAccount.Abrir(1, naturaleza, clase, 7, 3, HandlingMode.Indistinct, Hoy, 5, Ahora);

    private static MemberAccountOperation Operacion(MemberAccount cuenta, SelloDeCuenta sello, MemberAccountOperationKind clase = MemberAccountOperationKind.Deposit,
        long? reversa = null) => new()
    {
        MemberAccount = cuenta,
        Kind = clase,
        Number = "MA-1",
        Sequence = sello.Sequence,
        ValueDate = Hoy,
        AccountingDate = Hoy,
        RegisteredAt = sello.RegisteredAt,
        Amount = 1000m,
        ActorKind = ActorKind.Person,
        ReversesOperationId = reversa,
        InputsSha256 = new string('a', 64),
        RulesVersion = VersionesDeReglas.Vigente,
    };

    private static MemberAccountTransaction Fila(MemberAccountOperation? operacion, MemberAccountTransactionKind clase, MemberAccountConcept concepto, decimal monto,
        int? corrida = null) => new()
    {
        Operation = operacion,
        ProcessRunId = corrida,
        Kind = clase,
        Concept = concepto,
        Amount = monto,
        ValueDate = Hoy,
        AccountingDate = Hoy,
        RegisteredAt = Ahora,
    };

    [Fact]
    public void El_sello_es_monotono_por_cuenta_aunque_el_reloj_retroceda()
    {
        var control = new MemberAccountControl { MemberAccountId = 1, LastRegisteredAt = Ahora };

        var a = RegistroDeCuentas.Sellar(control, Ahora.AddMinutes(-5));
        var b = RegistroDeCuentas.Sellar(control, Ahora.AddMinutes(1));

        a.Sequence.Should().Be(1);
        a.RegisteredAt.Should().Be(Ahora.AddTicks(1));
        b.Sequence.Should().Be(2);
        b.RegisteredAt.Should().Be(Ahora.AddMinutes(1));
        control.Sequence.Should().Be(2);
    }

    [Fact]
    public void La_cuenta_nace_con_su_control_y_la_operacion_lleva_el_sello_del_registro()
    {
        var cuenta = Cuenta();
        var control = Registro().IniciarControl(cuenta, Hoy, Ahora);

        var operacion = Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s));

        operacion.Sequence.Should().Be(1);
        _db.MemberAccountControls.Local.Should().ContainSingle();
        _db.MemberAccountOperations.Local.Should().ContainSingle();
        FluentActions.Invoking(() => Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s with { Sequence = 99 })))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s, MemberAccountOperationKind.Reversal)))
            .Should().Throw<InvalidOperationException>("un reverso nombra la operación que reversa");
    }

    [Fact]
    public void Una_fila_lleva_importe_un_origen_y_la_precision_de_su_clase()
    {
        var cuenta = Cuenta();
        var control = Registro().IniciarControl(cuenta, Hoy, Ahora);
        var op = Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s));

        FluentActions.Invoking(() => Registro().RegistrarTransacciones([Fila(op, MemberAccountTransactionKind.Deposit, MemberAccountConcept.Principal, 0m)]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Registro().RegistrarTransacciones([Fila(op, MemberAccountTransactionKind.Deposit, MemberAccountConcept.Principal, 1m, corrida: 4)]))
            .Should().Throw<InvalidOperationException>("una fila tiene exactamente un origen");
        FluentActions.Invoking(() => Registro().RegistrarTransacciones([Fila(op, MemberAccountTransactionKind.Deposit, MemberAccountConcept.Principal, 10.005m)]))
            .Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(ErroresDelLibroDeCuentas.ImporteDemasiadoPrecisoCode);

        Registro().RegistrarTransacciones([
            Fila(op, MemberAccountTransactionKind.Deposit, MemberAccountConcept.Principal, 10.50m),
            Fila(null, MemberAccountTransactionKind.InterestAccrued, MemberAccountConcept.InterestPayable, 0.123456m, corrida: 4),
        ]);
        _db.MemberAccountTransactions.Local.Should().HaveCount(2, "la causación guarda hasta 6 decimales (MontoCausado)");
    }

    [Fact]
    public void La_retencion_y_el_gmf_van_en_pareja()
    {
        var cuenta = Cuenta();
        var control = Registro().IniciarControl(cuenta, Hoy, Ahora);
        var op = Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s, MemberAccountOperationKind.Withdrawal));

        FluentActions.Invoking(() => Registro().RegistrarTransacciones([
                Fila(op, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -4m),
                Fila(op, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, 3m),
            ]))
            .Should().Throw<InvalidOperationException>();

        Registro().RegistrarTransacciones([
            Fila(op, MemberAccountTransactionKind.Withdrawal, MemberAccountConcept.Principal, -1000m),
            Fila(op, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -4m),
            Fila(op, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, 4m),
        ]);
        _db.MemberAccountTransactions.Local.Should().HaveCount(3);
    }

    [Fact]
    public void El_espejo_invierte_el_signo_y_un_espejo_no_se_espeja()
    {
        var cuenta = Cuenta();
        var control = Registro().IniciarControl(cuenta, Hoy, Ahora);
        var op = Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s));
        var reverso = Registro().RegistrarOperacion(control, Ahora, s => Operacion(cuenta, s, MemberAccountOperationKind.Reversal, reversa: 1));
        var original = new MemberAccountTransaction
        {
            Id = 10, MemberAccountId = 1, OperationId = 1, Kind = MemberAccountTransactionKind.Deposit, Concept = MemberAccountConcept.Principal,
            Amount = 250m, ValueDate = Hoy.AddDays(-2), AccountingDate = Hoy.AddDays(-2), RegisteredAt = Ahora,
        };

        var espejo = RegistroDeCuentas.Espejar(original, reverso, Hoy);

        espejo.IsSuccess.Should().BeTrue();
        espejo.Value.Amount.Should().Be(-250m);
        espejo.Value.ValueDate.Should().Be(original.ValueDate, "el espejo vive en la fecha valor de su original");
        espejo.Value.AccountingDate.Should().Be(Hoy);
        espejo.Value.ReversesTransactionId.Should().Be(10);

        var deEspejo = RegistroDeCuentas.Espejar(new MemberAccountTransaction { Id = 11, ReversesTransactionId = 10, Amount = -250m }, reverso, Hoy);
        deEspejo.IsFailure.Should().BeTrue();
        deEspejo.Error.Code.Should().Be(ErroresDelLibroDeCuentas.ReversoDeReversoCode);
        op.ReversedByOperationId.Should().BeNull();
    }

    [Fact]
    public void La_causacion_avanza_de_a_un_dia_y_en_bloque()
    {
        var control = new MemberAccountControl { MemberAccountId = 1, LastAccruedDate = Hoy.AddDays(-1) };
        var fila = new MemberAccountTransaction
        {
            MemberAccountId = 1, ProcessRunId = 9, Kind = MemberAccountTransactionKind.InterestAccrued, Concept = MemberAccountConcept.InterestPayable,
            Amount = 12.345678m, ValueDate = Hoy, AccountingDate = Hoy, RegisteredAt = Ahora,
        };

        var resumen = Registro().CausarEnBloque([new CausacionDeCuentaAEscribir(control, Hoy, [fila])]);

        control.LastAccruedDate.Should().Be(Hoy);
        resumen.Should().BeEquivalentTo(new { Cuentas = 1, Dias = 1, Filas = 1 }, o => o.ExcludingMissingMembers());
        resumen.PorConcepto[MemberAccountConcept.InterestPayable].Should().Be(12.345678m);
        FluentActions.Invoking(() => RegistroDeCuentas.AvanzarCausacion(control, Hoy.AddDays(2))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Liquidar_exige_la_causacion_hasta_el_fin_del_periodo_y_no_retrocede()
    {
        var control = new MemberAccountControl { MemberAccountId = 1, LastAccruedDate = new DateOnly(2026, 10, 31) };

        RegistroDeCuentas.MarcarLiquidacion(control, new DateOnly(2026, 10, 31));
        control.LastSettledThrough.Should().Be(new DateOnly(2026, 10, 31));

        FluentActions.Invoking(() => RegistroDeCuentas.MarcarLiquidacion(control, new DateOnly(2026, 11, 30))).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => RegistroDeCuentas.MarcarLiquidacion(control, new DateOnly(2026, 10, 31))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void La_actividad_del_titular_no_cuenta_la_causacion_ni_los_cargos_y_no_retrocede()
    {
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.Deposit).Should().BeTrue();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.InterestAccrued).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.ChargeApplied).Should().BeFalse();
        RegistroDeCuentas.EsMovimientoDelTitular(MemberAccountTransactionKind.Reclassification).Should().BeFalse();

        var control = new MemberAccountControl { MemberAccountId = 1 };
        RegistroDeCuentas.MarcarActividadDelTitular(control, Hoy);
        RegistroDeCuentas.MarcarActividadDelTitular(control, Hoy.AddDays(-3));
        control.LastHolderActivityDate.Should().Be(Hoy);
    }

    [Fact]
    public void La_posicion_se_versiona_sin_sobrescribir_y_sale_de_la_reconstruccion()
    {
        var cuenta = Cuenta(LineNature.Contribution, null);
        cuenta.Activar(Hoy, Ahora);
        var control = Registro().IniciarControl(cuenta, Hoy, Ahora);
        var estado = MotorDeCaptaciones.Reconstruir([
            new MovimientoDeCuenta(1, MemberAccountTransactionKind.OpeningBalance, MemberAccountConcept.ContributionMinimum, 300_000m, Hoy, Ahora),
            new MovimientoDeCuenta(2, MemberAccountTransactionKind.Deposit, MemberAccountConcept.ContributionOrdinary, 20_000m, Hoy, Ahora),
        ], Hoy, Ahora);

        var primera = Registro().PosicionVersionada(control, RegistroDeCuentas.Posicion(estado, cuenta, 1, Ahora), 0, null);
        var igual = Registro().PosicionVersionada(control, RegistroDeCuentas.Posicion(estado, cuenta, 2, Ahora), 1, estado.Huella);

        primera.Should().NotBeNull();
        primera!.ContributionMinimum.Should().Be(300_000m);
        primera.ContributionOrdinary.Should().Be(20_000m);
        primera.AvailableBalance.Should().Be(320_000m);
        primera.Nature.Should().Be(LineNature.Contribution);
        igual.Should().BeNull("una huella igual no agrega versión");
        control.LastPositionDate.Should().Be(Hoy);
        FluentActions.Invoking(() => Registro().PosicionVersionada(control, RegistroDeCuentas.Posicion(estado, cuenta, 5, Ahora), 1, "otra"))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("10", 0)]
    [InlineData("10.50", 1)]
    [InlineData("0.123456", 6)]
    [InlineData("-3.1000", 1)]
    public void Los_decimales_se_cuentan_sin_los_ceros_a_la_derecha(string valor, int decimales)
    {
        RegistroDeCuentas.Decimales(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(decimales);
    }

    // ------------------------------------------------------------------------------------------------- T032 --

    [Fact]
    public async Task La_cuenta_se_numera_por_linea_y_salta_un_numero_que_ya_existe()
    {
        var numerador = new NumeradorDeCartera(_db, Substitute.For<ICerrojoDeCartera>());
        var existente = Cuenta();
        _db.MemberAccounts.Add(existente);
        await numerador.NumerarCuentaAsync(existente, "AHO", "AH-");
        await _db.SaveChangesAsync();
        existente.Number.Should().Be("AH-000001");

        var otraDeLaMisma = Cuenta();
        var deOtraLinea = Cuenta(LineNature.Contribution, null);
        await numerador.NumerarCuentaAsync(otraDeLaMisma, "AHO", "AH-");
        await numerador.NumerarCuentaAsync(deOtraLinea, "APO", "AP-");

        otraDeLaMisma.Number.Should().Be("AH-000002");
        deOtraLinea.Number.Should().Be("AP-000001", "cada línea lleva su consecutivo (data-model §0.4)");
        await FluentActions.Awaiting(() => numerador.NumerarCuentaAsync(otraDeLaMisma, "AHO", "AH-")).Should().ThrowAsync<InvalidOperationException>();

        (await _db.LendingNumberSequences.AsNoTracking().CountAsync(s => s.Kind == LendingNumberKind.MemberAccount)).Should().Be(1);
        _db.LendingNumberSequences.Local.Where(s => s.Kind == LendingNumberKind.MemberAccount).Select(s => s.ScopeKey).Should().BeEquivalentTo("AHO", "APO");
    }

    [Fact]
    public async Task Las_operaciones_de_cuenta_se_numeran_por_clase_y_saltan_lo_que_existe()
    {
        _db.LendingNumberSequences.Add(new LendingNumberSequence
        {
            Kind = LendingNumberKind.MemberAccountOperation, ScopeKey = nameof(MemberAccountOperationKind.Deposit), Prefix = "MA-", NextValue = 7, MinDigits = 3,
        });
        var cuenta = Cuenta();
        _db.MemberAccounts.Add(cuenta);
        // Un comprobante que ya existe (migrado): el numerador lo salta.
        _db.MemberAccountOperations.Add(new MemberAccountOperation
        {
            MemberAccount = cuenta, Kind = MemberAccountOperationKind.Deposit, Number = "MA-008", Sequence = 1, ValueDate = Hoy, AccountingDate = Hoy,
            RegisteredAt = Ahora, Amount = 1m, ActorKind = ActorKind.Person, InputsSha256 = new string('a', 64), RulesVersion = VersionesDeReglas.Vigente,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var numerador = new NumeradorDeCartera(_db, Substitute.For<ICerrojoDeCartera>());

        (await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Deposit, "X")).Should().Be("MA-007");
        (await numerador.NumerosDeOperacionDeCuentaAsync(MemberAccountOperationKind.Deposit, "X", 2)).Should().Equal("MA-009", "MA-010");
        (await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Withdrawal, "MR-")).Should().Be("MR-000001");
    }

    [Fact]
    public async Task El_consecutivo_de_cuentas_arranca_por_encima_de_lo_migrado_y_los_procesos_tienen_el_suyo()
    {
        var numerador = new NumeradorDeCartera(_db, Substitute.For<ICerrojoDeCartera>());

        await numerador.LlevarCuentasPorEncimaDeAsync("AHO", ["AH-000120", "35", "CTA-7X"], "AH-");
        var cuenta = Cuenta();
        await numerador.NumerarCuentaAsync(cuenta, "AHO", "AH-");
        cuenta.Number.Should().Be("AH-000121");

        (await numerador.NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind.MembershipWithdrawal, "RA-")).Should().Be("RA-000001");
        await FluentActions.Awaiting(() => numerador.NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind.Loan, "X")).Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
