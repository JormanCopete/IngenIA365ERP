using System.Reflection;
using IngenIA365ERP.App.Services;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Mock;
using IngenIA365ERP.Shared.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Blazor;

namespace IngenIA365ERP.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Register SyncFusion license
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("LICENSE_KEY_PLACEHOLDER");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Load embedded appsettings(.Development).json into the configuration root
            var configBuilder = new ConfigurationBuilder();
            AddEmbeddedJson(configBuilder, "appsettings.json", optional: false);
#if DEBUG
            AddEmbeddedJson(configBuilder, "appsettings.Development.json", optional: true);
#endif
            builder.Configuration.AddConfiguration(configBuilder.Build());

            // Apply AppMode (Environment / DataSource / ApiBaseUrl)
            AppMode.Configure(builder.Configuration);

            // Add SyncFusion Blazor services
            builder.Services.AddSyncfusionBlazor();

            // Add device-specific services used by the IngenIA365ERP.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<Shared.Services.ISecureStorage, SecureStorageService>();
            builder.Services.AddSingleton<ITenantService, TenantService>();

            // HTTP message handlers — every request gets X-Tenant-Id and Authorization Bearer.
            // Renovación silenciosa: ver la nota en Web.Client/Program.cs.
            builder.Services.AddSingleton<Shared.Services.Security.RenovadorDeSesion>();
            builder.Services.AddTransient<RenovacionDeSesionHandler>();
            // Feature 012 (T36): X-Canal para la auditoria; va despues de la renovacion y no toca Authorization.
            builder.Services.AddTransient(_ => new IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler(IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler.App));
            builder.Services.AddTransient<AuthBearerHandler>();
            builder.Services.AddTransient<TenantDelegatingHandler>();

            // Named HttpClient used by all pages/services.
            builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(AppMode.ApiBaseUrl))
                .AddHttpMessageHandler<RenovacionDeSesionHandler>()
                .AddHttpMessageHandler<IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler>()
                .AddHttpMessageHandler<AuthBearerHandler>()
                .AddHttpMessageHandler<TenantDelegatingHandler>();
            builder.Services.AddSingleton(sp =>
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

            // Add authentication services
            if (AppMode.UseMock)
                builder.Services.AddSingleton<IAuthService, MockAuthService>();
            else
                builder.Services.AddSingleton<IAuthService, AuthService>();
            System.Diagnostics.Debug.WriteLine($"{AppMode.Tag} AuthService listo · ApiBaseUrl={AppMode.ApiBaseUrl}");

            // Feature 012 (T170): cliente del catalogo tributario de Core. Como los demas clientes tipados de Shared,
            // depende de CentralAuthClient, que la app MAUI todavia no registra (verificacion de MAUI pendiente).
            builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Core.ImpuestosClient>();
            // Feature 012 (T183): cliente de Inventario; misma salvedad que el anterior (CentralAuthClient, verificacion de MAUI).
            builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Inventario.InventarioClient>();
            builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Compras.ComprasClient>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<ILoadingService, LoadingService>();

            // Feature 003 (US1) — estado de formularios sucios (guardia del
            // TenantSwitcher, FR-103). Singleton: MAUI es mono-usuario.
            builder.Services.AddSingleton<IFormDirtyStateService, InMemoryFormDirtyStateService>();

            builder.Services.AddSingleton<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddAuthorizationCore();

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static void AddEmbeddedJson(IConfigurationBuilder cfg, string fileName, bool optional)
        {
            var assembly = typeof(MauiProgram).Assembly;
            var resourceName = $"{assembly.GetName().Name}.{fileName}";
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                if (!optional)
                    throw new FileNotFoundException($"Embedded config not found: {resourceName}");
                return;
            }
            cfg.AddJsonStream(stream);
        }
    }
}
