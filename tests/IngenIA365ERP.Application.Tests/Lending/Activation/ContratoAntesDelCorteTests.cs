using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, US8, T0727 (FR-142; data-model §10.1; interfaces.md §3): con Cartera activada, el contrato de obligaciones rechaza un pago con fecha
/// valor igual o anterior al corte de la carga activada (<c>Lending.Contract.BeforeCutoff</c>, «debía venir en la carga») sin escribir nada, y uno con
/// fecha valor del día siguiente al corte entra con su fecha valor; antes de activar sigue <c>Lending.NotActivated</c>. El corte es la víspera de la
/// activación (data-model §30.1). (nuevo)
/// </summary>
public sealed class ContratoAntesDelCorteTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private PagosDePrueba _p = null!;

    public async Task InitializeAsync()
    {
        _p = await PagosDePrueba.CrearAsync();
        await ActivadaElAsync(Hoy);
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Cartera activada en <paramref name="fecha"/> (corte = la víspera), o sin activar.</summary>
    private async Task ActivadaElAsync(DateOnly? fecha)
    {
        var setup = await _p.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = fecha;
        await _p.Db.SaveChangesAsync();
        _p.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Un_pago_con_fecha_valor_en_el_corte_o_antes_debia_venir_en_la_carga()
    {
        var operaciones = await _p.Db.LoanOperations.CountAsync();

        var enElCorte = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, Hoy.AddDays(-1)));
        var antes = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, Hoy.AddDays(-20)));

        foreach (var r in new[] { enElCorte, antes })
        {
            r.Result.Should().Be(ResultadoDelContrato.Rejected);
            r.Code.Should().Be(ErroresDeMigracion.CodigoAntesDelCorte);
            r.Message.Should().Contain("debía venir en la carga");
        }
        enElCorte.Data!["cutoffDate"].Should().Be(Hoy.AddDays(-1));
        enElCorte.Data["valueDate"].Should().Be(Hoy.AddDays(-1));
        (await _p.Db.LoanOperations.CountAsync()).Should().Be(operaciones, "un pago rechazado no escribe nada");
    }

    [Fact]
    public async Task Un_pago_con_fecha_valor_del_dia_siguiente_al_corte_entra_con_su_fecha_valor()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, Hoy));

        c.ValueDate.Should().Be(Hoy);
        (await _p.OperacionAsync(c.OperationPublicId)).ValueDate.Should().Be(Hoy);
    }

    [Fact]
    public async Task Antes_de_activar_sigue_NotActivated()
    {
        await ActivadaElAsync(null);

        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, Hoy.AddDays(-1)));

        r.Result.Should().Be(ResultadoDelContrato.Rejected);
        r.Code.Should().Be(ErroresDePagos.CodigoNoActivada);
    }
}
