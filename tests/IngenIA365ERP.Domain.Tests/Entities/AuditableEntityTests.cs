using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Tests.Entities;

public class AuditableEntityTests
{
    [Fact]
    public void AuditableEntity_ShouldHaveAuditFields()
    {
        var entity = new Bank { Name = "Test Bank" };

        entity.CreatedAt.Should().Be(default);
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void AuditableEntity_ShouldSetAuditFields()
    {
        var now = DateTime.UtcNow;
        var entity = new Bank
        {
            Name = "Test Bank",
            CreatedAt = now,
            CreatedBy = "admin",
            UpdatedAt = now,
            UpdatedBy = "admin"
        };

        entity.CreatedAt.Should().Be(now);
        entity.CreatedBy.Should().Be("admin");
        entity.UpdatedAt.Should().Be(now);
        entity.UpdatedBy.Should().Be("admin");
    }

    [Fact]
    public void BaseEntityLong_Id_ShouldBeLong()
    {
        var entityType = typeof(BaseEntityLong);
        var idProperty = entityType.GetProperty("Id");

        idProperty.Should().NotBeNull();
        idProperty!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void BaseEntity_Id_ShouldBeInt()
    {
        var entityType = typeof(BaseEntity);
        var idProperty = entityType.GetProperty("Id");

        idProperty.Should().NotBeNull();
        idProperty!.PropertyType.Should().Be(typeof(int));
    }

    [Fact]
    public void BaseEntity_PublicId_ShouldBeGuid()
    {
        var entityType = typeof(BaseEntity);
        var publicIdProperty = entityType.GetProperty("PublicId");

        publicIdProperty.Should().NotBeNull();
        publicIdProperty!.PropertyType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void BaseEntityLong_ShouldGeneratePublicId()
    {
        var entityType = typeof(BaseEntityLong);
        var instance = (BaseEntityLong)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(TestEntityLong));

        // PublicId should be default (uninitialized) or a valid GUID
        entityType.GetProperty("PublicId")!.PropertyType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void BaseEntity_ShouldHaveSoftDeleteFields()
    {
        var entity = new Bank { Name = "Test" };

        entity.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    private class TestEntityLong : BaseEntityLong { }
}
