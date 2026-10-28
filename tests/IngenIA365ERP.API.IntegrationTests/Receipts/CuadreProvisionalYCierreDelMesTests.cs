using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0319 (quickstart recorrido L pasos 3–4; SC-029; FR-158, FR-228; decisión del dueño: cuadre provisional con aprobación y fecha límite): la bóveda y la
/// cuenta bancaria cargadas al corte no cuadran contra un libro en cero; Recaudo y Tesorería se activan con el <b>cuadre provisional</b> —con su permiso (sin él, 404),
/// su motivo y una fecha límite posterior al corte—; mientras siga provisional, cada día alerta (<c>Recaudo.CuadreProvisionalPendiente</c>,
/// <c>Tesoreria.CuadreProvisionalPendiente</c>) y Contabilidad niega el cierre del mes de la activación (<c>Accounting.Period.OpeningSquarePending</c>, que no se
/// reconoce). La contadora corrige la contabilidad con los saldos de apertura al corte, el definitivo cuadra y se registra, sus alertas se cierran y el mes cierra. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class CuadreProvisionalYCierreDelMesTests(CentralIdentityApiFixture fx)
{
    private const string CuentaDeBoveda = "11050581";   // 110505 CAJA GENERAL: la bóveda de la oficina principal (rol Boveda de la matriz de Recaudo)
    private const string Patrimonio = "31050581";        // 310505 APORTES SOCIALES: la contrapartida de la apertura

    [Fact]
    public async Task Se_activa_con_cuadre_provisional_que_alerta_y_niega_el_cierre_hasta_registrar_el_definitivo()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "cuadre_provisional", contabilidadIniciada: true);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var corte = hoy.AddDays(-1);

        // Lo cargado al corte: 6.000.000 en la bóveda y 1.500.000 en la cuenta propia; el libro, en cero.
        await PuestaEnMarchaE2E.BovedaAsync(http, admin, coop);
        await InventarioE2E.RevisarYAplicarAsync(http, admin, PuestaEnMarchaE2E.Recaudo(PuestaEnMarchaE2E.Bovedas),
            PuestaEnMarchaE2E.LibroDeBovedas(corte, (50_000m, 100), (20_000m, 50), saldo: 6_000_000m));
        await PuestaEnMarchaE2E.CuentasYPendientesAsync(http, coop, corte, saldo: 1_500_000m, documentoDelBeneficiario: string.Empty, chequeEnTransito: 0m, partida: 0m);
        foreach (var modulo in new[] { "receipts", "treasury" })
            (await CuadreAsync(http, admin, modulo, corte)).GetProperty("squares").GetBoolean().Should().BeFalse($"{modulo}: lo cargado no está en el libro");

        // El provisional exige su permiso (sin él la ruta no existe), su motivo y una fecha límite posterior al corte.
        var (sinPermiso, _) = await ListaDeGoNoGoTests.UsuarioConPermisosAsync(fx, http, coop, "RCDVER", "Receipts.Activation.View", "Treasury.Activation.View");
        var ajeno = await CarteraE2E.ConClaveAsync(http, sinPermiso, HttpMethod.Post, "/api/receipts/opening-squares/provisional",
            new { reason = "No me toca (e2e)", deadlineOn = corte.AddDays(7), cutoffDate = corte });
        ajeno.StatusCode.Should().Be(HttpStatusCode.NotFound, await ajeno.Content.ReadAsStringAsync());
        var sinMotivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/opening-squares/provisional",
            new { reason = "", deadlineOn = corte.AddDays(7), cutoffDate = corte });
        ((int)sinMotivo.StatusCode).Should().BeOneOf(400, 422);
        var limiteVencida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/opening-squares/provisional",
            new { reason = "Diferencia en revisión con la contadora (e2e)", deadlineOn = corte, cutoffDate = corte });
        limiteVencida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await limiteVencida.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(limiteVencida)).Should().Be("Receipts.Activation.ProvisionalDeadlineInvalid");
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var provisional = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/{modulo}/opening-squares/provisional",
                new { reason = "Diferencia en revisión con la contadora (e2e)", deadlineOn = corte.AddDays(7), cutoffDate = corte });
            provisional.StatusCode.Should().Be(HttpStatusCode.OK, await provisional.Content.ReadAsStringAsync());
            var registrado = await RecaudoYTesoreriaE2E.LeerAsync(provisional);
            registrado.GetProperty("status").GetString().Should().Be("ProvisionalApproved");
            registrado.GetProperty("totalDifference").GetDecimal().Should().NotBe(0m, "el provisional guarda la diferencia que aceptó");
            registrado.GetProperty("deadlineOn").GetString().Should().Be(corte.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            registrado.GetProperty("approvedBy").GetString().Should().NotBeNullOrWhiteSpace();
        }

        // La activación toma el provisional.
        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, coop, fx, corte);
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            (await CarteraE2E.GetAsync(http, admin, $"/api/{modulo}/activation/requirements")).GetProperty("status").GetString().Should().Be("Active", modulo);
            (await CuadreAsync(http, admin, modulo, corte)).GetProperty("pendingProvisional").ValueKind.Should().Be(JsonValueKind.Object, $"{modulo} sigue con el provisional");
        }

        // Cada día alerta mientras siga provisional.
        await fx.CorrerTareaAsync(coop.TenantPublicId, "recaudo.antiguedad");
        await fx.CorrerTareaAsync(coop.TenantPublicId, "tesoreria.vigilancia");
        (await AlertasPendientesAsync(http, admin, "Recaudo.CuadreProvisionalPendiente")).Should().NotBeEmpty("la tarea diaria de Recaudo alerta el provisional");
        (await AlertasPendientesAsync(http, admin, "Tesoreria.CuadreProvisionalPendiente")).Should().NotBeEmpty("la tarea diaria de Tesorería alerta el provisional");

        // Contabilidad niega el cierre del mes de la activación, aunque se reconozca todo lo reconocible.
        var cierre = await CerrarMesAsync(http, admin, hoy);
        cierre.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await cierre.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(cierre)).Should().Be("Accounting.Period.OpeningSquarePending");

        // Segunda ronda del 2026-10-27 (D-g, T0493; FR-232): el definitivo con diferencia exige el acta como adjunto de su tipo, de la configuración del módulo; un
        // identificador cualquiera no sirve; con el acta queda aceptado con su diferencia, y el acta citada ya no se borra.
        var conDiferencia = await CuadreAsync(http, admin, "receipts", corte);
        conDiferencia.GetProperty("squares").GetBoolean().Should().BeFalse();
        var duenoDelActa = conDiferencia.GetProperty("minutesOwnerPublicId").GetGuid();
        var actaInventada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/opening-squares/definitive",
            new { cutoffDate = corte, minutesAttachmentPublicId = Guid.NewGuid() });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(actaInventada)).Should().Be("Receipts.Activation.MinutesInvalid");
        var acta = await Attachments.AdjuntosE2E.SubirAsync(http, admin, duenoDelActa, Encoding.UTF8.GetBytes("%PDF-1.4 acta del cuadre de apertura (e2e)"), "acta-cuadre.pdf",
            "application/pdf", "OpeningSquareMinutes");
        acta.EstadoDelAlmacen.Should().Be(HttpStatusCode.NoContent);
        var conActa = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/opening-squares/definitive",
            new { cutoffDate = corte, minutesAttachmentPublicId = acta.Adjunto });
        conActa.StatusCode.Should().Be(HttpStatusCode.OK, await conActa.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.LeerAsync(conActa)).GetProperty("status").GetString().Should().Be("AcceptedWithMinutes");
        var borrar = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Delete, $"/api/attachments/{acta.Adjunto}", null);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(borrar)).Should().Be("Attachments.OwnerLocked", "el acta citada por un cuadre se conserva");

        // La contadora corrige la contabilidad: la bóveda en su cuenta (regla del rol Boveda) y la cuenta propia en su auxiliar, como saldos de apertura al corte.
        await ContabilidadE2E.CrearCuentaAsync(http, admin, CuentaDeBoveda, "Bóveda de la oficina principal (e2e)", "110505", ["CNT", "REC"]);
        await ContabilidadE2E.CrearCuentaAsync(http, admin, Patrimonio, "Aportes de la apertura (e2e)", "310505", ["CNT"]);
        var regla = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/receipts/rules", new
        {
            factType = "*", role = "Boveda", accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, CuentaDeBoveda), validFrom = corte.AddDays(-30),
            notes = "Bóveda (e2e)", reason = "La bóveda de la puesta en marcha tiene su cuenta",
        });
        regla.IsSuccessStatusCode.Should().BeTrue(await regla.Content.ReadAsStringAsync());
        await AperturaAsync(http, admin, corte,
            (CuentaDeBoveda, 6_000_000m, 0m), (PuestaEnMarchaE2E.CuentaContable, 1_500_000m, 0m), (Patrimonio, 0m, 7_500_000m));

        // El definitivo ya cuadra: se registra, reemplaza al provisional y cierra sus alertas.
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var propuesta = await CuadreAsync(http, admin, modulo, corte);
            propuesta.GetProperty("squares").GetBoolean().Should().BeTrue($"{modulo} cuadra después de la apertura: {propuesta}");
            var definitivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/{modulo}/opening-squares/definitive", new { cutoffDate = corte });
            definitivo.StatusCode.Should().Be(HttpStatusCode.OK, await definitivo.Content.ReadAsStringAsync());
            (await RecaudoYTesoreriaE2E.LeerAsync(definitivo)).GetProperty("status").GetString().Should().Be("Squared");
            (await CuadreAsync(http, admin, modulo, corte)).GetProperty("pendingProvisional").ValueKind.Should().Be(JsonValueKind.Null, $"{modulo} ya no tiene provisional");
        }
        (await AlertasPendientesAsync(http, admin, "Recaudo.CuadreProvisionalPendiente")).Should().BeEmpty("el definitivo cierra la alerta de Recaudo");
        (await AlertasPendientesAsync(http, admin, "Tesoreria.CuadreProvisionalPendiente")).Should().BeEmpty("el definitivo cierra la alerta de Tesorería");

        // Y el cierre del mes procede.
        var cerrado = await CerrarMesAsync(http, admin, hoy);
        cerrado.IsSuccessStatusCode.Should().BeTrue($"el cierre del mes de la activación procede con el definitivo: «{await cerrado.Content.ReadAsStringAsync()}»");
    }

    private static Task<JsonElement> CuadreAsync(HttpClient http, string admin, string modulo, DateOnly corte) =>
        CarteraE2E.GetAsync(http, admin, $"/api/{modulo}/opening-squares/current?cutoffDate={corte:yyyy-MM-dd}");

    private static async Task<List<JsonElement>> AlertasPendientesAsync(HttpClient http, string admin, string tipo) =>
        (await InventarioE2E.GetAsync(http, admin, $"/api/inventory/alerts?typeCode={tipo}&status=Pending&pageSize=50")).GetProperty("items").EnumerateArray().ToList();

    private static Task<HttpResponseMessage> CerrarMesAsync(HttpClient http, string admin, DateOnly dia) =>
        RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/periods/{dia.Year}/{dia.Month}/close", new
        {
            acknowledgeLendingPending = true, acknowledgeLendingLoad = true, acknowledgeInventoryPending = true, acknowledgeOperationalPending = true,
        });

    /// <summary>Los saldos de apertura al corte por la plantilla de Contabilidad (CSV con <c>;</c>), contabilizados.</summary>
    private static async Task AperturaAsync(HttpClient http, string admin, DateOnly fecha, params (string Cuenta, decimal Debito, decimal Credito)[] lineas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("cuenta;tercero;tipoDocumento;numeroDocumento;centroCosto;sucursal;debito;credito;detalle");
        foreach (var (cuenta, debito, credito) in lineas)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{cuenta};;;;;;{debito};{credito};Saldo al corte de la puesta en marcha (e2e)"));
        using var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(sb.ToString()));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "apertura.csv");
        contenido.Add(new StringContent(fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "date");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/accounting/opening/import") { Content = contenido };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        using var importada = await http.SendAsync(CarteraE2E.ConClave(req));
        importada.StatusCode.Should().Be(HttpStatusCode.Created, await importada.Content.ReadAsStringAsync());
        var borrador = (await RecaudoYTesoreriaE2E.LeerAsync(importada)).GetProperty("draftPublicId").GetGuid();
        var post = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{borrador}/post", null);
        post.StatusCode.Should().Be(HttpStatusCode.OK, await post.Content.ReadAsStringAsync());
    }
}
