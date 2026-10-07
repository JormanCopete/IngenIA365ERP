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
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Ledger;

/// <summary>
/// Feature 013, T0135 (M1, C45; quickstart §1.5; data-model §0.5, §5): las primitivas de <see cref="RegistroDeCartera"/>. Es el que
/// agrega operaciones, filas del libro, versiones de condiciones, planes con sus cuotas y posiciones, y el que avanza la marca de
/// agua; <c>Sequence</c> y <c>RegisteredAt</c> son monótonos por crédito (<c>max(ahora, último + 1 tick)</c>); un espejo lleva
/// <c>ReversesTransactionId</c> y signo contrario y no se espeja otro espejo; la causación avanza de a un día. US4 y US5 suman
/// <c>Conciliar</c> y la escritura en bloque. Todo en un solo guardado, por navegación, aunque el crédito sea nuevo.
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
}
