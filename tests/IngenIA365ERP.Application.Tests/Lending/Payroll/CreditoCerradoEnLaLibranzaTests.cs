using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Application.Lending.Payroll.Tasks;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 028 (E2, T067, T068, T073; R7, FR-022, HU4-4, H1): la respuesta de la pagaduría sobre un crédito que se cerró después de enviar su línea —lo recogió
/// el desembolso de otro, se canceló en caja, se anuló o se novó—. El resultado se guarda marcado («descuento de un crédito cancelado») y no deja no
/// descontado, ni regla del convenio, ni aviso al deudor, ni cuenta para la alerta del archivo; lo descontado no queda pendiente de giro, así que ni la tarea
/// de giros atrasados ni la consulta del convenio lo ven, y tampoco exige referencia. Un resultado de antes de la marca sale igual al cerrarse el crédito
/// y vuelve si se reabre. Regresión: con el crédito vigente todo sigue como siempre. (nuevo)
/// </summary>
public sealed class CreditoCerradoEnLaLibranzaTests : IAsyncLifetime
{
    private static readonly DateOnly Descuento = new(2027, 1, 30);

    private LibranzaDePrueba _l = null!;
    private PayrollDeductionFileDto _archivo = null!;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        _archivo = await _l.ArchivoAsync();
        await _l.ConfirmarYConsumirAsync(_archivo.PublicId);
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Lleva el crédito de Ana a <paramref name="estado"/> (como lo dejaría una cancelación o una reapertura).</summary>
    private async Task EstadoAsync(LoanStatus estado)
    {
        var credito = await _l.Db.Loans.SingleAsync(x => x.Id == _l.Credito.Id);
        credito.Status = estado;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
    }

    private async Task<decimal> EsperadoAsync() => (await _l.LineaAsync(_archivo.PublicId, _l.Credito)).TotalAmount;

    private async Task<int> GirosAtrasadosAsync(DateOnly dia)
    {
        _l.G.UtcNow = dia.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc);
        _l.G.HoyLocal = dia;
        _l.G.ActuarComoProceso();
        var r = await _l.TareaDeGiros().Handle(new AlertOverduePayrollTransfersCommand(), default);
        _l.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task El_no_descontado_de_un_credito_cerrado_no_deja_no_aplicado_ni_aviso_al_deudor_ni_alerta()
    {
        await EstadoAsync(LoanStatus.PaidOff);

        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 0m, null)));

        var r = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        (r.Outcome, r.LoanClosedAtResult).Should().Be((PayrollResultOutcome.NotDeducted, true), "queda como «descuento de un crédito cancelado»");
        (await _l.Db.InstallmentNonDeductions.AnyAsync()).Should().BeFalse("no se cobra una cuota de un crédito cancelado");
        (await _l.Db.DebtorMessages.AnyAsync()).Should().BeFalse("no se le avisa al deudor como si estuviera en mora");
        _l.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.NoAplicadoDePagaduria);
    }

    [Fact]
    public async Task Lo_descontado_de_un_credito_cerrado_no_queda_pendiente_de_giro_ni_alerta_atrasado_ni_avisa()
    {
        var esperado = await EsperadoAsync();
        await EstadoAsync(LoanStatus.PaidOff);

        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));

        var r = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        (r.Outcome, r.DeductedAmount, r.LoanClosedAtResult).Should().Be((PayrollResultOutcome.Deducted, esperado, true));
        (await PendientesDeGiro.Consulta(_l.Db).AnyAsync()).Should().BeFalse("lo descontado de un crédito cerrado no está pendiente de giro");
        var pendientes = await _l.Pendientes().Handle(new ListPendingPayrollTransfersQuery(_l.Convenio.PublicId), default);
        pendientes.Value.Should().BeEmpty();

        var superado = r.TransferDueDate!.Value.AddDays(r.PaymasterArrearsThresholdDays!.Value + 1);
        (await GirosAtrasadosAsync(superado)).Should().Be(0, "no se alerta giro atrasado de un crédito cancelado");
        _l.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.GiroDePagaduriaAtrasado);
        (await _l.Db.DebtorMessages.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task El_cuadre_dice_descuento_de_un_credito_cancelado_sin_pendiente_de_giro()
    {
        var esperado = await EsperadoAsync();
        await EstadoAsync(LoanStatus.PaidOff);
        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));
        _l.G.Conceder();

        var r = await new PayrollDeductionReconciliationReportQueryHandler(_l.Db, _l.G.Alcance)
            .Handle(new PayrollDeductionReconciliationReportQuery(new FiltrosDeInformeDeCartera(), _l.Convenio.PublicId, 2027, 1), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var fila = r.Value.Filas.Should().ContainSingle().Subject;
        fila.Valores[3].Should().Be(esperado, "lo descontado se sigue viendo");
        fila.Valores[4].Should().Be(0m, "pero no queda pendiente de giro");
        fila.Valores[7].Should().Be(PayrollDeductionReconciliationReportQueryHandler.TextoDeCreditoCerrado);
    }

    [Fact]
    public async Task Un_resultado_de_antes_de_la_marca_sale_al_cerrarse_el_credito_y_vuelve_si_se_reabre()
    {
        var esperado = await EsperadoAsync();
        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));
        (await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync()).LoanClosedAtResult.Should().BeFalse("el crédito seguía vigente");
        (await PendientesDeGiro.Consulta(_l.Db).CountAsync()).Should().Be(1, "regresión: con el crédito vigente queda pendiente de giro");

        await EstadoAsync(LoanStatus.PaidOff);
        (await PendientesDeGiro.Consulta(_l.Db).AnyAsync()).Should().BeFalse("se mira el estado del crédito al consultar");

        await EstadoAsync(LoanStatus.Active);
        (await PendientesDeGiro.Consulta(_l.Db).CountAsync()).Should().Be(1, "si el crédito se reabre, su pendiente vuelve");
    }

    [Fact]
    public async Task Regresion_con_el_credito_vigente_el_no_descontado_sigue_como_siempre()
    {
        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 0m, null)));

        (await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync()).LoanClosedAtResult.Should().BeFalse();
        (await _l.Db.InstallmentNonDeductions.AsNoTracking().SingleAsync()).Origin.Should().Be(NonDeductionOrigin.PaymasterResult);
        (await _l.Db.DebtorMessages.AnyAsync()).Should().BeTrue();
        _l.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.NoAplicadoDePagaduria);
    }

    [Theory]
    [InlineData(LoanStatus.PaidOff, true)]
    [InlineData(LoanStatus.Voided, true)]
    [InlineData(LoanStatus.Novated, true)]
    [InlineData(LoanStatus.Active, false)]
    [InlineData(LoanStatus.WrittenOff, false)]
    public void Un_credito_cerrado_es_cancelado_anulado_o_novado_y_el_castigado_no(LoanStatus estado, bool cerrado) =>
        PendientesDeGiro.CreditoCerrado(estado).Should().Be(cerrado);
}
