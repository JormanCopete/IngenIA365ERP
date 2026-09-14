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
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

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

    // ------------------------------------------------------ feature 008: maestros de persona --

    [Fact]
    public void Operator_CreaYEdita_PeroNoDaDeBaja()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("Operator", Catalogo);

        concedidos.Should().Contain(["Core.People.View", "Core.People.Create", "Core.People.Update",
                                     "Core.Associates.View", "Core.Associates.Create", "Core.Associates.Update",
                                     "Payroll.Employees.View", "Payroll.Employees.Create", "Payroll.Employees.Update"]);
        concedidos.Should().NotContain("Core.People.Delete", "eliminar y restaurar personas es del administrador");
        concedidos.Should().NotContain("Payroll.Employees.Terminate", "terminar contratos es del administrador");
    }

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("Auditor")]
    public void ReadOnlyYAuditor_SoloLeenLosMaestros(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo);

        concedidos.Should().Contain(BuiltInRolesSeeder.LecturaDeMaestros);
        concedidos.Should().NotContain(c =>
            (c.StartsWith("Core.People.") || c.StartsWith("Core.Associates.") || c.StartsWith("Payroll.Employees."))
            && !c.EndsWith(".View"));
    }

    [Fact]
    public void CompanyAdmin_PuedeTodoEnLosMaestros()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo);

        concedidos.Should().Contain(["Core.People.Delete", "Payroll.Employees.Terminate"]);
    }

    [Fact]
    public void LaLecturaDeMaestros_ExisteEnElCatalogo()
    {
        // Es lo que se concede a los roles personalizados el día del despliegue (FR-010):
        // una errata en la lista la volvería inerte sin avisar.
        foreach (var codigo in BuiltInRolesSeeder.LecturaDeMaestros)
        {
            Catalogo.Should().Contain(codigo);
        }
    }

    [Fact]
    public void LosCatalogosNoSePisan()
    {
        Catalogo.Should().OnlyHaveUniqueItems("un código repetido entre seeders se insertaría dos veces");
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
