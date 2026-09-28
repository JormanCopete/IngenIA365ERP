using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// La cooperativa de ensayo de I4 (quickstart §6.1), sobre la de ventas de I3 (<see cref="EscenarioDeVentas"/>): <b>obligada</b> a facturar
/// electrónicamente (<c>Dian.ObligadaAFacturar = true</c> desde el segundo día después del corte), con los tipos fiscales electrónicos —factura
/// <c>FE</c> (resolución, prefijo <c>FE</c>), nota crédito <c>NC</c> (consecutivo propio), documento equivalente POS <c>DE</c> y su nota de
/// ajuste <c>NA</c>, documento soporte <c>DS</c> y su nota <c>NDS</c>, y los de contingencia del facturador <c>CDE</c> (POS) y <c>CFE</c>
/// (factura)— y la caja <c>CJE</c> del punto <c>PV1</c> con placa y tipo de caja DIAN y un tipo por rol. Con <see cref="Configurar"/>, además,
/// la configuración de emisión por el canal <c>SIMULADO</c> en el ambiente de pruebas desde el día siguiente al corte, la credencial en el
/// directorio de la fixture (verificada) y las resoluciones de factura (rango 1–10), documento equivalente, documento soporte y las dos de
/// contingencia, asociadas al canal. Todo por HTTP, con el administrador. Las contrapartes se crean con el último dígito de su documento
/// elegido: <c>CanalSimulado</c> decide por él (1 rechaza, 2 valida con notificaciones, 3 queda en proceso, 4 DIAN no disponible, 5 canal
/// caído, otro valida). (nuevo)
/// </summary>
public sealed class EscenarioDeFacturacionElectronica
{
    public const string Canal = "SIMULADO";
    public const string Documentos = "/api/electronic-invoicing/documents";
    public const string Facturas = "/api/inventory/sales/invoices";
    public const string Notas = "/api/inventory/sales/credit-notes";

    public required EscenarioDeVentas Ventas { get; init; }
    public EscenarioDeInventario Inv => Ventas.Inv;
    public InventarioE2E.CooperativaAislada Coop => Ventas.Coop;
    public string Admin => Ventas.Admin;

    /// <summary>El primer día de la obligación y de la configuración de emisión.</summary>
    public required DateOnly Desde { get; init; }

    public required IReadOnlyDictionary<string, Guid> Tipos { get; init; }
    public required Guid CajaElectronica { get; init; }
    public bool Configurado { get; private set; }
    public IReadOnlyDictionary<string, Guid> Resoluciones => _resoluciones;

    private readonly Dictionary<string, Guid> _resoluciones = [];
    private static int _documento = 700_000;

    public Guid Tipo(string codigo) => Tipos[codigo];

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<EscenarioDeFacturacionElectronica>> Preparados = [];
    private static readonly object Cerrojo = new();

    /// <summary>El escenario de ese nombre (uno por fixture). Con <paramref name="configurar"/> deja la cooperativa lista para emitir.</summary>
    public static Task<EscenarioDeFacturacionElectronica> PrepararAsync(CentralIdentityApiFixture fx, string nombre, bool configurar = true)
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx, nombre, configurar);
                Preparados[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<EscenarioDeFacturacionElectronica> PrepararDeVerdadAsync(CentralIdentityApiFixture fx, string nombre, bool configurar)
    {
        var ventas = await EscenarioDeVentas.PrepararAsync(fx, nombre);
        using var http = fx.CreateClient();
        var t = ventas.Admin;
        var desde = ventas.Inv.Corte.AddDays(2);

        // Obligada a facturar desde el segundo día después del corte (la vigencia «no obligada» de I3 empieza el primero).
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/EINV/Dian.ObligadaAFacturar/versions", new
        {
            scopeKind = "None", value = "true", validFrom = desde.ToString("yyyy-MM-dd"), reason = "Ensayo de facturación electrónica",
        });

        var tipos = new Dictionary<string, Guid>
        {
            ["FE"] = await TipoFiscalAsync(http, t, "FE", "Factura electrónica", "SalesInvoice", "FE"),
            ["FX"] = await TipoFiscalAsync(http, t, "FX", "Factura de la resolución anterior", "SalesInvoice", "FX"),
            ["CFE"] = await TipoFiscalAsync(http, t, "CFE", "Factura de contingencia", "SalesInvoice", "CFE", contingencia: true),
            ["DE"] = await TipoFiscalAsync(http, t, "DE", "Documento equivalente POS", "PosEquivalentDocument", "DE"),
            ["CDE"] = await TipoFiscalAsync(http, t, "CDE", "Documento equivalente de contingencia", "PosEquivalentDocument", "CDE", contingencia: true),
            ["DS"] = await TipoFiscalAsync(http, t, "DS", "Documento soporte", "SupportDocument", "DS"),
            ["NC"] = await EscenarioDeVentas.TipoAsync(http, t, "NC", "Nota crédito electrónica", "CreditNote", desde),
            ["NA"] = await EscenarioDeVentas.TipoAsync(http, t, "NA", "Nota de ajuste POS", "PosAdjustmentNote", desde),
            ["NDS"] = await EscenarioDeVentas.TipoAsync(http, t, "NDS", "Nota de ajuste del documento soporte", "SupportDocumentAdjustmentNote", desde),
        };

        var caja = await CajaElectronicaAsync(http, t, ventas, tipos);
        var esc = new EscenarioDeFacturacionElectronica { Ventas = ventas, Desde = desde, Tipos = tipos, CajaElectronica = caja };
        if (configurar) await esc.ConfigurarAsync(fx, http);
        return esc;
    }

    /// <summary>Un tipo de una clase numerada por resolución: declara el prefijo de su resolución y no lleva consecutivo propio.</summary>
    public static async Task<Guid> TipoFiscalAsync(HttpClient http, string t, string codigo, string nombre, string clase, string prefijo, bool contingencia = false)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/document-types", new
        {
            code = codigo, name = nombre, @class = clase, requiresCounterparty = false, requiresCostCenter = false, requiresReason = false,
            requiresExternalReference = false, prefix = prefijo, isContingency = contingencia,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"tipo {codigo}: {await resp.Content.ReadAsStringAsync()}");
        var cuerpo = await InventarioE2E.LeerAsync(resp);
        return cuerpo.TryGetProperty("documentTypePublicId", out var id) ? id.GetGuid() : cuerpo.GetProperty("publicId").GetGuid();
    }

    private static async Task<Guid> CajaElectronicaAsync(HttpClient http, string t, EscenarioDeVentas ventas, IReadOnlyDictionary<string, Guid> tipos)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/inventory/points-of-sale/{ventas.Punto}/cash-registers", new
        {
            code = "CJE", name = "Caja electrónica", warehousePublicId = ventas.Inv.Bodega("PV1"), defaultCardTerminalPublicId = ventas.DatafonoRedeban,
            printFormat = "Ticket80", dianCashRegisterPlate = "PLACA-CJE-01", dianCashRegisterTypeCode = "POS",
            documentTypes = new object[]
            {
                new { role = "PosSale", documentTypePublicId = tipos["DE"] },
                new { role = "PosAdjustmentNote", documentTypePublicId = tipos["NA"] },
                new { role = "PosSaleContingency", documentTypePublicId = tipos["CDE"] },
                new { role = "InvoiceOnRequest", documentTypePublicId = tipos["FE"] },
                new { role = "InvoiceCreditNote", documentTypePublicId = tipos["NC"] },
                new { role = "InvoiceContingency", documentTypePublicId = tipos["CFE"] },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"caja CJE: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("cashRegisterPublicId").GetGuid();
    }

    // ------------------------------------------------------------------------------------------ configuración --

    /// <summary>Escribe la credencial del canal de esta cooperativa en el directorio de la fixture (<c>{tenant}.{canal}.json</c>).</summary>
    public static string CredencialEnArchivo(CentralIdentityApiFixture fx, Guid tenant, string canal)
    {
        var ruta = Path.Combine(fx.DirectorioDeCredenciales, $"{tenant:D}.{canal}.json");
        File.WriteAllText(ruta, """{ "usuario": "ensayo", "token": "sin-valor-real" }""");
        return ruta;
    }

    /// <summary>
    /// La configuración de quickstart §6.1: resoluciones asociadas al canal, emisión por <c>SIMULADO</c> en pruebas desde <see cref="Desde"/>, y
    /// la credencial con su verificación.
    /// </summary>
    public async Task ConfigurarAsync(CentralIdentityApiFixture fx, HttpClient http, bool conCredencial = true)
    {
        var t = Admin;
        var hasta = InventarioE2E.HoyEnColombia.AddYears(1);
        _resoluciones["FE"] = await ResolucionAsync(http, t, "Invoice", null, "FE", 1, 10, Desde, hasta);
        _resoluciones["FX"] = await ResolucionAsync(http, t, "Invoice", null, "FX", 1, 100, Desde, InventarioE2E.HoyEnColombia);
        _resoluciones["DE"] = await ResolucionAsync(http, t, "PosEquivalent", null, "DE", 1, 1000, Desde, hasta);
        _resoluciones["DS"] = await ResolucionAsync(http, t, "SupportDocument", null, "DS", 1, 1000, Desde, hasta);
        _resoluciones["CDE"] = await ResolucionAsync(http, t, "Contingency", "PosEquivalent", "CDE", 1, 1000, Desde, hasta);
        _resoluciones["CFE"] = await ResolucionAsync(http, t, "Contingency", "Invoice", "CFE", 1, 1000, Desde, hasta);

        // Las resoluciones se asocian al canal antes de configurarlo: la configuración exige una asociada por tipo en uso (§10.1).
        foreach (var (prefijo, id) in _resoluciones)
            await AsociarAsync(http, t, id, Canal, Desde, prefijo is "FE" or "FX" ? $"clave-tecnica-{prefijo.ToLowerInvariant()}-ab12" : null);

        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = Canal, environment = "Testing", softwareId = (string?)null, testSetId = (string?)null,
            emailDeliveryBy = "Erp", isEnabled = true, validFrom = Desde.ToString("yyyy-MM-dd"), reason = "Emisión del ensayo por el canal simulado",
            issuerTaxId = "900600123", issuerCheckDigit = "4", issuerBusinessName = "Cooperativa del ensayo", issuerAddress = "Calle 1 # 2-3",
            issuerMunicipalityDaneCode = "76001", issuerEmail = "facturacion@coop.ensayo.test",
        });
        Configurado = true;
        if (!conCredencial) return;
        CredencialEnArchivo(fx, Coop.TenantPublicId, Canal);
        var verificacion = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential", new { channelCode = Canal });
        verificacion.GetProperty("verified").GetBoolean().Should().BeTrue(verificacion.ToString());

    }

    public static async Task<Guid> ResolucionAsync(HttpClient http, string t, string kind, string? respalda, string prefijo, long desde, long hasta,
        DateOnly validaDesde, DateOnly validaHasta, string ambiente = "Testing")
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/resolutions", new
        {
            kind, backsUpKind = respalda, resolutionNumber = $"1876{Interlocked.Increment(ref _documento)}", resolutionDate = validaDesde.ToString("yyyy-MM-dd"),
            prefix = prefijo, rangeFrom = desde, rangeTo = hasta, validFrom = validaDesde.ToString("yyyy-MM-dd"), validTo = validaHasta.ToString("yyyy-MM-dd"),
            environment = ambiente, reason = "Resolución del ensayo",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"resolución {prefijo}: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("resolutionPublicId").GetGuid();
    }

    public static Task<JsonElement> AsociarAsync(HttpClient http, string t, Guid resolucion, string canal, DateOnly desde, string? claveTecnica) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/electronic-invoicing/resolutions/{resolucion}/channels", new
        {
            channelCode = canal, softwareId = (string?)null, validFrom = desde.ToString("yyyy-MM-dd"), technicalKey = claveTecnica,
            fetchTechnicalKeyFromChannel = false, reason = "Asociación del ensayo",
        });

    /// <summary>
    /// Un usuario con <b>sólo</b> el rol propio <paramref name="rol"/>: la invitación le da un rol integrado que ve todo <c>*.View</c>, y aquí
    /// se le quita para probar lo que pasa sin un permiso de lectura.
    /// </summary>
    public async Task<string> UsuarioSoloConAsync(CentralIdentityApiFixture fx, HttpClient http, string rol, string correo)
    {
        var (token, usuario) = await InventarioE2E.UsuarioAsync(fx, http, Coop, correo, rol);
        var detalle = await InventarioE2E.GetAsync(http, Admin, $"/api/admin/users/{usuario}");
        foreach (var r in detalle.GetProperty("roles").EnumerateArray().Where(r => r.GetProperty("code").GetString() != rol))
        {
            var quitar = await InventarioE2E.EnviarAsync(http, Admin, HttpMethod.Delete, $"/api/admin/users/{usuario}/roles/{r.GetProperty("publicId").GetGuid()}", null);
            quitar.IsSuccessStatusCode.Should().BeTrue(await quitar.Content.ReadAsStringAsync());
        }
        return token;
    }

    // ------------------------------------------------------------------------------------------ personas --

    /// <summary>Una persona natural cuyo documento termina en <paramref name="ultimoDigito"/> (lo que decide el canal simulado).</summary>
    public static async Task<Guid> PersonaAsync(HttpClient http, string t, string nombre, int ultimoDigito)
    {
        var documento = (Interlocked.Increment(ref _documento) * 10L + ultimoDigito).ToString();
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Comprador", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.fe.test",
            address = "Carrera 5 # 10-20", mobile = "3000000000",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"persona {nombre}: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetGuid();
    }

    // ------------------------------------------------------------------------------------------ ventas --

    /// <summary>
    /// Una factura de oficina en PV1 a <paramref name="comprador"/>, pagada en efectivo desde la sesión de <paramref name="usuario"/>; devuelve
    /// el borrador (con su <c>publicId</c> y lo que se paga).
    /// </summary>
    public async Task<JsonElement> BorradorDeFacturaAsync(HttpClient http, EscenarioDeVentas.Usuario usuario, Guid comprador, string tipo = "FE",
        DateOnly? fecha = null, params (string Producto, decimal Cantidad)[] lineas)
    {
        if (lineas.Length == 0) lineas = [("P7", 1m)];
        var total = lineas.Sum(l => EscenarioDeVentas.Precios[l.Producto] * l.Cantidad);
        return await InventarioE2E.BorradorAsync(http, usuario.Token, Facturas, new
        {
            documentTypePublicId = Tipo(tipo), warehousePublicId = Inv.Bodega("PV1"), operationDate = fecha?.ToString("yyyy-MM-dd"),
            counterpartyPersonPublicId = comprador,
            lines = lineas.Select(l => new { productPublicId = Inv.P(l.Producto).Id, unitPublicId = Inv.P(l.Producto).Unidad, quantity = l.Cantidad }).ToList(),
            payments = new[] { Ventas.Efectivo(total) },
        });
    }

    public static Guid Id(JsonElement documento) =>
        documento.TryGetProperty("documentPublicId", out var d) ? d.GetGuid()
        : documento.TryGetProperty("publicId", out var p) ? p.GetGuid()
        : documento.GetProperty("document").GetProperty("publicId").GetGuid();

    public static decimal AmountDue(JsonElement documento) =>
        documento.TryGetProperty("totals", out var t) ? t.GetProperty("amountDue").GetDecimal() : documento.GetProperty("amountDue").GetDecimal();

    /// <summary>Confirma una venta de oficina (o una nota) con lo que se ve a pagar; devuelve la respuesta sin juzgarla.</summary>
    public static Task<HttpResponseMessage> PedirConfirmarAsync(HttpClient http, string token, string ruta, Guid id, decimal esperado) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{ruta}/{id}/confirm", new { expectedAmountDue = esperado });

    /// <summary>Guarda y confirma una factura; exige 200 y devuelve el <c>SalesConfirmationDto</c>.</summary>
    public async Task<JsonElement> FacturaConfirmadaAsync(HttpClient http, EscenarioDeVentas.Usuario usuario, Guid comprador, string tipo = "FE",
        DateOnly? fecha = null, params (string Producto, decimal Cantidad)[] lineas)
    {
        await EscenarioDeVentas.SesionAsync(http, usuario.Token, CajaElectronica);
        var borrador = await BorradorDeFacturaAsync(http, usuario, comprador, tipo, fecha, lineas);
        var resp = await PedirConfirmarAsync(http, usuario.Token, Facturas, Id(borrador), AmountDue(borrador));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"confirmar la factura: {await resp.Content.ReadAsStringAsync()}");
        return await InventarioE2E.LeerAsync(resp);
    }

    /// <summary>El detalle de un documento de venta (<c>SalesDocumentDto</c>, con su bloque <c>electronic</c>).</summary>
    public static Task<JsonElement> VentaAsync(HttpClient http, string t, Guid documento) =>
        InventarioE2E.GetAsync(http, t, $"/api/inventory/sales/documents/{documento}");

    /// <summary>El documento electrónico de un documento de venta confirmado (su bloque <c>electronic</c>).</summary>
    public static Guid Electronico(JsonElement venta) => venta.GetProperty("electronic").GetProperty("electronicDocumentPublicId").GetGuid();

    /// <summary>El documento electrónico del documento de venta <paramref name="documento"/>.</summary>
    public static async Task<Guid> ElectronicoAsync(HttpClient http, string t, Guid documento) => Electronico(await VentaAsync(http, t, documento));

    // ------------------------------------------------------------------------------------------ documentos electrónicos --

    public static Task<JsonElement> DetalleAsync(HttpClient http, string t, Guid electronico) =>
        InventarioE2E.GetAsync(http, t, $"{Documentos}/{electronico}");

    public static int Estado(JsonElement detalle) => detalle.GetProperty("document").GetProperty("status").GetInt32();

    /// <summary>«Reintentar ahora» (<c>POST /documents/{id}/retry</c>): una emisión por el canal sellado; exige 200.</summary>
    public static Task<JsonElement> EmitirAsync(HttpClient http, string t, Guid electronico) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/retry");

    public static Task<JsonElement> ConsultarAsync(HttpClient http, string t, Guid electronico) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/query-status");

    /// <summary>Una pasada del procesador de documentos electrónicos sobre la cooperativa (apagado en las pruebas).</summary>
    public static Task ProcesarAsync(CentralIdentityApiFixture fx, Guid tenant) =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IngenIA365ERP.ElectronicInvoicing.Processor.ProcesadorDeDocumentosElectronicos>(fx.Factory.Services)
            .CorrerUnaPasadaAsync(tenant, CancellationToken.None);

    /// <summary>
    /// Los mensajes de integración de un tipo originados por <paramref name="origen"/> (si se da) cuyo contenido menciona <paramref name="texto"/>
    /// (si se da).
    /// </summary>
    public Task<int> MensajesAsync(CentralIdentityApiFixture fx, string tipo, Guid? origen = null, string? texto = null)
    {
        var pg = $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = '{tipo}'""";
        var ss = $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = '{tipo}'";
        if (origen is { } o) { pg += $""" AND "OriginPublicId" = '{o}'"""; ss += $" AND [OriginPublicId] = '{o}'"; }
        if (texto is not null) { pg += $""" AND "PayloadJson" LIKE '%{texto}%'"""; ss += $" AND [PayloadJson] LIKE '%{texto}%'"; }
        return ContarAsync(fx, pg, ss);
    }

    public async Task<int> ContarAsync(CentralIdentityApiFixture fx, string postgres, string sqlServer) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, Coop, postgres, sqlServer));

    /// <summary>Las alertas de un tipo de la cooperativa.</summary>
    public async Task<List<JsonElement>> AlertasAsync(HttpClient http, string tipo) =>
        (await InventarioE2E.GetAsync(http, Admin, $"/api/inventory/alerts?typeCode={tipo}&pageSize=100")).GetProperty("items").EnumerateArray().ToList();
}
