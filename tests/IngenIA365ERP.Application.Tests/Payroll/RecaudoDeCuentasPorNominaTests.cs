using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll;

/// <summary>
/// Feature 014 (T089, T092; E-02, R18; contrato-de-cuentas §6.1): la nómina ordinaria como recaudador del contrato de cuentas. Calcular trae las
/// cuotas exigibles de aportes y de ahorro como novedades <c>AccountDeduction</c> (una por empleado y cuenta) con <c>DESC_APORTES</c> /
/// <c>DESC_AHORROS</c>, en la prelación aportes → créditos → ahorros; aprobar consigna lo descontado (<c>Installments</c>, clave
/// <c>NOM:{run}:{cuenta}</c>, fecha valor = fin del período) y lo no descontado cuota por cuota; un rechazo no bloquea (la línea baja y el neto sube)
/// salvo «no contabilizable»; reversar reversa cada consignación, todo o nada. (nuevo)
/// </summary>
public class RecaudoDeCuentasPorNominaTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
    private static readonly Guid Aportes = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Ahorro = new("00000000-0000-0000-0000-0000000000b2");

    private sealed record Escenario(NominaTestData D, Guid RunPublicId);

    private static async Task<Escenario> CalculadoAsync(Action<NominaTestData, Guid>? preparar = null)
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var persona = d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId;
        if (preparar is null)
        {
            d.Cuentas.Cuota(persona, Aportes, 40_000m, "2026-02", new DateOnly(2026, 2, 28));
            d.Cuentas.Cuota(persona, Aportes, 50_000m);
            d.Cuentas.Cuota(persona, Ahorro, 30_000m, naturaleza: NaturalezaDeCuenta.Deposit, concepto: WellKnownConceptCodes.SavingsDeduction, orden: 2);
        }
        else preparar(d, persona);
        var calc = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        return new Escenario(d, calc.Value.RunPublicId);
    }

    private static CalculatePayrollRunCommandHandler Calculador(NominaTestData d) =>
        new(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);

    private static ApprovePayrollRunCommandHandler Aprobador(NominaTestData d) =>
        new(d.Db, d.Contabilizador(Contadora), d.Policies, d.Permissions, d.Clock, Contadora,
            new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual), d.StaleMarker, d.Recaudo, d.Obligaciones, d.RecaudoDeCuentas);

    private static ReversePayrollRunCommandHandler Reversor(NominaTestData d) =>
        new(d.Db, d.Contabilizador(Contadora), d.Clock, Contadora,
            new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual), d.StaleMarker, d.Recaudo, d.RecaudoDeCuentas);

    [Fact]
    public async Task Calcular_deja_una_novedad_por_cuenta_con_su_concepto_y_una_fila_de_descuento()
    {
        var e = await CalculadoAsync();

        var novedades = await e.D.Db.PayrollNovelties.AsNoTracking().Where(n => n.Origin == NoveltyOrigin.AccountDeduction).ToListAsync();
        novedades.Should().HaveCount(2);
        novedades.Single(n => n.AccountPublicId == Aportes).Should().Match<Domain.Entities.Payroll.PayrollNovelty>(n =>
            n.ConceptCode == WellKnownConceptCodes.ContributionDeduction && n.Amount == 90_000m, "la cuota atrasada y la del mes, en una sola novedad");
        novedades.Single(n => n.AccountPublicId == Ahorro).ConceptCode.Should().Be(WellKnownConceptCodes.SavingsDeduction);
        e.D.Cuentas.Listados.Should().OnlyContain(l => l.Collector == Recaudador.Nomina && l.PeriodEnd == new DateOnly(2026, 3, 31));

        var filas = await e.D.Db.PayrollAccountDeductions.AsNoTracking().ToListAsync();
        filas.Select(f => (f.AccountPublicId, f.ConceptCode, f.ExpectedAmount, f.DeductedAmount)).Should().BeEquivalentTo(new[]
        {
            (Aportes, WellKnownConceptCodes.ContributionDeduction, 90_000m, 90_000m),
            (Ahorro, WellKnownConceptCodes.SavingsDeduction, 30_000m, 30_000m),
        });
        filas.Should().OnlyContain(f => f.PayrollRunLineId != null && f.Status == PayrollLoanDeductionStatus.Calculated);
    }

    [Fact]
    public async Task Recalcular_sin_cambios_no_duplica_y_si_la_Cartera_deja_de_informar_la_anula()
    {
        var e = await CalculadoAsync();
        (await Calculador(e.D).Handle(new CalculatePayrollRunCommand(e.D.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await e.D.Db.PayrollNovelties.CountAsync(n => n.Origin == NoveltyOrigin.AccountDeduction && n.Status == NoveltyStatus.Active)).Should().Be(2);

        e.D.Cuentas.Exigibles.RemoveAll(x => x.AccountPublicId == Ahorro);
        (await Calculador(e.D).Handle(new CalculatePayrollRunCommand(e.D.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var anulada = await e.D.Db.PayrollNovelties.SingleAsync(n => n.AccountPublicId == Ahorro);
        anulada.Status.Should().Be(NoveltyStatus.Cancelled);
        anulada.StatusReason.Should().Be(CuotasDeCuentasEnNomina.AnuladaPorCartera);
    }

    [Fact]
    public async Task Si_la_Cartera_no_responde_el_calculo_sigue_y_avisa()
    {
        var d = new NominaTestData();
        d.Cuentas.FallaAlListar = true;

        var r = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().Contain(CuotasDeCuentasEnNomina.AvisoCarteraNoResponde);
    }

    [Fact]
    public void La_prelacion_pone_los_aportes_antes_que_los_creditos_y_los_ahorros_despues()
    {
        // E-02 (decisión de K7): aportes → créditos → ahorros, ante la falta de la política PrelacionDescuentosDeCartera.
        static NoveltyInput N(string code, NoveltyOrigin origen) => new() { PublicId = Guid.NewGuid(), ConceptCode = code, Origin = origen, Amount = 1m };
        var orden = new[]
        {
            N(WellKnownConceptCodes.SavingsDeduction, NoveltyOrigin.AccountDeduction),
            N(WellKnownConceptCodes.LoanDeduction, NoveltyOrigin.LoanDeduction),
            N(WellKnownConceptCodes.ContributionDeduction, NoveltyOrigin.AccountDeduction),
        }.OrderBy(PayrollCalculationEngine.PrelacionDeCartera).Select(n => n.ConceptCode);
        orden.Should().Equal(WellKnownConceptCodes.ContributionDeduction, WellKnownConceptCodes.LoanDeduction, WellKnownConceptCodes.SavingsDeduction);
    }

    [Fact]
    public void La_prelacion_la_fija_la_politica_de_la_cooperativa()
    {
        // Decisión del dueño del 2026-10-18 (D37): PrelacionDescuentosDeCartera con vigencia; el defecto es aportes → créditos → ahorros.
        static NoveltyInput N(string code, NoveltyOrigin origen) => new() { PublicId = Guid.NewGuid(), ConceptCode = code, Origin = origen, Amount = 1m };
        var novedades = new[]
        {
            N(WellKnownConceptCodes.SavingsDeduction, NoveltyOrigin.AccountDeduction),
            N(WellKnownConceptCodes.LoanDeduction, NoveltyOrigin.LoanDeduction),
            N(WellKnownConceptCodes.ContributionDeduction, NoveltyOrigin.AccountDeduction),
            N("OTRA", NoveltyOrigin.Manual),
        };
        var politica = CarteraDeductionKinds.Leer(CompanyPolicyKeys.PrelacionDescuentosDeCarteraValores.CreditosAhorrosAportes)!;

        novedades.OrderBy(n => PayrollCalculationEngine.PrelacionDeCartera(n, politica)).Select(n => n.ConceptCode).Should().Equal(
            WellKnownConceptCodes.LoanDeduction, WellKnownConceptCodes.SavingsDeduction, WellKnownConceptCodes.ContributionDeduction, "OTRA");
        CompanyPolicyKeys.Buscar(CompanyPolicyKeys.PrelacionDescuentosDeCartera)!.Defecto.Should().Be("Aportes,Creditos,Ahorros");
        CarteraDeductionKinds.Leer("Aportes,Aportes,Ahorros").Should().BeNull();
        CarteraDeductionKinds.Leer("Aportes,Creditos").Should().BeNull();
        CompanyPolicyKeys.PrelacionDescuentosDeCarteraValores.Todos.Select(CarteraDeductionKinds.Leer).Should().OnlyContain(l => l != null && l.Count == 3);
    }

    [Fact]
    public async Task Aprobar_consigna_lo_descontado_por_el_contrato_de_cuentas_antes_del_NM()
    {
        var e = await CalculadoAsync();

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var lote = e.D.Cuentas.Consignaciones.Should().ContainSingle().Subject;
        lote.Collector.Should().Be(Recaudador.Nomina);
        lote.Form.Should().Be(FormaDeAplicacion.NonInteractive);
        lote.Deposits.Should().HaveCount(2).And.OnlyContain(c => c.Modality == ModalidadDeConsignacion.Installments
            && c.MeansClass == ClaseDeMedioDeRecaudo.OwnPayrollDeduction && c.ValueDate == new DateOnly(2026, 3, 31)
            && c.IdempotencyKey == RecaudoDeCuentasPorNomina.Clave(e.RunPublicId, c.AccountPublicId) && c.Receipt.DocumentKind == "NM"
            && c.Receipt.Number.Length <= RecaudoPorNomina.LargoDelRecibo);
        lote.NotDeducted.Should().BeEmpty();
        (await e.D.Db.PayrollAccountDeductions.AsNoTracking().ToListAsync()).Should().OnlyContain(f =>
            f.Status == PayrollLoanDeductionStatus.Applied && f.AppliedAmount == f.DeductedAmount && f.CarteraReceiptPublicId != null);
        r.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Lo_que_no_cabe_se_informa_cuota_por_cuota_dejando_pendientes_las_ultimas()
    {
        // Un salario que no alcanza: el tope deja parte de la cuota de aportes sin descontar.
        var e = await CalculadoAsync((d, persona) =>
        {
            d.Cuentas.Cuota(persona, Aportes, 600_000m, "2026-02", new DateOnly(2026, 2, 28));
            d.Cuentas.Cuota(persona, Aportes, 900_000m);
        });
        var fila = await e.D.Db.PayrollAccountDeductions.AsNoTracking().SingleAsync();
        fila.NotDeductedAmount.Should().BePositive();

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var lote = e.D.Cuentas.Consignaciones.Single();
        lote.Deposits.Single().Amount.Should().Be(fila.DeductedAmount);
        var marzo = e.D.Cuentas.Exigibles.Single(x => x.Period == "2026-03");
        var noDescontado = lote.NotDeducted.Should().ContainSingle(n => n.InstallmentPublicId == marzo.InstallmentPublicId).Subject;
        noDescontado.NotDeductedAmount.Should().Be(1_500_000m - fila.DeductedAmount, "la cobertura en orden de vencimiento deja pendiente la última");
        noDescontado.Reason.Should().Be(MotivoDeNoDescuento.DeductionCap);
        noDescontado.SourceItemPublicId.Should().Be(fila.PublicId);
    }

    [Fact]
    public async Task Un_rechazo_no_bloquea_la_linea_baja_y_el_neto_sube()
    {
        var e = await CalculadoAsync();
        e.D.Cuentas.ComoRespondeConsignar[Ahorro] = (ResultadoDelContrato.Rejected, "Lending.Accounts.Blocked", "La cuenta está bloqueada.");
        var antes = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId);

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Payroll.AccountDeductionNotApplied").Which.Message.Should().Contain("bloqueada");
        var rechazada = await e.D.Db.PayrollAccountDeductions.AsNoTracking().SingleAsync(f => f.AccountPublicId == Ahorro);
        rechazada.Status.Should().Be(PayrollLoanDeductionStatus.Rejected);
        rechazada.ResultCode.Should().Be("Lending.Accounts.Blocked");
        (await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId)).TotalNet.Should().Be(antes.TotalNet + 30_000m);
        (await e.D.Db.PayrollRunLines.AsNoTracking().SingleAsync(l => l.ConceptCode == WellKnownConceptCodes.SavingsDeduction)).Amount.Should().Be(0m);
    }

    [Fact]
    public async Task No_contabilizable_tumba_la_aprobacion_entera()
    {
        var e = await CalculadoAsync();
        e.D.Cuentas.ComoRespondeConsignar[Aportes] = (ResultadoDelContrato.Rejected, RecaudoPorNomina.CodigoNoContabilizable, "Falta la regla del rol AportesSociales.");

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.AccountDeductionNotPostable");
        r.Error.Message.Should().Contain("AportesSociales");
        (await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Reversar_la_nomina_reversa_cada_consignacion_y_si_una_falla_no_reversa_nada()
    {
        var e = await CalculadoAsync();
        (await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None)).IsSuccess.Should().BeTrue();

        e.D.Cuentas.RechazarReverso = true;
        var fallida = await Reversor(e.D).Handle(new ReversePayrollRunCommand(e.RunPublicId, "Se digitó mal"), CancellationToken.None);
        fallida.IsFailure.Should().BeTrue();
        fallida.Error.Code.Should().Be("Payroll.AccountReversalRejected");

        e.D.Cuentas.RechazarReverso = false;
        var r = await Reversor(e.D).Handle(new ReversePayrollRunCommand(e.RunPublicId, "Se digitó mal"), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        e.D.Cuentas.Reversos.Select(x => x.IdempotencyKey).Should().Contain(new[]
        {
            RecaudoDeCuentasPorNomina.Clave(e.RunPublicId, Aportes), RecaudoDeCuentasPorNomina.Clave(e.RunPublicId, Ahorro),
        });
        (await e.D.Db.PayrollAccountDeductions.AsNoTracking().ToListAsync()).Should().OnlyContain(f =>
            f.Status == PayrollLoanDeductionStatus.Reversed && f.CarteraReversalPublicId != null);
    }
}
