using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Tests.Common;

public class BaseEntityTests
{
    [Fact]
    public void BaseEntity_ShouldGeneratePublicId()
    {
        var entity = new Bank { Name = "Test Bank" };
        entity.PublicId.Should().NotBeEmpty();
    }

    [Fact]
    public void BaseEntity_ShouldSupportDomainEvents()
    {
        var entity = new Bank { Name = "Test Bank" };
        var domainEvent = new TestDomainEvent();

        entity.AddDomainEvent(domainEvent);
        entity.DomainEvents.Should().ContainSingle();

        entity.RemoveDomainEvent(domainEvent);
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void BaseEntity_ClearDomainEvents_ShouldRemoveAll()
    {
        var entity = new Bank { Name = "Test Bank" };
        entity.AddDomainEvent(new TestDomainEvent());
        entity.AddDomainEvent(new TestDomainEvent());

        entity.ClearDomainEvents();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void BaseEntity_IsDeleted_ShouldDefaultToFalse()
    {
        var entity = new Bank { Name = "Test Bank" };
        entity.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    private class TestDomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
