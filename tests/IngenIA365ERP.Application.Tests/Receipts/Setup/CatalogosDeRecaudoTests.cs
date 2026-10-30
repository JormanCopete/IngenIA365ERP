using FluentAssertions;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// Feature 015, T0067 (US1; FR-026, FR-027, FR-179, FR-184, FR-186, FR-198): un tipo de documento activo por clase con su prefijo de punto opcional; los
/// conceptos del sistema no se inactivan; el concepto gravado exige factura (y con ella tercero y documento cruce); los talonarios de un punto no se cruzan;
/// la regla de modalidad por canal reemplaza las vigentes sin tocar lo que ya rigió; el duplicado de un código se avisa por el buscador común.
/// </summary>
public class CatalogosDeRecaudoTests
{
    private readonly EscenarioDeRecaudo _e = new();

    [Fact]
    public async Task Un_tipo_activo_por_clase()
    {
        var crear = new CreateReceiptDocumentTypeCommandHandler(_e.Db);
        var recibo = await crear.Handle(new CreateReceiptDocumentTypeCommand("rc", "Recibo de caja", ReceiptDocumentKind.CashReceipt, true), CancellationToken.None);
        var otro = await crear.Handle(new CreateReceiptDocumentTypeCommand("RC2", "Otro recibo", ReceiptDocumentKind.CashReceipt, false), CancellationToken.None);

        recibo.Value.UsesPointPrefix.Should().BeTrue();
        otro.Error.Code.Should().Be(ErroresDeRecaudo.CodigoTipoDeClaseOcupada);

        await new UpdateReceiptDocumentTypeCommandHandler(_e.Db).Handle(
            new UpdateReceiptDocumentTypeCommand(recibo.Value.PublicId, "Recibo", false, ReceiptsCatalogStatus.Inactive), CancellationToken.None);
        (await crear.Handle(new CreateReceiptDocumentTypeCommand("RC2", "Otro recibo", ReceiptDocumentKind.CashReceipt, false), CancellationToken.None)).IsSuccess
            .Should().BeTrue("con el anterior inactivo, la clase queda libre");
    }

    [Fact]
    public async Task Los_conceptos_del_sistema_no_se_inactivan_y_el_gravado_exige_factura()
    {
        var sistema = new MiscConcept { Code = "GIRODEV", Name = "Giro devuelto", IsSystem = true };
        _e.Db.MiscConcepts.Add(sistema);
        await _e.Db.SaveChangesAsync();

        (await new InactivateMiscConceptCommandHandler(_e.Db).Handle(new InactivateMiscConceptCommand(sistema.PublicId), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoConceptoDelSistema);

        var gravado = await new CreateMiscConceptCommandHandler(_e.Db).Handle(new CreateMiscConceptCommand("FORM", "Venta de formularios",
            MiscConceptDirection.Income, false, false, RequiresInvoice: true, false, null), CancellationToken.None);
        gravado.Value.RequiresInvoice.Should().BeTrue();
        gravado.Value.RequiresThirdParty.Should().BeTrue("la factura es de un tercero");
        gravado.Value.RequiresCrossDocument.Should().BeTrue("la factura es el documento cruce (FR-179)");
    }

    [Fact]
    public async Task Los_talonarios_de_un_punto_no_se_cruzan()
    {
        var punto = _e.Punto("NORTE");
        var registrar = new RegisterContingencyBookCommandHandler(_e.Db, _e.Alcance, _e.Actor);

        (await registrar.Handle(new RegisterContingencyBookCommand(punto.PublicId, 1, 50), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await registrar.Handle(new RegisterContingencyBookCommand(punto.PublicId, 40, 90), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoTalonarioSeCruza);
        (await registrar.Handle(new RegisterContingencyBookCommand(punto.PublicId, 51, 100), CancellationToken.None)).IsSuccess.Should().BeTrue();
        new RegisterContingencyBookCommandValidator().Validate(new RegisterContingencyBookCommand(punto.PublicId, 10, 5)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task La_regla_de_modalidad_reemplaza_las_vigentes_desde_hoy()
    {
        var vieja = new ChannelModalityRule { Channel = ReceiptChannel.Bank, Target = ApplicationTarget.Loan, LoanModality = 3, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-2) };
        _e.Db.ChannelModalityRules.Add(vieja);
        await _e.Db.SaveChangesAsync();

        var r = await new SetChannelModalityRulesCommandHandler(_e.Db, _e.Reloj).Handle(new SetChannelModalityRulesCommand([
            new ReglaDeModalidadInput(ReceiptChannel.Bank, ApplicationTarget.Loan, 7, null, false, 1),
            new ReglaDeModalidadInput(ReceiptChannel.Bank, ApplicationTarget.Account, null, 2, true, 1),
            new ReglaDeModalidadInput(ReceiptChannel.Bank, ApplicationTarget.Account, null, 1, false, 2),
        ]), CancellationToken.None);

        r.Value.Should().HaveCount(3);
        vieja.ValidTo.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(-1), "lo que ya rigió no cambia: se cierra la víspera");
        new SetChannelModalityRulesCommandValidator().Validate(new SetChannelModalityRulesCommand([
            new ReglaDeModalidadInput(ReceiptChannel.Bank, ApplicationTarget.Loan, null, null, false, 1)])).IsValid.Should().BeFalse("una regla de créditos lleva su modalidad");
    }

    [Fact]
    public async Task El_buscador_comun_avisa_el_codigo_duplicado()
    {
        _e.Punto("NORTE");
        var r = await new BuscarCodigoDeCatalogoQueryHandler(_e.Db).Handle(new BuscarCodigoDeCatalogoQuery("puntos-de-recaudo", "norte"), CancellationToken.None);
        r.Value.Existe.Should().BeTrue();
        r.Value.Nombre.Should().Be("Punto NORTE");
    }
}
