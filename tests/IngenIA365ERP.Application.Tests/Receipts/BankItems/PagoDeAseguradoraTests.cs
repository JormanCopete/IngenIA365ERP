using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, R2, T0431 (FR-200): el pago de una aseguradora (la reclamación del seguro de un crédito) entra por el canal banco con la aseguradora como pagadora —un
/// tercero distinto del deudor— y se aplica por el contrato de obligaciones; la aseguradora no puede ser el mismo deudor. Prueba escrita, sin correr. (nuevo)
/// </summary>
public class PagoDeAseguradoraTests
{
    private readonly BancosDePrueba _b = new();

    private Person Aseguradora()
    {
        var p = new Person { FirstName = "Aseguradora", LastName = "de pruebas", TaxId = "860000001" };
        _b.K.C.E.Db.People.Add(p);
        _b.K.C.E.Db.SaveChanges();
        return p;
    }

    [Fact]
    public async Task La_aseguradora_paga_el_credito_del_deudor_por_el_contrato()
    {
        var aseguradora = Aseguradora();
        var abono = _b.Abono(500_000m, referencia: "SINIESTRO-77");

        var r = await _b.Identificar().Handle(new IdentifyBankItemCommand(abono.PublicId, [_b.CuotaA(500_000m)], aseguradora.PublicId, InsurerClaim: true)
            { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.Channel.Should().Be(ReceiptChannel.Bank);
        recibo.PayerPersonId.Should().Be(aseguradora.Id);
        recibo.PayerIsThirdParty.Should().BeTrue();
        await _b.K.Obligaciones.Received().AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Single().PayerPublicId == aseguradora.PublicId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void La_aseguradora_no_es_el_deudor()
    {
        var v = new IdentifyBankItemCommandValidator();

        var mismo = v.Validate(new IdentifyBankItemCommand(Guid.NewGuid(), [_b.CuotaA(1m)], _b.K.Asociado.PublicId, InsurerClaim: true));
        var sinAseguradora = v.Validate(new IdentifyBankItemCommand(Guid.NewGuid(), [_b.CuotaA(1m)], InsurerClaim: true));

        mismo.IsValid.Should().BeFalse();
        sinAseguradora.IsValid.Should().BeFalse();
    }
}
