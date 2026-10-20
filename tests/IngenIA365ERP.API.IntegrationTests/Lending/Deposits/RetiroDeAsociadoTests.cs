using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T107 (US10, escenarios 1 a 4 de la spec; quickstart §2 US10; contracts/api.md §11; mensajes.md §5.1 y §5.3): el retiro del asociado por
/// HTTP, sobre la cooperativa del desembolso de la 013 (créditos activados, consejo con consejeros y secretaría) a la que se le activan aportes y
/// captaciones sin carga (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, mismo nombre <c>cap_…</c>):
/// <list type="number">
/// <item>La prueba independiente: 3.000.000 de aportes y un crédito de 2.500.000. Radica el administrador de la cooperativa; él mismo no lo aprueba
/// (<c>Lending.Segregation.Conflict</c>); el consejo decide el punto en un acta cerrada y otra persona registra la aprobación, que emite
/// <c>RetiroDeAsociadoAprobado</c>; la vista previa cruza el crédito (prelación 1, lo cancela) y la liquidación con su huella emite la orden de
/// devolución (<c>OrdenDeGiroEmitida</c> v2) por lo que sobra. Una huella vieja responde <c>Lending.Savings.PreviewChanged</c>.</item>
/// <item>Escenario 1: obligaciones mayores que los aportes: se cruza todo, la devolución es cero, el retiro queda pagado y el crédito conserva el saldo.</item>
/// <item>Escenario 2: descuento por pérdidas con punto de acta y la línea que lo admite: la línea <c>LossDeduction</c> y la devolución neta.</item>
/// <item>Escenario 3: con <c>Aportes.MinimoIrreducibleEntidad</c> que la devolución perforaría, el retiro se liquida y la devolución queda retenida, sin
/// orden (cooperativa propia: el parámetro es de toda la entidad).</item>
/// <item>Escenario 4: el reverso del pago del cruce desde la pantalla del crédito arrastra el débito de los aportes (vuelven a la cuenta) y el retiro
/// queda «por rehacer».</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class RetiroDeAsociadoTests(CentralIdentityApiFixture fx)
{
    private const string Retiros = "/api/lending/membership-withdrawals";
    private const string Cuentas = "/api/lending/accounts";
    private const string RutaDeLineas = "/api/lending/lines";
    private const string Aportes = "/api/lending/contributions";
    private const string Doble = "/api/lending/test-doubles/collector/accounts";

    private static int _documento;

    private sealed record Escenario(DesembolsoE2E.Escenario Des, CaptacionesE2E.CooperativaDeCaptaciones Cap, Guid LineaDeAportes)
    {
        public string Admin => Des.Coop.TokenAdmin;
        public DateOnly Hoy => Des.Hoy;
        public UsuariosDeCartera Usuarios => Des.Usuarios;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync(string nombre)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = ArmarAsync(nombre);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>La cooperativa del desembolso <c>cap_{nombre}</c> con aportes y captaciones activados y una línea de aportes que admite pérdidas.</summary>
    private async Task<Escenario> ArmarAsync(string nombre)
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, $"cap_{nombre}");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, nombre);
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId, "es la misma cooperativa aislada (cap_{nombre})");
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);

        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{RutaDeLineas}/contribution", new
        {
            code = "APRETIRO", name = "Aportes sociales (retiro e2e)", active = true,
            firstVersion = new
            {
                validFrom = des.Hoy, reason = "Estatutos, art. 20 (e2e del retiro)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                refundTermDays = 60, lossDeductionEnabled = true,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        var linea = (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
        return new Escenario(des, cap, linea);
    }

    // ------------------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Un asociado con fecha de nacimiento y las autorizaciones de tratamiento general y de estudio de crédito.</summary>
    private static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_800_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Retiro", personType = "01", dateOfBirth = new DateOnly(1980, 6, 1),
                mobile = "3001234560", email = $"{nombre.ToLowerInvariant()}.{documento}@retiro.test",
            },
            associate = new { joinDate = new DateOnly(2020, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "CreditProcessing");
        return persona;
    }

    /// <summary>Abre la cuenta de aportes y consigna <paramref name="aportes"/> como aporte extraordinario por el recaudador de prueba.</summary>
    private static async Task<Guid> ConAportesAsync(HttpClient http, Escenario esc, Guid persona, decimal aportes)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = esc.LineaDeAportes, holderPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct",
            parties = Array.Empty<object>(), beneficiaries = Array.Empty<object>(), markGmfExempt = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        var cuenta = (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
        var pago = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = $"TST:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = "Free", amount = aportes, valueDate = esc.Hoy,
                    meansClass = "Cash", depositorPublicId = persona,
                    receipt = new { number = "REC-RETIRO", date = esc.Hoy, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "RC" },
                },
            },
        });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(pago)).GetProperty("deposits")[0].GetProperty("result").GetInt32().Should().BeOneOf(1, 2);
        return cuenta;
    }

    /// <summary>Un crédito aprobado y desembolsado por cheque a la persona; devuelve el crédito.</summary>
    private static async Task<Guid> CreditoAsync(HttpClient http, Escenario esc, Guid persona, decimal monto)
    {
        var (solicitud, _) = await DesembolsoE2E.AprobadaAsync(http, esc.Des, persona, monto);
        var hecho = await DesembolsoE2E.DesembolsarAsync(http, esc.Des, DesembolsoE2E.Desembolso(esc.Des, solicitud, persona));
        return hecho.GetProperty("loanPublicId").GetGuid();
    }

    /// <summary>Radica el retiro con el administrador de la cooperativa; devuelve el trámite.</summary>
    private static async Task<Guid> RadicadoAsync(HttpClient http, Escenario esc, Guid persona)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Retiros, new
        {
            personPublicId = persona, cause = "Voluntary", requestedOn = esc.Hoy, supportAttachmentPublicIds = Array.Empty<Guid>(),
            note = "Carta de retiro voluntario (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"radicar el retiro: «{await resp.Content.ReadAsStringAsync()}»");
        var radicado = await CarteraE2E.LeerAsync(resp);
        radicado.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.Filed);
        return radicado.GetProperty("publicId").GetGuid();
    }

    /// <summary>El consejo decide el punto del retiro en un acta cerrada y el usuario «administrador» de Cartera (otra persona) registra la aprobación.</summary>
    private static async Task<(Guid Punto, JsonElement Aprobado)> AprobadoAsync(HttpClient http, Escenario esc, Guid retiro)
    {
        var punto = await ActasE2E.DecididoAsync(http, esc.Des, esc.Des.Originacion.Consejo, LendingDecisionSubjects.MembershipWithdrawal, retiro, esc.Hoy,
            "Decision");
        var aprueba = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Todos["administrador"]);
        var resp = await CarteraE2E.ConClaveAsync(http, aprueba, HttpMethod.Post, $"{Retiros}/{retiro}/approve", new
        {
            sessionItemPublicId = punto, effectiveDate = esc.Hoy, reason = "Aprobado por el consejo (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aprobar el retiro: «{await resp.Content.ReadAsStringAsync()}»");
        var aprobado = await CarteraE2E.LeerAsync(resp);
        aprobado.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.Approved);
        return (punto, aprobado);
    }

    private static object? Perdida(Guid? punto, decimal proporcion) =>
        punto is { } p ? new { sessionItemPublicId = p, year = 2025, proportion = proporcion } : null;

    private static Task<JsonElement> VistaPreviaAsync(HttpClient http, Escenario esc, Guid retiro, object? perdida = null) =>
        CarteraE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Retiros}/{retiro}/settlement/preview", new { date = esc.Hoy, lossDeduction = perdida });

    private static async Task<JsonElement> LiquidadoAsync(HttpClient http, Escenario esc, Guid retiro, string huella, object? perdida = null)
    {
        var resp = await LiquidarAsync(http, esc, retiro, huella, perdida);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"liquidar el retiro: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static Task<HttpResponseMessage> LiquidarAsync(HttpClient http, Escenario esc, Guid retiro, string huella, object? perdida = null) =>
        CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Retiros}/{retiro}/settle", new
        {
            date = esc.Hoy, expectedPreviewSha256 = huella, lossDeduction = perdida, refund = new { form = "DisbursementOrder" },
            reason = "Liquidación del retiro (e2e)",
        });

    private static IEnumerable<JsonElement> Lineas(JsonElement retiro, MembershipWithdrawalLineKind clase) =>
        retiro.GetProperty("lines").EnumerateArray().Where(l => l.GetProperty("kind").GetInt32() == (int)clase);

    // ----------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task US10_radicar_aprobar_con_acta_liquidar_con_cruce_y_ordenar_la_devolucion()
    {
        var esc = await EscenarioAsync("retiro");
        using var http = fx.CreateClient();
        var ana = await AsociadoAsync(http, esc.Admin, "Ana");
        await ConAportesAsync(http, esc, ana, 3_000_000m);
        var credito = await CreditoAsync(http, esc, ana, 2_500_000m);

        var retiro = await RadicadoAsync(http, esc, ana);
        var otro = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Retiros, new
        {
            personPublicId = ana, cause = "Voluntary", supportAttachmentPublicIds = Array.Empty<Guid>(),
        });
        (await CarteraE2E.CodigoDeErrorAsync(otro)).Should().Be("Lending.MembershipWithdrawal.AlreadyInProgress", "una solicitud viva por persona");

        // Quien radica no registra la aprobación (FR-005).
        var punto = await ActasE2E.DecididoAsync(http, esc.Des, esc.Des.Originacion.Consejo, LendingDecisionSubjects.MembershipWithdrawal, retiro, esc.Hoy,
            "Decision");
        var propia = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Retiros}/{retiro}/approve",
            new { sessionItemPublicId = punto, effectiveDate = esc.Hoy, reason = "Me lo apruebo (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict");

        var aprueba = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Todos["administrador"]);
        var aprobado = await CarteraE2E.ConClaveAsync(http, aprueba, HttpMethod.Post, $"{Retiros}/{retiro}/approve",
            new { sessionItemPublicId = punto, effectiveDate = esc.Hoy, reason = "Aprobado por el consejo (e2e)" });
        aprobado.StatusCode.Should().Be(HttpStatusCode.OK, await aprobado.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(aprobado)).GetProperty("approval").GetProperty("sessionItemPublicId").GetGuid().Should().Be(punto);
        (await PagosE2E.MensajesAsync(fx, esc.Des, retiro)).Should().Contain("RetiroDeAsociadoAprobado", "el aviso a Asociados nace con la aprobación");

        // La vista previa: el crédito se cruza primero y se cancela; lo que sobra se devuelve.
        var previa = await VistaPreviaAsync(http, esc, retiro);
        var cruce = previa.GetProperty("offsets").EnumerateArray().Should().ContainSingle().Subject;
        cruce.GetProperty("loanPublicId").GetGuid().Should().Be(credito);
        cruce.GetProperty("priority").GetInt32().Should().Be(1);
        cruce.GetProperty("paysOff").GetBoolean().Should().BeTrue();
        var cruzado = previa.GetProperty("crossed").GetDecimal();
        previa.GetProperty("contributions").GetDecimal().Should().Be(3_000_000m);
        previa.GetProperty("refund").GetDecimal().Should().Be(3_000_000m - cruzado);
        previa.GetProperty("entityMinimum").GetProperty("wouldBreach").GetBoolean().Should().BeFalse();

        var vieja = await LiquidarAsync(http, esc, retiro, new string('0', 64));
        (await CarteraE2E.CodigoDeErrorAsync(vieja)).Should().Be("Lending.Savings.PreviewChanged");

        var liquidado = await LiquidadoAsync(http, esc, retiro, previa.GetProperty("sha256").GetString()!);
        liquidado.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.Settled);
        var totales = liquidado.GetProperty("settlement");
        totales.GetProperty("crossed").GetDecimal().Should().Be(cruzado);
        totales.GetProperty("refund").GetDecimal().Should().Be(3_000_000m - cruzado);
        totales.GetProperty("refundHeldByInstitutionalMinimum").GetBoolean().Should().BeFalse();
        Lineas(liquidado, MembershipWithdrawalLineKind.LoanOffset).Should().OnlyContain(l => l.GetProperty("loanOperationNumber").ValueKind == JsonValueKind.String);
        var orden = liquidado.GetProperty("refundOrders").EnumerateArray().Should().ContainSingle().Subject;
        orden.GetProperty("number").GetString().Should().StartWith("OG-");
        orden.GetProperty("amount").GetDecimal().Should().Be(3_000_000m - cruzado);
        (await PagosE2E.MensajesAsync(fx, esc.Des, orden.GetProperty("publicId").GetGuid())).Should().Contain("OrdenDeGiroEmitida");
        (await PagosE2E.EstadoAsync(fx, esc.Des, credito)).Should().Be(LoanStatus.PaidOff, "el cruce canceló el crédito");
    }

    [Fact]
    public async Task US10_1_con_obligaciones_mayores_se_cruza_todo_la_devolucion_es_cero_y_el_credito_conserva_el_saldo()
    {
        var esc = await EscenarioAsync("retiro");
        using var http = fx.CreateClient();
        var beto = await AsociadoAsync(http, esc.Admin, "Beto");
        await ConAportesAsync(http, esc, beto, 1_000_000m);
        var credito = await CreditoAsync(http, esc, beto, 3_000_000m);
        var retiro = await RadicadoAsync(http, esc, beto);
        await AprobadoAsync(http, esc, retiro);

        var previa = await VistaPreviaAsync(http, esc, retiro);
        previa.GetProperty("crossed").GetDecimal().Should().Be(1_000_000m, "se cruza todo lo disponible");
        previa.GetProperty("refund").GetDecimal().Should().Be(0m);
        previa.GetProperty("offsets")[0].GetProperty("paysOff").GetBoolean().Should().BeFalse();

        var liquidado = await LiquidadoAsync(http, esc, retiro, previa.GetProperty("sha256").GetString()!);
        liquidado.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.Paid, "sin nada que devolver, el retiro queda terminado");
        liquidado.GetProperty("refundOrders").GetArrayLength().Should().Be(0);
        (await PagosE2E.EstadoAsync(fx, esc.Des, credito)).Should().Be(LoanStatus.Active, "el crédito conserva el saldo restante con sus garantías");
    }

    [Fact]
    public async Task US10_2_el_descuento_por_perdidas_con_acta_deja_su_linea_y_reduce_la_devolucion()
    {
        var esc = await EscenarioAsync("retiro");
        using var http = fx.CreateClient();
        var carla = await AsociadoAsync(http, esc.Admin, "Carla");
        await ConAportesAsync(http, esc, carla, 2_000_000m);
        var retiro = await RadicadoAsync(http, esc, carla);
        var (punto, _) = await AprobadoAsync(http, esc, retiro);

        var perdida = Perdida(punto, 0.1m);
        var previa = await VistaPreviaAsync(http, esc, retiro, perdida);
        previa.GetProperty("lossDeduction").GetProperty("amount").GetDecimal().Should().Be(200_000m, "el 10 % de los aportes");
        previa.GetProperty("refund").GetDecimal().Should().Be(1_800_000m);

        var liquidado = await LiquidadoAsync(http, esc, retiro, previa.GetProperty("sha256").GetString()!, perdida);
        Lineas(liquidado, MembershipWithdrawalLineKind.LossDeduction).Sum(l => l.GetProperty("amount").GetDecimal()).Should().Be(-200_000m);
        liquidado.GetProperty("settlement").GetProperty("lossDeduction").GetDecimal().Should().Be(200_000m);
        liquidado.GetProperty("settlement").GetProperty("refund").GetDecimal().Should().Be(1_800_000m);
        (await PagosE2E.MensajesAsync(fx, esc.Des, retiro)).Should().Contain("RetiroDeAsociadoAprobado");
    }

    [Fact]
    public async Task US10_3_la_devolucion_que_perforaria_el_minimo_irreducible_queda_retenida_y_el_retiro_se_liquida()
    {
        var esc = await EscenarioAsync("retiro_minimo");
        using var http = fx.CreateClient();
        await CarteraE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/lending/parameters/Aportes.MinimoIrreducibleEntidad/versions", new
        {
            scopeKind = 0, value = "1000000000000", validFrom = esc.Hoy, reason = "Mínimo irreducible de la entidad, estatutos (e2e)",
        });
        var dora = await AsociadoAsync(http, esc.Admin, "Dora");
        await ConAportesAsync(http, esc, dora, 500_000m);
        var retiro = await RadicadoAsync(http, esc, dora);
        await AprobadoAsync(http, esc, retiro);

        var previa = await VistaPreviaAsync(http, esc, retiro);
        previa.GetProperty("entityMinimum").GetProperty("wouldBreach").GetBoolean().Should().BeTrue();
        var liquidado = await LiquidadoAsync(http, esc, retiro, previa.GetProperty("sha256").GetString()!);
        liquidado.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.Settled, "se rechaza la devolución, no el retiro");
        liquidado.GetProperty("settlement").GetProperty("refundHeldByInstitutionalMinimum").GetBoolean().Should().BeTrue();
        liquidado.GetProperty("refundOrders").GetArrayLength().Should().Be(0, "la devolución queda pendiente");

        // Mientras el capital siga por debajo, aprobar la devolución responde el mínimo de la entidad.
        var aprueba = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Todos["administrador"]);
        var devolver = await CarteraE2E.ConClaveAsync(http, aprueba, HttpMethod.Post, $"{Retiros}/{retiro}/refund/approve",
            new { reason = "Ordenar la devolución (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(devolver)).Should().Be("Lending.Contributions.EntityMinimumBreached");
    }

    [Fact]
    public async Task US10_4_el_reverso_del_pago_del_cruce_devuelve_los_aportes_y_deja_el_retiro_por_rehacer()
    {
        var esc = await EscenarioAsync("retiro");
        using var http = fx.CreateClient();
        var elsa = await AsociadoAsync(http, esc.Admin, "Elsa");
        await ConAportesAsync(http, esc, elsa, 3_000_000m);
        var credito = await CreditoAsync(http, esc, elsa, 2_500_000m);
        var retiro = await RadicadoAsync(http, esc, elsa);
        await AprobadoAsync(http, esc, retiro);
        var previa = await VistaPreviaAsync(http, esc, retiro);
        var liquidado = await LiquidadoAsync(http, esc, retiro, previa.GetProperty("sha256").GetString()!);
        var cruzado = liquidado.GetProperty("settlement").GetProperty("crossed").GetDecimal();
        var numeroDelPago = Lineas(liquidado, MembershipWithdrawalLineKind.LoanOffset).First().GetProperty("loanOperationNumber").GetString();

        // Desde la pantalla del crédito: vista previa y reverso del pago del cruce.
        var pago = (await PagosE2E.OperacionesAsync(fx, esc.Des, credito)).Single(o => o.Numero == numeroDelPago);
        var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        var vista = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{credito}/operations/{pago.PublicId}/reversal-preview",
            new { reason = "Cruce aplicado por error (e2e)" });
        var reverso = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{credito}/operations/{pago.PublicId}/reverse",
            new { reason = "Cruce aplicado por error (e2e)", expectedPreviewSha256 = vista.GetProperty("previewSha256").GetString() });
        reverso.StatusCode.Should().Be(HttpStatusCode.Created, await reverso.Content.ReadAsStringAsync());

        var ahora = await CarteraE2E.GetAsync(http, esc.Admin, $"{Retiros}/{retiro}");
        ahora.GetProperty("status").GetInt32().Should().Be((int)MembershipWithdrawalStatus.ToRedo, "la liquidación queda por rehacer (US10-4)");
        ahora.GetProperty("toRedoReason").GetString().Should().Contain(numeroDelPago!);
        var resumen = await CarteraE2E.GetAsync(http, esc.Admin, $"{Aportes}/summary?personPublicId={elsa}");
        resumen.GetProperty("total").GetDecimal().Should().Be(cruzado, "el reverso arrastra el débito: lo cruzado vuelve a los aportes");
        (await PagosE2E.EstadoAsync(fx, esc.Des, credito)).Should().Be(LoanStatus.Active, "el crédito vuelve a deber");

        // Por rehacer, se vuelve a liquidar sobre el mismo trámite.
        var otraVez = await VistaPreviaAsync(http, esc, retiro);
        otraVez.GetProperty("crossed").GetDecimal().Should().BeGreaterThan(0m);
    }
}
