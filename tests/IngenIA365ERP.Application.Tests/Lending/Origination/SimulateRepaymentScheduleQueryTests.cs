using FluentAssertions;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0366 (api.md §4.1, FR-034, R40): el simulador. No escribe nada —ni una clave de operación—; devuelve el plan cuota a cuota con
/// capital, interés, seguros, otros y saldo, la efectiva, la nominal del período y la efectiva con cargos, los totales, la usura y la explicación,
/// con la versión de reglas y la huella; lo que se sale de la línea es <c>Lending.Schedule.OutOfLineRange</c> con <c>{ field, min, max }</c>; una
/// línea cerrada o bloqueada por usura no se simula; una efectiva sobre la usura no es error (200 con el aviso); sin usura del día,
/// <c>NationalValues.Missing</c> y la alerta a quien la registra (US1-3); sólo los seguros opcionales elegidos.
/// </summary>
public sealed class SimulateRepaymentScheduleQueryTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync(activada: false);

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private ScheduleSimulationInput Entrada(decimal monto = 12_000_000m, int cuotas = 36) => new(_o.Linea.PublicId, monto, cuotas);

    private Task<IngenIA365ERP.Application.Common.Models.Result<ScheduleSimulationDto>> Simular(ScheduleSimulationInput entrada) =>
        _o.Simular().Handle(new SimulateRepaymentScheduleQuery(entrada), default);

    [Fact]
    public async Task Simula_el_plan_cuota_a_cuota_con_tasas_totales_usura_y_explicacion()
    {
        var r = await Simular(Entrada());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var s = r.Value;
        s.RulesVersion.Should().Be(VersionesDeReglas.Vigente);
        s.Installments.Should().HaveCount(36);
        s.InstallmentCount.Should().Be(36);
        s.Installments.Sum(c => c.Principal).Should().Be(12_000_000m, "Σ capital = monto (SC-017)");
        s.Installments[^1].BalanceAfter.Should().Be(0m);
        s.Installments.Should().OnlyContain(c => c.Total == c.Principal + c.Interest + c.Insurance + c.Other);
        s.EffectiveAnnualRate.Should().Be(0.20m);
        s.NominalPeriodRate.Should().BeGreaterThan(0m).And.BeLessThan(0.02m);
        s.EffectiveRateWithCharges.Should().BeGreaterThanOrEqualTo(0.199m);
        s.Totals.Principal.Should().Be(12_000_000m);
        s.Totals.ToPay.Should().Be(s.Installments.Sum(c => c.Total));
        s.Totals.Interest.Should().Be(s.Installments.Sum(c => c.Interest));
        s.Usury.Passes.Should().BeTrue();
        s.Usury.ModalityKey.Should().Be(LineasDePrueba.ClaveConsumo);
        s.Usury.Rate.Should().Be(0.25m);
        s.Usury.Headroom.Should().BeGreaterThan(0m);
        s.Explanation.Should().HaveCount(36).And.OnlyContain(x => x.Days > 0 && x.DailyRate > 0m && x.Base > 0m);
        s.ExplanationLines.Should().NotBeEmpty();
        s.Warnings.Should().BeEmpty();
        s.SimulationSha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task No_escribe_nada_ni_una_clave_de_operacion()
    {
        var antes = (await _o.Db.OperationKeys.CountAsync(), await _o.Db.CreditApplications.CountAsync(), await _o.Db.LoanNumberReservations.CountAsync());

        (await Simular(Entrada())).IsSuccess.Should().BeTrue();

        _o.Db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).Should().BeEmpty();
        (await _o.Db.OperationKeys.CountAsync(), await _o.Db.CreditApplications.CountAsync(), await _o.Db.LoanNumberReservations.CountAsync())
            .Should().Be(antes);
        typeof(SimulateRepaymentScheduleQuery).GetInterfaces().Should().NotContain(typeof(IngenIA365ERP.Application.Common.Behaviors.IOperacionIdempotente),
            "simular es una consulta: no lleva clave (api.md §4.1)");
    }

    [Fact]
    public async Task La_huella_es_reproducible_y_cambia_con_lo_pedido()
    {
        var a = (await Simular(Entrada())).Value;
        var b = (await Simular(Entrada())).Value;
        var c = (await Simular(Entrada(monto: 12_000_001m))).Value;

        a.SimulationSha256.Should().Be(b.SimulationSha256);
        c.SimulationSha256.Should().NotBe(a.SimulationSha256);
    }

    [Theory]
    [InlineData(40_000_000, 36, "amount", 500000, 30000000)]
    [InlineData(100_000, 36, "amount", 500000, 30000000)]
    [InlineData(5_000_000, 61, "installments", 1, 60)]
    public async Task Lo_que_se_sale_de_la_linea_es_OutOfLineRange_con_campo_minimo_y_maximo(decimal monto, int cuotas, string campo, decimal min,
        decimal max)
    {
        var r = await Simular(Entrada(monto, cuotas));

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Schedule.OutOfLineRange");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("field").GetString().Should().Be(campo);
        datos.GetProperty("min").GetDecimal().Should().Be(min);
        datos.GetProperty("max").GetDecimal().Should().Be(max);
    }

    [Fact]
    public async Task Una_periodicidad_o_un_sistema_que_la_linea_no_admite_tambien_estan_fuera_de_rango()
    {
        var semanal = await Simular(Entrada() with { Periodicity = RepaymentPeriodicity.Weekly });
        semanal.Error.Code.Should().Be("Lending.Schedule.OutOfLineRange");
        OriginacionDePrueba.Datos(semanal.Error).GetProperty("field").GetString().Should().Be("periodicity");

        var abono = await Simular(Entrada() with { AmortizationSystem = AmortizationSystem.ConstantPrincipal });
        OriginacionDePrueba.Datos(abono.Error).GetProperty("field").GetString().Should().Be("amortizationSystem");

        var gracia = await Simular(Entrada() with { Grace = new ScheduleGraceInput(GraceKind.Principal, 2) });
        OriginacionDePrueba.Datos(gracia.Error).GetProperty("field").GetString().Should().Be("grace.kind");

        var extra = await Simular(Entrada() with { Extraordinary = [new ScheduleExtraordinaryInput([6], 500_000m)] });
        OriginacionDePrueba.Datos(extra.Error).GetProperty("field").GetString().Should().Be("extraordinary");
    }

    [Fact]
    public async Task Una_linea_cerrada_no_se_simula()
    {
        (await _o.L.Cerrar().Handle(new CloseCreditLineCommand(_o.Linea.PublicId, "Cierre de prueba") { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        _o.Db.ChangeTracker.Clear();

        var r = await Simular(Entrada());

        r.Error.Code.Should().Be("Lending.CreditLine.Closed");
    }

    [Fact]
    public async Task Una_linea_bloqueada_por_usura_responde_Usury_Exceeded()
    {
        var linea = await _o.Db.CreditLines.SingleAsync(l => l.PublicId == _o.Linea.PublicId);
        linea.BloquearPorUsura(OriginacionDePrueba.Hoy, Guid.NewGuid());
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();

        var r = await Simular(Entrada());

        r.Error.Code.Should().Be("Lending.Usury.Exceeded");
        OriginacionDePrueba.Datos(r.Error).GetProperty("usuryBlockedSince").GetString().Should().Be("2026-12-01");
    }

    [Fact]
    public async Task Una_efectiva_sobre_la_usura_no_es_error_es_un_aviso()
    {
        // La usura baja al 18 % desde enero: la línea al 20 % la supera en una simulación con desembolso en enero.
        await _o.L.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.18m, new DateOnly(2027, 1, 1));

        var r = await Simular(Entrada() with { DisbursementDate = new DateOnly(2027, 1, 5) });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Usury.Passes.Should().BeFalse();
        r.Value.Usury.Rate.Should().Be(0.18m);
        r.Value.Usury.Headroom.Should().BeNegative();
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Lending.Usury.Exceeded");
        r.Value.Warnings[0].Message.Should().NotContain("Lending.");
    }

    [Fact]
    public async Task Sin_usura_del_dia_es_NationalValues_Missing_y_alerta_a_quien_la_registra()
    {
        // La usura certificada rige sólo hasta el 31 de diciembre: un desembolso en enero no tiene usura del día.
        using var o = await OriginacionDePrueba.CrearAsync(activada: false, usuraHasta: new DateOnly(2026, 12, 31));

        var r = await o.Simular().Handle(new SimulateRepaymentScheduleQuery(
            new ScheduleSimulationInput(o.Linea.PublicId, 12_000_000m, 36, DisbursementDate: new DateOnly(2027, 1, 5))), default);

        r.Error.Code.Should().Be("NationalValues.Missing");
        o.L.Levantadas.Should().Contain(a => a.TypeCode == AlertasDeValoresNacionales.ValorNacionalFaltante
                                            && a.DedupKey == "Cartera.ValorNacionalFaltante:USURA:CONSUMO_Y_ORDINARIO:2027-01-05");
    }

    [Fact]
    public async Task Los_seguros_opcionales_van_solo_si_se_eligen()
    {
        var conSeguro = OriginacionDePrueba.VersionDeLinea() with
        {
            Insurances = [_o.L.Seguro(tarifa: 0.0005m) with { Mandatory = false }],
        };
        var linea = await _o.L.LineaAsync("CONSEG", conSeguro);

        var sin = await Simular(new ScheduleSimulationInput(linea.PublicId, 6_000_000m, 12));
        var con = await Simular(new ScheduleSimulationInput(linea.PublicId, 6_000_000m, 12, Insurances: ["vida"]));
        var otro = await Simular(new ScheduleSimulationInput(linea.PublicId, 6_000_000m, 12, Insurances: ["HOGAR"]));

        sin.Value.Totals.Insurance.Should().Be(0m);
        con.Value.Totals.Insurance.Should().BeGreaterThan(0m);
        con.Value.SimulationSha256.Should().NotBe(sin.Value.SimulationSha256);
        otro.Error.Code.Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task Una_linea_fuera_del_alcance_es_la_linea_inexistente()
    {
        _o.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;

        var r = await Simular(Entrada());

        r.Error.Code.Should().Be("Lending.CreditLine.NotFound");
    }
}
