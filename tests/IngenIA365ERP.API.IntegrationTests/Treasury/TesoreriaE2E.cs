using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Los ayudantes de las e2e de Tesorería (feature 015, fases 7 a 9, T0133, T0155, T0165): una cooperativa con Cartera real (el escenario del desembolso), la cuenta de
/// pagos vinculada a la cuenta contable bancaria del escenario, la cuenta destino del beneficiario registrada con doble control y certificación, Tesorería activada
/// (fase 19) y las órdenes que llegan por la bandeja cuando Cartera desembolsa por transferencia. Se escriben ahora y se corren en la fase 22. (nuevo)
/// </summary>
public static class TesoreriaE2E
{
    public sealed record Tesoreria(DesembolsoE2E.Escenario Escenario, string Admin, Guid CuentaDePagos, Guid Beneficiario);

    /// <summary>La cooperativa del recorrido G de quickstart.md: cuenta de pagos con su vínculo y la cuenta destino del beneficiario aprobada.</summary>
    public static async Task<Tesoreria> PreparadaAsync(CentralIdentityApiFixture fx, HttpClient http, string nombre)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, $"tesoreria_{nombre}");
        var admin = esc.Coop.TokenAdmin;
        var cuenta = await CuentaPropiaAsync(http, admin, esc, $"PAG{nombre.ToUpperInvariant()[..Math.Min(5, nombre.Length)]}", 2, esc.CuentaDeOrigen);
        var (beneficiario, _) = await DesembolsoE2E.DeudorAsync(http, admin, $"Giro{nombre}");
        await CuentaDestinoAprobadaAsync(http, esc, beneficiario, "0123456789");
        await TesoreroAsync(http, esc);
        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, esc.Coop);
        return new Tesoreria(esc, admin, cuenta, beneficiario);
    }

    /// <summary>Da de alta una cuenta propia con sus propósitos (flags) y, si se indica, su vínculo contable.</summary>
    public static async Task<Guid> CuentaPropiaAsync(HttpClient http, string token, DesembolsoE2E.Escenario esc, string codigo, int propositos, Guid? contable,
        string? numero = null)
    {
        var r = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, "/api/treasury/bank-accounts", new
        {
            code = codigo, name = $"Cuenta {codigo}", bankPublicId = esc.Banco, accountNumber = numero ?? $"77{Random.Shared.Next(10_000_000, 99_999_999)}",
            accountType = "Savings", branchPublicId = esc.Coop.SucursalPrincipal, purposes = propositos, isGmfExempt = false, chartAccountPublicId = contable,
        });
        return r.GetProperty("publicId").GetGuid();
    }

    private static readonly Dictionary<Guid, Task> Aprobadores = [];
    private static readonly Dictionary<Guid, Task> Tesoreros = [];

    /// <summary>
    /// El usuario «pruebas» del escenario de Cartera con el rol de la plantilla <c>tesoreria.tesorero</c> y alcance total (una vez por cooperativa): es la tercera
    /// persona que descarga el archivo de pagos y carga la respuesta del banco (segregación de quien armó y de quien aprobó). Hasta la corrida del 2026-10-26 sólo
    /// tenía permisos de Recaudo y esas rutas le respondían 404.
    /// </summary>
    public static Task TesoreroAsync(HttpClient http, DesembolsoE2E.Escenario esc)
    {
        lock (Tesoreros)
        {
            if (!Tesoreros.TryGetValue(esc.Coop.TenantPublicId, out var tarea))
            {
                tarea = RolConAlcanceTotalAsync(http, esc, esc.Usuarios.Pruebas.PublicId, "tesoreria.tesorero", "TTES", "Tesorero (e2e)");
                Tesoreros[esc.Coop.TenantPublicId] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>Un rol de la plantilla <paramref name="plantilla"/> más <c>Treasury.Scope.All</c>, asignado a <paramref name="usuario"/>.</summary>
    private static async Task RolConAlcanceTotalAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid usuario, string plantilla, string codigo, string nombre)
    {
        var admin = esc.Coop.TokenAdmin;
        var creado = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template", new { templateKey = plantilla, code = codigo, name = nombre });
        var rol = creado.GetProperty("rolePublicId").GetGuid();
        var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
        var permisos = (await CarteraE2E.GetAsync(http, admin, $"/api/admin/roles/{rol}")).GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetProperty("code").GetString()!).Append("Treasury.Scope.All").Distinct(StringComparer.OrdinalIgnoreCase).Select(c => catalogo[c]).ToList();
        var put = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/admin/roles/{rol}",
            new { name = nombre, description = (string?)null, isAssignable = true, permissionPublicIds = permisos });
        put.IsSuccessStatusCode.Should().BeTrue($"el alcance total de {codigo}: {await put.Content.ReadAsStringAsync()}");
        await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
    }

    /// <summary>
    /// El jefe del escenario de Cartera con el rol de la plantilla <c>tesoreria.aprobador</c> (una vez por cooperativa): aprueba las cuentas destino que pide el
    /// administrador (<c>Core.PersonBankAccounts.Approve</c>) y los lotes que arma otro. Hasta la corrida del 2026-10-26 el jefe no tenía el permiso y la
    /// aprobación respondía 404.
    /// </summary>
    public static Task AprobadorDeTesoreriaAsync(HttpClient http, DesembolsoE2E.Escenario esc)
    {
        lock (Aprobadores)
        {
            if (!Aprobadores.TryGetValue(esc.Coop.TenantPublicId, out var tarea))
            {
                tarea = DarRolAsync(http, esc);
                Aprobadores[esc.Coop.TenantPublicId] = tarea;
            }
            return tarea;
        }

        // La plantilla más Treasury.Scope.All: el aprobador ve todas las cuentas propias, también las que la prueba crea después (el fondo de liquidez); sin
        // alcance, el lote y el uso del fondo le responden 404 como inexistentes.
        static Task DarRolAsync(HttpClient http, DesembolsoE2E.Escenario esc) =>
            RolConAlcanceTotalAsync(http, esc, esc.Usuarios.Jefe.PublicId, "tesoreria.aprobador", "TAPR", "Aprobador de pagos (e2e)");
    }

    /// <summary>La cuenta destino del beneficiario: la pide el administrador con la certificación y la aprueba otra persona (el jefe de Cartera).</summary>
    public static async Task<Guid> CuentaDestinoAprobadaAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid persona, string numero, Guid? reemplaza = null)
    {
        var admin = esc.Coop.TokenAdmin;
        var certificado = await AdjuntosE2E.SubirAsync(http, admin, persona, Encoding.UTF8.GetBytes("%PDF-1.4 certificación bancaria"), "certificacion.pdf",
            "application/pdf", "PersonBankCertificate");
        var pedida = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/core/people/{persona}/bank-accounts", new
        {
            bankPublicId = esc.Banco, accountType = "Savings", accountNumber = numero, holderName = "Beneficiario de prueba", holderTaxId = "900100",
            certificateAttachmentPublicId = certificado.Adjunto, replacesPublicId = reemplaza,
        });
        var cuenta = pedida.GetProperty("publicId").GetGuid();
        await AprobadorDeTesoreriaAsync(http, esc);
        await InventarioE2E.ExitoAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/core/person-bank-accounts/{cuenta}/approve");
        return cuenta;
    }

    /// <summary>Cartera desembolsa por transferencia y el despachador entrega la <c>OrdenDeGiroEmitida</c> a Tesorería; devuelve la orden en la bandeja.</summary>
    /// <remarks>
    /// Con <paramref name="cuentaCambiada"/> el beneficiario tenía otra cuenta (0987654321) y la cambió hoy por la del desembolso, que la reemplaza: la orden queda
    /// con la marca de cuenta cambiada (<c>CuentasDestino.DiasDeMarca</c>).
    /// </remarks>
    public static async Task<JsonElement> OrdenDeDesembolsoAsync(CentralIdentityApiFixture fx, HttpClient http, Tesoreria t, decimal monto, bool cuentaCambiada = false)
    {
        await DesembolsoE2E.CreditoDesembolsadoAsync(http, t.Escenario, $"O{Guid.NewGuid():N}"[..8], monto, transferencia: true);
        await CarteraE2E.DespacharAsync(fx, t.Escenario.Coop.TenantPublicId, 3);
        var ordenes = await CarteraE2E.GetAsync(http, t.Admin, "/api/treasury/payment-orders");
        var orden = ordenes.EnumerateArray().First(o => o.GetProperty("netAmount").GetDecimal() == monto);
        // El deudor del desembolso es otra persona que el beneficiario de PreparadaAsync: su cuenta destino (la del desembolso, 0123456789) no está registrada y la
        // orden llega pendiente (Treasury.Order.DestinationNotRegistered). Se registra y aprueba con doble control y se revalida, como en
        // DesembolsoE2E.ResultadoDelGiroAsync (corrida del 2026-10-26: los lotes salían vacíos).
        if (orden.TryGetProperty("pendingReasons", out var motivos) && motivos.ValueKind == JsonValueKind.Array
            && motivos.EnumerateArray().Any(m => m.GetString() == "Treasury.Order.DestinationNotRegistered"))
        {
            var id = orden.GetProperty("publicId").GetGuid();
            var beneficiario = orden.GetProperty("beneficiaryPersonPublicId").GetGuid();
            Guid? anterior = cuentaCambiada ? await CuentaDestinoAprobadaAsync(http, t.Escenario, beneficiario, "0987654321") : null;
            await CuentaDestinoAprobadaAsync(http, t.Escenario, beneficiario, "0123456789", anterior);
            (await CarteraE2E.ConClaveAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-orders/{id}/revalidate", new { }))
                .IsSuccessStatusCode.Should().BeTrue("revalidar la orden con la cuenta destino aprobada");
            orden = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{id}");
        }
        return orden;
    }

    public static async Task<JsonElement> LoteAsync(HttpClient http, string token, Guid cuenta, IEnumerable<Guid> ordenes, int forma = 1, Guid? uso = null) =>
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = cuenta, form = forma, payDate = DateOnly.FromDateTime(DateTime.Today), orderPublicIds = ordenes.ToArray(),
            liquidityFundUsePublicId = uso,
        });

    public static async Task<HttpResponseMessage> MandarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null) =>
        await InventarioE2E.MandarAsync(http, token, metodo, url, cuerpo);

    public static void EsExito(HttpResponseMessage r, string que) =>
        ((int)r.StatusCode).Should().BeInRange(200, 299, $"{que}: «{r.Content.ReadAsStringAsync().GetAwaiter().GetResult()}»");

    public static HttpStatusCode Estado(HttpResponseMessage r) => r.StatusCode;
}
