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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll;

/// <summary>
/// Feature 013 (T0111, T0112; R106, FR-112, T92, G3, C32, C56, A9): la nómina ordinaria como pagaduría interna de Cartera. Aprobar
/// entrega a Cartera por el contrato de obligaciones (<see cref="ContratoDeObligacionesDePrueba"/>) lo descontado
/// (<c>CycleInstallment</c>) y lo no descontado en la misma llamada, <b>antes</b> del NM, con fecha valor = fin del período; un
/// rechazo o un excedente no bloquea: la línea baja a lo aplicado, el neto sube y se avisa; reversar reversa por el contrato, todo
/// o nada. La consulta <c>loan-deductions</c> lo muestra.
/// </summary>
public class RecaudoPorNominaTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private sealed record Escenario(NominaTestData D, Guid RunPublicId, Guid Cabe, Guid NoCabe);

    /// <summary>Marzo con dos cuotas de Ana: una de 150.000 que cabe y otra de 2.500.000 que sólo cabe en parte.</summary>
    private static async Task<Escenario> CalculadoAsync()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var persona = d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId;
        var cabe = new Guid("00000000-0000-0000-0000-00000000000a");
        var noCabe = new Guid("00000000-0000-0000-0000-000000000001");
        d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, cabe, "A-1", "CONSUMO", 3, new DateOnly(2026, 3, 31), 150_000m, 0m, 1));
        d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, noCabe, "B-1", "CONSUMO", 9, new DateOnly(2026, 3, 31), 2_500_000m, 0m, 2));
        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        return new Escenario(d, calc.Value.RunPublicId, cabe, noCabe);
    }

    private static ApprovePayrollRunCommandHandler Aprobador(NominaTestData d) =>
        new(d.Db, d.Contabilizador(Contadora), d.Policies, d.Permissions, d.Clock, Contadora,
            new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual), d.StaleMarker, d.Recaudo, d.Obligaciones, d.RecaudoDeCuentas);

    private static ReversePayrollRunCommandHandler Reversor(NominaTestData d) =>
        new(d.Db, d.Contabilizador(Contadora), d.Clock, Contadora,
            new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual), d.StaleMarker, d.Recaudo, d.RecaudoDeCuentas);

    [Fact]
    public async Task Aprobar_entrega_a_Cartera_lo_descontado_y_lo_no_descontado_en_una_llamada_antes_del_NM()
    {
        var e = await CalculadoAsync();
        var filas = await e.D.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync();

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var lote = e.D.Contrato.Aplicaciones.Should().ContainSingle().Subject;
        lote.Collector.Should().Be(Recaudador.Nomina);
        lote.Form.Should().Be(FormaDeAplicacion.NonInteractive);
        lote.Payments.Select(p => p.ObligationPublicId).Should().Equal(e.NoCabe, e.Cabe);
        lote.Payments.Should().OnlyContain(p => p.Modality == ModalidadDePago.CycleInstallment && p.MeansClass == ClaseDeMedioDeRecaudo.OwnPayrollDeduction
                                                && p.ValueDate == new DateOnly(2026, 3, 31) && p.IdempotencyKey == RecaudoPorNomina.Clave(e.RunPublicId, p.ObligationPublicId)
                                                && p.Receipt.DocumentKind == "NM");
        lote.Payments.Single(p => p.ObligationPublicId == e.NoCabe).Amount.Should().Be(1_044_548m - 150_000m, "lo que cupo después de la primera cuota");
        var noDescontado = lote.NotDeducted.Should().ContainSingle().Subject;
        noDescontado.ObligationPublicId.Should().Be(e.NoCabe);
        noDescontado.Reason.Should().Be(MotivoDeNoDescuento.DeductionCap);
        noDescontado.ExpectedAmount.Should().Be(2_500_000m);
        noDescontado.NotDeductedAmount.Should().Be(2_500_000m - (1_044_548m - 150_000m));
        noDescontado.SourceItemPublicId.Should().Be(filas.Single(f => f.ObligationPublicId == e.NoCabe).PublicId, "C32: la fila que lo informa");
        noDescontado.IdempotencyKey.Should().Be(RecaudoPorNomina.Clave(e.RunPublicId, e.NoCabe));

        var despues = await e.D.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync();
        despues.Should().OnlyContain(f => f.Status == PayrollLoanDeductionStatus.Applied && f.CarteraReceiptPublicId != null && f.AppliedAmount == f.DeductedAmount);
        r.Value.Warnings.Should().BeEmpty();
        (await e.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == e.RunPublicId)).Status.Should().Be(PayrollRunStatus.Approved);
    }

    [Fact]
    public async Task Un_rechazo_o_un_excedente_no_bloquea_la_linea_baja_a_lo_aplicado_el_neto_sube_y_se_avisa()
    {
        var e = await CalculadoAsync();
        e.D.Contrato.ComoRespondeAplicar[e.Cabe] = ContratoDeObligacionesDePrueba.Rechazo();
        e.D.Contrato.ComoRespondeAplicar[e.NoCabe] = new(ResultadoDelContrato.Applied, Excedente: 94_548m);
        var antes = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId);

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue("FR-112: un rechazo o un excedente no impide aprobar: " + r.Error.Message);
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Payroll.LoanDeductionNotApplied").Which.Message.Should().Contain("A-1").And.Contain("B-1");
        var filas = await e.D.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync();
        var rechazada = filas.Single(f => f.ObligationPublicId == e.Cabe);
        rechazada.Status.Should().Be(PayrollLoanDeductionStatus.Rejected);
        rechazada.AppliedAmount.Should().Be(0m);
        rechazada.ResultCode.Should().Be("Lending.Payment.ObligationNotPayable");
        rechazada.ResultMessage.Should().Contain("ya no recibe pagos");
        var parcial = filas.Single(f => f.ObligationPublicId == e.NoCabe);
        parcial.Status.Should().Be(PayrollLoanDeductionStatus.PartiallyApplied);
        parcial.AppliedAmount.Should().Be(parcial.DeductedAmount - 94_548m);

        var lineas = await e.D.Db.PayrollRunLines.AsNoTracking().Where(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction).ToListAsync();
        lineas.Sum(l => l.Amount).Should().Be(parcial.AppliedAmount!.Value, "cada línea DESC_CARTERA baja a lo que Cartera tomó");
        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId);
        run.TotalNet.Should().Be(antes.TotalNet + 150_000m + 94_548m, "el neto del empleado sube por lo que Cartera no tomó");
        run.Status.Should().Be(PayrollRunStatus.Approved);
    }

    [Fact]
    public async Task Un_descuento_que_Contabilidad_no_puede_registrar_tumba_la_aprobacion_entera_y_dice_que_regla_falta()
    {
        // Feature 013, T0801 (A8 = «Bloquear todo», respuesta del dueño del 2026-10-14): con Contabilidad.FaltaDeRegla = Bloquear el contrato rechaza
        // el pago con Lending.Prevalidation.NotPostable; la ordinaria no baja la línea como con otro rechazo: no se aprueba.
        var e = await CalculadoAsync();
        e.D.Contrato.ComoRespondeAplicar[e.Cabe] = ContratoDeObligacionesDePrueba.Rechazo(RecaudoPorNomina.CodigoNoContabilizable,
            "Contabilidad no tiene cómo registrar PagoAplicado en el rol RecaudoPorAplicar. Pida a la contadora completar la matriz de Cartera.");

        var r = await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.LoanDeductionNotPostable");
        r.Error.Message.Should().Contain("A-1").And.Contain("RecaudoPorAplicar").And.Contain("contadora");
        (await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == e.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
        (await e.D.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync()).Should().OnlyContain(f => f.Status != PayrollLoanDeductionStatus.Applied,
            "nada se dio por aplicado");
    }

    [Fact]
    public async Task Con_personas_migradas_sin_activar_la_aprobacion_exige_reconocerlo()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        d.Obligaciones.MarcarMigrada(d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId);
        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        var sin = await Aprobador(d).Handle(new ApprovePayrollRunCommand(calc.Value.RunPublicId, Confirm: true), CancellationToken.None);
        sin.Error.Code.Should().Be("Payroll.LendingPendingActivationAckRequired");
        sin.Error.Message.Should().Contain("Ana");

        var con = await Aprobador(d).Handle(new ApprovePayrollRunCommand(calc.Value.RunPublicId, Confirm: true, AcknowledgeLendingPendingActivation: true), CancellationToken.None);
        con.IsSuccess.Should().BeTrue(con.Error.Message);
        var run = await d.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calc.Value.RunPublicId);
        run.LendingActivationAckBy.Should().Be("contadora@demo");
        run.LendingActivationAckAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Reversar_reversa_por_el_contrato_y_deja_las_filas_Reversed()
    {
        var e = await CalculadoAsync();
        (await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var r = await Reversor(e.D).Handle(new ReversePayrollRunCommand(e.RunPublicId, "Faltó una incapacidad"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        e.D.Contrato.Reversos.Select(x => x.PaymentIdempotencyKey).Should().BeEquivalentTo(
            [RecaudoPorNomina.Clave(e.RunPublicId, e.Cabe), RecaudoPorNomina.Clave(e.RunPublicId, e.NoCabe)]);
        e.D.Contrato.Reversos.Should().OnlyContain(x => x.Reason == "Faltó una incapacidad");
        (await e.D.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync())
            .Should().OnlyContain(f => f.Status == PayrollLoanDeductionStatus.Reversed && f.CarteraReversalPublicId != null);
    }

    [Fact]
    public async Task Si_Cartera_rechaza_un_reverso_la_ordinaria_no_se_reversa()
    {
        var e = await CalculadoAsync();
        (await Aprobador(e.D).Handle(new ApprovePayrollRunCommand(e.RunPublicId, Confirm: true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        e.D.Contrato.ComoRespondeReversar[e.NoCabe] = ContratoDeObligacionesDePrueba.Rechazo("Lending.Reversal.OfReversal", "Ese pago ya fue reversado.");

        var r = await Reversor(e.D).Handle(new ReversePayrollRunCommand(e.RunPublicId, "error"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.LoanReversalRejected");
        r.Error.Message.Should().Contain("B-1").And.Contain("ya fue reversado");
        e.D.Db.ChangeTracker.Clear();
        (await e.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == e.RunPublicId)).Status.Should().Be(PayrollRunStatus.Approved);
    }

    /// <summary>
    /// Feature 013, US5, T0524 (C55): descartar el borrador anula las novedades <c>LoanDeduction</c> que materializó (como las recurrentes) y el cálculo
    /// siguiente las vuelve a generar desde <c>CuotasDeNominaAsync</c>, una por empleado, obligación y período.
    /// </summary>
    [Fact]
    public async Task Descartar_el_borrador_anula_las_cuotas_de_Cartera_y_el_calculo_siguiente_las_vuelve_a_generar()
    {
        var e = await CalculadoAsync();
        (await e.D.Db.PayrollNovelties.CountAsync(n => n.Origin == NoveltyOrigin.LoanDeduction && n.Status == NoveltyStatus.Active)).Should().Be(2);

        var descartado = await new IngenIA365ERP.Application.Payroll.Runs.DiscardPayrollRun.DiscardPayrollRunCommandHandler(e.D.Db, e.D.Lock, e.D.Clock, e.D.User,
            e.D.AuditEmitter).Handle(new IngenIA365ERP.Application.Payroll.Runs.DiscardPayrollRun.DiscardPayrollRunCommand(e.RunPublicId, "Fechas equivocadas"),
            CancellationToken.None);
        descartado.IsSuccess.Should().BeTrue(descartado.Error.Message);
        var anuladas = await e.D.Db.PayrollNovelties.AsNoTracking().Where(n => n.Origin == NoveltyOrigin.LoanDeduction).ToListAsync();
        anuladas.Should().HaveCount(2).And.OnlyContain(n => n.Status == NoveltyStatus.Cancelled && n.StatusReason == CuotasDeCarteraEnNomina.RecurringNoveltiesMaterializerReason);

        var otra = await new CalculatePayrollRunCommandHandler(e.D.Db, e.D.Loader, e.D.Recurrentes, e.D.CuotasDeCartera, e.D.CuotasDeCuentas, e.D.Lock, e.D.Clock, e.D.User,
            NullLogger<CalculatePayrollRunCommandHandler>.Instance).Handle(new CalculatePayrollRunCommand(e.D.Marzo.PublicId), CancellationToken.None);

        otra.IsSuccess.Should().BeTrue(otra.Error.Message);
        var vigentes = await e.D.Db.PayrollNovelties.AsNoTracking().Where(n => n.Origin == NoveltyOrigin.LoanDeduction && n.Status == NoveltyStatus.Active).ToListAsync();
        vigentes.Select(n => n.ObligationPublicId).Should().BeEquivalentTo(new Guid?[] { e.Cabe, e.NoCabe }, "una por empleado, obligación y período (C55)");
    }

    [Fact]
    public async Task La_consulta_de_descuentos_de_Cartera_de_la_corrida_los_muestra_por_empleado_y_obligacion()
    {
        var e = await CalculadoAsync();

        var r = await new ListPayrollLoanDeductionsQueryHandler(e.D.Db).Handle(new ListPayrollLoanDeductionsQuery(e.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Should().HaveCount(2);
        r.Value.Should().OnlyContain(x => x.Employee.PublicId == e.D.Ana.PublicId && x.Employee.Name.Contains("Ana") && x.Status == PayrollLoanDeductionStatus.Calculated
                                          && x.CarteraReceiptPublicId == null && x.CarteraReversalPublicId == null && x.Applied == null);
        var b = r.Value.Single(x => x.ObligationNumber == "B-1");
        b.ObligationPublicId.Should().Be(e.NoCabe);
        b.Expected.Should().Be(2_500_000m);
        b.Deducted.Should().Be(1_044_548m - 150_000m);
        b.NotDeducted.Should().Be(b.Expected - b.Deducted);
        b.Reason.Should().Be("DeductionCap");
        r.Value.Single(x => x.ObligationNumber == "A-1").Reason.Should().BeNull();

        (await new ListPayrollLoanDeductionsQueryHandler(e.D.Db).Handle(new ListPayrollLoanDeductionsQuery(Guid.NewGuid()), CancellationToken.None))
            .Error.Code.Should().Be("Payroll.RunNotFound");
    }
}
