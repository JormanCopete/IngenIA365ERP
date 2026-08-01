using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Reglas arquitectónicas específicas de la feature 002-identidad-central-federada
/// (T049 + T050). Refuerzan el principio constitucional II en el módulo de
/// identidad central:
/// <list type="bullet">
///   <item>ASP.NET Core Identity (UserManager, SignInManager, IdentityUser) está
///         encapsulado en <c>IngenIA365ERP.Identity</c>; ningún tipo en Application
///         o Domain lo referencia directamente — todos pasan por
///         <c>ICentralIdentityProvider</c>.</item>
///   <item><c>AdminDbContext</c> concreto vive en Persistence; ningún tipo en
///         Application lo conoce — los handlers usan <c>IAdminDbContext</c>.</item>
///   <item><c>CentralUserIdentity</c> (bridge IdentityUser&lt;Guid&gt;) está en
///         Persistence; ningún DTO o entity de Domain debe exponerlo.</item>
/// </list>
/// </summary>
public class Feature002_CentralIdentity
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly,
            typeof(IngenIA365ERP.Persistence.DbContext.AdminDbContext).Assembly,
            typeof(IngenIA365ERP.Identity.CentralIdentity.CentralJwtIssuer).Assembly)
        .Build();

    private readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("IngenIA365ERP.Domain")
               .As("Domain Layer");

    private readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespace("IngenIA365ERP.Application")
               .As("Application Layer");

    // ----------------------------------------------------------------
    // T049 — ICentralIdentityProvider es la única puerta a ASP.NET Identity
    // ----------------------------------------------------------------

    [Fact]
    public void Application_should_not_reference_AspNetCore_Identity_UserManager()
    {
        // UserManager<>, SignInManager<>, IdentityUser<>, etc. son detalles de framework
        // que solo pueden tocarse desde IngenIA365ERP.Identity. Application los inyecta
        // EXCLUSIVAMENTE vía ICentralIdentityProvider.
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore.Identity")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_reference_AspNetCore_Identity()
    {
        // Refuerza principio II en lo específico al módulo Identity (la prueba
        // genérica en PrincipioII_CleanArchitecture cubre Microsoft.AspNetCore en
        // general; aquí explicitamos el sub-namespace Identity por documentación).
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore.Identity")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    // ----------------------------------------------------------------
    // T050 — AdminDbContext concreto solo desde Persistence
    // ----------------------------------------------------------------

    [Fact]
    public void Application_should_not_reference_concrete_AdminDbContext()
    {
        // Los handlers de Application inyectan IAdminDbContext (abstracción en
        // Common.Interfaces), NO la clase concreta AdminDbContext de Persistence.
        // Si alguien lo referencia directamente, este test se pone rojo.
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAnyTypesThat()
            .HaveFullName("IngenIA365ERP.Persistence.DbContext.AdminDbContext")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_reference_CentralUserIdentity_bridge()
    {
        // CentralUserIdentity es un tipo de Infrastructure (bridge IdentityUser<Guid>).
        // Domain solo conoce el POCO CentralUser. Si Domain referencia el bridge,
        // colapsa la separación y los Domain tests dependerían de ASP.NET Identity.
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .HaveFullName("IngenIA365ERP.Persistence.Identity.CentralUserIdentity")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Application_should_not_reference_CentralUserIdentity_bridge()
    {
        // Misma regla para Application: CentralUserIdentity es detalle de Infrastructure.
        // Los handlers que necesitan datos del usuario central reciben CentralUser POCO
        // a través de ICentralIdentityProvider.FindByIdAsync / FindByEmailAsync.
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAnyTypesThat()
            .HaveFullName("IngenIA365ERP.Persistence.Identity.CentralUserIdentity")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}
