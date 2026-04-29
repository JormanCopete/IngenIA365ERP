using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();
        var response = await next(cancellationToken);
        _timer.Stop();

        var elapsedMs = _timer.ElapsedMilliseconds;
        if (elapsedMs > 500)
        {
            var requestName = typeof(TRequest).Name;
            logger.LogWarning(
                "Long Running Request: {Name} ({ElapsedMilliseconds}ms)",
                requestName, elapsedMs);
        }

        return response;
    }
}
