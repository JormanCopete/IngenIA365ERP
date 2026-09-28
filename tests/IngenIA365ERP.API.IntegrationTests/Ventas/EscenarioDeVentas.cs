using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// La cooperativa de ensayo de ventas de quickstart §5.1 reducida a lo que usan las e2e de I3 (T563–T570, T650), sobre el
/// escenario de inventario de I1 (<see cref="EscenarioDeInventario"/>, bodegas activadas): <b>no obligada</b> a facturar
/// (<c>Dian.ObligadaAFacturar = false</c>, hasta I4), los tipos <c>RV</c> (comprobante de venta no electrónico) y <c>NV</c> (su
/// nota), el canal <c>MOSTRADOR</c>, el punto <c>PV1</c> (bodega PV1) con las cajas <c>CJ1</c> y <c>CJ2</c>, los medios de §5.1
/// (<c>EFECTIVO</c> sembrado, <c>VISARB</c>, <c>MCCB</c>, <c>BONOMERC</c>, <c>TRANSFBCO</c>, <c>CREDASOC</c>) con sus franquicias,
/// adquirentes y datáfonos, la lista <c>GENERAL</c> (con IVA incluido) de P1 a P7, existencias en PV1 y los usuarios
/// <c>cajero.1</c>, <c>cajero.2</c> (tope 5 %, crédito hasta 100.000) y <c>supervisor</c> (tope 15 %, crédito hasta 1.000.000),
/// todos con alcance sobre PV1. Con <paramref name="contabilidad"/> la cooperativa inicia la contabilidad y lleva la matriz de I2
/// (<see cref="ContabilidadDeInventarioE2E"/>). Todo por HTTP, con el administrador. (nuevo)
/// </summary>
public sealed class EscenarioDeVentas
{
    public required EscenarioDeInventario Inv { get; init; }
    public InventarioE2E.CooperativaAislada Coop => Inv.Coop;
    public string Admin => Inv.Admin;
    public required Guid Canal { get; init; }
    public required Guid Punto { get; init; }
    public required Guid Caja1 { get; init; }
    public required Guid Caja2 { get; init; }
    public required Guid TipoRv { get; init; }
    public required Guid TipoNv { get; init; }
    public required IReadOnlyDictionary<string, Guid> Medios { get; init; }
    public required Guid DatafonoRedeban { get; init; }
    public required Guid DatafonoCredibanco { get; init; }
    public required Guid ListaGeneral { get; init; }
    public required Usuario Cajero1 { get; init; }
    public required Usuario Cajero2 { get; init; }
    public required Usuario Supervisor { get; init; }
    public required Guid RolCajero { get; init; }
    public required Guid RolSupervisor { get; init; }

    public sealed record Usuario(string Token, Guid PublicId, string Correo);

    public Guid Medio(string codigo) => Medios[codigo];

    /// <summary>Los precios de la lista general (con IVA incluido): P7, excluido de IVA, a 100.000 para las ventas redondas.</summary>
    public static readonly IReadOnlyDictionary<string, decimal> Precios = new Dictionary<string, decimal>
    {
        ["P1"] = 2_380m, ["P2"] = 5_950m, ["P3"] = 11_900m, ["P4"] = 3_570m, ["P5"] = 8_330m, ["P6"] = 1_190m, ["P7"] = 100_000m,
    };

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<EscenarioDeVentas>> Preparados = [];
    private static readonly object Cerrojo = new();

    /// <summary>El escenario de ese nombre (uno por fixture: las pruebas que piden el mismo nombre lo comparten).</summary>
    public static Task<EscenarioDeVentas> PrepararAsync(CentralIdentityApiFixture fx, string nombre, bool contabilidad = false)
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx, nombre, contabilidad);
                Preparados[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<EscenarioDeVentas> PrepararDeVerdadAsync(CentralIdentityApiFixture fx, string nombre, bool contabilidad)
    {
        var inv = contabilidad
            ? await ContabilidadDeInventarioE2E.PrepararAsync(fx, nombre)
            : await EscenarioDeInventario.PrepararAsync(fx, nombre);
        using var http = fx.CreateClient();
        var t = inv.Admin;
        var desde = inv.Corte.AddDays(1);

        // Cooperativa no obligada a facturar electrónicamente mientras no exista I4 (quickstart §5.13).
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/EINV/Dian.ObligadaAFacturar/versions", new
        {
            scopeKind = "None", value = "false", validFrom = desde.ToString("yyyy-MM-dd"), reason = "Ensayo de ventas sin facturación electrónica",
        });

        // Tipos de venta con su consecutivo.
        var tipoRv = await TipoAsync(http, t, "RV", "Comprobante de venta", "NonElectronicSalesReceipt", desde);
        var tipoNv = await TipoAsync(http, t, "NV", "Nota de venta", "NonElectronicSalesNote", desde);

        // Canal, punto y cajas.
        var canal = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/sales-channels", new { code = "MOSTRADOR", name = "Mostrador" }))
            .GetProperty("publicId").GetGuid();
        var punto = await PuntoAsync(http, t, "PV1", "Almacén Florida", inv.S1, canal, inv.Bodega("PV1"), posHabilitado: true);

        // Medios de pago de §5.1 (EFECTIVO lo siembra DefaultPaymentMeansSeeder).
        var visa = await CreadoAsync(http, t, "/api/core/card-networks", new { code = "VISA", name = "Visa", cardKind = "Both" }, "cardNetworkPublicId");
        var master = await CreadoAsync(http, t, "/api/core/card-networks", new { code = "MASTER", name = "Mastercard", cardKind = "Both" }, "cardNetworkPublicId");
        var redeban = await CreadoAsync(http, t, "/api/core/card-acquirers", new { code = "REDEBAN", name = "Redeban" }, "cardAcquirerPublicId");
        var credibanco = await CreadoAsync(http, t, "/api/core/card-acquirers", new { code = "CREDIBANCO", name = "Credibanco" }, "cardAcquirerPublicId");
        var datafonoRedeban = await CreadoAsync(http, t, "/api/core/card-terminals", new { code = "TER01", cardAcquirerPublicId = redeban }, "cardTerminalPublicId");
        var datafonoCredibanco = await CreadoAsync(http, t, "/api/core/card-terminals", new { code = "TER02", cardAcquirerPublicId = credibanco }, "cardTerminalPublicId");

        var banco = await CreadoAsync(http, t, "/api/core/banks", new { code = "BCOENS", name = "Banco del ensayo", shortName = "BCOENS", transferCode = "1099" },
            "bankPublicId");

        var medios = new Dictionary<string, Guid>
        {
            ["EFECTIVO"] = (await InventarioE2E.GetAsync(http, t, "/api/core/payment-means")).EnumerateArray()
                .Single(m => m.GetProperty("code").GetString() == "EFECTIVO").GetProperty("paymentMeansPublicId").GetGuid(),
            ["VISARB"] = await MedioAsync(http, t, desde, new { code = "VISARB", name = "Visa Redeban", displayOrder = 2, @class = "CreditCard",
                cardNetworkPublicId = visa, cardAcquirerPublicId = redeban, countMethod = "VoucherTotal", dianPaymentMeansCode = "48" }),
            ["MCCB"] = await MedioAsync(http, t, desde, new { code = "MCCB", name = "Mastercard Credibanco", displayOrder = 3, @class = "CreditCard",
                cardNetworkPublicId = master, cardAcquirerPublicId = credibanco, countMethod = "VoucherTotal", dianPaymentMeansCode = "48" }),
            ["BONOMERC"] = await MedioAsync(http, t, desde, new { code = "BONOMERC", name = "Bono de mercancía", displayOrder = 4, @class = "Voucher",
                requiresReference = true, referenceKind = "VoucherNumber", uniqueReference = true, countMethod = "ByReference", dianPaymentMeansCode = "71" }),
            ["TRANSFBCO"] = await MedioAsync(http, t, desde, new { code = "TRANSFBCO", name = "Transferencia", displayOrder = 5, @class = "Transfer",
                requiresReference = true, referenceKind = "Receipt", countMethod = "ByReference", bankPublicId = banco, destinationAccountNumber = "001122334455",
                destinationAccountType = 1, dianPaymentMeansCode = "47" }),
            ["CREDASOC"] = await MedioAsync(http, t, desde, new { code = "CREDASOC", name = "Crédito de asociado", displayOrder = 6, @class = "AssociateCredit",
                countMethod = "None", allowsPartial = true, dianPaymentMeansCode = "1",
                creditDefaults = new { termDays = 90, maxInstallments = 6, periodicityDays = 30, suggestedLineCode = "CONSUMO", maxTermDays = 180 } }),
        };

        var roles = new[] { ("PosSale", tipoRv), ("PosAdjustmentNote", tipoNv) };
        var caja1 = await CajaAsync(http, t, punto, "CJ1", "Caja 1", inv.Bodega("PV1"), datafonoRedeban, roles);
        var caja2 = await CajaAsync(http, t, punto, "CJ2", "Caja 2", inv.Bodega("PV1"), datafonoRedeban, roles);

        // Lista general con IVA incluido.
        var lista = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/price-lists", new
        {
            code = "GENERAL", name = "Lista general", includesTaxes = true, scope = new { }, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Lista del ensayo",
        })).GetProperty("priceListPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{lista}/items", new
        {
            items = Precios.Select(p => new { productPublicId = inv.P(p.Key).Id, unitPublicId = inv.P(p.Key).Unidad, price = p.Value }).ToList(),
            reason = "Precios del ensayo",
        });

        // Existencias en PV1 (P6 con pocas: la carrera de §5.3 la mide otra prueba).
        await inv.AjusteConfirmadoAsync(http, t, "AJP", "PV1",
        [
            new("P1", 500, 1_000m), new("P2", 500, 2_500m), new("P3", 500, 5_000m), new("P4", 500, 1_150m), new("P5", 500, 4_000m),
            new("P6", 500, 500m), new("P7", 500, 60_000m),
        ]);

        // Roles, montos, topes, usuarios y alcance (quickstart §2.4).
        var rolCajero = await InventarioE2E.RolAsync(http, inv.Coop, "CAJERO", "inventario.cajero");
        var rolSupervisor = await InventarioE2E.RolAsync(http, inv.Coop, "SUPERVISOR", "inventario.aprobador",
            extra: ["Inventory.Discounts.Authorize", "Inventory.Sales.SellOnCredit", "Inventory.Pos.Sell", "Inventory.Sales.View",
                "Inventory.CashDifferences.Approve", "Inventory.CashMovements.Approve", "Inventory.DayClose.Execute", "Inventory.CashSessions.ViewAll",
                "Inventory.CashSessions.Open", "Inventory.CashSessions.Close", "Inventory.Sales.Create", "Inventory.Sales.Confirm"]);
        await MontoAsync(http, t, rolCajero, "Inventory.Sales.SellOnCredit", 100_000m, desde);
        await MontoAsync(http, t, rolSupervisor, "Inventory.Sales.SellOnCredit", 1_000_000m, desde);
        await TopeAsync(http, t, rolCajero, 0.05m, desde);
        await TopeAsync(http, t, rolSupervisor, 0.15m, desde);

        var cajero1 = await UsuarioAsync(fx, http, inv, $"cajero1.{nombre}@coop.ventas.test", rolCajero, punto);
        var cajero2 = await UsuarioAsync(fx, http, inv, $"cajero2.{nombre}@coop.ventas.test", rolCajero, punto);
        var supervisor = await UsuarioAsync(fx, http, inv, $"supervisor.{nombre}@coop.ventas.test", rolSupervisor, punto);

        return new EscenarioDeVentas
        {
            Inv = inv, Canal = canal, Punto = punto, Caja1 = caja1, Caja2 = caja2, TipoRv = tipoRv, TipoNv = tipoNv, Medios = medios,
            DatafonoRedeban = datafonoRedeban, DatafonoCredibanco = datafonoCredibanco, ListaGeneral = lista,
            Cajero1 = cajero1, Cajero2 = cajero2, Supervisor = supervisor, RolCajero = rolCajero, RolSupervisor = rolSupervisor,
        };
    }

    // ---------------------------------------------------------------------------------------------- alta --

    public static async Task<Guid> TipoAsync(HttpClient http, string t, string codigo, string nombre, string clase, DateOnly desde)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/document-types", new
        {
            code = codigo, name = nombre, @class = clase, requiresCounterparty = false, requiresCostCenter = false, requiresReason = false,
            requiresExternalReference = false, prefix = codigo, firstNumber = 1, validFrom = desde.ToString("yyyy-MM-dd"),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"tipo {codigo}: {await resp.Content.ReadAsStringAsync()}");
        return Id(await InventarioE2E.LeerAsync(resp), "documentTypePublicId");
    }

    public static async Task<Guid> PuntoAsync(HttpClient http, string t, string codigo, string nombre, Guid sucursal, Guid canal, Guid bodega, bool posHabilitado)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/points-of-sale", new
        {
            code = codigo, name = nombre, branchPublicId = sucursal, salesChannelPublicId = canal, posEnabled = posHabilitado, defaultWarehousePublicId = bodega,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"punto {codigo}: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("pointOfSalePublicId").GetGuid();
    }

    public static async Task<Guid> CajaAsync(HttpClient http, string t, Guid punto, string codigo, string nombre, Guid bodega, Guid? datafono,
        (string Rol, Guid Tipo)[] roles)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/inventory/points-of-sale/{punto}/cash-registers", new
        {
            code = codigo, name = nombre, warehousePublicId = bodega, defaultCardTerminalPublicId = datafono, printFormat = "Ticket80",
            documentTypes = roles.Select(r => new { role = r.Rol, documentTypePublicId = r.Tipo }).ToList(),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"caja {codigo}: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("cashRegisterPublicId").GetGuid();
    }

    private static async Task<Guid> CreadoAsync(HttpClient http, string t, string ruta, object cuerpo, string campo)
    {
        var resp = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, ruta, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"{ruta}: {await resp.Content.ReadAsStringAsync()}");
        return Id(await InventarioE2E.LeerAsync(resp), campo);
    }

    /// <summary>El id de una respuesta de alta: el campo dado, <c>publicId</c> o el cuerpo si es un GUID suelto.</summary>
    private static Guid Id(JsonElement cuerpo, string campo)
    {
        if (cuerpo.ValueKind == JsonValueKind.String) return cuerpo.GetGuid();
        if (cuerpo.TryGetProperty(campo, out var v)) return v.GetGuid();
        return cuerpo.GetProperty("publicId").GetGuid();
    }

    private static async Task<Guid> MedioAsync(HttpClient http, string t, DateOnly desde, object datos)
    {
        // Se completa con la vigencia: PaymentMeansInput la exige.
        var json = JsonSerializer.SerializeToNode(datos)!.AsObject();
        json["validFrom"] = desde.ToString("yyyy-MM-dd");
        return await CreadoAsync(http, t, "/api/core/payment-means", json, "paymentMeansPublicId");
    }

    private static Task<JsonElement> MontoAsync(HttpClient http, string t, Guid rol, string permiso, decimal monto, DateOnly desde) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/amount-limits", new
        {
            rolePublicId = rol, permissionCode = permiso, maxAmount = monto, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Monto del ensayo",
        });

    private static Task<JsonElement> TopeAsync(HttpClient http, string t, Guid rol, decimal porcentaje, DateOnly desde) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/discount-caps", new
        {
            rolePublicId = rol, maxLinePercent = porcentaje, maxDocumentPercent = porcentaje, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Tope del ensayo",
        });

    private static async Task<Usuario> UsuarioAsync(CentralIdentityApiFixture fx, HttpClient http, EscenarioDeInventario inv, string correo, Guid rol, Guid punto)
    {
        var (token, id) = await InventarioE2E.UsuarioAsync(fx, http, inv.Coop, correo);
        var asignacion = await InventarioE2E.EnviarAsync(http, inv.Admin, HttpMethod.Post, $"/api/admin/users/{id}/roles", new { rolePublicId = rol });
        asignacion.IsSuccessStatusCode.Should().BeTrue($"rol a {correo}: {await asignacion.Content.ReadAsStringAsync()}");
        await InventarioE2E.ExitoAsync(http, inv.Admin, HttpMethod.Put, $"/api/inventory/scopes/users/{id}", new
        {
            warehouses = new[] { new { warehousePublicId = inv.Bodega("PV1"), isDefault = true } },
            pointsOfSale = new[] { new { pointOfSalePublicId = punto, isDefault = true } },
        });
        return new Usuario(token, id, correo);
    }

    // ------------------------------------------------------------------------------------- caja y POS --

    public const string Sesiones = "/api/inventory/cash-sessions";
    public const string Borradores = "/api/inventory/pos/drafts";

    /// <summary><c>POST /cash-sessions</c> sin juzgar la respuesta.</summary>
    public static Task<HttpResponseMessage> PedirAbrirAsync(HttpClient http, string token, Guid caja, decimal baseInicial = 200_000m) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, Sesiones, new { cashRegisterPublicId = caja, openingBase = baseInicial });

    /// <summary>Abre una sesión y devuelve su id.</summary>
    public static async Task<Guid> AbrirAsync(HttpClient http, string token, Guid caja, decimal baseInicial = 200_000m)
    {
        var resp = await PedirAbrirAsync(http, token, caja, baseInicial);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la sesión: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("session").GetProperty("cashSessionPublicId").GetGuid();
    }

    /// <summary>La sesión abierta del usuario (<c>mine=true&amp;status=Open</c>) o, si no tiene, una nueva en esa caja.</summary>
    public static async Task<Guid> SesionAsync(HttpClient http, string token, Guid caja)
    {
        var mias = await InventarioE2E.GetAsync(http, token, $"{Sesiones}?mine=true&status=Open&pageSize=5");
        var abierta = mias.GetProperty("items").EnumerateArray().FirstOrDefault();
        return abierta.ValueKind == JsonValueKind.Object ? abierta.GetProperty("cashSessionPublicId").GetGuid() : await AbrirAsync(http, token, caja);
    }

    /// <summary>
    /// Los eventos de auditoría de esa acción que mencionan el texto (el documento), después de llevar la bandeja de auditoría de la
    /// cooperativa a Mongo (los eventos del comercio van por <c>COR_AuditOutbox</c>).
    /// </summary>
    public async Task<List<JsonElement>> AuditadosAsync(CentralIdentityApiFixture fx, HttpClient http, string accion, string texto)
    {
        await fx.ReenviarAuditoriaAsync(Coop.TenantPublicId);
        await Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<Application.Common.Interfaces.IAuditService>(fx.Factory.Services).FlushAsync();
        var pagina = await InventarioE2E.GetAsync(http, Admin,
            $"/api/audit/logs?action={Uri.EscapeDataString(accion)}&from={DateTime.UtcNow.AddHours(-2):O}&to={DateTime.UtcNow.AddMinutes(5):O}&pageSize=200");
        return pagina.GetProperty("items").EnumerateArray().Where(e => e.ToString().Contains(texto, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>La decisión de una aprobación respondió 200 (si no, el mensaje lleva el cuerpo).</summary>
    public static async Task AprobadaAsync(HttpResponseMessage decision) =>
        decision.StatusCode.Should().Be(HttpStatusCode.OK, $"aprobar: {await decision.Content.ReadAsStringAsync()}");

    /// <summary>La venta nueva del POS con su primera lectura (opcional).</summary>
    public static async Task<JsonElement> NuevaVentaAsync(HttpClient http, string token, Guid sesion, string? primerCodigo = null)
    {
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, Borradores, new
        {
            cashSessionPublicId = sesion, firstLine = primerCodigo is null ? null : new { code = primerCodigo },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la venta: {await resp.Content.ReadAsStringAsync()}");
        return await InventarioE2E.LeerAsync(resp);
    }

    /// <summary>Una lectura del lector.</summary>
    public static Task<JsonElement> LeerCodigoAsync(HttpClient http, string token, Guid venta, string codigo, decimal? cantidad = null) =>
        InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"{Borradores}/{venta}/lines", new { code = codigo, quantity = cantidad });

    public static decimal AmountDue(JsonElement venta) => venta.GetProperty("totals").GetProperty("amountDue").GetDecimal();

    public static Guid VentaId(JsonElement venta) => venta.GetProperty("draftPublicId").GetGuid();

    /// <summary><c>POST /pos/drafts/{id}/checkout</c> sin juzgar la respuesta.</summary>
    public static Task<HttpResponseMessage> PedirCobrarAsync(HttpClient http, string token, Guid venta, decimal esperado, object[] pagos, Guid? clave = null) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Borradores}/{venta}/checkout", new { payments = pagos, expectedAmountDue = esperado }, clave);

    /// <summary>Cobra y exige 200; devuelve el <c>CheckoutResultDto</c>.</summary>
    public static async Task<JsonElement> CobrarAsync(HttpClient http, string token, Guid venta, decimal esperado, object[] pagos)
    {
        var resp = await PedirCobrarAsync(http, token, venta, esperado, pagos);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"cobrar: {await resp.Content.ReadAsStringAsync()}");
        return await InventarioE2E.LeerAsync(resp);
    }

    public object Efectivo(decimal valor, decimal? entregado = null) => new { paymentMeansPublicId = Medio("EFECTIVO"), amount = valor, tendered = entregado };

    /// <summary>Una venta de contado en el POS con esas lecturas, cobrada en efectivo; devuelve el resultado del cobro.</summary>
    public async Task<JsonElement> VentaDeContadoAsync(HttpClient http, string token, Guid sesion, params string[] productos)
    {
        var venta = await NuevaVentaAsync(http, token, sesion, Inv.P(productos[0]).CodigoDeBarras);
        foreach (var p in productos.Skip(1)) venta = await LeerCodigoAsync(http, token, VentaId(venta), Inv.P(p).CodigoDeBarras);
        return await CobrarAsync(http, token, VentaId(venta), AmountDue(venta), [Efectivo(AmountDue(venta))]);
    }

    /// <summary>
    /// Cierra una sesión contando exactamente lo esperado por medio (efectivo por total, tarjetas por su lote, referencias
    /// marcadas); <paramref name="ajuste"/> cambia lo contado de un medio (con motivo). Devuelve la respuesta sin juzgarla.
    /// </summary>
    public async Task<HttpResponseMessage> CerrarAsync(HttpClient http, string token, Guid sesion, IReadOnlyDictionary<string, decimal>? ajuste = null)
    {
        var esperado = await InventarioE2E.GetAsync(http, token, $"{Sesiones}/{sesion}/expected");
        var conteos = new List<object>();
        foreach (var linea in esperado.GetProperty("lines").EnumerateArray())
        {
            var medio = linea.GetProperty("paymentMeans");
            var codigo = medio.GetProperty("code").GetString()!;
            var metodo = medio.GetProperty("countMethod").GetInt32();
            var cuanto = linea.GetProperty("expected").GetDecimal() + (ajuste?.GetValueOrDefault(codigo) ?? 0m);
            var motivo = ajuste?.ContainsKey(codigo) == true ? "Diferencia del ensayo" : null;
            var id = medio.GetProperty("publicId").GetGuid();
            switch (metodo)
            {
                case 1:
                    conteos.Add(new { paymentMeansPublicId = id, countedTotal = cuanto, reason = motivo });
                    break;
                case 2:
                    var lotes = linea.TryGetProperty("terminals", out var ts) && ts.ValueKind == JsonValueKind.Array
                        ? ts.EnumerateArray().Select((d, i) => (object)new
                        {
                            cardTerminalPublicId = d.GetProperty("cardTerminalPublicId").GetGuid(), batchNumber = $"L{i + 1:000}",
                            total = d.GetProperty("expected").GetDecimal(), count = d.GetProperty("paymentsCount").GetInt32(),
                        }).ToList()
                        : [];
                    conteos.Add(new { paymentMeansPublicId = id, terminalBatches = lotes, reason = motivo });
                    break;
                case 3:
                    var marcas = linea.TryGetProperty("references", out var rs) && rs.ValueKind == JsonValueKind.Array
                        ? rs.EnumerateArray().Select(r => (object)new { documentPaymentPublicId = r.GetProperty("documentPaymentPublicId").GetGuid(), @checked = true }).ToList()
                        : [];
                    conteos.Add(new { paymentMeansPublicId = id, referenceChecks = marcas, reason = motivo });
                    break;
                default:
                    conteos.Add(new { paymentMeansPublicId = id });
                    break;
            }
        }
        return await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Sesiones}/{sesion}/close", new { counts = conteos });
    }
}
