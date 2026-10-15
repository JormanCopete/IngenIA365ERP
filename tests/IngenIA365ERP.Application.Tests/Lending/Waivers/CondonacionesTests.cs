using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Waivers;
using IngenIA365ERP.Application.Tests.Lending.Novelties;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Waivers;

/// <summary>
/// Las condonaciones (feature 013, K3, T0864; FR-094, US11-5; mensajes.md §10.21): cada línea no supera el saldo del concepto, los días de mora y el
/// vinculado quedan sellados, el capital exige el nivel de excepción, toda decisión lleva motivo, y aplicar deja la operación <c>Waiver</c> con sus filas y
/// <c>CondonacionAprobada</c> con lo que sale contra el deterioro individual del último cierre. (nuevo)
/// </summary>
public sealed class CondonacionesTests : IAsyncLifetime
{
    private NovedadesDePrueba _n = null!;

    public async Task InitializeAsync()
    {
        _n = await NovedadesDePrueba.CrearAsync();
        var vencimiento = await _n.PrimerVencimientoAsync();
        await _n.P.CausarHastaAsync(vencimiento.AddDays(60));
    }

    public Task DisposeAsync()
    {
        _n.Dispose();
        return Task.CompletedTask;
    }

    private async Task<decimal> SaldoAsync(LoanConcept concepto)
    {
        var cargado = await _n.P.C.Cargador().CargarAsync(_n.Credito.Id, null, _n.G.HoyLocal, default);
        return Domain.Lending.MotorDeCartera.Reconstruir(cargado!.Entrada!, _n.G.HoyLocal).Saldo(concepto);
    }

    private async Task<WaiverDto> AprobadaAsync(params WaiverConceptInput[] conceptos)
    {
        var pedida = await _n.PedirCondonacionAsync(conceptos);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        var d = await _n.DecidirAsync(LendingDecisionSubjects.Condonation, pedida.Value.PublicId, pedida.Value.Routing!.ContentSha256);
        d.IsSuccess.Should().BeTrue(d.IsFailure ? $"{d.Error.Code}: {d.Error.Message}" : string.Empty);
        return pedida.Value;
    }

    [Fact]
    public async Task Pedirla_sella_la_mora_y_el_vinculado_y_no_pasa_del_saldo_del_concepto()
    {
        var mora = await SaldoAsync(LoanConcept.DefaultInterest);
        mora.Should().BePositive();

        var excede = await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, mora + 0.01m));
        excede.IsFailure.Should().BeTrue();
        excede.Error.Code.Should().Be(WaiverLine.CodigoExcedeElSaldo);
        var datos = NovedadesDePrueba.Datos(excede.Error);
        (datos.GetProperty("concept").GetString(), datos.GetProperty("balance").GetDecimal()).Should().Be(("DefaultInterest", mora));

        var r = await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, mora));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.Status, r.Value.TotalAmount, r.Value.IsRelatedParty).Should().Be((WaiverStatus.Submitted, mora, false));
        r.Value.DaysPastDueAtRequest.Should().BePositive();
        r.Value.Number.Should().StartWith(CondonacionesDeCartera.Prefijo);
        r.Value.Routing!.LevelCode.Should().Be("ANALISTA");
    }

    [Fact]
    public async Task Condonar_capital_sube_al_nivel_de_excepcion()
    {
        var r = await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.Principal, 10_000m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Routing!.LevelCode.Should().Be("EXCEP", "el capital sólo lo condona el nivel de excepción (FR-094)");
        r.Value.Routing.Reasons.Should().Contain(x => x.Kind == RazonDeEnrutamiento.Exception);
    }

    [Fact]
    public async Task Toda_decision_lleva_motivo_y_quien_la_pidio_no_la_decide()
    {
        var pedida = (await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, 100m))).Value;

        var sinMotivo = await _n.DecidirAsync(LendingDecisionSubjects.Condonation, pedida.PublicId, pedida.Routing!.ContentSha256, motivo: null);
        sinMotivo.IsFailure.Should().BeTrue();
        sinMotivo.Error.Code.Should().Be(DecisionSobreCondonacion.CodigoMotivoRequerido);

        _n.G.ActuarComo(_n.D.O.Asesora);
        _n.G.Conceder("Lending.Loans.Read", "Lending.Decisions.Analyst");
        var propia = await _n.Decidir().Handle(new Application.Lending.Attributions.RecordCreditDecisionCommand(LendingDecisionSubjects.Condonation,
            pedida.PublicId, CreditDecisionOutcome.Approve, "Cumple", null, pedida.Routing.ContentSha256) { OperationKey = Guid.NewGuid() }, default);
        propia.Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Aplicarla_deja_la_operacion_Waiver_las_filas_y_el_hecho_y_el_libro_cuadra()
    {
        var mora = await SaldoAsync(LoanConcept.DefaultInterest);
        var aprobada = await AprobadaAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, mora), new WaiverConceptInput(LoanConcept.CurrentInterest, 1_000m));

        var r = await _n.AplicarCondonacionAsync(aprobada.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var operacion = await _n.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.OperationPublicId);
        (operacion.Kind, operacion.Amount).Should().Be((LoanOperationKind.Waiver, mora + 1_000m));
        operacion.Number.Should().StartWith(CondonacionesDeCartera.PrefijoDeOperacion);
        var filas = await _n.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id && t.Kind == LoanTransactionKind.Waiver).ToListAsync();
        filas.Sum(f => f.Amount).Should().Be(-(mora + 1_000m));
        (await SaldoAsync(LoanConcept.DefaultInterest)).Should().Be(0m);
        var w = await _n.Db.Waivers.AsNoTracking().SingleAsync();
        (w.Status, w.AppliedOperationId).Should().Be((WaiverStatus.Applied, operacion.Id));

        var hecho = await _n.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CondonacionAprobadaV1.Type);
        r.Value.Messages.Should().ContainSingle(hecho.PublicId.ToString());
        (r.Value.ImpairmentApplied, r.Value.ImpairmentShortfall).Should().Be((0m, mora + 1_000m), "sin cierre no hay deterioro que consumir");
        await _n.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Sin_aprobar_no_se_aplica_y_quien_la_pidio_no_la_aplica()
    {
        var pedida = (await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, 100m))).Value;
        var sinAprobar = await _n.AplicarCondonacionAsync(pedida.PublicId);
        sinAprobar.Error.Code.Should().Be(Waiver.CodigoNoAprobada);

        await _n.DecidirAsync(LendingDecisionSubjects.Condonation, pedida.PublicId, pedida.Routing!.ContentSha256);
        _n.G.ActuarComo(_n.D.O.Asesora);
        _n.G.Conceder("Lending.Loans.Read", "Lending.Condonations.Apply");
        var propia = await _n.AplicarCondonacion().Handle(new ApplyWaiverCommand(pedida.PublicId) { OperationKey = Guid.NewGuid() }, default);
        propia.Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Lo_condonado_sale_contra_el_deterioro_del_ultimo_cierre_hasta_agotarlo()
    {
        var cierre = new MonthlyClose
        {
            Kind = MonthlyCloseKind.Base, PeriodYear = (short)_n.G.HoyLocal.Year, PeriodMonth = (byte)_n.G.HoyLocal.Month, Version = 1,
            CloseDate = _n.G.HoyLocal.AddDays(-1), InformationCutoffAt = _n.G.UtcNow.AddDays(-1), LinesSha256 = new string('0', 64),
            OrderedAt = _n.G.UtcNow.AddDays(-1), ClosedAt = _n.G.UtcNow.AddDays(-1),
        };
        _n.Db.MonthlyCloses.Add(cierre);
        await _n.Db.SaveChangesAsync();
        _n.Db.MonthlyCloseLoans.Add(new MonthlyCloseLoan
        {
            MonthlyCloseId = cierre.Id, LoanId = _n.Credito.Id, PersonId = _n.Credito.DebtorPersonId, OfficePublicId = Guid.NewGuid(), BranchId = _n.Credito.BranchId,
            CreditLineId = _n.Credito.CreditLineId, ModalityCode = "CONSUMO", SarcSegmentCode = "CONSUMO", ImpairmentModel = ImpairmentModel.ArrearsAnnex1,
            ModelBasis = ModelBasis.NotObligated, ArrearsRating = RatingCategory.B, FinalRating = RatingCategory.B, AccountingRating = RatingCategory.B,
            RatingCause = RatingCause.Arrears, InterestImpairment = 1_000m, TotalImpairment = 1_000m,
            BalancesByConceptJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["CurrentInterest"] = 0m, ["DefaultInterest"] = 5_000m }),
        });
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();

        var primera = await AprobadaAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, 600m));
        var r1 = await _n.AplicarCondonacionAsync(primera.PublicId);
        r1.IsSuccess.Should().BeTrue(r1.IsFailure ? $"{r1.Error.Code}: {r1.Error.Message}" : string.Empty);
        (r1.Value.ImpairmentApplied, r1.Value.ImpairmentShortfall).Should().Be((600m, 0m));

        var segunda = await AprobadaAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, 600m));
        var r2 = await _n.AplicarCondonacionAsync(segunda.PublicId);
        r2.IsSuccess.Should().BeTrue(r2.IsFailure ? $"{r2.Error.Code}: {r2.Error.Message}" : string.Empty);
        (r2.Value.ImpairmentApplied, r2.Value.ImpairmentShortfall).Should().Be((400m, 200m), "la primera ya consumió 600 del deterioro del cierre");
        var hecho = await _n.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CondonacionAprobadaV1.Type).OrderBy(m => m.Id).LastAsync();
        hecho.PayloadJson.Should().Contain("impairmentApplied");
        await _n.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_lista_respeta_el_alcance_y_anular_la_deja_cancelada()
    {
        var pedida = (await _n.PedirCondonacionAsync(new WaiverConceptInput(LoanConcept.DefaultInterest, 100m))).Value;

        var lista = await _n.Condonadas().Handle(new ListWaiversQuery(LoanPublicId: _n.Credito.PublicId), default);
        lista.Value.Items.Should().ContainSingle(w => w.PublicId == pedida.PublicId);

        var anulada = await _n.AnularCondonacion().Handle(new CancelWaiverCommand(pedida.PublicId, "El deudor pagó") { OperationKey = Guid.NewGuid() }, default);
        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : string.Empty);
        (await _n.Db.Waivers.AsNoTracking().SingleAsync()).Status.Should().Be(WaiverStatus.Cancelled);
    }
}
