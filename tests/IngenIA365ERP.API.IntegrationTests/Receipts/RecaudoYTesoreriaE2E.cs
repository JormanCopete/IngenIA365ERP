using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Colección «Recaudo y Tesorería e2e» (feature 015, T0009; quickstart.md): un host propio, con sus contenedores, para las e2e de los dos
/// módulos, en PostgreSQL y SQL Server (<c>DB_PROVIDER</c>). Lo que activa Recaudo o Tesorería corre en una cooperativa aislada por prueba
/// (<see cref="RecaudoYTesoreriaE2E.CooperativaAisladaAsync"/>): la activación cambia lo que ven Cartera y Captaciones. Se escriben durante
/// cada fase y se corren una sola vez al final de R0/T0 (fase 22), salvo las dos excepciones declaradas en tasks.md (T0183c y T0301).
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class RecaudoYTesoreriaCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Recaudo y Tesorería e2e";
}

/// <summary>
/// Ayudantes de las e2e de Recaudo y Tesorería (feature 015, T0009; molde <see cref="CarteraE2E"/>): la clave de idempotencia, la cooperativa
/// aislada, el token del maestro y el envío y la lectura de peticiones, delegados en los de Cartera para que las dos colecciones hablen igual
/// con la API. <see cref="RegistrarYActivarAsync"/> es el esqueleto de la activación que completa la fase 19 (US9).
/// </summary>
public static class RecaudoYTesoreriaE2E
{
    public const string CabeceraDeClave = CarteraE2E.CabeceraDeClave;

    /// <summary>Pone una <c>Idempotency-Key</c> nueva; la prueba de repetición pone la misma clave a mano.</summary>
    public static HttpRequestMessage ConClave(HttpRequestMessage peticion) => CarteraE2E.ConClave(peticion);

    /// <summary>
    /// Una cooperativa aislada por nombre y fixture (la segunda llamada con el mismo nombre devuelve la misma). Con
    /// <paramref name="contabilidadIniciada"/> la cooperativa nace con su primer ejercicio contable en el año de hoy (los hechos pasan a
    /// Contabilidad); sin ella, el modo de paso por defecto es «no pasa».
    /// </summary>
    public static Task<CarteraE2E.CooperativaAislada> CooperativaAisladaAsync(CentralIdentityApiFixture fx, string nombre, bool contabilidadIniciada = false) =>
        CarteraE2E.CooperativaAisladaAsync(fx, $"ryt-{nombre}", contabilidadIniciada ? CarteraE2E.Hoy(fx).Year : null);

    /// <summary>El token del maestro, compartido con Inventario y Cartera: el limitador de login admite 10 por minuto por IP.</summary>
    public static Task<string> TokenMaestroAsync(CentralIdentityApiFixture fx, HttpClient http) => CarteraE2E.TokenMaestroAsync(fx, http);

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo) =>
        CarteraE2E.EnviarAsync(http, token, metodo, url, cuerpo);

    public static Task<JsonElement> LeerAsync(HttpResponseMessage resp) => CarteraE2E.LeerAsync(resp);

    public static Task<string> CodigoDeErrorAsync(HttpResponseMessage resp) => CarteraE2E.CodigoDeErrorAsync(resp);

    /// <summary>
    /// Registra a Recaudo y Tesorería como participantes reales y los activa en la cooperativa con la <b>activación mínima</b> del bloque 2 (T0183b;
    /// <c>POST /api/receipts/activation</c> y <c>/api/treasury/activation</c>): cada módulo se activa si sus requisitos básicos ya se cumplen (Recaudo: punto, caja
    /// con cajero, efectivo y transferencia; Tesorería: una cuenta de pagos vinculada) y la activación registra en el mismo guardado sus filas del registro de
    /// integración. Un módulo ya activo no se vuelve a activar. La fase 19 completa la activación (el cuadre) sobre los mismos comandos.
    /// </summary>
    public static async Task RegistrarYActivarAsync(HttpClient http, CarteraE2E.CooperativaAislada coop)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(coop);
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var estado = await LeerAsync(await EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, $"/api/{modulo}/activation/requirements", null));
            if (estado.GetProperty("status").GetString() == "Active" || !estado.GetProperty("canActivate").GetBoolean()) continue;
            var activado = await EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/{modulo}/activation", new { });
            ((int)activado.StatusCode).Should().BeInRange(200, 299, $"activar {modulo}: «{await activado.Content.ReadAsStringAsync()}»");
        }
    }
}

/// <summary>La prueba humo de la colección (T0009): crea una cooperativa aislada y su administrador entra. Se escribe ahora y corre en la fase 22.</summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class RecaudoYTesoreriaHumoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_coleccion_crea_su_cooperativa_aislada_y_su_administrador_entra()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "humo");
        coop.TenantPublicId.Should().NotBe(Guid.Empty);

        using var http = fx.CreateClient();
        var resp = await RecaudoYTesoreriaE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/admin/permissions/mine", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
