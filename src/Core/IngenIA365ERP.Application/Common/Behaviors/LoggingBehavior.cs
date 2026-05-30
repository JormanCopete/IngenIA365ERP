using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Envuelve cada request en un <c>BeginScope</c> con las claves
/// <c>TenantId</c>, <c>UserId</c>, <c>Operation</c>. Serilog renderiza ese
/// scope en cada línea emitida desde dentro del handler (incluyendo los
/// logs del propio EF Core), lo que cumple FR-049 / SC-006 (trazabilidad
/// por operación) sin requerir que cada handler los pase a mano.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var scope = new Dictionary<string, object?>
        {
            ["Operation"] = requestName,
            ["TenantId"] = currentTenant.TenantId,
            ["UserId"] = currentUser.UserId,
            ["UserName"] = currentUser.UserName
        };

        using (logger.BeginScope(scope))
        {
            logger.LogInformation("Handling {RequestName}", requestName);
            try
            {
                var response = await next(cancellationToken);
                logger.LogInformation("Handled {RequestName}", requestName);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in {RequestName}", requestName);
                throw;
            }
        }
    }
}
