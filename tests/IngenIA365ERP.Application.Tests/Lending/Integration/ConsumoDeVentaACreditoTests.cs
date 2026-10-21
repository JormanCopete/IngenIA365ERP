using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Application.Common.Alerts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0569 (T17; data-model §21.1; mensajes.md §15.3): <see cref="ConsumeCommercialCreditMessageCommand"/> con una
/// <c>VentaACreditoRegistrada</c> v1 o v2 —la tabla de desenlaces—: la obligación nace siempre con desembolso en especie, la línea por FR-050, la fecha de
/// la venta y las condiciones traducidas; el número reservado o uno nuevo; sin pendiente no se reevalúa y con pendiente se evalúa a la fecha de la venta
/// (negativa: validación fallida y bloqueo, sin anular la venta); lo no convertible queda reprocesable sin respuesta (C95); el recibo repetido es
/// <c>AlreadyProcessed</c>; la preferencia heredada del cupo rotativo y los cobros provisionales de la factura una sola vez (C62); y en el mismo
/// guardado <c>CreditoDesembolsado</c> en especie y <c>ObligacionCreada</c> con la venta como raíz de cadena y el usuario de Comercial (T11).
/// </summary>
public sealed class ConsumoDeVentaACreditoTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Contenido(Domain.Entities.Integration.Transactions.IntegrationMessage m) => JsonDocument.Parse(m.PayloadJson).RootElement;

    // ------------------------------------------------------------------------------------------------------- nacimiento --

    [Fact]
    public async Task Una_v2_validada_crea_la_obligacion_en_especie_con_la_fecha_y_la_factura_de_la_venta()
    {
        var venta = _v.NuevaVenta(600_000m);

        var (mensaje, r) = await _v.VenderAsync(venta);

        var credito = await _v.CreditoDeAsync(venta);
        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(credito.PublicId, null, credito.Number));
        (credito.Origin, credito.Status, credito.OpenedOn, credito.ApprovedAmount).Should()
            .Be((LoanOrigin.Commercial, LoanStatus.Active, venta.Fecha, 600_000m));
        (credito.SourceSalesDocumentPublicId, credito.SourceInvoiceNumber, credito.SourceMessagePublicId, credito.AccountsReceivableRecordedBy)
            .Should().Be(((Guid?)venta.Documento, venta.Numero, (Guid?)mensaje, "Cartera"));
        credito.SaleObligationStatus.Should().Be(SaleObligationStatus.Validated);
        var desembolso = await _v.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.LoanId == credito.Id);
        (desembolso.Kind, desembolso.DisbursementForm, desembolso.Amount, desembolso.ValueDate, desembolso.ActorKind).Should()
            .Be((LoanOperationKind.Disbursement, (DisbursementForm?)DisbursementForm.InKind, 600_000m, venta.Fecha, ActorKind.Process));
        desembolso.OriginUserCentralId.Should().Be(_v.Cajera.CentralUserId, "el usuario de origen es el de Comercial (T11)");
        (await _v.Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == credito.Id)).Cause.Should().Be(TermsVersionCause.CommercialSale);
        var plan = await _v.Db.RepaymentScheduleVersions.AsNoTracking().SingleAsync(p => p.LoanId == credito.Id);
        plan.InstallmentCount.Should().Be(3, "las condiciones se traducen por las equivalencias (MONTHLY, MONTHS)");
        (await _v.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == credito.Id).SumAsync(t => t.Amount)).Should().Be(600_000m);
    }

    [Fact]
    public async Task En_el_mismo_guardado_salen_el_desembolso_en_especie_y_ObligacionCreada_con_la_venta_como_raiz()
    {
        var venta = _v.NuevaVenta();

        var (mensaje, _) = await _v.VenderAsync(venta);

        var credito = await _v.CreditoDeAsync(venta);
        var desembolsado = (await _v.MensajesAsync(CreditoDesembolsadoV1.Type)).Single();
        var inKind = Contenido(desembolsado).GetProperty("inKind");
        (inKind.GetProperty("saleMessageId").GetGuid(), inKind.GetProperty("creditPaymentPublicId").GetGuid()).Should().Be((mensaje, venta.Pago));
        Contenido(desembolsado).GetProperty("disbursementForm").GetString().Should().Be("InKind");
        Contenido(desembolsado).GetProperty("netToDisburse").GetDecimal().Should().Be(0m);

        var creada = (await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single();
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync(x => x.MessagePublicId == mensaje);
        (creada.OriginPublicId, creada.OriginEventKey, creada.ChainRootPublicId).Should().Be((recibo.PublicId, ClavesDeEvento.Confirmacion, venta.Documento));
        (creada.OriginActorKind, creada.OriginUserCentralId).Should().Be((ActorKind.Person, _v.Cajera.CentralUserId),
            "lo emite el proceso a nombre del usuario de Comercial que confirmó la venta (T11)");
        (await _v.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == creada.Id)).Destination.Should().Be(IntegrationDestinations.Inventory);
        var c = Contenido(creada);
        (c.GetProperty("loan").GetProperty("loanNumber").GetString(), c.GetProperty("status").GetString(), c.GetProperty("numberSource").GetString())
            .Should().Be((credito.Number, "Validated", "Assigned"));
        c.GetProperty("plan").GetProperty("installmentCount").GetInt32().Should().Be(3);
        (recibo.Status, recibo.LoanId, recibo.ResponseMessagePublicId).Should().Be((InboundMessageStatus.Processed, (int?)credito.Id, (Guid?)creada.PublicId));
        var evaluacion = await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync(e => e.InboundMessageId == recibo.Id);
        (evaluacion.Outcome, evaluacion.CreditLineCode, evaluacion.PrevalidationOutcome).Should().Be((InboundEvaluationOutcome.Accepted, "CONLIB", "Postable"), "US7: la evaluación la responde el adaptador real (aquí, un doble que dice contabilizable)");
    }

    [Fact]
    public async Task La_v1_tambien_crea_la_obligacion()
    {
        var venta = _v.NuevaVenta();
        var v2 = VentasACreditoDePrueba.ContenidoV2(venta);
        var v1 = new VentaACreditoRegistradaV1
        {
            ThirdPartyKind = v2.ThirdPartyKind, Person = v2.Person, CreditPayment = v2.CreditPayment, DocumentTotal = v2.DocumentTotal, AmountDue = v2.AmountDue,
            Terms = v2.Terms, CreditLineCode = null, SuggestedCreditLineCode = null, PendingValidation = false, Origin = v2.Origin,
            AccountsReceivableRecordedBy = "Contabilidad",
        };

        var r = await _v.ConsumirAsync(await _v.EmitirAsync(venta, v1));

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var credito = await _v.CreditoDeAsync(venta);
        credito.AccountsReceivableRecordedBy.Should().Be("Contabilidad", "el sello de la venta nunca cambia (FR-056)");
        (await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync()).CreditLineCode.Should().Be("CONLIB",
            "sin línea en el mensaje rige la de defecto de origen comercial del tipo de comprador (FR-050)");
    }

    // --------------------------------------------------------------------------------------------------------- número --

    [Fact]
    public async Task Con_la_reserva_de_la_v2_toma_el_numero_reservado_y_la_usa()
    {
        var venta = _v.NuevaVenta();
        var reserva = Reserva(venta, "CR-000777", LoanNumberReservationStatus.Reserved);

        await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, numeroReservado: reserva.Number, reserva: reserva.PublicId));

        var credito = await _v.CreditoDeAsync(venta);
        credito.Number.Should().Be("CR-000777");
        var usada = await _v.Db.LoanNumberReservations.AsNoTracking().SingleAsync();
        (usada.Status, usada.LoanId).Should().Be((LoanNumberReservationStatus.Used, (int?)credito.Id));
        Contenido((await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single()).GetProperty("numberSource").GetString().Should().Be("Reservation");
    }

    [Fact]
    public async Task Una_reserva_vencida_tambien_se_toma_y_alerta()
    {
        var venta = _v.NuevaVenta();
        var reserva = Reserva(venta, "CR-000778", LoanNumberReservationStatus.Expired);

        await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, numeroReservado: reserva.Number, reserva: reserva.PublicId));

        (await _v.CreditoDeAsync(venta)).Number.Should().Be("CR-000778", "el número nunca se reasigna: la venta que llega lo toma (C21)");
        await _v.D.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.ReservaVencida),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_reserva_que_no_existe_no_rechaza_la_venta_y_se_anota()
    {
        var venta = _v.NuevaVenta();

        var (mensaje, r) = await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, numeroReservado: "CR-999999", reserva: Guid.NewGuid()));

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var credito = await _v.CreditoDeAsync(venta);
        credito.Number.Should().NotBe("CR-999999");
        Contenido((await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single()).GetProperty("numberSource").GetString().Should().Be("Assigned");
        var evaluacion = await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync();
        evaluacion.ChecksJson.Should().Contain(ErroresDeVentasACredito.CodigoReservaInexistente, "la reserva que no se encontró queda anotada (C20)");
        evaluacion.Outcome.Should().Be(InboundEvaluationOutcome.Accepted);
        _ = mensaje;
    }

    private LoanNumberReservation Reserva(VentasACreditoDePrueba.Venta v, string numero, LoanNumberReservationStatus estado)
    {
        var r = new LoanNumberReservation
        {
            Number = numero, PersonId = _v.Ana.Id, Amount = v.Monto, IdempotencyKey = $"INV:{v.Pago:N}:{v.Monto:0.00}", RequestSha256 = new string('a', 64),
            SalesDocumentPublicId = v.Documento, PaymentPublicId = v.Pago, Status = estado, ReservedAt = _v.G.UtcNow.AddDays(-2),
            ReservedByUserId = _v.Cajera.Id, ExpiresOn = VentasACreditoDePrueba.Hoy.AddDays(estado == LoanNumberReservationStatus.Expired ? -1 : 3),
        };
        _v.Db.LoanNumberReservations.Add(r);
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    // ------------------------------------------------------------------------------------------------------- validación --

    [Fact]
    public async Task Sin_pendiente_no_se_reevalua()
    {
        _v.O.ListasDe[_v.Ana.Id] = ScreeningOutcome.Match;
        var venta = _v.NuevaVenta();

        var (_, r) = await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, pendiente: false));

        r.Should().BeOfType<ResultadoDeConsumo.Processed>("la venta ya validada en línea no se reevalúa (FR-051)");
        (await _v.Db.ScreeningResults.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Pendiente_y_evaluada_en_contra_crea_la_obligacion_marcada_bloquea_y_responde_ValidationFailed()
    {
        _v.O.ListasDe[_v.Ana.Id] = ScreeningOutcome.Match;
        var venta = _v.NuevaVenta();

        var (_, r) = await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, pendiente: true));

        var credito = await _v.CreditoDeAsync(venta);
        var fallida = r.Should().BeOfType<ResultadoDeConsumo.ValidationFailed>().Subject;
        (fallida.LoanPublicId, fallida.LoanNumber, fallida.Code).Should().Be((credito.PublicId, credito.Number, "Compliance.RestrictiveList.Match"));
        (credito.Status, credito.SaleObligationStatus, credito.ValidationFailureCode).Should()
            .Be((LoanStatus.Active, (SaleObligationStatus?)SaleObligationStatus.ValidationFailed, "Compliance.RestrictiveList.Match"));
        (await _v.Db.DebtorBlocks.AsNoTracking().Where(b => b.PersonId == _v.Ana.Id).Select(b => b.Reason).ToListAsync()).Should()
            .Contain(DebtorBlockReason.ValidationFailed, "la validación fallida bloquea crédito nuevo al deudor (FR-052)");
        (await _v.Db.ScreeningResults.AsNoTracking().SingleAsync()).Context.Should().Be(ScreeningContext.Sale, "la evaluación guarda la consulta de listas");
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        recibo.Status.Should().Be(InboundMessageStatus.ValidationFailed);
        var c = Contenido((await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single());
        (c.GetProperty("status").GetString(), c.GetProperty("validation").GetProperty("code").GetString())
            .Should().Be(("ValidationFailed", "Compliance.RestrictiveList.Match"));
    }

    [Fact]
    public async Task Con_pendiente_y_todo_en_regla_queda_validada()
    {
        _v.Rotativo(_v.Ana, 5_000_000m);
        var venta = _v.NuevaVenta(800_000m);

        var (_, r) = await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, pendiente: true));

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var evaluacion = await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync();
        foreach (var check in new[] { "DebtorStatus", "CreditLimit", "Concentration", "Usury", "CommercialLine", "RestrictiveLists", "Authorization" })
            evaluacion.ChecksJson.Should().Contain(check);
    }

    [Fact]
    public async Task Pendiente_se_evalua_contando_las_anteriores_y_sin_contar_la_venta()
    {
        _v.Rotativo(_v.Ana, 1_000_000m);
        var primera = _v.NuevaVenta(700_000m);
        var segunda = _v.NuevaVenta(700_000m);

        var (_, r1) = await _v.VenderAsync(primera, VentasACreditoDePrueba.ContenidoV2(primera, pendiente: true));
        var (_, r2) = await _v.VenderAsync(segunda, VentasACreditoDePrueba.ContenidoV2(segunda, pendiente: true));

        r1.Should().BeOfType<ResultadoDeConsumo.Processed>();
        r2.Should().BeOfType<ResultadoDeConsumo.ValidationFailed>().Which.Code.Should().Be("Lending.CreditLimit.Insufficient",
            "la primera ya consume 700.000 del cupo de 1.000.000 (en orden de llegada)");
    }

    // ------------------------------------------------------------------------------------------------- no convertible --

    [Fact]
    public async Task Una_persona_inexistente_deja_el_recibo_rechazado_y_reprocesable_sin_respuesta()
    {
        var venta = _v.NuevaVenta();
        var fantasma = new Domain.Entities.Core.Person { FirstName = "Sin", LastName = "Maestro", TaxId = "1", IdType = "C", CreatedBy = "test" };

        var r = await _v.ConsumirAsync(await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta), comprador: fantasma));

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeVentasACredito.CodigoPersonaInexistente);
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        (recibo.Status, recibo.NotRetryableAt, recibo.ResponseMessagePublicId).Should().Be((InboundMessageStatus.Rejected, (DateTime?)null, (Guid?)null));
        recibo.PersonId.Should().BeNull("el comprador no existe en la cooperativa");
        var evaluacion = await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync();
        (evaluacion.Outcome, evaluacion.Retryable).Should().Be((InboundEvaluationOutcome.NotConvertible, true));
        (await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Should().BeEmpty("no convertible no responde mientras sea reprocesable (C95)");
        (await _v.Db.Loans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_periodicidad_sin_equivalencia_es_no_convertible_y_un_reproceso_suma_otra_evaluacion()
    {
        var venta = _v.NuevaVenta();
        var id = await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, periodicidad: "Biweekly"));

        (await _v.ConsumirAsync(id)).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeVentasACredito.CodigoSinEquivalencia);
        (await _v.ConsumirAsync(id)).Should().BeOfType<ResultadoDeConsumo.Rejected>();

        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        recibo.EvaluationCount.Should().Be(2);
        recibo.PersonId.Should().Be(_v.Ana.Id, "el recibo no convertible lleva al comprador: niega su paz y salvo general mientras siga en reproceso (US13-4)");
        (await _v.Db.LendingInboundMessageEvaluations.CountAsync()).Should().Be(2);
        (await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Should().BeEmpty("un reproceso que vuelve a fallar sigue sin respuesta (C102)");
    }

    [Fact]
    public async Task Una_linea_invalida_sin_alterna_es_no_convertible()
    {
        var venta = _v.NuevaVenta(numero: "PV01-LIN");
        // Sin línea por defecto: la línea de la venta no existe y la de defecto no admite al tipo de comprador.
        var r = await _v.ConsumirAsync(await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, linea: "NOEXISTE") with
        {
            ThirdPartyKind = ConversionDeVentaACredito.Cliente,
        }));

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeVentasACredito.CodigoLineaInvalida);
    }

    // ---------------------------------------------------------------------------------------------------- repetido --

    [Fact]
    public async Task El_mismo_mensaje_dos_veces_no_crea_otro_credito()
    {
        var venta = _v.NuevaVenta();
        var (mensaje, primera) = await _v.VenderAsync(venta);

        var segunda = await _v.ConsumirAsync(mensaje);

        var credito = await _v.CreditoDeAsync(venta);
        segunda.Should().BeEquivalentTo(new ResultadoDeConsumo.AlreadyProcessed(credito.PublicId, null, credito.Number));
        primera.Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await _v.Db.Loans.CountAsync()).Should().Be(1);
        (await _v.MensajesAsync(ObligacionCreadaV1.Type)).Should().ContainSingle("lo ya procesado no emite otra vez (SC-010)");
    }

    // ------------------------------------------------------------------------------------------- preferencia y cobros --

    [Fact]
    public async Task Hereda_la_preferencia_de_abono_del_cupo_rotativo()
    {
        _v.Rotativo(_v.Ana, 5_000_000m, PrepaymentChoice.ReduceInstallment);
        var venta = _v.NuevaVenta();

        await _v.VenderAsync(venta);

        var credito = await _v.CreditoDeAsync(venta);
        credito.CreditLimitId.Should().NotBeNull("la venta consume el cupo rotativo");
        var heredada = await _v.Db.PrepaymentPreferences.AsNoTracking().SingleAsync(p => p.LoanId == credito.Id);
        (heredada.Choice, heredada.Origin).Should().Be(((PrepaymentChoice?)PrepaymentChoice.ReduceInstallment, PreferenceOrigin.InheritedFromLimit));
    }

    [Fact]
    public async Task Aplica_una_sola_vez_los_cobros_provisionales_de_la_factura()
    {
        var venta = _v.NuevaVenta(600_000m, numero: "PV01-COBRO");
        var activacion = new LendingActivation
        {
            MigrationLoadId = 1, CutoffDate = VentasACreditoDePrueba.Hoy.AddDays(-40), ActivationDate = VentasACreditoDePrueba.Hoy.AddDays(-30),
            RegisteredByUserId = _v.Cajera.Id, RegisteredAt = _v.G.UtcNow.AddDays(-30), SessionItemId = 1, BalanceCheckSha256 = "x", SalesReconciliationSha256 = "y",
        };
        _v.Db.LendingActivations.Add(activacion);
        _v.Db.SaveChanges();
        _v.Db.ProvisionalCollections.Add(new ProvisionalCollection
        {
            ActivationId = activacion.Id, RowNumber = 2, InvoiceNumber = "PV01-COBRO", Amount = 100_000m, ValueDate = VentasACreditoDePrueba.Hoy,
            AccountingReference = "CI-55",
        });
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();

        var (mensaje, _) = await _v.VenderAsync(venta);
        await _v.ConsumirAsync(mensaje);

        var credito = await _v.CreditoDeAsync(venta);
        var cobro = await _v.Db.ProvisionalCollections.AsNoTracking().SingleAsync();
        (cobro.LoanId, cobro.OperationId is not null, cobro.AppliedAt is not null, cobro.MessagePublicId is not null).Should()
            .Be(((int?)credito.Id, true, true, true));
        var operacion = await _v.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.ProvisionalCollection);
        (operacion.Amount, operacion.ValueDate, operacion.FinalityDate, operacion.CollectorCode).Should()
            .Be((100_000m, VentasACreditoDePrueba.Hoy, (DateOnly?)VentasACreditoDePrueba.Hoy, (string?)null));
        var incorporado = (await _v.MensajesAsync(CobroProvisionalIncorporadoV1.Type)).Should().ContainSingle("una sola vez (C62)").Subject;
        Contenido(incorporado).GetProperty("accountingReference").GetString().Should().Be("CI-55");
        // El día de la venta no hay nada exigible: el cobro queda como anticipo de las cuotas que vienen (FR-074).
        (await _v.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id && t.Kind == LoanTransactionKind.AdvanceReceived)
            .SumAsync(t => t.Amount)).Should().Be(100_000m);
    }

    // ---------------------------------------------------------------------------------------------------- versión --

    [Fact]
    public async Task Una_version_desconocida_queda_no_convertible_y_reprocesable()
    {
        var venta = _v.NuevaVenta();
        var id = await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        // El mensaje dice ser de una versión que Cartera no conoce (C16): el consumidor la responde, no el despachador.
        var guardado = await _v.Db.IntegrationMessages.SingleAsync(m => m.PublicId == id);
        _v.Db.Entry(guardado).Property(nameof(guardado.Version)).CurrentValue = (short)9;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        var r = await _v.ConsumirAsync(id);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeVentasACredito.CodigoVersionNoAceptada);
        (await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync()).Retryable.Should().BeTrue();
    }
}
