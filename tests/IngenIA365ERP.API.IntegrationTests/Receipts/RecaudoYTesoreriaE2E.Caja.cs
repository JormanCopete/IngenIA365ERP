using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Los ayudantes de las e2e de la caja (feature 015, fase 6, T0108): una cooperativa con Cartera real (el escenario del desembolso), una persona con dos créditos
/// desembolsados, un punto con su caja, efectivo y transferencia, la cajera asignada, Recaudo activado (fase 19) y la sesión abierta con 500.000. Se escriben
/// ahora y se corren en la fase 22. Los ítems de aportes se suman cuando la fase 22 componga este escenario con el de Captaciones (<c>CaptacionesE2E</c>).
/// (nuevo)
/// </summary>
public static class CajaE2E
{
    public sealed record Caja(DesembolsoE2E.Escenario Escenario, string Token, Guid Persona, Guid CreditoA, Guid CreditoB, Guid Punto, Guid CajaPublicId,
        Guid Sesion, Guid Efectivo, Guid Transferencia, Guid Billete, IReadOnlyList<Guid>? MediosAdicionales = null);

    /// <summary>
    /// La caja preparada. Con <paramref name="mediosAdicionales"/> (corrida e2e del 2026-10-27), los medios que la prueba crea —el cheque, con su parámetro— se ofrecen
    /// en el punto <b>antes</b> de abrir la sesión, que sella los medios del día; quedan en <see cref="Caja.MediosAdicionales"/>.
    /// </summary>
    public static async Task<Caja> PreparadaAsync(CentralIdentityApiFixture fx, HttpClient http, string nombre,
        Func<DesembolsoE2E.Escenario, Task<Guid[]>>? mediosAdicionales = null)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, $"recaudo_{nombre}");
        var t = esc.Coop.TokenAdmin;
        var (persona, _) = await DesembolsoE2E.DeudorAsync(http, t, $"Caja{nombre}");
        var creditoA = await CierreE2E.DesembolsadoAsync(http, esc, persona, 2_000_000m);
        var creditoB = await CierreE2E.DesembolsadoAsync(http, esc, persona, 3_000_000m);

        var efectivo = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "EFCJ", "Cash", esc.Hoy.AddDays(-1));
        var transferencia = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "TRCJ", "Transfer", esc.Hoy.AddDays(-1));
        Guid[] adicionales = mediosAdicionales is null ? [] : await mediosAdicionales(esc);
        var (punto, caja) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, t, esc.Coop.SucursalPrincipal, $"CJ{nombre.ToUpperInvariant()[..Math.Min(6, nombre.Length)]}",
            [efectivo, transferencia, .. adicionales]);
        var cajera = await CarteraE2E.UsuarioPorCorreoAsync(http, t, esc.Coop.CorreoAdmin!);
        // El cajero es una persona (Receipts.Cashier.PersonRequired): el administrador con la persona de la cooperativa.
        await CarteraE2E.PersonaDelAdministradorAsync(http, esc.Coop);
        var asignado = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja}/cashiers", new { userPublicId = cajera });
        asignado.StatusCode.Should().Be(HttpStatusCode.OK, await asignado.Content.ReadAsStringAsync());
        var billete = await BilleteAsync(http, t, 50_000m);

        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, esc.Coop);
        // Segunda ronda del 2026-10-27 (D-f; FR-231): la transferencia de la caja entra a una cuenta propia de recaudo habilitada en el punto (una: la caja la propone).
        await CuentaDeTransferenciaAsync(http, t, esc, punto, transferencia, $"TR{nombre.ToUpperInvariant()[..Math.Min(6, nombre.Length)]}", null);
        var sesion = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/sessions",
            new { cashBoxPublicId = caja, openingBase = new[] { new { denominationPublicId = billete, quantity = 10 } } }));
        return new Caja(esc, t, persona, creditoA, creditoB, punto, caja, sesion.GetProperty("publicId").GetGuid(), efectivo, transferencia, billete, adicionales);
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-f): da de alta una cuenta propia de recaudo (con su vínculo contable si se da) y la habilita para el medio en el punto; devuelve su
    /// <c>PublicId</c>.
    /// </summary>
    public static async Task<Guid> CuentaDeTransferenciaAsync(HttpClient http, string token, DesembolsoE2E.Escenario esc, Guid punto, Guid medio, string codigo,
        Guid? contable, string? numero = null)
    {
        var cuenta = await Treasury.TesoreriaE2E.CuentaPropiaAsync(http, token, esc, codigo, 1, contable, numero);
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, "/api/receipts/transfer-accounts",
            new { collectionPointPublicId = punto, paymentMeansPublicId = medio, ownBankAccountPublicId = cuenta });
        return cuenta;
    }

    public static async Task<Guid> BilleteAsync(HttpClient http, string token, decimal valor)
    {
        var lista = await InventarioE2E.GetAsync(http, token, "/api/core/cash-denominations");
        var ya = lista.EnumerateArray().FirstOrDefault(d => d.GetProperty("value").GetDecimal() == valor);
        if (ya.ValueKind == JsonValueKind.Object) return ya.GetProperty("cashDenominationPublicId").GetGuid();
        var r = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/core/cash-denominations",
            new { currency = "COP", kind = "Bill", value = valor, displayOrder = 1, validFrom = "2020-01-01" });
        return (await RecaudoYTesoreriaE2E.LeerAsync(r)).GetProperty("cashDenominationPublicId").GetGuid();
    }

    /// <summary>Una operación con sus ítems y medios; devuelve su PublicId.</summary>
    public static async Task<Guid> OperacionAsync(HttpClient http, Caja c, object[] items, object[] medios)
    {
        var op = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = c.Persona }));
        var id = op.GetProperty("publicId").GetGuid();
        (await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/items", new { items }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/means", new { means = medios }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private static readonly Dictionary<Guid, Task> Supervisores = [];

    /// <summary>
    /// El jefe del escenario de Cartera como supervisor de Recaudo (plantilla <c>recaudo.supervisor</c> más <c>Receipts.Scope.All</c>), una vez por cooperativa: ve lo
    /// esperado de la sesión (el cajero cuenta a ciegas) y aprueba la anulación que otro pidió. Hasta la corrida del 2026-10-26 no tenía permisos de Recaudo.
    /// </summary>
    public static Task SupervisorAsync(HttpClient http, Caja c)
    {
        lock (Supervisores)
        {
            if (!Supervisores.TryGetValue(c.Escenario.Coop.TenantPublicId, out var tarea))
            {
                tarea = DarAsync(http, c);
                Supervisores[c.Escenario.Coop.TenantPublicId] = tarea;
            }
            return tarea;
        }

        static async Task DarAsync(HttpClient http, Caja c)
        {
            var admin = c.Escenario.Coop.TokenAdmin;
            var creado = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template",
                new { templateKey = "recaudo.supervisor", code = "RSUPE2E", name = "Supervisor de Recaudo (e2e)" });
            var rol = creado.GetProperty("rolePublicId").GetGuid();
            var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
                .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
            var permisos = (await CarteraE2E.GetAsync(http, admin, $"/api/admin/roles/{rol}")).GetProperty("permissions").EnumerateArray()
                .Select(p => p.GetProperty("code").GetString()!).Append("Receipts.Scope.All").Distinct(StringComparer.OrdinalIgnoreCase).Select(x => catalogo[x]).ToList();
            var put = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/admin/roles/{rol}",
                new { name = "Supervisor de Recaudo (e2e)", description = (string?)null, isAssignable = true, permissionPublicIds = permisos });
            put.IsSuccessStatusCode.Should().BeTrue($"el alcance total del supervisor: {await put.Content.ReadAsStringAsync()}");
            await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{c.Escenario.Usuarios.Jefe.PublicId}/roles", new { rolePublicId = rol });
        }
    }

    /// <summary>Confirma con una clave nueva, exige el 200 (con el cuerpo en el mensaje si no) y devuelve el recibo.</summary>
    public static async Task<JsonElement> ReciboAsync(HttpClient http, Caja c, Guid operacion)
    {
        var resp = await ConfirmarAsync(http, c, operacion, Guid.NewGuid());
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await RecaudoYTesoreriaE2E.LeerAsync(resp);
    }

    /// <summary>Confirma con la <paramref name="clave"/> dada (la misma para repetir).</summary>
    public static async Task<HttpResponseMessage> ConfirmarAsync(HttpClient http, Caja c, Guid operacion, Guid clave)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/cash-operations/{operacion}/confirm") { Headers = { Authorization = new("Bearer", c.Token) } };
        req.Headers.Add("Idempotency-Key", clave.ToString());
        return await http.SendAsync(req);
    }

    /// <summary>
    /// La cuota del ciclo (<c>CycleInstallment</c>): Cartera aplica hasta lo de la cuota del ciclo y lo que sobra es excedente, que la caja no confirma sin
    /// destinarlo (FR-043, <c>Receipts.Receipt.SurplusNotAllocated</c>). En el escenario de <see cref="PreparadaAsync"/> la cuota de A (2.000.000) es de
    /// 74.397,28 y la de B (3.000.000) de unos 111.600: las pruebas pagan por debajo (corrida del 2026-10-26; antes pagaban 100.000 a 150.000 sobre A).
    /// </summary>
    public static object Cuota(Guid credito, decimal valor) => new { target = "Loan", amount = valor, productPublicId = credito, loanModality = "CycleInstallment" };

    public static object Abono(Guid credito, decimal valor) =>
        new { target = "Loan", amount = valor, productPublicId = credito, loanModality = "ExtraordinaryPrincipal", choice = "ReduceTerm" };

    public static object Medio(Guid medio, decimal valor, string? referencia = null) => new { paymentMeansPublicId = medio, amount = valor, reference = referencia };
}
