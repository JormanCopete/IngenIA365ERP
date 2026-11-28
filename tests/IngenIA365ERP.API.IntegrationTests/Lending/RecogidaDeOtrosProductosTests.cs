using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Lending.Deposits;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.RecogidaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T098 (feature 028, HU6 y HU7; FR-001, FR-025, FR-026; colección «Captaciones e2e», los dos motores por <c>DB_PROVIDER</c>): la recogida de otros productos
/// en el desembolso, por HTTP, en la cooperativa aislada «recogida_otros» (el escenario del desembolso con aportes activados sin carga):
/// <list type="number">
/// <item>la cooperativa admite recoger aportes (cuotas y abono) y facturas a crédito (<c>Recogida.ProductosAdmitidos</c>) y crea el tipo de documento de Recaudo
/// «Recogida en el desembolso»;</item>
/// <item>el asociado tiene una cuenta de aportes con dos cuotas vencidas (el ciclo diario del mes siguiente) y una factura a crédito de servicios por cobrar
/// (la cuenta por cobrar comercial que Recaudo consulta en Contabilidad);</item>
/// <item>un crédito nuevo paga las cuotas de aportes y la factura: el neto descuenta las dos, la cuenta recibe la consignación «por el desembolso», Recaudo emite el
/// recibo sin caja y la factura queda en cero;</item>
/// <item>otro crédito nuevo abona un aporte extraordinario: el neto lo descuenta y el resumen de aportes lo muestra como extraordinario.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public sealed class RecogidaDeOtrosProductosTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "recogida_otros";
    private const string CuentaPorCobrar = "16150598";
    private const string Ingreso = "41501099";
    private const decimal ValorDeLaFactura = 73_425m;
    private const decimal Cuota = 50_000m;
    private const decimal Extraordinario = 200_000m;

    private static int _documento;

    [Fact]
    public async Task Las_cuotas_de_aportes_un_extraordinario_y_una_factura_a_credito_se_recogen_en_el_desembolso()
    {
        var inicial = await DesembolsoE2E.EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var esc = await RefrescarAsync(http, inicial, Cooperativa);
        var admin = esc.Coop.TokenAdmin;
        var hoy = esc.Hoy;

        // ------------------------------------------------------------------------- aportes activados sin carga y su línea --
        await CaptacionesE2E.AlcanceTotalDelAdministradorAsync(fx, esc.Coop);
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);
        var activacion = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/deposits/activation", new
        {
            nature = "Contribution", activationDate = hoy, acknowledgeWarnings = true, reason = "Activación sin carga de aportes (e2e 028)",
        });
        if (activacion.StatusCode != HttpStatusCode.Created)
            (await CarteraE2E.CodigoDeErrorAsync(activacion)).Should().Be("Lending.Activation.AlreadyActivated", await activacion.Content.ReadAsStringAsync());
        admin = await LibranzaE2E.AdministradorDeLaCooperativaAsync(http, Cooperativa);
        var lineaDeAportes = await CrearAsync(http, admin, "/api/lending/lines/contribution", new
        {
            code = "APORTES", name = "Aportes sociales", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos, art. 20 (e2e 028)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                installment = new { kind = "FixedAmount", amount = Cuota, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                refundTermDays = 60,
            },
        });
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, lineaDeAportes };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        // ---------------------------------------- lo que la cooperativa admite recoger y el tipo de documento del recibo sin caja --
        await ParametroAsync(http, admin, "Recogida.ProductosAdmitidos", "Creditos:Total,Parcial;Aportes:Cuotas,Abono;FacturasComerciales:Total,Parcial", hoy);
        var tipo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/document-types", new
        {
            code = "RDE", name = "Recogida en el desembolso", kind = "LoanDisbursementReceipt", usesPointPrefix = false,
        });
        tipo.StatusCode.Should().Be(HttpStatusCode.Created, $"el tipo de documento de la recogida: «{await tipo.Content.ReadAsStringAsync()}»");

        // ------------------------------------------------------------ el asociado, su cuenta de aportes y su factura a crédito --
        var persona = await AsociadoAsync(http, admin, "Otros");
        var cuenta = await CrearAsync(http, admin, "/api/lending/accounts", new
        {
            linePublicId = lineaDeAportes, holderPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct",
            parties = Array.Empty<object>(), beneficiaries = Array.Empty<object>(), markGmfExempt = false,
        });
        var factura = $"FE-028-{Interlocked.Increment(ref _documento):D4}";
        await FacturaACreditoAsync(http, admin, persona, factura, hoy);
        var porCobrar = await CarteraE2E.GetAsync(http, admin, $"/api/receipts/commercial-receivables?person={persona}");
        porCobrar.EnumerateArray().Should().ContainSingle(f => f.GetProperty("invoiceNumber").GetString() == factura)
            .Which.GetProperty("pending").GetDecimal().Should().Be(ValorDeLaFactura);

        // El ciclo diario del día 6 del mes siguiente deja dos cuotas de aportes vencidas (la del período de la apertura y la siguiente).
        var dia = new DateOnly(hoy.Year, hoy.Month, 6).AddMonths(1);
        using var reloj = RelojEn(fx, dia);
        await CicloDeHoyAsync(fx, http, admin, esc.Coop.TenantPublicId);
        esc = await RefrescarAsync(http, esc, Cooperativa);
        admin = esc.Coop.TokenAdmin;
        var cuotas = await CuotasPendientesAsync(http, admin, persona);
        cuotas.Should().Be(2 * Cuota, "dos cuotas de aportes vencidas al día de la recogida");

        // ------------------------------------------------------------- HU6, HU7: los dos productos se pueden recoger --
        var candidatos = await CarteraE2E.ExitoAsync(http, esc.Usuarios.AsesorO1.Token, HttpMethod.Post, Candidatos, new
        {
            personPublicId = persona, creditLinePublicId = esc.Originacion.Origen, projectedDisbursementDate = dia,
        });
        var deAportes = candidatos.GetProperty("products").EnumerateArray().Single(p => p.GetProperty("productPublicId").GetGuid() == cuenta);
        deAportes.GetProperty("eligible").GetBoolean().Should().BeTrue(deAportes.ToString());
        var deLaFactura = candidatos.GetProperty("products").EnumerateArray().Single(p => p.GetProperty("number").GetString() == factura);
        deLaFactura.GetProperty("eligible").GetBoolean().Should().BeTrue(deLaFactura.ToString());
        deLaFactura.GetProperty("toPayOff").GetDecimal().Should().Be(ValorDeLaFactura);

        // ------------------------------------------------- el primer crédito nuevo paga las cuotas de aportes y la factura --
        const decimal Primero = 2_000_000m;
        var primera = await AprobadaQueRecogeAsync(http, esc, SolicitudQueRecoge(esc, persona, Primero, dia,
            [Cuenta(cuenta, "Installments"), Factura(factura)]));
        var hecho = await DesembolsarAsync(http, esc, primera.GetProperty("publicId").GetGuid(), persona);
        var netoPrimero = Primero - cuotas - ValorDeLaFactura;
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(netoPrimero, "el neto descuenta las cuotas de aportes y la factura");
        var recogidas = hecho.GetProperty("payoffs").EnumerateArray().ToList();
        var consignacion = recogidas.Single(p => p.GetProperty("kind").GetString() == "Account");
        consignacion.GetProperty("applied").GetDecimal().Should().Be(cuotas);
        var operacionDeCuotas = consignacion.GetProperty("accountOperationPublicId").GetGuid();
        var recibo = recogidas.Single(p => p.GetProperty("kind").GetString() == "CommercialReceivable");
        recibo.GetProperty("applied").GetDecimal().Should().Be(ValorDeLaFactura);
        recibo.GetProperty("reference").GetString().Should().Be(factura);
        var numeroDelRecibo = recibo.GetProperty("receiptNumber").GetString();
        numeroDelRecibo.Should().NotBeNullOrWhiteSpace("Recaudo emitió el recibo sin caja de la factura (FR-026)");
        var deLaOrden = await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{DesembolsoE2E.Ordenes}/{hecho.GetProperty("order").GetProperty("publicId").GetGuid()}");
        deLaOrden.GetProperty("netAmount").GetDecimal().Should().Be(netoPrimero);

        (await CuotasPendientesAsync(http, admin, persona)).Should().Be(0m, "las cuotas quedaron pagadas por el desembolso");
        (await CarteraE2E.GetAsync(http, admin, $"/api/receipts/commercial-receivables?person={persona}")).EnumerateArray()
            .Should().NotContain(f => f.GetProperty("invoiceNumber").GetString() == factura, "la factura quedó en cero");
        var recibos = await CarteraE2E.GetAsync(http, admin, "/api/receipts/receipts");
        (recibos.ValueKind == JsonValueKind.Array ? recibos : recibos.GetProperty("items")).EnumerateArray()
            .Should().Contain(r => r.GetProperty("number").GetString() == numeroDelRecibo, "el recibo de la recogida está en Recaudo");

        // ------------------------------------------------------------ el segundo crédito nuevo abona un aporte extraordinario --
        const decimal Segundo = 1_500_000m;
        var segunda = await AprobadaQueRecogeAsync(http, esc, SolicitudQueRecoge(esc, persona, Segundo, dia, [Cuenta(cuenta, "Deposit", Extraordinario)]));
        var otro = await DesembolsarAsync(http, esc, segunda.GetProperty("publicId").GetGuid(), persona);
        otro.GetProperty("netToDisburse").GetDecimal().Should().Be(Segundo - Extraordinario);
        var abono = otro.GetProperty("payoffs").EnumerateArray().Single();
        abono.GetProperty("mode").GetString().Should().Be("Deposit");
        abono.GetProperty("applied").GetDecimal().Should().Be(Extraordinario);
        var operacionDelAbono = abono.GetProperty("accountOperationPublicId").GetGuid();

        // La cuenta muestra los dos movimientos «por el desembolso» y el resumen de aportes el extraordinario.
        var operaciones = (await CarteraE2E.GetAsync(http, admin, $"/api/lending/accounts/{cuenta}/operations?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
        foreach (var (operacion, credito) in new[] { (operacionDeCuotas, hecho.GetProperty("loanNumber").GetString()!), (operacionDelAbono, otro.GetProperty("loanNumber").GetString()!) })
        {
            var op = operaciones.Should().ContainSingle(o => o.GetProperty("publicId").GetGuid() == operacion).Subject;
            op.GetProperty("reason").GetString().Should().Contain(credito, "el movimiento dice el crédito que lo pagó")
                .And.ContainEquivalentOf("desembolso");
        }
        var resumen = await CarteraE2E.GetAsync(http, admin, $"/api/lending/contributions/summary?personPublicId={persona}&date={dia:yyyy-MM-dd}");
        resumen.GetProperty("total").GetDecimal().Should().Be(cuotas + Extraordinario);
        resumen.GetProperty("byClass").GetProperty("extraordinary").GetDecimal().Should().Be(Extraordinario, "el abono entra como aporte extraordinario");
    }

    /// <summary>La vista previa y el desembolso por cheque con <c>jefe</c>, reconociendo diferencias si las hubiera; devuelve el resultado.</summary>
    private static async Task<JsonElement> DesembolsarAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid solicitud, Guid persona)
    {
        var cuerpo = DesembolsoE2E.Desembolso(esc, solicitud, persona);
        cuerpo["acknowledgedPayoffDifferences"] = true;
        var previa = await CarteraE2E.ExitoAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
        previa.GetProperty("blocking").GetArrayLength().Should().Be(0, previa.ToString());
        return await DesembolsoE2E.DesembolsarAsync(http, esc, cuerpo);
    }

    /// <summary>
    /// Una factura a crédito de servicios por cobrar al asociado: la cuenta por cobrar en la matriz de Recaudo (rol <c>CuentasPorCobrarComerciales</c>) y el
    /// comprobante que la carga con el asociado de tercero y la factura de cruce, como lo deja una venta a crédito de Comercial.
    /// </summary>
    private static async Task FacturaACreditoAsync(HttpClient http, string admin, Guid persona, string factura, DateOnly hoy)
    {
        await ContabilidadE2E.CrearCuentaAsync(http, admin, CuentaPorCobrar, "Deudores por prestación de servicios (e2e 028)", "161505", ["CNT", "REC", "INV"],
            tercero: true, cruce: true);
        await ContabilidadE2E.CrearCuentaAsync(http, admin, Ingreso, "Ingresos por servicios (e2e 028)", "415010", ["CNT", "INV"]);
        var reglas = await CarteraE2E.GetAsync(http, admin, "/api/accounting/receipts/rules?role=CuentasPorCobrarComerciales");
        if ((reglas.ValueKind == JsonValueKind.Array ? reglas : reglas.GetProperty("items")).GetArrayLength() == 0)
        {
            var regla = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/receipts/rules", new
            {
                factType = "*", role = "CuentasPorCobrarComerciales", accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, CuentaPorCobrar),
                validFrom = new DateOnly(hoy.Year, hoy.Month, 1), notes = "Cuentas por cobrar comerciales (e2e 028)",
                reason = "Las facturas a crédito que Recaudo cobra",
            });
            regla.IsSuccessStatusCode.Should().BeTrue($"la regla de las cuentas por cobrar comerciales: «{await regla.Content.ReadAsStringAsync()}»");
        }
        await ContabilidadE2E.ContabilizarAsync(http, admin, hoy, $"Factura a crédito {factura} (e2e 028)",
        [
            ContabilidadE2E.Linea(CuentaPorCobrar, null, ValorDeLaFactura, 0m, persona, "FV", factura, "Servicio facturado a crédito"),
            ContabilidadE2E.Linea(Ingreso, null, 0m, ValorDeLaFactura, detalle: "Servicio facturado a crédito"),
        ]);
    }

    /// <summary>Lo pendiente de las cuotas de aportes de la persona.</summary>
    private static async Task<decimal> CuotasPendientesAsync(HttpClient http, string token, Guid persona)
    {
        var cuotas = await CarteraE2E.GetAsync(http, token, $"/api/lending/contributions/installments?personPublicId={persona}&pageSize=100");
        return cuotas.GetProperty("items").EnumerateArray().Sum(c => c.GetProperty("pending").GetDecimal());
    }

    /// <summary>Un asociado (persona y afiliación) con sus autorizaciones de tratamiento de datos y de estudio del crédito.</summary>
    private static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_802_800_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Recogida", personType = "01", dateOfBirth = new DateOnly(1985, 3, 10),
                mobile = "3001234560", email = $"{nombre.ToLowerInvariant()}.{documento}@recogida.test",
            },
            associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "CreditProcessing");
        return persona;
    }

    private static async Task<Guid> CrearAsync(HttpClient http, string admin, string ruta, object cuerpo)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, ruta, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"{ruta}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }
}
