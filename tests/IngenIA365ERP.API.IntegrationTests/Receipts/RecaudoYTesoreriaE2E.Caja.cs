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
        Guid Sesion, Guid Efectivo, Guid Transferencia, Guid Billete);

    public static async Task<Caja> PreparadaAsync(CentralIdentityApiFixture fx, HttpClient http, string nombre)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, $"recaudo_{nombre}");
        var t = esc.Coop.TokenAdmin;
        var (persona, _) = await DesembolsoE2E.DeudorAsync(http, t, $"Caja{nombre}");
        var creditoA = await CierreE2E.DesembolsadoAsync(http, esc, persona, 2_000_000m);
        var creditoB = await CierreE2E.DesembolsadoAsync(http, esc, persona, 3_000_000m);

        var efectivo = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "EFCJ", "Cash", esc.Hoy.AddDays(-1));
        var transferencia = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "TRCJ", "Transfer", esc.Hoy.AddDays(-1));
        var (punto, caja) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, t, esc.Coop.SucursalPrincipal, $"CJ{nombre.ToUpperInvariant()[..Math.Min(6, nombre.Length)]}",
            efectivo, transferencia);
        var cajera = await CarteraE2E.UsuarioPorCorreoAsync(http, t, esc.Coop.CorreoAdmin!);
        (await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja}/cashiers", new { userPublicId = cajera }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var billete = await BilleteAsync(http, t, 50_000m);

        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(esc.Coop);
        var sesion = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/sessions",
            new { cashBoxPublicId = caja, openingBase = new[] { new { denominationPublicId = billete, quantity = 10 } } }));
        return new Caja(esc, t, persona, creditoA, creditoB, punto, caja, sesion.GetProperty("publicId").GetGuid(), efectivo, transferencia, billete);
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

    /// <summary>Confirma con la <paramref name="clave"/> dada (la misma para repetir).</summary>
    public static async Task<HttpResponseMessage> ConfirmarAsync(HttpClient http, Caja c, Guid operacion, Guid clave)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/cash-operations/{operacion}/confirm") { Headers = { Authorization = new("Bearer", c.Token) } };
        req.Headers.Add("Idempotency-Key", clave.ToString());
        return await http.SendAsync(req);
    }

    public static object Cuota(Guid credito, decimal valor) => new { target = "Loan", amount = valor, productPublicId = credito, loanModality = "CycleInstallment" };

    public static object Abono(Guid credito, decimal valor) =>
        new { target = "Loan", amount = valor, productPublicId = credito, loanModality = "ExtraordinaryPrincipal", choice = "ReduceTerm" };

    public static object Medio(Guid medio, decimal valor, string? referencia = null) => new { paymentMeansPublicId = medio, amount = valor, reference = referencia };
}
