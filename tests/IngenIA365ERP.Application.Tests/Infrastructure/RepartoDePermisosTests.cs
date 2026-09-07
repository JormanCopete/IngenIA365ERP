using FluentAssertions;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Reparto de permisos a los roles built-in.
///
/// <para>
/// Los permisos <c>Admin.Tenants.*</c> y <c>Saas.*</c> operan SOBRE las
/// cooperativas: listar todas, suspender una. No son de nadie dentro de una
/// cooperativa. Los patrones glob del sembrador los repartían solos —
/// <c>"*"</c> se los daba enteros a CompanyAdmin y <c>"*.View"</c> le daba
/// <c>Admin.Tenants.View</c> a ReadOnly— y el aprovisionamiento de cada
/// cooperativa clonaba esos vínculos. Como <c>/api/saas/tenants</c> no filtra
/// por cooperativa, el administrador de la A podía alcanzar la B.
/// </para>
///
/// <para>
/// Estaba dormido sólo porque el token central no lleva claims <c>perm</c>.
/// Estas pruebas existen para que no despierte.
/// </para>
/// </summary>
public class RepartoDePermisosTests
{
    private static readonly string[] Catalogo =
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    public static TheoryData<string> RolesBuiltIn =>
        [.. BuiltInRolesSeeder.PermissionPatterns.Keys];

    [Theory]
    [MemberData(nameof(RolesBuiltIn))]
    public void NingunRolRecibePermisosSaasGlobales(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo);

        concedidos.Where(BuiltInRolesSeeder.EsSaasGlobal)
            .Should().BeEmpty($"«{rol}» vive dentro de una cooperativa y no puede " +
                              "administrar el conjunto de cooperativas");
    }

    [Fact]
    public void ElGlobPorSiSolo_SiConcederiaLosGlobales()
    {
        // Documenta POR QUE hace falta la lista de retencion. Si alguien la quita
        // pensando que el glob ya es seguro, esta prueba dice que no lo es.
        BuiltInRolesSeeder.MatchesGlob("*.View", "Admin.Tenants.View").Should().BeTrue();
        BuiltInRolesSeeder.MatchesGlob("*", "Admin.Tenants.Suspend").Should().BeTrue();
    }

    [Fact]
    public void CompanyAdmin_ConservaLoQueSiEsSuyo()
    {
        // La lista de retencion no puede pasarse de frenada: las sucursales son
        // de la cooperativa pese a llamarse Admin.Branches.
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo);

        concedidos.Should().Contain("Admin.Branches.Create");
        concedidos.Should().Contain("Security.Users.AssignRole");
        concedidos.Should().NotContain("Admin.Tenants.Suspend");
    }

    [Fact]
    public void ReadOnly_SoloLectura_YSinLaDeCooperativas()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("ReadOnly", Catalogo);

        concedidos.Should().Contain("Admin.Branches.View");
        concedidos.Should().NotContain("Admin.Tenants.View");
        concedidos.Should().OnlyContain(c => c.EndsWith(".View"));
    }

    [Fact]
    public void UnRolDesconocido_NoRecibeNada()
    {
        BuiltInRolesSeeder.CodigosParaRol("NoExiste", Catalogo).Should().BeEmpty();
    }

    [Fact]
    public void LosCodigosRetenidos_ExistenEnElCatalogo()
    {
        // Una errata en la lista de retencion la volveria inerte sin avisar.
        foreach (var codigo in Catalogo.Where(BuiltInRolesSeeder.EsSaasGlobal))
        {
            Catalogo.Should().Contain(codigo);
        }

        Catalogo.Count(BuiltInRolesSeeder.EsSaasGlobal)
            .Should().Be(6, "son los 5 Admin.Tenants.* mas Saas.AuditLog.Verify");
    }
}
