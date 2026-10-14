using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, bloque 12C (api.md §16.3): los hechos del cierre mensual —reclasificación, deterioro y sus reversos— no tienen filas del libro,
/// así que su lista por crédito sale de su contenido. Defecto que destapó la e2e del cierre: la lista de hechos filtrada por crédito no los
/// traía y el detalle de su parte llegaba sin créditos.
/// </summary>
public class HechosDelCierrePorCreditoTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await _c.LineaDelBaseAsync(RatingCategory.C, deterioroDeCapital: 100_000m);
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task La_lista_de_hechos_del_credito_trae_los_del_cierre_y_su_parte_lista_el_credito_por_concepto()
    {
        var cierre = await _c.CerrarAsync(2026, 12);
        _c.G.ActuarComo(_c.Jefe);

        var hechos = await new ListLendingAccountingFactsQueryHandler(_c.Db, _c.G.Alcance)
            .Handle(new ListLendingAccountingFactsQuery(LoanPublicId: _c.Credito.PublicId, PageSize: 50), default);

        hechos.IsSuccess.Should().BeTrue(hechos.IsFailure ? hechos.Error.Message : string.Empty);
        hechos.Value.Items.Select(h => h.Type).Should().Contain([CarteraReclasificadaPorCalificacionV1.Type, DeterioroCalculadoV1.Type]);

        var parte = await _c.Db.AccountingFactParts.AsNoTracking()
            .FirstAsync(p => p.MonthlyCloseId == cierre.Id && p.FactType == CarteraReclasificadaPorCalificacionV1.Type);
        var detalle = await new GetAccountingFactPartQueryHandler(_c.Db, _c.G.Alcance).Handle(new GetAccountingFactPartQuery(parte.PublicId), default);

        detalle.IsSuccess.Should().BeTrue(detalle.IsFailure ? detalle.Error.Message : string.Empty);
        var credito = detalle.Value.Loans.Items.Should().ContainSingle().Subject;
        credito.LoanPublicId.Should().Be(_c.Credito.PublicId);
        credito.ByConcept.Should().ContainKey("Principal").WhoseValue.Should().Be(3_000_000m);
        credito.ByConcept.Should().ContainKey("Principal · Individual", "el deterioro que se reclasifica va aparte del saldo");
    }
}
