using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Application.Security.Roles.UpdateRole;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Roles;

/// <summary>
/// Editar un rol: reponer permisos y no dejar la cooperativa sin administración.
///
/// <para>
/// <b>Dos fallos, uno de uso diario y otro irreversible.</b> Los vínculos
/// rol↔permiso no se borran, se marcan <c>IsDeleted</c>; pero el índice único de
/// <c>(RoleId, PermissionId)</c> no filtra por ese campo, así que volver a
/// conceder un permiso que se quitó antes intentaba insertar una fila existente y
/// reventaba. No era un caso raro: quitar un permiso y volver a ponerlo fallaba
/// siempre.
/// </para>
///
/// <para>
/// Y vaciar los permisos de <c>CompanyAdmin</c> dejaba la cooperativa sin nadie
/// capaz de arreglarla: repararlo exige el permiso que se acaba de quitar, la
/// resiembra de arranque no repone —lee los vínculos ignorando el filtro y da los
/// borrados por existentes— y el administrador maestro no puede rescatarla,
/// porque para operar dentro de una cooperativa necesita una membresía activa que
/// por diseño no tiene.
/// </para>
/// </summary>
public class UpdateRoleCommandHandlerTests
{
    private static readonly Guid VerRoles = Guid.NewGuid();
    private static readonly Guid EditarRoles = Guid.NewGuid();
    private static readonly Guid VerUsuarios = Guid.NewGuid();

    private static ICurrentUserService Usuario()
    {
        var u = Substitute.For<ICurrentUserService>();
        u.UserName.Returns("ana@demo");
        return u;
    }

    /// <summary>
    /// Cooperativa con el catálogo mínimo, un rol administrador con los tres
    /// permisos y una persona activa asignada a él.
    /// </summary>
    private static (IApplicationDbContext Db, Role Admin) ConAdministrador()
    {
        var db = TestDbContextFactory.Create();

        var permisos = new[]
        {
            new Permission { Id = 1, PublicId = VerRoles, Resource = "Security.Roles", Action = "View" },
            new Permission { Id = 2, PublicId = EditarRoles, Resource = "Security.Roles", Action = "Update" },
            new Permission { Id = 3, PublicId = VerUsuarios, Resource = "Security.Users", Action = "View" },
        };
        db.Permissions.AddRange(permisos);

        var admin = new Role
        {
            Id = 10, PublicId = Guid.NewGuid(), Code = "CompanyAdmin",
            Name = "Administrador", IsBuiltIn = true, IsActive = true, IsAssignable = true,
        };
        db.Roles.Add(admin);

        foreach (var p in permisos)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = p.Id });
        }

        db.Users.Add(new User { Id = 100, Username = "ana", Email = "ana@demo", IsActive = true });
        db.UserRoles.Add(new UserRole { UserId = 100, RoleId = admin.Id });

        ((DbContext)db).SaveChanges();
        return (db, admin);
    }

    private static UpdateRoleCommand Pidiendo(Role rol, params Guid[] permisos) =>
        new(rol.PublicId, rol.Name, rol.Description, IsAssignable: true, PermissionPublicIds: permisos.ToList());

    [Fact]
    public async Task Volver_a_conceder_un_permiso_quitado_revive_el_vinculo_y_no_inserta_otro()
    {
        // El caso de uso diario que fallaba siempre. Si se insertara una fila
        // nueva, el índice único de (RoleId, PermissionId) la rechazaría.
        var (db, admin) = ConAdministrador();
        var handler = new UpdateRoleCommandHandler(db, Usuario());

        // 1) Quitar "Ver usuarios".
        var quitar = await handler.Handle(Pidiendo(admin, VerRoles, EditarRoles), default);
        quitar.IsSuccess.Should().BeTrue();

        // 2) Volverlo a conceder.
        var reponer = await handler.Handle(Pidiendo(admin, VerRoles, EditarRoles, VerUsuarios), default);
        reponer.IsSuccess.Should().BeTrue("reponer un permiso quitado no puede fallar");

        var filas = await db.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.RoleId == admin.Id && rp.PermissionId == 3)
            .ToListAsync();

        filas.Should().HaveCount(1, "hay que revivir la fila, no crear una segunda del mismo par");
        filas[0].IsDeleted.Should().BeFalse();
        filas[0].DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Vaciar_los_permisos_del_unico_rol_administrador_se_rechaza()
    {
        // El PUT irreversible. Se rechaza ANTES de guardar.
        var (db, admin) = ConAdministrador();

        var result = await new UpdateRoleCommandHandler(db, Usuario())
            .Handle(Pidiendo(admin), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(RoleErrorCodes.LastAdminLockout);

        var siguenVivos = await db.RolePermissions
            .CountAsync(rp => rp.RoleId == admin.Id && !rp.IsDeleted);
        siguenVivos.Should().Be(3, "si el rechazo no impidiera el guardado, no serviría de nada");
    }

    [Fact]
    public async Task Quitar_un_permiso_que_no_hace_falta_para_administrar_se_permite()
    {
        // La invariante no puede convertirse en «los roles no se editan».
        var (db, admin) = ConAdministrador();

        var result = await new UpdateRoleCommandHandler(db, Usuario())
            .Handle(Pidiendo(admin, VerRoles, EditarRoles), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_usuarios_activos_no_bloquea_nada()
    {
        // Una cooperativa recién creada no tiene usuarios todavía, y es justo
        // cuando hay que configurar sus roles. Bloquear aquí haría imposible
        // prepararla: el cambio no deja sin administrador a nadie porque no había
        // nadie que perder.
        var (db, admin) = ConAdministrador();
        var ana = await db.Users.FirstAsync(u => u.Id == 100);
        ana.IsActive = false;
        await db.SaveChangesAsync(default);

        var result = await new UpdateRoleCommandHandler(db, Usuario())
            .Handle(Pidiendo(admin), default);

        result.IsSuccess.Should().BeTrue(
            "sin nadie a quien dejar sin permisos, el cambio no es la causa de nada");
    }
}
