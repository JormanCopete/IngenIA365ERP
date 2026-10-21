using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Tests.Common;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Feature 014 (decisión del dueño del 2026-10-18, D52; cierra T113): el doble de Tesorería como recaudador de cuentas —<c>TES</c>, clase
/// <c>Treasury</c>— consigna y reversa por el contrato de cuentas con su clase, y la tesorería de prueba recibe los avisos de lo que la Cartera le reversó.
/// </summary>
public class DobleDeTesoreriaTests
{
    [Fact]
    public async Task Las_rutas_de_Tesoreria_llaman_al_contrato_con_el_recaudador_Treasury()
    {
        var db = TestDbContextFactory.Create();
        var contrato = Substitute.For<IContratoDeCuentas>();
        contrato.ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>()).Returns(new RespuestaDeConsignacionDto([], []));
        IDestinoDeMensajes[] destinos = [new RecaudadorDePrueba(), new TesoreriaDePrueba()];

        var r = await new ApplyTestCollectorAccountDepositsCommandHandler(db, contrato, destinos)
            .Handle(new ApplyTestCollectorAccountDepositsCommand([], [], AsTreasury: true), default);

        r.IsSuccess.Should().BeTrue();
        await contrato.Received(1).ConsignarAsync(Arg.Is<SolicitudDeConsignacion>(s => s.Collector.Code == "TES" && s.Collector.Class == ClaseDeRecaudador.Treasury),
            Arg.Any<CancellationToken>());
        GramaticaDeClaves.Cumple(ClaseDeRecaudador.Treasury, RecaudadorDePrueba.ClaveDeTesoreria(Guid.NewGuid(), Guid.NewGuid())).Should().BeTrue();
        var tesoreria = new TesoreriaDePrueba();
        tesoreria.Acepta(ConsignacionReversadaPorCarteraV1.Type, 1).Should().BeTrue();
        tesoreria.Acepta(PagoReversadoPorCarteraV1.Type, 1).Should().BeTrue();
    }
}
