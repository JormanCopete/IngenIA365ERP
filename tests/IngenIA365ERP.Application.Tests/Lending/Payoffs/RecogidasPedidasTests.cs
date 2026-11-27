using FluentAssertions;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T013, R1): la lista vieja <c>ownLoanPayoffs</c> se sigue aceptando y se traduce a <see cref="PayoffInput"/> de créditos
/// (<c>Full</c> → <c>Total</c>, <c>Partial</c> → <c>Partial</c>); la nueva pasa tal cual; las dos a la vez es <c>Request.BodyInvalid</c>.
/// </summary>
public class RecogidasPedidasTests
{
    private static readonly Guid Credito1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Credito2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Sin_ninguna_lista_no_hay_recogidas()
    {
        var r = RecogidasPedidas.Unificar(null, null);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeEmpty();
        RecogidasPedidas.Unificar([], []).Value.Should().BeEmpty();
    }

    [Fact]
    public void La_lista_vieja_se_traduce_a_creditos()
    {
        var r = RecogidasPedidas.Unificar(null,
        [
            new OwnLoanPayoffInput(Credito1),
            new OwnLoanPayoffInput(Credito2, "Partial", 300_000m),
        ]);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Equal(
            new PayoffInput(PayoffProductKind.Loan, Credito1, PayoffMode.Total),
            new PayoffInput(PayoffProductKind.Loan, Credito2, PayoffMode.Partial, 300_000m));
    }

    [Theory]
    [InlineData("Full", PayoffMode.Total)]
    [InlineData("full", PayoffMode.Total)]
    [InlineData("", PayoffMode.Total)]
    [InlineData(" Partial ", PayoffMode.Partial)]
    [InlineData("PARTIAL", PayoffMode.Partial)]
    public void Los_modos_viejos_se_leen_sin_distinguir_mayusculas(string modo, PayoffMode esperado) =>
        RecogidasPedidas.Unificar(null, [new OwnLoanPayoffInput(Credito1, modo, 10m)]).Value.Single().Mode.Should().Be(esperado);

    [Fact]
    public void Un_total_viejo_no_arrastra_el_valor()
    {
        var r = RecogidasPedidas.Unificar(null, [new OwnLoanPayoffInput(Credito1, "Full", 500m)]);

        r.Value.Single().Amount.Should().BeNull("un total recoge lo que cancela, como hoy");
    }

    [Fact]
    public void Un_modo_viejo_desconocido_es_un_cuerpo_invalido()
    {
        var r = RecogidasPedidas.Unificar(null, [new OwnLoanPayoffInput(Credito1, "Todo")]);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(RecogidasPedidas.CodigoCuerpoInvalido);
        r.Error.Message.Should().Contain("Todo");
    }

    [Fact]
    public void La_lista_nueva_pasa_tal_cual()
    {
        IReadOnlyList<PayoffInput> nuevas =
        [
            new(PayoffProductKind.Loan, Credito1, PayoffMode.Partial, 100m, InstallmentTreatment: InstallmentTreatment.Exclude),
            new(PayoffProductKind.CommercialReceivable, null, PayoffMode.Total, Reference: "FV-77"),
        ];

        var r = RecogidasPedidas.Unificar(nuevas, []);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeSameAs(nuevas);
    }

    [Fact]
    public void Las_dos_listas_a_la_vez_son_un_cuerpo_invalido()
    {
        var r = RecogidasPedidas.Unificar(
            [new PayoffInput(PayoffProductKind.Loan, Credito1, PayoffMode.Total)],
            [new OwnLoanPayoffInput(Credito2)]);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Request.BodyInvalid");
    }
}
