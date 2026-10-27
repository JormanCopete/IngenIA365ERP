using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0041 (feature 013, fase 2; T16, T69–T73; contracts/api.md §21, §22), en el motor de <c>DB_PROVIDER</c> y en cooperativas aisladas de
/// «Cartera e2e»: el registro de participantes viene sembrado por la migración <c>PlataformaParaCartera</c> (<c>CNT</c>, <c>INV</c>,
/// <c>NOM</c> habilitados; <c>CAR</c> inhabilitado) y admite un doble de prueba fuera de producción; una venta a crédito de Comercial queda
/// <c>Pending</c> sin intentos porque el destino <c>Lending</c> no está habilitado; el canal <c>SIMULADO</c> de listas se configura sin
/// credencial y verifica <c>Ok</c>; la carga de listas restrictivas se revisa sin escribir y se aplica; una solicitud del titular recibe su
/// plazo en días hábiles; una autorización por finalidad se registra con su evidencia adjunta; un perfil regulatorio con un tipo de
/// organización inexistente responde 422; y toda escritura repetida con la misma <c>Idempotency-Key</c> responde
/// <c>Idempotent-Replayed: true</c>. Está en el filtro <c>F_K1</c> de quickstart §1.7. (nuevo)
///
/// <para>Escrita el 2026-10-06 sin poder correrla (Docker no arranca en la máquina de desarrollo): compila; la corrida es T0098.</para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class PlataformaParaCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Participantes = "/api/core/integration-participants";
    private const string Canales = "/api/core/external-channels";
    private const string Listas = "/api/compliance/restrictive-lists";
    private const string Solicitudes = "/api/compliance/data-subject-requests";
    private const string Autorizaciones = "/api/compliance/habeas-data/authorizations";
    private const string Perfil = "/api/core/regulatory-profile";
    private const string Repetida = "Idempotent-Replayed";

    [Fact]
    public async Task El_registro_viene_sembrado_y_no_admite_un_doble_en_ningun_ambiente()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "plataforma");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;

        var registro = (await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Get, Participantes)).EnumerateArray().ToList();
        JsonElement Uno(string codigo) => registro.Single(p => p.GetProperty("code").GetString() == codigo && p.GetProperty("validTo").ValueKind == JsonValueKind.Null);
        Uno("CNT").GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        Uno("CNT").GetProperty("destination").GetString().Should().Be("Accounting");
        Uno("INV").GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        Uno("INV").GetProperty("adoptions").EnumerateArray().Select(a => a.GetProperty("contract").GetString())
            // Feature 015, T2 (E-03): TesoreriaAmpliada le adopta a Inventario el PagoDeFacturaDeProveedor que Tesorería emite al pagar sus facturas.
            .Should().BeEquivalentTo(["ObligacionCreada", "ObligacionRechazada", "PagoDeFacturaDeProveedor"]);
        Uno("NOM").GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        Uno("NOM").GetProperty("role").GetInt32().Should().Be(1, "Nómina es recaudadora del contrato de obligaciones");
        Uno("CAR").GetProperty("isEnabled").GetBoolean().Should().BeFalse("Cartera nace inhabilitada hasta su activación");
        Uno("CAR").GetProperty("counts").GetBoolean().Should().BeFalse();
        Uno("CNT").GetProperty("counts").GetBoolean().Should().BeTrue("Contabilidad tiene consumidor en el despliegue");

        // Feature 015, T0308 (2026-10-26): un doble de prueba no se registra en ningún ambiente —tampoco en este host, que no es producción—; repetirlo con
        // la misma clave tampoco deja fila. Hasta entonces se aceptaba fuera de producción.
        var clave = Guid.NewGuid();
        var doble = new
        {
            code = "TST", @class = "Test", role = "MessageDestination", destination = "Receipts", isEnabled = true, isTestDouble = true,
            adoptions = Array.Empty<object>(), validFrom = InventarioE2E.HoyEnColombia, reason = "Doble de Recaudo para las e2e",
        };
        var primera = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Participantes, doble, clave);
        (await CarteraE2E.CodigoDeErrorAsync(primera)).Should().Be("Integration.Participant.TestDoublesRetired");
        var segunda = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Participantes, doble, clave);
        segunda.IsSuccessStatusCode.Should().BeFalse(await segunda.Content.ReadAsStringAsync());
        (await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Get, Participantes)).EnumerateArray()
            .Count(p => p.GetProperty("code").GetString() == "TST" && p.GetProperty("validTo").ValueKind == JsonValueKind.Null)
            .Should().Be(0, "ninguna fila del doble");
    }

    [Fact]
    public async Task La_venta_a_credito_queda_pendiente_sin_intentos_mientras_Cartera_no_este_habilitada()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "car_plataforma");
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var x = await ContabilidadDeVentasE2E.AsociadoAsync(http, esc.Admin, "Ximena", "72000001");

        var oficina = await InventarioE2E.BorradorAsync(http, cajero, "/api/inventory/sales/invoices", new
        {
            documentTypePublicId = esc.TipoRv, warehousePublicId = esc.Inv.Bodega("PV1"), counterpartyPersonPublicId = x,
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 1m } },
            payments = new[]
            {
                new { paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 100_000m, credit = new { installments = 2, termDays = 60, periodicityDays = 30 } },
            },
        });
        var id = oficina.GetProperty("documentPublicId").GetGuid();
        var confirmar = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Post, $"/api/inventory/sales/invoices/{id}/confirm", new { expectedAmountDue = 100_000m });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());
        var enAprobacion = await InventarioE2E.LeerAsync(confirmar);
        if (enAprobacion.GetProperty("status").GetInt32() == 1)
            await EscenarioDeVentas.AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token,
                enAprobacion.GetProperty("approval").GetProperty("requestPublicId").GetGuid(), aprobar: true));

        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId, 3);

        var mensajes = (await ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, id))
            .Where(m => m.GetProperty("type").GetString() == "VentaACreditoRegistrada").ToList();
        mensajes.Should().ContainSingle();
        ContabilidadDeInventarioE2E.Estado(mensajes[0]).Should().Be(0, "Pending: el destino Lending está registrado pero inhabilitado");
        var intentos = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationDeliveryAttempts" a JOIN dbo."COR_IntegrationMessageDeliveries" d ON d."Id" = a."DeliveryId" WHERE d."Destination" = 'Lending'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationDeliveryAttempts] a JOIN [dbo].[COR_IntegrationMessageDeliveries] d ON d.[Id] = a.[DeliveryId] WHERE d.[Destination] = 'Lending'");
        intentos.Should().Be(0, "el despachador no recorre un destino sin fila que cuente");
        var alertas = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" LIKE 'Integracion.%'""",
            "SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] LIKE 'Integracion.%'");
        alertas.Should().Be(0, "ni alertas");

        // Contabilidad sí cuenta (fila CNT sembrada): la venta se contabiliza por mensajes como antes de la 013.
        var facturada = await ContabilidadDeInventarioE2E.MensajeAsync(http, esc.Admin, id, "VentaFacturada");
        ContabilidadDeInventarioE2E.Estado(facturada).Should().Be(2, $"Accounting sigue consumiendo: {facturada}");
    }

    [Fact]
    public async Task Canales_externos_listas_restrictivas_titular_autorizaciones_y_perfil()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "plataforma_cumplimiento");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = InventarioE2E.HoyEnColombia;

        // (1) Canal SIMULADO de listas, sin credencial; verificar responde Ok (1). La misma clave repetida no crea otra vigencia.
        var claveCanal = Guid.NewGuid();
        var canal = new { kind = "RestrictiveLists", channelCode = "SIMULADO", validFrom = hoy, reason = "Pruebas de la plataforma" };
        var configurado = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Canales, canal, claveCanal);
        configurado.StatusCode.Should().Be(HttpStatusCode.Created, await configurado.Content.ReadAsStringAsync());
        var vigencia = await InventarioE2E.LeerAsync(configurado);
        vigencia.GetProperty("simulated").GetBoolean().Should().BeTrue();
        vigencia.GetProperty("credentialKey").ValueKind.Should().Be(JsonValueKind.Null, "el simulado no usa credencial");
        EsRepeticion(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Canales, canal, claveCanal)).Should().BeTrue();
        var verificacion = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Canales}/{vigencia.GetProperty("publicId").GetGuid()}/verify");
        verificacion.GetProperty("outcome").GetInt32().Should().Be(1, "Ok");

        // Un proveedor sin adaptador en el despliegue no se puede configurar. «CredentialMissing» (credencial nombrada sin archivo) necesita un
        // adaptador real instalado: hoy sólo hay simulados, así que se prueba en Application (CredencialesDeCanalExternoEnArchivoTests, T0030).
        var desconocido = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Canales, new
        {
            kind = "Sms", channelCode = "PROVEEDORX", validFrom = hoy.AddDays(1), credentialKey = $"{coop.TenantPublicId}.PROVEEDORX", reason = "Prueba",
        });
        await InventarioE2E.FallaAsync(desconocido, "Core.ExternalChannel.ChannelUnknown");

        // (2) Listas restrictivas: revisar no escribe; aplicar escribe la carga con su versión.
        var libro = LibroDeListas(hoy, ("79.123.456", "JUAN PEREZ GOMEZ"), ("80123456", "PEDRO RUIZ"));
        var revision = await ImportarListaAsync(http, admin, libro, "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        (await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Get, Listas)).EnumerateArray().Should().BeEmpty("revisar no guarda nada");
        var aplicacion = await ImportarListaAsync(http, admin, libro, "apply");
        aplicacion.StatusCode.Should().Be(HttpStatusCode.OK, await aplicacion.Content.ReadAsStringAsync());
        var cargas = (await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Get, Listas)).EnumerateArray().ToList();
        cargas.Should().ContainSingle();
        cargas[0].GetProperty("listCode").GetString().Should().Be("PROPIA");
        cargas[0].GetProperty("version").GetInt32().Should().Be(1);
        cargas[0].GetProperty("rows").GetInt32().Should().Be(2);

        // (3) Una solicitud del titular recibe su plazo en días hábiles (consulta: 10 hábiles, sin sábados, domingos ni festivos).
        var titular = await ContabilidadE2E.CrearPersonaAsync(http, admin, "Titular");
        var claveSolicitud = Guid.NewGuid();
        var solicitud = new
        {
            personPublicId = titular, kind = "Query", channel = "Presencial", receivedAt = DateTime.UtcNow,
            description = "Pide conocer los datos personales que la cooperativa tiene de él.",
        };
        var radicada = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Solicitudes, solicitud, claveSolicitud);
        radicada.StatusCode.Should().Be(HttpStatusCode.Created, await radicada.Content.ReadAsStringAsync());
        var plazo = DateOnly.Parse((await InventarioE2E.LeerAsync(radicada)).GetProperty("dueDate").GetString()!);
        plazo.DayOfWeek.Should().NotBe(DayOfWeek.Saturday).And.NotBe(DayOfWeek.Sunday);
        plazo.DayNumber.Should().BeGreaterThanOrEqualTo(hoy.AddDays(12).DayNumber, "diez días hábiles cruzan al menos dos fines de semana");
        EsRepeticion(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Solicitudes, solicitud, claveSolicitud)).Should().BeTrue();

        // (4) Una autorización por finalidad con su evidencia adjunta (dueño: la persona).
        (await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de tratamiento de datos", contentMarkdown = "Texto de la política para las e2e.", effectiveFrom = DateTime.UtcNow,
        })).IsSuccessStatusCode.Should().BeTrue("hace falta una política vigente para registrar la autorización");
        var evidencia = await SubirEvidenciaAsync(http, admin, titular);
        var sinEvidencia = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Autorizaciones, new
        {
            personPublicId = titular, purpose = "GeneralTreatment", grantedAt = DateTime.UtcNow,
        });
        sinEvidencia.IsSuccessStatusCode.Should().BeFalse("una autorización nueva exige su evidencia");
        var autorizada = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, Autorizaciones, new
        {
            personPublicId = titular, purpose = "GeneralTreatment", evidenceAttachmentPublicId = evidencia, grantedAt = DateTime.UtcNow,
        });
        autorizada.StatusCode.Should().Be(HttpStatusCode.Created, await autorizada.Content.ReadAsStringAsync());
        var historial = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Get, $"{Autorizaciones}?personPublicId={titular}");
        historial.ToString().Should().Contain(evidencia.ToString(), "la autorización cita su evidencia");

        // (5) Perfil regulatorio con un tipo de organización que no está en el documento vigente → 422.
        var perfil = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"{Perfil}/versions", new
        {
            organizationType = "NO_EXISTE", financialActivity = false, supervisionLevel = 2, validFrom = hoy, source = "Certificado de prueba",
            reason = "Prueba de tipo inexistente",
        });
        await InventarioE2E.FallaAsync(perfil, "Core.RegulatoryProfile.OrganizationTypeUnknown");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static bool EsRepeticion(HttpResponseMessage resp) =>
        resp.Headers.TryGetValues(Repetida, out var valores) && valores.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>El libro de la plantilla <c>compliance.restrictive-lists</c> (hoja «Datos», encabezados en la fila 1) con una lista PROPIA.</summary>
    private static byte[] LibroDeListas(DateOnly vigente, params (string Documento, string Nombre)[] entradas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Datos");
        string[] encabezados = ["lista", "fuente", "publicadaEl", "vigenteDesde", "tipoDeEntidad", "tipoDeDocumento", "numeroDeDocumento", "nombreCompleto",
            "alias", "programa", "notas"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < entradas.Length; f++)
        {
            var fila = f + 2;
            hoja.Cell(fila, 1).Value = "PROPIA";
            hoja.Cell(fila, 2).Value = "Lista interna del oficial de cumplimiento";
            hoja.Cell(fila, 3).Value = vigente.ToString("yyyy-MM-dd");
            hoja.Cell(fila, 4).Value = vigente.ToString("yyyy-MM-dd");
            hoja.Cell(fila, 5).Value = "Person";
            hoja.Cell(fila, 6).Value = "CC";
            hoja.Cell(fila, 7).Value = entradas[f].Documento;
            hoja.Cell(fila, 8).Value = entradas[f].Nombre;
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static Task<HttpResponseMessage> ImportarListaAsync(HttpClient http, string token, byte[] contenido, string modo)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "archivo", "listas.xlsx");
        formulario.Add(new StringContent("Carga inicial de la lista propia"), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Listas}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    /// <summary>Sube una evidencia <c>DataAuthorization</c> de la persona como lo hace el navegador: pedir, enviar al almacén, confirmar.</summary>
    private static async Task<Guid> SubirEvidenciaAsync(HttpClient http, string token, Guid persona)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, persona, contenido, "autorizacion-firmada.pdf", ownerEntityType: "DataAuthorization");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");

        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "autorizacion-firmada.pdf");
        // En este host el almacén es el local (rutas anónimas local-blob de la misma API): se le habla por el cliente del servidor de
        // pruebas. Con un almacén S3 (otra fixture) la URL es absoluta y va por el «navegador».
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        var local = !absoluta || (http.BaseAddress is { } baseApi && destino!.Authority == baseApi.Authority);
        using var almacen = local
            ? await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario)
            : await AdjuntosE2E.Navegador.PostAsync(url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe la evidencia: {(int)almacen.StatusCode}");

        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }
}
