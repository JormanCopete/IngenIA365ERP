using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests;

public class CleanArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.DependencyInjection).Assembly)
        .Build();

    private readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("IngenIA365ERP.Domain").As("Domain Layer");

    private readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespace("IngenIA365ERP.Application").As("Application Layer");

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer);
        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Persistence()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("IngenIA365ERP.Persistence");
        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Identity()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespace("IngenIA365ERP.Identity");
        rule.Check(Architecture);
    }
}
