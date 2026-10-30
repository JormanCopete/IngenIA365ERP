using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0250 y T0262 (FR-221): pasar la política contable de Recaudo o Tesorería de «bloquear» a «advertir» escribe la vigencia de
/// <c>Contabilidad.FaltaDeRegla</c> por el único escritor de parámetros, con motivo, y alerta de una vez; volver a «bloquear» no alerta. Prueba escrita, sin correr.
/// </summary>
public class PoliticaContableTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public PoliticaContableTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 11, 3));
        _sender.Send(Arg.Any<AddParameterVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AddParameterVersionResponse([Guid.NewGuid()], null, null)));
    }

    [Fact]
    public async Task Advertir_en_Recaudo_escribe_la_vigencia_y_alerta()
    {
        var r = await new SetReceiptsAccountingPolicyCommandHandler(_sender, _alertas, _reloj)
            .Handle(new SetReceiptsAccountingPolicyCommand("Advertir", "Matriz incompleta en la puesta en marcha") { OperationKey = Guid.NewGuid() }, default);

        r.Value.Policy.Should().Be("Advertir");
        await _sender.Received(1).Send(Arg.Is<AddParameterVersionCommand>(c => c.Module == ParametrosDeRecaudoYTesoreria.ModuloRecaudo
            && c.Key == ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla && c.Value == "Advertir" && c.Reason.Length > 0), Arg.Any<CancellationToken>());
        await _alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoPoliticaContableEnAdvertir), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bloquear_en_Tesoreria_no_alerta()
    {
        await new SetTreasuryAccountingPolicyCommandHandler(_sender, _alertas, _reloj)
            .Handle(new SetTreasuryAccountingPolicyCommand("Bloquear", "Matriz completa") { OperationKey = Guid.NewGuid() }, default);

        await _sender.Received(1).Send(Arg.Is<AddParameterVersionCommand>(c => c.Module == ParametrosDeRecaudoYTesoreria.ModuloTesoreria), Arg.Any<CancellationToken>());
        await _alertas.DidNotReceive().LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void La_politica_sólo_admite_bloquear_o_advertir()
    {
        new SetReceiptsAccountingPolicyCommandValidator().Validate(new SetReceiptsAccountingPolicyCommand("Ignorar", "x")).IsValid.Should().BeFalse();
        new SetTreasuryAccountingPolicyCommandValidator().Validate(new SetTreasuryAccountingPolicyCommand("Advertir", "x")).IsValid.Should().BeTrue();
    }
}
