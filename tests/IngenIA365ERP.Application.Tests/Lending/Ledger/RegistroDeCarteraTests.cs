using FluentAssertions;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Ledger;

/// <summary>
/// Feature 013, T0135 (M1, C45; quickstart §1.5; data-model §0.5, §5): las primitivas de <see cref="RegistroDeCartera"/>. Es el que
/// agrega operaciones, filas del libro, versiones de condiciones, planes con sus cuotas y posiciones, y el que avanza la marca de
/// agua; <c>Sequence</c> y <c>RegisteredAt</c> son monótonos por crédito (<c>max(ahora, último + 1 tick)</c>); un espejo lleva
/// <c>ReversesTransactionId</c> y signo contrario y no se espeja otro espejo; la causación avanza de a un día. US4 y US5 suman
/// <c>Conciliar</c> y la escritura en bloque. Todo en un solo guardado, por navegación, aunque el crédito sea nuevo.
///
/// <para>US4 (T0469): la causación del proceso entra en bloque (un <c>AddRange</c>) avanzando la marca de d − 1 a d, con su resumen de tanda y
/// el mensaje de su parte en cada fila; la posición de un día cambiado agrega una versión nueva y nunca sobrescribe; <c>Conciliar</c> registra
/// cada diferencia por (día valor, concepto, cuota) con su clase y la fecha contable del período abierto; <c>Reversar</c> espeja todo o nada.</para>
/// </summary>
public class RegistroDeCarteraTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 10, 7);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private RegistroDeCartera Registro() => new(_db);

    private static Loan Credito() => new() { Number = "1", DebtorPersonId = 1, BranchId = 1, CreditLineId = 1, Status = LoanStatus.Active, OpenedOn = new DateOnly(2026, 10, 6) };

    private static LoanOperation Operacion(Loan credito, SelloDeCartera sello, LoanOperationKind clase = LoanOperationKind.Disbursement) => new()
    {
        Loan = credito, LoanId = credito.Id, Kind = clase, Number = $"OP-{sello.Sequence}", Sequence = sello.Sequence,
        RegisteredAt = sello.RegisteredAt, ValueDate = Hoy, AccountingDate = Hoy, Amount = 1_000_000m, ActorKind = ActorKind.Person,
        InputsSha256 = new string('a', 64), RulesVersion = "LND-2026.1",
    };

    [Fact]
    public async Task Nacen_en_un_solo_guardado_el_control_la_operacion_el_libro_las_condiciones_el_plan_y_la_posicion()
    {
        var registro = Registro();
        var credito = Credito();
        _db.Loans.Add(credito);
        var control = registro.IniciarControl(credito, new DateOnly(2026, 10, 6), Ahora);

        var op = registro.RegistrarOperacion(control, Ahora, sello => Operacion(credito, sello));
        registro.RegistrarTransacciones([new LoanTransaction
        {
            Loan = credito, Operation = op, Kind = LoanTransactionKind.Disbursement, Concept = LoanConcept.Principal,
            Amount = 1_000_000m, ValueDate = Hoy, AccountingDate = Hoy, RegisteredAt = op.RegisteredAt,
        }]);
        var condiciones = new LoanTermsVersion { Loan = credito, Operation = op, VersionNumber = 1, EffectiveValueDate = Hoy, RegisteredAt = op.RegisteredAt };
        registro.RegistrarVersionDeCondiciones(condiciones);
        var plan = new RepaymentScheduleVersion { Loan = credito, Operation = op, TermsVersion = condiciones, VersionNumber = 1, EffectiveValueDate = Hoy, RegisteredAt = op.RegisteredAt, PrincipalBase = 1_000_000m, FirstRegeneratedInstallment = 1, InstallmentCount = 1 };
        registro.RegistrarPlan(plan, [new RepaymentInstallment { ScheduleVersion = plan, InstallmentNumber = 1, PeriodStart = Hoy, DueDate = Hoy.AddMonths(1), ExigibleDate = Hoy.AddMonths(1), OpeningPrincipal = 1_000_000m, Principal = 1_000_000m, Total = 1_000_000m }]);
        registro.RegistrarPosicion(control, new LoanDailyPosition { Loan = credito, PositionDate = Hoy, Version = 1, RegisteredAt = op.RegisteredAt, Principal = 1_000_000m, PayoffAmount = 1_000_000m });

        await _db.SaveChangesAsync();

        var guardada = await _db.LoanTransactions.SingleAsync();
        guardada.LoanId.Should().Be(credito.Id);
        guardada.OperationId.Should().Be(op.Id, "la navegación enlaza la fila con su operación sin un guardado intermedio");
        (await _db.LoanControls.SingleAsync()).LastPositionDate.Should().Be(Hoy);
        (await _db.RepaymentInstallments.SingleAsync()).ScheduleVersionId.Should().Be(plan.Id);
        (await _db.LoanTermsVersions.SingleAsync()).OperationId.Should().Be(op.Id);
    }

    [Fact]
    public void Sequence_y_RegisteredAt_son_monotonos_por_credito()
    {
        var control = new LoanControl { LoanId = 1, Sequence = 4, LastRegisteredAt = Ahora };

        var primero = RegistroDeCartera.Sellar(control, Ahora.AddSeconds(-30));
        primero.Should().Be(new SelloDeCartera(5, Ahora.AddTicks(1)), "un reloj atrasado no hace retroceder el registro");

        var segundo = RegistroDeCartera.Sellar(control, Ahora.AddMinutes(1));
        segundo.Should().Be(new SelloDeCartera(6, Ahora.AddMinutes(1)));
        control.Sequence.Should().Be(6);
        control.LastRegisteredAt.Should().Be(Ahora.AddMinutes(1));
    }

    [Fact]
    public void Una_operacion_con_otro_sello_se_rechaza()
    {
        var credito = Credito();
        var control = new LoanControl { Loan = credito, LastRegisteredAt = Ahora };

        var accion = () => Registro().RegistrarOperacion(control, Ahora, sello => Operacion(credito, sello with { Sequence = 99 }));

        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void El_espejo_lleva_la_original_y_signo_contrario_y_no_se_espeja_un_espejo()
    {
        var original = new LoanTransaction { LoanId = 3, OperationId = 10, Kind = LoanTransactionKind.PaymentAllocation, Concept = LoanConcept.CurrentInterest, InstallmentNumber = 2, Amount = -15_000m, ValueDate = Hoy, AccountingDate = Hoy };
        typeof(LoanTransaction).GetProperty(nameof(LoanTransaction.Id))!.SetValue(original, 77L);
        var reverso = new LoanOperation { LoanId = 3, Kind = LoanOperationKind.PaymentReversal, RegisteredAt = Ahora };

        var espejo = RegistroDeCartera.Espejar(original, reverso, Hoy.AddDays(1));

        espejo.IsSuccess.Should().BeTrue();
        espejo.Value.Should().BeEquivalentTo(new { LoanId = 3, ReversesTransactionId = 77L, Amount = 15_000m, Kind = LoanTransactionKind.PaymentAllocation,
            Concept = LoanConcept.CurrentInterest, InstallmentNumber = 2, ValueDate = Hoy, AccountingDate = Hoy.AddDays(1), RegisteredAt = Ahora });
        espejo.Value.Operation.Should().BeSameAs(reverso);

        var deUnEspejo = RegistroDeCartera.Espejar(espejo.Value, reverso, Hoy);
        deUnEspejo.IsFailure.Should().BeTrue();
        deUnEspejo.Error.Code.Should().Be("Lending.Reversal.OfReversal");
    }

    [Fact]
    public void Una_fila_del_libro_lleva_importe_y_exactamente_un_origen()
    {
        var registro = Registro();
        var sinImporte = new LoanTransaction { LoanId = 1, OperationId = 1, Amount = 0m };
        var sinOrigen = new LoanTransaction { LoanId = 1, Amount = 5m };
        var conDos = new LoanTransaction { LoanId = 1, OperationId = 1, ProcessRunId = 2, Amount = 5m };

        ((Action)(() => registro.RegistrarTransacciones([sinImporte]))).Should().Throw<InvalidOperationException>();
        ((Action)(() => registro.RegistrarTransacciones([sinOrigen]))).Should().Throw<InvalidOperationException>();
        ((Action)(() => registro.RegistrarTransacciones([conDos]))).Should().Throw<InvalidOperationException>();
        ((Action)(() => registro.RegistrarTransacciones([new LoanTransaction { LoanId = 1, ProcessRunId = 2, Amount = 5m }]))).Should().NotThrow();
    }

    [Fact]
    public void La_causacion_avanza_de_a_un_dia()
    {
        var control = new LoanControl { LoanId = 1, LastAccruedDate = new DateOnly(2026, 10, 5) };

        RegistroDeCartera.AvanzarCausacion(control, new DateOnly(2026, 10, 6));
        control.LastAccruedDate.Should().Be(new DateOnly(2026, 10, 6));

        ((Action)(() => RegistroDeCartera.AvanzarCausacion(control, new DateOnly(2026, 10, 8)))).Should().Throw<InvalidOperationException>(
            "causar el 8 exige la marca en el 7 (T6)");
        ((Action)(() => RegistroDeCartera.AvanzarCausacion(control, new DateOnly(2026, 10, 6)))).Should().Throw<InvalidOperationException>(
            "un día ya causado no se vuelve a causar");
    }

    [Fact]
    public void Las_cuotas_son_de_su_plan_y_no_repiten_numero()
    {
        var plan = new RepaymentScheduleVersion { VersionNumber = 1 };
        var otro = new RepaymentScheduleVersion { VersionNumber = 2 };

        ((Action)(() => Registro().RegistrarPlan(plan, [new RepaymentInstallment { ScheduleVersion = otro, InstallmentNumber = 1 }])))
            .Should().Throw<InvalidOperationException>();
        ((Action)(() => Registro().RegistrarPlan(plan, [new RepaymentInstallment { ScheduleVersion = plan, InstallmentNumber = 1 },
            new RepaymentInstallment { ScheduleVersion = plan, InstallmentNumber = 1 }]))).Should().Throw<InvalidOperationException>();
    }

    // ------------------------------------------------------------------------------------- US4 (T0469: causación y conciliación) --

    private static MovimientoDeLibro Causado(DateOnly dia, LoanConcept concepto, int? cuota, decimal monto) =>
        new(dia, LoanTransactionKind.Accrual, concepto, cuota, monto);

    [Fact]
    public async Task La_causacion_del_proceso_entra_en_bloque_avanza_la_marca_de_un_dia_y_resume_la_tanda()
    {
        var credito = Credito();
        _db.Loans.Add(credito);
        await _db.SaveChangesAsync();
        var control = Registro().IniciarControl(credito, new DateOnly(2026, 10, 6), Ahora);
        await _db.SaveChangesAsync();
        var mensaje = Guid.NewGuid();
        var dias = new[] { new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8) };

        var resumen = Registro().CausarEnBloque(dias.Select(d => new CausacionAEscribir(control, d,
        [
            RegistroDeCartera.FilaDeCausacion(credito.Id, Causado(d, LoanConcept.CurrentInterest, 1, 1_234.56m), 9, null, d, Ahora, mensaje),
            RegistroDeCartera.FilaDeCausacion(credito.Id, Causado(d, LoanConcept.Insurance, 1, 100m), 9, null, d, Ahora, mensaje),
        ])).ToList());
        _db.ChangeTracker.Entries<LoanTransaction>().Should().HaveCount(4, "un solo AddRange con las cuatro filas");
        await _db.SaveChangesAsync();

        control.LastAccruedDate.Should().Be(new DateOnly(2026, 10, 8), "de d − 1 a d, en orden");
        resumen.Should().BeEquivalentTo(new { Creditos = 1, Dias = 2, Filas = 4 });
        resumen.PorConcepto[LoanConcept.CurrentInterest].Should().Be(2_469.12m);
        resumen.Total.Should().Be(2_669.12m);
        var filas = await _db.LoanTransactions.ToListAsync();
        filas.Should().OnlyContain(f => f.ProcessRunId == 9 && f.OperationId == null && f.MessagePublicId == mensaje && f.Kind == LoanTransactionKind.Accrual);
    }

    [Fact]
    public void La_causacion_en_bloque_rechaza_un_dia_fuera_de_orden_y_filas_ajenas()
    {
        var control = new LoanControl { LoanId = 1, LastAccruedDate = new DateOnly(2026, 10, 5) };
        var saltado = new DateOnly(2026, 10, 7);
        ((Action)(() => Registro().CausarEnBloque([new CausacionAEscribir(control, saltado, [])]))).Should().Throw<InvalidOperationException>(
            "causar el 7 exige la marca en el 6 (T6)");

        var ajena = RegistroDeCartera.FilaDeCausacion(2, Causado(new DateOnly(2026, 10, 6), LoanConcept.CurrentInterest, 1, 5m), 9, null, new DateOnly(2026, 10, 6), Ahora, null);
        ((Action)(() => Registro().CausarEnBloque([new CausacionAEscribir(control, new DateOnly(2026, 10, 6), [ajena])]))).Should().Throw<InvalidOperationException>();
        ((Action)(() => RegistroDeCartera.FilaDeCausacion(1, new MovimientoDeLibro(Hoy, LoanTransactionKind.Disbursement, LoanConcept.Principal, null, 5m), 9, null,
            Hoy, Ahora, null))).Should().Throw<InvalidOperationException>("la causación sólo escribe filas Accrual");
    }

    [Fact]
    public async Task Una_operacion_fechada_atras_agrega_una_version_nueva_de_cada_dia_cambiado_y_nunca_sobrescribe()
    {
        using var c = await Accrual.CausacionDePrueba.CrearAsync();
        var d = c.D.G.HoyLocal.AddDays(2);
        await c.CausarHastaAsync(d);
        var credito = c.Credito;
        var antes = await c.Db.LoanDailyPositions.Where(p => p.LoanId == credito.Id && p.PositionDate == d).ToListAsync();
        antes.Should().ContainSingle().Which.Version.Should().Be(1);

        // La misma reconstrucción: misma huella, no hay versión nueva.
        var cargado = await c.Cargador().CargarAsync(credito.Id, null, d, default);
        var control = await c.Db.LoanControls.SingleAsync(x => x.LoanId == credito.Id);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, d);
        c.D.Registro().PosicionVersionada(control, estado, credito, cargado.Vigentes, 1, antes[0].InputsSha256, Ahora).Should().BeNull();

        // Con un cambio (otra huella): versión 2, la 1 intacta.
        var cambiada = c.D.Registro().PosicionVersionada(control, estado, credito, cargado.Vigentes, 1, "otra", Ahora);
        await c.Db.SaveChangesAsync();
        cambiada!.Version.Should().Be(2);
        (await c.Db.LoanDailyPositions.CountAsync(p => p.LoanId == credito.Id && p.PositionDate == d)).Should().Be(2);
        (await c.Db.LoanDailyPositions.SingleAsync(p => p.LoanId == credito.Id && p.PositionDate == d && p.Version == 1)).InputsSha256
            .Should().Be(antes[0].InputsSha256);
    }

    [Fact]
    public async Task Conciliar_registra_cada_diferencia_como_fila_nueva_de_su_clase_con_fecha_contable_del_periodo_abierto()
    {
        var credito = Credito();
        _db.Loans.Add(credito);
        var control = Registro().IniciarControl(credito, Hoy, Ahora);
        var op = Registro().RegistrarOperacion(control, Ahora, sello => Operacion(credito, sello, LoanOperationKind.PaymentReversal));
        var dia = new DateOnly(2026, 10, 1);
        List<DiferenciaDeLibro> diferencias =
        [
            new(dia, LoanConcept.CurrentInterest, 2, 500m, 300m),
            new(dia, LoanConcept.DefaultInterest, 2, 100m, 0m),
            new(dia, LoanConcept.DefaultInterestInSuspense, 2, 0m, 100m),
            new(dia, LoanConcept.Principal, 2, 1_000m, 900m),
        ];

        var filas = Registro().Conciliar(credito.Id, diferencias, op, null, Hoy, Ahora, null);
        await _db.SaveChangesAsync();

        filas.Select(f => (f.Kind, f.Concept, f.Amount)).Should().BeEquivalentTo(new[]
        {
            (LoanTransactionKind.AccrualAdjustment, LoanConcept.CurrentInterest, -200m),
            (LoanTransactionKind.SuspenseReclassification, LoanConcept.DefaultInterest, -100m),
            (LoanTransactionKind.SuspenseReclassification, LoanConcept.DefaultInterestInSuspense, 100m),
            (LoanTransactionKind.PaymentReallocation, LoanConcept.Principal, -100m),
        });
        (await _db.LoanTransactions.ToListAsync()).Should().OnlyContain(f => f.ValueDate == dia && f.AccountingDate == Hoy && f.OperationId == op.Id
            && f.InstallmentNumber == 2, "cada diferencia por (día valor, concepto, cuota) enlazada a su operación");
    }

    [Fact]
    public void Reversar_espeja_todo_o_nada()
    {
        var original = new LoanTransaction { LoanId = 3, OperationId = 10, Kind = LoanTransactionKind.Accrual, Concept = LoanConcept.CurrentInterest, Amount = 10m, ValueDate = Hoy };
        var espejo = new LoanTransaction { LoanId = 3, OperationId = 11, Kind = LoanTransactionKind.Accrual, Concept = LoanConcept.CurrentInterest, Amount = -10m, ValueDate = Hoy, ReversesTransactionId = 5 };
        var reverso = new LoanOperation { LoanId = 3, Kind = LoanOperationKind.PaymentReversal, RegisteredAt = Ahora };

        Registro().Reversar([original, espejo], reverso, Hoy).Error.Code.Should().Be(ErroresDelLibroDelCartera());
        _db.ChangeTracker.Entries<LoanTransaction>().Should().BeEmpty("si una no se espeja, no se agrega ninguna");
        Registro().Reversar([original], reverso, Hoy).Value.Should().ContainSingle(f => f.Amount == -10m);
    }

    private static string ErroresDelLibroDelCartera() => ErroresDelLibroDeCartera.ReversoDeReversoCode;
}
