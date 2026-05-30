using IngenIA365ERP.Application.Common.Behaviors;
using MediatR;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio X — Trazabilidad SARLAFT: todo Command de Application pasa
/// por <see cref="AuditBehavior{TRequest, TResponse}"/>.
///
/// El AuditBehavior está registrado globalmente como
/// <c>IPipelineBehavior&lt;,&gt;</c> en <see cref="IngenIA365ERP.Application.DependencyInjection.AddApplicationServices"/>.
/// El check verifica estructuralmente que:
///  1) El tipo <see cref="AuditBehavior{TRequest, TResponse}"/> existe.
///  2) El método <c>AddApplicationServices</c> lo registra.
///
/// Una eliminación accidental del registro hace que este test se ponga
/// rojo. (La cobertura por handler concreto la valida T080 en US3.)
/// </summary>
public class PrincipioX_AuditMandatory
{
    [Fact]
    public void AuditBehavior_type_exists_and_is_a_pipeline_behavior()
    {
        var open = typeof(AuditBehavior<,>);
        Assert.True(open is not null);
        var i = open.GetInterfaces();
        Assert.Contains(i, t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void Application_DependencyInjection_source_registers_AuditBehavior()
    {
        var sourcePath = System.IO.Path.Combine(
            Helpers.RepoPath.FindRepoRoot(),
            "src", "Core", "IngenIA365ERP.Application", "DependencyInjection.cs");
        var text = System.IO.File.ReadAllText(sourcePath);
        Assert.Contains("typeof(AuditBehavior<,>)", text);
    }
}
