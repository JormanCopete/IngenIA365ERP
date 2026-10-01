using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using static IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing.EscenarioDeFacturacionElectronica;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// T687 (feature 012, I4, US8; quickstart §6.10; contracts/dian.md §18; escenario US8-8), sobre <c>CanalSimulado</c> y el canal de ensayo
/// <c>PRUEBA</c> (<see cref="CanalDePruebaE2E"/>), en el motor de <c>DB_PROVIDER</c>: el cambio de canal con vigencia no toca lo ya numerado —lo
/// pendiente sigue por su canal sellado y lo nuevo, notas incluidas, sale por el vigente—; «transmitir por el canal vigente» sólo con la
/// resolución asociada; dos emisiones concurrentes del mismo documento dan una sola transmisión; las credenciales son de su cooperativa y una
/// <c>CredentialKey</c> alterada no emite; y la factura en lugar del documento equivalente sale en una acción, sin mover pagos ni kardex dos
/// veces, y se transmite después de su nota. Cada caso en su cooperativa. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CanalYConcurrenciaTests(CentralIdentityApiFixture fx)
{
    private const int Pendiente = 0;
    private const int Validado = 2;
    private const int ValidadoConNotificaciones = 3;

    [Fact]
    public async Task El_cambio_de_canal_rige_para_lo_nuevo_y_lo_pendiente_sigue_por_su_canal_sellado()
    {
        var esc = await PrepararAsync(fx, "fecanal");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var supervisor = esc.Ventas.Supervisor;
        var hoy = InventarioE2E.HoyEnColombia;

        // (1) Numeradas por SIMULADO y pendientes: una con la resolución FE y otra con la FX, que no se asociará al canal nuevo.
        var comprador = await PersonaAsync(http, t, "Pendiente", 7);
        var fe = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        var feElectronica = await ElectronicoAsync(http, t, Id(fe));
        var fx1 = await esc.FacturaConfirmadaAsync(http, supervisor, comprador, "FX");
        var fxElectronica = await ElectronicoAsync(http, t, Id(fx1));
        var otraFe = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        var otraFeElectronica = await ElectronicoAsync(http, t, Id(otraFe));

        // (2) El canal PRUEBA desde hoy: las resoluciones en uso se asocian a él (menos FX) y su credencial se verifica.
        foreach (var prefijo in new[] { "FE", "DE", "DS", "CDE", "CFE" })
            await AsociarAsync(http, t, esc.Resoluciones[prefijo], CanalDePruebaE2E.Codigo, hoy, prefijo == "FE" ? "clave-tecnica-prueba-cd34" : null);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = CanalDePruebaE2E.Codigo, environment = "Testing", emailDeliveryBy = "Erp", isEnabled = true,
            validFrom = hoy.ToString("yyyy-MM-dd"), reason = "Cambio de proveedor tecnológico",
        });
        CredencialEnArchivo(fx, esc.Coop.TenantPublicId, CanalDePruebaE2E.Codigo);
        (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential",
            new { channelCode = CanalDePruebaE2E.Codigo })).GetProperty("verified").GetBoolean().Should().BeTrue();

        // (3) Lo numerado desde hoy sale por el canal nuevo, sin cambiar de pantalla ni de ruta.
        var nueva = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        var nuevaElectronica = await ElectronicoAsync(http, t, Id(nueva));
        (await DetalleAsync(http, t, nuevaElectronica)).GetProperty("document").GetProperty("channelCode").GetString().Should().Be(CanalDePruebaE2E.Codigo);
        await EmitirAsync(http, t, nuevaElectronica);
        Estado(await DetalleAsync(http, t, nuevaElectronica)).Should().Be(Validado);

        // (4) Lo pendiente del canal anterior sigue por el sellado.
        await EmitirAsync(http, t, feElectronica);
        var pendienteEmitida = await DetalleAsync(http, t, feElectronica);
        pendienteEmitida.GetProperty("document").GetProperty("channelCode").GetString().Should().Be(Canal);
        Estado(pendienteEmitida).Should().Be(Validado);

        // (5) La nota nueva de una factura del canal anterior sale por el vigente.
        var nota = await InventarioE2E.BorradorAsync(http, supervisor.Token, Notas, new
        {
            originDocumentPublicId = Id(fe), reason = "Anulación", totalVoid = true, withReturn = true,
            lines = Array.Empty<object>(), refunds = Array.Empty<object>(), documentTypePublicId = esc.Tipo("NC"), correctionConceptCode = "2",
        });
        var notaConfirmada = await InventarioE2E.ExitoAsync(http, supervisor.Token, HttpMethod.Post, $"{Notas}/{Id(nota)}/confirm",
            new { expectedAmountDue = AmountDue(nota) });
        (await DetalleAsync(http, t, await ElectronicoAsync(http, t, Id(notaConfirmada)))).GetProperty("document").GetProperty("channelCode").GetString()
            .Should().Be(CanalDePruebaE2E.Codigo);

        // (6) «Transmitir por el canal vigente»: no con una resolución que no está asociada a él; sí con una asociada, y queda auditado.
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{fxElectronica}/transmit-by-current-channel",
            new { reason = "El proveedor anterior se retiró" }), "ElectronicInvoicing.Document.ChannelNotLinked");
        var cambio = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{otraFeElectronica}/transmit-by-current-channel",
            new { reason = "El proveedor anterior se retiró" });
        cambio.GetProperty("previousChannelCode").GetString().Should().Be(Canal);
        cambio.GetProperty("channelCode").GetString().Should().Be(CanalDePruebaE2E.Codigo);
        await EmitirAsync(http, t, otraFeElectronica);
        var transmitida = await DetalleAsync(http, t, otraFeElectronica);
        Estado(transmitida).Should().Be(Validado);
        transmitida.GetProperty("document").GetProperty("channelCode").GetString().Should().Be(CanalDePruebaE2E.Codigo);
    }

    [Fact]
    public async Task Dos_emisiones_concurrentes_del_mismo_documento_hacen_una_sola_transmision()
    {
        var esc = await PrepararAsync(fx, "feconcurre");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "Concurrente", 7));
        var electronico = await ElectronicoAsync(http, t, Id(factura));

        // «Reintentar ahora» desde la pantalla y el procesador a la vez.
        var reintento = InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/retry");
        var procesador = ProcesarAsync(fx, esc.Coop.TenantPublicId);
        await Task.WhenAll(reintento, procesador);
        var respuesta = await reintento;
        if (respuesta.StatusCode != HttpStatusCode.OK)
            (await InventarioE2E.CodigoDeErrorAsync(respuesta)).Should().BeOneOf("ElectronicInvoicing.Document.InProgress", "ElectronicInvoicing.Document.Final");

        var detalle = await DetalleAsync(http, t, electronico);
        Estado(detalle).Should().Be(Validado);
        detalle.GetProperty("transmissions").GetArrayLength().Should().Be(1, "una sola transmisión efectiva");
        (await esc.ContarAsync(fx,
            $"""SELECT COUNT(*) FROM dbo."COR_ElectronicDocumentTransmissions" x JOIN dbo."COR_ElectronicDocuments" d ON d."Id" = x."ElectronicDocumentId" WHERE d."PublicId" = '{electronico}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_ElectronicDocumentTransmissions] x JOIN [dbo].[COR_ElectronicDocuments] d ON d.[Id] = x.[ElectronicDocumentId] WHERE d.[PublicId] = '{electronico}'"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Las_credenciales_son_de_su_cooperativa_y_una_clave_alterada_no_emite()
    {
        var esc = await PrepararAsync(fx, "fecredencial");
        var otra = await PrepararAsync(fx, "fecredotra", configurar: false);
        using var http = fx.CreateClient();

        // El canal simulado no tiene secreto: otra cooperativa sin archivo se verifica igual (2026-10-01, para poder probar
        // ventas en los ambientes desplegados, que no montan el Secret). Que un canal REAL no tome el archivo de otra
        // cooperativa lo fija CredencialesEnArchivoTests con un canal que sí exige archivo.
        await otra.ConfigurarAsync(fx, http, conCredencial: false);
        await InventarioE2E.ExitoAsync(http, otra.Admin, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential",
            new { channelCode = Canal });
        var preparacion = await InventarioE2E.GetAsync(http, otra.Admin, $"/api/electronic-invoicing/readiness?documentType={otra.Tipo("FE")}");
        preparacion.GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("code").GetString())
            .Should().NotContain("ElectronicInvoicing.Readiness.CredentialNotVerified");

        // CredentialKey cambiada a mano en la base: la emisión no sale.
        var t = esc.Admin;
        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "Clave", 7));
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        var ajena = $"{otra.Coop.TenantPublicId:D}.{Canal}.json";
        await InventarioE2E.SqlEnLaCooperativaAsync(fx, esc.Coop,
            $"""UPDATE dbo."COR_ElectronicEmissionSettings" SET "CredentialKey" = '{ajena}'""",
            $"UPDATE [dbo].[COR_ElectronicEmissionSettings] SET [CredentialKey] = '{ajena}'");
        var intento = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/retry");
        var texto = await intento.Content.ReadAsStringAsync();
        texto.Should().Contain("ElectronicInvoicing.CredentialMismatch", $"la emisión con la clave alterada: {texto}");
        Estado(await DetalleAsync(http, t, electronico)).Should().Be(Pendiente, "no se transmitió");
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential",
            new { channelCode = Canal }), "ElectronicInvoicing.CredentialMismatch");
    }

    [Fact]
    public async Task La_factura_en_lugar_del_documento_equivalente_sale_en_una_accion_despues_de_su_nota()
    {
        var esc = await PrepararAsync(fx, "feenlugar");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var cajero = esc.Ventas.Cajero1;
        var sesion = await EscenarioDeVentas.SesionAsync(http, cajero.Token, esc.CajaElectronica);

        // Un documento equivalente POS validado (consumidor final).
        var venta = await esc.Ventas.VentaDeContadoAsync(http, cajero.Token, sesion, "P7");
        venta.GetProperty("prefix").GetString().Should().Be("DE");
        var original = venta.GetProperty("documentPublicId").GetGuid();
        var existenciaAntes = await esc.Inv.FisicaAsync(http, t, "P7", "PV1");
        var cajaAntes = await EfectivoEsperadoAsync(http, cajero.Token, sesion);

        // El comprador pide factura: una acción, la nota de ajuste y la factura.
        var comprador = await PersonaAsync(http, t, "PideFactura", 7);
        var hecho = await InventarioE2E.ExitoAsync(http, esc.Ventas.Supervisor.Token, HttpMethod.Post, $"/api/inventory/sales/documents/{original}/invoice-instead",
            new { buyerPersonPublicId = comprador, reason = "El comprador pidió factura", expectedAmountDue = venta.GetProperty("amountDue").GetDecimal() });
        var notaDeAjuste = hecho.GetProperty("adjustmentNotePublicId").GetGuid();
        var factura = hecho.GetProperty("invoicePublicId").GetGuid();
        (await VentaAsync(http, t, factura)).GetProperty("prefix").GetString().Should().Be("FE", "el número del rol InvoiceOnRequest de la caja");

        // Sin mover pagos ni kardex dos veces.
        (await esc.Inv.FisicaAsync(http, t, "P7", "PV1")).Should().Be(existenciaAntes, "ni la nota devuelve ni la factura vuelve a sacar");
        (await EfectivoEsperadoAsync(http, cajero.Token, sesion)).Should().Be(cajaAntes, "la caja no registra entrada ni salida");

        // La factura espera a la nota: primero sale la nota, después la factura.
        var notaElectronica = await ElectronicoAsync(http, t, notaDeAjuste);
        var facturaElectronica = await ElectronicoAsync(http, t, factura);
        await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{facturaElectronica}/retry");
        (await DetalleAsync(http, t, facturaElectronica)).GetProperty("transmissions").GetArrayLength().Should().Be(0, "no sale antes que su nota");
        await EmitirAsync(http, t, notaElectronica);
        Estado(await DetalleAsync(http, t, notaElectronica)).Should().BeOneOf(Validado, ValidadoConNotificaciones);
        await EmitirAsync(http, t, facturaElectronica);
        Estado(await DetalleAsync(http, t, facturaElectronica)).Should().Be(Validado);
    }

    /// <summary>
    /// SC-019 sobre el canal simulado: 30 emisiones a la vez (una por caja) con p95 de a lo sumo 5 s. Sin <c>RUN_PERF_TESTS=1</c> se reporta
    /// omitida con su motivo; la medición real con el proveedor es de T764.
    /// </summary>
    [FactDeRendimiento]
    public async Task Treinta_emisiones_a_la_vez_responden_con_p95_de_a_lo_sumo_5_segundos()
    {
        var esc = await PrepararAsync(fx, "fevolumen");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var tipo = await TipoFiscalAsync(http, t, "FP", "Factura del ensayo de volumen", "SalesInvoice", "FP");
        var resolucion = await ResolucionAsync(http, t, "Invoice", null, "FP", 1, 1000, esc.Desde, InventarioE2E.HoyEnColombia.AddYears(1));
        await AsociarAsync(http, t, resolucion, Canal, esc.Desde, "clave-tecnica-fp-ef56");
        var comprador = await PersonaAsync(http, t, "Volumen", 7);
        var tipos = esc.Tipos.ToDictionary(p => p.Key, p => p.Value);
        tipos["FP"] = tipo;
        var conFp = new EscenarioDeFacturacionElectronica { Ventas = esc.Ventas, Desde = esc.Desde, Tipos = tipos, CajaElectronica = esc.CajaElectronica };

        var electronicos = new List<Guid>();
        for (var i = 0; i < 30; i++)
            electronicos.Add(await ElectronicoAsync(http, t, Id(await conFp.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, comprador, "FP"))));

        var tiempos = await Task.WhenAll(electronicos.Select(async e =>
        {
            using var cliente = fx.CreateClient();
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            await EmitirAsync(cliente, t, e);
            return cronometro.Elapsed;
        }));
        var ordenados = tiempos.OrderBy(x => x).ToList();
        var p95 = ordenados[(int)Math.Ceiling(ordenados.Count * 0.95) - 1];
        p95.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(5), $"p95 de 30 emisiones a la vez: {p95.TotalMilliseconds:0} ms");
    }

    private static async Task<decimal> EfectivoEsperadoAsync(HttpClient http, string token, Guid sesion)
    {
        var esperado = await InventarioE2E.GetAsync(http, token, $"{EscenarioDeVentas.Sesiones}/{sesion}/expected");
        return esperado.GetProperty("lines").EnumerateArray()
            .Where(l => l.GetProperty("paymentMeans").GetProperty("code").GetString() == "EFECTIVO")
            .Sum(l => l.GetProperty("expected").GetDecimal());
    }
}
