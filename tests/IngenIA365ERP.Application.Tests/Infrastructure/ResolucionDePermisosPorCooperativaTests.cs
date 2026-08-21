using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Resolución de permisos acotada a una cooperativa.
///
/// <para>
/// Es la frontera que decide qué puede hacer cada quien. El método anterior
/// (<c>ResolveAsync</c>) recibía el tenant como cadena y no filtraba por él:
/// devolvía los permisos de TODOS los roles del usuario, de la cooperativa que
/// fueran. Mientras nadie autorizara con el resultado, daba igual. Desde que el
/// filtro lo lee, cada prueba de aquí es un candado.
/// </para>
/// </summary>
public class ResolucionDePermisosPorCooperativaTests
{
    private const int CoopA = 1;
    private const int CoopB = 2;

    private static TestApplicationDbContext Sembrar(string nombre)
    {
        var db = TestDbContextFactory.Create(nombre);

        // Códigos: dos propios de la cooperativa y uno SaaS-global.
        var verSucursales = new Permission { Id = 10, Resource = "Admin.Branches", Action = "View" };
        var crearSucursal = new Permission { Id = 11, Resource = "Admin.Branches", Action = "Create" };
        var verCooperativas = new Permission { Id = 12, Resource = "Admin.Tenants", Action = "View" };
        db.Permissions.AddRange(verSucursales, crearSucursal, verCooperativas);

        db.Roles.AddRange(
            new Role { Id = 100, Code = "CompanyAdmin", Name = "Admin A", TenantId = CoopA, IsActive = true },
            new Role { Id = 200, Code = "CompanyAdmin", Name = "Admin B", TenantId = CoopB, IsActive = true },
            new Role { Id = 300, Code = "CompanyAdmin", Name = "Plantilla", TenantId = null, IsActive = true },
            new Role { Id = 400, Code = "Suspendido", Name = "Inactivo", TenantId = CoopA, IsActive = false });

        db.RolePermissions.AddRange(
            new RolePermission { RoleId = 100, PermissionId = 10 },
            new RolePermission { RoleId = 100, PermissionId = 11 },
            // El vínculo prohibido, puesto a mano: así se comprueba el segundo candado.
            new RolePermission { RoleId = 100, PermissionId = 12 },
            new RolePermission { RoleId = 200, PermissionId = 10 },
            new RolePermission { RoleId = 300, PermissionId = 10 },
            new RolePermission { RoleId = 400, PermissionId = 11 });

        db.SaveChanges();
        return db;
    }

    private static UserPermissionResolver Resolutor(TestApplicationDbContext db) =>
        new(db, NullLogger<UserPermissionResolver>.Instance, cache: null);

    private static void Asignar(TestApplicationDbContext db, int usuario, params int[] roles)
    {
        foreach (var rol in roles)
        {
            db.UserRoles.Add(new UserRole { UserId = usuario, RoleId = rol });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task ResuelveLosPermisosDeSuCooperativa()
    {
        using var db = Sembrar(nameof(ResuelveLosPermisosDeSuCooperativa));
        Asignar(db, usuario: 7, roles: 100);

        var r = await Resolutor(db).ResolveForTenantAsync(7, CoopA, default);

        r.Should().Contain("Admin.Branches.View");
        r.Should().Contain("Admin.Branches.Create");
    }

    [Fact]
    public async Task NoResuelveNadaEnUnaCooperativaAjena()
    {
        // El candado principal: el usuario sólo tiene rol en A.
        using var db = Sembrar(nameof(NoResuelveNadaEnUnaCooperativaAjena));
        Asignar(db, usuario: 7, roles: 100);

        var r = await Resolutor(db).ResolveForTenantAsync(7, CoopB, default);

        r.Should().BeEmpty("el usuario no tiene ningún rol en esa cooperativa");
    }

    [Fact]
    public async Task NuncaDevuelvePermisosSaasGlobales()
    {
        // Aunque el vínculo esté en la base: el rol 100 tiene Admin.Tenants.View.
        using var db = Sembrar(nameof(NuncaDevuelvePermisosSaasGlobales));
        Asignar(db, usuario: 7, roles: 100);

        var r = await Resolutor(db).ResolveForTenantAsync(7, CoopA, default);

        r.Should().NotContain("Admin.Tenants.View",
            "opera sobre el conjunto de cooperativas, no dentro de una");
    }

    [Fact]
    public async Task UnRolPlantillaNoConcedeNada()
    {
        // TenantId == null son las plantillas SaaS. Honrarlas daría a cualquiera
        // los permisos del molde del que se clonan los roles de cada cooperativa.
        using var db = Sembrar(nameof(UnRolPlantillaNoConcedeNada));
        Asignar(db, usuario: 7, roles: 300);

        var r = await Resolutor(db).ResolveForTenantAsync(7, CoopA, default);

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task UnRolDesactivadoNoConcedeNada()
    {
        using var db = Sembrar(nameof(UnRolDesactivadoNoConcedeNada));
        Asignar(db, usuario: 7, roles: 400);

        var r = await Resolutor(db).ResolveForTenantAsync(7, CoopA, default);

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task UnUsuarioSinRolesNoResuelveNada()
    {
        using var db = Sembrar(nameof(UnUsuarioSinRolesNoResuelveNada));

        var r = await Resolutor(db).ResolveForTenantAsync(99, CoopA, default);

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task ConRolesEnDosCooperativas_CadaUnaDevuelveLoSuyo()
    {
        using var db = Sembrar(nameof(ConRolesEnDosCooperativas_CadaUnaDevuelveLoSuyo));
        Asignar(db, usuario: 7, roles: [100, 200]);
        var resolutor = Resolutor(db);

        var enA = await resolutor.ResolveForTenantAsync(7, CoopA, default);
        var enB = await resolutor.ResolveForTenantAsync(7, CoopB, default);

        enA.Should().Contain("Admin.Branches.Create");
        enB.Should().NotContain("Admin.Branches.Create",
            "en B sólo tiene el rol que concede View; los permisos no se suman entre cooperativas");
        enB.Should().Contain("Admin.Branches.View");
    }
}
