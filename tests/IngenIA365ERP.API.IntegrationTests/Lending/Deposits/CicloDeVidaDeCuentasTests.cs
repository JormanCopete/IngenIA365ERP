using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T213 (US14, escenarios 1 y 2 de la spec; contracts/api.md §13 y §14; research §23): el ciclo de vida de las cuentas por HTTP, sobre una
/// cooperativa con captaciones activadas (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, nombre <c>cap_ciclo_*</c>).
/// <list type="number">
/// <item>US14-1: una cuenta a la vista con 30 días de inactividad en su línea pasa a inactiva con el ciclo diario del día 30; el ciclo del fin del mes abona
/// sus intereses y sigue inactiva (el abono no es movimiento del titular); una consignación del titular la reactiva.</item>
/// <item>US14-2: el titular fallecido con dos beneficiarios al 50 %: registrar el fallecimiento bloquea la cuenta; la entrega a los beneficiarios la
/// registra una persona y la aprueba otra; salen dos órdenes de giro y la cuenta queda en cierre hasta que se paguen (lo que sigue al giro lo prueban las
/// pruebas de aplicación, <c>CicloDeVidaDeCuentasTests</c>).</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase. Exige <c>EntregaVigente</c> ≥ K10.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class CicloDeVidaDeCuentasTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Lineas = "/api/lending/lines";
    private const string Fallecimientos = "/api/lending/deceased-cases";

    [Fact]
    public async Task US14_1_inactiva_a_los_dias_de_la_linea_el_interes_no_la_reactiva_y_la_consignacion_si()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "ciclo_inactiva");
        using var http = fx.CreateClient();
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        var linea = await LineaConInactividadAsync(http, coop.TokenAdmin, hoy, "AHINACT");
        var titular = await MovimientosTests.AsociadoAsync(http, coop.TokenAdmin, "Inactiva");
        var cuenta = await MovimientosTests.CuentaAsync(http, coop.TokenAdmin, usuarios.O1.AgenciaPublicId, linea, titular);
        await MovimientosTests.ConsignarAsync(http, coop.TokenAdmin, cuenta, titular, 2_000_000m, hoy, medio: "Transfer");

        var dia30 = hoy.AddDays(30);
        using (CausacionE2E.RelojEn(fx, dia30))
            await CausacionE2E.CicloDeHoyAsync(fx, http, coop.TokenAdmin, coop.TenantPublicId);
        (await EstadoAsync(http, coop.TokenAdmin, cuenta)).Should().Be((int)MemberAccountStatus.Inactive);

        // El fin del mes liquida y abona los intereses: sigue inactiva.
        var finDeMes = new DateOnly(dia30.Year, dia30.Month, DateTime.DaysInMonth(dia30.Year, dia30.Month));
        if (finDeMes > dia30)
            using (CausacionE2E.RelojEn(fx, finDeMes))
                await CausacionE2E.CicloDeHoyAsync(fx, http, coop.TokenAdmin, coop.TenantPublicId);
        (await EstadoAsync(http, coop.TokenAdmin, cuenta)).Should().Be((int)MemberAccountStatus.Inactive, "el abono de intereses no es movimiento del titular");
        var reclasificadas = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, db => db.MemberAccountOperations.AsNoTracking()
            .CountAsync(o => o.Kind == MemberAccountOperationKind.Reclassification && o.Payload.Contains("\"cause\":\"Inactivity\"")));
        reclasificadas.Should().Be(1);

        // Una consignación del titular la reactiva (con su reclasificación de vuelta).
        using (CausacionE2E.RelojEn(fx, finDeMes.AddDays(1)))
        {
            await MovimientosTests.ConsignarAsync(http, coop.TokenAdmin, cuenta, titular, 10_000m, finDeMes.AddDays(1), medio: "Transfer");
            (await EstadoAsync(http, coop.TokenAdmin, cuenta)).Should().Be((int)MemberAccountStatus.Active);
        }
    }

    [Fact]
    public async Task US14_2_fallecido_con_dos_beneficiarios_al_50_aprobado_por_otra_persona_genera_dos_ordenes_de_giro()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_ciclo_fallece");
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "ciclo_fallece");
        coop.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var hoy = CarteraE2E.Hoy(fx);
        var linea = await LineaConInactividadAsync(http, admin, hoy, "AHFALLE");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Fallecido");
        var hija = await MovimientosTests.AsociadoAsync(http, admin, "Hija");
        var hijo = await MovimientosTests.AsociadoAsync(http, admin, "Hijo");

        var constancia = await SoporteAsync(http, admin, titular, "LendingAccountPartySupport", "constancia-beneficiarios.pdf");
        var abierta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = titular, officePublicId = des.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct",
            parties = Array.Empty<object>(), beneficiaries = new[] { new { personPublicId = hija, percent = 50m }, new { personPublicId = hijo, percent = 50m } },
            beneficiaryRecordAttachmentPublicId = constancia, markGmfExempt = false,
        });
        abierta.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta: «{await abierta.Content.ReadAsStringAsync()}»");
        var cuenta = (await CarteraE2E.LeerAsync(abierta)).GetProperty("publicId").GetGuid();
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 1_000_000m, hoy, medio: "Transfer");

        // Registrar el fallecimiento bloquea la cuenta.
        var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Fallecimientos, new
        {
            personPublicId = titular, dateOfDeath = hoy, deathCertificateAttachmentPublicId = await SoporteAsync(http, admin, titular, "LendingDeceasedSupport",
                "registro-civil.pdf"),
            reason = "Lo informa la familia (e2e)",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var caso = await CarteraE2E.LeerAsync(registro);
        var casoId = caso.GetProperty("publicId").GetGuid();
        caso.GetProperty("beneficiaries").GetArrayLength().Should().Be(2);
        var detalle = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}");
        detalle.GetProperty("marks").GetProperty("deceasedHolder").GetBoolean().Should().BeTrue();

        // La entrega a los beneficiarios: la registra el administrador y la aprueba otra persona.
        var previa = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Fallecimientos}/{casoId}/delivery/preview", new { mode = "Beneficiaries" });
        previa.GetProperty("portions").EnumerateArray().Select(p => p.GetProperty("amount").GetDecimal()).Should().AllBeEquivalentTo(500_000m);
        var entrega = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Fallecimientos}/{casoId}/delivery", new
        {
            mode = "Beneficiaries", supportAttachmentPublicIds = Array.Empty<Guid>(), expectedPreviewSha256 = previa.GetProperty("sha256").GetString(),
            reason = "Entrega a los beneficiarios (e2e)",
        });
        entrega.StatusCode.Should().Be(HttpStatusCode.OK, await entrega.Content.ReadAsStringAsync());

        var propia = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Fallecimientos}/{casoId}/delivery/approve", new { reason = "Aprobada" });
        (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict");
        var otra = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
        var aprobada = await CarteraE2E.ConClaveAsync(http, otra, HttpMethod.Post, $"{Fallecimientos}/{casoId}/delivery/approve", new { reason = "Aprobada (e2e)" });
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        var aprobado = await CarteraE2E.LeerAsync(aprobada);
        aprobado.GetProperty("status").GetInt32().Should().Be((int)DeceasedCaseStatus.Approved);
        aprobado.GetProperty("deliveries").GetArrayLength().Should().Be(2, "una orden de giro por beneficiario");

        var ordenes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
            .Where(o => o.Purpose == PayoutPurpose.DeceasedDelivery).Select(o => o.Amount).ToListAsync());
        ordenes.Should().HaveCount(2).And.OnlyContain(v => v == 500_000m);
        (await EstadoAsync(http, admin, cuenta)).Should().Be((int)MemberAccountStatus.Closing, "se cancela cuando se paguen las dos órdenes");
    }

    // ------------------------------------------------------------------------------------------------------------ apoyo --

    private static async Task<int> EstadoAsync(HttpClient http, string token, Guid cuenta) =>
        (await CarteraE2E.GetAsync(http, token, $"{Cuentas}/{cuenta}")).GetProperty("status").GetInt32();

    /// <summary>Una línea a la vista al 1 % con 30 días de inactividad que sigue causando inactiva.</summary>
    private static async Task<Guid> LineaConInactividadAsync(HttpClient http, string admin, DateOnly hoy, string codigo)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo, name = $"Ahorro {codigo}", depositClass = "Demand", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Reglamento de ahorro (e2e del ciclo de vida)", allowsPartialWithdrawals = true,
                rate = new { kind = "Fixed", effectiveAnnual = 0.01m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual365", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = false },
                inactivity = new { days = 30, keepsAccruing = true, suspendsOnTotalGarnishment = false, suspendsOnDeceased = true },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>Sube un soporte con dueño la persona, como el navegador: pedir, enviar al almacén local, confirmar.</summary>
    private static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid persona, string clase, string nombre)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, persona, contenido, nombre, ownerEntityType: clase);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, nombre);
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        using var almacen = await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el soporte: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }
}
