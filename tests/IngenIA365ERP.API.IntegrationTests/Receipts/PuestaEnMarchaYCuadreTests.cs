using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0285 (US13 «Independent Test»; quickstart recorrido L pasos 1–3; SC-021; FR-157, FR-158): la puesta en marcha por plantillas y el cuadre de
/// apertura contra Contabilidad, sobre una cooperativa aislada con la contabilidad iniciada y sin activar Recaudo ni Tesorería:
/// <list type="number">
/// <item><c>recaudo.bovedas-al-corte</c> con un error de fila: la revisión dice fila y columna; aplicar es todo o nada (<c>Import.Invalid</c>) y no guarda
/// nada; corregida, entra;</item>
/// <item>las cuentas bancarias propias con su saldo al corte y su vínculo contable, la chequera, los cheques girados en tránsito y las partidas por identificar al
/// corte: <b>ningún hecho contable</b> (ni mensaje a Contabilidad ni comprobante de Recaudo o Tesorería);</item>
/// <item>el cuadre contra Contabilidad muestra la diferencia por concepto (lo cargado contra un libro en cero) y el definitivo no se registra con ella y sin acta
/// (<c>{Receipts|Treasury}.Activation.SquareHasDifference</c>).</item>
/// </list>
/// La activación con el cuadre provisional y el cierre del mes la recorre <see cref="CuadreProvisionalYCierreDelMesTests"/>. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class PuestaEnMarchaYCuadreTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_plantilla_con_error_no_guarda_nada_la_carga_al_corte_no_contabiliza_y_el_cuadre_muestra_la_diferencia()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "puesta_en_marcha", contabilidadIniciada: true);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var corte = CarteraE2E.Hoy(fx).AddDays(-1);
        await PuestaEnMarchaE2E.BovedaAsync(http, admin, coop);

        // 1. La bóveda al corte con un error de fila (cantidad cero en la segunda fila de datos, la fila 3 del libro).
        var conError = PuestaEnMarchaE2E.LibroDeBovedas(corte, (50_000m, 100), (20_000m, 0), saldo: 6_000_000m);
        var revision = await InventarioE2E.ImportarAsync(http, admin, PuestaEnMarchaE2E.Recaudo(PuestaEnMarchaE2E.Bovedas), "review", conError);
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var revisada = await RecaudoYTesoreriaE2E.LeerAsync(revision);
        revisada.GetProperty("valid").GetBoolean().Should().BeFalse();
        revisada.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("row").GetInt32() == 3
            && e.GetProperty("column").GetString() == "cantidad", $"la revisión dice fila y columna: {revisada}");

        var aplicarConError = await InventarioE2E.ImportarAsync(http, admin, PuestaEnMarchaE2E.Recaudo(PuestaEnMarchaE2E.Bovedas), "apply", conError);
        aplicarConError.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await aplicarConError.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(aplicarConError)).Should().Be("Import.Invalid");
        (await PuestaEnMarchaE2E.CargadoAsync(http, admin, "receipts", corte, "Boveda")).Should().Be(0m, "con un error no se guarda nada");

        var corregida = PuestaEnMarchaE2E.LibroDeBovedas(corte, (50_000m, 100), (20_000m, 50), saldo: 6_000_000m);
        var aplicada = await InventarioE2E.RevisarYAplicarAsync(http, admin, PuestaEnMarchaE2E.Recaudo(PuestaEnMarchaE2E.Bovedas), corregida);
        aplicada.GetProperty("applied").GetBoolean().Should().BeTrue(aplicada.ToString());
        (await PuestaEnMarchaE2E.CargadoAsync(http, admin, "receipts", corte, "Boveda")).Should().Be(6_000_000m);

        // 2. Cuentas bancarias con saldo al corte, la chequera, los cheques girados en tránsito y las partidas por identificar al corte.
        var beneficiario = await PuestaEnMarchaE2E.PersonaAsync(http, admin, "Beneficiario", "Transito");
        await PuestaEnMarchaE2E.CuentasYPendientesAsync(http, coop, corte, saldo: 1_500_000m, beneficiario.Documento, chequeEnTransito: 300_000m, partida: 250_000m);

        var hechos = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, async db => new
        {
            Mensajes = await db.IntegrationMessages.AsNoTracking().CountAsync(m => m.OriginModule == "REC" || m.OriginModule == "TES"),
            Comprobantes = await db.AccountingDocuments.AsNoTracking().CountAsync(d => d.OriginModule == "REC" || d.OriginModule == "TES"),
        });
        hechos.Mensajes.Should().Be(0, "la puesta en marcha no emite hechos: los saldos ya están en la contabilidad (o los trae su apertura)");
        hechos.Comprobantes.Should().Be(0);

        // 3. El cuadre contra Contabilidad: lo cargado contra un libro en cero, por concepto.
        var recaudo = await CarteraE2E.GetAsync(http, admin, $"/api/receipts/opening-squares/current?cutoffDate={corte:yyyy-MM-dd}");
        recaudo.GetProperty("squares").GetBoolean().Should().BeFalse(recaudo.ToString());
        var lineasDeRecaudo = recaudo.GetProperty("proposal").EnumerateArray().ToList();
        lineasDeRecaudo.Should().Contain(l => l.GetProperty("concept").GetString() == "Boveda" && l.GetProperty("loaded").GetDecimal() == 6_000_000m
            && !l.GetProperty("squares").GetBoolean());
        lineasDeRecaudo.Should().Contain(l => l.GetProperty("concept").GetString() == "PartidasPorIdentificar" && l.GetProperty("loaded").GetDecimal() == 250_000m);
        recaudo.GetProperty("totalDifference").GetDecimal().Should().NotBe(0m);

        var tesoreria = await CarteraE2E.GetAsync(http, admin, $"/api/treasury/opening-squares/current?cutoffDate={corte:yyyy-MM-dd}");
        tesoreria.GetProperty("squares").GetBoolean().Should().BeFalse(tesoreria.ToString());
        var lineasDeTesoreria = tesoreria.GetProperty("proposal").EnumerateArray().ToList();
        var banco = lineasDeTesoreria.Single(l => l.GetProperty("concept").GetString() == "Banco" && l.GetProperty("dimension").GetString() == PuestaEnMarchaE2E.CuentaPropia);
        banco.GetProperty("loaded").GetDecimal().Should().Be(1_500_000m);
        (banco.GetProperty("ledger").ValueKind == JsonValueKind.Null ? 0m : banco.GetProperty("ledger").GetDecimal()).Should().Be(0m, "el libro no tiene ese saldo");
        banco.GetProperty("difference").GetDecimal().Should().NotBe(0m);
        lineasDeTesoreria.Should().Contain(l => l.GetProperty("concept").GetString() == "ChequesGiradosEnTransito" && l.GetProperty("loaded").GetDecimal() == 300_000m
            && !l.GetProperty("isComparable").GetBoolean(), "los cheques en tránsito se informan: el saldo en libros ya los descuenta");

        // Con diferencia y sin acta, el definitivo no se registra: se corrige, se adjunta el acta o se activa con un provisional.
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var definitivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/{modulo}/opening-squares/definitive", new { cutoffDate = corte });
            definitivo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await definitivo.Content.ReadAsStringAsync());
            (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(definitivo)).Should().EndWith(".Activation.SquareHasDifference");
            (await RecaudoYTesoreriaE2E.LeerAsync(definitivo)).GetProperty("data").GetProperty("lines").GetArrayLength().Should().BeGreaterThan(0, "dice qué no cuadra");
        }
    }
}

/// <summary>
/// Los ayudantes de la puesta en marcha de Recaudo y Tesorería por plantillas (feature 015, T0285, T0319): las rutas, los libros con los encabezados de la fila 1
/// en la hoja «Datos», la bóveda de la oficina principal, la cuenta bancaria del plan y la carga de las cuentas propias con sus pendientes al corte. (nuevo)
/// </summary>
public static class PuestaEnMarchaE2E
{
    public const string Bovedas = "recaudo.bovedas-al-corte";
    public const string CuentaPropia = "RECPM";
    public const string CuentaContable = "11100597";
    public const string Oficina = "Principal";

    public static string Recaudo(string plantilla) => $"/api/receipts/imports/{plantilla}";

    public static string Tesoreria(string plantilla) => $"/api/treasury/imports/{plantilla}";

    private static string Fecha(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Monto(decimal m) => m.ToString(CultureInfo.InvariantCulture);

    /// <summary>La bóveda de la oficina principal y los billetes de 50.000 y 20.000 vigentes. Idempotente.</summary>
    public static async Task BovedaAsync(HttpClient http, string admin, CarteraE2E.CooperativaAislada coop)
    {
        await CajaE2E.BilleteAsync(http, admin, 50_000m);
        await CajaE2E.BilleteAsync(http, admin, 20_000m);
        var bovedas = await InventarioE2E.GetAsync(http, admin, "/api/receipts/vaults");
        if (bovedas.ValueKind == JsonValueKind.Array && bovedas.GetArrayLength() > 0) return;
        var alta = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, "/api/receipts/vaults", new { branchPublicId = coop.SucursalPrincipal, mode = "Vault" });
        ((int)alta.StatusCode).Should().BeInRange(200, 299, await alta.Content.ReadAsStringAsync());
    }

    /// <summary>El libro de <c>recaudo.bovedas-al-corte</c> de la oficina principal: una fila por denominación, el mismo corte y el mismo saldo declarado.</summary>
    public static byte[] LibroDeBovedas(DateOnly corte, (decimal Denominacion, int Cantidad) primera, (decimal Denominacion, int Cantidad) segunda, decimal saldo) =>
        InventarioE2E.Libro(("Datos", ["oficina", "fechaDeCorte", "denominacion", "cantidad", "saldoDeclarado"],
        [
            [Oficina, Fecha(corte), Monto(primera.Denominacion), primera.Cantidad.ToString(CultureInfo.InvariantCulture), Monto(saldo)],
            [Oficina, Fecha(corte), Monto(segunda.Denominacion), segunda.Cantidad.ToString(CultureInfo.InvariantCulture), Monto(saldo)],
        ]));

    /// <summary>Lo cargado de un concepto en la propuesta del cuadre de <paramref name="modulo"/> (<c>receipts</c> o <c>treasury</c>) al corte (cero si no hay línea).</summary>
    public static async Task<decimal> CargadoAsync(HttpClient http, string admin, string modulo, DateOnly corte, string concepto)
    {
        var actual = await CarteraE2E.GetAsync(http, admin, $"/api/{modulo}/opening-squares/current?cutoffDate={corte:yyyy-MM-dd}");
        return actual.GetProperty("proposal").EnumerateArray().Where(l => l.GetProperty("concept").GetString() == concepto).Sum(l => l.GetProperty("loaded").GetDecimal());
    }

    private static int _documento;

    /// <summary>Una persona con un documento conocido (las plantillas la citan por documento).</summary>
    public static async Task<(Guid Persona, string Documento)> PersonaAsync(HttpClient http, string admin, string nombre, string apellido)
    {
        var documento = (7_700_000_000L + Interlocked.Increment(ref _documento) * 10L + (Environment.ProcessId % 7)).ToString(CultureInfo.InvariantCulture);
        var resp = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = apellido, email = $"{nombre.ToLowerInvariant()}.{documento}@persona.recaudo.test",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return ((await RecaudoYTesoreriaE2E.LeerAsync(resp)).GetGuid(), documento);
    }

    /// <summary>La auxiliar bancaria del plan (<see cref="CuentaContable"/>) del banco de los pagos, habilitada para Tesorería y Recaudo. Idempotente.</summary>
    public static async Task CuentaBancariaDelPlanAsync(HttpClient http, string admin)
    {
        var banco = await RecaudoYTesoreriaE2E.BancoAsync(http, admin);
        var padre = await Accounting.ContabilidadE2E.CuentaAsync(http, admin, "111005");
        var alta = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = CuentaContable, name = "Banco de recaudo de la puesta en marcha (e2e)", parentPublicId = padre, enabledModules = new[] { "CNT", "TES", "REC", "CAR" },
            requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
            bank = new { bankPublicId = banco, accountNumber = "0099001122" },
        });
        if (alta.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return;
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// La cuenta propia <see cref="CuentaPropia"/> (recaudo y pagos, vinculada a <see cref="CuentaContable"/>) con su saldo al corte, una chequera, un cheque girado en
    /// tránsito a <paramref name="documentoDelBeneficiario"/> y una partida por identificar al corte, por las plantillas de Tesorería y de Recaudo. Sin cheque ni partida
    /// cuando su valor es cero.
    /// </summary>
    public static async Task CuentasYPendientesAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, DateOnly corte, decimal saldo, string documentoDelBeneficiario,
        decimal chequeEnTransito, decimal partida)
    {
        var admin = coop.TokenAdmin;
        await CuentaBancariaDelPlanAsync(http, admin);
        await InventarioE2E.RevisarYAplicarAsync(http, admin, Tesoreria("tesoreria.cuentas-bancarias"), InventarioE2E.Libro(("Datos",
            ["codigo", "nombre", "banco", "numero", "tipo", "oficina", "propositos", "cuentaContable", "exentaGmf", "saldoAlCorte", "fechaDeCorte"],
            [[CuentaPropia, "Recaudo y pagos (puesta en marcha e2e)", RecaudoYTesoreriaE2E.CodigoDelBanco, "0099001122", "Savings", Oficina, "Recaudo,Pagos", CuentaContable,
                null, Monto(saldo), Fecha(corte)]])));
        if (chequeEnTransito > 0m)
        {
            await InventarioE2E.RevisarYAplicarAsync(http, admin, Tesoreria("tesoreria.chequeras"),
                InventarioE2E.Libro(("Datos", ["cuenta", "desde", "hasta", "siguiente"], [[CuentaPropia, "1", "50", "10"]])));
            await InventarioE2E.RevisarYAplicarAsync(http, admin, Tesoreria("tesoreria.cheques-en-transito"),
                InventarioE2E.Libro(("Datos", ["cuenta", "numero", "beneficiario", "valor", "fecha", "entregado"],
                    [[CuentaPropia, "5", documentoDelBeneficiario, Monto(chequeEnTransito), Fecha(corte.AddDays(-3)), null]])));
        }
        if (partida > 0m)
            await InventarioE2E.RevisarYAplicarAsync(http, admin, Recaudo("recaudo.partidas-al-corte"),
                InventarioE2E.Libro(("Datos", ["cuentaPropia", "oficina", "fecha", "valor", "referencia", "consignante", "documentoConsignante"],
                    [[CuentaPropia, Oficina, Fecha(corte.AddDays(-2)), Monto(partida), "CONSIG-PM-1", "Consignante sin identificar", null]])));
    }
}
