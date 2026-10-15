using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.DebtorEvents;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Novelties;

/// <summary>
/// La aplicación de una novedad aprobada (feature 013, K3, T0863; FR-090, FR-091, US11-3; mensajes.md §10.20): la operación <c>TermsChange</c> por el
/// registro, las versiones nuevas de condiciones (con <c>NoveltyId</c>) y de plan, el crédito enlazado de la novación, los hechos en línea y la
/// validación previa; el libro sigue cuadrando con la reconstrucción. (nuevo)
/// </summary>
public sealed class AplicacionDeNovedadTests : IAsyncLifetime
{
    private NovedadesDePrueba _n = null!;

    public async Task InitializeAsync() => _n = await NovedadesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _n.Dispose();
        return Task.CompletedTask;
    }

    private static NewTermsInput Prorroga(int cuotas = 36, bool capitalizar = false) => new(Installments: cuotas, CapitalizeInterest: capitalizar);

    /// <summary>Causa hasta un mes después del primer vencimiento sin pagar: hay interés corriente y de mora pendientes.</summary>
    private async Task EnMoraAsync()
    {
        var vencimiento = await _n.PrimerVencimientoAsync();
        await _n.P.CausarHastaAsync(vencimiento.AddDays(20));
    }

    [Fact]
    public async Task La_prorroga_deja_la_operacion_las_versiones_nuevas_el_conteo_y_el_hecho_en_linea_y_el_libro_cuadra()
    {
        await EnMoraAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga());
        var versionesAntes = await _n.Db.LoanTermsVersions.CountAsync(t => t.LoanId == _n.Credito.Id);

        var r = await _n.AplicarAsync(aprobada.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var operacion = await _n.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.OperationPublicId);
        operacion.Kind.Should().Be(LoanOperationKind.TermsChange);
        operacion.Number.Should().StartWith(NovedadesDeCartera.PrefijoDeOperacion);
        var novedad = await _n.Db.LoanNovelties.AsNoTracking().SingleAsync();
        (novedad.Status, novedad.AppliedOperationId, novedad.RestructuringCountAfter).Should().Be((LoanNoveltyStatus.Applied, operacion.Id, (int?)1));
        operacion.NoveltyId.Should().Be(novedad.Id);

        var condiciones = await _n.Db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == _n.Credito.Id).OrderByDescending(t => t.VersionNumber).ToListAsync();
        condiciones.Should().HaveCount(versionesAntes + 1);
        (condiciones[0].NoveltyId, condiciones[0].RestructuringCount).Should().Be((novedad.Id, 1));
        (await _n.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _n.Credito.Id)).Number.Should().Be(_n.Credito.Number, "el número se conserva");
        (await _n.Db.RepaymentScheduleVersions.CountAsync(v => v.LoanId == _n.Credito.Id)).Should().Be(2, "el plan anterior queda como versión");

        var hecho = await _n.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoReestructuradoV1.Type);
        hecho.PayloadJson.Should().Contain("previousDimensions").And.Contain("newPlan").And.Contain("\"restructuringCount\":1");
        r.Value.Messages.Should().Contain(hecho.PublicId.ToString());
        await _n.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_prorroga_con_capitalizacion_lleva_el_interes_al_capital_por_filas_del_libro()
    {
        await EnMoraAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga(capitalizar: true));

        var r = await _n.AplicarAsync(aprobada.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var operacion = await _n.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.OperationPublicId);
        var filas = await _n.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id && t.Kind == LoanTransactionKind.Capitalization).ToListAsync();
        filas.Should().NotBeEmpty();
        filas.Sum(f => f.Amount).Should().Be(0m, "lo que sale del interés entra al capital");
        filas.Where(f => f.Concept == LoanConcept.Principal).Sum(f => f.Amount).Should().BePositive();
        (await _n.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoReestructuradoV1.Type)).PayloadJson.Should().Contain("Capitalizacion");
        await _n.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_novacion_crea_el_enlazado_que_hereda_y_el_anterior_queda_novado_con_el_libro_cuadrado_en_los_dos()
    {
        await EnMoraAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Novation, new NewTermsInput(Installments: 30));

        var r = await _n.AplicarAsync(aprobada.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.NewLoanPublicId.Should().NotBeNull();
        var anterior = await _n.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _n.Credito.Id);
        var nuevo = await _n.Db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == r.Value.NewLoanPublicId);
        (anterior.Status, nuevo.Status).Should().Be((LoanStatus.Novated, LoanStatus.Active));
        (nuevo.OriginLoanId, nuevo.OriginLinkKind).Should().Be((anterior.Id, LoanLinkKind.Novation));
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).ResultingLoanId.Should().Be(nuevo.Id);
        (await _n.Db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == nuevo.Id).Select(t => t.RestructuringCount).SingleAsync())
            .Should().Be(1, "hereda el conteo y suma la novación");

        var trasladado = await _n.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == anterior.Id && t.Kind == LoanTransactionKind.TransferToLinkedLoan)
            .SumAsync(t => t.Amount);
        var recibido = await _n.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == nuevo.Id && t.Kind == LoanTransactionKind.TransferFromLinkedLoan)
            .SumAsync(t => t.Amount);
        recibido.Should().Be(-trasladado, "lo que sale del anterior entra al enlazado");
        (await _n.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoNovadoV1.Type)).PayloadJson.Should().Contain("newLoan");
        await _n.P.ElLibroCuadraAsync(anterior.Id);
        await _n.P.ElLibroCuadraAsync(nuevo.Id);
    }

    [Fact]
    public async Task Sin_aprobar_no_se_aplica_y_aplicada_no_se_aplica_otra_vez()
    {
        var radicada = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga());
        var sinAprobar = await _n.AplicarAsync(radicada.PublicId);
        sinAprobar.IsFailure.Should().BeTrue();
        sinAprobar.Error.Code.Should().Be(ErroresDeNovedades.CodigoNoAprobada);

        await _n.DecidirAsync(Domain.Lending.Attributions.LendingDecisionSubjects.Novelty, radicada.PublicId, radicada.Routing!.ContentSha256);
        (await _n.AplicarAsync(radicada.PublicId)).IsSuccess.Should().BeTrue();
        var otraVez = await _n.AplicarAsync(radicada.PublicId);
        otraVez.IsFailure.Should().BeTrue();
        otraVez.Error.Code.Should().Be(ErroresDeNovedades.CodigoYaAplicada);
    }

    [Fact]
    public async Task Quien_la_radico_no_la_aplica()
    {
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga());
        _n.G.ActuarComo(_n.D.O.Asesora);
        _n.G.Conceder("Lending.Novelties.Read", "Lending.Novelties.Apply");

        var r = await _n.Aplicar().Handle(new ApplyLoanNoveltyCommand(aprobada.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Sin_regla_contable_y_con_Bloquear_no_se_aplica()
    {
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga());
        _n.SinRegla(CreditoReestructuradoV1.Type);

        var r = await _n.AplicarAsync(aprobada.PublicId);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Prevalidation.NotPostable");
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).Status.Should().Be(LoanNoveltyStatus.Approved, "nada se escribió");
        (await _n.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.TermsChange)).Should().Be(0);
    }

    [Fact]
    public async Task El_acuerdo_de_insolvencia_se_aplica_como_reestructuracion_y_cierra_el_proceso()
    {
        _n.G.ActuarComo(_n.D.O.Asesora);
        var abierto = await _n.AbrirInsolvencia()
            .Handle(new OpenInsolvencyProceedingCommand(_n.D.O.Ana.PublicId, _n.G.HoyLocal, InsolvencyRegime.NaturalPersonDebtNegotiation, "Centro de conciliación",
                "2026-001", Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);
        abierto.IsSuccess.Should().BeTrue(abierto.IsFailure ? abierto.Error.Message : string.Empty);
        _n.Db.ChangeTracker.Clear();

        _n.G.Conceder("Lending.Novelties.Read", "Lending.Novelties.Request");
        var pedida = await _n.Pedir().Handle(new RequestLoanNoveltyCommand([_n.Credito.PublicId], NoveltyMechanism.Extension, NoveltyRequestMotive.DebtorRequest,
            Prorroga(48), "Acuerdo de pago del proceso", abierto.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        pedida.Value.Cause.Should().Be(NoveltyCause.Restructuring, "el acuerdo de insolvencia es reestructuración");
        _n.Db.ChangeTracker.Clear();
        (await _n.Simular().Handle(new SimulateLoanNoveltyQuery(pedida.Value.PublicId), default)).IsSuccess.Should().BeTrue();
        var radicada = await _n.Radicar().Handle(new SubmitLoanNoveltyCommand(pedida.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _n.Db.ChangeTracker.Clear();
        (await _n.DecidirAsync(Domain.Lending.Attributions.LendingDecisionSubjects.Novelty, radicada.Value.PublicId, radicada.Value.Routing!.ContentSha256))
            .IsSuccess.Should().BeTrue();

        var r = await _n.AplicarAsync(radicada.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var proceso = await _n.Db.InsolvencyProceedings.AsNoTracking().SingleAsync();
        (proceso.Status, proceso.AgreementNoveltyId).Should().Be((InsolvencyProceedingStatus.Agreement,
            (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).Id));
        // Bloque B2: el acuerdo termina la negociación y levanta el bloqueo de crédito nuevo.
        (await _n.Db.DebtorBlocks.AsNoTracking().SingleAsync(b => b.Reason == DebtorBlockReason.Insolvency)).Vigente.Should().BeFalse();
    }

}
