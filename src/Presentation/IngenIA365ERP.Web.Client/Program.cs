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
// Feature 003 (US4, FR-113): sessionStorage-backed — la sesión sobrevive a F5
// con alcance solo-pestaña. Reemplaza el diccionario en memoria (WebAssemblySecureStorage).
builder.Services.AddSingleton<ISecureStorage, BrowserSessionSecureStorage>();
builder.Services.AddSingleton<ITenantService, TenantService>();

// HTTP message handlers — every request gets X-Tenant-Id and Authorization Bearer.
builder.Services.AddTransient<AuthBearerHandler>();
builder.Services.AddTransient<TenantDelegatingHandler>();

// Named HttpClient used by all pages/services. Pages get this via the default
// HttpClient injection below.
//
// Un ApiBaseUrl RELATIVO ("/") se resuelve contra el origen desde el que se
// sirvió la aplicación, y ésa es la única forma de que esto funcione desplegado:
// wwwroot/appsettings.json es un archivo ESTÁTICO que descarga el navegador, así
// que ninguna variable de entorno lo pisa. El despliegue no puede corregirlo sin
// reconstruir el wwwroot — y mientras no se corrigió, producción sirvió
// "http://localhost:5100", o sea la máquina de quien abría la página.
//
// Todas las rutas que piden los clientes empiezan por "/api/...", de modo que lo
// único que hay que acertar aquí es el ORIGEN. Un valor absoluto se sigue
// respetando tal cual: es lo que necesitan desarrollo (API en otro puerto) y
// cualquier ambiente donde la API no comparta host.
var origenDelNavegador = builder.HostEnvironment.BaseAddress;
var apiBaseUrl = AppMode.UseMock
    ? origenDelNavegador
    : Uri.TryCreate(AppMode.ApiBaseUrl, UriKind.Absolute, out var apiAbsoluta)
        ? apiAbsoluta.ToString()
        : new Uri(new Uri(origenDelNavegador), AppMode.ApiBaseUrl).ToString();
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

// Feature 003 (US1) — estado de formularios sucios para la guardia del
// TenantSwitcher (FR-103).
builder.Services.AddScoped<IFormDirtyStateService, InMemoryFormDirtyStateService>();

// Feature 002 (US1) — cliente del módulo de invitaciones consumido por
// AcceptInvitation.razor. Usa el HttpClient 'api' configurado arriba.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.InvitationClient>();
// Feature 002 (US2) — cliente del flujo de autenticación central
// consumido por Login.razor + MfaChallenge.razor + SelectTenant.razor.
// Scoped para mantener tokens en memoria por sesión WASM.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.CentralAuthClient>();
// Phase 4b — cliente del módulo de perfil y recuperación (MFA enrollment,
// change password, forgot/reset). Reusa CentralAuthClient para resolver
// qué token enviar (access full o challenge mfa-enroll).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.ProfileClient>();
// Puente con navigator.credentials (passkeys). Va en los dos hosts porque el
// prerender del servidor instancia las mismas pantallas; ahí devuelve
// «no disponible» y el botón aparece recién en el primer render interactivo.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.WebAuthnInterop>();
// Feature 003 (US6) — cliente Fase 0 per-tenant que usa la consola de
// aprobaciones de MFA reset (request/approve/list). Nunca estuvo registrado
// y la página crasheaba el runtime WASM al inyectarlo.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.MfaResetClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.PreferenciasClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.PromocionesClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.ParametrosClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.CooperativasClient>();
// US3 — cliente del módulo de sesiones (active-tenants, switch, default).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.TenantSessionClient>();
// US4 — cliente de gestión de membresías y política MFA.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.MembershipsClient>();
// US5 — cliente master admin (register tenant + admin, force MFA reset).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.SaasAdminClient>();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
