using IngenIA365ERP.Architecture.Tests.Helpers;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Reglas arquitectónicas de la feature 004-multi-motor-bd (T018). La
/// variabilidad de proveedor de BD está encapsulada en
/// <c>IngenIA365ERP.Persistence.Providers</c> (IDbProviderConfigurator y los
/// dos configuradores concretos) y en los ensamblados de migraciones por
/// proveedor. Ningún otro tipo del sistema puede depender de tipos de los
/// paquetes de proveedor (UseSqlServer/UseNpgsql y compañía) — FR-005 y
/// principio constitucional II.
/// </summary>
[Xunit.Collection(Arquitecturas.Coleccion)]
public class Feature004_MultiProvider
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = Arquitecturas.ConPersistenciaEIdentidad.Value;

    /// <summary>Tipos de los paquetes de proveedor EF (SqlServer / Npgsql).</summary>
    private readonly IObjectProvider<IType> ProviderPackageTypes =
        Types().That().HaveFullNameContaining("Npgsql")
               .Or().HaveFullNameContaining("SqlServerDbContextOptions")
               .Or().HaveFullNameContaining("EntityFrameworkCore.SqlServer")
               .As("Tipos de paquetes de proveedor EF");

    [Fact]
    public void Domain_and_Application_should_not_reference_provider_packages()
    {
        Types().That()
            .ResideInNamespace("IngenIA365ERP.Domain")
            .Or().ResideInNamespace("IngenIA365ERP.Application")
            .Should().NotDependOnAnyTypesThat().Are(ProviderPackageTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Persistence_outside_Providers_should_not_call_UseSqlServer_or_UseNpgsql()
    {
        // El único punto autorizado a invocar UseSqlServer/UseNpgsql es
        // Persistence/Providers (más las design-time factories, que viven en
        // los ensamblados de migraciones y no participan de este grafo).
        Types().That()
            .ResideInNamespace("IngenIA365ERP.Persistence")
            .And().DoNotResideInNamespace("IngenIA365ERP.Persistence.Providers")
            .Should().NotDependOnAnyTypesThat().Are(ProviderPackageTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Identity_should_not_reference_provider_packages()
    {
        // Feature 004 retiró Microsoft.EntityFrameworkCore.SqlServer de
        // IngenIA365ERP.Identity: ErpIdentityDbContext se configura vía
        // IDbProviderConfigurator.
        Types().That()
            .ResideInNamespace("IngenIA365ERP.Identity")
            .Should().NotDependOnAnyTypesThat().Are(ProviderPackageTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}
