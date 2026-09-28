using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using static IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing.EscenarioDeFacturacionElectronica;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// T686 (feature 012, I4, US8; quickstart §6.3, §6.5 a §6.7; escenarios US8-3, US8-4 y US8-5), sobre <c>CanalSimulado</c> y en el motor de
/// <c>DB_PROVIDER</c>: el rechazo con sus motivos y su alerta, y sus tres salidas —caso a (mismo número, sin mensajes nuevos, copia fiscal
/// nueva; con un cambio económico remite al b), caso b (el mismo número en un reemplazo, anulación con <c>fiscalCase</c>) y caso c (sin
/// reemplazo)—; la espera del POS con el canal «en proceso», que a la tercera abre la contingencia 03 y numera la cuarta venta con la
/// resolución de contingencia; y la contingencia 04, que entrega en el acto y transmite al volver la DIAN. Cada caso en su cooperativa. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class RechazosYContingenciasTests(CentralIdentityApiFixture fx)
{
    private const int Pendiente = 0;
    private const int Enviado = 1;
    private const int Validado = 2;
    private const int ValidadoConNotificaciones = 3;
    private const int Rechazado = 4;
    private const int ContingenciaDelFacturador = 5;
    private const int ContingenciaDian = 6;
    private const int CanceladoSinReemplazo = 7;

    [Fact]
    public async Task Un_rechazo_se_resuelve_por_los_casos_a_b_y_c_sin_numeros_sin_explicacion()
    {
        var esc = await PrepararAsync(fx, "ferechazo");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var supervisor = esc.Ventas.Supervisor;

        // (1) El canal rechaza (documento terminado en 1): motivos traducidos y alerta.
        var comprador = await PersonaAsync(http, t, "Rechazado", 1);
        var factura = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        await EmitirAsync(http, t, electronico);
        var rechazado = await DetalleAsync(http, t, electronico);
        Estado(rechazado).Should().Be(Rechazado, rechazado.ToString());
        rechazado.GetProperty("document").GetProperty("lastMessage").GetString().Should().NotBeNullOrWhiteSpace("el motivo, traducido");
        (await esc.AlertasAsync(http, "Dian.DocumentoRechazado")).Should().NotBeEmpty();

        // Un validado no admite ninguno de los tres casos.
        var validado = await esc.FacturaConfirmadaAsync(http, supervisor, await PersonaAsync(http, t, "Valido", 7));
        var validadoElectronico = await ElectronicoAsync(http, t, Id(validado));
        await EmitirAsync(http, t, validadoElectronico);
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{validadoElectronico}/cancel",
            new { reason = "No aplica" }), "ElectronicInvoicing.Document.NotRejected");

        // (2) Caso a: se corrige el correo del comprador; versión 2 con el mismo número, sin mensajes de negocio nuevos.
        var mensajesAntes = await MensajesDelDocumentoAsync(esc, Id(factura));
        var corregido = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/correct", new
        {
            reason = "El correo del comprador estaba mal", partySnapshotChanges = new { email = "comprador.corregido@coop.fe.test" },
        });
        corregido.GetProperty("versionNumber").GetInt32().Should().Be(2);
        await EmitirAsync(http, t, electronico);
        var corregidoDetalle = await DetalleAsync(http, t, electronico);
        Estado(corregidoDetalle).Should().Be(Validado, "la versión 2 del simulado se valida");
        corregidoDetalle.GetProperty("document").GetProperty("number").GetString().Should().Be("FE1", "el mismo número");
        corregidoDetalle.GetProperty("versions").GetArrayLength().Should().Be(2);
        (await MensajesDelDocumentoAsync(esc, Id(factura))).Should().Be(mensajesAntes, "el caso a no emite mensajes de negocio");
        (await esc.ContarAsync(fx,
            $"""SELECT MAX(s."Version") FROM dbo."INV_DocumentPartySnapshots" s JOIN dbo."INV_Documents" d ON d."Id" = s."DocumentId" WHERE d."PublicId" = '{Id(factura)}'""",
            $"SELECT MAX(s.[Version]) FROM [dbo].[INV_DocumentPartySnapshots] s JOIN [dbo].[INV_Documents] d ON d.[Id] = s.[DocumentId] WHERE d.[PublicId] = '{Id(factura)}'"))
            .Should().Be(2, "la copia fiscal de la contraparte pasa a la versión 2");

        // (3) Caso a con un dato económico distinto del expedido: remite al caso b nombrando el campo.
        var compradorB = await PersonaAsync(http, t, "Economico", 1);
        var otra = await esc.FacturaConfirmadaAsync(http, supervisor, compradorB);
        var otraElectronica = await ElectronicoAsync(http, t, Id(otra));
        await EmitirAsync(http, t, otraElectronica);
        await InventarioE2E.SqlEnLaCooperativaAsync(fx, esc.Coop,
            $"""UPDATE dbo."INV_DocumentLines" SET "Quantity" = 2, "QuantityBase" = 2 WHERE "DocumentId" = (SELECT "Id" FROM dbo."INV_Documents" WHERE "PublicId" = '{Id(otra)}')""",
            $"UPDATE [dbo].[INV_DocumentLines] SET [Quantity] = 2, [QuantityBase] = 2 WHERE [DocumentId] = (SELECT [Id] FROM [dbo].[INV_Documents] WHERE [PublicId] = '{Id(otra)}')");
        var economico = await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{otraElectronica}/correct",
            new { reason = "Corregir" }), "ElectronicInvoicing.Document.EconomicFootprintChanged");
        economico.GetProperty("data").GetProperty("fields").EnumerateArray().Select(f => f.ToString())
            .Should().Contain(f => f.Contains("quantity", StringComparison.OrdinalIgnoreCase), economico.ToString());
        await InventarioE2E.SqlEnLaCooperativaAsync(fx, esc.Coop,
            $"""UPDATE dbo."INV_DocumentLines" SET "Quantity" = 1, "QuantityBase" = 1 WHERE "DocumentId" = (SELECT "Id" FROM dbo."INV_Documents" WHERE "PublicId" = '{Id(otra)}')""",
            $"UPDATE [dbo].[INV_DocumentLines] SET [Quantity] = 1, [QuantityBase] = 1 WHERE [DocumentId] = (SELECT [Id] FROM [dbo].[INV_Documents] WHERE [PublicId] = '{Id(otra)}')");

        // (4) Caso b: sin rechazo confirmado no procede; confirmado, el reemplazo toma el mismo número y la anulación dice por qué.
        var borradorPrematuro = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{otraElectronica}/replacement-draft");
        await InventarioE2E.FallaAsync(borradorPrematuro, "ElectronicInvoicing.Document.RejectionNotConfirmed");
        await ConsultarAsync(http, t, otraElectronica);
        var borrador = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{otraElectronica}/replacement-draft");
        borrador.StatusCode.Should().Be(HttpStatusCode.Created, await borrador.Content.ReadAsStringAsync());
        var reemplazo = (await InventarioE2E.LeerAsync(borrador)).GetProperty("replacementDraftPublicId").GetGuid();
        // El borrador trae las líneas del rechazado; los pagos los pone quien lo edita (desde su propia sesión de caja).
        await EscenarioDeVentas.SesionAsync(http, t, esc.Ventas.Caja2);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"{Facturas}/{reemplazo}", new
        {
            documentTypePublicId = esc.Tipo("FE"), warehousePublicId = esc.Inv.Bodega("PV1"), counterpartyPersonPublicId = compradorB,
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 1m } },
            payments = new[] { esc.Ventas.Efectivo(EscenarioDeVentas.Precios["P7"]) },
        });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{otraElectronica}/replace",
            new { replacementDocumentPublicId = reemplazo, reason = "La DIAN rechazó la factura" });
        var confirmadoElReemplazo = await VentaAsync(http, t, reemplazo);
        confirmadoElReemplazo.GetProperty("prefix").GetString().Should().Be("FE");
        confirmadoElReemplazo.GetProperty("number").GetInt64().Should().Be((await VentaAsync(http, t, Id(otra))).GetProperty("number").GetInt64(),
            "el reemplazo conserva el número fiscal");
        (await esc.MensajesAsync(fx, "DocumentoAnulado", texto: "DianRejectionReplaced")).Should().Be(1);
        await EmitirAsync(http, t, otraElectronica);
        Estado(await DetalleAsync(http, t, otraElectronica)).Should().Be(Validado);

        // (5) Caso c: anulado sin reemplazo, con motivo y responsable.
        var tercera = await esc.FacturaConfirmadaAsync(http, supervisor, await PersonaAsync(http, t, "SinReemplazo", 1));
        var terceraElectronica = await ElectronicoAsync(http, t, Id(tercera));
        await EmitirAsync(http, t, terceraElectronica);
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{terceraElectronica}/cancel",
            new { reason = "El cliente desistió" }), "ElectronicInvoicing.Document.RejectionNotConfirmed");
        await ConsultarAsync(http, t, terceraElectronica);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Documentos}/{terceraElectronica}/cancel", new { reason = "El cliente desistió" });
        var cancelado = await DetalleAsync(http, t, terceraElectronica);
        Estado(cancelado).Should().Be(CanceladoSinReemplazo);
        cancelado.GetProperty("cancellation").GetProperty("reason").GetString().Should().Be("El cliente desistió");
    }

    [Fact]
    public async Task Tres_esperas_vencidas_del_POS_abren_la_03_y_la_cuarta_venta_sale_en_papel_con_la_numeracion_de_contingencia()
    {
        var esc = await PrepararAsync(fx, "fepos03");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var cajero = esc.Ventas.Cajero1;
        var sesion = await EscenarioDeVentas.SesionAsync(http, cajero.Token, esc.CajaElectronica);
        var enProceso = await PersonaAsync(http, t, "EnProceso", 3);

        // (1) Con el canal «en proceso», tres ventas quedan confirmadas y pendientes de entrega con su número normal.
        var pendientes = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            var cobro = await VentaPosAsync(http, esc, cajero.Token, sesion, enProceso);
            cobro.GetProperty("prefix").GetString().Should().Be("DE");
            cobro.GetProperty("number").GetInt64().Should().Be(i);
            var bloque = cobro.GetProperty("electronic");
            bloque.GetProperty("pendingDelivery").GetBoolean().Should().BeTrue($"venta {i}: {cobro}");
            bloque.GetProperty("deliverable").GetBoolean().Should().BeFalse("nunca se entrega un comprobante sin validar");
            pendientes.Add(bloque.GetProperty("electronicDocumentPublicId").GetGuid());
        }
        var pendientesDeEntrega = await InventarioE2E.GetAsync(http, cajero.Token,
            $"/api/inventory/sales/documents?pendingDelivery=true&cashSession={sesion}&pageSize=50");
        pendientesDeEntrega.GetProperty("items").GetArrayLength().Should().Be(3);

        // (2) La tercera falla seguida abrió la contingencia del facturador.
        var abierta = (await ContingenciasAsync(http, t, "?isOpen=true")).Single();
        abierta.GetProperty("type").GetInt32().Should().Be(3, "Issuer03");
        (await esc.AlertasAsync(http, "Dian.ContingenciaAbierta")).Should().NotBeEmpty();

        // (3) La cuarta venta sale en el acto con la numeración de contingencia y su representación de papel.
        var cuarta = await VentaPosAsync(http, esc, cajero.Token, sesion, null);
        cuarta.GetProperty("prefix").GetString().Should().Be("CDE", cuarta.ToString());
        cuarta.GetProperty("number").GetInt64().Should().Be(1);
        cuarta.GetProperty("electronic").GetProperty("contingencyType").GetInt32().Should().Be(3);
        cuarta.GetProperty("electronic").GetProperty("deliverable").GetBoolean().Should().BeTrue("el papel se entrega en el acto");
        cuarta.GetProperty("ticket").ValueKind.Should().Be(JsonValueKind.Object);
        var deContingencia = cuarta.GetProperty("electronic").GetProperty("electronicDocumentPublicId").GetGuid();
        Estado(await DetalleAsync(http, t, deContingencia)).Should().Be(ContingenciaDelFacturador);

        // (4) Al cerrar la 03, el procesador transmite lo de contingencia como tipo 03, referido a su número de papel.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/electronic-invoicing/contingencies/{abierta.GetProperty("contingencyPublicId").GetGuid()}/close",
            new { reason = "El canal volvió" });
        await ProcesarAsync(fx, esc.Coop.TenantPublicId);
        var transmitido = await DetalleAsync(http, t, deContingencia);
        Estado(transmitido).Should().BeOneOf([Validado, ValidadoConNotificaciones], transmitido.ToString());
        transmitido.GetProperty("document").GetProperty("number").GetString().Should().Be("CDE1");
        transmitido.GetProperty("document").GetProperty("contingencyType").GetInt32().Should().Be(3);

        // Las tres pendientes siguen con su número normal: nada se renumera.
        foreach (var (pendiente, n) in pendientes.Select((p, i) => (p, i + 1)))
        {
            if (Estado(await DetalleAsync(http, t, pendiente)) is not (Validado or ValidadoConNotificaciones)) await EmitirAsync(http, t, pendiente);
            var detalle = await DetalleAsync(http, t, pendiente);
            detalle.GetProperty("document").GetProperty("number").GetString().Should().Be($"DE{n}");
            Estado(detalle).Should().BeOneOf([Validado, ValidadoConNotificaciones], detalle.ToString());
        }
    }

    [Fact]
    public async Task Con_la_DIAN_caida_la_factura_se_entrega_en_contingencia_04_y_se_transmite_al_volver()
    {
        var esc = await PrepararAsync(fx, "fedian04");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var comprador = await PersonaAsync(http, t, "DianCaida", 4);

        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, comprador);
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        await EmitirAsync(http, t, electronico);

        // Numeración normal y código único; se entrega en el acto, marcado pendiente de validación de la DIAN.
        var enContingencia = await DetalleAsync(http, t, electronico);
        Estado(enContingencia).Should().Be(ContingenciaDian, enContingencia.ToString());
        enContingencia.GetProperty("document").GetProperty("number").GetString().Should().Be("FE1");
        enContingencia.GetProperty("document").GetProperty("contingencyType").GetInt32().Should().Be(4);
        var codigo = enContingencia.GetProperty("document").GetProperty("uniqueCode").GetString();
        codigo.Should().NotBeNullOrWhiteSpace();
        (await VentaAsync(http, t, Id(factura))).GetProperty("electronic").GetProperty("deliverable").GetBoolean().Should().BeTrue();
        (await ContingenciasAsync(http, t, "?isOpen=true")).Should().Contain(c => c.GetProperty("type").GetInt32() == 4);

        // La DIAN vuelve: se transmite con el mismo código y el 04 se cierra solo.
        await EmitirAsync(http, t, electronico);
        var validado = await DetalleAsync(http, t, electronico);
        Estado(validado).Should().Be(Validado, validado.ToString());
        validado.GetProperty("document").GetProperty("uniqueCode").GetString().Should().Be(codigo);
        (await ContingenciasAsync(http, t, "?isOpen=true")).Should().NotContain(c => c.GetProperty("type").GetInt32() == 4);
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    /// <summary>Una venta del POS de P7 a <paramref name="cliente"/> (o al consumidor final), cobrada en efectivo; devuelve el cobro.</summary>
    private static async Task<JsonElement> VentaPosAsync(HttpClient http, EscenarioDeFacturacionElectronica esc, string token, Guid sesion, Guid? cliente)
    {
        var venta = await EscenarioDeVentas.NuevaVentaAsync(http, token, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var id = EscenarioDeVentas.VentaId(venta);
        if (cliente is { } c)
            venta = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Patch, $"{EscenarioDeVentas.Borradores}/{id}", new { customerPersonPublicId = c });
        var aPagar = EscenarioDeVentas.AmountDue(venta);
        return await EscenarioDeVentas.CobrarAsync(http, token, id, aPagar, [esc.Ventas.Efectivo(aPagar)]);
    }

    private static async Task<List<JsonElement>> ContingenciasAsync(HttpClient http, string t, string filtro)
    {
        var r = await InventarioE2E.GetAsync(http, t, $"/api/electronic-invoicing/contingencies{filtro}");
        return (r.ValueKind == JsonValueKind.Array ? r : r.GetProperty("items")).EnumerateArray().ToList();
    }

    private Task<int> MensajesDelDocumentoAsync(EscenarioDeFacturacionElectronica esc, Guid documento) =>
        esc.ContarAsync(fx,
            $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "OriginPublicId" = '{documento}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [OriginPublicId] = '{documento}'");
}
