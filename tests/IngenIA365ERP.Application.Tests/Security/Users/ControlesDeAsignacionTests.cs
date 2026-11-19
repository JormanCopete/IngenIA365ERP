using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Users.AssignRole;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Security.Users.RemoveRole;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// 2026-11-19: nadie se asigna a sí mismo un rol con permisos que no tiene, y quitar un rol no deja a la cooperativa sin quien
/// pueda editar roles.
/// </summary>
public class ControlesDeAsignacionTests
{
    private sealed record Escenario(
        TestApplicationDbContext Db, User Admin, User Otro, Role Administrador, Role Consulta);

    private static Escenario Sembrar()
    {
        var db = TestDbContextFactory.Create();
        var ver = new Permission { Resource = "Security.Roles", Action = "View" };
        var editar = new Permission { Resource = "Security.Roles", Action = "Update" };
        var consultar = new Permission { Resource = "Accounting.Vouchers", Action = "View" };
        db.Permissions.AddRange(ver, editar, consultar);
        var admin = new User { Username = "admin", Email = "admin@x", PasswordHash = "h", IsActive = true };
        var otro = new User { Username = "ana", Email = "ana@x", PasswordHash = "h", IsActive = true };
        db.Users.AddRange(admin, otro);
        var administrador = new Role { Code = "ADMIN", Name = "Administrador", IsAssignable = true };
        var consulta = new Role { Code = "CONSULTA", Name = "Consulta", IsAssignable = true };
        db.Roles.AddRange(administrador, consulta);
        db.SaveChanges();

        db.RolePermissions.AddRange(
            new RolePermission { RoleId = administrador.Id, PermissionId = ver.Id },
            new RolePermission { RoleId = administrador.Id, PermissionId = editar.Id },
            new RolePermission { RoleId = administrador.Id, PermissionId = consultar.Id },
            new RolePermission { RoleId = consulta.Id, PermissionId = consultar.Id });
        db.SaveChanges();
        return new Escenario(db, admin, otro, administrador, consulta);
    }

    private static void Asignar(TestApplicationDbContext db, User u, Role r)
    {
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = r.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "seed" });
        db.SaveChanges();
    }

    private static AssignRoleCommandHandler Asignador(TestApplicationDbContext db, User quienPide)
    {
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserName.Returns(quienPide.Username);
        cu.TenantId.Returns("1");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, quienPide.Id, quienPide.PublicId, null, quienPide.Username, null, ExecutionChannel.Web, "web", null, null));
        return new AssignRoleCommandHandler(db, cu, clock, mediator, actor: actor);
    }

    private static RemoveRoleCommandHandler Quitador(TestApplicationDbContext db)
    {
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserName.Returns("admin");
        cu.TenantId.Returns("1");
        return new RemoveRoleCommandHandler(db, cu, Substitute.For<IDateTimeService>());
    }

    [Fact]
    public async Task Nadie_se_asigna_a_si_mismo_un_rol_con_permisos_que_no_tiene()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Otro, e.Consulta);

        var r = await Asignador(e.Db, e.Otro).Handle(new AssignRoleCommand(e.Otro.PublicId, e.Administrador.PublicId), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(UserErrorCodes.SelfEscalation);
        e.Db.UserRoles.Count(ur => ur.UserId == e.Otro.Id).Should().Be(1);
    }

    [Fact]
    public async Task Asignarse_un_rol_sin_permisos_nuevos_si_pasa()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Admin, e.Administrador);

        var r = await Asignador(e.Db, e.Admin).Handle(new AssignRoleCommand(e.Admin.PublicId, e.Consulta.PublicId), default);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Asignarle_a_otro_el_rol_de_administrador_si_pasa()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Admin, e.Administrador);

        var r = await Asignador(e.Db, e.Admin).Handle(new AssignRoleCommand(e.Otro.PublicId, e.Administrador.PublicId), default);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task No_se_quita_el_rol_a_la_ultima_persona_que_puede_editar_roles()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Admin, e.Administrador);

        var r = await Quitador(e.Db).Handle(new RemoveRoleCommand(e.Admin.PublicId, e.Administrador.PublicId), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(UserErrorCodes.LastAdminLockout);
    }

    [Fact]
    public async Task Con_otro_administrador_el_rol_si_se_quita()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Admin, e.Administrador);
        Asignar(e.Db, e.Otro, e.Administrador);

        var dejaSinAdministrador = await ControlesDeAsignacion.DejaSinAdministradorAsync(e.Db, e.Admin.Id, e.Administrador.Id, default);

        dejaSinAdministrador.Should().BeFalse();
    }

    [Fact]
    public async Task Quitar_un_rol_que_no_da_administracion_no_se_bloquea()
    {
        var e = Sembrar();
        Asignar(e.Db, e.Admin, e.Administrador);
        Asignar(e.Db, e.Admin, e.Consulta);

        var dejaSinAdministrador = await ControlesDeAsignacion.DejaSinAdministradorAsync(e.Db, e.Admin.Id, e.Consulta.Id, default);

        dejaSinAdministrador.Should().BeFalse();
    }
}
