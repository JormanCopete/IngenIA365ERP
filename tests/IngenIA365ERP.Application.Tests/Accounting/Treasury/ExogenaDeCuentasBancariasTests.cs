using FluentAssertions;
using IngenIA365ERP.Application.Tests.Treasury;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Treasury;

/// <summary>
/// Feature 015, T2, T0450 (FR-222): para la exógena (formato 1012 y los de pagos), cada cuenta bancaria propia lleva su banco como tercero y Contabilidad entrega su saldo
/// en libros al 31 de diciembre por el vínculo de la cuenta; una cuenta sin su banco como tercero se marca para completarla. Prueba escrita, sin correr. (nuevo)
/// </summary>
public class ExogenaDeCuentasBancariasTests
{
    private readonly EscenarioDeTesoreria _t = new();

    [Fact]
    public async Task Cada_cuenta_con_su_banco_como_tercero_y_el_saldo_al_31_de_diciembre()
    {
        _t.Contabilidad.SaldoEnLibrosAsync(Arg.Any<Guid>(), new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>()).Returns(2_500_000m);
        var h = new ListBankAccountsForExogenaQueryHandler(_t.E.Db, _t.Alcance, _t.Contabilidad, new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance));

        var r = await h.Handle(new ListBankAccountsForExogenaQuery(2026), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().OnlyContain(c => c.YearEndBalance == 2_500_000m);
        r.Value.Should().OnlyContain(c => c.MissingThirdParty == (c.BankThirdPartyPersonPublicId == null));
        await _t.Contabilidad.Received().SaldoEnLibrosAsync(_t.Pagos.PublicId, new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>());
    }
}
