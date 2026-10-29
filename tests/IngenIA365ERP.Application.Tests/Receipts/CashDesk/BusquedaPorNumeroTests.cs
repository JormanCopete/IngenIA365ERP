using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, decisión del dueño del 2026-10-24 sobre T0113: la caja llega a la persona y a lo que puede pagar por el número del crédito o de la cuenta, que
/// Cartera y Captaciones resuelven por su puerto de lectura (<see cref="IBusquedaDeObligaciones"/>); Recaudo no lee <c>LND_</c>. Sin coincidencia, persona no
/// encontrada; con dos personas, número ambiguo. Prueba escrita, sin correr.
/// </summary>
public class BusquedaPorNumeroTests
{
    private readonly CajaConContratos _k = new();

    [Fact]
    public async Task El_numero_del_credito_lleva_a_su_deudor_y_a_lo_que_puede_pagar()
    {
        _k.Busqueda.PorNumeroAsync(Arg.Is<BusquedaPorNumero>(b => b.Number == "CR-000123"), Arg.Any<CancellationToken>())
            .Returns([new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, _k.CreditoA, "CR-000123", _k.Asociado.PublicId, "Ana Asociada", "2001", "Active")]);

        var r = await _k.Pagables().Handle(new GetPayablesQuery(ObligationNumber: "CR-000123"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.PersonPublicId.Should().Be(_k.Asociado.PublicId);
    }

    [Fact]
    public async Task Sin_coincidencia_no_hay_persona_y_con_dos_personas_es_ambiguo()
    {
        _k.Busqueda.PorNumeroAsync(Arg.Any<BusquedaPorNumero>(), Arg.Any<CancellationToken>()).Returns([]);
        (await _k.Pagables().Handle(new GetPayablesQuery(ObligationNumber: "NO-EXISTE"), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoPersonaNoEncontrada);

        _k.Busqueda.PorNumeroAsync(Arg.Any<BusquedaPorNumero>(), Arg.Any<CancellationToken>()).Returns(
        [
            new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, Guid.NewGuid(), "77", Guid.NewGuid(), "Uno", "1", "Active"),
            new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Account, Guid.NewGuid(), "77", Guid.NewGuid(), "Otra", "2", "Active"),
        ]);
        (await _k.Pagables().Handle(new GetPayablesQuery(ObligationNumber: "77"), default)).Error.Code.Should().Be("Receipts.Payables.AmbiguousNumber");
    }
}
