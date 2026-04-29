using FluentAssertions;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Tests.Common;

public class ValueObjectTests
{
    [Fact]
    public void ValueObjects_WithSameValues_ShouldBeEqual()
    {
        var vo1 = new TestValueObject("A", 1);
        var vo2 = new TestValueObject("A", 1);

        vo1.Should().Be(vo2);
        (vo1 == vo2).Should().BeTrue();
    }

    [Fact]
    public void ValueObjects_WithDifferentValues_ShouldNotBeEqual()
    {
        var vo1 = new TestValueObject("A", 1);
        var vo2 = new TestValueObject("B", 2);

        vo1.Should().NotBe(vo2);
        (vo1 != vo2).Should().BeTrue();
    }

    [Fact]
    public void ValueObject_ComparedToNull_ShouldNotBeEqual()
    {
        var vo = new TestValueObject("A", 1);
        vo.Equals(null).Should().BeFalse();
    }

    private class TestValueObject(string name, int value) : ValueObject
    {
        public string Name { get; } = name;
        public int Value { get; } = value;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Name;
            yield return Value;
        }
    }
}
