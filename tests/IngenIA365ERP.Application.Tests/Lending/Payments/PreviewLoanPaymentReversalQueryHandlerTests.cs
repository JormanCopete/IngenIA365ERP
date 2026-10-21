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
/// Feature 013, US5, T0520 (api.md §10; FR-080, FR-081, T31, C37; nombre de quickstart §1.5): la vista previa del reverso no escribe y muestra lo que
/// restituye, la redistribución de los pagos posteriores, los documentos a anular (vacío hasta US13) y si avisa al recaudador; un pago de Nómina no se
/// reversa desde Cartera y una operación que no es pago responde <c>Lending.LoanOperation.NotPayment</c>.
/// </summary>
public class PreviewLoanPaymentReversalQueryHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    private PagosDePrueba _p = null!;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task La_vista_previa_muestra_lo_que_restituye_y_la_redistribucion_del_pago_posterior_sin_escribir()
    {
        var (primero, segundo) = await PagosEnPantalla.DosPagosAsync(_p);
        var antes = await _p.Db.LoanOperations.CountAsync();

        var r = await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, primero), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.PreviewSha256.Should().HaveLength(64);
        r.Value.Reversal.ByConcept.Values.Sum().Should().BeGreaterThan(100_000m,
            "restituye lo que el pago cubrió y deshace los ajustes de causación que el pago dejó en los días ya causados");
        var reasignado = r.Value.Reallocations.Should().ContainSingle().Subject;
        reasignado.OperationPublicId.Should().Be(segundo);
        reasignado.After.Should().NotBeEquivalentTo(reasignado.Before);
        r.Value.DocumentsToVoid.Should().BeEmpty("la vigencia de documentos llega con US13");
        (r.Value.RatingNow, r.Value.RatingAfter).Should().Be(((string?)null, (string?)null));
        r.Value.NotifiesCollector.Should().BeTrue("el recaudador de prueba tiene destino Receipts");
        (await _p.Db.LoanOperations.CountAsync()).Should().Be(antes);
        (await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, primero), default)).Value.PreviewSha256
            .Should().Be(r.Value.PreviewSha256, "la huella es estable mientras nada cambie");
    }

    [Fact]
    public async Task Un_pago_de_Nomina_no_se_reversa_desde_Cartera()
    {
        var c = (await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1_000m, clave: $"NOM:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}",
            medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction), recaudador: Recaudador.Nomina)).Value!;

        var r = await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, c.OperationPublicId), default);

        r.Error.Code.Should().Be("Lending.Reversal.OwnedByCollector");
        r.Error.Message.Should().Contain("sólo Nómina lo reversa");
    }

    [Fact]
    public async Task Una_operacion_que_no_es_pago_responde_NotPayment()
    {
        var desembolso = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.LoanId == _p.Credito.Id && o.Kind == LoanOperationKind.Disbursement);
        var r = await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, desembolso.PublicId), default);
        r.Error.Code.Should().Be("Lending.LoanOperation.NotPayment");
    }
}
