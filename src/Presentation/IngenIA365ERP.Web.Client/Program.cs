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
//
// El origen va como segundo argumento porque la base puede venir RELATIVA ("/"),
// y resolverla es responsabilidad de AppMode: asi lo que circula por la
// aplicacion —incluido AppSettings.GetApiBaseUrl(), que concatenan 19
// pantallas— ya es absoluto. Ver AppMode.ResolverBase.
AppMode.Configure(builder.Configuration, builder.HostEnvironment.BaseAddress);

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
// Renovación silenciosa (RenovadorDeSesion): singleton por la misma razón que
// ISecureStorage —el handler y las pantallas tienen que ver la misma sesión— y
// su handler va PRIMERO en la cadena, para que AuthBearerHandler encuentre la
// cabecera ya puesta con un token que no está por vencer.
builder.Services.AddSingleton<IngenIA365ERP.Shared.Services.Security.RenovadorDeSesion>();
builder.Services.AddTransient<RenovacionDeSesionHandler>();
// Feature 012 (T36): X-Canal para la auditoria; va despues de la renovacion y no toca Authorization.
builder.Services.AddTransient(_ => new IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler(IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler.Web));
builder.Services.AddTransient<AuthBearerHandler>();
builder.Services.AddTransient<TenantDelegatingHandler>();

// Named HttpClient used by all pages/services. Pages get this via the default
// HttpClient injection below.
//
// AppMode.ApiBaseUrl YA viene resuelto y absoluto: la resolución vive allí y no
// aquí, porque no es sólo este HttpClient quien la necesita —19 pantallas
// concatenan AppSettings.GetApiBaseUrl()— y tener dos copias de esa lógica fue
// justamente el defecto: una se arregló y la otra siguió rota.
var apiBaseUrl = AppMode.UseMock ? builder.HostEnvironment.BaseAddress : AppMode.ApiBaseUrl;
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<RenovacionDeSesionHandler>()
    .AddHttpMessageHandler<IngenIA365ERP.Shared.Services.Http.CanalDeOrigenHandler>()
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
// Feature 005 — cliente tipado del módulo de nómina (planes, períodos, novedades, liquidación).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Nomina.NominaClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Nomina.DescargaDeArchivos>();
// Feature 008 — cliente tipado del maestro de personas (Personas, Empleados y Asociados).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Core.PersonasClient>();
// Feature 012 (T170): catalogo tributario de Core (impuestos, tarifas, conceptos de retencion).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Core.ImpuestosClient>();
// Feature 012 (T183): cliente tipado de Inventario (documentos, tipos, plantillas, informes; cada historia suma su parcial).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Inventario.InventarioClient>();
// Feature 009: cliente tipado de contabilidad (mismo molde que NominaClient).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Contabilidad.ContabilidadClient>();
// Feature 011: adjuntos (subida y descarga directas al almacén; el archivo no pasa por .NET).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Adjuntos.AdjuntosClient>();
// Feature 008 — permisos efectivos del usuario en la cooperativa activa (PermissionGate).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.PermisosDelUsuario>();
// Feature 009 (FR-051): el ingreso a cada opción queda en la auditoría (cola con reintentos; nunca frena la pantalla).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Auditoria.RegistroDeAccesos>();
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
