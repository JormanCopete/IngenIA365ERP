using System.Reflection;
using FluentAssertions;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// 2026-11-23: la descripción de <c>Core.Catalogs.Manage</c> pasó de los 200 caracteres de <c>SEC_Permissions.Description</c>
/// (<c>PermissionConfiguration</c>) y la API no arrancó en producción: el seeder corre al iniciar y el pod quedó en
/// <c>CrashLoopBackOff</c>. Todo catálogo de permisos que siembra la API (<c>*PermissionCatalogSeeder.Catalog</c>) cabe en sus columnas.
/// </summary>
public class CatalogosDePermisosCabenEnLaBaseTests
{
    private const int LargoDeLaDescripcion = 200;

    public static TheoryData<string> Catalogos()
    {
        var datos = new TheoryData<string>();
        foreach (var tipo in CatalogosDelEnsamblado()) datos.Add(tipo.Name);
        return datos;
    }

    private static IEnumerable<Type> CatalogosDelEnsamblado() =>
        typeof(CorePermissionCatalogSeeder).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("PermissionCatalogSeeder", StringComparison.Ordinal)
                        && t.GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is not null)
            .OrderBy(t => t.Name);

    [Fact]
    public void Se_revisan_todos_los_catalogos_de_permisos() =>
        CatalogosDelEnsamblado().Select(t => t.Name).Should().Contain(["CorePermissionCatalogSeeder", "DomainPermissionCatalogSeeder",
            "PayrollPermissionCatalogSeeder", "LendingPermissionCatalogSeeder"]);

    [Theory]
    [MemberData(nameof(Catalogos))]
    public void Cada_descripcion_cabe_en_la_columna(string catalogo)
    {
        var tipo = CatalogosDelEnsamblado().Single(t => t.Name == catalogo);
        var filas = (IEnumerable<(string Resource, string Action, string Description)>)tipo
            .GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;

        filas.Where(f => f.Description.Length > LargoDeLaDescripcion)
            .Select(f => $"{f.Resource}.{f.Action} ({f.Description.Length})")
            .Should().BeEmpty($"SEC_Permissions.Description admite {LargoDeLaDescripcion} caracteres");
    }
}
