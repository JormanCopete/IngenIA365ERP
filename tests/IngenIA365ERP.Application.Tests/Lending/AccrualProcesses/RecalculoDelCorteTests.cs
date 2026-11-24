using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Lending.AccrualProcesses;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Lending.Activation;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.AccrualProcesses;

/// <summary>
/// Feature 025, fase 3 (HU1 + HU3; FR-001–FR-006; D1, D2): el recálculo de la carga desde el corte sobre la cartera activada de
/// <see cref="ActivacionConCarteraDePrueba"/> (corte el 2026-12-01, registro tres días después): <c>CR-200</c> de Beto, cuya cuota 1 vence dos días después
/// del corte y nació un mes antes (le faltan los 30 días del desembolso al corte); <c>CO-300</c> castigado y <c>CR-100</c> de Ana con la causación
/// suspendida.
/// <list type="bullet">
/// <item>el borrador lista lo de cada crédito —el castigado y el suspendido fuera con su motivo— y no escribe nada;</item>
/// <item>en firme pide la aprobación de otra persona, carga el interés que faltó en la primera cuota sin vencer (la 2: la 1 ya venció) sin mora ni efecto en
/// la altura, sella la tasa si corresponde, deja el plan nuevo y la marca, y no se repite ni se reprocesa;</item>
/// <item>la causación y la verificación de la noche no lo revierten: el libro sigue igual a la reconstrucción.</item>
/// </list>
/// </summary>
public sealed class RecalculoDelCorteTests : IAsyncLifetime
{
    private ActivacionConCarteraDePrueba _a = null!;
    private CausacionDePrueba _c = null!;
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IAutoridadDeOtroAprobador _otroAprobador = Substitute.For<IAutoridadDeOtroAprobador>();

    public async Task InitializeAsync()
    {
        _a = await ActivacionConCarteraDePrueba.CrearAsync();
        await _a.AplicarAsync();
        await _a.AceptarTodoAsync();
        var activada = await _a.ActivarAsync(_a.Orden(_a.Acta()));
        activada.IsSuccess.Should().BeTrue(activada.IsFailure ? $"{activada.Error.Code}: {activada.Error.Message}" : string.Empty);
        _c = CausacionDePrueba.Sobre(_a.D);
        await _c.SembrarTiposDeHechoAsync();
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _otroAprobador.TienePermisoAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _otroAprobador.AlcanceAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _a.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _a.Dispose();
        return Task.CompletedTask;
    }

    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => _a.Db;

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private RecalculoDelCorte Clase() => new(Db, _c.Cargador(), _c.Partes(), _c.D.Registro(), _c.D.O.Cerrojo, _c.D.FechaContable(), _c.D.G.Actor,
        _c.D.G.Reloj);

    private ProcesosDeCausacion Procesos() => new(Db, [Clase()], _c.D.G.Alcance, _c.D.Series(), _c.D.O.Cerrojo, _c.D.Numerador(), _c.D.G.Auditoria,
        _c.D.G.Reloj);

    private MotorDeAprobaciones Motor(ProcesosDeCausacion procesos)
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var alcance = Substitute.For<IAlcanceDeInventario>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        var fuente = new FuenteDeAprobacionDeCausacion(Db, new PersonasDeLosUsuarios(Db), procesos);
        return new MotorDeAprobaciones(Db, _c.D.G.Actor, _permisos, alcance, limites, _otroAprobador, Substitute.For<IAvisosDeAprobacion>(),
            new VistaDeSolicitudes(Db, [fuente]), _c.D.G.Reloj);
    }

    private async Task<Result<AccrualProcessDto>> ArmarAsync()
    {
        var procesos = Procesos();
        var hoy = _c.D.G.HoyLocal;
        var r = await new CreateAccrualProcessCommandHandler(Db, procesos,
                new EntradaDeProcesoDeCausacion(Db, procesos, new CausacionPorContabilizar(Db), _c.D.G.Parametros(), _c.D.G.Reloj), _c.D.Numerador(),
                _c.D.G.Actor)
            .Handle(new CreateAccrualProcessCommand(AccrualProcessPeriodicity.Daily, null, hoy, hoy, [AccrualProcessConcepts.CurrentInterest],
                Kind: AccrualProcessKind.CutoverRecalculation) { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<AccrualProcessDto> ArmadoAsync()
    {
        var r = await ArmarAsync();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<IReadOnlyList<AccrualProcessLineDto>> LineasAsync(Guid proceso)
    {
        var r = await new ListAccrualProcessLinesQueryHandler(Db, Procesos(), _c.D.G.Alcance)
            .Handle(new ListAccrualProcessLinesQuery(proceso, PageSize: 200), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value.Items;
    }

    private async Task<Result<AccrualProcessDto>> PedirFirmaAsync(AccrualProcessDto p)
    {
        var procesos = Procesos();
        var r = await new ConfirmAccrualProcessCommandHandler(Db, procesos, Motor(procesos), _c.D.G.Permisos, _c.D.G.Actor, _c.D.G.Reloj)
            .Handle(new ConfirmAccrualProcessCommand(p.PublicId, "Recálculo de la carga de COOFLOPAL desde el corte", p.ContentSha256!)
                { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<DecisionResultDto>> DecidirAsync(AccrualProcessDto p, User? presente)
    {
        var r = await Motor(Procesos()).DecidirAsync(new DecisionDeAprobacion(p.Approval!.RequestPublicId, ApprovalDecisionKind.Approve, null,
            p.Approval.ContentSha256, presente is null ? null : new AprobadorPresente(presente.Id, presente.Email!, ApprovalMethod.InPersonTotp, null)), default);
        Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<AccrualProcessDto> LeerAsync(Guid proceso)
    {
        var r = await new GetAccrualProcessQueryHandler(Procesos()).Handle(new GetAccrualProcessQuery(proceso), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<AccrualProcessDto> EnFirmeAsync(AccrualProcessDto borrador)
    {
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var pedida = await PedirFirmaAsync(borrador);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        var propia = await DecidirAsync(pedida.Value, _c.D.O.Asesora);
        propia.IsSuccess.Should().BeFalse("quien pidió la firma no la aprueba (cuatro ojos)");
        var aprobada = await DecidirAsync(pedida.Value, _c.D.O.Analista);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        return await LeerAsync(borrador.PublicId);
    }

    private Task<Domain.Entities.Lending.Loans.Loan> CreditoAsync(string numero) => Db.Loans.AsNoTracking().SingleAsync(l => l.Number == numero);

    // ------------------------------------------------------------------------------------------------------------ pruebas --

    [Fact]
    public async Task El_borrador_lista_lo_de_cada_credito_cargado_y_no_escribe_nada()
    {
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var p = await ArmadoAsync();

        p.Kind.Should().Be(AccrualProcessKind.CutoverRecalculation);
        p.CanConfirm.Should().BeTrue(p.Issues.Count > 0 ? string.Join(" | ", p.Issues.Select(i => i.Message)) : string.Empty);
        var lineas = await LineasAsync(p.PublicId);
        var beto = (await CreditoAsync("CR-200")).PublicId;
        var delCorte = lineas.Single(l => l.LoanPublicId == beto && l.FactType == InteresDelCorteV1.Type);
        delCorte.Status.Should().Be(AccrualProcessItemStatus.Included, $"motivo {delCorte.Reason}; {string.Join(" | ", lineas.Select(l => $"{l.LoanNumber} {l.FactType} {l.Status} {l.Reason} {l.Amount}"))}");
        delCorte.Cutover.Should().NotBeNull();
        var d = delCorte.Cutover!;
        d.CutoffDate.Should().Be(ActivacionConCarteraDePrueba.Corte);
        d.CutoffInstallment.Should().Be(1);
        d.PeriodStart.Should().Be(ActivacionConCarteraDePrueba.Corte.AddDays(-30), "la cuota 1 empezó con el desembolso");
        d.MissingDays.Should().Be(30, "del desembolso (01-11) al corte (01-12) en 30/360");
        d.PrincipalAtCutoff.Should().Be(1_200_000m);
        d.Adjustment.Should().Be(delCorte.Amount).And.BeGreaterThan(1m);
        (d.FullPeriodInterest - d.AlreadyAccrued - d.LoadedInterest).Should().Be(d.Adjustment);
        delCorte.InstallmentNumber.Should().Be(2, "la cuota 1 venció el 03-12, antes de la firma: el ajuste va a la primera sin vencer, sin mora");
        var castigado = (await CreditoAsync("CO-300")).PublicId;
        lineas.Where(l => l.LoanPublicId == castigado).Should().OnlyContain(l => l.Reason == AccrualExclusionReason.WrittenOff, "el castigado queda fuera");

        (await Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.CutoverInterestAdjustment)).Should().BeFalse("el borrador no escribe");
        (await Db.LoanTermsVersions.AnyAsync(t => t.Cause == TermsVersionCause.CutoverCorrection)).Should().BeFalse();
        (await Db.LoanControls.AnyAsync(c => c.CutoverAdjustedAt != null)).Should().BeFalse();
        (await Db.IntegrationMessages.AnyAsync(m => m.Type == InteresDelCorteV1.Type)).Should().BeFalse();
    }

    [Fact]
    public async Task En_firme_pide_otra_persona_carga_el_interes_sin_mora_y_no_se_repite()
    {
        var credito = await CreditoAsync("CR-200");
        var hoy = _c.D.G.HoyLocal;
        var antes = MotorDeCartera.Reconstruir((await _c.Cargador().CargarAsync(credito.Id, null, hoy, default))!.Entrada!, hoy);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var borrador = await ArmadoAsync();
        var linea = (await LineasAsync(borrador.PublicId)).Single(l => l.LoanPublicId == credito.PublicId && l.FactType == InteresDelCorteV1.Type);

        var enFirme = await EnFirmeAsync(borrador);

        enFirme.Status.Should().Be(AccrualProcessStatus.Confirmed);
        var fila = await Db.LoanTransactions.AsNoTracking().SingleAsync(t => t.LoanId == credito.Id && t.Kind == LoanTransactionKind.CutoverInterestAdjustment);
        (fila.Concept, fila.InstallmentNumber, fila.Amount, fila.ValueDate).Should().Be((LoanConcept.CurrentInterest, (int?)2, linea.Amount, hoy));
        fila.ProcessRunId.Should().NotBeNull("la escribe la corrida del proceso");
        (await Db.LoanControls.AsNoTracking().SingleAsync(c => c.LoanId == credito.Id)).CutoverAdjustedAt.Should().NotBeNull();
        (await Db.IntegrationMessages.CountAsync(m => m.Type == InteresDelCorteV1.Type)).Should().Be(1);
        if (linea.Cutover!.ProposedMonthlyRate is { } mv)
        {
            var corregida = await Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == credito.Id && t.Cause == TermsVersionCause.CutoverCorrection);
            (corregida.RateBasis, corregida.MonthlyRate, corregida.EffectiveValueDate).Should().Be((RateBasis.MonthlyNominal, (decimal?)mv, ActivacionConCarteraDePrueba.Corte));
        }
        var plan = await Db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == credito.Id).OrderByDescending(v => v.VersionNumber).FirstAsync();
        plan.IsMigratedPlan.Should().BeFalse("el plan nuevo es otra versión; el migrado queda como estaba");
        var cuota2 = await Db.RepaymentInstallments.AsNoTracking().SingleAsync(i => i.ScheduleVersionId == plan.Id && i.InstallmentNumber == 2);
        cuota2.Principal.Should().Be(300_000m, "el capital del plan no cambia");

        // Sin mora ni efecto en la altura: el interés del corte está en una cuota que no ha vencido.
        var despues = MotorDeCartera.Reconstruir((await _c.Cargador().CargarAsync(credito.Id, null, hoy, default))!.Entrada!, hoy);
        despues.AlturaDeMora.Dias.Should().Be(antes.AlturaDeMora.Dias);
        despues.Saldo(LoanConcept.DefaultInterest).Should().Be(antes.Saldo(LoanConcept.DefaultInterest));
        despues.Saldo(LoanConcept.CurrentInterest, 2).Should().Be(antes.Saldo(LoanConcept.CurrentInterest, 2) + linea.Amount);
        despues.InteresFueraDelPlan.GetValueOrDefault(2).Should().Be(linea.Amount, "la cuota 2 vale al vencer lo del plan más el interés del corte");

        // No se repite ni se reprocesa.
        var otro = await ArmarAsync();
        if (otro.IsSuccess)
            (await LineasAsync(otro.Value.PublicId)).Single(l => l.LoanPublicId == credito.PublicId && l.FactType == InteresDelCorteV1.Type)
                .Reason.Should().Be(AccrualExclusionReason.AlreadyAdjusted);
        var reproceso = await new ReprocessAccrualProcessCommandHandler(Db, Procesos(), _c.D.Numerador(), _c.D.G.Actor)
            .Handle(new ReprocessAccrualProcessCommand(borrador.PublicId, "otra vez") { OperationKey = Guid.NewGuid() }, default);
        reproceso.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoSinReproceso);
    }

    [Fact]
    public async Task La_causacion_y_la_verificacion_de_la_noche_no_lo_revierten()
    {
        var credito = await CreditoAsync("CR-200");
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var borrador = await ArmadoAsync();
        var linea = (await LineasAsync(borrador.PublicId)).Single(l => l.LoanPublicId == credito.PublicId && l.FactType == InteresDelCorteV1.Type);
        await EnFirmeAsync(borrador);

        var hoy = _c.D.G.HoyLocal;
        await _c.CausarHastaAsync(hoy.AddDays(1));
        Db.ChangeTracker.Clear();

        var libro = await _c.Cargador().LibroAsync([credito.Id], hoy.AddDays(1), default);
        var entrada = (await _c.Cargador().CargarAsync(credito.Id, null, hoy.AddDays(1), default))!.Entrada!;
        MotorDeCartera.Verificar(entrada, (await _c.ControlAsync(credito.Id)).LastAccruedDate, libro[credito.Id]).Should()
            .BeEmpty("el libro sigue igual a la reconstrucción: el ajuste y la tasa del corte son su entrada");
        (await Db.LoanTransactions.AsNoTracking()
                .Where(t => t.LoanId == credito.Id && t.Concept == LoanConcept.CurrentInterest && t.InstallmentNumber == 2 && t.Kind != LoanTransactionKind.Accrual)
                .SumAsync(t => t.Amount))
            .Should().Be(linea.Amount, "nada revirtió el interés del corte");
    }
}
