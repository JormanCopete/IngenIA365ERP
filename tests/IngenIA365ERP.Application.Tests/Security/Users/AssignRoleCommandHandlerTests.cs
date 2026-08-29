using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Users.AssignRole;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

public class AssignRoleCommandHandlerTests
{
    private static (AssignRoleCommandHandler Handler, TestApplicationDbContext Db,
                    IPermissionClaimsCache Cache, ISender Mediator)
        Build()
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserId.Returns(99);
        cu.UserName.Returns("admin@test");
        cu.TenantId.Returns("1");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc));
        var cache = Substitute.For<IPermissionClaimsCache>();
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(IngenIA365ERP.Application.Common.Models.Result.Success());
        return (new AssignRoleCommandHandler(db, cu, clock, mediator, cache), db, cache, mediator);
    }

    private static (User U, Role R) SeedUserAndRole(TestApplicationDbContext db,
        bool roleAssignable = true, bool alreadyAssigned = false)
    {
        var u = new User { Username = "ana", Email = "ana@x", PasswordHash = "h", IsActive = true };
        // TenantId 7, distinto del "1" que devuelve el contexto de la peticion en
        // Build(): asi la prueba distingue de cual de los dos sale la clave.
        var r = new Role { Code = "TesoreroJunior", Name = "Tesorero Junior", TenantId = 7, IsAssignable = roleAssignable };
        db.Users.Add(u);
        db.Roles.Add(r);
        db.SaveChanges();
        if (alreadyAssigned)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = u.Id, RoleId = r.Id,
                AssignedAt = DateTime.UtcNow, AssignedBy = "seed"
            });
            db.SaveChanges();
        }
        return (u, r);
    }

    [Fact]
    public async Task Assigns_role_invalidates_cache_and_notifies()
    {
        var (handler, db, cache, mediator) = Build();
        var (user, role) = SeedUserAndRole(db);

        var result = await handler.Handle(
            new AssignRoleCommand(user.PublicId, role.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.UserRoles.Should().HaveCount(1);

        // La cooperativa sale de la peticion —"1" en Build()— y no del rol: bajo
        // schema-per-tenant los roles viven DENTRO del esquema de su cooperativa y
        // llevan TenantId nulo, asi que tomarla del rol daria clave vacia. Tiene que
        // coincidir con la que usa PermisosDeLaPeticion al poblar el cache, o la
        // invalidacion no encuentra la entrada y el permiso revocado sigue vivo.
        await cache.Received(1).InvalidateAsync(user.Id, "1", Arg.Any<CancellationToken>());

        await mediator.Received(1).Send(
            Arg.Is<SendNotificationCommand>(c => c.Payload.Type == NotificationType.RoleAssigned),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_when_role_not_assignable()
    {
        var (handler, db, _, _) = Build();
        var (user, role) = SeedUserAndRole(db, roleAssignable: false);

        var result = await handler.Handle(
            new AssignRoleCommand(user.PublicId, role.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrorCodes.RoleNotAssignable);
    }

    [Fact]
    public async Task Rejects_double_assignment_with_AlreadyAssignedRole()
    {
        var (handler, db, _, _) = Build();
        var (user, role) = SeedUserAndRole(db, alreadyAssigned: true);

        var result = await handler.Handle(
            new AssignRoleCommand(user.PublicId, role.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrorCodes.AlreadyAssignedRole);
    }
}
