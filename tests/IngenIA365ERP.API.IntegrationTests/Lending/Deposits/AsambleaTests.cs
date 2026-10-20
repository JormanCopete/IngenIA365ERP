using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T206 (US12, escenarios 1 y 2 de la spec; contracts/api.md §12; research §22): la asamblea por HTTP, sobre la cooperativa del desembolso de
/// la 013 con aportes y captaciones activados (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, nombre <c>cap_asamblea</c>). Ana y Beto aportan
/// hoy; el reloj de la suite pasa al 20 de marzo del año siguiente (la asamblea ordinaria) y la decisión es sobre el ejercicio de hoy.
/// <list type="number">
/// <item>US12-1: revalorizar al 5 % sobre el saldo promedio con el IPC del ejercicio (valor de la central) y un fondo menor que lo calculado: la simulación
/// informa el límite y el prorrateo, y la suma de los abonos es el fondo. Quien registra no aplica; otra persona aplica con el punto del acta cerrada, el
/// acta subida como soporte y la huella de la simulación.</item>
/// <item>US12-2: aplicarla otra vez se rechaza (<c>AlreadyApplied</c>); se reversa entera y se registra otra que la corrige, con la misma acta.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase. Exige <c>EntregaVigente</c> ≥ K10.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class AsambleaTests(CentralIdentityApiFixture fx)
{
    private const string Asamblea = "/api/lending/assembly-distributions";
    private const string Cuentas = "/api/lending/accounts";
    private const string RutaDeLineas = "/api/lending/lines";
    private const string Doble = "/api/lending/test-doubles/collector/accounts";

    private static int _documento;

    [Fact]
    public async Task US12_revalorizacion_limitada_por_el_IPC_y_prorrateada_al_fondo_aplicada_con_acta_reversada_y_corregida()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Ana"), 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;

        // El IPC del ejercicio en la central, replicado a la cooperativa.
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "IPC", key = "ANUAL", validFrom = des.Hoy, referenceDate = des.Hoy, value = 0.04m, unit = "Fraccion", source = "DANE, boletín de prueba (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, des.Coop.TenantPublicId);

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;

            // Registra el administrador de la cooperativa.
            var registrada = await RegistrarAsync(http, admin, ejercicio, 0.05m, 9_000m, null);
            var decision = registrada.GetProperty("publicId").GetGuid();
            registrada.GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Simulated);

            // US12-1: la simulación se limita al IPC si lo supera y se prorratea al fondo.
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            var ipc = simulacion.GetProperty("limitedByIpc").GetProperty("ipc").GetDecimal();
            simulacion.GetProperty("appliedRate").GetDecimal().Should().Be(Math.Min(0.05m, ipc));
            simulacion.GetProperty("proratedToFund").GetProperty("applied").GetBoolean().Should().BeTrue("lo calculado supera el fondo de 9.000");
            simulacion.GetProperty("totals").GetProperty("applied").GetDecimal().Should().Be(9_000m, "la suma de los abonos es el fondo aprobado");
            simulacion.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("appliedAmount").GetDecimal()).Should().Be(9_000m);
            var huella = simulacion.GetProperty("sha256").GetString()!;

            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea,
                "Decision");
            var soporte = await SoporteAsync(http, admin, decision);

            // Quien registra no aplica (FR-005); otra persona sí.
            var propia = await AplicarAsync(http, admin, decision, punto, huella, soporte);
            (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict");
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, huella, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var dto = await CarteraE2E.LeerAsync(aplicada);
            dto.GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Applied);
            dto.GetProperty("appliedTotal").GetDecimal().Should().Be(9_000m);
            (await PagosE2E.MensajesAsync(fx, des, decision)).Should().Contain("RevalorizacionDeAportesAplicada");

            // US12-2: no se aplica dos veces; se reversa entera y se corrige con otra que hereda el acta.
            var otraVez = await AplicarAsync(http, aplica, decision, punto, huella, soporte);
            (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.AssemblyDistribution.AlreadyApplied");
            var reversada = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{decision}/reverse",
                new { reason = "El fondo del acta era otro (e2e)" });
            reversada.StatusCode.Should().Be(HttpStatusCode.OK, await reversada.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(reversada)).GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Reversed);

            var correccion = await RegistrarAsync(http, admin, ejercicio, 0.03m, 1_000_000m, decision);
            var corrige = correccion.GetProperty("publicId").GetGuid();
            correccion.GetProperty("sessionItemPublicId").GetGuid().Should().Be(punto, "la corrección hereda el acta");
            var otra = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{corrige}/simulate");
            var corregida = await AplicarAsync(http, aplica, corrige, null, otra.GetProperty("sha256").GetString()!, await SoporteAsync(http, admin, corrige));
            corregida.StatusCode.Should().Be(HttpStatusCode.OK, await corregida.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(corregida)).GetProperty("correctsPublicId").GetGuid().Should().Be(decision);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 93 del dueño (E257): los excedentes van a ahorro y Beto no tiene cuenta de ahorro: la decisión no se bloquea, a Ana se le abona en su ahorro y
    /// el de Beto se capitaliza en sus aportes, sin abrirle ninguna cuenta.
    /// </summary>
    [Fact]
    public async Task E257_los_excedentes_a_ahorro_de_quien_no_tiene_cuenta_se_capitalizan_en_sus_aportes()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_93");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_93");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        var ana = await AsociadoAsync(http, admin, "Ana");
        var beto = await AsociadoAsync(http, admin, "Beto");
        await ConAportesAsync(http, admin, des, linea, ana, 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, beto, 2_000_000m);
        var ahorro = await MovimientosTests.LineaALaVistaAsync(http, admin, des.Hoy, "AHEXCED");
        await MovimientosTests.CuentaAsync(http, admin, des.Usuarios.O1.AgenciaPublicId, ahorro, ana);
        var ejercicio = des.Hoy.Year;

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 30_000m, percent = 0.01m, @base = "YearEndBalance", destination = "Savings",
                targetDepositLineCode = "AHEXCED", reason = "Asamblea general ordinaria (e2e de la respuesta 93)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var decision = (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            simulacion.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("se capitaliza en sus aportes"));
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var soporte = await SoporteAsync(http, admin, decision);
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var lineas = (await CarteraE2E.LeerAsync(aplicada)).GetProperty("lines").EnumerateArray().ToList();
            lineas.Should().HaveCount(2);
            lineas.Count(l => l.GetProperty("destinationAccountNumber").ValueKind == JsonValueKind.Null).Should().Be(1, "Beto no tiene ahorro: capitaliza");
            var cuentasDeBeto = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.MemberAccounts.AsNoTracking()
                .CountAsync(a => a.Nature == LineNature.Deposit && db.People.Any(p => p.PublicId == beto && p.Id == a.HolderPersonId)));
            cuentasDeBeto.Should().Be(0, "no se le abrió ninguna cuenta");
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 92 del dueño (E258): los excedentes se pagan por orden de giro y Tesorería rechaza la de Ana: no hay saldo que reintegrar (sigue por pagar a su favor),
    /// la orden se regira por HTTP —una sola vez— y el regiro se gira.
    /// </summary>
    [Fact]
    public async Task E258_el_giro_rechazado_de_un_excedente_se_regira_por_HTTP()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_92");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_92");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Ana"), 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 30_000m, percent = 0.01m, @base = "YearEndBalance", destination = "Payment",
                reason = "Asamblea general ordinaria (e2e de la respuesta 92)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var decision = (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var soporte = await SoporteAsync(http, admin, decision);
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());

            var ordenes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.Purpose == PayoutPurpose.SurplusPayment).OrderBy(o => o.Id).Select(o => new { o.PublicId, o.Amount }).ToListAsync());
            ordenes.Should().HaveCount(2);
            var rechazo = await DesembolsoE2E.ResultadoDelGiroAsync(http, des, ordenes[0].PublicId, "Rejected", ordenes[0].Amount);
            await DesembolsoE2E.ConsumirAsync(fx, des, rechazo);

            var ruta = $"{Asamblea}/{decision}/orders/{ordenes[0].PublicId}/reissue";
            var regiro = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, ruta, new { reason = "Cuenta bancaria corregida (e2e)" });
            regiro.StatusCode.Should().Be(HttpStatusCode.OK, await regiro.Content.ReadAsStringAsync());
            var nueva = await CarteraE2E.LeerAsync(regiro);
            nueva.GetProperty("amount").GetDecimal().Should().Be(ordenes[0].Amount);
            (await CarteraE2E.CodigoDeErrorAsync(await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, ruta, new { reason = "Otra vez (e2e)" })))
                .Should().Be("Lending.PayoutOrder.NotReissuable");
            var sinRechazo = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{decision}/orders/{ordenes[1].PublicId}/reissue",
                new { reason = "No aplica (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(sinRechazo)).Should().Be("Lending.PayoutOrder.NotReissuable");

            var giro = await DesembolsoE2E.ResultadoDelGiroAsync(http, des, nueva.GetProperty("orderPublicId").GetGuid(), "Disbursed", ordenes[0].Amount);
            await DesembolsoE2E.ConsumirAsync(fx, des, giro);
            var estado = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.PublicId == nueva.GetProperty("orderPublicId").GetGuid()).Select(o => o.Status).SingleAsync());
            estado.Should().Be(DisbursementOrderStatus.Disbursed);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    // ------------------------------------------------------------------------------------------------------------ apoyo --

    private static async Task<JsonElement> RegistrarAsync(HttpClient http, string token, int ejercicio, decimal porcentaje, decimal fondo, Guid? corrige)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Asamblea, new
        {
            kind = "Revaluation", year = ejercicio, fundAmount = fondo, percent = porcentaje, @base = "AverageBalance", correctsPublicId = corrige,
            reason = "Asamblea general ordinaria (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar la decisión: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static Task<HttpResponseMessage> AplicarAsync(HttpClient http, string token, Guid decision, Guid? punto, string huella, Guid soporte) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Asamblea}/{decision}/apply", new
        {
            sessionItemPublicId = punto, expectedPreviewSha256 = huella, supportAttachmentPublicIds = new[] { soporte }, reason = "Según el acta (e2e)",
        });

    private static async Task<Guid> LineaDeAportesAsync(HttpClient http, string admin, DateOnly hoy)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{RutaDeLineas}/contribution", new
        {
            code = "APASAMB", name = "Aportes sociales (asamblea e2e)", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos (e2e de la asamblea)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                refundTermDays = 60,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    private static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_900_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Asamblea", personType = "01", dateOfBirth = new DateOnly(1980, 6, 1),
                mobile = "3001234560", email = $"{nombre.ToLowerInvariant()}.{documento}@asamblea.test",
            },
            associate = new { joinDate = new DateOnly(2020, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        return persona;
    }

    private static async Task ConAportesAsync(HttpClient http, string admin, DesembolsoE2E.Escenario des, Guid linea, Guid persona, decimal aportes)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = persona, officePublicId = des.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct",
            parties = Array.Empty<object>(), beneficiaries = Array.Empty<object>(), markGmfExempt = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        var cuenta = (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
        var pago = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = $"TST:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = "Free", amount = aportes, valueDate = des.Hoy,
                    meansClass = "Cash", depositorPublicId = persona,
                    receipt = new { number = "REC-ASAMBLEA", date = des.Hoy, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "RC" },
                },
            },
        });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());
    }

    /// <summary>Sube el acta firmada como soporte de la decisión, como el navegador: pedir, enviar al almacén local, confirmar.</summary>
    private static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid decision)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, decision, contenido, "acta-asamblea.pdf", ownerEntityType: "LendingAssemblySupport");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "acta-asamblea.pdf");
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        using var almacen = await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el acta: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }
}
