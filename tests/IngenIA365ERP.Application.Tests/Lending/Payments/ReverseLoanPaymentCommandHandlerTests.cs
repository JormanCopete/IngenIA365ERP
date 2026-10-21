using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0520 (api.md §10; FR-080, FR-081, T31, T33, C37; nombre de quickstart §1.5): el reverso por pantalla compara la vista previa
/// (<c>Lending.Reversal.PreviewChanged</c>), rechaza los pagos de Nómina y las recogidas (<c>Lending.Reversal.OwnedByCollector</c>) y los reversos
/// (<c>Lending.Reversal.OfReversal</c>), redistribuye los posteriores, reabre un crédito cancelado y emite <c>PagoReversado</c> y
/// <c>PagoReversadoPorCartera</c>.
/// </summary>
public class ReverseLoanPaymentCommandHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    private PagosDePrueba _p = null!;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> HuellaAsync(Guid operacion) =>
        (await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, operacion), default)).Value.PreviewSha256;

    [Fact]
    public async Task Reversar_desde_Cartera_redistribuye_el_posterior_y_emite_PagoReversado_y_PagoReversadoPorCartera()
    {
        var (primero, segundo) = await PagosEnPantalla.DosPagosAsync(_p);
        var huella = await HuellaAsync(primero);

        var r = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, primero, "Cheque devuelto", huella) { OperationKey = Guid.NewGuid() }, default);
        _p.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Messages.Select(m => m.Type).Should().BeEquivalentTo([PagoReversadoV1.Type, PagoReversadoPorCarteraV1.Type]);
        var numeroDelSegundo = (await _p.OperacionAsync(segundo)).Number;
        r.Value.Reallocations.Should().Equal(numeroDelSegundo);
        var aviso = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoReversadoPorCarteraV1.Type);
        (await _p.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(d => d.MessageId == aviso.Id)).Destination.Should().Be(IntegrationDestinations.Receipts);
        aviso.RelatedPublicId.Should().Be(primero);
        (await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoReversadoV1.Type)).PayloadJson.Should().Contain("\"initiatedBy\":\"Lending\"");
        var pago = await _p.OperacionAsync(primero);
        pago.ReversedByOperationId.Should().NotBeNull();
        var reasignaciones = await _p.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.OperationId == pago.ReversedByOperationId && t.Kind == LoanTransactionKind.PaymentReallocation).ToListAsync();
        reasignaciones.Should().NotBeEmpty("el pago posterior se redistribuye (FR-081)");
        (await _p.EventosAsync()).Should().Contain("Lending.Operation.Registered");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Una_vista_previa_vieja_responde_PreviewChanged_con_la_huella_nueva()
    {
        var (primero, _) = await PagosEnPantalla.DosPagosAsync(_p);
        var r = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, primero, "x", new string('0', 64)) { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be("Lending.Reversal.PreviewChanged");
        Disbursements.DesembolsoDePrueba.Datos(r.Error).GetProperty("previewSha256").GetString().Should().Be(await HuellaAsync(primero));
    }

    [Theory]
    [InlineData("NOM", ClaseDeRecaudador.Payroll, ClaseDeMedioDeRecaudo.OwnPayrollDeduction)]
    [InlineData("CAR", ClaseDeRecaudador.OwnLoanPayoff, ClaseDeMedioDeRecaudo.InternalOffset)]
    public async Task Los_pagos_de_Nomina_y_las_recogidas_los_reversa_su_recaudador(string codigo, ClaseDeRecaudador clase, ClaseDeMedioDeRecaudo medio)
    {
        var c = (await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1_000m, clave: $"{codigo}:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}", medio: medio),
            recaudador: new Recaudador(codigo, clase))).Value!;

        var r = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, c.OperationPublicId, "x", "h") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.Reversal.OwnedByCollector");
        Disbursements.DesembolsoDePrueba.Datos(r.Error).GetProperty("collectorClass").GetString().Should().Be(clase.ToString());
    }

    [Fact]
    public async Task Un_reverso_no_se_reversa()
    {
        var (primero, _) = await PagosEnPantalla.DosPagosAsync(_p);
        var r = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, primero, "x", await HuellaAsync(primero)) { OperationKey = Guid.NewGuid() }, default);
        _p.Db.ChangeTracker.Clear();
        var otra = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, r.Value.ReversalOperationPublicId, "x", "h")
            { OperationKey = Guid.NewGuid() }, default);
        otra.Error.Code.Should().Be("Lending.Reversal.OfReversal");
        var deNuevo = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, primero, "x", "h") { OperationKey = Guid.NewGuid() }, default);
        deNuevo.Error.Code.Should().Be("Lending.Reversal.AlreadyReversed");
    }

    [Fact]
    public async Task Reversar_el_pago_que_cancelo_reabre_el_credito()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FullPayoff, 3_000_000m));
        var r = await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, c.OperationPublicId, "Error de caja",
            await HuellaAsync(c.OperationPublicId)) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Reopened.Should().BeTrue();
        _p.Db.ChangeTracker.Clear();
        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.Active);
    }
}
