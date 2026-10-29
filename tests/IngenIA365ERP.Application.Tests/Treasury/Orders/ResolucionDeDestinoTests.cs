using FluentAssertions;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// Feature 015, D-a (decisión del dueño del 2026-10-27; FR-229): la orden de transferencia que llegó sin cuenta destino (el giro de Captaciones a un asociado sin cuenta
/// de depósito) la resuelve Tesorería: completa la cuenta con una registrada y activa del beneficiario —nunca con una de otra persona ni con una por aprobar— o la
/// pasa a cheque; las dos la vuelven a validar y la dejan lista. Lo que no le falta la cuenta, o ya está en un lote, no se toca.
/// </summary>
public class ResolucionDeDestinoTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private CompletePaymentOrderDestinationCommandHandler Completar() => new(_t.E.Db, _t.E.Actor, _t.Cerrojo, _t.Validador, Auditoria);

    private PayPaymentOrderByCheckCommandHandler PorCheque() => new(_t.E.Db, _t.E.Actor, _t.Cerrojo, _t.Validador, _t.Parametros, Auditoria);

    private IReadOnlyList<string> Eventos() => _t.E.Auditoria.ReceivedCalls()
        .Select(c => c.GetArguments().OfType<Application.Common.Interfaces.AuditLogCommand>().FirstOrDefault()?.Action).OfType<string>().ToList();

    private PaymentOrder SinDestino(PaymentOrderState estado = PaymentOrderState.Pending)
    {
        var orden = _t.Orden(estado: estado, origen: PaymentOrderOrigin.DepositsPayout, clase: "DepositWithdrawal");
        orden.DestinationBankCode = null;
        orden.DestinationBankId = null;
        orden.DestinationAccountNumber = null;
        orden.DestinationAccountType = null;
        orden.PendingReasonsJson = $"[\"{ErroresDeTesoreria.CodigoSinCuentaDestino}\"]";
        _t.E.Db.SaveChanges();
        return orden;
    }

    [Fact]
    public async Task Completa_la_cuenta_con_una_registrada_del_beneficiario_y_queda_lista()
    {
        var orden = SinDestino();
        var r = await Completar().Handle(new CompletePaymentOrderDestinationCommand(orden.PublicId, _t.CuentaDeLaBeneficiaria.PublicId, "Soporte del asociado")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(PaymentOrderState.Ready);
        r.Value.PendingReasons.Should().BeEmpty();
        orden.DestinationBankCode.Should().Be("1007");
        orden.DestinationAccountNumber.Should().Be("123456789");
        orden.PersonBankAccountId.Should().Be(_t.CuentaDeLaBeneficiaria.Id);
        orden.Form.Should().Be(PaymentForm.Transfer);
        Eventos().Should().Contain(ResolucionDeDestinoDeOrdenes.EventoCuentaCompletada);
    }

    [Fact]
    public async Task No_completa_con_una_cuenta_de_otra_persona_ni_con_una_por_aprobar()
    {
        var orden = SinDestino();
        var otra = new Person { FirstName = "Otra", LastName = "Persona", TaxId = "3001", IdType = "C" };
        _t.E.Db.People.Add(otra);
        _t.E.Db.SaveChanges();
        var ajena = new PersonBankAccount
        {
            PersonId = otra.Id, BankId = _t.Banco.Id, AccountType = "Savings", AccountNumber = "555", HolderName = "Otra", HolderTaxId = "3001",
            Status = PersonBankAccountState.Active, RequestedByName = "test",
        };
        var porAprobar = new PersonBankAccount
        {
            PersonId = _t.Beneficiaria.Id, BankId = _t.Banco.Id, AccountType = "Savings", AccountNumber = "777", HolderName = "Berta", HolderTaxId = "2001",
            Status = PersonBankAccountState.PendingApproval, RequestedByName = "test",
        };
        _t.E.Db.PersonBankAccounts.AddRange(ajena, porAprobar);
        _t.E.Db.SaveChanges();

        foreach (var cuenta in new[] { ajena, porAprobar })
            (await Completar().Handle(new CompletePaymentOrderDestinationCommand(orden.PublicId, cuenta.PublicId, "Motivo") { OperationKey = Guid.NewGuid() }, default))
                .Error.Code.Should().Be("Treasury.Order.DestinationAccountInvalid");
        orden.DestinationAccountNumber.Should().BeNull();
        orden.Status.Should().Be(PaymentOrderState.Pending);
    }

    [Fact]
    public async Task La_pasa_a_cheque_y_queda_lista_para_un_lote_de_cheques()
    {
        var orden = SinDestino();
        var r = await PorCheque().Handle(new PayPaymentOrderByCheckCommand(orden.PublicId, "El asociado no tiene cuenta") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Form.Should().Be(PaymentForm.Check);
        r.Value.Status.Should().Be(PaymentOrderState.Ready);
        orden.DestinationAccountNumber.Should().BeNull();
        orden.PersonBankAccountId.Should().BeNull();
        Eventos().Should().Contain(ResolucionDeDestinoDeOrdenes.EventoPasadaACheque);
    }

    [Fact]
    public async Task Lo_que_no_le_falta_la_cuenta_o_ya_esta_en_un_lote_no_se_toca()
    {
        var conCuenta = _t.Orden(estado: PaymentOrderState.Pending);
        conCuenta.PendingReasonsJson = $"[\"{ErroresDeTesoreria.CodigoDestinoNoRegistrado}\"]";
        _t.E.Db.SaveChanges();
        (await PorCheque().Handle(new PayPaymentOrderByCheckCommand(conCuenta.PublicId, "Motivo") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Treasury.Order.DestinationNotMissing");

        var enLote = SinDestino(PaymentOrderState.InBatch);
        (await Completar().Handle(new CompletePaymentOrderDestinationCommand(enLote.PublicId, _t.CuentaDeLaBeneficiaria.PublicId, "Motivo")
            {
                OperationKey = Guid.NewGuid(),
            }, default))
            .Error.Code.Should().Be("Treasury.Order.DestinationNotResolvable");
        enLote.Form.Should().Be(PaymentForm.Transfer);
    }

    [Fact]
    public void El_motivo_y_la_cuenta_son_obligatorios()
    {
        new CompletePaymentOrderDestinationCommandValidator().Validate(new CompletePaymentOrderDestinationCommand(Guid.NewGuid(), Guid.Empty, "x")).IsValid.Should().BeFalse();
        new CompletePaymentOrderDestinationCommandValidator().Validate(new CompletePaymentOrderDestinationCommand(Guid.NewGuid(), Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
        new PayPaymentOrderByCheckCommandValidator().Validate(new PayPaymentOrderByCheckCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
    }
}
