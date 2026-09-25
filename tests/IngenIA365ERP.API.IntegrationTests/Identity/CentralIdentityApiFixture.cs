using System.Data.Common;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DotNet.Testcontainers.Containers;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Fixture de integración de Identidad Central, PARAMETRIZADA POR PROVEEDOR
/// desde el feature 004 (T052, D-11): la variable <c>DB_PROVIDER</c>
/// (PostgreSql | SqlServer; default PostgreSql — clarificación #5) decide el
/// contenedor. El esquema YA NO sale de los DDL congelados: lo aprovisiona el
/// propio <c>DatabaseInitializerHostedService</c> de la aplicación con las
/// migraciones EF del proveedor (la fuente única de verdad) + seed paramétrico
/// — con lo cual cada corrida de la suite valida también el arranque real.
///
/// <para>
/// Feature 011: una subclase puede levantar algo más antes del host (<see cref="AntesDeArrancarAsync"/>)
/// y ajustar el host (<see cref="ConfigurarHost"/>). Así existe <c>ApiConAlmacenS3Fixture</c>, el mismo
/// host con los adjuntos en MinIO en vez del disco.
/// </para>
/// </summary>
public class CentralIdentityApiFixture : IAsyncLifetime
{
    public const string MasterEmail = "master@integration.test";
    public const string MasterPassword = "Master-Integration-2026!";

    public static readonly string ProviderKey =
        (Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "PostgreSql")
            .Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        ? "SqlServer"
        : "PostgreSQL";

    private readonly IDatabaseContainer _db = ProviderKey == "SqlServer"
        ? new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("IngenIA365_Test2026!")
            .Build()
        : new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithUsername("ingenia")
            .WithPassword("IngenIA365_Test2026!")
            .WithDatabase("ingenia365erp_test")
            .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public CapturingEmailSender Emails { get; } = new();

    /// <summary>Lo que una subclase necesita levantado antes que el host (un almacén, por ejemplo).</summary>
    protected virtual Task AntesDeArrancarAsync() => Task.CompletedTask;

    /// <summary>El último ajuste del host, después de los de esta fixture.</summary>
    protected virtual void ConfigurarHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) { }

    /// <summary>Lo que una subclase tiene que cerrar después del host.</summary>
    protected virtual Task DespuesDeCerrarAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            ((DotNet.Testcontainers.Containers.IContainer)_db).StartAsync(),
            _mongo.StartAsync(),
            _redis.StartAsync(),
            AntesDeArrancarAsync());

        var operationalConnection = _db.GetConnectionString();
        var adminConnection = WithDatabaseName(operationalConnection,
            ProviderKey == "SqlServer" ? "IngenIA365ERP_AdminTest" : "ingenia365erp_admin_test");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Feature 004: la seccion Database gobierna el motor. AutoMigrate
            // aprovisiona el esquema al arrancar el host de test (fuente de
            // verdad = migraciones EF del proveedor).
            builder.UseSetting("Database:Provider", ProviderKey);
            builder.UseSetting($"Database:ConnectionStrings:{ProviderKey}", operationalConnection);
            builder.UseSetting($"Database:AdminConnectionStrings:{ProviderKey}", adminConnection);
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("Database:Seed:RunParametricSeed", "true");
            builder.UseSetting("Database:Seed:RunTestSeed", "false");

            // Claves legacy que otras piezas (health, herramientas Fase 0) aun leen.
            builder.UseSetting("ConnectionStrings:DefaultConnection", operationalConnection);
            builder.UseSetting("ConnectionStrings:TenantConnection", adminConnection);
            builder.UseSetting("ConnectionStrings:SqlServer", operationalConnection);
            builder.UseSetting("ConnectionStrings:SqlServerAdmin", adminConnection);
            builder.UseSetting("MongoDb:ConnectionString", _mongo.GetConnectionString());
            builder.UseSetting("MongoDb:DatabaseName", "IngenIA365ERP_Audit_Test");
            builder.UseSetting("ConnectionStrings:MongoDB", _mongo.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
            // Pwned check apagado: los tests no deben depender de un servicio externo.
            builder.UseSetting("PwnedPassword:Enabled", "false");

            // Correo saliente «configurado» a ojos de IOutboundEmailStatus (feature 005): el
            // envío de comprobantes se niega antes de intentar si la sección Smtp no declara
            // host, puerto y remitente. El transporte real está sustituido por
            // CapturingEmailSender, así que nada sale de la máquina.
            builder.UseSetting("Smtp:Host", "smtp.integration.test");
            builder.UseSetting("Smtp:Port", "25");
            builder.UseSetting("Smtp:FromAddress", "noresponder@integration.test");

            // La suite no depende de nada que git ignore (2026-09-23): hasta esa fecha
            // el host sólo arrancaba con appsettings.Development.json (WebAuthn), las
            // credenciales del maestro de appsettings.Development.local.json y las
            // llaves de Keys/, los tres locales de cada máquina. En un clon limpio o en
            // un worktree todas las pruebas caían en ObjectDisposedException.
            //
            // Llaves RS256 propias de esta corrida, en una carpeta temporal: la API las
            // carga por ruta y, si no existen, sólo genera una efímera fuera de
            // Production según ASPNETCORE_ENVIRONMENT del PROCESO, que aquí no está.
            var (privada, publica) = CrearLlavesJwt();
            builder.UseSetting("JwtSettings:PrivateKeyPath", privada);
            builder.UseSetting("JwtSettings:PublicKeyPath", publica);

            // WebAuthn no tiene valor por defecto a propósito (cambiarlo invalida las
            // llaves inscritas): el host no arranca sin él.
            builder.UseSetting("WebAuthn:RelyingPartyId", "localhost");
            builder.UseSetting("WebAuthn:RelyingPartyName", "IngenIA365ERP (pruebas)");
            builder.UseSetting("WebAuthn:OrigenesPermitidos:0", "https://localhost:7200");

            // El maestro que siembra el host es el mismo que usa la fixture: sin
            // credenciales, el sembrador se niega a arrancar una instalación sin gobierno.
            builder.UseSetting("MasterAdmin:Email", MasterEmail);
            builder.UseSetting("MasterAdmin:Password", MasterPassword);

            // Feature 012 (T008; decisiones-transversales T10, T47): los trabajos de fondo nuevos
            // de la plataforma arrancan APAGADOS en las pruebas. Un despachador, un reenviador o un
            // programador corriendo por su cuenta harían que una e2e viera efectos a destiempo; las
            // pruebas conducen cada pasada a mano. Hoy nadie lee estas claves todavía: las enlaza
            // IntegrationOptions (T049) y las consultan el despachador (I2), AuditOutboxForwarder
            // (T065), ProgramadorDeTareas (T050) y el despachador de correo (T47).
            builder.UseSetting("Integration:Dispatcher:Enabled", "false");
            builder.UseSetting("Integration:AuditForwarder:Enabled", "false");
            builder.UseSetting("Integration:ScheduledTasks:Enabled", "false");
            builder.UseSetting("Integration:EmailDispatcher:Enabled", "false");

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            });

            ConfigurarHost(builder);
        });

        // Forzar arranque del host (dispara el DatabaseInitializerHostedService:
        // espera BD → lock → migra admin + operativa → seed parametrico).
        _ = Factory.Server;

        // Antes de la primera escritura: que el host hable con NUESTRO contenedor.
        Infrastructure.InfraestructuraDeLaSuite.ExigirQueElHostHableConElContenedor(
            Factory.Services, operationalConnection);

        await SeedMasterAdminAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    // TODO(feature 012, T008 parcial): con los trabajos de fondo apagados, las e2e conducen el
    // ciclo a mano con dos métodos que resuelven el servicio del contenedor y corren UNA pasada
    // para la cooperativa indicada:
    //   · ReenviarAuditoriaAsync(Guid tenantPublicId) → AuditOutboxForwarder, que crea T065
    //     (specs/012-inventario-comercial/tasks.md, fase 2: «expone una pasada manual para la fixture»);
    //     lo usa IntegridadDeAuditoriaTests (T023).
    //   · CorrerTareasProgramadasAsync(Guid tenantPublicId) → ProgramadorDeTareas e ITareaProgramada,
    //     que crea T050 (fase 2), sobre IntegrationOptions (T049) e IEjecutorEnCooperativa.
    // No se escriben todavía porque esos servicios no existen: se agregan aquí en cuanto T050 y
    // T065 los creen, y con eso T008 queda completa.

    /// <summary>Secreto TOTP del maestro, una vez inscrito. Lo usa <see cref="IniciarSesionMaestroAsync"/>.</summary>
    private string? _secretoMaestro;

    /// <summary>
    /// Inicia sesión como maestro y devuelve su access token, recorriendo el
    /// segundo factor de verdad.
    ///
    /// <para>
    /// El maestro ya no entra con sólo contraseña: era la cuenta con más poder
    /// del sistema —crear cooperativas, apagar la política de MFA de una
    /// cooperativa ajena, borrar el segundo factor de cualquiera— y la única que
    /// no tenía segundo factor. Este método hace lo que hará una persona: si no
    /// está inscrito, se inscribe; después verifica.
    /// </para>
    ///
    /// <para>
    /// Vive en la fixture y no en cada prueba a propósito: media suite necesita
    /// un token de maestro, y si cada una improvisa el flujo, cambiar la política
    /// de MFA obligaría a tocar quince archivos.
    /// </para>
    /// </summary>
    public async Task<string> IniciarSesionMaestroAsync(HttpClient http) =>
        (await SesionMaestroAsync(http)).GetProperty("accessToken").GetString()
        ?? throw new InvalidOperationException("mfa/verify no devolvió accessToken para el maestro.");

    /// <summary>
    /// La sesión completa del maestro, no sólo su access token: quien pruebe el
    /// refresh o el cierre de sesión necesita el resto del cuerpo.
    /// </summary>
    public async Task<System.Text.Json.JsonElement> SesionMaestroAsync(HttpClient http)
    {
        var login = await PostAsync(http, "/api/auth/login",
            new { email = MasterEmail, password = MasterPassword });
        var cuerpo = await LeerAsync(login);
        var reto = cuerpo.GetProperty("challenge").GetString();

        if (reto == "MfaEnrollmentRequired")
        {
            await InscribirMfaAsync(http, cuerpo.GetProperty("challengeToken").GetString()!);

            login = await PostAsync(http, "/api/auth/login",
                new { email = MasterEmail, password = MasterPassword });
            cuerpo = await LeerAsync(login);
            reto = cuerpo.GetProperty("challenge").GetString();
        }

        if (reto != "MfaRequired")
        {
            throw new InvalidOperationException(
                $"El login del maestro devolvió challenge='{reto}'. Se esperaba MfaRequired: " +
                "si el maestro vuelve a entrar sin segundo factor, el arreglo se revirtió.");
        }

        var verify = await PostAsync(http, "/api/auth/mfa/verify", new
        {
            code = CodigoTotp(_secretoMaestro!),
            useRecoveryCode = false,
        }, cuerpo.GetProperty("challengeToken").GetString());

        return await LeerAsync(verify);
    }

    private async Task InscribirMfaAsync(HttpClient http, string tokenDeInscripcion)
    {
        var inicio = await PostAsync(http, "/api/profile/mfa/enroll", new { }, tokenDeInscripcion);
        _secretoMaestro = (await LeerAsync(inicio)).GetProperty("secretBase32").GetString();

        var confirmar = await PostAsync(http, "/api/profile/mfa/confirm",
            new { code = CodigoTotp(_secretoMaestro!) }, tokenDeInscripcion);

        if (!confirmar.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"No se pudo inscribir el MFA del maestro: {(int)confirmar.StatusCode} " +
                await confirmar.Content.ReadAsStringAsync());
        }
    }

    /// <summary>Código TOTP válido ahora mismo, con el mismo algoritmo que valida el servidor.</summary>
    private static string CodigoTotp(string secretoBase32) =>
        new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secretoBase32)).ComputeTotp();

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient http, string url, object cuerpo, string? bearer = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = System.Net.Http.Json.JsonContent.Create(cuerpo),
        };
        if (bearer is not null) req.Headers.Authorization = new("Bearer", bearer);

        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"POST {url} respondió {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        }
        return resp;
    }

    private static async Task<System.Text.Json.JsonElement> LeerAsync(HttpResponseMessage resp) =>
        System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            await resp.Content.ReadAsStringAsync(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    private static string WithDatabaseName(string connectionString, string database)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var key = builder.ContainsKey("Database") ? "Database"
                : builder.ContainsKey("Initial Catalog") ? "Initial Catalog" : "Database";
        builder[key] = database;
        return builder.ConnectionString;
    }

    private async Task SeedMasterAdminAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<CentralUserIdentity>>();

        if (await userManager.FindByEmailAsync(MasterEmail) is not null) return;

        var master = new CentralUserIdentity
        {
            Id = Guid.NewGuid(),
            UserName = MasterEmail,
            Email = MasterEmail,
            EmailConfirmed = true,
            IsGlobalMasterAdmin = true,
        };
        var created = await userManager.CreateAsync(master, MasterPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo sembrar el master admin: " +
                string.Join("; ", created.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }
    }

    private string? _carpetaDeLlaves;

    /// <summary>Un par RS256 nuevo para esta corrida; se borra al cerrar la fixture.</summary>
    private (string Privada, string Publica) CrearLlavesJwt()
    {
        _carpetaDeLlaves = Path.Combine(Path.GetTempPath(), "ingenia-jwt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpetaDeLlaves);
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var privada = Path.Combine(_carpetaDeLlaves, "privada.pem");
        var publica = Path.Combine(_carpetaDeLlaves, "publica.pem");
        File.WriteAllText(privada, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(publica, rsa.ExportSubjectPublicKeyInfoPem());
        return (privada, publica);
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        try { if (_carpetaDeLlaves is not null && Directory.Exists(_carpetaDeLlaves)) Directory.Delete(_carpetaDeLlaves, recursive: true); }
        catch (IOException) { /* el SO puede tener el archivo abierto un instante más: queda en la carpeta temporal */ }
        await Task.WhenAll(
            ((DotNet.Testcontainers.Containers.IContainer)_db).DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            DespuesDeCerrarAsync());
    }
}

/// <summary>
/// <see cref="IEmailSender"/> de prueba: captura los mensajes en memoria para
/// que los tests extraigan tokens de invitación / reset sin servidor SMTP.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }
}
