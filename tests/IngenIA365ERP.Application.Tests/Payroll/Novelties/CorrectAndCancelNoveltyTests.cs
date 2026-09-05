using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.CancelNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.CorrectNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>T056 — FR-005: corregir crea versión; anular marca; nada se borra.</summary>
public class CorrectAndCancelNoveltyTests
{
    private static async Task<Guid> Registrar(NominaTestData d, string concepto = "HEX_NOCTURNA", decimal? cantidad = 6m,
        DateTime? desde = null, DateTime? hasta = null)
    {
        var handler = new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);
        var r = await handler.Handle(new RegisterNoveltyCommand
        {
            PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = concepto,
            Quantity = cantidad, StartDate = desde, EndDate = hasta,
        }, CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    [Fact]
    public async Task Corregir_crea_una_version_nueva_y_marca_la_anterior_con_motivo()
    {
        var d = new NominaTestData();
        var original = await Registrar(d);
        var handler = new CorrectNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);

        var r = await handler.Handle(new CorrectNoveltyCommand { NoveltyPublicId = original, Quantity = 8m, Reason = "Eran 8 horas, no 6" }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var anterior = await d.Db.PayrollNovelties.SingleAsync(n => n.PublicId == original);
        var nueva = await d.Db.PayrollNovelties.SingleAsync(n => n.PublicId == r.Value);
        anterior.Status.Should().Be(NoveltyStatus.Superseded);
        anterior.StatusReason.Should().Be("Eran 8 horas, no 6");
        anterior.UpdatedBy.Should().Be("ana@demo");
        anterior.Quantity.Should().Be(6m, "la versión anterior no se toca");
        nueva.Status.Should().Be(NoveltyStatus.Active);
        nueva.Quantity.Should().Be(8m);
        nueva.SupersedesNoveltyId.Should().Be(anterior.Id);
        d.Db.PayrollNovelties.Count().Should().Be(2);
    }

    [Fact]
    public async Task Corregir_una_incapacidad_rehace_el_traslado()
    {
        var d = new NominaTestData();
        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var original = await Registrar(d, "INCAP_GENERAL", null, new DateTime(2026, 3, 28), new DateTime(2026, 4, 3));
        var handler = new CorrectNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);

        var r = await handler.Handle(new CorrectNoveltyCommand
        {
            NoveltyPublicId = original, StartDate = new DateTime(2026, 3, 28), EndDate = new DateTime(2026, 4, 6), Reason = "Prórroga",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var trasladosAbril = await d.Db.PayrollNovelties.Where(n => n.PayPeriodId == abril.Id).ToListAsync();
        trasladosAbril.Should().HaveCount(2);
        trasladosAbril.Single(t => t.Status == NoveltyStatus.Cancelled).EndDate.Should().Be(new DateTime(2026, 4, 3));
        var vivo = trasladosAbril.Single(t => t.Status == NoveltyStatus.Active);
        vivo.EndDate.Should().Be(new DateTime(2026, 4, 6));
        vivo.DaysInPeriod.Should().Be(6);
    }

    [Fact]
    public void Corregir_exige_motivo()
    {
        var v = new CorrectNoveltyCommandValidator();
        v.Validate(new CorrectNoveltyCommand { NoveltyPublicId = Guid.NewGuid(), Quantity = 1m, Reason = "" }).IsValid.Should().BeFalse();
        v.Validate(new CorrectNoveltyCommand { NoveltyPublicId = Guid.NewGuid(), Quantity = 1m, Reason = "ok" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Anular_marca_cancelled_con_motivo_y_desactualiza_el_borrador()
    {
        var d = new NominaTestData();
        var id = await Registrar(d);
        var run = d.Borrador(d.Marzo);
        var handler = new CancelNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);

        var r = await handler.Handle(new CancelNoveltyCommand(id, "Registrada al empleado equivocado"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var n = await d.Db.PayrollNovelties.SingleAsync(x => x.PublicId == id);
        n.Status.Should().Be(NoveltyStatus.Cancelled);
        n.StatusReason.Should().Be("Registrada al empleado equivocado");
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task Anular_en_periodo_aprobado_se_niega_y_apunta_al_siguiente_abierto()
    {
        var d = new NominaTestData();
        var id = await Registrar(d);
        d.Marzo.Status = PayPeriodStatus.Approved;
        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var handler = new CancelNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);

        var r = await handler.Handle(new CancelNoveltyCommand(id, "Tarde"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PeriodApproved");
        r.Error.Message.Should().Contain($"retroactiveTargetPeriodPublicId={abril.PublicId}");
        (await d.Db.PayrollNovelties.SingleAsync(x => x.PublicId == id)).Status.Should().Be(NoveltyStatus.Active);
    }

    [Fact]
    public async Task Una_novedad_ya_anulada_no_se_anula_dos_veces()
    {
        var d = new NominaTestData();
        var id = await Registrar(d);
        var handler = new CancelNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);
        (await handler.Handle(new CancelNoveltyCommand(id, "una"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var r = await handler.Handle(new CancelNoveltyCommand(id, "dos"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.NoveltyNotActive");
    }
}
