using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Treasury.Accounting;

/// <summary>
/// Feature 015, US8 (pendiente de T0250 cerrado el 2026-10-24; FR-221; contabilidad.md §6): las pantallas de Tesorería (resultado del giro, cheques, remesas)
/// preguntan la validación previa antes de confirmar, como la caja de Recaudo. Con «Bloquear» y una regla que falta, <c>Treasury.Prevalidation.NotPostable</c> con
/// <c>data.errors[] { factType, role, combination, whoFixes }</c>; con «Advertir» o sin contabilidad iniciada, sigue; la cuenta propia viaja para los roles por
/// vínculo. Prueba escrita, sin correr.
/// </summary>
public class ValidacionPreviaEnPantallasDeTesoreriaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 2);

    private static (EmisionDeTesoreria Emision, IContabilidadParaTesoreria Contabilidad) Crear(EvaluacionContableDto respuesta)
    {
        var contabilidad = Substitute.For<IContabilidadParaTesoreria>();
        contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>()).Returns(respuesta);
        return (new EmisionDeTesoreria(null!, null!, contabilidad, null!, null!), contabilidad);
    }

    [Fact]
    public async Task Con_Bloquear_y_una_regla_que_falta_la_pantalla_no_confirma()
    {
        var (emision, _) = Crear(new EvaluacionContableDto(false, "Bloquear", [new CombinacionSinReglaDto(RemesaConfirmadaV1.Type, "Banco", "sin vínculo")]));

        var r = await emision.ValidacionPreviaAsync([RemesaConfirmadaV1.Type], new OwnBankAccount { Code = "CTA1" }, Hoy, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoNoContabilizable).And.Be("Treasury.Prevalidation.NotPostable");
    }

    [Theory]
    [InlineData("Advertir")]
    [InlineData("SinContabilidad")]
    public async Task Con_Advertir_o_sin_contabilidad_iniciada_sigue(string politica)
    {
        var (emision, _) = Crear(new EvaluacionContableDto(politica == "SinContabilidad", politica, politica == "SinContabilidad"
            ? []
            : [new CombinacionSinReglaDto(PagoDeOrdenGiradoV1.Type, "PuenteDeGiroDelOrigen", "sin regla")]));

        (await emision.ValidacionPreviaAsync([PagoDeOrdenGiradoV1.Type], null, Hoy, CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Pregunta_por_cada_tipo_de_hecho_con_la_cuenta_propia_y_sin_repetir()
    {
        var (emision, contabilidad) = Crear(new EvaluacionContableDto(true, "Bloquear", []));
        var cuenta = new OwnBankAccount { Code = "CTA1" };

        await emision.ValidacionPreviaAsync([PagoDeOrdenGiradoV1.Type, GmfDePagoCausadoV1.Type, PagoDeOrdenGiradoV1.Type], cuenta, Hoy, CancellationToken.None);

        await contabilidad.Received(1).EvaluarAsync(Arg.Is<IReadOnlyList<CombinacionDeHecho>>(l =>
            l.Count == 2 && l.All(c => c.Module == "TES" && c.AccountingDate == Hoy
                                       && c.Dimensions[nameof(TreasuryFactDimensionsV1.OwnBankAccountPublicId)] == cuenta.PublicId.ToString())), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_hechos_no_pregunta()
    {
        var (emision, contabilidad) = Crear(new EvaluacionContableDto(false, "Bloquear", []));

        (await emision.ValidacionPreviaAsync([], null, Hoy, CancellationToken.None)).IsSuccess.Should().BeTrue();
        await contabilidad.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }
}
