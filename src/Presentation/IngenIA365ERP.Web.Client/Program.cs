using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Mock;
using IngenIA365ERP.Shared.Configuration;
using IngenIA365ERP.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register SyncFusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXted3VSRGhZWENzWEZWYEo=");

// Add SyncFusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Read AppMode from wwwroot/appsettings(.{Environment}).json
AppMode.Configure(builder.Configuration);

// Add device-specific services used by the IngenIA365ERP.Shared project.
// SecureStorage + TenantService are Singletons because IHttpClientFactory creates
// its own DI scope when resolving DelegatingHandlers; if these were Scoped the
// handlers would see a different in-memory Dictionary than the page does.
// In WASM there is one user per app, so Singleton is safe.
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<ISecureStorage, WebAssemblySecureStorage>();
builder.Services.AddSingleton<ITenantService, TenantService>();

// HTTP message handlers — every request gets X-Tenant-Id and Authorization Bearer.
builder.Services.AddTransient<AuthBearerHandler>();
builder.Services.AddTransient<TenantDelegatingHandler>();

// Named HttpClient used by all pages/services. Pages get this via the default
// HttpClient injection below.
var apiBaseUrl = AppMode.UseMock ? builder.HostEnvironment.BaseAddress : AppMode.ApiBaseUrl;
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthBearerHandler>()
    .AddHttpMessageHandler<TenantDelegatingHandler>();
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

// Add authentication services
if (AppMode.UseMock)
    builder.Services.AddScoped<IAuthService, MockAuthService>();
else
    builder.Services.AddScoped<IAuthService, AuthService>();
Console.WriteLine($"{AppMode.Tag} AuthService listo · ApiBaseUrl={apiBaseUrl}");

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILoadingService, LoadingService>();

// Feature 002 (US1) — cliente del módulo de invitaciones consumido por
// AcceptInvitation.razor. Usa el HttpClient 'api' configurado arriba.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.InvitationClient>();
// Feature 002 (US2) — cliente del flujo de autenticación central
// consumido por Login.razor + MfaChallenge.razor + SelectTenant.razor.
// Scoped para mantener tokens en memoria por sesión WASM.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.CentralAuthClient>();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
