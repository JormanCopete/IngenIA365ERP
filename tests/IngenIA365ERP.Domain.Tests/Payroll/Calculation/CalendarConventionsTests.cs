using FluentAssertions;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Tests.Payroll.Calculation;

/// <summary>FR-008: el calendario comercial de 30 días, con febrero y los meses de 31.</summary>
public class CalendarConventionsTests
{
    [Theory]
    [InlineData("2026-03-01", "2026-03-31", 30)]
    [InlineData("2026-02-01", "2026-02-28", 30)]
    [InlineData("2026-03-10", "2026-03-31", 21)]
    [InlineData("2026-02-10", "2026-02-28", 21)]
    [InlineData("2026-03-16", "2026-03-31", 15)]
    [InlineData("2026-03-01", "2026-03-15", 15)]
    [InlineData("2026-03-31", "2026-03-31", 1)]
    [InlineData("2026-03-30", "2026-03-31", 1)]
    [InlineData("2026-03-20", "2026-04-05", 16)]
    [InlineData("2026-01-15", "2026-03-14", 60)]
    [InlineData("2026-03-10", "2026-03-09", 0)]
    public void Dias_comerciales(string desde, string hasta, int esperado)
    {
        CalendarConventions.Days(DateTime.Parse(desde), DateTime.Parse(hasta)).Should().Be(esperado);
    }

    [Fact]
    public void Solape_de_rangos()
    {
        var s = CalendarConventions.Overlap(new(2026, 3, 1), new(2026, 3, 31), new(2026, 3, 28), new(2026, 4, 3));
        s.Should().NotBeNull();
        s!.Value.From.Should().Be(new DateTime(2026, 3, 28));
        s.Value.To.Should().Be(new DateTime(2026, 3, 31));

        CalendarConventions.Overlap(new(2026, 3, 1), new(2026, 3, 31), new(2026, 4, 1), new(2026, 4, 3)).Should().BeNull();
    }
}
