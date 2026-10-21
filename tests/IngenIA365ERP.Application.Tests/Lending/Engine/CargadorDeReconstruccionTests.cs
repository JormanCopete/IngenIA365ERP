using FluentAssertions;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Engine;

/// <summary>
/// Feature 013, US4, T0470 (T24, R32): <see cref="CargadorDeReconstruccion"/> lee en bloque 1..N créditos —las series una vez por modalidad— y
/// arma la entrada del motor con lo conocido en R: sólo operaciones registradas hasta R (un reverso registrado después no cuenta) y las versiones
/// de condiciones registradas hasta R. El plan pactado que arma es exactamente el que quedó guardado al desembolsar.
/// </summary>
public class CargadorDeReconstruccionTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task El_plan_pactado_que_arma_es_el_que_quedo_guardado_al_desembolsar()
    {
        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, Hoy, default);

        cargado!.Listo.Should().BeTrue(cargado.Error?.Message);
        var plan = MotorDeCartera.Reconstruir(cargado.Entrada!, Hoy).Plan!;
        var guardadas = await (from v in _c.Db.RepaymentScheduleVersions join i in _c.Db.RepaymentInstallments on v.Id equals i.ScheduleVersionId
                               where v.LoanId == _c.Credito.Id && v.VersionNumber == 1 orderby i.InstallmentNumber select i).ToListAsync();
        plan.Cuotas.Select(q => (q.Numero, q.Vencimiento, q.Capital, q.Interes, q.Seguro))
            .Should().Equal(guardadas.Select(i => (i.InstallmentNumber, i.DueDate, i.Principal, i.Interest, i.Insurance)));
        cargado.Entrada!.Condiciones.Should().BeEquivalentTo(new
        {
            RulesVersion = "LND-2026.1", TasaEfectivaAnual = 0.20m, ClaseDeMora = DefaultRateKind.RelativeToCurrent, FactorDeMora = (decimal?)1.5m,
            Convencion = DayCountConvention.Actual365, UsuraRequerida = true,
        });
        cargado.Entrada.Operaciones.Should().ContainSingle(o => o.Clase == LoanOperationKind.Disbursement && o.Monto == 3_000_000m);
    }

    [Fact]
    public async Task Lee_en_bloque_varios_creditos_y_las_series_una_vez_por_modalidad()
    {
        var otro = await _c.DesembolsarAsync(_c.D.O.Solicitud(solicitante: _c.D.O.Beto));
        var series = Substitute.For<ISeriesRegulatorias>();
        var real = _c.D.Series();
        series.LeerAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(ci =>
            real.LeerAsync(ci.ArgAt<string>(0), ci.ArgAt<string?>(1), ci.ArgAt<DateOnly>(2), ci.ArgAt<DateOnly>(3), ci.ArgAt<DateTime?>(4), default));

        var cargados = await new CargadorDeReconstruccion(_c.Db, series).CargarAsync([_c.Credito.Id, otro.Id, 999_999], null, Hoy.AddDays(10), default);

        cargados.Keys.Should().BeEquivalentTo([_c.Credito.Id, otro.Id], "el inexistente no viene");
        cargados.Values.Should().OnlyContain(c => c.Listo);
        await series.ReceivedWithAnyArgs(2).LeerAsync(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Solo_entran_las_operaciones_y_las_condiciones_registradas_hasta_R()
    {
        var r = _c.D.G.UtcNow.AddHours(1);
        var despues = r.AddHours(1);
        var desembolso = await _c.Db.LoanOperations.SingleAsync(o => o.LoanId == _c.Credito.Id);
        _c.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = _c.Credito.Id, Kind = LoanOperationKind.DisbursementReversal, Number = "DRV-9", Sequence = 9, ValueDate = Hoy, AccountingDate = Hoy,
            RegisteredAt = despues, Amount = desembolso.Amount, ActorKind = ActorKind.Process, ReversesOperationId = desembolso.Id,
            InputsSha256 = new string('a', 64), RulesVersion = "LND-2026.1",
        });
        var v1 = await _c.Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == _c.Credito.Id);
        _c.Db.LoanTermsVersions.Add(VersionPosterior(v1, desembolso.Id, despues));
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        var enR = await _c.Cargador().CargarAsync(_c.Credito.Id, r, Hoy, default);
        var ahora = await _c.Cargador().CargarAsync(_c.Credito.Id, null, Hoy, default);

        enR!.Entrada!.Operaciones.Should().ContainSingle("el reverso se registró después de R");
        enR.Vigentes.RestructuringCount.Should().Be(0);
        enR.Entrada.InformacionHasta.Should().Be(r);
        ahora!.Entrada!.Operaciones.Should().HaveCount(2);
        ahora.Vigentes.RestructuringCount.Should().Be(2);
        ahora.Entrada.Condiciones.Reestructuraciones.Should().Be(2);
        ReconstruccionDeCredito.OperacionesVigentes(ahora.Entrada, Hoy).Should().BeEmpty("la pareja reversada no entra");
    }

    [Fact]
    public async Task Con_una_operacion_sin_guardar_la_entrada_la_incluye_y_corre_R()
    {
        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, _c.D.G.UtcNow.AddMinutes(1), Hoy, default);
        var reajuste = new LoanOperation { LoanId = _c.Credito.Id, Kind = LoanOperationKind.RateReset, Sequence = 2, ValueDate = Hoy, RegisteredAt = _c.D.G.UtcNow.AddHours(2) };

        var con = CargadorDeReconstruccion.ConOperacion(cargado!.Entrada!, reajuste);

        con.Operaciones.Should().Contain(o => o.Id == "nueva:2" && o.Clase == LoanOperationKind.RateReset);
        con.InformacionHasta.Should().Be(reajuste.RegisteredAt);
    }

    private static LoanTermsVersion VersionPosterior(LoanTermsVersion v, long operacion, DateTime registrada) => new()
    {
        LoanId = v.LoanId, VersionNumber = 2, OperationId = operacion, EffectiveValueDate = v.EffectiveValueDate, RegisteredAt = registrada,
        Cause = TermsVersionCause.Restructuring, RulesVersion = v.RulesVersion, CreditLineVersionId = v.CreditLineVersionId, ModalityCode = v.ModalityCode,
        SubsegmentCode = v.SubsegmentCode, RateKind = v.RateKind, AnnualRate = v.AnnualRate, DefaultRateKind = v.DefaultRateKind,
        DefaultRateFactor = v.DefaultRateFactor, DayCount = v.DayCount, RateEquivalence = v.RateEquivalence, AmortizationSystem = v.AmortizationSystem,
        Periodicity = v.Periodicity, Installments = v.Installments, TermMonths = v.TermMonths, FirstDueDate = v.FirstDueDate, RoundingUnit = v.RoundingUnit,
        RoundingMode = v.RoundingMode, RoundingMoment = v.RoundingMoment, ConceptPriority = v.ConceptPriority, Insurances = v.Insurances,
        RestructuringCount = 2, InputsSha256 = v.InputsSha256,
    };
}
