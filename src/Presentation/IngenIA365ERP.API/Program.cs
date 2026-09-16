using Microsoft.AspNetCore.Diagnostics;
using AspNetCoreRateLimit;
using Carter;
using IngenIA365ERP.API.Middleware;
using IngenIA365ERP.API.Middleware.CentralIdentity;
using IngenIA365ERP.API.Services;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application;
using IngenIA365ERP.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Caching.Services;
using IngenIA365ERP.Identity;
using IngenIA365ERP.Identity.CentralIdentity;
using IngenIA365ERP.Identity.Seed;
using IngenIA365ERP.Persistence;
using IngenIA365ERP.Storage;
using IngenIA365ERP.Caching;
using IngenIA365ERP.API.Hubs;
using IngenIA365ERP.API.HealthChecks;
using Microsoft.OpenApi;  // En OpenApi 2.x los tipos se movieron de Microsoft.OpenApi.Models a la raiz Microsoft.OpenApi
using Serilog;
using IngenIA365ERP.Persistence.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

// Logger de arranque: solo consola, y solo hasta que el host lea la configuracion.
// CreateBootstrapLogger deja un logger intercambiable que UseSerilog reemplaza
// abajo; lo que se escribe antes (el «Starting…», un fallo de configuracion)
// no se pierde y no queda un segundo logger con sus propios sinks abiertos.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting IngenIA365ERP API");

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);

    // === Infraestructura de la maquina (local / docker / wsl) ===
    // Se agregan DESPUES de CreateBuilder para que ganen sobre appsettings, y
    // se vuelven a poner las variables de entorno encima al final: en un
    // cluster la configuracion llega por variables y un archivo del
    // repositorio no puede pisarla.
    builder.Configuration.AgregarInfraestructuraDeLaMaquina(builder.Environment.EnvironmentName);
    var destinosResueltos = InfraestructuraConfiguracion.Resolver(builder.Configuration);
    if (destinosResueltos.Count > 0)
    {
        builder.Configuration.AddInMemoryCollection(destinosResueltos);
    }
    builder.Configuration.AddEnvironmentVariables();

    // Serilog: niveles desde la configuracion, sinks en codigo y asincronos.
    //
    // Hasta el 2026-09-12 esto construia el logger solo con WriteTo.Console +
    // WriteTo.File + Enrich, sin ReadFrom.Configuration. La seccion Serilog de
    // appsettings (Override Microsoft → Warning) era letra muerta, y la seccion
    // «Logging» no filtra nada cuando manda Serilog, porque UseSerilog sustituye
    // la ILoggerFactory entera. Resultado medido en produccion: cada peticion y
    // cada DbCommand de EF Core —con el SQL— salian en Information, a consola y a
    // archivo, por dos sinks sincronicos que escriben en el hilo de la peticion.
    //
    // Ahora los niveles los dice appsettings (y el del ambiente, y las variables
    // Serilog__*), los sinks se quedan aca para no tener que declarar «Using» en
    // JSON, y los envuelve WriteTo.Async: una cola y un hilo de fondo; con
    // blockWhenFull el hilo de la peticion espera en vez de perder eventos.
    // El archivo en logs/ solo tiene sentido fuera del contenedor: adentro es el
    // disco efimero del pod, que nadie lee y muere con el; ahi la consola es el
    // unico camino y la recoge la plataforma. DOTNET_RUNNING_IN_CONTAINER=true lo
    // pone el Dockerfile.
    //
    // T123 — el enricher pone central_user_id + active_tenant_id desde los claims
    // del JWT; se rehidrata desde DI para tener IHttpContextAccessor.
    var enContenedor = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.With(new IngenIA365ERP.API.Logging.CentralIdentityLogEnricher(
            services.GetRequiredService<IHttpContextAccessor>()))
        .WriteTo.Async(sinks =>
        {
            sinks.Console();
            if (!enContenedor)
            {
                sinks.File("logs/ingenia365erp-.log", rollingInterval: RollingInterval.Day);
            }
        }, blockWhenFull: true));

    // Add services to the container
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "IngenIA365ERP API",
            Version = "v1",
            Description = "API para el ERP Financiero IngenIA365ERP"
        });

        // JWT Bearer in Swagger.
        // Microsoft.OpenApi 2.x removio OpenApiSecurityScheme.Reference; ahora se usa
        // OpenApiSecuritySchemeReference y AddSecurityRequirement con delegate (document =>).
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Ejemplo: 'Bearer {token}'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            // El value type del diccionario en OpenApi 2.x es List<string>, no string[].
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });
    });

    // Carter for Minimal API endpoints
    builder.Services.AddCarter();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    "https://localhost:7200",
                    "http://localhost:5200",
                    "https://localhost:7052")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // HttpContext accessor for tenant resolution
    builder.Services.AddHttpContextAccessor();
    // Singleton: consumed by Singleton MongoAuditService — IHttpContextAccessor handles per-request context.
    builder.Services.AddSingleton<ICurrentTenantService, TenantContextAccessor>();

    // === Cross-cutting services consumed by Identity & Audit ===
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
    builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();

    // Scoped: abre DbContexts. Lo consume PermissionAuthorizationFilter
    // por peticion, no en el arranque.
    builder.Services.AddScoped<IngenIA365ERP.API.Services.PermisosDeLaPeticion>();
    // Feature 002 — accessor del JWT central (sub, email, active_tenant_id,
    // tenant_admin, is_global_master_admin, purpose, mfa_verified).
    builder.Services.AddSingleton<
        IngenIA365ERP.Application.Common.Interfaces.Identity.ICurrentCentralUserContext,
        CurrentCentralUserContextAccessor>();
    builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
    // T012: acceso a la IP del cliente desde Application/handlers, sin acoplar a HttpContext.
    builder.Services.AddSingleton<IIpAddressAccessor, IpAddressAccessor>();

    // === Identity & Security ===
    // Fase 0 (legacy ApplicationUser, JwtBearer, PermissionService).
    builder.Services.AddIdentityServices(builder.Configuration);
    // Feature 005: los handlers preguntan por permisos por el mismo camino que la puerta del
    // endpoint (identidad central → SEC_Users → cooperativa). Va después de Identity para que
    // esta registración sea la que resuelva IPermissionChecker.
    builder.Services.AddScoped<IngenIA365ERP.Application.Payroll.Services.IPermissionChecker, IngenIA365ERP.API.Services.PermisosDelHandler>();
    // Feature 008: los mismos permisos, expuestos al cliente por GET /api/admin/permissions/mine.
    builder.Services.AddScoped<IngenIA365ERP.Application.Common.Interfaces.Security.ICurrentUserPermissions, IngenIA365ERP.API.Services.PermisosDelHandler>();
    // Feature 009 (FR-035): sucursales asignadas de quien digita, para el contrato de contabilizacion.
    builder.Services.AddScoped<IngenIA365ERP.Application.Common.Interfaces.Security.IUserBranchScope, IngenIA365ERP.API.Services.UserBranchScope>();
    // T048 (Feature 002) — identidad central federada sobre AdminDbContext.
    // Registra IdentityCore<CentralUserIdentity>, BcryptPasswordHasher,
    // PwnedPasswordService, CentralJwtIssuer, ICentralIdentityProvider.
    // AdminDbContext ya queda registrado por AddPersistenceServices() abajo.
    builder.Services.AddCentralIdentity(builder.Configuration);

    builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
    // T075 — cache de permisos efectivos (fallback dev). En producción
    // AddCachingServices registra el RedisPermissionClaimsCache.
    builder.Services.AddSingleton<IPermissionClaimsCache, InMemoryPermissionClaimsCache>();

    // === Proxies conocidos: quien es «el cliente» detras del tunel ===
    // En produccion el trafico entra SOLO por el tunel de Cloudflare
    // (cloudflared → Traefik → API), asi que el socket siempre ve un pod de la red
    // 10.42.0.0/16 y X-Forwarded-For trae «visitante, cloudflared». Con esto,
    // UseForwardedHeaders (primer middleware) deja en Connection.RemoteIpAddress
    // la IP del visitante y en Request.Scheme el https original. ForwardLimit=2
    // son exactamente los dos saltos; un tercero (un X-Forwarded-For que el
    // cliente traiga puesto) no se consume, y solo se cree la cabecera si el
    // socket es de la red conocida —en local, loopback, que viene por defecto—.
    // KnownIPNetworks y no KnownNetworks: en .NET 10 la segunda esta obsoleta y
    // ambas son la misma lista por dentro (DualIPNetworkList).
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 2;
        o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.42.0.0"), 16));
    });

    // === Rate Limiting ===
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(options =>
    {
        options.EnableEndpointRateLimiting = true;
        options.StackBlockedRequests = false;
        options.HttpStatusCode = 429;
        // La IP que cuenta es la del visitante, y la trae Cloudflare en
        // CF-Connecting-IP. Hecho medido: con X-Real-IP toda la tabla
        // ADM_CentralUserLoginAttempts tenia IpAddress = 10.42.0.25 —el pod de
        // cloudflared: la IP interna del ultimo salto, no la del visitante—, asi
        // que los 10 logins por minuto y las 1000 peticiones por minuto se
        // aplicaban a TODA la plataforma junta, no a cada quien. Un cliente no
        // puede falsificar esta cabecera porque el origen solo es alcanzable por
        // el tunel y Cloudflare la sobrescribe en el borde; si algun dia hubiera
        // otra entrada directa, esa condicion deja de valer y hay que revisar
        // esto. Sin la cabecera (en local) el paquete cae a
        // Connection.RemoteIpAddress.
        // Se quito ClientIdHeader = "X-Tenant-Id": el limitador por IP solo usa el
        // ClientId para la lista blanca de clientes, que no existe, y la API dejo
        // de leer esa cabecera en la Feature 002 (el tenant sale del JWT).
        options.RealIpHeader = "CF-Connecting-IP";
        options.GeneralRules =
        [
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/login",
                Period = "1m",
                Limit = 10
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/refresh",
                Period = "1m",
                Limit = 20
            },
            new RateLimitRule
            {
                Endpoint = "*",
                Period = "1m",
                Limit = 1000
            }
        ];
    });
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

    // === Audit (MongoDB) ===
    builder.Services.AddAuditServices(builder.Configuration);

    // Application layer DI registration (MediatR, FluentValidation, Mapster)
    builder.Services.AddApplicationServices();

    // Feature 002 — opciones del flujo de identidad central
    // (URL base del frontend para el enlace de invitaciones, lifetime, etc.).
    builder.Services.Configure<IngenIA365ERP.Application.Common.Configuration.IdentityEmailOptions>(
        builder.Configuration.GetSection(
            IngenIA365ERP.Application.Common.Configuration.IdentityEmailOptions.SectionName));

    // Cuánto vive una sesión (inactividad y duración máxima). Se valida al arrancar:
    // un cero aquí no se descubre con la primera persona expulsada.
    builder.Services
        .AddOptions<IngenIA365ERP.Application.Common.Configuration.PoliticaDeSesionOptions>()
        .Bind(builder.Configuration.GetSection(
            IngenIA365ERP.Application.Common.Configuration.PoliticaDeSesionOptions.SectionName))
        .Validate(o => o.EsValida(out _), "La sección Sesion de la configuración no es válida; ver PoliticaDeSesionOptions.EsValida.")
        .ValidateOnStart();

    // Infrastructure layer DI registrations
    builder.Services.AddPersistenceServices(builder.Configuration);
    // Feature 002 (US2+US3+US4+Phase 4b) — los handlers de identidad central
    // requieren Redis para: IDistributedLock (single-use de invitaciones),
    // ICentralRefreshTokenStore (family rotation), IMfaPendingStore (secret
    // temporal de enrollment), ITenantMembershipReader (cache + pub/sub),
    // IMembershipChangedNotifier (invalidación distribuida), ILoginAttemptCounter
    // (lockout progresivo). El docker-compose levanta Redis en localhost:6379.
    builder.Services.AddCachingServices(builder.Configuration);

    // T030 — Email (MailKit) y T025/T030a — Storage abstractions.
    builder.Services.AddStorageServices(builder.Configuration);
    // Feature 005: el comprobante de pago se pinta con QuestPDF, que solo conoce la API.
    builder.Services.AddSingleton<IngenIA365ERP.Application.Payroll.Services.IPayslipPdfRenderer, IngenIA365ERP.API.Reports.PayslipPdfRenderer>();
    builder.Services.AddSingleton<IngenIA365ERP.Application.Accounting.Documents.IVoucherPdfRenderer, IngenIA365ERP.API.Reports.VoucherPdfRenderer>();
    // Feature 009: lector de archivos tabulares (catalogo propio, apertura, extracto). ClosedXML solo lo conoce la API.
    builder.Services.AddSingleton<IngenIA365ERP.Application.Common.Interfaces.Files.ITabularFileReader, IngenIA365ERP.API.Reports.Importadores.ClosedXmlTabularFileReader>();

    // T031 — SignalR para el push de notificaciones in-app.
    builder.Services.AddSignalR();
    // T120 — Implementación SignalR de INotificationPusher (US6).
    builder.Services.AddScoped<
        IngenIA365ERP.Application.Notifications.Contracts.INotificationPusher,
        IngenIA365ERP.API.Hubs.SignalRNotificationPusher>();

    // T136 — Health checks: /health/live (proceso) y /health/ready (deps).
    builder.Services.AddIngenIaHealthChecks(builder.Configuration);

    var app = builder.Build();

    // Feature 004 (T019/T020) — bitacora de arranque de base de datos.
    // La validacion fail-fast de la seccion Database corre via ValidateOnStart
    // al iniciar el host (Database.InvalidProvider / ConnectionStringMissing);
    // aqui solo se deja constancia del proveedor efectivo. TODA cadena citada
    // pasa por el masker (FR-006).
    {
        var dbOpts = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IngenIA365ERP.Persistence.Providers.DatabaseOptions>>()
            .Value;
        var providerOrigin = Environment.GetEnvironmentVariable("Database__Provider") is not null
            ? "variable de entorno"
            : "appsettings";
        Log.Information(
            "Base de datos: proveedor {Provider} (origen: {Origin}) — operativa {Conn} — admin {AdminConn} — AutoMigrate={AutoMigrate} RunParametricSeed={RunParametricSeed} RunTestSeed={RunTestSeed}",
            dbOpts.ProviderKey,
            providerOrigin,
            IngenIA365ERP.Persistence.Providers.ConnectionStringMasker.Mask(dbOpts.GetActiveConnectionString()),
            IngenIA365ERP.Persistence.Providers.ConnectionStringMasker.Mask(dbOpts.GetActiveAdminConnectionString()),
            dbOpts.AutoMigrate,
            dbOpts.Seed.RunParametricSeed,
            dbOpts.Seed.RunTestSeed?.ToString() ?? "(default por ambiente)");

        Log.Information("{Resumen}", InfraestructuraConfiguracion.Describir(app.Configuration));
    }

    // Initialize MongoDB collections and indexes
    var mongoInit = app.Services.GetRequiredService<MongoDbInitializer>();
    await mongoInit.InitializeDefaultAsync();

    // Feature 004 (T037): los seeders Phase 0 (catálogo de permisos, roles
    // built-in, SEC_Users admin) ya NO corren aquí — viven en el framework de
    // seeding (PhaseZeroSecuritySeeder) y los dispara el
    // DatabaseInitializerHostedService DESPUÉS de migrar. IdentitySeedData
    // (AspNetUsers legacy) quedó retirado: sus tablas no existen en
    // instalaciones greenfield.

    // Configure the HTTP request pipeline

    // PRIMERO: corrige RemoteIpAddress y Scheme con lo que dejaron los proxies
    // conocidos (opciones arriba, «Proxies conocidos»). Todo lo que sigue —el
    // limitador, la auditoria de ingresos, HSTS, la redireccion a https— lee
    // esos dos campos y tiene que verlos ya corregidos.
    app.UseForwardedHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        // T137 — HSTS solo en prod (en dev HTTP local rompería).
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    app.UseHttpsRedirection();
    // Red de seguridad de excepciones. Va lo mas arriba posible, para envolver
    // tambien lo que revienta en los middleware de abajo.
    //
    // No habia ninguna: cualquier excepcion no capturada salia como 500 crudo, sin
    // el envelope {code,message,traceId} que el resto de la API respeta, y en
    // Development con la traza entera. Dos consecuencias: la interfaz no sabia
    // pintar el error —mostraba un mensaje vacio— y quien depuraba perseguia
    // sintomas en vez de causas.
    //
    // Importa especialmente ahora: la fabrica de ErpTenantInfo lanza a proposito
    // cuando una peticion llega sin cooperativa resuelta, y eso tiene que verse como
    // un error con codigo, no como una pared de texto.
    app.UseExceptionHandler(rama => rama.Run(async contexto =>
    {
        var fallo = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

        // Feature 009: la carrera de edicion que ApplicationDbContext traduce a
        // ConcurrencyConflictException salia de aqui como 500 Generic.Unexpected, cuando el
        // ErrorEnvelopeFilter promete 409 para Concurrency.*. Es el usuario quien puede
        // resolverla (refrescar y reintentar), asi que se le dice.
        if (fallo is IngenIA365ERP.Domain.Exceptions.ConcurrencyConflictException conflicto)
        {
            contexto.Response.StatusCode = StatusCodes.Status409Conflict;
            contexto.Response.ContentType = "application/json";
            await contexto.Response.WriteAsJsonAsync(new
            {
                code = IngenIA365ERP.Application.Common.Models.Error.StaleRowVersion.Code,
                message = conflicto.Message,
                traceId = contexto.TraceIdentifier,
            });
            return;
        }

        contexto.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ExcepcionNoControlada")
            .LogError(fallo, "Excepcion no controlada en {Metodo} {Ruta}.",
                contexto.Request.Method, contexto.Request.Path);

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        contexto.Response.ContentType = "application/json";

        // Sin detalles del fallo en el cuerpo, ni en Development: el traceId lleva
        // a la linea del registro, que si los tiene.
        await contexto.Response.WriteAsJsonAsync(new
        {
            code = "Generic.Unexpected",
            message = "Ocurrio un error inesperado al procesar la solicitud.",
            traceId = contexto.TraceIdentifier,
        });
    }));

    app.UseSerilogRequestLogging();
    app.UseIpRateLimiting();
    app.UseCors("AllowFrontend");
    // T048: orden estricto — Authentication primero para que TenantResolution
    // pueda leer el claim active_tenant_id (T044). CentralIdentityChallenge
    // envuelve respuestas 401 vacías en el envelope JSON canónico (T045).
    app.UseAuthentication();
    app.UseCentralIdentityChallenge();
    app.UseTenantResolution();
    app.UseAuthorization();
    // T074 — convierte cualquier 404 bajo /api/* en el envelope canónico,
    // haciendo indistinguible "endpoint no existe" vs "no tienes permiso".
    app.UseNotFoundEnvelope();

    // Map Carter endpoints
    app.MapCarter();

    // T031 — Hub de notificaciones (autenticación JWT obligatoria por [Authorize]).
    app.MapHub<NotificationsHub>("/hubs/notifications");

    // Health check endpoint
    app.MapIngenIaHealthChecks();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the implicit Program class accessible to integration tests
public partial class Program;
