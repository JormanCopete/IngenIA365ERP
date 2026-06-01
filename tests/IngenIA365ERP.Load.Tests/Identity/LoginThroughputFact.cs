using NBomber.CSharp;
using Xunit;

namespace IngenIA365ERP.Load.Tests.Identity;

/// <summary>
/// Wrapper xUnit del <see cref="LoginThroughputScenario"/>. Marcado como
/// <c>Skip</c> por defecto — el load test requiere un entorno corriendo y
/// se invoca explícitamente con:
/// <code>
/// dotnet test --filter "FullyQualifiedName~LoginThroughput" --no-build
/// </code>
/// removiendo el SkipReason o exportando <c>RUN_LOAD_TESTS=1</c>.
/// </summary>
public sealed class LoginThroughputFact
{
    [Fact(Skip = "Load test — activar con RUN_LOAD_TESTS=1 + entorno corriendo.")]
    public void Runs_within_aceptance_thresholds()
    {
        if (Environment.GetEnvironmentVariable("RUN_LOAD_TESTS") != "1")
        {
            return; // Defensa adicional por si Skip se ignora.
        }

        var stats = NBomberRunner
            .RegisterScenarios(LoginThroughputScenario.Build())
            .WithReportFolder("nbomber-reports/identity")
            .Run();

        // Validaciones de aceptación.
        foreach (var scenario in stats.ScenarioStats)
        {
            Assert.True(scenario.Fail.Request.Percent < 1.0,
                $"Fail rate > 1%: {scenario.Fail.Request.Percent}%");
            Assert.True(scenario.Ok.Latency.Percent95 < 800,
                $"p95 > 800ms: {scenario.Ok.Latency.Percent95}ms");
        }
    }
}
