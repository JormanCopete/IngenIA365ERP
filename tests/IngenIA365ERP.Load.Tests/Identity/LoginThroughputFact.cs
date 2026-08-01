using NBomber.CSharp;
using Xunit;

namespace IngenIA365ERP.Load.Tests.Identity;

/// <summary>
/// Wrapper xUnit del <see cref="LoginThroughputScenario"/>. Sin
/// <c>RUN_LOAD_TESTS=1</c> es un no-op instantáneo (el load test necesita un
/// entorno corriendo); con la env var exportada ejecuta la corrida real:
/// <code>
/// RUN_LOAD_TESTS=1 LOADTEST_BASE_URL=http://localhost:5100 \
/// dotnet test tests/IngenIA365ERP.Load.Tests --filter LoginThroughput
/// </code>
/// (El atributo Skip anterior era incondicional y la env var jamás se
/// evaluaba — el test no se podía ejecutar de ninguna forma.)
/// </summary>
public sealed class LoginThroughputFact
{
    [Fact]
    public void Runs_within_aceptance_thresholds()
    {
        if (Environment.GetEnvironmentVariable("RUN_LOAD_TESTS") != "1")
        {
            return; // No-op sin opt-in explícito.
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
