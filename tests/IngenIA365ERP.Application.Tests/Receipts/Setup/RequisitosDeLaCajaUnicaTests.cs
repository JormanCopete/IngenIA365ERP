using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using NSubstitute;
using Xunit;
using P = IngenIA365ERP.Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// 2026-11-25 (pedido del dueño: la caja única con «parámetros simples y seguros»): <c>Caja.SesionUnica</c> no se enciende sin una caja de Recaudo unida
/// a una caja del punto de venta ni sin la cuenta puente en la matriz contable de Recaudo (cuando la falta de regla bloquea); apagarla no exige nada.
/// </summary>
public class RequisitosDeLaCajaUnicaTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly IContabilidadParaRecaudo _contabilidad = Substitute.For<IContabilidadParaRecaudo>();

    public RequisitosDeLaCajaUnicaTests() => Contabilidad(postable: true, politica: "Bloquear");

    private void Contabilidad(bool postable, string politica) =>
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>())
            .Returns(new EvaluacionContableDto(postable, politica, []));

    private ReglasDeParametrosDeRecaudo Reglas() => new(ReglasDeParametrosDeRecaudo.InicioPorDefecto, _e.Db, _contabilidad);

    private static AltaDeParametro Alta(string valor) =>
        new(CatalogoDeParametros.Buscar(P.ModuloRecaudo, P.CajaSesionUnica)!, ParameterScopeKind.None, null, null, valor, EscenarioDeRecaudo.Hoy, false);

    private void CajaUnida()
    {
        var caja = _e.Caja(_e.Punto("P1"), "C1");
        caja.PosCashRegisterPublicId = Guid.NewGuid();
        _e.Db.SaveChanges();
    }

    [Fact]
    public async Task Sin_ninguna_caja_unida_al_punto_de_venta_no_se_enciende()
    {
        _e.Caja(_e.Punto("P1"), "C1");

        var r = await Reglas().EvaluarAsync(Alta("true"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoCajaUnicaSinRequisitos);
        r.Error.Message.Should().Contain("Cajas y cajeros");
    }

    [Fact]
    public async Task Sin_la_cuenta_puente_y_con_la_falta_de_regla_bloqueando_no_se_enciende()
    {
        CajaUnida();
        Contabilidad(postable: false, politica: "Bloquear");

        var r = await Reglas().EvaluarAsync(Alta("true"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Message.Should().Contain(RequisitosDeLaCajaUnica.SinCuentaPuente);
    }

    [Fact]
    public async Task Con_la_falta_de_regla_en_advertir_se_enciende()
    {
        CajaUnida();
        Contabilidad(postable: false, politica: "Advertir");

        (await Reglas().EvaluarAsync(Alta("true"), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Con_una_caja_unida_y_la_cuenta_puente_se_enciende_y_apagarla_no_exige_nada()
    {
        CajaUnida();

        (await Reglas().EvaluarAsync(Alta("true"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await _contabilidad.Received(1).EvaluarAsync(
            Arg.Is<IReadOnlyList<CombinacionDeHecho>>(c => c.Single().Dimensions[ConfirmacionDeReciboRol] == "PuenteDelPuntoDeVenta"), Arg.Any<CancellationToken>());

        var vacia = new EscenarioDeRecaudo();
        (await new ReglasDeParametrosDeRecaudo(ReglasDeParametrosDeRecaudo.InicioPorDefecto, vacia.Db, _contabilidad)
            .EvaluarAsync(Alta("false"), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    private const string ConfirmacionDeReciboRol = IngenIA365ERP.Application.Receipts.CashDesk.ConfirmacionDeRecibo.ClaveDelRol;
}
