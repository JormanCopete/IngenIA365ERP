using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.WriteOffs;

/// <summary>
/// La cartera castigada (feature 013, K3, US14, T0898; FR-096, US14-3): un crédito en mora que ya causaba en suspenso se castiga —el suspenso pasa a
/// control en el <c>CreditoCastigado</c> y sale <c>CausacionSuspendida</c> con causa <c>WriteOff</c>—, sigue causando su interés en suspenso con la marca
/// de castigado (en control), sigue en la consulta de obligaciones y en el contrato, un pago sobre él es una recuperación (<c>RecuperacionDeCastigada</c> en la
/// unidad del <c>PagoAplicado</c>) y la vista <c>charge-offs</c> lo reúne con lo castigado y lo recuperado. (nuevo)
/// </summary>
public sealed class CarteraCastigadaTests : IAsyncLifetime
{
    private CastigosDePrueba _c = null!;
    private DateOnly _castigo;

    public async Task InitializeAsync()
    {
        _c = await CastigosDePrueba.CrearAsync();
        var vencimiento = await _c.N.PrimerVencimientoAsync();
        await _c.P.CausarHastaAsync(vencimiento.AddDays(75));
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync(deterioroDeCapital: 3_000_000m, deterioroDeIntereses: 1_000_000m));
        var punto = await _c.AprobadoAsync(lote);
        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _castigo = _c.G.HoyLocal;
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task El_suspenso_pasa_a_control_y_la_suspension_por_el_castigo_sale_aparte()
    {
        var castigo = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoCastigadoV1.Type);
        castigo.PayloadJson.Should().Contain("suspenseWrittenOff").And.Contain("CurrentInterestInSuspense");
        var suspension = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CausacionSuspendidaV1.Type).OrderBy(m => m.Id).LastAsync();
        suspension.PayloadJson.Should().Contain("WriteOff").And.Contain(castigo.PublicId.ToString(), "causeFact es el castigo");
    }

    [Fact]
    public async Task Sigue_causando_en_suspenso_con_la_marca_de_castigado()
    {
        await _c.P.CausarHastaAsync(_castigo.AddDays(5));

        var despues = await _c.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == _c.Credito.Id && t.Kind == LoanTransactionKind.Accrual && t.ValueDate > _castigo).ToListAsync();
        despues.Should().NotBeEmpty("el ciclo diario sigue causando la cartera castigada");
        despues.Should().OnlyContain(t => t.Concept == LoanConcept.CurrentInterestInSuspense || t.Concept == LoanConcept.DefaultInterestInSuspense
                                          || t.Concept == LoanConcept.Insurance);
        var causados = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == "InteresCausado").Select(m => m.PayloadJson).ToListAsync();
        causados.Should().Contain(p => p.Contains("\"writtenOff\":true"), "la causación del castigado lleva su marca: Contabilidad la lleva a control");
        var control = await _c.Db.LoanControls.AsNoTracking().SingleAsync(x => x.LoanId == _c.Credito.Id);
        control.LastAccruedDate.Should().Be(_castigo.AddDays(5));
        await _c.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Sigue_en_la_consulta_de_obligaciones_y_un_pago_es_recuperacion()
    {
        var obligaciones = await _c.P.Obligaciones().ObligacionesAsync(new ConsultaDeObligaciones([_c.D.O.Ana.PublicId], _c.G.HoyLocal, false), default);
        var obligacion = obligaciones.People.Single().Obligations.Should().ContainSingle(o => o.ObligationPublicId == _c.Credito.PublicId).Subject;
        (obligacion.IsLive, obligacion.TotalBalance > 3_000_000m).Should().Be((true, true));

        var pago = await _c.P.AplicadoAsync(_c.P.Pago(ModalidadDePago.FreeAmount, 200_000m));

        var recuperacion = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == RecuperacionDeCastigadaV1.Type);
        var principal = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoAplicadoV1.Type);
        recuperacion.OriginPublicId.Should().Be(principal.OriginPublicId, "en la unidad del PagoAplicado");
        var filas = await _c.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == _c.Credito.Id && t.Kind == LoanTransactionKind.Recovery).ToListAsync();
        filas.Sum(f => f.Amount).Should().Be(-200_000m);
        pago.Should().NotBeNull();
        await _c.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_vista_charge_offs_reune_lo_castigado_y_lo_recuperado()
    {
        await _c.P.AplicadoAsync(_c.P.Pago(ModalidadDePago.FreeAmount, 150_000m));
        var operacion = await _c.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.LoanId == _c.Credito.Id && o.Kind == LoanOperationKind.WriteOff);

        var r = await new ChargeOffsReportQueryHandler(_c.Db, _c.G.Alcance, _c.G.Reloj)
            .Handle(new ChargeOffsReportQuery(new FiltrosDeInformeDeCartera { From = _castigo, To = _c.G.HoyLocal }), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Columnas.Select(c => c.Nombre).Should().Equal("Crédito", "Deudor", "Fecha de castigo", "Punto de acta", "Saldo castigado", "Recuperado",
            "Gestiones", "Último contacto", "_credito");
        var fila = r.Value.Filas.Should().ContainSingle().Subject.Valores;
        fila[0].Should().Be(_c.Credito.Number);
        fila[2].Should().Be(_castigo);
        ((string)fila[3]!).Should().StartWith("Acta ");
        fila[4].Should().Be(operacion.Amount);
        fila[5].Should().Be(150_000m);

        _c.G.AlcanceActual = Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;
        (await new ChargeOffsReportQueryHandler(_c.Db, _c.G.Alcance, _c.G.Reloj)
            .Handle(new ChargeOffsReportQuery(new FiltrosDeInformeDeCartera { From = _castigo, To = _c.G.HoyLocal }), default)).Value.Filas.Should().BeEmpty();
    }
}
