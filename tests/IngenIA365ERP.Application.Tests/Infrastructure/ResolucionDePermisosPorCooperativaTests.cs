using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Resolución de permisos dentro del esquema de una cooperativa.
///
/// <para>
/// <b>De dónde viene ahora el aislamiento.</b> Antes se intentaba con un
/// predicado de columna (<c>Role.TenantId == idCooperativa</c>) que nunca pudo
/// funcionar: la clave foránea apuntaba al <c>ADM_Tenants</c> local de cada
/// esquema, vacío, así que insertar un rol de cooperativa era físicamente
/// imposible. Ahora el aislamiento es el <b>esquema</b>: dentro del de una
/// cooperativa, todos los roles son suyos.
/// </para>
///
/// <para>
/// <b>Qué ya no se puede probar aquí, y dónde se prueba.</b> "El rol de otra
/// cooperativa no concede nada" era comprobable con una columna; con esquemas
/// no lo es, porque EF InMemory no los tiene. Esa garantía se verifica con dos
/// cooperativas reales sobre PostgreSQL. Lo que sí queda aquí es que el caché
/// nunca las mezcle, que es la otra mitad del mismo riesgo.
/// </para>
/// </summary>
public class ResolucionDePermisosPorCooperativaTests
{
    private const int Cooperativa = 1;

    private static TestApplicationDbContext Sembrar(string nombre)
    {
        var db = TestDbContextFactory.Create(nombre);

        var verSucursales = new Permission { Id = 10, Resource = "Admin.Branches", Action = "View" };
        var crearSucursal = new Permission { Id = 11, Resource = "Admin.Branches", Action = "Create" };
        var verCooperativas = new Permission { Id = 12, Resource = "Admin.Tenants", Action = "View" };
        db.Permissions.AddRange(verSucursales, crearSucursal, verCooperativas);

        // TenantId nulo: es lo normal dentro del esquema de una cooperativa.
        db.Roles.AddRange(
            new Role { Id = 100, Code = "CompanyAdmin", Name = "Admin", TenantId = null, IsActive = true },
            new Role { Id = 400, Code = "Suspendido", Name = "Inactivo", TenantId = null, IsActive = false });

        db.RolePermissions.AddRange(
            new RolePermission { RoleId = 100, PermissionId = 10 },
            new RolePermission { RoleId = 100, PermissionId = 11 },
            // Vínculo prohibido puesto a mano: comprueba el segundo candado.
            new RolePermission { RoleId = 100, PermissionId = 12 },
            new RolePermission { RoleId = 400, PermissionId = 11 });

        db.SaveChanges();
        return db;
    }

    private static UserPermissionResolver Resolutor(
        TestApplicationDbContext db, IPermissionClaimsCache? cache = null) =>
        new(db, NullLogger<UserPermissionResolver>.Instance, cache);

    private static void Asignar(TestApplicationDbContext db, int usuario, params int[] roles)
    {
        foreach (var rol in roles) db.UserRoles.Add(new UserRole { UserId = usuario, RoleId = rol });
        db.SaveChanges();
    }

    [Fact]
    public async Task ResuelveLosPermisosDeLosRolesDelEsquema()
    {
        using var db = Sembrar(nameof(ResuelveLosPermisosDeLosRolesDelEsquema));
        Asignar(db, usuario: 7, roles: 100);

        var r = await Resolutor(db).ResolveForTenantAsync(7, Cooperativa, default);

        r.Should().Contain("Admin.Branches.View");
        r.Should().Contain("Admin.Branches.Create");
    }

    [Fact]
    public async Task NuncaDevuelvePermisosSaasGlobales()
    {
        // Aunque el vínculo esté puesto en la base: Admin.Tenants.* opera SOBRE el
        // conjunto de cooperativas y sólo lo ejerce el administrador maestro.
        using var db = Sembrar(nameof(NuncaDevuelvePermisosSaasGlobales));
        Asignar(db, usuario: 7, roles: 100);

        var r = await Resolutor(db).ResolveForTenantAsync(7, Cooperativa, default);

        r.Should().NotContain("Admin.Tenants.View");
    }

    [Fact]
    public async Task UnRolDesactivadoNoConcedeNada()
    {
        using var db = Sembrar(nameof(UnRolDesactivadoNoConcedeNada));
        Asignar(db, usuario: 7, roles: 400);

        var r = await Resolutor(db).ResolveForTenantAsync(7, Cooperativa, default);

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task UnUsuarioSinRolesNoResuelveNada()
    {
        using var db = Sembrar(nameof(UnUsuarioSinRolesNoResuelveNada));

        var r = await Resolutor(db).ResolveForTenantAsync(99, Cooperativa, default);

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task ElCacheSeparaLasCooperativas()
    {
        // La otra mitad del aislamiento. El esquema separa las FILAS; esto separa lo
        // que se recuerda de ellas. Con una clave compartida —y la había: se invalidaba
        // con cadena vacía— un usuario que cambia de cooperativa leería el conjunto de
        // permisos de la anterior, con los esquemas perfectamente aislados.
        using var db = Sembrar(nameof(ElCacheSeparaLasCooperativas));
        Asignar(db, usuario: 7, roles: 100);
        var cache = Substitute.For<IPermissionClaimsCache>();
        cache.GetAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>?)null);

        await Resolutor(db, cache).ResolveForTenantAsync(7, tenantInternalId: 1, default);
        await Resolutor(db, cache).ResolveForTenantAsync(7, tenantInternalId: 2, default);

        await cache.Received(1).SetAsync(7, "1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await cache.Received(1).SetAsync(7, "2", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().SetAsync(
            Arg.Any<int>(), string.Empty, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoCacheadoSeDevuelveSinTocarLaBase()
    {
        using var db = Sembrar(nameof(LoCacheadoSeDevuelveSinTocarLaBase));
        var cache = Substitute.For<IPermissionClaimsCache>();
        cache.GetAsync(7, "1", Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>?>(["Algo.Guardado"]);

        var r = await Resolutor(db, cache).ResolveForTenantAsync(7, 1, default);

        r.Should().Equal("Algo.Guardado");
    }
}
