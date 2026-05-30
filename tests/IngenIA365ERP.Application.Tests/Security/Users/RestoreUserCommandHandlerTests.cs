using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Security.Users.RestoreUser;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// T096 — Tests del handler de restauración de usuarios soft-deleted.
/// Valida los 3 caminos: éxito, usuario inexistente, usuario activo.
/// </summary>
public class RestoreUserCommandHandlerTests
{
    private static (RestoreUserCommandHandler Handler, TestApplicationDbContext Db)
        Build()
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserName.Returns("admin@test");
        return (new RestoreUserCommandHandler(db, cu), db);
    }

    [Fact]
    public async Task Restores_user_when_soft_deleted_and_clears_delete_columns()
    {
        var (handler, db) = Build();
        var deletedAt = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Username = "ana",
            Email = "ana@x",
            PasswordHash = "h",
            IsActive = false,
            IsDeleted = true,
            DeletedAt = deletedAt,
            DeletedBy = "previous@admin"
        };
        db.Users.Add(user);
        db.SaveChanges();

        var result = await handler.Handle(
            new RestoreUserCommand(user.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var restored = db.Users.IgnoreQueryFilters().Single();
        restored.IsDeleted.Should().BeFalse();
        restored.DeletedAt.Should().BeNull();
        restored.DeletedBy.Should().BeNull();
        restored.IsActive.Should().BeTrue("restaurar implica reactivar");
        restored.UpdatedBy.Should().Be("admin@test");
    }

    [Fact]
    public async Task Fails_with_NotFound_when_user_does_not_exist()
    {
        var (handler, _) = Build();

        var result = await handler.Handle(
            new RestoreUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Fails_with_NotDisabled_when_user_is_already_active()
    {
        var (handler, db) = Build();
        var user = new User
        {
            Username = "ana",
            Email = "ana@x",
            PasswordHash = "h",
            IsActive = true,
            IsDeleted = false
        };
        db.Users.Add(user);
        db.SaveChanges();

        var result = await handler.Handle(
            new RestoreUserCommand(user.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrorCodes.NotDisabled);
    }

}
