using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, US14, T0235 (FR-097, FR-204 parte R0): una fila de <c>RCD_CashTransactions</c> por recibo con efectivo (pagador, titular, quien efectúa); la anulación escribe
/// otra con signo contrario; el acumulado del mes sólo con lo de Recaudo; las señales quedan en <c>RCD_ComplianceAlertsLog</c> con la versión del parámetro y alertan al
/// oficial (<c>Recaudo.AcumuladoDeEfectivo</c>, <c>Recaudo.Fraccionamiento</c>). Prueba escrita, sin correr.
/// </summary>
public class RegistroDeEfectivoTests
{
    private readonly CajaConContratos _k = new();

    private RegistroDeEfectivo Registro => new(_k.C.E.Db, _k.C.E.Lector, _k.C.E.Alertas, _k.C.E.Reloj);

    private ConfirmacionDeRecibo Confirmacion() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.E.Reloj, _k.C.Cerrojo, _k.C.Numerador, _k.C.E.Parametros, _k.C.Presente,
        new Application.Accounting.Receipts.ContabilidadParaRecaudo(Substitute.For<MediatR.ISender>()), _k.Obligaciones, _k.Cuentas, _k.Listas, _k.C.Emision, _k.C.E.Alertas,
        Registro);

    private async Task<ReceiptDto> ReciboAsync(decimal efectivo)
    {
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, efectivo)], [_k.Efectivo(efectivo)]);
        var r = await Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return r.Value;
    }

    [Fact]
    public async Task Cada_recibo_con_efectivo_deja_su_fila_con_el_mes()
    {
        await _k.AbrirAsync();
        var recibo = await ReciboAsync(150_000m);

        var fila = await _k.C.E.Db.ReceiptsCashTransactions.SingleAsync();
        fila.DocumentPublicId.Should().Be(recibo.PublicId);
        fila.DocumentKind.Should().Be(CashTransactionDocumentKind.Receipt);
        fila.CashAmount.Should().Be(150_000m);
        fila.PersonId.Should().Be(_k.Asociado.Id);
        fila.Month.Should().Be(EscenarioDeRecaudo.Hoy.Year * 100 + EscenarioDeRecaudo.Hoy.Month);
    }

    [Fact]
    public async Task El_acumulado_del_mes_alerta_al_oficial_con_la_version_del_parametro()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionesMultiples, "250000");
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, "900000000");
        await _k.AbrirAsync();
        await ReciboAsync(150_000m);
        await ReciboAsync(150_000m);

        var senal = await _k.C.E.Db.ReceiptsComplianceAlertLogs.FirstAsync(s => s.Signal == SenalDeEfectivo.Accumulated);
        senal.Total.Should().Be(300_000m);
        senal.ParameterVersion.Should().NotBeNullOrEmpty();
        await _k.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoAcumuladoDeEfectivo), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_anulacion_escribe_la_fila_con_signo_contrario()
    {
        await _k.AbrirAsync();
        var recibo = await ReciboAsync(150_000m);
        var fila = await _k.C.E.Db.IssuedReceipts.SingleAsync(r => r.PublicId == recibo.PublicId);

        await Registro.AlAnularAsync(fila, Guid.NewGuid(), "AN-1", default);
        await _k.C.E.Db.SaveChangesAsync();

        var filas = await _k.C.E.Db.ReceiptsCashTransactions.OrderBy(t => t.Id).ToListAsync();
        filas.Select(f => f.CashAmount).Should().Equal(150_000m, -150_000m);
        filas[1].DocumentKind.Should().Be(CashTransactionDocumentKind.Voiding);
        filas.Sum(f => f.CashAmount).Should().Be(0m, "el acumulado del mes descuenta la anulación");
    }
}
