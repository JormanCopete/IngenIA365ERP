using System.Globalization;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using M = IngenIA365ERP.Application.Tests.Lending.Deposits.Migration.MigracionDeCaptacionesDePrueba;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Migration;

/// <summary>
/// Feature 014, US6 (pendiente de la fase 10; plantillas-y-archivos.md §4.3; data-model §7.1; research E148): la migración de los CDAT vigentes al corte sobre la
/// mecánica de US9 —revisión que no guarda, aplicación todo o nada con el plan regenerado por <see cref="PlanDeCdat"/>, cuadre con el plazo de emisión y
/// nacimiento al activar con su versión 1 sellada (<c>Cause = Migration</c>), su plan y su saldo inicial, sin hechos—. El CDAT del escenario: 10.000.000 al
/// 10 % EA en base 360, 180 días desde el 2026-09-10 con pago mensual vencido: al corte (2026-11-30) van dos períodos pagados y 21 días causados del tercero.
/// </summary>
public sealed class MigracionDeCdatTests : IAsyncLifetime
{
    private static readonly DateOnly Inicio = new(2026, 9, 10);
    private M _m = null!;
    private Domain.Entities.Lending.Deposits.Configuration.DepositLineVersion _version = null!;

    public async Task InitializeAsync()
    {
        _m = await M.CrearAsync();
        _m.G.HoyLocal = M.DesdeLineas;
        await _m.Cu.LineaDeCaptacionAsync("CDAT", new DepositLineVersionInput
        {
            ValidFrom = M.DesdeLineas,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = new TermDepositLineInput
            {
                MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment, RepaymentPeriodicity.Monthly],
                Timings = [InterestTiming.Arrears, InterestTiming.Advance],
            },
        }, DepositClass.TermDeposit);
        _m.G.HoyLocal = M.Hoy;
        _m.Db.ChangeTracker.Clear();
        var linea = await _m.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Code == "CDAT");
        _version = await _m.Db.DepositLineVersions.AsNoTracking().SingleAsync(v => v.CreditLineId == linea.Id);
    }

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>El interés causado y no pagado al corte que espera el plan del CDAT del escenario.</summary>
    private decimal CausadoEsperado(InterestTiming modalidad = InterestTiming.Arrears)
    {
        var fila = new MigrationTermDepositRow
        {
            PrincipalAmount = 10_000_000m, AppliedAnnualRate = 0.10m, StartDate = Inicio, TermDays = 180, InterestPeriodicity = RepaymentPeriodicity.Monthly,
            InterestTiming = modalidad,
        };
        return CdatMigrado.EsperadoAlCorte(PlanDeCdat.Generar(CdatMigrado.Condiciones(fila, _version), CdatMigrado.SinFestivos), M.Corte);
    }

    private static string Texto(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', ',');

    private static Dictionary<string, string?> Cdat(string numero, string causado, string destino = "AH-1", string titular = "1001", string pagados = "2",
        string linea = "CDAT", string vencimiento = "2027-03-09", string? plazoDeEmision = null, string modalidad = "Arrears") => M.Fila(
        (C.Numero, numero), (C.Titular, titular), (C.Oficina, "PRINCIPAL"), (C.Linea, linea), (C.NumeroDeTitulo, "T-" + numero), (C.Capital, "10000000"),
        (C.FechaDeConstitucion, "2026-03-14"), (C.FechaDeInicioVigente, "2026-09-10"), (C.PlazoDias, "180"), (C.FechaDeVencimiento, vencimiento),
        (C.TasaPactadaEA, "10"), (C.PeriodicidadDeIntereses, "Monthly"), (C.Modalidad, modalidad), (C.PeriodosPagados, pagados),
        (C.InteresCausadoNoPagado, causado), (C.DestinoDeIntereses, "Account"), (C.CuentaDestino, destino), (C.InstruccionAlVencimiento, "Rollover"),
        (C.ProrrogasHechas, "1"), (C.PlazoDeEmision, plazoDeEmision));

    private async Task<MigrationLoad> CargaConCdatAsync(string causado, string destino = "AH-1", string pagados = "2")
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        await _m.CargarAsync(carga, P.MemberAccounts, M.Ahorro("AH-1", capital: "1000000"), M.Ahorro("AH-2", titular: "1002", capital: "500000"));
        await _m.CargarAsync(carga, P.TermDeposits, Cdat("CD-1", causado, destino, pagados: pagados));
        return carga;
    }

    [Fact]
    public async Task La_parte_de_CDAT_esta_disponible_con_K9_y_revisar_no_guarda()
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        _m.Archivo(P.TermDeposits, Cdat("CD-1", Texto(CausadoEsperado())));

        var revisada = await _m.ImportarAsync(carga, P.TermDeposits, ModoDeImportacion.Review);

        revisada.IsSuccess.Should().BeTrue(revisada.IsFailure ? $"{revisada.Error.Code}: {revisada.Error.Message}" : string.Empty);
        M.Errores(revisada).Should().BeEmpty();
        (await _m.Db.MigrationTermDepositRows.AnyAsync()).Should().BeFalse("revisar no guarda");

        _m.Entrega = Domain.Lending.EntregaDeCartera.K8;
        (await _m.ImportarAsync(carga, P.TermDeposits)).Error.Code.Should().Be(ErroresDeMigracionDeCaptaciones.CodigoParteNoDisponible);
    }

    [Fact]
    public async Task Una_fila_mal_formada_lista_fila_y_columna_y_no_guarda_nada()
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        _m.Archivo(P.TermDeposits,
            Cdat("CD-1", "100", linea: "AHVISTA"),
            Cdat("CD-2", "100", vencimiento: "2026-11-15"),
            Cdat("CD-3", "100", plazoDeEmision: "UnderSixMonths"));

        var r = await _m.ImportarAsync(carga, P.TermDeposits);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = M.Errores(r);
        errores.Should().Contain(e => e.Row == 2 && e.Column == C.Linea);
        errores.Should().Contain(e => e.Row == 3 && e.Column == C.FechaDeVencimiento);
        errores.Should().Contain(e => e.Row == 4 && e.Column == C.PlazoDeEmision);
        (await _m.Db.MigrationTermDepositRows.AnyAsync()).Should().BeFalse("todo o nada");
    }

    [Fact]
    public async Task Aplicar_compara_el_plan_regenerado_los_periodos_pagados_y_la_cuenta_destino()
    {
        var carga = await CargaConCdatAsync(Texto(CausadoEsperado() + 5m), destino: "AH-2", pagados: "3");

        var r = await _m.AplicarAsync(carga);

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoErroresEntrePartes);
        var errores = M.ErroresEntrePartes(r.Error);
        errores.Should().Contain(e => e.Part == P.TermDeposits && e.Column == C.InteresCausadoNoPagado && e.Code == CdatMigrado.CodigoPlanDistinto);
        errores.Should().Contain(e => e.Part == P.TermDeposits && e.Column == C.PeriodosPagados && e.Code == CdatMigrado.CodigoPlanDistinto);
        errores.Should().Contain(e => e.Part == P.TermDeposits && e.Column == C.CuentaDestino && e.Code == "Lending.TermDeposit.DestinationInvalid",
            "AH-2 es de otro titular");
    }

    [Fact]
    public async Task Al_activar_nace_el_CDAT_con_su_version_su_plan_y_su_saldo_inicial_sin_hechos()
    {
        var causado = CausadoEsperado();
        _m.SaldoContable = rol => rol switch { "CapitalDeCaptacion" => 11_500_000m, "InteresPorPagar" => causado, _ => 0m };
        var carga = await CargaConCdatAsync(Texto(causado));
        var aplicada = await _m.AplicarAsync(carga);
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? $"{aplicada.Error.Code}: {aplicada.Error.Message}" : string.Empty);
        aplicada.Value.BalanceCheck.IsBalanced.Should().BeTrue("el capital de las tres cuentas y el interés del CDAT cuadran con Contabilidad");

        var r = await _m.ActivarAsync(await _m.RecargarAsync(carga), _m.Acta(carga.PublicId));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var cdat = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Number == "CD-1");
        var destino = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Number == "AH-1");
        (cdat.DepositClass, cdat.Origin, cdat.Status, cdat.OpenedOn).Should()
            .Be((DepositClass.TermDeposit, MemberAccountOrigin.Migration, MemberAccountStatus.Active, new DateOnly(2026, 3, 14)));

        var version = await _m.Db.MemberAccountTermsVersions.AsNoTracking().SingleAsync(v => v.MemberAccountId == cdat.Id);
        (version.VersionNumber, version.Cause, version.StartDate, version.MaturityDate, version.AppliedAnnualRate, version.RolloverCount)
            .Should().Be((1, AccountTermsCause.Migration, Inicio, new DateOnly(2027, 3, 9), 0.10m, 1));
        (version.IssueTermBand, version.DestinationAccountId, version.InterestDestination)
            .Should().Be((IssueTermBand.SixToTwelve, destino.Id, InterestDestination.ToAccount));
        var plan = await _m.Db.TermDepositScheduleItems.AsNoTracking().Where(p => p.TermsVersionId == version.Id).OrderBy(p => p.PeriodNumber).ToListAsync();
        plan.Should().HaveCount(6);
        plan.Should().OnlyContain(p => p.Interest == 79_741m);

        var filas = await _m.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cdat.Id).ToListAsync();
        filas.Should().OnlyContain(t => t.Kind == MemberAccountTransactionKind.OpeningBalance && t.TermsVersionId == version.Id && t.MessagePublicId == null);
        filas.Single(t => t.Concept == MemberAccountConcept.Principal).Amount.Should().Be(10_000_000m);
        var interes = filas.Single(t => t.Concept == MemberAccountConcept.InterestPayable);
        (interes.Amount, interes.SchedulePeriodNumber).Should().Be((causado, 3));

        var control = await _m.Db.MemberAccountControls.AsNoTracking().SingleAsync(x => x.MemberAccountId == cdat.Id);
        control.LastAccruedDate.Should().Be(M.Corte);
        var leido = await new LectorDeCdat(_m.Db, Substitute()).LeerAsync(cdat, default);
        var pagados = await PiezasDeCdat.PeriodosPagadosAsync(_m.Db, cdat, PlanDeCdat.Generar(LectorDeCdat.Condiciones(leido.Vigente!), CdatMigrado.SinFestivos), default);
        pagados.Should().BeEquivalentTo([1, 2], "los períodos con fecha de pago hasta el corte los pagó el sistema anterior");
    }

    private static IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil Substitute()
    {
        var calendario = NSubstitute.Substitute.For<IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil>();
        NSubstitute.SubstituteExtensions.Returns(calendario.ObtenerAsync(NSubstitute.Arg.Any<DateOnly>(), NSubstitute.Arg.Any<DateOnly>(),
            NSubstitute.Arg.Any<IReadOnlyCollection<DayOfWeek>>(), NSubstitute.Arg.Any<DateTime?>(), NSubstitute.Arg.Any<CancellationToken>()),
            Task.FromResult(CdatMigrado.SinFestivos));
        return calendario;
    }
}
