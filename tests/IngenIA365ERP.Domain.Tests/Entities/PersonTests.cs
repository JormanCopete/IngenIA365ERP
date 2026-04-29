using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Tests.Entities;

public class PersonTests
{
    [Fact]
    public void NewPerson_ShouldHaveDefaultValues()
    {
        var person = new Person
        {
            FirstName = "Juan",
            LastName = "Garcia",
            TaxId = "1234567890"
        };

        person.PublicId.Should().NotBeEmpty();
        person.IsDeleted.Should().BeFalse();
        person.IdType.Should().Be("C");
        person.IsAssociate.Should().BeFalse();
        person.IsEmployee.Should().BeFalse();
    }

    [Fact]
    public void Person_Properties_ShouldRetainValues()
    {
        var person = new Person
        {
            FirstName = "Maria",
            LastName = "Lopez",
            Email = "maria@test.com",
            Phone1 = "3001234567"
        };

        person.FirstName.Should().Be("Maria");
        person.LastName.Should().Be("Lopez");
        person.Email.Should().Be("maria@test.com");
        person.Phone1.Should().Be("3001234567");
    }

    [Fact]
    public void Person_Navigation_ShouldBeNullByDefault()
    {
        var person = new Person
        {
            FirstName = "Test",
            LastName = "User",
            TaxId = "999"
        };

        person.City.Should().BeNull();
        person.Associate.Should().BeNull();
        person.Spouse.Should().BeNull();
        person.Financial.Should().BeNull();
    }
}
