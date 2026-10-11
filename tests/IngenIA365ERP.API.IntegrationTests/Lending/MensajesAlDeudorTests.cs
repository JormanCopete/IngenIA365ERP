using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0274 (feature 013, US17, C108; T74–T76, FR-122; contracts/api.md §14; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>):
/// la cola de mensajes al deudor por HTTP sobre una cooperativa aislada activada con <see cref="CarteraE2E.ActivarCarteraAsync"/>, con el
/// reloj de la suite y las pasadas de <c>cartera.mensajes</c> a mano (la fixture deja el programador apagado).
/// <list type="bullet">
/// <item>un envío manual inmediato por SMS sale por el canal <c>SIMULADO</c> (no hay SMS configurado) y queda <c>Simulated</c> con el destino
/// copiado del maestro al despachar (C57);</item>
/// <item>uno programado espera su hora: con el reloj de la suite adelantado, <c>CorrerTareasProgramadasAsync</c> lo despacha;</item>
/// <item>se cancela antes de salir y, cancelado, no sale ni se vuelve a cancelar (<c>Lending.DebtorMessage.AlreadySent</c>);</item>
/// <item>una versión nueva de <c>ABONOCAP</c> con una variable fuera de su lista blanca → <c>Lending.MessageTemplate.VariableNotAllowed</c>; la
/// válida cierra la anterior; y el mensaje siguiente usa la versión nueva mientras el anterior conserva la suya.</item>
/// </list>
/// Sin créditos sólo hay un propósito que se puede enviar: la entrega de un documento (<c>DocumentDelivery</c>, sin crédito y exenta de los
/// horarios, regla 7). Lo que pide un crédito —el inmediato de un domingo → <c>Lending.DebtorMessage.OutsideLegalWindow</c> con
/// <c>nextAllowedAt</c>, el programado para el domingo → <c>Deferred</c> y despachado el lunes a la primera hora permitida, y el informativo
/// con la versión nueva de <c>ABONOCAP</c>— lo suma a esta clase la fase de US3 sobre el primer crédito desembolsado, nunca con
/// <c>Skip</c>. US3 (bloque 7C) lo suma sobre la cooperativa «desembolso» (<see cref="DesembolsoE2E"/>). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class MensajesAlDeudorTests(CentralIdentityApiFixture fx)
{
    private const string Mensajes = "/api/lending/debtor-messages";
    private const string Plantillas = "/api/lending/message-templates";
    private const string Movil = "3001234560";

    [Fact]
    public async Task Un_envio_inmediato_sale_por_el_simulado_con_el_destino_copiado()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();

        var mensaje = await EnviarAsync(http, esc, "PS-0001");
        mensaje.GetProperty("status").GetInt32().Should().Be((int)DebtorMessageEventKind.Queued);
        mensaje.GetProperty("template").GetProperty("code").GetString().Should().Be("ENVIODOC");
        var id = mensaje.GetProperty("publicId").GetGuid();

        await DespacharAsync(esc);

        var detalle = await CarteraE2E.GetAsync(http, esc.Coop.TokenAdmin, $"{Mensajes}/{id}");
        detalle.GetProperty("message").GetProperty("status").GetInt32().Should().Be((int)DebtorMessageEventKind.Simulated, detalle.ToString());
        detalle.GetProperty("message").GetProperty("simulated").GetBoolean().Should().BeTrue();
        detalle.GetProperty("channelCode").GetString().Should().Be("SIMULADO", "sin SMS configurado el canal es el simulado");
        detalle.GetProperty("destinationAddress").GetString().Should().Be(Movil, "el destino se copia del maestro al despachar (C57)");
        var simulado = detalle.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("kind").GetInt32() == (int)DebtorMessageEventKind.Simulated);
        simulado.GetProperty("destinationAddress").GetString().Should().Be(Movil);
        detalle.GetProperty("body").GetString().Should().Contain("PS-0001", "el cuerpo se renderiza una vez, al encolar");
    }

    [Fact]
    public async Task Uno_programado_espera_su_hora_y_sale_cuando_el_reloj_llega()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var conductor = new ConductorDelDespachador(fx);
        var para = conductor.Reloj.AhoraLocal.AddDays(1);

        var mensaje = await EnviarAsync(http, esc, "PS-0002", para);
        mensaje.GetProperty("status").GetInt32().Should().Be((int)DebtorMessageEventKind.Queued);
        var id = mensaje.GetProperty("publicId").GetGuid();

        await DespacharAsync(esc);
        (await EstadoAsync(http, esc, id)).Should().Be((int)DebtorMessageEventKind.Queued, "todavía no es su hora");

        using (conductor.Adelantar(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(5)))
            await CarteraE2E.CorrerTareasProgramadasAsync(fx, esc.Coop.TenantPublicId);

        // La misma pasada corre cartera.mensajes y, a su hora, cartera.entregas: el simulado puede quedar ya Delivered.
        var detalle = await CarteraE2E.GetAsync(http, esc.Coop.TokenAdmin, $"{Mensajes}/{id}");
        detalle.GetProperty("message").GetProperty("status").GetInt32().Should()
            .BeOneOf([(int)DebtorMessageEventKind.Simulated, (int)DebtorMessageEventKind.Delivered], "llegada su hora, cartera.mensajes lo despacha");
        detalle.GetProperty("events").EnumerateArray().Should()
            .Contain(e => e.GetProperty("kind").GetInt32() == (int)DebtorMessageEventKind.Simulated
                          && e.GetProperty("destinationAddress").GetString() == Movil);
    }

    [Fact]
    public async Task Se_cancela_antes_de_salir_y_cancelado_no_sale()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var para = new ConductorDelDespachador(fx).Reloj.AhoraLocal.AddDays(3);

        var id = (await EnviarAsync(http, esc, "PS-0003", para)).GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Mensajes}/{id}/cancel", new { reason = "El titular recogió el documento en la oficina" });
        (await EstadoAsync(http, esc, id)).Should().Be((int)DebtorMessageEventKind.Cancelled);

        var otraVez = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Mensajes}/{id}/cancel", new { reason = "Otra vez" });
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await otraVez.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.DebtorMessage.AlreadySent");

        await DespacharAsync(esc);
        (await EstadoAsync(http, esc, id)).Should().Be((int)DebtorMessageEventKind.Cancelled, "lo cancelado no sale");
        var detalle = await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{id}");
        detalle.GetProperty("events").EnumerateArray().Last().GetProperty("reasonText").GetString()
            .Should().Be("El titular recogió el documento en la oficina");
    }

    [Fact]
    public async Task Una_version_nueva_de_plantilla_se_valida_cierra_la_anterior_y_la_usa_el_mensaje_siguiente()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;

        // ABONOCAP (informativo): una variable de cobranza no está en su lista blanca.
        var invalida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Plantillas}/ABONOCAP/versions", new
        {
            purpose = "Informative", channel = "Sms", body = "{{NombreDeLaCooperativa}}: su credito {{NumeroDelCredito}} tiene {{ValorVencido}} vencido.",
            validFrom = esc.Hoy, reason = "Texto nuevo del abono a capital",
        });
        invalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await invalida.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(invalida)).Should().Be("Lending.MessageTemplate.VariableNotAllowed");

        var valida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Plantillas}/ABONOCAP/versions", new
        {
            purpose = "Informative", channel = "Sms", body = "{{NombreDeLaCooperativa}}: abonamos {{ValorAplicado}} a su credito {{NumeroDelCredito}}.",
            validFrom = esc.Hoy, reason = "Texto nuevo del abono a capital",
        });
        valida.StatusCode.Should().Be(HttpStatusCode.Created, await valida.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(valida)).GetProperty("version").GetInt32().Should().Be(2);
        var historia = (await CarteraE2E.GetAsync(http, admin, $"{Plantillas}/ABONOCAP/history?channel=Sms")).EnumerateArray().ToList();
        historia.Single(v => v.GetProperty("version").GetInt32() == 1).GetProperty("validTo").GetString().Should().Be(esc.Hoy.AddDays(-1).ToString("yyyy-MM-dd"));
        historia.Single(v => v.GetProperty("version").GetInt32() == 2).GetProperty("validTo").ValueKind.Should().Be(JsonValueKind.Null);

        // El mismo mecanismo sobre la plantilla que sí se puede enviar sin crédito: el anterior conserva su versión.
        var anterior = await EnviarAsync(http, esc, "PS-0004");
        var versionAnterior = anterior.GetProperty("template").GetProperty("version").GetInt32();
        var nueva = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Plantillas}/ENVIODOC/versions", new
        {
            purpose = "DocumentDelivery", channel = "Sms",
            body = "{{NombreDeLaCooperativa}}: su documento {{NumeroDelDocumento}} esta listo. Codigo {{CodigoDeVerificacion}}.",
            validFrom = esc.Hoy, reason = "Texto más corto del envío de documentos",
        });
        nueva.GetProperty("version").GetInt32().Should().Be(versionAnterior + 1);

        var siguiente = await EnviarAsync(http, esc, "PS-0005");
        siguiente.GetProperty("template").GetProperty("version").GetInt32().Should().Be(versionAnterior + 1, "el mensaje siguiente usa la vigente");
        (await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{anterior.GetProperty("publicId").GetGuid()}"))
            .GetProperty("message").GetProperty("template").GetProperty("version").GetInt32().Should().Be(versionAnterior, "el anterior conserva la suya");
        (await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{siguiente.GetProperty("publicId").GetGuid()}"))
            .GetProperty("body").GetString().Should().Contain("esta listo");
    }

    // ------------------------------------------------------------------------------ con un crédito (US3) --

    /// <summary>
    /// US3 (bloque 7C, T0429): sobre el primer crédito desembolsado (la cooperativa «desembolso», <see cref="DesembolsoE2E"/>), un informativo
    /// por SMS de <c>ABONOCAP</c>: inmediato un domingo → <c>Lending.DebtorMessage.OutsideLegalWindow</c> con <c>nextAllowedAt</c>; programado
    /// para el domingo → <c>Deferred</c> y despachado el día hábil siguiente a la primera hora permitida; y una versión nueva de <c>ABONOCAP</c>
    /// la usa el mensaje siguiente.
    /// </summary>
    [Fact]
    public async Task Con_un_credito_el_domingo_no_sale_se_difiere_al_dia_habil_y_la_version_nueva_de_ABONOCAP_rige()
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var (_, persona, hecho) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "Informado");
        await AutorizarCanalAsync(http, admin, persona);
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var conductor = new ConductorDelDespachador(fx);

        var ahora = conductor.Reloj.AhoraLocal;
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var dias = ((int)DayOfWeek.Sunday - (int)hoy.DayOfWeek + 7) % 7;
        var domingo = new DateTimeOffset(hoy.AddDays(dias == 0 ? 7 : dias).ToDateTime(new TimeOnly(10, 0)), ahora.Offset);

        // Programado para el domingo: nace diferido al primer instante permitido después.
        var programado = await InformativoAsync(http, admin, credito, persona, domingo);
        programado.StatusCode.Should().Be(HttpStatusCode.Created, await programado.Content.ReadAsStringAsync());
        var diferido = await CarteraE2E.LeerAsync(programado);
        diferido.GetProperty("status").GetInt32().Should().Be((int)DebtorMessageEventKind.Deferred, diferido.ToString());
        var siguiente = Instante(diferido.GetProperty("lastEvent").GetProperty("nextAttemptAt").GetString()!, ahora.Offset);
        siguiente.Should().BeAfter(domingo);
        siguiente.ToOffset(ahora.Offset).DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        var id = diferido.GetProperty("publicId").GetGuid();

        // Inmediato un domingo: no sale y dice desde cuándo se puede.
        using (conductor.Adelantar(domingo - ahora))
        {
            var inmediato = await InformativoAsync(http, admin, credito, persona, null);
            var fuera = await InventarioE2E.FallaAsync(inmediato, "Lending.DebtorMessage.OutsideLegalWindow");
            fuera.GetProperty("data").GetProperty("nextAllowedAt").GetString().Should().NotBeNullOrWhiteSpace();
            fuera.GetProperty("data").GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).Should().Contain("Sunday");

            await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, TareaDeMensajesAlDeudor.NombreDeLaTarea);
            (await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{id}")).GetProperty("message").GetProperty("status").GetInt32()
                .Should().Be((int)DebtorMessageEventKind.Deferred, "el domingo no se despacha");
        }

        // Llegado el día hábil siguiente, cartera.mensajes lo despacha.
        using (conductor.Adelantar(siguiente - ahora + TimeSpan.FromMinutes(5)))
            await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, TareaDeMensajesAlDeudor.NombreDeLaTarea);
        var despachado = await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{id}");
        despachado.GetProperty("message").GetProperty("status").GetInt32().Should()
            .BeOneOf([(int)DebtorMessageEventKind.Simulated, (int)DebtorMessageEventKind.Delivered], despachado.ToString());

        // La versión nueva de ABONOCAP rige para el mensaje siguiente; el anterior conserva la suya.
        var antes = despachado.GetProperty("message").GetProperty("template").GetProperty("version").GetInt32();
        var nueva = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Plantillas}/ABONOCAP/versions", new
        {
            purpose = "Informative", channel = "Sms", body = "{{NombreDeLaCooperativa}}: abonamos {{ValorAplicado}} a su credito {{NumeroDelCredito}}.",
            validFrom = esc.Hoy, reason = "Texto nuevo del abono a capital (desembolso)",
        });
        nueva.StatusCode.Should().Be(HttpStatusCode.Created, await nueva.Content.ReadAsStringAsync());
        var version = (await CarteraE2E.LeerAsync(nueva)).GetProperty("version").GetInt32();
        version.Should().Be(antes + 1);
        var otroDia = new DateTimeOffset(siguiente.ToOffset(ahora.Offset).Date.AddDays(1).AddHours(10), ahora.Offset);
        var siguienteMensaje = await InformativoAsync(http, admin, credito, persona, otroDia);
        siguienteMensaje.StatusCode.Should().Be(HttpStatusCode.Created, await siguienteMensaje.Content.ReadAsStringAsync());
        var leido = await CarteraE2E.LeerAsync(siguienteMensaje);
        leido.GetProperty("template").GetProperty("version").GetInt32().Should().Be(version, "el mensaje siguiente usa la versión nueva");
        (await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{id}")).GetProperty("message").GetProperty("template").GetProperty("version").GetInt32()
            .Should().Be(antes, "el anterior conserva la suya");
        (await CarteraE2E.GetAsync(http, admin, $"{Mensajes}/{leido.GetProperty("publicId").GetGuid()}")).GetProperty("body").GetString()
            .Should().Contain("abonamos");
    }

    /// <summary>Un informativo por SMS de <c>ABONOCAP</c> sobre el crédito; sin <paramref name="para"/> es inmediato.</summary>
    private static Task<HttpResponseMessage> InformativoAsync(HttpClient http, string token, Guid credito, Guid persona, DateTimeOffset? para) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Mensajes, new
        {
            loanPublicId = credito, recipientPersonPublicId = persona, channel = "Sms", purpose = "Informative", templateCode = "ABONOCAP", scheduledFor = para,
            variables = new Dictionary<string, string> { ["ValorAplicado"] = "$ 500.000", ["NuevaCuota"] = "$ 120.000", ["NuevoPlazo"] = "30 meses" },
        });

    /// <summary>La autorización del canal SMS (con su evidencia y la política de esa finalidad): sin ella un informativo se excluye.</summary>
    private static async Task AutorizarCanalAsync(HttpClient http, string admin, Guid persona)
    {
        (await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de datos: ContactChannel", contentMarkdown = "Texto de la política de contacto para las e2e del desembolso.",
            effectiveFrom = DateTime.UtcNow.AddMinutes(-1), purpose = "ContactChannel",
        })).IsSuccessStatusCode.Should().BeTrue();
        var evidencia = await OriginacionE2E.SubirEvidenciaAsync(http, admin, persona);
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/authorizations", new
        {
            personPublicId = persona, purpose = "ContactChannel", contactChannel = "Sms", evidenceAttachmentPublicId = evidencia, grantedAt = DateTime.UtcNow,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"autorización del canal: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Un instante del contrato: con zona, tal cual; sin ella, en hora de Colombia.</summary>
    private static DateTimeOffset Instante(string texto, TimeSpan zonaLocal) =>
        texto.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(texto, @"[+-]\d{2}:\d{2}$")
            ? DateTimeOffset.Parse(texto, System.Globalization.CultureInfo.InvariantCulture)
            : new DateTimeOffset(DateTime.Parse(texto, System.Globalization.CultureInfo.InvariantCulture), zonaLocal);

    // ---------------------------------------------------------------------------------------- escenario --

    /// <summary>La cooperativa «mensajes» activada, con su política de datos y un titular con celular y autorización general.</summary>
    private sealed record Escenario(CarteraE2E.CooperativaAislada Coop, DateOnly Hoy, Guid Titular);

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync()
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararAsync();
                Escenarios[fx] = tarea;
            }
            return tarea;
        }
    }

    private async Task<Escenario> PrepararAsync()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "mensajes", primerEjercicioContable: hoy.Year);
        await CarteraE2E.ActivarCarteraAsync(fx, coop);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;

        (await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de tratamiento de datos", contentMarkdown = "Texto de la política para las e2e de mensajes.", effectiveFrom = DateTime.UtcNow.AddMinutes(-1),
        })).IsSuccessStatusCode.Should().BeTrue("hace falta una política vigente para registrar la autorización");
        var titular = await CarteraE2E.PersonaAsync(http, admin, "Titular", "Mensajes", Movil);
        // La autorización general que traía el sistema anterior (origen migración, sin evidencia): basta para la entrega de un documento.
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/authorizations", new
        {
            personPublicId = titular, purpose = "GeneralTreatment", origin = "Migration", grantedAt = DateTime.UtcNow.AddDays(-30),
        });
        return new Escenario(coop, hoy, titular);
    }

    /// <summary>Un envío manual por SMS de la entrega de un documento; sin <paramref name="para"/> es inmediato. Exige 201.</summary>
    private static async Task<JsonElement> EnviarAsync(HttpClient http, Escenario esc, string documento, DateTimeOffset? para = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, Mensajes, new
        {
            recipientPersonPublicId = esc.Titular, channel = "Sms", purpose = "DocumentDelivery", templateCode = "ENVIODOC", scheduledFor = para,
            variables = new Dictionary<string, string>
            {
                ["ClaseDeDocumento"] = "Paz y salvo", ["NumeroDelDocumento"] = documento, ["CodigoDeVerificacion"] = "ABCD-EFGH-JKLM-NPQR",
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"enviar {documento}: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Una pasada de <c>cartera.mensajes</c> ahora, sin mirar si ya corrió en el último minuto (el disparo manual de la fixture).</summary>
    private Task DespacharAsync(Escenario esc) => fx.CorrerTareaAsync(esc.Coop.TenantPublicId, TareaDeMensajesAlDeudor.NombreDeLaTarea);

    private static async Task<int> EstadoAsync(HttpClient http, Escenario esc, Guid mensaje) =>
        (await CarteraE2E.GetAsync(http, esc.Coop.TokenAdmin, $"{Mensajes}/{mensaje}")).GetProperty("message").GetProperty("status").GetInt32();
}
