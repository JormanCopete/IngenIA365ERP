using Microsoft.Extensions.Configuration;

namespace IngenIA365ERP.Shared.Configuration;

public enum AppEnvironment { Development, QA, Production }
public enum AppDataSource { Mock, Api }

public static class AppMode
{
    public static AppEnvironment Environment { get; private set; } = AppEnvironment.Development;
    public static AppDataSource DataSource { get; private set; } = AppDataSource.Mock;
    public static string ApiBaseUrl { get; private set; } = "http://localhost:5100";

    public static bool UseMock => DataSource == AppDataSource.Mock;
    public static string Tag => $"[{Environment}][{DataSource}]";

    public static void Configure(IConfiguration configuration)
    {
        var section = configuration.GetSection("AppMode");
        if (Enum.TryParse<AppEnvironment>(section["Environment"], true, out var env))
            Environment = env;
        if (Enum.TryParse<AppDataSource>(section["DataSource"], true, out var ds))
            DataSource = ds;
        var apiBaseUrl = section["ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            ApiBaseUrl = apiBaseUrl;

        // Sync legacy flag for code paths that still read AppSettings.UseMockServices
        AppSettings.UseMockServices = UseMock;
        AppSettings.ApiBaseUrl = ApiBaseUrl;
        AppSettings.LocalApiBaseUrl = ApiBaseUrl;
    }
}
