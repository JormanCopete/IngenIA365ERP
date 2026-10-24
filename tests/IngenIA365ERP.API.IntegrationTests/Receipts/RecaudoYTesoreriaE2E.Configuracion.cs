using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Los ayudantes de configuración de las e2e de Recaudo (feature 015, fases 3 y 4): un medio del catálogo común, un punto con su caja y un cajero. Se escriben
/// con las fases y se corren una sola vez al final de R0/T0 (fase 22), salvo la humo de la caja (T0183c). (nuevo)
/// </summary>
public static class ConfiguracionDeRecaudoE2E
{
    /// <summary>El medio con <paramref name="codigo"/> del catálogo común, creado si falta (clase por nombre: <c>Cash</c>, <c>Transfer</c>…).</summary>
    public static async Task<Guid> MedioAsync(HttpClient http, string token, string codigo, string clase, DateOnly desde)
    {
        var existentes = await InventarioE2E.GetAsync(http, token, "/api/core/payment-means");
        var ya = existentes.EnumerateArray().FirstOrDefault(m => m.GetProperty("code").GetString() == codigo);
        if (ya.ValueKind == JsonValueKind.Object) return ya.GetProperty("paymentMeansPublicId").GetGuid();
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/core/payment-means", new
        {
            code = codigo, name = codigo, displayOrder = 1, @class = clase, countMethod = clase == "Cash" ? "PhysicalCount" : "ByReference",
            allowsPartial = true, allowsChange = clase == "Cash", requiresReference = clase != "Cash", toleranceAmount = 1000m,
            dianPaymentMeansCode = clase == "Cash" ? "10" : "47", validFrom = desde.ToString("yyyy-MM-dd"),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(resp);
        return cuerpo.ValueKind == JsonValueKind.Object ? cuerpo.GetProperty("paymentMeansPublicId").GetGuid() : cuerpo.GetGuid();
    }

    /// <summary>Un punto de la sucursal principal con una caja (base 500.000) y sus medios ofrecidos.</summary>
    public static async Task<(Guid Punto, Guid Caja)> PuntoConCajaAsync(HttpClient http, string token, Guid oficina, string codigo, params Guid[] medios)
    {
        var punto = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/receipts/collection-points",
            new { branchPublicId = oficina, code = codigo, name = $"Punto {codigo}" }));
        var puntoId = punto.GetProperty("publicId").GetGuid();
        var caja = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/receipts/cash-boxes",
            new { collectionPointPublicId = puntoId, code = "C1", name = "Caja 1", baseMode = "DailyBase", baseAmount = 500_000m, isMainCashBox = false }));
        foreach (var medio in medios)
        {
            var r = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"/api/receipts/collection-points/{puntoId}/payment-means",
                new { paymentMeansPublicId = medio });
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        }
        return (puntoId, caja.GetProperty("publicId").GetGuid());
    }
}
