using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// El chequeo <c>rabbitmq</c> (feature 023; research R9). Sin broker la API sigue vendiendo (el barrido hace el trabajo), así que responde
/// <see cref="HealthStatus.Degraded"/> —HTTP 200 en <c>/health/ready</c>— y nunca saca la réplica del Service. Lo que mira es el estado de
/// la conexión que ya mantiene <see cref="ConexionRabbitMq"/>: no abre nada por sí mismo. (nuevo)
/// </summary>
public sealed class RabbitMqHealthCheck(ConexionRabbitMq conexion) : IHealthCheck
{
    public const string Nombre = "rabbitmq";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(conexion.Abierta
            ? HealthCheckResult.Healthy("Conectado a RabbitMQ.")
            : HealthCheckResult.Degraded("Sin conexión a RabbitMQ: el barrido emite cada pocos segundos; nada se pierde."));
}
