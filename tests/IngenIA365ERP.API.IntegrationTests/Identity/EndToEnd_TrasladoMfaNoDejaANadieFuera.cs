using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// El traslado de secretos no puede dejar a nadie fuera de su cuenta.
///
/// <para>
/// <b>Por qué esta prueba tiene que sembrar el estado a mano.</b> La base de
/// desarrollo no tiene ni un secreto que trasladar, y los contenedores de esta
/// suite arrancan vacíos y aplican las migraciones antes de que exista un solo
/// dato. Correr el traslado en cualquiera de los dos entornos daría verde sin
/// haber movido nada — la peor clase de verde. Así que aquí se fabrica
/// deliberadamente el estado anterior a la migración: alguien con su secreto en
/// <c>ADM_CentralUsers.MfaSecret</c> y sin fila en <c>ADM_MfaCredentials</c>.
/// </para>
///
/// <para>
/// Y ejecuta <b>la migración de verdad</b>, retrocediendo y volviendo a aplicar,
/// en vez de repetir su SQL desde la prueba. Una prueba que reimplementa lo que
/// quiere verificar sólo se comprueba a sí misma.
/// </para>
///
/// <para>
/// Lo que se afirma no es que la fila aparezca —eso es un detalle de
/// almacenamiento— sino que <b>la persona sigue pudiendo entrar con su
/// autenticador</b>, que es lo único que le importa a quien tiene el teléfono en
/// la mano.
/// </para>
/// </summary>
public sealed class EndToEnd_TrasladoMfaNoDejaANadieFuera(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Correo = "rosa.traslado@coop.traslado.test";
    private const string Clave = "Rosa-Strong-Pwd-2026!";

    [Fact]
    public async Task Un_secreto_del_modelo_viejo_sigue_sirviendo_antes_y_despues_del_traslado()
    {
        using var http = fx.CreateClient();

        // 1) Montar una cooperativa con Rosa dentro, y que Rosa inscriba su MFA
        //    por el camino normal.
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Traslado MFA",
                schemaName = "tenant_traslado_mfa",
                nit = "900333222",
                legalName = "Coop. Traslado MFA SAS",
                contactEmail = "contacto@coop.traslado.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = Correo,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(registerReq)).StatusCode);

        var tokenRosa = await AceptarInvitacionAsync(http);
        var secreto = await InscribirMfaAsync(http, tokenRosa);

        // 2) Fabricar el estado ANTERIOR a la migración: la credencial nueva no
        //    existe y el secreto sólo vive en la columna vieja. Es exactamente la
        //    foto de cualquier persona que inscribió su MFA antes de este trabajo.
        await EjecutarSqlAsync(@"DELETE FROM {0}ADM_MfaCredentials{1}");

        Assert.Equal(0, await ContarCredencialesAsync());

        // Cuántas personas hay que rescatar. No se fija a mano: además de Rosa, el
        // fixture inscribe el segundo factor del maestro. El número exacto da
        // igual — lo que la migración promete es que NINGUNA se quede sin fila.
        var aRescatar = await ContarSecretosHeredadosAsync();
        Assert.True(aRescatar > 0, "El montaje debía dejar al menos un secreto heredado que trasladar.");

        // 3) Con ese estado, Rosa TIENE que poder entrar igual: es el modo
        //    compatibilidad, la red que sostiene el despliegue escalonado.
        Assert.True(await PuedeEntrarConSuAutenticadorAsync(http, secreto),
            "Antes del traslado, el modo compatibilidad debe dejar entrar leyendo la columna vieja.");

        // 4) Ejecutar la migración de datos REAL: atrás y adelante.
        await ReaplicarTrasladoAsync();

        // 5) La credencial existe y guarda EXACTAMENTE el mismo ciphertext. Si
        //    alguien la recodificara o truncara por el camino, el texto diferiría
        //    y el secreto quedaría indescifrable para siempre.
        Assert.Equal(aRescatar, await ContarCredencialesAsync());
        Assert.True(await ElCifradoCoincideAsync(),
            "El ciphertext trasladado difiere del original: se recodificó o se truncó.");

        // 6) Y lo único que de verdad importa: Rosa sigue entrando.
        Assert.True(await PuedeEntrarConSuAutenticadorAsync(http, secreto),
            "Después del traslado, la persona debe seguir entrando con el mismo autenticador.");
    }

    // ---------- Acceso a la base ----------

    private IServiceScope NuevoScope() => fx.Factory.Services.CreateScope();

    private static bool EsPostgres => CentralIdentityApiFixture.ProviderKey != "SqlServer";

    /// <summary>Aplica el dialecto de identificadores del motor en curso.</summary>
    private static string Dialecto(string plantilla) => EsPostgres
        ? string.Format(plantilla, "\"dbo\".\"", "\"")
        : string.Format(plantilla, "[dbo].[", "]");

    private async Task EjecutarSqlAsync(string plantilla)
    {
        using var scope = NuevoScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        await db.Database.ExecuteSqlRawAsync(Dialecto(plantilla));
    }

    private async Task<int> ContarCredencialesAsync()
    {
        using var scope = NuevoScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        return await db.MfaCredentials.IgnoreQueryFilters().CountAsync();
    }

    private async Task<int> ContarSecretosHeredadosAsync()
    {
        using var scope = NuevoScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        return await db.Users.IgnoreQueryFilters().CountAsync(u => u.MfaSecret != null);
    }

    /// <summary>
    /// Compara el ciphertext trasladado contra el de la columna origen. La copia
    /// tiene que ser literal: cualquier recodificación rompe el descifrado.
    /// </summary>
    private async Task<bool> ElCifradoCoincideAsync()
    {
        using var scope = NuevoScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        var origen = await db.Users.IgnoreQueryFilters()
            .Where(u => u.MfaSecret != null)
            .Select(u => new { u.Id, u.MfaSecret })
            .ToListAsync();

        var copias = await db.MfaCredentials.IgnoreQueryFilters()
            .OfType<Domain.Entities.Admin.TotpCredential>()
            .Select(c => new { c.CentralUserId, c.SecretProtected })
            .ToListAsync();

        return origen.All(o =>
            copias.Any(c => c.CentralUserId == o.Id && c.SecretProtected == o.MfaSecret));
    }

    /// <summary>
    /// Retrocede el contexto administrativo a la migración anterior al traslado y
    /// vuelve a avanzarlo. Ejecuta el Down y el Up REALES.
    /// </summary>
    private async Task ReaplicarTrasladoAsync()
    {
        using var scope = NuevoScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var migrador = db.GetService<IMigrator>();

        await migrador.MigrateAsync("CredencialesMfa");
        await migrador.MigrateAsync();
    }

    // ---------- Flujo HTTP ----------

    private async Task<string> AceptarInvitacionAsync(HttpClient http)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == Correo);
        Assert.NotNull(mensaje);
        var match = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, "El correo de invitación no trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = Clave },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<string> InscribirMfaAsync(HttpClient http, string token)
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var beginResp = await http.SendAsync(beginReq);
        Assert.Equal(HttpStatusCode.OK, beginResp.StatusCode);
        var secreto = (await LeerJsonAsync(beginResp)).GetProperty("secretBase32").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secreto));

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        confirmReq.Headers.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(confirmReq)).StatusCode);
        return secreto!;
    }

    /// <summary>
    /// Login completo con el autenticador: es la comprobación que le importa a la
    /// persona, no el contenido de una tabla.
    /// </summary>
    private static async Task<bool> PuedeEntrarConSuAutenticadorAsync(HttpClient http, string secreto)
    {
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new { email = Correo, password = Clave });
        if (loginResp.StatusCode != HttpStatusCode.OK) return false;

        var login = await LeerJsonAsync(loginResp);
        if (login.GetProperty("challenge").GetString() != "MfaRequired") return false;

        var challengeToken = login.GetProperty("challengeToken").GetString();

        using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        verifyReq.Headers.Authorization = new("Bearer", challengeToken);
        var verifyResp = await http.SendAsync(verifyReq);
        return verifyResp.StatusCode == HttpStatusCode.OK;
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
