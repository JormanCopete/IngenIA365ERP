using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio II — Clean Architecture: <c>Domain</c> es la capa más interna
/// y no conoce ASP.NET Core, EF Core, MongoDB ni Carter.
/// <c>Application</c> conoce <c>Domain</c> y sus propias abstracciones,
/// pero no implementa nada de infraestructura.
///
/// Cada regla negativa cierra con <c>WithoutRequiringPositiveResults()</c>:
/// la ausencia de violaciones (caso "verde") es el resultado esperado,
/// y ArchUnitNET por defecto trata "nadie hizo X" como inconcluyente.
/// </summary>
public class PrincipioII_CleanArchitecture
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build();

    private readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("IngenIA365ERP.Domain").As("Domain Layer");

    [Fact]
    public void Domain_should_not_depend_on_Application()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("IngenIA365ERP.Application")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_Persistence()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("IngenIA365ERP.Persistence")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_Identity()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("IngenIA365ERP.Identity")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.EntityFrameworkCore")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_AspNetCore()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_MongoDB()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("MongoDB")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_Carter()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("Carter")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}
