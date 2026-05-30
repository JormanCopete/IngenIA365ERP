using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Application.Security.Roles.CreateRole;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Roles;

public class CreateRoleCommandHandlerTests
{
    private static (CreateRoleCommandHandler Handler, TestApplicationDbContext Db) Build(int? tenantId = 1)
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserId.Returns(99);
        cu.UserName.Returns("admin@test");
        cu.TenantId.Returns(tenantId?.ToString());
        return (new CreateRoleCommandHandler(db, cu), db);
    }

    private static Permission SeedPermission(TestApplicationDbContext db, string resource, string action)
    {
        var p = new Permission { Resource = resource, Action = action };
        db.Permissions.Add(p);
        db.SaveChanges();
        return p;
    }

    [Fact]
    public async Task Creates_role_with_unique_code_and_returns_publicId()
    {
        var (handler, db) = Build();
        var p1 = SeedPermission(db, "Users", "View");
        var p2 = SeedPermission(db, "Roles", "View");

        var result = await handler.Handle(
            new CreateRoleCommand("CajaJunior", "Caja Junior", "desc",
                new[] { p1.PublicId, p2.PublicId }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        var role = db.Roles.Single();
        role.Code.Should().Be("CajaJunior");
        role.TenantId.Should().Be(1);
        role.IsBuiltIn.Should().BeFalse();
        db.RolePermissions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Rejects_duplicate_code_within_same_tenant()
    {
        var (handler, db) = Build();
        db.Roles.Add(new Role { TenantId = 1, Code = "CajaJunior", Name = "Existente" });
        db.SaveChanges();

        var result = await handler.Handle(
            new CreateRoleCommand("CajaJunior", "Caja Junior", null, Array.Empty<Guid>()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(RoleErrorCodes.CodeAlreadyExists);
    }

    [Fact]
    public async Task Rejects_when_some_permission_ids_do_not_exist()
    {
        var (handler, db) = Build();
        var realPerm = SeedPermission(db, "Users", "View");
        var fakePerm = Guid.NewGuid();

        var result = await handler.Handle(
            new CreateRoleCommand("Test", "Test", null, new[] { realPerm.PublicId, fakePerm }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(RoleErrorCodes.PermissionsInvalid);
        db.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_when_code_collides_with_builtin_role_in_same_tenant()
    {
        // FR-020: los built-ins viven en SEC_Roles del tenant tras provisioning
        // (ver BuiltInRolesSeeder + ProvisionTenantSchemaCommand). Crear un rol
        // custom con el mismo Code debe fallar con CodeAlreadyExists — el filtro
        // por tenantId asegura que el built-in de otro tenant NO bloquea.
        var (handler, db) = Build();
        db.Roles.Add(new Role
        {
            TenantId = 1,
            Code = "CompanyAdmin",
            Name = "Administrador de Cooperativa",
            IsBuiltIn = true,
            IsAssignable = true,
            IsActive = true
        });
        db.SaveChanges();

        var result = await handler.Handle(
            new CreateRoleCommand("CompanyAdmin", "Mi Admin Custom", null, Array.Empty<Guid>()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(RoleErrorCodes.CodeAlreadyExists);
        db.Roles.Should().HaveCount(1); // solo el built-in original
    }
}
