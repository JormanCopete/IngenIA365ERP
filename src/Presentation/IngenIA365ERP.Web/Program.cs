using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Mock;
using IngenIA365ERP.Shared.Configuration;
using IngenIA365ERP.Web.Components;
using IngenIA365ERP.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Syncfusion.Blazor;

// Register SyncFusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXted3VSRGhZWENzWEZWYEo=");

var builder = WebApplication.CreateBuilder(args);

// Read AppMode from appsettings(.{Environment}).json
AppMode.Configure(builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Add SyncFusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Add device-specific services used by the IngenIA365ERP.Shared project.
// SecureStorage + TenantService are Singletons because IHttpClientFactory creates
// its own DI scope for DelegatingHandlers; Scoped here would give the handlers
// a different in-memory Dictionary than the page populated.
// NOTE: For multi-user production on Blazor Server, replace WebSecureStorage with
// a circuit-bound implementation (e.g. ProtectedSessionStorage) to avoid leaking
// state across users.
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<ISecureStorage, WebSecureStorage>();
builder.Services.AddSingleton<ITenantService, TenantService>();

// HTTP message handlers — every request gets X-Tenant-Id and Authorization Bearer.
builder.Services.AddTransient<AuthBearerHandler>();
builder.Services.AddTransient<TenantDelegatingHandler>();

// Named HttpClient used by all pages/services.
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(AppMode.ApiBaseUrl))
    .AddHttpMessageHandler<AuthBearerHandler>()
    .AddHttpMessageHandler<TenantDelegatingHandler>();
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

// Add authentication services
if (AppMode.UseMock)
    builder.Services.AddScoped<IAuthService, MockAuthService>();
else
    builder.Services.AddScoped<IAuthService, AuthService>();
Console.WriteLine($"{AppMode.Tag} AuthService listo · ApiBaseUrl={AppMode.ApiBaseUrl}");

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILoadingService, LoadingService>();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// Add authentication with a dummy scheme to satisfy ASP.NET Core requirements
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.Name = "IngenIA365ERP.Auth";
    });

// Add authorization services
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(IngenIA365ERP.Shared._Imports).Assembly,
        typeof(IngenIA365ERP.Web.Client._Imports).Assembly);

app.Run();
