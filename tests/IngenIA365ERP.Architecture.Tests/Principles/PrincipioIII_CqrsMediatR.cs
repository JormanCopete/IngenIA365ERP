using MediatR;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio III — CQRS con MediatR: todo Command/Query <strong>en su
/// namespace canónico</strong> (<c>.Commands.</c>/<c>.Queries.</c>) implementa
/// <see cref="IRequest"/> o <see cref="IRequest{TResponse}"/>. Esto evita
/// que un Command se ejecute fuera del pipeline (saltándose
/// Validation/Logging/Audit/Performance).
///
/// El test ignora records cuyo nombre coincidentalmente termina en
/// "Command"/"Query" pero viven en <c>Common.Interfaces</c> (son DTOs de
/// servicios de infraestructura, no requests MediatR).
/// </summary>
public class PrincipioIII_CqrsMediatR
{
    [Fact]
    public void Every_command_in_canonical_namespace_implements_IRequest()
    {
        var asm = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        var offenders = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => t.Namespace?.Contains(".Commands.", StringComparison.Ordinal) == true)
            .Where(t => !ImplementsAnyMediatRRequest(t))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Commands en namespace canónico que NO implementan IRequest:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_query_in_canonical_namespace_implements_IRequest()
    {
        var asm = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        var offenders = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal))
            .Where(t => t.Namespace?.Contains(".Queries", StringComparison.Ordinal) == true)
            .Where(t => !ImplementsAnyMediatRRequest(t))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Queries en namespace canónico que NO implementan IRequest:\n  "
            + string.Join("\n  ", offenders));
    }

    private static bool ImplementsAnyMediatRRequest(Type t)
    {
        return t.GetInterfaces().Any(i =>
            i == typeof(IRequest)
            || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotification)));
    }
}
