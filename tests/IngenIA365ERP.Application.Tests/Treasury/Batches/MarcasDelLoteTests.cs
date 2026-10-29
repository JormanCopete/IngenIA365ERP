using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Batches;

/// <summary>
/// Feature 015, US26, T0164 (FR-210; SC-026): la orden a una cuenta cambiada dentro de <c>CuentasDestino.DiasDeMarca</c> lleva <c>RecentAccountChange</c> y el lote no
/// se aprueba sin su revisión expresa por otra persona (<c>Treasury.Batch.ReviewRequired</c>, <c>.SelfReview</c>); la cuenta compartida con otro beneficiario o con un
/// usuario interno alerta y se marca; la importada sin certificación se marca (Q4). Prueba escrita, sin correr.
/// </summary>
public class MarcasDelLoteTests
{
    private readonly EscenarioDeTesoreria _t = new();

    public MarcasDelLoteTests()
    {
        var generico = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos().AplicarA(generico, bankId: null);
        _t.E.Db.BankFileFormats.Add(generico);
        _t.E.Db.SaveChanges();
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.CuentasDestinoDiasDeMarca, "10", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
    }

    private CreatePaymentBatchCommandHandler Crear() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Contabilidad, _t.Cerrojo, _t.Numerador, _t.Validador, _t.Fondo,
        _t.Parametros, _t.E.Reloj);

    private GuardasDeAprobacionDelLote Guardas => new(_t.E.Db, _t.Listas, _t.E.Alertas);

    private ReviewPaymentBatchItemCommandHandler Revisar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.E.Personas,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    private async Task<PaymentBatch> LoteAsync(PaymentOrder orden)
    {
        var r = await Crear().Handle(new CreatePaymentBatchCommand(_t.Pagos.PublicId, PaymentBatchForm.File, EscenarioDeRecaudo.Hoy, [orden.PublicId])
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return _t.E.Db.PaymentBatches.Single(b => b.PublicId == r.Value.PublicId);
    }

    /// <summary>La cuenta de la beneficiaria reemplazó a otra hace <paramref name="dias"/> días.</summary>
    private void CuentaCambiadaHace(int dias)
    {
        var anterior = new PersonBankAccount
        {
            PersonId = _t.Beneficiaria.Id, BankId = _t.Banco.Id, AccountType = "Savings", AccountNumber = "555000111", HolderName = "Berta Beneficiaria",
            HolderTaxId = "2001", Status = PersonBankAccountState.Retired, RequestedByName = "test",
        };
        _t.E.Db.PersonBankAccounts.Add(anterior);
        _t.E.Db.SaveChanges();
        _t.CuentaDeLaBeneficiaria.ReplacesId = anterior.Id;
        _t.CuentaDeLaBeneficiaria.DecidedAt = EscenarioDeRecaudo.Hoy.AddDays(-dias).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        _t.E.Db.SaveChanges();
    }

    [Fact]
    public async Task La_cuenta_cambiada_marca_la_orden_y_el_lote_no_se_aprueba_sin_su_revision()
    {
        CuentaCambiadaHace(3);
        var lote = await LoteAsync(_t.Orden());
        var item = _t.E.Db.PaymentBatchItems.Single(i => i.PaymentBatchId == lote.Id);
        item.Flags.Should().HaveFlag(PaymentBatchItemFlags.RecentAccountChange);

        (await Guardas.AntesDeAprobarAsync(lote, default)).Error.Code.Should().Be("Treasury.Batch.ReviewRequired");
        await _t.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaCuentaDestinoCambiada), Arg.Any<CancellationToken>());

        // Quien preparó (la cajera) no revisa; otra persona sí, con su nota.
        (await Revisar().Handle(new ReviewPaymentBatchItemCommand(lote.PublicId, item.PublicId, "Revisé la certificación") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Treasury.Batch.SelfReview");
        _t.E.Como(_t.E.Supervisor);
        var revisado = await Revisar().Handle(new ReviewPaymentBatchItemCommand(lote.PublicId, item.PublicId, "Llamé a la beneficiaria y confirmó la cuenta")
            { OperationKey = Guid.NewGuid() }, default);
        revisado.IsSuccess.Should().BeTrue(revisado.IsFailure ? revisado.Error.Message : null);
        item.ExplicitlyReviewedByUserId.Should().Be(_t.E.Supervisor.Id);
        (await Guardas.AntesDeAprobarAsync(lote, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_cuenta_cambiada_hace_mas_dias_que_la_marca_no_exige_revision()
    {
        CuentaCambiadaHace(30);
        var lote = await LoteAsync(_t.Orden());
        _t.E.Db.PaymentBatchItems.Single(i => i.PaymentBatchId == lote.Id).Flags.Should().NotHaveFlag(PaymentBatchItemFlags.RecentAccountChange);
        (await Guardas.AntesDeAprobarAsync(lote, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_cuenta_compartida_con_otro_beneficiario_alerta_y_se_marca()
    {
        var otra = new Person { FirstName = "Olga", LastName = "Otra", TaxId = "2002", IdType = "C" };
        _t.E.Db.People.Add(otra);
        _t.E.Db.SaveChanges();
        _t.E.Db.PersonBankAccounts.Add(new PersonBankAccount
        {
            PersonId = otra.Id, BankId = _t.Banco.Id, AccountType = "Savings", AccountNumber = "123456789", HolderName = "Olga Otra", HolderTaxId = "2002",
            Status = PersonBankAccountState.PendingApproval, RequestedByName = "test",
        });
        _t.E.Db.SaveChanges();

        var lote = await LoteAsync(_t.Orden());

        _t.E.Db.PaymentBatchItems.Single(i => i.PaymentBatchId == lote.Id).Flags.Should().HaveFlag(PaymentBatchItemFlags.SharedDestination);
        await _t.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.PlataformaCuentaDestinoCompartida), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_cuenta_importada_sin_certificacion_se_marca()
    {
        _t.CuentaDeLaBeneficiaria.Origin = PersonBankAccountOrigin.Imported;
        _t.CuentaDeLaBeneficiaria.CertificateAttachmentPublicId = null;
        _t.E.Db.SaveChanges();

        var lote = await LoteAsync(_t.Orden());

        _t.E.Db.PaymentBatchItems.Single(i => i.PaymentBatchId == lote.Id).Flags.Should().HaveFlag(PaymentBatchItemFlags.ImportedAccountWithoutCertificate);
    }

    [Fact]
    public async Task Una_orden_sin_marcas_no_se_revisa()
    {
        var lote = await LoteAsync(_t.Orden());
        var item = _t.E.Db.PaymentBatchItems.Single(i => i.PaymentBatchId == lote.Id);
        _t.E.Como(_t.E.Supervisor);

        var r = await Revisar().Handle(new ReviewPaymentBatchItemCommand(lote.PublicId, item.PublicId, "nada") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ReviewPaymentBatchItemCommandHandler.CodigoSinMarca);
    }

    [Fact]
    public async Task Un_beneficiario_en_lista_vinculante_alerta_al_aprobar()
    {
        var lote = await LoteAsync(_t.Orden());
        _t.Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Application.Treasury.Orders.ResultadoDeListasDeTesoreria([_t.Beneficiaria.Id], []));

        (await Guardas.AntesDeAprobarAsync(lote, default)).Error.Code.Should().Be("Treasury.Compliance.BindingListMatch");
        await _t.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaListaVinculante), Arg.Any<CancellationToken>());
    }
}
