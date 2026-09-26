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

// Named HttpClient used by all pages/services.
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(AppMode.ApiBaseUrl))
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
Console.WriteLine($"{AppMode.Tag} AuthService listo · ApiBaseUrl={AppMode.ApiBaseUrl}");

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILoadingService, LoadingService>();

// Feature 003 (US1) — estado de formularios sucios para la guardia del
// TenantSwitcher (FR-103).
builder.Services.AddScoped<IFormDirtyStateService, InMemoryFormDirtyStateService>();

// Feature 002 — clientes de identidad central consumidos por las páginas de
// IngenIA365ERP.Shared. El host server los necesita igual que el WASM
// (Web.Client/Program.cs) porque el prerender instancia los componentes acá.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.InvitationClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.CentralAuthClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.ProfileClient>();
// Puente con navigator.credentials (passkeys). Va en los dos hosts porque el
// prerender del servidor instancia las mismas pantallas; ahí devuelve
// «no disponible» y el botón aparece recién en el primer render interactivo.
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.WebAuthnInterop>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.TenantSessionClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.MembershipsClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.SaasAdminClient>();
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
// Feature 012, T429: la verificación de integridad de la auditoría (pestaña «Integridad» de la consola).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Auditoria.IntegridadDeAuditoriaClient>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Compras.ComprasClient>();
// Feature 009: cliente tipado de contabilidad (mismo molde que NominaClient).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Contabilidad.ContabilidadClient>();
// Feature 011: adjuntos (subida y descarga directas al almacén; el archivo no pasa por .NET).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Adjuntos.AdjuntosClient>();
// Feature 008 — permisos efectivos del usuario en la cooperativa activa (PermissionGate).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.PermisosDelUsuario>();
// Feature 009 (FR-051): el ingreso a cada opción queda en la auditoría (cola con reintentos; nunca frena la pantalla).
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Auditoria.RegistroDeAccesos>();
builder.Services.AddScoped<IngenIA365ERP.Shared.Services.Security.CooperativasClient>();

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

// Sondas de Kubernetes. Sin esto pegaban a "/", y "/" prerenderiza el shell de
// Blazor (layout, AuthorizeView, componentes) cada 10-20 s por réplica, sólo para
// contestar «vivo». Sin chequeos registrados el endpoint responde 200 «Healthy»
// si el proceso atiende peticiones, que es exactamente lo que la sonda pregunta:
// este host no tiene base de datos ni dependencias propias que verificar (la API
// las tiene, y las sondea la API).
builder.Services.AddHealthChecks();

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

// /health para las sondas (ver AddHealthChecks arriba). El pipeline no tiene
// UseAuthorization ni política de respaldo, así que hoy nada lo bloquearía;
// AllowAnonymous queda declarado igual, como en MapRazorComponents, para que el
// día que alguien agregue una política global la sonda no empiece a fallar en
// silencio. La ruta literal gana al comodín de MapRazorComponents sin importar
// el orden: el enrutador elige por precedencia, no por registro.
app.MapHealthChecks("/health").AllowAnonymous();

// Feature 003 (US4, FR-113): la sesión vive SOLO en el cliente (JWT central en
// sessionStorage) — el servidor no puede conocerla. Sin AllowAnonymous, el
// [Authorize] de las páginas propaga el challenge de la cookie y un F5 sobre
// una ruta protegida responde 302 → /login antes de que el WASM hidrate la
// sesión. El shell se sirve anónimo; AuthorizeRouteView + RedirectToLogin
// (Shared/Routes.razor) siguen protegiendo las rutas en el cliente.
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(IngenIA365ERP.Shared._Imports).Assembly,
        typeof(IngenIA365ERP.Web.Client._Imports).Assembly)
    .AllowAnonymous();

app.Run();
