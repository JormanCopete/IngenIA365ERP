using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Colección «Recaudo y Tesorería e2e» (feature 015, T0009; quickstart.md): un host propio, con sus contenedores, para las e2e de los dos
/// módulos, en PostgreSQL y SQL Server (<c>DB_PROVIDER</c>). Lo que activa Recaudo o Tesorería corre en una cooperativa aislada por prueba
/// (<see cref="RecaudoYTesoreriaE2E.CooperativaAisladaAsync"/>): la activación cambia lo que ven Cartera y Captaciones. Se escriben durante
/// cada fase y se corren una sola vez al final de R0/T0 (fase 22), salvo las dos excepciones declaradas en tasks.md (T0183c y T0301).
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class RecaudoYTesoreriaCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Recaudo y Tesorería e2e";
}

/// <summary>
/// Ayudantes de las e2e de Recaudo y Tesorería (feature 015, T0009; molde <see cref="CarteraE2E"/>): la clave de idempotencia, la cooperativa
/// aislada, el token del maestro y el envío y la lectura de peticiones, delegados en los de Cartera para que las dos colecciones hablen igual
/// con la API. Desde la fase 19 (T0298) <see cref="RegistrarYActivarAsync"/> deja a Recaudo y a Tesorería <b>reales</b> registrados y activados —con la
/// activación completa: configuración mínima, cuadre provisional y activación por sus rutas—, y los ayudantes <see cref="PagarEnCajaAsync"/>,
/// <see cref="IdentificarAbonoAsync"/>, <see cref="GirarOrdenAsync"/> y <see cref="RegistrarResultadoAsync"/> recorren las rutas reales: con ellos las e2e de
/// Cartera y Captaciones dejan de necesitar los dobles de prueba (FR-224). Escritos, sin correr (la puerta T0301 los corre).
/// </summary>
public static class RecaudoYTesoreriaE2E
{
    public const string CabeceraDeClave = CarteraE2E.CabeceraDeClave;

    /// <summary>Los códigos de lo que la configuración mínima crea en una cooperativa (no chocan con los de otras e2e).</summary>
    public const string CodigoDelPunto = "RYT";
    public const string CodigoDelEfectivo = "EFRYT";
    public const string CodigoDeLaTransferencia = "TRRYT";
    public const string CodigoDelBanco = "BCORYT";
    public const string CuentaContableDelBanco = "11100596";
    public const string CodigoDeLaCuentaDePagos = "PAGRYT";

    /// <summary>Pone una <c>Idempotency-Key</c> nueva; la prueba de repetición pone la misma clave a mano.</summary>
    public static HttpRequestMessage ConClave(HttpRequestMessage peticion) => CarteraE2E.ConClave(peticion);

    /// <summary>
    /// Una cooperativa aislada por nombre y fixture (la segunda llamada con el mismo nombre devuelve la misma). Con
    /// <paramref name="contabilidadIniciada"/> la cooperativa nace con su primer ejercicio contable en el año de hoy (los hechos pasan a
    /// Contabilidad); sin ella, el modo de paso por defecto es «no pasa».
    /// </summary>
    public static Task<CarteraE2E.CooperativaAislada> CooperativaAisladaAsync(CentralIdentityApiFixture fx, string nombre, bool contabilidadIniciada = false) =>
        CarteraE2E.CooperativaAisladaAsync(fx, $"ryt-{nombre}", contabilidadIniciada ? CarteraE2E.Hoy(fx).Year : null);

    /// <summary>El token del maestro, compartido con Inventario y Cartera: el limitador de login admite 10 por minuto por IP.</summary>
    public static Task<string> TokenMaestroAsync(CentralIdentityApiFixture fx, HttpClient http) => CarteraE2E.TokenMaestroAsync(fx, http);

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo) =>
        CarteraE2E.EnviarAsync(http, token, metodo, url, cuerpo);

    public static Task<JsonElement> LeerAsync(HttpResponseMessage resp) => CarteraE2E.LeerAsync(resp);

    public static Task<string> CodigoDeErrorAsync(HttpResponseMessage resp) => CarteraE2E.CodigoDeErrorAsync(resp);

    /// <summary>
    /// Registra a Recaudo y Tesorería <b>reales</b> en el registro de participantes por su propia activación (T0298; FR-147, FR-150, FR-159): por cada módulo que
    /// no esté activo, la configuración mínima que le falte —Recaudo: los medios efectivo y transferencia, un punto con su caja ofrecida y el administrador como
    /// cajero (y supervisor: tiene todos los permisos); Tesorería: un banco, su auxiliar bancario en el plan y una cuenta propia de pagos vinculada—, el cuadre
    /// provisional de la puesta en marcha al corte (con su motivo y fecha límite), y la activación por <c>POST /api/{receipts,treasury}/activation</c>. Con
    /// Contabilidad iniciada y la matriz del módulo incompleta, deja el modo de paso del módulo en «no pasa» desde el día de la activación (las e2e de Cartera y
    /// Captaciones no cargan las matrices de Recaudo y Tesorería; las de contabilización sí, antes de activar). El corte es <paramref name="corte"/> o ayer: el
    /// registro queda vigente desde el día siguiente, como lo dejaban los dobles. Con <paramref name="minima"/>, la activación mínima del paralelo (sin cuadre;
    /// fuera de producción). Con <paramref name="iniciarContabilidad"/>, si Tesorería no puede vincular su cuenta porque la contabilidad no está iniciada, la
    /// inicia (la usan Cartera y Captaciones, cuya activación la exige de todos modos). Un módulo activo no se vuelve a activar. Idempotente.
    /// </summary>
    public static async Task RegistrarYActivarAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, CentralIdentityApiFixture? fx = null, DateOnly? corte = null,
        bool minima = false, bool iniciarContabilidad = false)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(coop);
        var hoy = fx is null ? DateOnly.FromDateTime(DateTime.Today) : CarteraE2E.Hoy(fx);
        var alCorte = corte ?? hoy.AddDays(-1);
        await ActivarRecaudoAsync(http, coop, fx, alCorte, minima);
        await ActivarTesoreriaAsync(http, coop, fx, alCorte, minima, iniciarContabilidad);
    }

    private static async Task<JsonElement> EstadoAsync(HttpClient http, string token, string modulo, DateOnly corte, bool minima) =>
        await LeerAsync(await EnviarAsync(http, token, HttpMethod.Get,
            $"/api/{modulo}/activation/requirements?cutoffDate={corte:yyyy-MM-dd}{(minima ? "&minimal=true" : string.Empty)}", null));

    private static bool Cumple(JsonElement estado, string codigo) =>
        estado.GetProperty("requirements").EnumerateArray().Where(r => r.GetProperty("code").GetString() == codigo).All(r => r.GetProperty("met").GetBoolean());

    private static async Task ActivarRecaudoAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, CentralIdentityApiFixture? fx, DateOnly corte, bool minima)
    {
        var admin = coop.TokenAdmin;
        var estado = await EstadoAsync(http, admin, "receipts", corte, minima);
        if (estado.GetProperty("status").GetString() == "Active") return;

        if (!Cumple(estado, "CollectionPoint") || !Cumple(estado, "CashMeans") || !Cumple(estado, "TransferMeans") || !Cumple(estado, "CashBoxWithCashier"))
        {
            var efectivo = await ConfiguracionDeRecaudoE2E.MedioAsync(http, admin, CodigoDelEfectivo, "Cash", corte.AddDays(-1));
            var transferencia = await ConfiguracionDeRecaudoE2E.MedioAsync(http, admin, CodigoDeLaTransferencia, "Transfer", corte.AddDays(-1));
            var (_, caja) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, admin, coop.SucursalPrincipal, CodigoDelPunto, efectivo, transferencia);
            if (coop.CorreoAdmin is { } correo)
            {
                var cajera = await CarteraE2E.UsuarioPorCorreoAsync(http, admin, correo);
                (await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja}/cashiers", new { userPublicId = cajera }))
                    .StatusCode.Should().Be(HttpStatusCode.OK, "el administrador es el cajero de la configuración mínima");
            }
        }

        if (!minima)
        {
            await CuadreProvisionalAsync(http, admin, "receipts", corte);
            estado = await EstadoAsync(http, admin, "receipts", corte, minima);
            if (!Cumple(estado, "AccountingMatrixComplete")) await NoPasaAsync(fx, coop, "RCD", corte.AddDays(1));
        }
        var activado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/receipts/activation", new { cutoffDate = corte, minimal = minima });
        ((int)activado.StatusCode).Should().BeInRange(200, 299, $"activar Recaudo: «{await activado.Content.ReadAsStringAsync()}»");
    }

    private static async Task ActivarTesoreriaAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, CentralIdentityApiFixture? fx, DateOnly corte, bool minima,
        bool iniciarContabilidad)
    {
        var admin = coop.TokenAdmin;
        var estado = await EstadoAsync(http, admin, "treasury", corte, minima);
        if (estado.GetProperty("status").GetString() == "Active") return;

        if (!Cumple(estado, "LinkedPaymentsAccount"))
        {
            var contable = await CuentaContableDelBancoAsync(http, coop, corte, iniciarContabilidad);
            if (contable is null) return; // sin contabilidad no hay cuenta bancaria del plan que vincular: Tesorería queda sin activar.
            await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/treasury/bank-accounts", new
            {
                code = CodigoDeLaCuentaDePagos, name = "Cuenta de pagos (e2e)", bankPublicId = contable.Value.Banco, accountNumber = "0011223344", accountType = "Savings",
                branchPublicId = coop.SucursalPrincipal, purposes = 2, isGmfExempt = false, chartAccountPublicId = contable.Value.Cuenta,
            });
        }

        if (!minima)
        {
            await CuadreProvisionalAsync(http, admin, "treasury", corte);
            estado = await EstadoAsync(http, admin, "treasury", corte, minima);
            if (!Cumple(estado, "AccountingMatrixComplete")) await NoPasaAsync(fx, coop, "TES", corte.AddDays(1));
        }
        var activado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/treasury/activation", new { cutoffDate = corte, minimal = minima });
        ((int)activado.StatusCode).Should().BeInRange(200, 299, $"activar Tesorería: «{await activado.Content.ReadAsStringAsync()}»");
    }

    /// <summary>El cuadre provisional al corte, con su motivo y fecha límite (decisión del dueño); si ya hay uno aprobado de ese corte, no hace nada.</summary>
    private static async Task CuadreProvisionalAsync(HttpClient http, string admin, string modulo, DateOnly corte)
    {
        var actual = await LeerAsync(await EnviarAsync(http, admin, HttpMethod.Get, $"/api/{modulo}/opening-squares/current?cutoffDate={corte:yyyy-MM-dd}", null));
        if (actual.TryGetProperty("current", out var vigente) && vigente.ValueKind == JsonValueKind.Object) return;
        if (actual.TryGetProperty("pendingProvisional", out var pendiente) && pendiente.ValueKind == JsonValueKind.Object) return;
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/{modulo}/opening-squares/provisional", new
        {
            reason = "Cuadre provisional de las e2e: el definitivo lo registra la contadora", deadlineOn = corte.AddDays(4), cutoffDate = corte,
        });
        ((int)resp.StatusCode).Should().BeInRange(200, 299, $"el cuadre provisional de {modulo}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary><c>Contabilidad.ModoDePaso = NoPasa</c> del módulo desde <paramref name="desde"/>, por el único escritor de parámetros (las claves RCD/TES no tienen ruta).</summary>
    private static async Task NoPasaAsync(CentralIdentityApiFixture? fx, CarteraE2E.CooperativaAislada coop, string modulo, DateOnly desde)
    {
        fx.Should().NotBeNull($"la matriz contable de {modulo} está incompleta: pase la fixture para dejar el módulo en «no pasa»");
        var r = await fx!.Despachador.EnviarComoProcesoAsync(coop.TenantPublicId, "e2e:recaudo-y-tesoreria",
            new AddParameterVersionCommand(modulo, "Contabilidad.ModoDePaso", ParameterScopeKind.None, null, null, "NoPasa", desde,
                "Las e2e de Cartera y Captaciones no cargan la matriz de Recaudo y Tesorería", null) { OperationKey = Guid.NewGuid() });
        if (r.IsSuccess) return;
        r.Error.Code.Should().Contain("Overlap", $"«no pasa» en {modulo}: {r.Error.Code} {r.Error.Message}");
    }

    /// <summary>El banco y su auxiliar bancario en el plan (<see cref="CuentaContableDelBanco"/>); nulo si la contabilidad no está iniciada y no se pide iniciarla.</summary>
    private static async Task<(Guid Banco, Guid Cuenta)?> CuentaContableDelBancoAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, DateOnly corte,
        bool iniciarContabilidad)
    {
        var admin = coop.TokenAdmin;
        var cuentas = await EnviarAsync(http, admin, HttpMethod.Get, "/api/accounting/accounts/bank-accounts", null);
        if (!cuentas.IsSuccessStatusCode && !iniciarContabilidad) return null;
        var lista = cuentas.IsSuccessStatusCode ? await LeerAsync(cuentas) : default;
        if (lista.ValueKind == JsonValueKind.Array
            && lista.EnumerateArray().FirstOrDefault(c => c.GetProperty("code").GetString() == CuentaContableDelBanco) is { ValueKind: JsonValueKind.Object } ya)
            return (await BancoAsync(http, admin), ya.GetProperty("accountPublicId").GetGuid());

        var busqueda = await EnviarAsync(http, admin, HttpMethod.Get, "/api/accounting/accounts/search?q=111005&onlyMovement=false", null);
        if (!busqueda.IsSuccessStatusCode || (await LeerAsync(busqueda)).GetArrayLength() == 0)
        {
            if (!iniciarContabilidad) return null;
            var inicio = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/accounting/setup/initialize", new
            {
                catalogCode = "PUC-SOLIDARIO", movementLevel = 5, niifGroup = 2, firstFiscalYear = corte.Year, mainBranchPublicId = coop.SucursalPrincipal, fourEyes = false,
            });
            if (!inicio.IsSuccessStatusCode)
                (await CodigoDeErrorAsync(inicio)).Should().Be("Accounting.Setup.AlreadyInitialized", "Tesorería necesita la cuenta bancaria del plan");
            busqueda = await EnviarAsync(http, admin, HttpMethod.Get, "/api/accounting/accounts/search?q=111005&onlyMovement=false", null);
        }
        var padre = (await LeerAsync(busqueda)).EnumerateArray().First(c => c.GetProperty("code").GetString() == "111005");
        var banco = await BancoAsync(http, admin);
        var alta = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = CuentaContableDelBanco, name = "Banco de los pagos (e2e)", parentPublicId = padre.GetProperty("publicId").GetGuid(),
            enabledModules = new[] { "CNT", "CAR", "TES", "REC" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false,
            requiresBranch = false, bank = new { bankPublicId = banco, accountNumber = "0011223344" },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"la cuenta bancaria del plan: «{await alta.Content.ReadAsStringAsync()}»");
        var vinculables = await CarteraE2E.GetAsync(http, admin, "/api/accounting/accounts/bank-accounts");
        return (banco, vinculables.EnumerateArray().Single(c => c.GetProperty("code").GetString() == CuentaContableDelBanco).GetProperty("accountPublicId").GetGuid());
    }

    private static async Task<Guid> BancoAsync(HttpClient http, string admin)
    {
        var bancos = await EnviarAsync(http, admin, HttpMethod.Get, "/api/core/banks", null);
        if (bancos.IsSuccessStatusCode)
        {
            var lista = await LeerAsync(bancos);
            var items = lista.ValueKind == JsonValueKind.Array ? lista : lista.TryGetProperty("items", out var i) ? i : default;
            if (items.ValueKind == JsonValueKind.Array
                && items.EnumerateArray().FirstOrDefault(b => b.TryGetProperty("code", out var c) && c.GetString() == CodigoDelBanco) is { ValueKind: JsonValueKind.Object } ya)
                return ya.GetProperty("publicId").GetGuid();
        }
        var resp = await EnviarAsync(http, admin, HttpMethod.Post, "/api/core/banks", new
        {
            code = CodigoDelBanco, name = "Banco de los pagos (e2e)", shortName = CodigoDelBanco, transferCode = "0059",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"el banco de los pagos: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetGuid();
    }

    // ------------------------------------------------------------------------------------------------ las rutas reales --

    /// <summary>
    /// Un recibo de caja real (T0298): la operación de <paramref name="pagador"/> en la sesión abierta, sus ítems (<c>CajaE2E.Cuota</c>, <c>Abono</c> o un ítem de
    /// cuenta) y sus medios, y la confirmación con su clave. Devuelve el recibo.
    /// </summary>
    public static async Task<JsonElement> PagarEnCajaAsync(HttpClient http, string token, Guid sesion, Guid pagador, object[] items, object[] medios, Guid? clave = null)
    {
        var op = await LeerAsync(await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = sesion, payerPersonPublicId = pagador }));
        var id = op.GetProperty("publicId").GetGuid();
        (await InventarioE2E.MandarAsync(http, token, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/items", new { items }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "los ítems de la operación");
        (await InventarioE2E.MandarAsync(http, token, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/means", new { means = medios }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "los medios de la operación");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/cash-operations/{id}/confirm") { Headers = { Authorization = new("Bearer", token) } };
        req.Headers.Add(CabeceraDeClave, (clave ?? Guid.NewGuid()).ToString());
        using var resp = await http.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"confirmar el recibo: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    /// <summary>Identifica un abono del extracto real con sus partes (T0298; <c>POST /api/receipts/bank-items/{línea}/identify</c>). Devuelve lo identificado.</summary>
    public static Task<JsonElement> IdentificarAbonoAsync(HttpClient http, string token, Guid lineaDelExtracto, object[] partes, Guid? corrida = null) =>
        InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/receipts/bank-items/{lineaDelExtracto}/identify", new { parts = partes, runPublicId = corrida });

    /// <summary>La orden de pago de Tesorería que nació de la orden de giro del origen (Cartera o Captaciones), después de una pasada del despachador.</summary>
    public static async Task<JsonElement> OrdenDeTesoreriaAsync(CentralIdentityApiFixture fx, HttpClient http, CarteraE2E.CooperativaAislada coop, Guid ordenDelOrigen)
    {
        await CarteraE2E.DespacharAsync(fx, coop.TenantPublicId, 3);
        var ordenes = await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/treasury/payment-orders");
        return ordenes.EnumerateArray().First(o =>
            (o.TryGetProperty("originOrderPublicId", out var origen) && origen.ValueKind == JsonValueKind.String && origen.GetGuid() == ordenDelOrigen)
            || o.GetProperty("publicId").GetGuid() == ordenDelOrigen);
    }

    /// <summary>
    /// Gira una orden de Tesorería real (T0298; recorrido G de quickstart.md): un lote de la cuenta de pagos con esa orden por la forma «portal del banco», a
    /// aprobación, aprobado por <paramref name="aprobador"/> (otra persona: segregación), su archivo y marcado enviado. Devuelve el lote. La cuenta es la de
    /// <see cref="RegistrarYActivarAsync"/> si no se indica.
    /// </summary>
    public static async Task<JsonElement> GirarOrdenAsync(HttpClient http, string token, string aprobador, Guid ordenDeTesoreria, Guid? cuentaDePagos = null)
    {
        var cuenta = cuentaDePagos ?? await CuentaDePagosAsync(http, token);
        var lote = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = cuenta, form = 1, payDate = DateOnly.FromDateTime(DateTime.Today), orderPublicIds = new[] { ordenDeTesoreria },
            liquidityFundUsePublicId = (Guid?)null,
        });
        var id = lote.GetProperty("publicId").GetGuid();
        ((int)(await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit", null)).StatusCode)
            .Should().BeInRange(200, 299, "enviar el lote a aprobación");
        ((int)(await InventarioE2E.MandarAsync(http, aprobador, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { })).StatusCode)
            .Should().BeInRange(200, 299, "aprobar el lote");
        var archivo = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/file");
        ((int)(await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"/api/treasury/payment-files/{archivo.GetProperty("publicId").GetGuid()}/sent",
            new { uploadedAtBankBy = "Carga (e2e)", authorizedAtBankBy = "Autoriza (e2e)" })).StatusCode).Should().BeInRange(200, 299, "marcar enviado el archivo");
        return lote;
    }

    /// <summary>
    /// Registra el resultado manual de una orden enviada (T0298; <c>POST /api/treasury/payment-orders/{id}/results</c>, con su soporte): <c>Disbursed</c>,
    /// <c>Rejected</c> (con causal) o <c>CheckNotCashed</c>. Tesorería informa el <c>ResultadoDelGiro</c> a su origen a nombre de quien registra.
    /// </summary>
    public static async Task<JsonElement> RegistrarResultadoAsync(HttpClient http, string token, Guid ordenDeTesoreria, string resultado, DateOnly fecha,
        string? causal = null)
    {
        var soporte = await AdjuntosE2E.SubirAsync(http, token, ordenDeTesoreria, Encoding.UTF8.GetBytes("%PDF-1.4 soporte del resultado del giro (e2e)"),
            "soporte-del-giro.pdf", "application/pdf", "TreasuryResultSupport");
        return await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/treasury/payment-orders/{ordenDeTesoreria}/results", new
        {
            outcome = resultado, occurredOn = fecha, treasuryReference = $"TES-{Guid.NewGuid():N}"[..20],
            reason = causal ?? (resultado == "Disbursed" ? null : "Cuenta inexistente en el banco destino (e2e)"), checkNumber = (string?)null,
            supportAttachmentPublicId = soporte.Adjunto,
        });
    }

    /// <summary>La cuenta de pagos de la configuración mínima (<see cref="CodigoDeLaCuentaDePagos"/>), o la primera de pagos activa.</summary>
    public static async Task<Guid> CuentaDePagosAsync(HttpClient http, string token)
    {
        var cuentas = await CarteraE2E.GetAsync(http, token, "/api/treasury/bank-accounts");
        var lista = cuentas.ValueKind == JsonValueKind.Array ? cuentas : cuentas.GetProperty("items");
        var propia = lista.EnumerateArray().FirstOrDefault(c => c.GetProperty("code").GetString() == CodigoDeLaCuentaDePagos);
        return (propia.ValueKind == JsonValueKind.Object ? propia : lista.EnumerateArray().First()).GetProperty("publicId").GetGuid();
    }
}

/// <summary>La prueba humo de la colección (T0009): crea una cooperativa aislada y su administrador entra. Se escribe ahora y corre en la fase 22.</summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class RecaudoYTesoreriaHumoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_coleccion_crea_su_cooperativa_aislada_y_su_administrador_entra()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "humo");
        coop.TenantPublicId.Should().NotBe(Guid.Empty);

        using var http = fx.CreateClient();
        var resp = await RecaudoYTesoreriaE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/admin/permissions/mine", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
