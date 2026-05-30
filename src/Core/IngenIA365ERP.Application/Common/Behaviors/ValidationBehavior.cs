using System.Reflection;
using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Ejecuta todos los <see cref="IValidator{TRequest}"/> registrados. Si el
/// handler devuelve <see cref="Result"/> o <see cref="Result{T}"/>, los
/// fallos se traducen a <c>Result.Failure("Validation.Invalid", msg)</c>
/// con el detalle combinado de las reglas violadas. Para handlers que no
/// usan Result, se lanza <see cref="ValidationException"/> (compatibilidad).
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var combined = string.Join("; ", failures.Select(f => f.ErrorMessage));

        // Si TResponse es Result o Result<T>, devolvemos un fallo del propio Result
        // — el pipeline mantiene la semántica funcional y el endpoint puede
        // mapearlo via ErrorEnvelopeFilter sin necesidad de try/catch.
        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure("Validation.Invalid", combined);
        }
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = responseType.GetGenericArguments()[0];
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m =>
                    m.Name == nameof(Result.Failure)
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .MakeGenericMethod(inner);
            return (TResponse)method.Invoke(null, ["Validation.Invalid", combined])!;
        }

        throw new ValidationException(failures);
    }
}
