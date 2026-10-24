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
        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(esc.Coop);
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

    /// <summary>La cuenta destino del beneficiario: la pide el administrador con la certificación y la aprueba otra persona (el jefe de Cartera).</summary>
    public static async Task<Guid> CuentaDestinoAprobadaAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid persona, string numero)
    {
        var admin = esc.Coop.TokenAdmin;
        var certificado = await AdjuntosE2E.SubirAsync(http, admin, persona, Encoding.UTF8.GetBytes("%PDF-1.4 certificación bancaria"), "certificacion.pdf",
            "application/pdf", "PersonBankCertificate");
        var pedida = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/core/people/{persona}/bank-accounts", new
        {
            bankPublicId = esc.Banco, accountType = "Savings", accountNumber = numero, holderName = "Beneficiario de prueba", holderTaxId = "900100",
            certificateAttachmentPublicId = certificado.Adjunto,
        });
        var cuenta = pedida.GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/core/person-bank-accounts/{cuenta}/approve");
        return cuenta;
    }

    /// <summary>Cartera desembolsa por transferencia y el despachador entrega la <c>OrdenDeGiroEmitida</c> a Tesorería; devuelve la orden en la bandeja.</summary>
    public static async Task<JsonElement> OrdenDeDesembolsoAsync(CentralIdentityApiFixture fx, HttpClient http, Tesoreria t, decimal monto)
    {
        await DesembolsoE2E.CreditoDesembolsadoAsync(http, t.Escenario, $"O{Guid.NewGuid():N}"[..8], monto, transferencia: true);
        await CarteraE2E.DespacharAsync(fx, t.Escenario.Coop.TenantPublicId, 3);
        var ordenes = await CarteraE2E.GetAsync(http, t.Admin, "/api/treasury/payment-orders");
        return ordenes.EnumerateArray().First(o => o.GetProperty("netAmount").GetDecimal() == monto);
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
