using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payoffs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T014, T021; HU1, api.md §1): <see cref="GetPayoffCandidatesQuery"/> lista los créditos vivos del deudor —sólo como deudor y en el alcance de
/// quien consulta— liquidados por <see cref="LiquidacionDeRecogida"/> a la fecha proyectada; sólo el vigente al día se puede recoger (en mora, castigado o no
/// vigente sale con su motivo); de fábrica se admiten créditos total y parcial, y con <c>Recogida.ProductosAdmitidos</c> de la línea del crédito nuevo, lo
/// que ella diga; avisa si otra solicitud en curso ya lo recoge (salvo la que se corrige); resume lo marcado —total, parcial topado, neto, cuotas que se
/// liberan— y audita <c>Lending.Payoff.Viewed</c> por crédito liquidado. El escenario es el de <see cref="PagosDePrueba"/>: Ana con su crédito de 3.000.000
/// desembolsado hoy (con plan, por caja) y, según la prueba, créditos de libro sin condiciones selladas. (nuevo)
/// </summary>
public sealed class GetPayoffCandidatesQueryTests
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    // ------------------------------------------------------------------------------------------------------------ piezas --

    /// <summary>El listado de créditos por el handler real (el alcance del escenario).</summary>
    private static ISender Remitente(PagosDePrueba p)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListLoansQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ListLoansQueryHandler(p.Db, p.G.Alcance).Handle(ci.Arg<ListLoansQuery>(), ci.Arg<CancellationToken>()));
        return sender;
    }

    private static GetPayoffCandidatesQueryHandler Handler(PagosDePrueba p) => new(p.Db, Remitente(p), p.D.LiquidacionDeRecogida(), p.D.O.Capacidad(),
        p.D.Parametros(), p.G.Alcance, p.G.Auditoria, p.G.Reloj, Substitute.For<IBlobStore>());

    private static async Task<PayoffCandidatesDto> ConsultarAsync(PagosDePrueba p, GetPayoffCandidatesQuery consulta)
    {
        var r = await Handler(p).Handle(consulta, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        p.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private static GetPayoffCandidatesQuery De(PagosDePrueba p, params PayoffInput[] marcados) =>
        new(p.D.O.Ana.PublicId, Selected: marcados.Length == 0 ? null : marcados);

    private static Loan ConEstado(PagosDePrueba p, Loan credito, LoanStatus estado)
    {
        credito.Status = estado;
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
        return credito;
    }

    /// <summary>Un crédito de libro en mora: su posición de ayer con los días de mora y lo que cuesta cancelarlo.</summary>
    private static Loan EnMora(PagosDePrueba p, int dias)
    {
        var credito = p.D.O.Credito(p.D.O.Ana, 500_000m, "CR-MORA");
        p.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = credito.Id, PositionDate = Hoy.AddDays(-1), Version = 1, RegisteredAt = p.G.UtcNow.AddDays(-1), Status = LoanStatus.Active,
            BranchId = credito.BranchId, CreditLineId = credito.CreditLineId, ModalityCode = "CONSUMO", Principal = 500_000m, CurrentInterest = 20_000m,
            PayoffAmount = 520_000m, DaysPastDue = dias, InstallmentsPastDue = 1, PrincipalPastDue = 40_000m,
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
        return credito;
    }

    private static void Vigencia(PagosDePrueba p, string clave, string valor, int? linea = null)
    {
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    private static async Task<int> VistasAsync(PagosDePrueba p) =>
        (await p.EventosAsync()).Count(e => e == AuditEventTypes.LendingPayoffViewed);

    // ------------------------------------------------------------------------------------------------------ elegibilidad --

    [Fact]
    public async Task Lista_los_creditos_vivos_del_deudor_y_solo_el_vigente_al_dia_se_puede_recoger()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var libro = p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-LIBRO");
        var mora = EnMora(p, 45);
        var castigado = ConEstado(p, p.D.O.Credito(p.D.O.Ana, 300_000m, "CR-CASTIGO"), LoanStatus.WrittenOff);
        ConEstado(p, p.D.O.Credito(p.D.O.Ana, 200_000m, "CR-PAGADO"), LoanStatus.PaidOff);

        var r = await ConsultarAsync(p, De(p));

        r.Date.Should().Be(Hoy, "sin fecha proyectada se liquida a hoy");
        r.Person!.Nombre.Should().ContainEquivalentOf("ana", "la consulta trae al deudor");
        r.Products.Select(x => x.Number).Should().BeEquivalentTo(new[] { p.Credito.Number, "CR-LIBRO", "CR-MORA", "CR-CASTIGO" },
            "lo cancelado no es un producto vivo");
        r.Products.Should().OnlyContain(x => x.Kind == PayoffProductKind.Loan, "en E1 sólo se listan créditos");

        var vigente = r.Products.Single(x => x.ProductPublicId == p.Credito.PublicId);
        vigente.Eligible.Should().BeTrue();
        vigente.BlockReason.Should().BeNull();
        vigente.ToPayOff.Should().BePositive();
        vigente.Breakdown.Principal.Should().Be(3_000_000m);
        vigente.InterestMode.Should().Be(ParametrosDeCartera.InteresesCorridos, "de fábrica la recogida cobra lo de la caja: el corrido");
        vigente.Channel.Should().Be(RepaymentChannel.Cashier);
        vigente.Origin.Should().Be(LoanOrigin.Origination);
        vigente.Status.Should().Be(LoanStatus.Active);
        vigente.Installment.Should().BePositive("la próxima cuota de su plan vigente");
        vigente.Fingerprint.Should().StartWith("sha256:");
        vigente.CurrentInstallment.Should().NotBeNull();
        vigente.CurrentInstallment!.Treatment.Should().Be(InstallmentTreatment.Include, "en E1 la cuota causada siempre se incluye");
        vigente.CurrentInstallment.Choosable.Should().BeFalse();
        vigente.CurrentInstallment.Sent.Should().BeNull();
        vigente.ToPayOffExcludingCurrentInstallment.Should().BeNull("«todo menos la cuota causada» llega con E2");

        var deLibro = r.Products.Single(x => x.ProductPublicId == libro.PublicId);
        deLibro.Eligible.Should().BeTrue();
        deLibro.ToPayOff.Should().Be(1_000_000m, "sin condiciones selladas sale de su libro");
        deLibro.Channel.Should().BeNull("sin vínculo de forma de pago");

        var enMora = r.Products.Single(x => x.ProductPublicId == mora.PublicId);
        enMora.Eligible.Should().BeFalse();
        enMora.BlockReason.Should().Be(ErroresDeRecogida.MotivoEnMora);
        enMora.BlockMessage.Should().Contain("45 días de mora").And.Contain("reestructuración");
        enMora.DaysPastDue.Should().Be(45);

        var deCastigo = r.Products.Single(x => x.ProductPublicId == castigado.PublicId);
        deCastigo.Eligible.Should().BeFalse();
        deCastigo.BlockReason.Should().Be(ErroresDeRecogida.MotivoCastigado);
        deCastigo.Status.Should().Be(LoanStatus.WrittenOff);
    }

    [Fact]
    public async Task Un_credito_pendiente_de_giro_se_muestra_pero_no_se_recoge()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var pendiente = ConEstado(p, p.D.O.Credito(p.D.O.Ana, 700_000m, "CR-PEND"), LoanStatus.PendingDisbursement);

        var r = await ConsultarAsync(p, De(p));

        var x = r.Products.Single(c => c.ProductPublicId == pendiente.PublicId);
        x.Eligible.Should().BeFalse();
        x.BlockReason.Should().Be(ErroresDeRecogida.MotivoNoActivo);
        x.BlockMessage.Should().Contain("todavía no se ha girado");
    }

    // ------------------------------------------------------------------------------------------------------------ alcance --

    [Fact]
    public async Task No_aparecen_los_creditos_de_otra_persona_ni_los_que_tiene_como_codeudora()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        p.D.O.Credito(p.D.O.Beto, 2_000_000m, "CR-BETO", p.D.O.Ana);

        var r = await ConsultarAsync(p, De(p));

        r.Products.Select(x => x.Number).Should().Equal(p.Credito.Number);
    }

    [Fact]
    public async Task Fuera_del_alcance_de_oficinas_de_quien_consulta_no_aparece_nada()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        p.G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { 999_999 }, TodasLasLineas = true };

        var r = await ConsultarAsync(p, De(p));

        r.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task La_linea_del_credito_nuevo_fuera_del_alcance_es_el_404_de_la_linea()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var linea = await p.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Id == p.Credito.CreditLineId);
        p.G.AlcanceActual = AlcanceDeCartera.Vacio with { TodasLasOficinas = true, Lineas = new HashSet<int> { 999_999 } };

        var r = await Handler(p).Handle(De(p) with { CreditLinePublicId = linea.PublicId }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeGobierno.CodigoLineaInexistente);
    }

    [Fact]
    public async Task Marcar_un_credito_que_no_es_del_deudor_es_un_error()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var ajeno = p.D.O.Credito(p.D.O.Beto, 2_000_000m, "CR-BETO", p.D.O.Ana);

        var r = await Handler(p).Handle(De(p, new PayoffInput(PayoffProductKind.Loan, ajeno.PublicId, PayoffMode.Total)), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoNoEsDelSolicitante);
    }

    // ----------------------------------------------------------------------------------------------------------- admitidos --

    [Fact]
    public async Task De_fabrica_se_admiten_creditos_total_y_parcial()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var linea = await p.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Id == p.Credito.CreditLineId);

        var general = await ConsultarAsync(p, De(p));
        var conLinea = await ConsultarAsync(p, De(p) with { CreditLinePublicId = linea.PublicId });

        foreach (var r in new[] { general, conLinea })
            r.Products.Single().AdmittedModes.Should().Equal(PayoffMode.Total, PayoffMode.Partial);
        conLinea.NewLoanLine.Should().Be($"{linea.Code} · {linea.Name}");
    }

    [Fact]
    public async Task La_linea_del_credito_nuevo_decide_los_productos_admitidos_con_caida_al_general()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var linea = await p.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Id == p.Credito.CreditLineId);
        Vigencia(p, ParametrosDeCartera.RecogidaProductosAdmitidos, "Aportes:Abono", linea.Id);
        Vigencia(p, ParametrosDeCartera.RecogidaProductosAdmitidos, "Creditos:Total");

        var conLinea = await ConsultarAsync(p, De(p) with { CreditLinePublicId = linea.PublicId });
        var general = await ConsultarAsync(p, De(p));

        var x = conLinea.Products.Single();
        x.Eligible.Should().BeFalse("la línea del crédito nuevo no admite recoger créditos");
        x.BlockReason.Should().Be(ErroresDeRecogida.MotivoNoAdmitido);
        x.AdmittedModes.Should().BeEmpty();
        general.Products.Single().AdmittedModes.Should().Equal(new[] { PayoffMode.Total }, "sin línea rige el general");
        general.Products.Single().Eligible.Should().BeTrue();
    }

    [Fact]
    public async Task Lo_marcado_en_un_modo_que_la_linea_no_admite_no_se_cuenta_y_se_avisa()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Vigencia(p, ParametrosDeCartera.RecogidaProductosAdmitidos, "Creditos:Total");

        var r = await ConsultarAsync(p, De(p, new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Partial, 100_000m)));

        r.Summary.SelectedTotal.Should().Be(0m);
        r.Summary.SelectedCount.Should().Be(0);
        r.Summary.Warnings.Should().ContainSingle(a => a.Contains("no admite recogerlo así"));
        r.Products.Single().Selected.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------ otra solicitud viva --

    [Fact]
    public async Task Avisa_si_otra_solicitud_en_curso_ya_lo_recoge_salvo_la_que_se_corrige_o_una_cancelada()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var libro = p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-LIBRO");
        var otra = await p.D.O.BorradorAsync();
        p.G.Conceder();
        var solicitud = await p.Db.CreditApplications.SingleAsync(a => a.PublicId == otra.PublicId);
        var fila = CreditApplicationPayoff.Proyectar(solicitud.Id, ProductoRecogido.Credito(libro.Id, libro.Number), PayoffMode.Total, null, 1,
            new ProyeccionDeRecogida(Hoy, p.G.UtcNow, new DesgloseDeRecogida(Capital: 1_000_000m), "sha256:" + new string('a', 64)));
        fila.CreatedBy = "test";
        p.Db.CreditApplicationPayoffs.Add(fila);
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        var esperado = solicitud.Number ?? string.Empty;

        var r = await ConsultarAsync(p, De(p));
        var corrigiendola = await ConsultarAsync(p, De(p) with { ExcludeApplicationPublicId = otra.PublicId });

        var x = r.Products.Single(c => c.ProductPublicId == libro.PublicId);
        x.InOtherApplication.Should().Be(esperado);
        x.Warnings.Should().Contain(a => a.Contains("que también lo recoge"));
        x.Eligible.Should().BeTrue("el aviso no impide marcarlo: lo decide quien vende");
        r.Products.Single(c => c.ProductPublicId == p.Credito.PublicId).InOtherApplication.Should().BeNull();
        corrigiendola.Products.Single(c => c.ProductPublicId == libro.PublicId).InOtherApplication.Should().BeNull("la que se corrige no es «otra»");

        var cancelada = await p.Db.CreditApplications.SingleAsync(a => a.PublicId == otra.PublicId);
        cancelada.Status = CreditApplicationStatus.Cancelled;
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        (await ConsultarAsync(p, De(p))).Products.Single(c => c.ProductPublicId == libro.PublicId).InOtherApplication.Should().BeNull(
            "una solicitud cancelada ya no recoge nada");
    }

    // ------------------------------------------------------------------------------------------------------------- resumen --

    [Fact]
    public async Task El_resumen_suma_lo_marcado_topa_el_parcial_y_dice_el_neto_y_las_cuotas_que_se_liberan()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var libro = p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-LIBRO");
        var mora = EnMora(p, 30);

        var r = await ConsultarAsync(p, De(p,
                new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total),
                new PayoffInput(PayoffProductKind.Loan, libro.PublicId, PayoffMode.Partial, 5_000_000m),
                new PayoffInput(PayoffProductKind.Loan, mora.PublicId, PayoffMode.Total))
            with { NewLoan = new PayoffNewLoanInput(10_000_000m, 450_000m) });

        var vigente = r.Products.Single(c => c.ProductPublicId == p.Credito.PublicId);
        var cuotaDelPlan = await p.D.O.Capacidad().CuotasPropiasAsync(p.D.O.Ana.Id, Hoy, soloNomina: false, default);
        r.Summary.SelectedTotal.Should().Be(vigente.ToPayOff + 1_000_000m, "el parcial se topa en lo que cuesta recoger");
        r.Summary.SelectedCount.Should().Be(2, "el de mora no se cuenta");
        r.Summary.NetToDisburse.Should().Be(10_000_000m - r.Summary.SelectedTotal);
        r.Summary.NetNotPositive.Should().BeFalse();
        r.Summary.ReleasedInstallments.Should().Be(vigente.Installment!.Value, "sólo libera su cuota el recogido total");
        r.Summary.ReleasedInstallments.Should().Be(cuotaDelPlan, "la misma próxima cuota que cuenta la capacidad de pago");
        r.Summary.NewInstallment.Should().Be(450_000m);
        r.Summary.CapacityAfter.Should().BeNull("la capacidad después de recoger la calcula la radicación");
        r.Summary.Warnings.Should().Contain(a => a.Contains("CR-MORA") && a.Contains("no se puede recoger"));
        r.Summary.Warnings.Should().Contain(a => a.Contains("CR-LIBRO") && a.Contains("pasa de lo que cuesta"));

        vigente.Selected.Should().BeTrue();
        vigente.SelectedMode.Should().Be(PayoffMode.Total);
        vigente.SelectedAmount.Should().Be(vigente.ToPayOff);
        var parcial = r.Products.Single(c => c.ProductPublicId == libro.PublicId);
        parcial.SelectedMode.Should().Be(PayoffMode.Partial);
        parcial.SelectedAmount.Should().Be(1_000_000m);
        r.Products.Single(c => c.ProductPublicId == mora.PublicId).Selected.Should().BeFalse();
    }

    [Fact]
    public async Task Un_parcial_dentro_de_lo_que_cuesta_se_toma_por_su_valor_y_no_libera_cuota()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var r = await ConsultarAsync(p, De(p, new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Partial, 250_000m))
            with { NewLoan = new PayoffNewLoanInput(4_000_000m) });

        r.Summary.SelectedTotal.Should().Be(250_000m);
        r.Summary.NetToDisburse.Should().Be(3_750_000m);
        r.Summary.ReleasedInstallments.Should().Be(0m);
        r.Summary.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_neto_en_cero_o_negativo_se_avisa()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var r = await ConsultarAsync(p, De(p, new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total))
            with { NewLoan = new PayoffNewLoanInput(1_000_000m) });

        r.Summary.NetToDisburse.Should().BeNegative();
        r.Summary.NetNotPositive.Should().BeTrue();
        r.Summary.Warnings.Should().ContainSingle(a => a.Contains("neto a girar") && a.Contains("no se puede radicar"));
    }

    [Fact]
    public async Task Sin_credito_nuevo_no_hay_neto()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var r = await ConsultarAsync(p, De(p, new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total)));

        r.Summary.NetToDisburse.Should().BeNull();
        r.Summary.NetNotPositive.Should().BeFalse();
        r.Summary.SelectedTotal.Should().Be(r.Products.Single().ToPayOff);
    }

    // ------------------------------------------------------------------------------------------- fecha, auditoría, membrete --

    [Fact]
    public async Task Otra_fecha_proyectada_liquida_a_esa_fecha()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var hoy = await ConsultarAsync(p, De(p));
        var despues = await ConsultarAsync(p, De(p) with { ProjectedDisbursementDate = Hoy.AddDays(10) });

        despues.Date.Should().Be(Hoy.AddDays(10));
        despues.Products.Single().ToPayOff.Should().BeGreaterThan(hoy.Products.Single().ToPayOff, "diez días más de interés corrido");
        despues.Products.Single().Fingerprint.Should().NotBe(hoy.Products.Single().Fingerprint);
    }

    [Fact]
    public async Task Una_fecha_pasada_se_rechaza_en_el_validador_y_en_el_handler()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var ayer = De(p) with { ProjectedDisbursementDate = Hoy.AddDays(-1) };

        new GetPayoffCandidatesQueryValidator(p.G.Reloj).Validate(ayer).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(GetPayoffCandidatesQuery.ProjectedDisbursementDate));
        var r = await Handler(p).Handle(ayer, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoFechaPasada);
    }

    [Fact]
    public async Task El_validador_pide_creditos_un_valor_en_el_parcial_y_sin_repetidos()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var validador = new GetPayoffCandidatesQueryValidator(p.G.Reloj);
        var id = Guid.NewGuid();

        validador.Validate(De(p, new PayoffInput(PayoffProductKind.Loan, id, PayoffMode.Total))).IsValid.Should().BeTrue();
        validador.Validate(De(p, new PayoffInput(PayoffProductKind.Account, id, PayoffMode.Deposit, 1_000m))).IsValid
            .Should().BeFalse("en E1 sólo se recogen créditos");
        validador.Validate(De(p, new PayoffInput(PayoffProductKind.Loan, id, PayoffMode.Partial))).IsValid.Should().BeFalse("el parcial lleva valor");
        validador.Validate(De(p, new PayoffInput(PayoffProductKind.Loan, id, PayoffMode.Installments))).IsValid.Should().BeFalse();
        validador.Validate(De(p, new PayoffInput(PayoffProductKind.Loan, id, PayoffMode.Total), new PayoffInput(PayoffProductKind.Loan, id, PayoffMode.Total)))
            .IsValid.Should().BeFalse("un producto marcado dos veces");
        validador.Validate(De(p) with { NewLoan = new PayoffNewLoanInput(0m) }).IsValid.Should().BeFalse();
        validador.Validate(De(p) with { PersonPublicId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Audita_la_liquidacion_vista_de_cada_credito_y_no_escribe_nada_mas()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-LIBRO");
        var antes = await VistasAsync(p);
        var recogidas = await p.Db.CreditApplicationPayoffs.CountAsync();

        await ConsultarAsync(p, De(p, new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total)));

        (await VistasAsync(p) - antes).Should().Be(2, "una liquidación vista por crédito liquidado");
        (await p.Db.CreditApplicationPayoffs.CountAsync()).Should().Be(recogidas, "consultar no guarda recogidas");
    }

    [Fact]
    public async Task Para_imprimir_trae_el_membrete()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var pantalla = await ConsultarAsync(p, De(p));
        var impresion = await ConsultarAsync(p, De(p) with { ForPrint = true });

        pantalla.Letterhead.Should().BeNull();
        impresion.Letterhead.Should().NotBeNull();
        impresion.Letterhead!.Cooperativa.Should().NotBeNullOrWhiteSpace();
    }
}
