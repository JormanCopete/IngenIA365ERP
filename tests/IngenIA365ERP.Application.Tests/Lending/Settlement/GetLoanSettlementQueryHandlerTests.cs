using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Settlement;

/// <summary>
/// Feature 013, US4, T0477 (api.md §8.1; FR-068, FR-011; T24, D11, C83; SC-001): la liquidación a (D, R) por concepto y cuota, con su explicación
/// (días, tasas, bases) y su huella, sale de la reconstrucción y no del libro; una D futura se proyecta sin pagos con la última usura conocida y
/// dice hasta cuándo vale; <c>NationalValues.Missing</c> sólo si D ≤ hoy; repetida da lo mismo y con R anterior a una operación posterior,
/// idéntica; consultar no escribe y audita <c>Lending.Payoff.Viewed</c>; fuera del alcance, el 404 genérico con <c>Lending.Access.OutOfScope</c>.
/// </summary>
public class GetLoanSettlementQueryHandlerTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private Task<Application.Common.Models.Result<LoanSettlementDto>> LiquidarAsync(DateOnly? d = null, DateTime? r = null) =>
        _c.Liquidar().Handle(new GetLoanSettlementQuery(_c.Credito.PublicId, d, r), default);

    [Fact]
    public async Task La_liquidacion_sale_de_la_reconstruccion_por_concepto_y_cuota_con_su_explicacion_y_no_del_libro()
    {
        _c.Ir(Hoy.AddDays(15));

        var r = await LiquidarAsync(Hoy.AddDays(15));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var l = r.Value;
        l.ByConcept["Principal"].Should().Be(3_000_000m);
        l.ByConcept["CurrentInterest"].Should().BePositive("quince días de corriente sin que el ciclo haya causado nada: no sale del libro");
        l.ToPayOff.Should().Be(l.ByConcept.Values.Sum());
        l.ByInstallment.Should().Contain(q => q.Number == 1 && q.Balances.ContainsKey("CurrentInterest"));
        l.Explanation.Should().Contain(t => t.Concept == "CurrentInterest" && t.Days == 15 && t.Base == 3_000_000m && t.EffectiveAnnual == 0.20m);
        l.Fingerprint.Should().StartWith("sha256:");
        l.RulesVersion.Should().Be("LND-2026.1");
        l.ValidUntil.Should().BeNull();

        _c.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = _c.Credito.Id, OperationId = (await _c.Db.LoanOperations.FirstAsync()).Id, Kind = LoanTransactionKind.Accrual,
            Concept = LoanConcept.CurrentInterest, InstallmentNumber = 1, Amount = 999_999m, ValueDate = Hoy.AddDays(2), AccountingDate = Hoy.AddDays(2),
        });
        await _c.Db.SaveChangesAsync();
        (await LiquidarAsync(Hoy.AddDays(15))).Value.Fingerprint.Should().Be(l.Fingerprint, "una fila de más en el libro no cambia la liquidación");
    }

    [Fact]
    public async Task Repetida_da_lo_mismo_y_con_R_anterior_a_una_operacion_posterior_es_identica()
    {
        _c.Ir(Hoy.AddDays(20));
        var r = _c.D.G.UtcNow;
        var antes = await LiquidarAsync(Hoy.AddDays(20), r);
        (await LiquidarAsync(Hoy.AddDays(20), r)).Value.Should().BeEquivalentTo(antes.Value);

        // Hoy se registra un reajuste con fecha valor anterior (otra tasa desde el día 5).
        var control = await _c.Db.LoanControls.SingleAsync();
        var desembolso = await _c.Db.LoanOperations.AsNoTracking().SingleAsync();
        var v1 = await _c.Db.LoanTermsVersions.AsNoTracking().SingleAsync();
        var reajuste = _c.D.Registro().RegistrarOperacion(control, r.AddMinutes(10), sello => new LoanOperation
        {
            LoanId = _c.Credito.Id, Kind = LoanOperationKind.RateReset, Number = "RR-1", Sequence = sello.Sequence, ValueDate = Hoy.AddDays(5),
            AccountingDate = Hoy.AddDays(20), RegisteredAt = sello.RegisteredAt, Amount = 0m, ActorKind = ActorKind.Process, InputsSha256 = new string('b', 64),
            RulesVersion = "LND-2026.1",
        });
        _c.Db.LoanTermsVersions.Add(Application.Lending.Accrual.ReajusteDeTasa.Version(v1, reajuste, Hoy.AddDays(5), 0.15m, Hoy.AddMonths(3)));
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        (await LiquidarAsync(Hoy.AddDays(20), r)).Value.Fingerprint.Should().Be(antes.Value.Fingerprint, "con R de antes no se ve la operación (US4-6)");
        (await LiquidarAsync(Hoy.AddDays(20))).Value.Fingerprint.Should().NotBe(antes.Value.Fingerprint);
        desembolso.Should().NotBeNull();
    }

    [Fact]
    public async Task Una_D_futura_se_proyecta_con_la_ultima_usura_y_dice_hasta_cuando_vale_y_una_D_pasada_sin_usura_es_NationalValues_Missing()
    {
        using var c = await CausacionDePrueba.CrearAsync(usuraHasta: Hoy.AddDays(10));

        var futura = await c.Liquidar().Handle(new GetLoanSettlementQuery(c.Credito.PublicId, Hoy.AddDays(30)), default);

        futura.IsSuccess.Should().BeTrue(futura.IsFailure ? futura.Error.Message : string.Empty);
        futura.Value.ValidUntil.Should().Be(Hoy.AddDays(10), "D11: vale hasta la víspera del primer día sin usura certificada");
        futura.Value.Warnings.Should().NotBeEmpty();

        c.Ir(Hoy.AddDays(20));
        var pasada = await c.Liquidar().Handle(new GetLoanSettlementQuery(c.Credito.PublicId, Hoy.AddDays(15)), default);
        pasada.Error.Code.Should().Be("NationalValues.Missing", "D ≤ hoy sin usura certificada");
    }

    [Fact]
    public async Task Consultar_no_escribe_y_audita_la_lectura_con_D_R_y_la_huella()
    {
        var filas = await _c.Db.LoanTransactions.CountAsync();
        var posiciones = await _c.Db.LoanDailyPositions.CountAsync();

        var r = await LiquidarAsync(Hoy.AddDays(3));

        (await _c.Db.LoanTransactions.CountAsync()).Should().Be(filas);
        (await _c.Db.LoanDailyPositions.CountAsync()).Should().Be(posiciones);
        (await _c.D.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingPayoffViewed);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Fuera_del_alcance_es_el_404_generico_y_se_audita()
    {
        _c.D.G.AlcanceActual = AlcanceDeCartera.Vacio;

        var r = await LiquidarAsync();
        var estado = await _c.EstadoDeCuenta().Handle(new GetLoanStatementQuery(_c.Credito.PublicId), default);

        r.Error.Code.Should().Be(LecturaDelCredito.CodigoInexistente);
        estado.Error.Code.Should().Be(LecturaDelCredito.CodigoInexistente);
        (await _c.D.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingAccessOutOfScope).And.NotContain(AuditEventTypes.LendingPayoffViewed);
        (await _c.Liquidar().Handle(new GetLoanSettlementQuery(Guid.NewGuid()), default)).Error.Code.Should().Be(LecturaDelCredito.CodigoInexistente);
    }
}
