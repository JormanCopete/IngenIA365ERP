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
/// <c>OrdenDeGiroEmitida</c> v2 adoptada y activa aportes y captaciones sin carga por <c>POST /api/lending/deposits/activation</c>. Desde la
/// decisión del dueño del 2026-10-18 (research §13 D14, D18) los requisitos se cumplen como en una cooperativa real, porque ya no se dispensan
/// fuera de producción (<see cref="RequisitosDeCaptacionesAsync"/>).
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
    /// <c>OrdenDeGiroEmitida</c> v2 (<c>TreasuryDestinationRegistered</c>) y deja cumplidos los demás requisitos (<see cref="RequisitosDeCaptacionesAsync"/>:
    /// los cuatro roles, el perfil que autoriza captar y la matriz mínima si ya hay líneas activas). La cooperativa opera con
    /// <c>Contabilidad.FaltaDeRegla = Advertir</c> (<see cref="CarteraE2E.ActivarCarteraAsync"/>); las e2e de US8 siembran su matriz. La segunda llamada
    /// con el mismo nombre devuelve la misma. Exige un despliegue con <b>K11</b>: sin K8 responde <c>Lending.Savings.DeliveryNotAvailable</c> y, desde la
    /// decisión del dueño del 2026-10-18 (D14), sin K11 falta <c>DeliveriesComplete</c> en cualquier ambiente. Las e2e corren al final de todas las fases.
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
        await RequisitosDeCaptacionesAsync(http, admin, hoy);

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

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (research §13 D14, D18): la activación de aportes y captaciones exige en todos los ambientes lo que antes sólo
    /// exigía producción. Deja a la cooperativa cumpliéndolo por las rutas, idempotente:
    /// <list type="bullet">
    /// <item>los roles de las cuatro plantillas de captaciones (<c>DepositRolesCreated</c>; <c>POST /api/lending/activation/suggested-roles</c> con sus
    /// claves);</item>
    /// <item>el perfil regulatorio que autoriza captar ahorro (<c>ProfileAllowsClasses</c>): corrige el vigente desde su misma fecha si no lo autoriza;</item>
    /// <item>la matriz mínima (<c>LinesWithMatrix</c>): si ya hay líneas activas de aportes o de captación, las reglas del hecho principal con el que la
    /// activación pregunta —<c>RecaudoPorAplicar</c> de Recaudo, <c>AportesSociales</c> ordinarios y <c>CapitalDeCaptacion</c> a la vista estándar—
    /// vigentes desde el primer día del mes. Sin líneas activas no se exige (así una cooperativa que se activa antes de crear líneas no estrena reglas).</item>
    /// </list>
    /// <c>DeliveriesComplete</c> no lo puede cumplir una prueba: lo trae el despliegue (K11).
    /// </summary>
    public static async Task RequisitosDeCaptacionesAsync(HttpClient http, string admin, DateOnly hoy)
    {
        var roles = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/activation/suggested-roles", new
        {
            templateKeys = new[] { "cartera.jefe-captaciones", "cartera.asesor-captaciones", "cartera.analista-aportes", "cartera.tesorero-liquidez" },
        });
        roles.IsSuccessStatusCode.Should().BeTrue($"crear los roles de captaciones: «{await roles.Content.ReadAsStringAsync()}»");

        const string Perfil = "/api/core/regulatory-profile";
        var vigente = (await CarteraE2E.GetAsync(http, admin, Perfil)).GetProperty("current");
        if (!vigente.TryGetProperty("mayTakeDeposits", out var capta) || capta.ValueKind != JsonValueKind.True)
        {
            var perfil = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Perfil}/versions", new
            {
                organizationType = vigente.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2,
                validFrom = DateOnly.Parse(vigente.GetProperty("validFrom").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                source = "Certificado de la Supersolidaria (e2e)", reason = "La cooperativa capta ahorro: requisito de la activación (e2e)",
                supersedesPublicId = vigente.GetProperty("versionPublicId").GetGuid(), mayTakeDeposits = true,
            });
            perfil.StatusCode.Should().Be(HttpStatusCode.Created, $"el perfil que autoriza captar: «{await perfil.Content.ReadAsStringAsync()}»");
        }

        var hayLineasActivas = false;
        foreach (var naturaleza in new[] { "Contribution", "Deposit" })
            hayLineasActivas |= (await CarteraE2E.GetAsync(http, admin, $"/api/lending/lines?nature={naturaleza}")).EnumerateArray()
                .Any(l => l.GetProperty("status") is var s && (s.ValueKind == JsonValueKind.String ? s.GetString() == "Active" : s.GetInt32() == 1));
        if (!hayLineasActivas) return;

        var desde = new DateOnly(hoy.Year, hoy.Month, 1);
        await ReglaMinimaAsync(http, admin, desde, "RecaudoPorAplicar", "collectorClass=Receipts", new { collectorClass = "Receipts" }, "24101097",
            "Recaudo por aplicar de captaciones (e2e)", "241010");
        await ReglaMinimaAsync(http, admin, desde, "AportesSociales", "contributionClass=Ordinary", new { contributionClass = "Ordinary" }, "31050597",
            "Aportes ordinarios (e2e)", "310505");
        await ReglaMinimaAsync(http, admin, desde, "CapitalDeCaptacion", "depositClass=Demand&accountFlag=Standard",
            new { depositClass = "Demand", accountFlag = "Standard" }, "21050597", "Depósitos de ahorro a la vista (e2e)", "210505");
        // Feature 014, US8 (T145; research E108): la activación pregunta por todos los hechos que la naturaleza emite desde el primer día —en aportes el
        // cruce y la devolución del retiro; en el ahorro a la vista el retiro por giro con su GMF, la causación, la liquidación y la retención—. La lista de
        // reglas no filtra por las dimensiones de captaciones: se busca por la cuenta.
        foreach (var (rol, dimensiones, cuenta, nombre, padre) in new (string, object, string, string, string)[]
                 {
                     ("RecaudoPorAplicar", new { collectorClass = "OwnLoanPayoff" }, "24101096", "Puente del retiro del asociado (e2e)", "241010"),
                     ("DevolucionPorPagar", new { lineNature = "Contribution", reasonCode = "MembershipRefund" }, "24650597", "Aportes de exasociados (e2e)", "246505"),
                     ("DevolucionPorPagar", new { lineNature = "Deposit", reasonCode = "DepositWithdrawal" }, "24651097", "Retiros por girar (e2e)", "246510"),
                     ("GmfPorPagar", new { depositClass = "Demand" }, "24300597", "GMF sobre depósitos de ahorro (e2e)", "243005"),
                     ("InteresPorPagar", new { depositClass = "Demand" }, "21059597", "Intereses por pagar del ahorro (e2e)", "210595"),
                     ("GastoPorIntereses", new { depositClass = "Demand" }, "61500597", "Intereses del ahorro a la vista (e2e)", "615005"),
                     ("RetencionPorPagar", new { depositClass = "Demand" }, "24353597", "Retención sobre rendimientos (e2e)", "243535"),
                 })
            await ReglaMinimaAsync(http, admin, desde, rol, $"account={cuenta}", dimensiones, cuenta, nombre, padre);
    }

    private static async Task ReglaMinimaAsync(HttpClient http, string admin, DateOnly desde, string rol, string filtro, object dimensiones, string cuenta,
        string nombre, string padre)
    {
        const string Reglas = "/api/accounting/lending/rules";
        if ((await CarteraE2E.GetAsync(http, admin, $"{Reglas}?role={rol}&{filtro}&pageSize=1")).GetProperty("totalCount").GetInt32() > 0) return;
        await Accounting.ContabilidadE2E.CrearCuentaAsync(http, admin, cuenta, nombre, padre, ["CNT", "CAR"]);
        var regla = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Reglas, new
        {
            operation = "*", role = rol, dimensions = dimensiones, accountPublicId = await Accounting.ContabilidadE2E.CuentaAsync(http, admin, cuenta),
            validFrom = desde, notes = "Requisito de la activación de captaciones (e2e)",
            reason = "La activación exige la matriz del hecho principal en todos los ambientes",
        });
        regla.StatusCode.Should().Be(HttpStatusCode.Created, $"la regla mínima {rol}: «{await regla.Content.ReadAsStringAsync()}»");
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
