using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T150 (US8, escenarios 1 a 4 de la spec; quickstart §2; contracts/contabilidad.md §3 a §8 de la 014): Contabilidad recibe cada hecho de
/// captaciones por la matriz de la Cartera, sin que la Cartera escriba un asiento. En una cooperativa aislada con captaciones activadas
/// (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>), con la matriz de captaciones sembrada por la API (<see cref="MatrizAsync"/>) y el despachador
/// conducido a mano (<see cref="ConductorDelDespachador"/>):
/// <list type="number">
/// <item>Escenario 1: la causación diaria sale por lotes resumida: cada día un comprobante <c>CC</c> con el interés por pagar de la cuenta (que exige tercero)
/// en su propia línea.</item>
/// <item>Escenario 2: con <c>Contabilidad.FaltaDeRegla = Bloquear</c> y sin la regla del GMF, la pantalla bloquea el traslado diciendo el rol que falta y el
/// contrato rechaza el retiro con el mismo código; con «Advertir» el contrato aplica, sella <c>NotPostable</c> y el hecho queda rechazado en la bandeja.</item>
/// <item>Escenario 3: con captaciones activadas y sin el cierre de la Cartera, Contabilidad no cierra el mes.</item>
/// <item>Escenario 4: el comprobante de captaciones no se reversa desde Comprobantes; la conciliación de captaciones explica cada diferencia con los hechos que
/// Contabilidad todavía no recibe.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escribe y compila; no se corre por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class ContabilidadDeCaptacionesTests(CentralIdentityApiFixture fx)
{
    private const string Reglas = "/api/accounting/lending/rules";
    private const string Hechos = "/api/lending/accounting/facts";
    private const string Cuentas = "/api/lending/accounts";

    /// <summary>Las auxiliares de captaciones del ensayo (8 dígitos bajo las subcuentas del CUIF sembrado).</summary>
    public const string CuentaDeCapital = "21050598";
    public const string CuentaDeInteresPorPagar = "21059598";

    /// <summary>
    /// La matriz de captaciones del ahorro a la vista por la API: capital estándar y exento, inactivo, interés por pagar (exige tercero y cruce), gasto,
    /// retención, GMF, devolución por pagar y los puentes del recaudador de prueba y de Tesorería. <paramref name="conGmf"/> en falso deja el hueco del GMF.
    /// </summary>
    public static async Task MatrizAsync(HttpClient http, string admin, DateOnly desde, bool conGmf = true)
    {
        async Task ReglaAsync(string rol, object dimensiones, string cuenta, string nombre, string padre, bool tercero = false, bool cruce = false)
        {
            var existe = (await CarteraE2E.GetAsync(http, admin, $"/api/accounting/accounts/search?q={cuenta}&onlyMovement=false")).EnumerateArray()
                .Any(c => c.GetProperty("code").GetString() == cuenta);
            if (!existe) await ContabilidadE2E.CrearCuentaAsync(http, admin, cuenta, nombre, padre, ["CNT", "CAR"], tercero: tercero, cruce: cruce);
            var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Reglas, new
            {
                operation = "*", role = rol, dimensions = dimensiones, accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, cuenta), validFrom = desde,
                notes = "Matriz de captaciones (e2e de US8)", reason = "Ensayo de la integración contable de captaciones",
            });
            if (resp.StatusCode != HttpStatusCode.Created && (await CarteraE2E.CodigoDeErrorAsync(resp)) != "Accounting.LendingRule.Overlaps")
                resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la regla {rol}: «{await resp.Content.ReadAsStringAsync()}»");
        }

        await ReglaAsync("CapitalDeCaptacion", new { depositClass = "Demand", accountFlag = "Standard" }, CuentaDeCapital, "Ahorro a la vista (e2e US8)", "210505",
            tercero: true, cruce: true);
        await ReglaAsync("CapitalDeCaptacion", new { depositClass = "Demand", accountFlag = "GmfExempt" }, CuentaDeCapital, "Ahorro a la vista (e2e US8)", "210505",
            tercero: true, cruce: true);
        await ReglaAsync("CapitalDeCaptacionInactiva", new { depositClass = "Demand" }, "21051098", "Ahorro a la vista inactivo (e2e US8)", "210510", tercero: true);
        await ReglaAsync("InteresPorPagar", new { depositClass = "Demand" }, CuentaDeInteresPorPagar, "Intereses por pagar del ahorro (e2e US8)", "210595",
            tercero: true, cruce: true);
        await ReglaAsync("GastoPorIntereses", new { depositClass = "Demand" }, "61500598", "Intereses del ahorro a la vista (e2e US8)", "615005");
        await ReglaAsync("RetencionPorPagar", new { depositClass = "Demand" }, "24353598", "Retención sobre rendimientos (e2e US8)", "243535");
        if (conGmf) await ReglaAsync("GmfPorPagar", new { depositClass = "Demand" }, "24300598", "GMF sobre depósitos de ahorro (e2e US8)", "243005");
        await ReglaAsync("DevolucionPorPagar", new { lineNature = "Deposit", reasonCode = "DepositWithdrawal" }, "24651098", "Retiros por girar (e2e US8)", "246510");
        await ReglaAsync("RecaudoPorAplicar", new { collectorClass = "Receipts" }, "24101098", "Recaudo del recaudador de prueba (e2e US8)", "241010");
        await ReglaAsync("RecaudoPorAplicar", new { collectorClass = "Treasury" }, "24101099", "Puente de Tesorería (e2e US8)", "241010");
    }

    private async Task<(CaptacionesE2E.CooperativaDeCaptaciones Coop, Guid Oficina, Guid Linea, DateOnly Hoy)> CooperativaAsync(string nombre, bool conGmf = true)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, nombre);
        using var http = fx.CreateClient();
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        await MatrizAsync(http, coop.TokenAdmin, new DateOnly(hoy.Year, hoy.Month, 1), conGmf);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, coop.TokenAdmin, hoy, $"AHC{nombre[..Math.Min(4, nombre.Length)].ToUpperInvariant()}");
        return (coop, usuarios.O1.AgenciaPublicId, linea, hoy);
    }

    private static async Task<IReadOnlyList<JsonElement>> HechosAsync(HttpClient http, string token, string tipo) =>
        (await CarteraE2E.GetAsync(http, token, $"{Hechos}?type={tipo}&pageSize=100")).GetProperty("items").EnumerateArray().ToList();

    private static Task<JsonElement> ParametroAsync(HttpClient http, string token, string clave, string valor, DateOnly desde) =>
        CarteraE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions", new
        {
            scopeKind = 0, value = valor, validFrom = desde, reason = "Ensayo de la validación previa de captaciones (e2e)",
        });

    private static async Task<Guid> ComprobanteAsync(HttpClient http, string token, Guid mensaje)
    {
        var recibos = (await CarteraE2E.GetAsync(http, token, $"/api/accounting/lending/postings?message={mensaje}"))
            .GetProperty("items").EnumerateArray().Where(r => r.GetProperty("messagePublicId").GetGuid() == mensaje).ToList();
        recibos.Should().ContainSingle($"el hecho {mensaje} tiene un recibo");
        return recibos[0].GetProperty("accountingDocumentPublicId").GetGuid();
    }

    [Fact]
    public async Task US8_1_y_4_la_causacion_sale_resumida_en_un_CC_por_dia_y_su_comprobante_no_se_reversa_desde_Comprobantes()
    {
        var (coop, oficina, linea, hoy) = await CooperativaAsync("contab1");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var despachador = new ConductorDelDespachador(fx);
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Causa");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, oficina, linea, titular);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 50_000_000m, hoy, medio: "Transfer");
        await despachador.PasadasAsync(coop.TenantPublicId, 3);

        foreach (var dia in new[] { hoy.AddDays(1), hoy.AddDays(2) })
            using (CausacionE2E.RelojEn(fx, dia))
            {
                await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);
                await despachador.PasadasAsync(coop.TenantPublicId, 3);
                await despachador.PasadasAsync(coop.TenantPublicId, 3);
            }

        var causados = await HechosAsync(http, admin, "InteresDeCaptacionCausado");
        causados.Should().HaveCountGreaterThanOrEqualTo(2, "una parte por día causado");
        causados.Should().OnlyContain(h => h.GetProperty("status").GetString() == "Processed", string.Join(Environment.NewLine, causados));
        causados.Select(h => h.GetProperty("voucher").GetProperty("voucherTypeCode").GetString()).Should().OnlyContain(t => t == "CC");
        var cc = await ComprobanteAsync(http, admin, causados[0].GetProperty("messagePublicId").GetGuid());
        var lineas = (await CarteraE2E.GetAsync(http, admin, $"/api/accounting/documents/{cc}")).GetProperty("lines").EnumerateArray().ToList();
        lineas.Where(l => l.GetProperty("accountCode").GetString() == CuentaDeInteresPorPagar)
            .Should().NotBeEmpty().And.OnlyContain(l => l.GetProperty("personPublicId").ValueKind == JsonValueKind.String,
                "el interés por pagar exige tercero: una línea por cuenta con su titular");

        var consignacion = (await HechosAsync(http, admin, "ConsignacionEnCuentaAplicada")).Single();
        consignacion.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("MA");
        var ma = await ComprobanteAsync(http, admin, consignacion.GetProperty("messagePublicId").GetGuid());
        var reverso = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{ma}/reverse", new { reason = "Desde Comprobantes (e2e)" });
        reverso.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await reverso.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(reverso)).Should().Be("Accounting.Document.ModuleOwned");
        (await reverso.Content.ReadAsStringAsync()).Should().Contain("captaciones y aportes");

        var conciliacion = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, admin,
            $"/api/reports/lending/deposits-reconciliation?asOf={hoy.AddDays(2):yyyy-MM-dd}"));
        conciliacion.Filas.Should().NotBeEmpty("el capital y el interés por pagar del ahorro se concilian");
        conciliacion.Filas.Should().OnlyContain(f => ContabilidadE2E.Tabla.Celda(f, conciliacion.Indice("Diferencia")).GetDecimal()
                                                     == ContabilidadE2E.Tabla.Celda(f, conciliacion.Indice("Explicada")).GetDecimal(),
            "toda diferencia la explican los hechos que Contabilidad todavía no recibe");
    }

    [Fact]
    public async Task US8_2_sin_la_regla_del_GMF_la_pantalla_y_el_contrato_bloquean_y_con_Advertir_el_contrato_aplica_y_sella()
    {
        var (coop, oficina, linea, hoy) = await CooperativaAsync("contab2", conGmf: false);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var despachador = new ConductorDelDespachador(fx);
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Bloqueo");
        var origen = await MovimientosTests.CuentaAsync(http, admin, oficina, linea, titular);
        var destino = await MovimientosTests.CuentaAsync(http, admin, oficina, await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHCDEST"), titular);
        await MovimientosTests.ConsignarAsync(http, admin, origen, titular, 2_000_000m, hoy, medio: "Transfer");
        await ParametroAsync(http, admin, "Contabilidad.FaltaDeRegla", "Bloquear", hoy);

        var traslado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{origen}/operations/transfers", new
        {
            targetAccountPublicId = destino, amount = 100_000m, valueDate = hoy, signatures = new[] { titular }, reason = "Traslado (e2e US8)",
        });
        traslado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await traslado.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(traslado)).Should().Be("Lending.Prevalidation.NotPostable");
        (await CarteraE2E.LeerAsync(traslado)).GetProperty("data").GetProperty("errors").EnumerateArray()
            .Should().Contain(e => e.GetProperty("role").GetString() == "GmfPorPagar", "dice la regla que falta");

        var retiro = await MovimientosTests.RetirarAsync(http, admin, origen, titular, 100_000m, hoy);
        var rechazado = await CarteraE2E.LeerAsync(retiro);
        rechazado.ToString().Should().Contain("Lending.Prevalidation.NotPostable", "A8 = «Bloquear todo»: el contrato también rechaza");

        // Una vigencia nueva del parámetro empieza otro día (Parameters.Overlaps): Advertir rige desde mañana y el retiro se hace con el reloj de mañana.
        var manana = hoy.AddDays(1);
        await ParametroAsync(http, admin, "Contabilidad.FaltaDeRegla", "Advertir", manana);
        using var reloj = CausacionE2E.RelojEn(fx, manana);
        (await MovimientosTests.RetirarAsync(http, admin, origen, titular, 100_000m, manana)).StatusCode.Should().Be(HttpStatusCode.OK);
        await despachador.PasadasAsync(coop.TenantPublicId, 3);
        var retiros = await HechosAsync(http, admin, "RetiroDeCuentaAplicado");
        retiros.Should().ContainSingle();
        retiros[0].GetProperty("prevalidationOutcome").GetString().Should().Be("NotPostable", "con Advertir el contrato aplica y sella");
        retiros[0].GetProperty("status").GetString().Should().Be("Rejected", "sin la regla del GMF la unidad no se contabiliza y queda para reprocesar");
    }

    [Fact]
    public async Task US8_3_con_captaciones_activadas_y_sin_el_cierre_de_la_Cartera_Contabilidad_no_cierra_el_mes()
    {
        var (coop, _, _, hoy) = await CooperativaAsync("contab3");
        using var http = fx.CreateClient();
        // El bloqueo se lee de los recibos del informativo CaptacionesActivadas en Contabilidad: hay que dejar que el despachador lo entregue.
        await new ConductorDelDespachador(fx).PasadasAsync(coop.TenantPublicId, 3);
        await CierreE2E.CerrarContabilidadAsync(http, coop.TokenAdmin, hoy.Year, hoy.Month, "Accounting.Period.LendingNotClosed");
    }
}
