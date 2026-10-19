using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using M = IngenIA365ERP.Application.Tests.Lending.Deposits.Migration.MigracionDeCaptacionesDePrueba;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Migration;

/// <summary>
/// Feature 014, US9 (T155, T157; FR-114 a FR-116; api.md §16; data-model §7.2): la activación de aportes y captaciones <b>con carga</b> —corte + 1, plazo,
/// acta, cuadre recalculado y diferencias aceptadas— y lo que nace: cada cuenta con su saldo inicial por concepto fechado al corte y <b>sin hechos
/// contables</b> (US9-4), su titularidad, su control, las cuotas atrasadas con lo pagado, la marca y el uso del GMF y el acumulado tributario. El interés
/// migrado se liquida sin volver a causarse. (nuevo)
/// </summary>
public sealed class ActivacionConCargaDeCaptacionesTests : IAsyncLifetime
{
    private M _m = null!;

    public async Task InitializeAsync() => _m = await M.CrearAsync();

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    private async Task<MigrationLoad> AportesAplicadosAsync()
    {
        _m.SaldoContable = rol => rol == "AportesSociales" ? 2_500_000m : 0m;
        var carga = await _m.CargaAsync(LineNature.Contribution);
        await _m.CargarAsync(carga, P.ContributionAccounts, M.Aporte("1001", "AP-77"), M.Aporte("1002"));
        await _m.CargarAsync(carga, P.MemberAccountParties, M.Parte("AP-77", "1003", "Beneficiary", beneficiario: "100"));
        await _m.CargarAsync(carga, P.AccountInstallments,
            M.Fila((C.Cuenta, "AP-77"), (C.Periodo, "2026-10"), (C.FechaDeVencimiento, "2026-10-05"), (C.Valor, "50000"), (C.Pagado, "20000"),
                (C.MotivoNoDescontado, "InsufficientNet")),
            M.Fila((C.Cuenta, "AP-77"), (C.Periodo, "2026-11"), (C.FechaDeVencimiento, "2026-11-05"), (C.Valor, "50000")));
        var aplicada = await _m.AplicarAsync(carga);
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? $"{aplicada.Error.Code}: {aplicada.Error.Message}" : string.Empty);
        aplicada.Value.BalanceCheck.IsBalanced.Should().BeFalse("la carga suma 5.000.000 de aportes y Contabilidad 2.500.000");
        return await _m.RecargarAsync(carga);
    }

    private async Task AceptarTodoAsync(MigrationLoad carga, Guid punto)
    {
        var diferencias = await _m.Db.MigrationLoadDifferences.AsNoTracking().Where(d => d.MigrationLoadId == carga.Id && d.AcceptedAt == null)
            .Select(d => d.PublicId).ToListAsync();
        _m.G.ActuarComo(_m.Contadora);
        var r = await _m.Aceptador().Handle(new AcceptMigrationDifferencesCommand(carga.PublicId, diferencias, punto, "Diferencia conciliada con la contadora",
            DeCaptaciones: true) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _m.G.ActuarComo(_m.Jefe);
        _m.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Sin_aceptar_las_diferencias_no_se_activa_y_aceptadas_con_acta_si_US9_2()
    {
        var carga = await AportesAplicadosAsync();
        var punto = _m.Acta(carga.PublicId);

        (await _m.ActivarAsync(carga, punto)).Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciasSinAceptar);
        (await _m.Db.MemberAccounts.AnyAsync()).Should().BeFalse();

        await AceptarTodoAsync(carga, punto);
        var r = await _m.ActivarAsync(carga, punto);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Accounts.Should().Be(2);
        r.Value.Messages.Should().ContainSingle(x => x.Type == CaptacionesActivadasV1.Type);
        var activacion = await _m.Db.LendingActivations.AsNoTracking().SingleAsync(a => a.Nature == LineNature.Contribution);
        activacion.MigrationLoadId.Should().Be(carga.Id);
        activacion.SessionItemId.Should().NotBeNull();
        activacion.CutoffDate.Should().Be(M.Corte);
        activacion.ActivationDate.Should().Be(M.Hoy);
        (await _m.Db.LendingSetups.AsNoTracking().SingleAsync()).ContributionsActivationDate.Should().Be(M.Hoy);
        (await _m.RecargarAsync(carga)).Status.Should().Be(MigrationLoadStatus.Activated);
    }

    [Fact]
    public async Task Cada_cuenta_nace_con_su_saldo_inicial_por_concepto_al_corte_sin_hechos_contables_US9_4()
    {
        var carga = await AportesAplicadosAsync();
        var punto = _m.Acta(carga.PublicId);
        await AceptarTodoAsync(carga, punto);
        var mensajesAntes = await _m.Db.IntegrationMessages.CountAsync();

        (await _m.ActivarAsync(carga, punto)).IsSuccess.Should().BeTrue();

        var cuenta = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Number == "AP-77");
        cuenta.Origin.Should().Be(MemberAccountOrigin.Migration);
        cuenta.MigrationLoadId.Should().Be(carga.Id);
        cuenta.Status.Should().Be(MemberAccountStatus.Active);
        cuenta.OpenedOn.Should().Be(new DateOnly(2020, 1, 1), "conserva su vinculación");
        cuenta.ActivatedOn.Should().Be(M.Hoy);
        var sinNumero = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.HolderPersonId == _m.Cu.Beto.Id);
        sinNumero.Number.Should().StartWith("APORTES-", "sin número en la carga, el consecutivo de su línea");

        var filas = await _m.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cuenta.Id).ToListAsync();
        filas.Should().OnlyContain(t => t.Kind == MemberAccountTransactionKind.OpeningBalance && t.ValueDate == M.Corte && t.AccountingDate == M.Corte
                                        && t.MessagePublicId == null);
        filas.GroupBy(t => t.Concept).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount)).Should().BeEquivalentTo(new Dictionary<MemberAccountConcept, decimal>
        {
            [MemberAccountConcept.ContributionMinimum] = 500_000m,
            [MemberAccountConcept.ContributionOrdinary] = 2_000_000m,
        });
        var operacion = await _m.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.MemberAccountId == cuenta.Id);
        operacion.Kind.Should().Be(MemberAccountOperationKind.OpeningBalance);
        operacion.MigrationLoadId.Should().Be(carga.Id);
        (await _m.Db.IntegrationMessages.CountAsync()).Should().Be(mensajesAntes + 1, "sólo el informativo CaptacionesActivadas: ningún hecho de saldo");

        var control = await _m.Db.MemberAccountControls.AsNoTracking().SingleAsync(x => x.MemberAccountId == cuenta.Id);
        control.LastAccruedDate.Should().Be(M.Corte, "la marca de causación en el corte");

        var cuotas = await _m.Db.AccountInstallments.AsNoTracking().Where(q => q.MemberAccountId == cuenta.Id).OrderBy(q => q.PeriodMonth).ToListAsync();
        cuotas.Select(q => (q.PeriodMonth, q.Amount)).Should().Equal(((byte)10, 50_000m), ((byte)11, 50_000m));
        cuotas.Should().OnlyContain(q => q.GeneratedByOperationId == operacion.Id);
        filas.Where(t => t.InstallmentId == cuotas[0].Id).Sum(t => t.Amount).Should().Be(20_000m, "lo pagado de la cuota es parte del saldo inicial");
        (await _m.Db.AccountInstallmentNonDeductions.AsNoTracking().SingleAsync()).Reason.Should().Be(NonDeductionReason.InsufficientNet);

        var partes = await _m.Db.MemberAccountParties.AsNoTracking().Where(p => p.MemberAccountId == cuenta.Id).ToListAsync();
        partes.Select(p => (p.PersonId, p.Role, p.Share)).Should().BeEquivalentTo(new[]
        {
            (_m.Cu.Ana.Id, MemberAccountPartyRole.Holder, (decimal?)null),
            (_m.Cu.Carla.Id, MemberAccountPartyRole.Beneficiary, (decimal?)1m),
        });
    }

    [Fact]
    public async Task La_cuenta_de_ahorro_migrada_trae_su_control_su_canje_su_marca_del_GMF_y_su_acumulado_tributario()
    {
        _m.SaldoContable = rol => rol switch { "CapitalDeCaptacion" => 1_500_000m, "InteresPorPagar" => 3_210.5m, _ => 0m };
        var carga = await _m.CargaAsync(LineNature.Deposit);
        await _m.CargarAsync(carga, P.MemberAccounts,
            M.Ahorro("AH-500", capital: "1500000", interes: "3210,5", liquidadoHasta: "2026-10-31", canje: "200000"));
        await _m.CargarAsync(carga, P.GmfExemptions,
            M.Fila((C.Persona, "1001"), (C.CuentaExenta, "AH-500"), (C.VigenteDesde, "2024-01-15"), (C.ExentoUsadoDelMes, "350000")));
        await _m.CargarAsync(carga, P.TaxYearToDate,
            M.Fila((C.Titular, "1001"), (C.Cuenta, "AH-500"), (C.Anio, "2026"), (C.Concepto, "Interest"), (C.Valor, "40000")),
            M.Fila((C.Titular, "1001"), (C.Cuenta, "AH-500"), (C.Anio, "2026"), (C.Concepto, "Withholding"), (C.Valor, "2800"), (C.BaseDeRetencion, "40000")));
        var aplicada = await _m.AplicarAsync(carga);
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? $"{aplicada.Error.Code}: {aplicada.Error.Message}" : string.Empty);
        aplicada.Value.BalanceCheck.IsBalanced.Should().BeTrue("el capital y el interés cuadran con Contabilidad");

        var r = await _m.ActivarAsync(await _m.RecargarAsync(carga), _m.Acta(carga.PublicId));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var cuenta = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Number == "AH-500");
        var control = await _m.Db.MemberAccountControls.AsNoTracking().SingleAsync(x => x.MemberAccountId == cuenta.Id);
        control.LastAccruedDate.Should().Be(M.Corte);
        control.LastSettledThrough.Should().Be(new DateOnly(2026, 10, 31));

        var operaciones = await _m.Db.MemberAccountOperations.AsNoTracking().Where(o => o.MemberAccountId == cuenta.Id).OrderBy(o => o.Sequence).ToListAsync();
        operaciones.Should().HaveCount(2, "el saldo inicial y, aparte, lo que estaba en canje");
        operaciones[1].ClearingUntil.Should().NotBeNull();
        operaciones[1].Amount.Should().Be(200_000m);
        var filas = await _m.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cuenta.Id).ToListAsync();
        filas.Where(t => t.Concept == MemberAccountConcept.Principal).Sum(t => t.Amount).Should().Be(1_500_000m);
        filas.Where(t => t.Concept == MemberAccountConcept.InterestPayable).Sum(t => t.Amount).Should().Be(3_210.5m);

        var marca = await _m.Db.GmfExemptAccounts.AsNoTracking().SingleAsync();
        (marca.MemberAccountId, marca.ValidFrom).Should().Be((cuenta.Id, new DateOnly(2024, 1, 15)));
        var uso = await _m.Db.GmfMonthlyUsages.AsNoTracking().SingleAsync();
        (uso.Origin, uso.ExemptAmount, uso.Year, uso.Month).Should().Be((GmfMonthlyUsage.OrigenMigracion, 350_000m, (short)2026, (byte)11));
        var acumulado = await _m.Db.WithholdingAccumulations.AsNoTracking().SingleAsync();
        (acumulado.Origin, acumulado.GrossInterest, acumulado.WithholdingAmount, acumulado.WithholdingBase, acumulado.TaxYear)
            .Should().Be((WithholdingAccumulation.OrigenMigracion, 40_000m, 2_800m, 40_000m, (short)2026));

        // El interés migrado no se vuelve a causar: la ventana de la cuenta migrada arranca en el corte (research E121).
        var cuentaConVersion = await _m.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Id == cuenta.Id);
        LectorDeInteres.CorteDeMigracion(cuentaConVersion).Should().Be(M.Corte);
        var version = await _m.Db.DepositLineVersions.AsNoTracking().FirstAsync(v => v.CreditLineId == cuenta.CreditLineId);
        LectorDeInteres.InicioDeLaVentana(cuentaConVersion, control, version, M.Hoy).Should().Be(M.Corte);
    }

    [Fact]
    public async Task La_fecha_el_plazo_el_acta_y_la_naturaleza_se_exigen()
    {
        var carga = await AportesAplicadosAsync();
        var punto = _m.Acta(carga.PublicId);
        await AceptarTodoAsync(carga, punto);

        (await _m.ActivarAsync(carga, punto, M.Hoy.AddDays(-1))).Error.Code.Should().Be(ErroresDeActivacion.CodigoCorteInvalido, "corte + 1");
        (await _m.ActivarAsync(carga, _m.Acta(Guid.NewGuid()))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await _m.Activacion().ActivarAsync(LineNature.Deposit, carga.PublicId, M.Hoy, punto, true, "otra naturaleza", default)).Error.Code
            .Should().Be(ErroresDeActivacion.CodigoCargaNoEncontrada);

        _m.G.HoyLocal = M.Corte.AddDays(10);
        (await _m.ActivarAsync(carga, punto)).Error.Code.Should().Be(ErroresDeMigracion.CodigoPlazoVencido);
    }

    [Fact]
    public async Task Si_el_cuadre_cambia_al_activar_nace_otra_version_y_hay_que_volver_a_aceptar()
    {
        var carga = await AportesAplicadosAsync();
        var punto = _m.Acta(carga.PublicId);
        await AceptarTodoAsync(carga, punto);

        _m.SaldoContable = rol => rol == "AportesSociales" ? 3_000_000m : 0m;
        var r = await _m.ActivarAsync(carga, punto);

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciasSinAceptar);
        var recargada = await _m.RecargarAsync(carga);
        recargada.CurrentCheckVersion.Should().Be(2);
        recargada.IsBalanced.Should().BeFalse();
        (await _m.Db.MemberAccounts.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task El_estado_de_la_activacion_dice_la_carga_la_fecha_esperada_y_los_requisitos()
    {
        var carga = await AportesAplicadosAsync();

        var estado = await new GetDepositsActivationStatusQueryHandler(_m.Db, _m.Requisitos(), _m.G.Reloj)
            .Handle(new GetDepositsActivationStatusQuery(LineNature.Contribution), default);

        estado.Value.Status.Should().Be(GetDepositsActivationStatusQueryHandler.SinActivar);
        estado.Value.Load!.PublicId.Should().Be(carga.PublicId);
        estado.Value.ExpectedDate.Should().Be(M.Hoy);
        estado.Value.Deadline.Should().Be(M.Corte.AddDays(5));
        estado.Value.Requirements.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.CargaAplicada && q.Met);
        estado.Value.Requirements.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.DiferenciasAceptadas && !q.Met);
    }

    [Fact]
    public void El_interes_migrado_se_reparte_por_igual_entre_los_dias_que_cubre()
    {
        var dias = LiquidacionDeIntereses.RepartirInteresMigrado(100m, new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 3));

        dias.Select(d => (d.Dia, d.Interes)).Should().Equal(
            (new DateOnly(2026, 11, 1), 33.333333m), (new DateOnly(2026, 11, 2), 33.333333m), (new DateOnly(2026, 11, 3), 33.333334m));
        LiquidacionDeIntereses.RepartirInteresMigrado(0m, new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 3)).Should().BeEmpty();
    }
}
