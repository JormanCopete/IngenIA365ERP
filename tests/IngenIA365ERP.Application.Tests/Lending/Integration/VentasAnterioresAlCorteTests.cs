using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0571 (FR-143, C63, C99; mensajes.md §15.4; data-model §21.1, §21.5): una venta fechada hasta el corte de la carga activada
/// <b>nunca se procesa en vivo</b>: con la fila vigente de la conciliación responde <c>ObligacionCreada</c> <c>Migrated</c> o
/// <c>CancelledBeforeCutoff</c> (<c>Processed</c>), o <c>ObligacionRechazada</c> <c>AceptadaSinMigrar</c> / <c>AceptadaFueraDeCartera</c>
/// (<c>NotIncorporated</c>); sin fila —emitida después de activar— es <c>NotIncorporated</c> con <c>Lending.Sale.NotIncorporated</c>, no reprocesable,
/// y alerta <c>Cartera.VentaNoIncorporada</c> (dueño: pregunta I16; propuesta: fuera de Cartera).
/// </summary>
public sealed class VentasAnterioresAlCorteTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = VentasACreditoDePrueba.Hoy.AddDays(-30);
    private VentasACreditoDePrueba _v = null!;
    private LendingActivation _activacion = null!;

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _activacion = new LendingActivation
        {
            MigrationLoadId = 1, CutoffDate = Corte, ActivationDate = Corte.AddDays(1), RegisteredByUserId = _v.Cajera.Id, RegisteredAt = _v.G.UtcNow.AddDays(-29),
            SessionItemId = 1, CheckVersion = 2, BalanceCheckSha256 = "x", SalesReconciliationSha256 = "y",
        };
        _v.Db.LendingActivations.Add(_activacion);
        _v.Db.SaveChanges();
        var setup = _v.Db.LendingSetups.Single();
        setup.ActivationId = _activacion.Id;
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Contenido(Domain.Entities.Integration.Transactions.IntegrationMessage m) => JsonDocument.Parse(m.PayloadJson).RootElement;

    private VentasACreditoDePrueba.Venta Anterior() => _v.NuevaVenta(500_000m, fecha: Corte.AddDays(-5));

    private MigrationSaleReconciliation Fila(VentasACreditoDePrueba.Venta v, SaleReconciliationOutcome desenlace, int? credito = null, int version = 2)
    {
        var fila = new MigrationSaleReconciliation
        {
            MigrationLoadId = 1, CheckVersion = version, MessagePublicId = Guid.NewGuid(), SaleDocumentPublicId = v.Documento, InvoiceNumber = v.Numero,
            CreditPaymentPublicId = v.Pago, SaleDate = v.Fecha, Amount = v.Monto, PersonId = _v.Ana.Id, Outcome = desenlace, ResultingLoanId = credito,
        };
        _v.Db.MigrationSaleReconciliations.Add(fila);
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
        return fila;
    }

    [Theory]
    [InlineData(SaleReconciliationOutcome.Migrated, "Migrated")]
    [InlineData(SaleReconciliationOutcome.CancelledBeforeCutoff, "CancelledBeforeCutoff")]
    public async Task La_migrada_o_cancelada_antes_del_corte_responde_ObligacionCreada_con_su_estado(SaleReconciliationOutcome desenlace, string estado)
    {
        var venta = Anterior();
        var migrado = _v.O.Credito(_v.Ana, 500_000m, "CR-MIG");
        Fila(venta, desenlace, migrado.Id);

        var (_, r) = await _v.VenderAsync(venta);

        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(migrado.PublicId, null, migrado.Number));
        (await _v.Db.Loans.CountAsync()).Should().Be(1, "nunca se procesa como venta en vivo (FR-143)");
        var c = Contenido((await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single());
        (c.GetProperty("status").GetString(), c.GetProperty("numberSource").GetString(), c.GetProperty("loan").GetProperty("loanNumber").GetString())
            .Should().Be((estado, "Migration", "CR-MIG"));
        if (desenlace == SaleReconciliationOutcome.CancelledBeforeCutoff) c.GetProperty("amount").GetDecimal().Should().Be(0m);
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        (recibo.Status, recibo.LoanId, recibo.SaleReconciliationId is not null).Should().Be((InboundMessageStatus.Processed, (int?)migrado.Id, true));
    }

    [Theory]
    [InlineData(SaleReconciliationOutcome.NotMigratedWithBalance, "AceptadaSinMigrar")]
    [InlineData(SaleReconciliationOutcome.OutsideLendingD29, "AceptadaFueraDeCartera")]
    public async Task La_aceptada_sin_migrar_o_fuera_de_Cartera_no_se_incorpora(SaleReconciliationOutcome desenlace, string motivo)
    {
        var venta = Anterior();
        Fila(venta, desenlace);

        var (_, r) = await _v.VenderAsync(venta);

        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated));
        var c = Contenido((await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Single());
        (c.GetProperty("motive").GetString(), c.GetProperty("code").GetString(), c.GetProperty("retryable").GetBoolean())
            .Should().Be((motivo, ErroresDeVentasACredito.CodigoNoIncorporada, false));
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        recibo.Status.Should().Be(InboundMessageStatus.NotIncorporated);
        (await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync()).Retryable.Should().BeFalse();
        (await _v.Db.Loans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_fila_de_conciliacion_no_se_incorpora_y_alerta()
    {
        var venta = Anterior();

        var (_, r) = await _v.VenderAsync(venta);

        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated));
        var c = Contenido((await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Single());
        (c.GetProperty("code").GetString(), c.GetProperty("retryable").GetBoolean()).Should().Be((ErroresDeVentasACredito.CodigoNoIncorporada, false));
        await _v.D.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.VentaNoIncorporada), Arg.Any<CancellationToken>());
        (await _v.Db.Loans.CountAsync()).Should().Be(0, "nunca se procesa como venta en vivo");
    }

    [Fact]
    public async Task Solo_cuenta_la_version_del_cuadre_de_la_activacion()
    {
        var venta = Anterior();
        Fila(venta, SaleReconciliationOutcome.Migrated, _v.O.Credito(_v.Ana, 500_000m, "CR-VIEJA").Id, version: 1);

        var (_, r) = await _v.VenderAsync(venta);

        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated),
            "la fila de una versión anterior del cuadre no rige: la activación usó la 2");
    }

    [Fact]
    public async Task Una_venta_posterior_al_corte_se_procesa_en_vivo()
    {
        var venta = _v.NuevaVenta(500_000m);

        var (_, r) = await _v.VenderAsync(venta);

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await _v.CreditoDeAsync(venta)).Origin.Should().Be(LoanOrigin.Commercial);
    }
}
