using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T333 y T334 (quickstart §3.6; US9, US3-6; FR-011, FR-044, FR-050, FR-051; api.md §14, §29), en el motor de
/// <c>DB_PROVIDER</c>, sobre el escenario aislado «compras»: la compra directa de 3 cajas registra 36 unidades a 1.300 con su
/// número; la factura contra una recepción retiene con la base justo en el mínimo (27 UVT) y no un peso por debajo; un tipo
/// de recepción no descontable lleva el IVA al costo; la factura a crédito nace con sus eventos RADIAN pendientes, levanta la
/// alerta en la revisión disparada a mano y la cierra al registrar los dos; la devolución sale al costo con que entró y deja
/// el ajuste de costo; la recepción con factura no se anula hasta anular la factura; la misma <c>Idempotency-Key</c> dos veces
/// da un solo efecto; y el proveedor nuevo se crea con su autorización de datos (aceptada o negada) o, sin política
/// publicada, con la constancia de que no la hay.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CompraDirectaTests(CentralIdentityApiFixture fx)
{
    private const string Compras = "/api/inventory/purchases";
    private static int _numeroDeFactura = 1000;

    private Task<EscenarioDeInventario> EscenarioAsync() => EscenarioDeInventario.PrepararAsync(fx, "compras");

    [Fact]
    public async Task Tres_cajas_de_compra_directa_entran_como_36_unidades_a_1300_y_se_devuelven_al_costo_de_entrada()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorA");
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10, 1_000m)]);

        var compra = await CompraDirectaAsync(http, esc, proveedor, [new("P1", 3, 15_600m, EnCajas: true)], "Cash");
        compra.GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        var recepcion = compra.GetProperty("receipt").GetProperty("publicId").GetGuid();
        compra.GetProperty("receipt").GetProperty("displayNumber").GetString().Should().NotBeNullOrWhiteSpace();

        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}");
        var entrada = kardex.Filas.Single(f => kardex.Numero(f, "Entrada") == 36m);
        kardex.Numero(entrada, "Costo unitario").Should().Be(1_300m, "15.600 la caja de 12, con el IVA descontable fuera del costo");

        // Devolución de 6 al costo de entrada (1.300) contra un promedio de (10 × 1.000 + 36 × 1.300) / 46.
        var detalle = await InventarioE2E.GetAsync(http, esc.Admin, $"{Compras}/receipts/{recepcion}");
        var lineaRecibida = detalle.GetProperty("document").GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        var devolucion = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/returns", new
        {
            documentTypePublicId = esc.Tipos["DVP"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor, reason = "Mercancía averiada",
            lines = new[] { new { productPublicId = esc.P("P1").Id, unitPublicId = esc.P("P1").Unidad, quantity = 6m, receiptLinePublicId = lineaRecibida } },
        });
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/returns", devolucion.GetProperty("publicId").GetGuid());
        var despues = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}");
        despues.Numero(despues.Filas.Single(f => despues.Numero(f, "Salida") == 6m), "Costo unitario").Should().Be(1_300m, "sale al costo con que entró");
        (await ContarMensajesAsync(esc, "DevolucionRegistrada")).Should().Be(1);
        (await ContarMensajesAsync(esc, "AjusteDeCostoReconocido")).Should().BeGreaterThan(0, "la diferencia contra el promedio vigente es ajuste de costo");
    }

    [Fact]
    public async Task La_factura_contra_la_recepcion_retiene_con_la_base_en_el_minimo_y_no_por_debajo()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorRete");
        var uvt = Convert.ToDecimal(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT "Value" FROM dbo."PAY_LegalParameters" WHERE "Code" = 'UVT' AND "IsDeleted" = false ORDER BY "ValidFrom" DESC LIMIT 1""",
            "SELECT TOP 1 [Value] FROM [dbo].[PAY_LegalParameters] WHERE [Code] = 'UVT' AND [IsDeleted] = 0 ORDER BY [ValidFrom] DESC"));
        var minimo = Math.Round(27m * uvt, 0, MidpointRounding.AwayFromZero);

        var enElMinimo = await FacturaContraRecepcionAsync(http, esc, proveedor, "P3", minimo);
        enElMinimo.Any(EsReteFuente).Should().BeTrue($"la base igual al mínimo de 27 UVT ({minimo}) retiene");
        var porDebajo = await FacturaContraRecepcionAsync(http, esc, proveedor, "P3", minimo - 1m);
        porDebajo.Any(EsReteFuente).Should().BeFalse("un peso por debajo, no");
        (await ContarMensajesAsync(esc, "FacturaProveedorRegistrada")).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Un_tipo_de_recepcion_no_descontable_lleva_el_iva_al_costo()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var proveedor = await PersonaAsync(http, esc.Admin, "ProveedorConsumo", null, responsableIva: true);
        var tipo = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/document-types", new
        {
            code = "RECND", name = "Recepción para consumo interno", @class = "PurchaseReceipt", prefix = "RN", vatNonDeductible = true,
        });

        var recepcion = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/receipts", new
        {
            documentTypePublicId = tipo.GetProperty("publicId").GetGuid(), warehousePublicId = esc.Bodega("PV1"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = esc.P("P2").Id, unitPublicId = esc.P("P2").Unidad, quantity = 1m, unitPrice = 1_000m } },
        });
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/receipts", recepcion.GetProperty("publicId").GetGuid());

        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P2").Id}&warehouse={esc.Bodega("PV1")}&from={hoy}&to={hoy}");
        kardex.Numero(kardex.Filas.Single(f => kardex.Numero(f, "Entrada") == 1m), "Costo unitario").Should().Be(1_190m,
            "el proveedor responsable cobra el IVA del 19 % y el tipo no descontable lo lleva al costo");
    }

    [Fact]
    public async Task La_factura_a_credito_levanta_la_alerta_de_eventos_RADIAN_y_se_cierra_al_registrarlos()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorCredito");
        var emision = esc.Corte.AddDays(2);

        var compra = await CompraDirectaAsync(http, esc, proveedor, [new("P4", 2, 3_000m)], "Credit", emision);
        var factura = compra.GetProperty("supplierInvoice").GetProperty("publicId").GetGuid();
        var eventos = await InventarioE2E.GetAsync(http, esc.Admin, $"{Compras}/supplier-invoices/{factura}/radian-events");
        eventos.EnumerateArray().Select(e => e.GetProperty("status").ToString()).Should().HaveCount(2).And.OnlyContain(s => s == "Pending" || s == "0");

        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, Application.Inventory.Purchasing.TareaDeEventosRadian.NombreDeLaTarea);
        var alertas = await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/alerts?typeCode=Compras.EventosRadianFaltantes&status=Pending&pageSize=50");
        alertas.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0, "la factura lleva más de Compras.DiasAlertaEventosRadian días sin 030 ni 032");

        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/supplier-invoices/{factura}/radian-events",
            new { eventCode = "Receipt030", date = hoy, source = "DianPortal" });
        var ambos = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/supplier-invoices/{factura}/radian-events",
            new { eventCode = "GoodsReceived032", date = hoy, source = "SupplierPortal" });
        ambos.EnumerateArray().Select(e => e.GetProperty("status").ToString()).Should().OnlyContain(s => s == "RegisteredExternally" || s == "1");

        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, Application.Inventory.Purchasing.TareaDeEventosRadian.NombreDeLaTarea);
        var pendientes = await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/alerts?typeCode=Compras.EventosRadianFaltantes&status=Pending&pageSize=50");
        pendientes.GetProperty("items").GetArrayLength().Should().Be(0, "con los dos eventos la alerta se atiende sola");
    }

    [Fact]
    public async Task La_recepcion_con_factura_no_se_anula_hasta_anular_la_factura_y_la_misma_clave_da_un_solo_efecto()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorAnula");

        var clave = Guid.NewGuid();
        var cuerpo = CuerpoDeCompra(esc, proveedor, [new("P5", 4, 2_000m)], "Cash", null);
        var primera = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/direct", cuerpo, clave);
        primera.StatusCode.Should().Be(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());
        var repetida = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/direct", cuerpo, clave);
        repetida.Headers.GetValues("Idempotent-Replayed").Single().Should().Be("true");
        var compra = await InventarioE2E.LeerAsync(primera);
        (await InventarioE2E.LeerAsync(repetida)).GetProperty("receipt").GetProperty("publicId").GetGuid()
            .Should().Be(compra.GetProperty("receipt").GetProperty("publicId").GetGuid());
        (await esc.FisicaAsync(http, esc.Admin, "P5", "PRIN")).Should().Be(4m, "un solo efecto");

        var recepcion = compra.GetProperty("receipt").GetProperty("publicId").GetGuid();
        var factura = compra.GetProperty("supplierInvoice").GetProperty("publicId").GetGuid();
        var conFactura = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/receipts/{recepcion}/void", new { reason = "Error" });
        var dependientes = (await InventarioE2E.FallaAsync(conFactura, "Inventory.Document.HasDependents")).GetProperty("data").GetProperty("dependents");
        dependientes.EnumerateArray().Should().Contain(d => d.GetProperty("publicId").GetGuid() == factura);

        (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/supplier-invoices/{factura}/void", new { reason = "Factura mal registrada" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Compras}/receipts/{recepcion}/void", new { reason = "Compra anulada" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await ContarMensajesAsync(esc, "DocumentoAnulado")).Should().BeGreaterThanOrEqualTo(2);
        (await esc.FisicaAsync(http, esc.Admin, "P5", "PRIN")).Should().Be(0m);
    }

    [Fact]
    public async Task El_proveedor_nuevo_se_crea_con_su_autorizacion_de_datos()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "comprashabeas");
        using var http = fx.CreateClient();
        var comprador = await CompradorAsync(http, esc);

        // Sin política publicada: 404 y el alta procede con la constancia de que no la hay.
        (await InventarioE2E.MandarAsync(http, comprador, HttpMethod.Get, "/api/compliance/habeas-data/policies/current"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PersonaAsync(http, comprador, "SinPolitica", null);

        var publicada = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de tratamiento de datos", contentMarkdown = "Tratamos sus datos para facturar y comprar.", effectiveFrom = DateTime.UtcNow.AddMinutes(-1),
        });
        publicada.IsSuccessStatusCode.Should().BeTrue(await publicada.Content.ReadAsStringAsync());
        var politica = (await InventarioE2E.GetAsync(http, comprador, "/api/compliance/habeas-data/policies/current")).GetProperty("policyVersionPublicId").GetGuid();

        var negada = await PersonaAsync(http, comprador, "Negada", new { decision = "Declined", policyVersionPublicId = politica, channel = "Compras" });
        var aceptada = await PersonaAsync(http, comprador, "Aceptada", new { decision = "Accepted", policyVersionPublicId = politica, channel = "Compras" });
        (await AccionDeConsentimientoAsync(esc, negada)).Should().Be("Declined");
        (await AccionDeConsentimientoAsync(esc, aceptada)).Should().Be("Accepted");

        var compra = await CompraDirectaAsync(http, esc, negada, [new("P6", 1, 500m)], "Cash", token: comprador);
        compra.GetProperty("status").GetInt32().Should().Be(2, "negarse al tratamiento de datos no impide comprarle");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    /// <summary>¿El renglón es una retención en la fuente (TaxKind.ReteFuente = 3)?</summary>
    private static bool EsReteFuente(JsonElement renglon) => renglon.GetProperty("kind").ToString() is "ReteFuente" or "3";

    private static object CuerpoDeCompra(EscenarioDeInventario esc, Guid proveedor, EscenarioDeInventario.Linea[] lineas, string formaDePago, DateOnly? emision)
    {
        var numero = Interlocked.Increment(ref _numeroDeFactura).ToString();
        var fecha = emision ?? InventarioE2E.HoyEnColombia;
        return new
        {
            receipt = new
            {
                documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = lineas.Select(l => new
                {
                    productPublicId = esc.P(l.Producto).Id, unitPublicId = l.EnCajas ? esc.P(l.Producto).Caja!.Value : esc.P(l.Producto).Unidad,
                    quantity = l.Cantidad, unitPrice = l.Costo,
                }).ToList(),
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new
                {
                    prefix = "FV", number = numero, cufe = (string?)null, issueDate = fecha.ToString("yyyy-MM-dd"),
                    dueDate = formaDePago == "Credit" ? fecha.AddDays(30).ToString("yyyy-MM-dd") : null, paymentForm = formaDePago, isElectronic = false,
                },
            },
        };
    }

    private static async Task<JsonElement> CompraDirectaAsync(HttpClient http, EscenarioDeInventario esc, Guid proveedor, EscenarioDeInventario.Linea[] lineas,
        string formaDePago, DateOnly? emision = null, string? token = null)
    {
        var resp = await InventarioE2E.MandarAsync(http, token ?? esc.Admin, HttpMethod.Post, $"{Compras}/direct", CuerpoDeCompra(esc, proveedor, lineas, formaDePago, emision));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await InventarioE2E.LeerAsync(resp);
    }

    /// <summary>Recepción de 1 unidad a ese precio y su factura; devuelve los renglones tributarios de la factura confirmada.</summary>
    private static async Task<List<JsonElement>> FacturaContraRecepcionAsync(HttpClient http, EscenarioDeInventario esc, Guid proveedor, string producto, decimal precio)
    {
        var recepcion = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/receipts", new
        {
            documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = esc.P(producto).Id, unitPublicId = esc.P(producto).Unidad, quantity = 1m, unitPrice = precio } },
        });
        var idRecepcion = recepcion.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/receipts", idRecepcion);
        var linea = (await InventarioE2E.GetAsync(http, esc.Admin, $"{Compras}/receipts/{idRecepcion}")).GetProperty("document").GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();

        var factura = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/supplier-invoices", new
        {
            documentTypePublicId = esc.Tipos["FCP"], supplierPersonPublicId = proveedor,
            supplier = new { prefix = "FR", number = Interlocked.Increment(ref _numeroDeFactura).ToString(), issueDate = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd"),
                paymentForm = "Cash", isElectronic = false },
            lines = new[] { new { receiptLinePublicId = linea, productPublicId = esc.P(producto).Id, unitPublicId = esc.P(producto).Unidad, quantity = 1m, unitPrice = precio } },
        });
        var idFactura = factura.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/supplier-invoices", idFactura);
        return (await InventarioE2E.GetAsync(http, esc.Admin, $"{Compras}/supplier-invoices/{idFactura}")).GetProperty("document").GetProperty("taxLines").EnumerateArray().ToList();
    }

    private async Task<int> ContarMensajesAsync(EscenarioDeInventario esc, string tipo) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = '{tipo}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = '{tipo}'"));

    private async Task<string> CompradorAsync(HttpClient http, EscenarioDeInventario esc)
    {
        await InventarioE2E.RolAsync(http, esc.Coop, "ENSCOMPH", "inventario.comprador");
        var (token, usuario) = await InventarioE2E.UsuarioAsync(fx, http, esc.Coop, $"comprador.habeas.{esc.Coop.TenantPublicId:N}@coop.inventario.test", "ENSCOMPH");
        await InventarioE2E.AlcanceAsync(http, esc.Admin, usuario, esc.Bodega("PRIN"));
        return token;
    }

    private static async Task<Guid> PersonaAsync(HttpClient http, string token, string nombre, object? autorizacion, bool responsableIva = false)
    {
        var documento = (900_000_000 + Math.Abs(Guid.NewGuid().GetHashCode()) % 99_999_999).ToString();
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Proveedor", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.compras.test",
            authorization = autorizacion, isVatResponsible = responsableIva,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await InventarioE2E.LeerAsync(resp)).GetGuid();
    }

    private async Task<string?> AccionDeConsentimientoAsync(EscenarioDeInventario esc, Guid persona) =>
        (await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            $"""SELECT c."Action" FROM dbo."CMP_HabeasDataConsents" c JOIN dbo."COR_People" p ON p."Id" = c."PersonId" WHERE p."PublicId" = '{persona}'""",
            $"SELECT c.[Action] FROM [dbo].[CMP_HabeasDataConsents] c JOIN [dbo].[COR_People] p ON p.[Id] = c.[PersonId] WHERE p.[PublicId] = '{persona}'"))?.ToString();
}
