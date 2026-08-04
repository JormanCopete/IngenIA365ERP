using System.Runtime.CompilerServices;

namespace IngenIA365ERP.API.IntegrationTests.Infrastructure;

/// <summary>
/// Feature 004: los tests que crean un WebApplicationFactory "pelado" (sin
/// fixture) heredan el ambiente del proceso — sin esto el host corre como
/// Production y el fail-fast de RsaKeyGuard (claves RS256) + la validacion de
/// la seccion Database tumban el arranque. Todo el assembly de tests corre
/// como Development.
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Init()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
    }
}
