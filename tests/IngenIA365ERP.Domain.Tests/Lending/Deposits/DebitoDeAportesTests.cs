using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>Feature 014, US3 (T086, T092; FR-042): de qué clases sale un débito de aportes y que nunca perfora el mínimo no reducible.</summary>
public class DebitoDeAportesTests
{
    private static readonly IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos = new Dictionary<MemberAccountConcept, decimal>
    {
        [MemberAccountConcept.ContributionMinimum] = 500_000m,
        [MemberAccountConcept.ContributionOrdinary] = 300_000m,
        [MemberAccountConcept.ContributionExtraordinary] = 100_000m,
    };

    [Fact]
    public void Primero_los_extraordinarios_luego_los_ordinarios_y_por_ultimo_el_minimo()
    {
        DebitoDeAportes.Repartir(Saldos, 250_000m, 500_000m)!.Should().Equal(
            (MemberAccountConcept.ContributionExtraordinary, 100_000m), (MemberAccountConcept.ContributionOrdinary, 150_000m));
        DebitoDeAportes.Repartir(Saldos, 600_000m, 0m)!.Should().Equal(
            (MemberAccountConcept.ContributionExtraordinary, 100_000m), (MemberAccountConcept.ContributionOrdinary, 300_000m),
            (MemberAccountConcept.ContributionMinimum, 200_000m));
    }

    [Fact]
    public void Nunca_deja_el_total_por_debajo_del_minimo()
    {
        DebitoDeAportes.Devolvible(Saldos, 500_000m).Should().Be(400_000m);
        DebitoDeAportes.Repartir(Saldos, 400_000m, 500_000m).Should().NotBeNull();
        DebitoDeAportes.Repartir(Saldos, 400_001m, 500_000m).Should().BeNull();
    }
}
