using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Colección «Captaciones e2e» (feature 014, T003; research R30): un host propio, con sus contenedores, para las e2e de
/// captaciones y aportes, en paralelo con «Cartera e2e». Todo corre en cooperativas aisladas (<see cref="CaptacionesE2E"/>): la
/// activación de cada naturaleza es irreversible.
///
/// <para>
/// Decisión del dueño: las e2e contra base de datos se corren <b>una sola vez al final de todas las fases</b> (como las de K5 de la
/// 013); mientras tanto se escriben y compilan.
/// </para>
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class CaptacionesCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Captaciones e2e";
}

/// <summary>
/// Ayudantes de las e2e de captaciones (feature 014, T003): la cooperativa aislada con <b>créditos activados primero</b> —con carga
/// vacía, por las rutas reales de la 013 (<see cref="CarteraE2E.ActivarCarteraAsync"/>), que también registra el recaudador
/// <c>TST</c> y la tesorería <c>TES</c> de prueba—, y el punto donde la fase fundacional suma la activación de aportes y
/// captaciones sin carga (T055).
///
/// <para>
/// Hasta T055 la cooperativa queda con la Cartera activada y las dos naturalezas de captación sin activar: el contrato de cuentas
/// responde «no habilitado» (FR-093), que es lo que prueban las e2e de la fase fundacional. Desde T055,
/// <see cref="CooperativaConCaptacionesAsync"/> registra el recaudador de cuentas (<c>ProveedorDeCuentas</c>) y la tesorería con
/// <c>OrdenDeGiroEmitida</c> v2 adoptada y activa aportes y captaciones sin carga por <c>POST /api/lending/deposits/activation</c> (la matriz
/// mínima no hace falta fuera de producción: <c>LinesWithMatrix</c> queda dispensado, research E32).
/// </para>
/// </summary>
public static class CaptacionesE2E
{
    /// <summary>Lo que dejan los ayudantes: la cooperativa y su activación de créditos.</summary>
    public sealed record CooperativaDeCaptaciones(CarteraE2E.CooperativaAislada Cooperativa, CarteraE2E.CarteraActivadaE2E Creditos)
    {
        public string TokenAdmin => Cooperativa.TokenAdmin;
        public Guid TenantPublicId => Cooperativa.TenantPublicId;
    }

    /// <summary>
    /// Una cooperativa aislada por nombre (prefijo <c>cap_</c>, para no cruzarse con las de «Cartera e2e») con la Cartera de créditos
    /// activada con carga vacía. La segunda llamada con el mismo nombre devuelve la misma.
    /// </summary>
    public static async Task<CooperativaDeCaptaciones> CooperativaConCreditosAsync(CentralIdentityApiFixture fx, string nombre)
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, $"cap_{nombre}");
        var creditos = await CarteraE2E.ActivarCarteraAsync(fx, coop);
        return new CooperativaDeCaptaciones(coop, creditos);
    }

    private static readonly object Cerrojo = new();

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<CooperativaDeCaptaciones>> ConCaptaciones = [];

    /// <summary>
    /// La cooperativa con créditos activados y aportes y captaciones activados <b>sin carga</b> (T055; FR-117) por
    /// <c>POST /api/lending/deposits/activation</c>, con fecha = hoy (la de los créditos). Antes registra de nuevo, desde hoy, el recaudador de prueba
    /// <c>TST</c> con <c>ProveedorDeCuentas</c> adoptado (<c>AccountsCollectorRegistered</c>) y la tesorería de prueba <c>TES</c> con
    /// <c>OrdenDeGiroEmitida</c> v2 (<c>TreasuryDestinationRegistered</c>). La matriz contable de los hechos de la naturaleza (<c>LinesWithMatrix</c>) no
    /// se siembra: fuera de producción ese requisito queda dispensado y la cooperativa opera con <c>Contabilidad.FaltaDeRegla = Advertir</c>
    /// (<see cref="CarteraE2E.ActivarCarteraAsync"/>); las e2e de US8 siembran la suya. La segunda llamada con el mismo nombre devuelve la misma.
    /// Exige un despliegue con K8 (sin él responde <c>Lending.Savings.DeliveryNotAvailable</c>): las e2e corren al final de todas las fases.
    /// </summary>
    public static Task<CooperativaDeCaptaciones> CooperativaConCaptacionesAsync(CentralIdentityApiFixture fx, string nombre)
    {
        lock (Cerrojo)
        {
            // Por nombre: la cooperativa aislada se resuelve dentro de la tarea y el mismo nombre es la misma cooperativa.
            var clave = (fx, nombre);
            if (!ConCaptaciones.TryGetValue(clave, out var tarea))
            {
                tarea = ActivarCaptacionesAsync(fx, nombre);
                ConCaptaciones[clave] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<CooperativaDeCaptaciones> ActivarCaptacionesAsync(CentralIdentityApiFixture fx, string nombre)
    {
        var coop = await CooperativaConCreditosAsync(fx, nombre);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);

        await RegistrarDobleDeCuentasAsync(http, admin, hoy, new
        {
            code = "TST", @class = "Test", role = "ObligationsCollector", destination = (string?)null, isEnabled = true, isTestDouble = true,
            adoptions = new[] { new { contract = "ProveedorDeObligaciones", version = 1 }, new { contract = "ProveedorDeCuentas", version = 1 } },
            validFrom = hoy, reason = "Recaudador de prueba con el contrato de cuentas (e2e de captaciones)",
        }, "ProveedorDeCuentas");
        await RegistrarDobleDeCuentasAsync(http, admin, hoy, new
        {
            code = "TES", @class = "Test", role = "MessageDestination", destination = "Treasury", isEnabled = true, isTestDouble = true,
            adoptions = new[]
            {
                new { contract = "OrdenDeGiroEmitida", version = 1 }, new { contract = "OrdenDeGiroEmitida", version = 2 },
                new { contract = "AnulacionDeOrdenDeGiro", version = 1 },
            },
            validFrom = hoy, reason = "Tesorería de prueba con la orden de giro v2 (e2e de captaciones)",
        }, "OrdenDeGiroEmitida", version: 2);

        foreach (var naturaleza in new[] { "Contribution", "Deposit" })
        {
            var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/deposits/activation", new
            {
                nature = naturaleza, activationDate = hoy, acknowledgeWarnings = true, reason = $"Activación sin carga de {naturaleza} (e2e)",
            });
            if (resp.StatusCode == HttpStatusCode.Created) continue;
            (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.Activation.AlreadyActivated",
                $"activar {naturaleza} sin carga: «{await resp.Content.ReadAsStringAsync()}»");
        }
        return coop;
    }

    /// <summary>Registra de nuevo un doble desde hoy con la adopción que falta, si el vigente no la tiene.</summary>
    private static async Task RegistrarDobleDeCuentasAsync(HttpClient http, string admin, DateOnly hoy, object cuerpo, string contrato, int version = 1)
    {
        var codigo = (string)cuerpo.GetType().GetProperty("code")!.GetValue(cuerpo)!;
        var registro = await CarteraE2E.GetAsync(http, admin, "/api/core/integration-participants?class=Test");
        var vigente = registro.EnumerateArray().FirstOrDefault(p => p.GetProperty("code").GetString() == codigo
                                                                    && p.GetProperty("validTo").ValueKind == JsonValueKind.Null);
        if (vigente.ValueKind == JsonValueKind.Object
            && vigente.GetProperty("adoptions").EnumerateArray().Any(a => a.GetProperty("contract").GetString() == contrato
                                                                          && a.GetProperty("version").GetInt32() == version))
            return;
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/core/integration-participants", cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar el doble {codigo} desde {hoy:yyyy-MM-dd}: «{await resp.Content.ReadAsStringAsync()}»");
    }
}
