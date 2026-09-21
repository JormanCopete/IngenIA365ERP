using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// Feature 010, US3 (FR-018a, T065): el descuento propuesto sólo se baja, con motivo; la línea y los
/// totales del borrador cambian sin versión nueva; subir o bajar sin motivo se rechaza; si la suma
/// aplicada supera el neto queda <c>DeductionOverNet</c>; todo va a la auditoría.
/// </summary>
public class AdjustSettlementDeductionCommandHandlerTests
{
    [Fact]
    public async Task Bajar_con_motivo_cambia_la_linea_y_el_neto_sin_version_nueva_y_queda_auditado()
    {
        var p = new DefinitivaDePrueba();
        var t = await p.RegistrarAnaAsync();
        var credito = p.Descuento(t, 1001);

        var r = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, credito.ObligationPublicId, 1_000_000m, "El asociado refinancia el resto con la cooperativa"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Applied.Should().Be(1_000_000m);
        r.Value.Proposed.Should().Be(1_500_000m);
        r.Value.Reason.Should().Be("El asociado refinancia el resto con la cooperativa");
        r.Value.AdjustedBy.Should().Be("ana@demo");
        r.Value.Status.Should().Be(SettlementDeductionStatus.Adjusted);
        r.Value.RemainingAfter.Should().Be(500_000m, "lo que quedará en Cartera");

        var run = await p.D.Db.PayrollRuns.Include(x => x.Employees).ThenInclude(e => e.Lines).SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Version.Should().Be(1, "el ajuste no crea versión");
        run.Status.Should().Be(PayrollRunStatus.Draft);
        var fila = run.Employees.Single();
        var descuento = await p.D.Db.SettlementDeductions.SingleAsync(d => d.PublicId == credito.ObligationPublicId);
        var linea = fila.Lines.Single(l => l.SettlementDeductionId == descuento.Id);
        linea.Amount.Should().Be(1_000_000m);
        linea.ExplanationJson.Should().Contain("bajado con motivo").And.Contain("refinancia");
        fila.TotalDeductions.Should().Be(t.Totals.Deductions - 500_000m);
        fila.NetPay.Should().Be(t.Totals.Net + 500_000m);
        run.TotalNet.Should().Be(fila.NetPay);

        var consulta = await new GetSettlementDeductionsQueryHandler(p.D.Db).Handle(new GetSettlementDeductionsQuery(t.RunPublicId), CancellationToken.None);
        consulta.Value.TotalApplied.Should().Be(1_900_000m - 500_000m);
        consulta.Value.Net.Should().Be(t.Deductions.Net, "el neto antes de descuentos no cambia");
        consulta.Value.NetAfterDeductions.Should().Be(fila.NetPay);

        await p.D.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollSettlementDeductionAdjusted
                                            && e.NewValuesJson!.Contains("\"proposed\":1500000") && e.NewValuesJson.Contains("\"applied\":1000000")
                                            && e.NewValuesJson.Contains("refinancia") && e.NewValuesJson.Contains("ana@demo")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subir_sobre_lo_propuesto_se_rechaza_con_el_propuesto_en_data()
    {
        var p = new DefinitivaDePrueba();
        var t = await p.RegistrarAnaAsync();

        var r = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, p.Descuento(t, 1002).ObligationPublicId, 300_001m, "más"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.DeductionAboveProposed");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { proposed = 300_000m });
    }

    [Fact]
    public async Task Bajar_sin_motivo_se_rechaza_y_volver_al_propuesto_no_lo_exige()
    {
        var p = new DefinitivaDePrueba();
        var t = await p.RegistrarAnaAsync();
        var libranza = p.LibranzaDe(t);

        var sinMotivo = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, libranza.ObligationPublicId, 50_000m, "  "), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Payroll.Settlement.DeductionReasonRequired");

        (await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, libranza.ObligationPublicId, 50_000m, "Una cuota la paga el tercero"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var vuelta = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, libranza.ObligationPublicId, 100_000m, null), CancellationToken.None);
        vuelta.IsSuccess.Should().BeTrue(vuelta.Error.Message);
        vuelta.Value.Status.Should().Be(SettlementDeductionStatus.Proposed);
        vuelta.Value.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Cuando_la_suma_aplicada_supera_el_neto_queda_la_bandera_y_bajar_la_quita()
    {
        var p = new DefinitivaDePrueba(saldo1001: 80_000_000m);
        var t = await p.RegistrarAnaAsync();
        t.Deductions.DeductionOverNet.Should().BeTrue("un saldo de 80 millones supera cualquier neto de Ana");
        var run = await p.D.Db.PayrollRuns.Include(x => x.Employees).SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Employees.Single().Flags.Should().HaveFlag(RunEmployeeFlag.DeductionOverNet);

        var r = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, p.Descuento(t, 1001).ObligationPublicId, 1_000_000m, "Hasta donde alcanza el neto; el resto sigue en Cartera"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var despues = await p.D.Db.PayrollRunEmployees.SingleAsync(e => e.PayrollRunId == run.Id);
        despues.Flags.Should().NotHaveFlag(RunEmployeeFlag.DeductionOverNet);
        despues.Flags.Should().NotHaveFlag(RunEmployeeFlag.NegativeNet);
        despues.NetPay.Should().BePositive();
        (await new GetSettlementDeductionsQueryHandler(p.D.Db).Handle(new GetSettlementDeductionsQuery(t.RunPublicId), CancellationToken.None)).Value.DeductionOverNet.Should().BeFalse();
    }

    [Fact]
    public async Task Sobre_una_corrida_que_no_es_borrador_o_un_descuento_ajeno_se_rechaza()
    {
        var p = new DefinitivaDePrueba();
        var t = await p.RegistrarAnaAsync();

        var ajeno = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, Guid.NewGuid(), 1m, "x"), CancellationToken.None);
        ajeno.Error.Code.Should().Be("Payroll.Settlement.DeductionNotFound");

        var run = await p.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Status = PayrollRunStatus.Superseded;
        await p.D.Db.SaveChangesAsync();
        var cerrado = await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, p.Descuento(t, 1001).ObligationPublicId, 1m, "x"), CancellationToken.None);
        cerrado.Error.Code.Should().Be("Payroll.Settlement.NotDraft");
    }
}
