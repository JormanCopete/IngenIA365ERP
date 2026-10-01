using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// Las ventas de I3 con la integración contable de I2 (T567, T570, T650): el escenario de ventas (<see cref="EscenarioDeVentas"/>) en
/// una cooperativa con la contabilidad iniciada y la matriz de <see cref="ContabilidadDeInventarioE2E"/>, más las cuentas y reglas de la
/// venta de quickstart §5.1 paso 3: una cuenta por medio de pago (la caja por punto), el crédito de asociados a una cuenta provisional
/// que exige tercero y documento cruce (contracts/contabilidad.md §3.4), ingreso, costo y devolución por grupo contable. Las ventas del
/// ensayo contable son de P7, excluido de IVA: la matriz no necesita cuentas de impuesto. (nuevo)
/// </summary>
public static class ContabilidadDeVentasE2E
{
    public const string CajaPv1 = "11050501";
    public const string PorCobrarVisa = "16050501";
    public const string PorCobrarMaster = "16050502";
    public const string Bonos = "16050503";
    public const string CreditoAsociados = "16050504";
    public const string Transferencias = "16050505";
    public const string Ingresos = "41359501";
    public const string Devoluciones = "41359502";
    public const string CostoDeVentas = "61352001";

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<EscenarioDeVentas>> Preparados = [];
    private static readonly object Cerrojo = new();

    /// <summary>El escenario contable de ventas de ese nombre (uno por fixture).</summary>
    public static Task<EscenarioDeVentas> PrepararAsync(CentralIdentityApiFixture fx, string nombre)
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx, nombre);
                Preparados[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<EscenarioDeVentas> PrepararDeVerdadAsync(CentralIdentityApiFixture fx, string nombre)
    {
        // Primero la contabilidad y la matriz de I2 (la existencia de PV1 entra por un ajuste que se contabiliza); después, lo de ventas.
        await ContabilidadDeInventarioE2E.PrepararAsync(fx, nombre);
        using var http = fx.CreateClient();
        var inv = await EscenarioDeInventario.PrepararAsync(fx, nombre);
        var t = inv.Admin;
        string[] modulos = ["CNT", "INV"];
        await ContabilidadE2E.CrearCuentaAsync(http, t, CajaPv1, "Caja del punto PV1", "110505", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, PorCobrarVisa, "Por cobrar a Redeban", "160505", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, PorCobrarMaster, "Por cobrar a Credibanco", "160505", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Bonos, "Bonos de mercancía", "160505", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, CreditoAsociados, "Por cobrar provisional a asociados", "160505", modulos, tercero: true, cruce: true);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Transferencias, "Transferencias por identificar", "160505", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Ingresos, "Venta de mercancía", "413595", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Devoluciones, "Devoluciones en ventas", "413595", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, t, CostoDeVentas, "Costo de la mercancía vendida", "613520", modulos);

        var desde = new DateOnly(InventarioE2E.HoyEnColombia.Year, 1, 1);
        foreach (var grupo in new[] { "ASEO", "ABARROTES" })
        {
            var g = new { accountingGroupCode = grupo };
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "Venta", "Ingreso", Ingresos, desde, g);
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "CostoDeVenta", "Costo", CostoDeVentas, desde, g);
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "CostoDeVenta", "Inventario", ContabilidadDeInventarioE2E.Inventario, desde, g);
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "DevolucionDeCliente", "Inventario", ContabilidadDeInventarioE2E.Inventario, desde, g);
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "DevolucionDeCliente", "Costo", CostoDeVentas, desde, g);
        }
        await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "NotaCredito", "Devolucion", Devoluciones, desde, new { });
        await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "Venta", "Descuento", Devoluciones, desde, new { });
        await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "NotaCredito", "Descuento", Devoluciones, desde, new { });

        var escenario = await EscenarioDeVentas.PrepararAsync(fx, nombre, contabilidad: true);
        foreach (var operacion in new[] { "Venta", "NotaCredito" })
        {
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", CajaPv1, desde, new { paymentMeansCode = "EFECTIVO", pointOfSaleCode = "PV1" });
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", PorCobrarVisa, desde, new { paymentMeansCode = "VISARB" });
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", PorCobrarMaster, desde, new { paymentMeansCode = "MCCB" });
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", Bonos, desde, new { paymentMeansCode = "BONOMERC" });
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", Transferencias, desde, new { paymentMeansCode = "TRANSFBCO" });
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, operacion, "MedioDePago", CreditoAsociados, desde, new { paymentMeansCode = "CREDASOC" });
        }
        return escenario;
    }

    /// <summary>Un asociado nuevo (persona y afiliación en un paso); devuelve su persona.</summary>
    public static async Task<Guid> AsociadoAsync(HttpClient http, string token, string nombre, string documento)
    {
        var alta = await InventarioE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new { idType = "C", taxId = documento, firstName = nombre, lastName = "Asociado", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.ventas.test" },
            associate = new { joinDate = new DateOnly(2026, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        return (await InventarioE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
    }

    /// <summary>Las líneas del comprobante contable (por su PublicId): cuenta, débito, crédito, tercero y documento cruce.</summary>
    public static async Task<List<JsonElement>> LineasDelComprobanteAsync(HttpClient http, string token, Guid comprobante)
    {
        var documento = await InventarioE2E.GetAsync(http, token, $"/api/accounting/documents/{comprobante}");
        return documento.GetProperty("lines").EnumerateArray().ToList();
    }

    /// <summary>El código de cuenta de una línea del comprobante (viene como objeto o como código).</summary>
    public static string Cuenta(JsonElement linea) =>
        linea.TryGetProperty("account", out var c) && c.ValueKind == JsonValueKind.Object ? c.GetProperty("code").GetString()!
        : linea.TryGetProperty("accountCode", out var codigo) ? codigo.GetString()! : c.GetString()!;

    public static decimal Debito(JsonElement linea) => linea.GetProperty("debit").GetDecimal();

    public static decimal Credito(JsonElement linea) => linea.GetProperty("credit").GetDecimal();
}
