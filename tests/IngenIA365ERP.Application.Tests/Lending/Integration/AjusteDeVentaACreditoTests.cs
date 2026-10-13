using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0570 (FR-054, FR-143, C17; mensajes.md §12.3, §15.4): el consumo de un <c>AjusteDeVentaACredito</c>: la nota crédito o la
/// devolución baja el capital, reversa el interés de lo devuelto, recalcula y deja el exceso como saldo a favor (<c>CreditoAjustadoDesdeComercial</c> y
/// su <c>SaldoAFavorReconocido</c>); la anulación deja el crédito sin efecto desde su origen y lo pagado como saldo a favor; la nota débito se evalúa
/// como venta; el reemplazo crea un crédito nuevo enlazado; el ajuste espera a su venta; el ajuste hasta el corte de una venta migrada se da por
/// procesado; y el de una venta no incorporada responde <c>SinObligacion</c>.
/// </summary>
public sealed class AjusteDeVentaACreditoTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Contenido(Domain.Entities.Integration.Transactions.IntegrationMessage m) => JsonDocument.Parse(m.PayloadJson).RootElement;

    private void Avanzar(int dias)
    {
        _v.G.HoyLocal = VentasACreditoDePrueba.Hoy.AddDays(dias);
        _v.G.UtcNow = VentasACreditoDePrueba.Hoy.AddDays(dias).ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
    }

    private static AjusteDeVentaACreditoV1 Ajuste(VentasACreditoDePrueba.Venta v, Guid mensajeDeLaVenta, string clase, decimal monto, CreditTermsV1? terminos = null) =>
        new()
        {
            AdjustmentClass = clase, Amount = monto, OriginalMessageId = mensajeDeLaVenta, OriginalCreditPaymentPublicId = v.Pago,
            OriginalDocument = new DocumentRefV1 { PublicId = v.Documento, DocumentClass = DocumentClass.PosEquivalentDocument, Number = v.Numero },
            PaymentMeansCode = "CREDASOC", Terms = terminos, Reason = "Devolución de mercancía", AccountsReceivableRecordedBy = "Cartera",
        };

    private async Task<ResultadoDeConsumo> AjustarAsync(VentasACreditoDePrueba.Venta v, AjusteDeVentaACreditoV1 a, int dias, string numero = "NC01-1")
    {
        Avanzar(dias);
        var id = await _v.EmitirAsync(v, a, relacionado: v.Documento, documento: Guid.NewGuid(), numero: numero, fecha: _v.G.HoyLocal);
        return await _v.ConsumirAsync(id);
    }

    // ------------------------------------------------------------------------------------------------------- notas --

    [Fact]
    public async Task La_nota_credito_parcial_baja_el_capital_reversa_el_interes_y_recalcula_el_plan()
    {
        var venta = _v.NuevaVenta(900_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);

        var r = await AjustarAsync(venta, Ajuste(venta, mensaje, "CreditNote", -300_000m), dias: 15);

        var credito = await _v.CreditoDeAsync(venta);
        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(credito.PublicId, null, credito.Number));
        credito.Status.Should().Be(LoanStatus.Active);
        var ajuste = await _v.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.CommercialAdjustment);
        (ajuste.Amount, ajuste.ValueDate).Should().Be((-300_000m, VentasACreditoDePrueba.Hoy.AddDays(15)));
        var filas = await _v.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == ajuste.Id).ToListAsync();
        filas.Where(f => f.Concept == LoanConcept.Principal).Sum(f => f.Amount).Should().Be(-300_000m);
        filas.Should().Contain(f => f.Concept == LoanConcept.CurrentInterest && f.Amount < 0m, "se reversa el interés causado sobre lo devuelto");
        (await _v.Db.RepaymentScheduleVersions.AsNoTracking().Where(p => p.LoanId == credito.Id).MaxAsync(p => p.VersionNumber)).Should().Be(2,
            "el plan se recalcula según NoteRecalculation");

        var hecho = (await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Single();
        var c = Contenido(hecho);
        (c.GetProperty("adjustmentClass").GetString(), c.GetProperty("voidedFromOrigin").GetBoolean()).Should().Be(("CreditNote", false));
        hecho.RelatedPublicId.Should().Be((await _v.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.Disbursement)).PublicId,
            "related = el desembolso en especie");
        (await _v.MensajesAsync(SaldoAFavorReconocidoV1.Type)).Should().BeEmpty();
    }

    [Fact]
    public async Task Una_devolucion_mayor_que_lo_que_se_debe_deja_el_exceso_como_saldo_a_favor()
    {
        var venta = _v.NuevaVenta(600_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);

        await AjustarAsync(venta, Ajuste(venta, mensaje, "Return", -700_000m), dias: 5);

        var credito = await _v.CreditoDeAsync(venta);
        credito.Status.Should().Be(LoanStatus.PaidOff, "no queda nada que cobrar");
        var saldo = (await _v.MensajesAsync(SaldoAFavorReconocidoV1.Type)).Should().ContainSingle().Subject;
        Contenido(saldo).GetProperty("cause").GetString().Should().Be("CommercialAdjustment");
        Contenido((await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Single()).GetProperty("creditBalanceRecognized").GetDecimal()
            .Should().Be(100_000m);
    }

    [Fact]
    public async Task La_anulacion_deja_el_credito_sin_efecto_desde_su_origen_y_lo_pagado_como_saldo_a_favor()
    {
        var venta = _v.NuevaVenta(600_000m, numero: "PV01-ANUL");
        ConCobroProvisional("PV01-ANUL", 100_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);

        await AjustarAsync(venta, Ajuste(venta, mensaje, "VoidingByDianRejection", -600_000m), dias: 20);

        var credito = await _v.CreditoDeAsync(venta);
        (credito.Status, credito.VoidedOn).Should().Be((LoanStatus.Voided, (DateOnly?)VentasACreditoDePrueba.Hoy.AddDays(20)));
        var c = Contenido((await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Single());
        (c.GetProperty("voidedFromOrigin").GetBoolean(), c.GetProperty("creditBalanceRecognized").GetDecimal()).Should().Be((true, 100_000m),
            "lo pagado (el cobro provisional que quedó de anticipo) vuelve como saldo a favor");
    }

    [Fact]
    public async Task La_nota_debito_sube_el_capital_y_se_evalua_como_una_venta()
    {
        var venta = _v.NuevaVenta(600_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);
        _v.O.ListasDe[_v.Ana.Id] = ScreeningOutcome.Match;

        var r = await AjustarAsync(venta, Ajuste(venta, mensaje, "DebitNote", 200_000m), dias: 10);

        var credito = await _v.CreditoDeAsync(venta);
        r.Should().BeOfType<ResultadoDeConsumo.ValidationFailed>().Which.Code.Should().Be("Compliance.RestrictiveList.Match");
        credito.SaleObligationStatus.Should().Be(SaleObligationStatus.ValidationFailed, "la nota débito evaluada en contra marca el crédito (FR-052)");
        var ajuste = await _v.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.CommercialAdjustment);
        (await _v.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == ajuste.Id && t.Concept == LoanConcept.Principal).SumAsync(t => t.Amount))
            .Should().Be(200_000m);
        Contenido((await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Single()).GetProperty("validationFailed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task El_reemplazo_crea_un_credito_nuevo_enlazado_al_anulado()
    {
        var venta = _v.NuevaVenta(600_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);
        await AjustarAsync(venta, Ajuste(venta, mensaje, "VoidingByDianRejection", -600_000m), dias: 2, numero: "AN01-1");

        var terminos = new CreditTermsV1
        {
            TermUnit = "Months", Term = 3, Installments = 3, Periodicity = "Monthly", FirstDueDate = VentasACreditoDePrueba.Hoy.AddDays(3).AddMonths(1),
            FinalDueDate = VentasACreditoDePrueba.Hoy.AddDays(3).AddMonths(3),
        };
        var r = await AjustarAsync(venta, Ajuste(venta, mensaje, "Replacement", 650_000m, terminos), dias: 3, numero: "PV01-REEMP");

        var anulado = await _v.Db.Loans.AsNoTracking().SingleAsync(l => l.Status == LoanStatus.Voided);
        var nuevo = await _v.Db.Loans.AsNoTracking().SingleAsync(l => l.Id != anulado.Id);
        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(nuevo.PublicId, null, nuevo.Number));
        (nuevo.OriginLoanId, nuevo.OriginLinkKind, nuevo.ApprovedAmount).Should().Be(((int?)anulado.Id, (LoanLinkKind?)LoanLinkKind.Replacement, 650_000m));
        var desembolsos = await _v.MensajesAsync(CreditoDesembolsadoV1.Type);
        Contenido(desembolsos[^1]).GetProperty("inKind").GetProperty("replacesLoan").GetProperty("loanNumber").GetString().Should().Be(anulado.Number);
        var creada = (await _v.MensajesAsync(ObligacionCreadaV1.Type))[^1];
        Contenido(creada).GetProperty("replacesLoan").GetProperty("loanPublicId").GetGuid().Should().Be(anulado.PublicId);
    }

    // ---------------------------------------------------------------------------------------------------- orden y corte --

    [Fact]
    public async Task El_ajuste_espera_a_su_venta()
    {
        var venta = _v.NuevaVenta();
        var r = await AjustarAsync(venta, Ajuste(venta, Guid.NewGuid(), "CreditNote", -100_000m), dias: 1);

        r.Should().BeOfType<ResultadoDeConsumo.Retry>();
        (await _v.Db.LendingInboundMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_ajuste_de_una_venta_no_incorporada_responde_SinObligacion()
    {
        var venta = _v.NuevaVenta();
        var original = Recibo(venta, InboundMessageStatus.NotIncorporated);

        var r = await AjustarAsync(venta, Ajuste(venta, original.MessagePublicId, "CreditNote", -100_000m), dias: 1);

        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated));
        var c = Contenido((await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Single());
        (c.GetProperty("motive").GetString(), c.GetProperty("code").GetString(), c.GetProperty("retryable").GetBoolean())
            .Should().Be(("SinObligacion", ErroresDeVentasACredito.CodigoNoIncorporada, false));
        (await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync(x => x.MessagePublicId != original.MessagePublicId)).Status
            .Should().Be(InboundMessageStatus.NotIncorporated);
    }

    [Fact]
    public async Task El_ajuste_hasta_el_corte_de_una_venta_migrada_se_da_por_procesado_sin_hechos()
    {
        var venta = _v.NuevaVenta(fecha: VentasACreditoDePrueba.Hoy.AddDays(-60));
        var activacion = Activacion(corte: VentasACreditoDePrueba.Hoy.AddDays(-30));
        var original = Recibo(venta, InboundMessageStatus.Processed, conciliacion: 1);
        _ = activacion;
        _v.G.HoyLocal = VentasACreditoDePrueba.Hoy;

        var id = await _v.EmitirAsync(venta, Ajuste(venta, original.MessagePublicId, "CreditNote", -50_000m), relacionado: venta.Documento,
            documento: Guid.NewGuid(), numero: "NC01-9", fecha: VentasACreditoDePrueba.Hoy.AddDays(-40));
        var r = await _v.ConsumirAsync(id);

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await _v.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.CommercialAdjustment)).Should().Be(0, "ya está en el saldo neto (FR-143)");
        (await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Should().BeEmpty();
    }

    private LendingInboundMessage Recibo(VentasACreditoDePrueba.Venta v, InboundMessageStatus estado, int? conciliacion = null)
    {
        var r = new LendingInboundMessage
        {
            MessagePublicId = Guid.NewGuid(), MessageType = VentaACreditoRegistradaV1.Type, MessageVersion = 2, OriginModule = "INV", OriginPublicId = v.Documento,
            OriginEventKey = ClavesDeEvento.ConfirmacionPor(v.Pago), ChainRootPublicId = v.Documento, OperationDate = v.Fecha, Status = estado,
            CreditPaymentPublicId = v.Pago, SaleReconciliationId = conciliacion, FirstReceivedAt = _v.G.UtcNow, LastProcessedAt = _v.G.UtcNow,
        };
        _v.Db.LendingInboundMessages.Add(r);
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    private LendingActivation Activacion(DateOnly corte)
    {
        var a = new LendingActivation
        {
            MigrationLoadId = 1, CutoffDate = corte, ActivationDate = corte.AddDays(1), RegisteredByUserId = _v.Cajera.Id, RegisteredAt = _v.G.UtcNow.AddDays(-29),
            SessionItemId = 1, CheckVersion = 1, BalanceCheckSha256 = "x", SalesReconciliationSha256 = "y",
        };
        _v.Db.LendingActivations.Add(a);
        _v.Db.SaveChanges();
        var setup = _v.Db.LendingSetups.Single();
        setup.ActivationId = a.Id;
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
        return a;
    }

    private void ConCobroProvisional(string factura, decimal monto)
    {
        var a = new LendingActivation
        {
            MigrationLoadId = 1, CutoffDate = VentasACreditoDePrueba.Hoy.AddDays(-40), ActivationDate = VentasACreditoDePrueba.Hoy.AddDays(-39),
            RegisteredByUserId = _v.Cajera.Id, RegisteredAt = _v.G.UtcNow.AddDays(-39), SessionItemId = 1, BalanceCheckSha256 = "x", SalesReconciliationSha256 = "y",
        };
        _v.Db.LendingActivations.Add(a);
        _v.Db.SaveChanges();
        _v.Db.ProvisionalCollections.Add(new ProvisionalCollection
        {
            ActivationId = a.Id, RowNumber = 1, InvoiceNumber = factura, Amount = monto, ValueDate = VentasACreditoDePrueba.Hoy, AccountingReference = "CI-1",
        });
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
    }
}
