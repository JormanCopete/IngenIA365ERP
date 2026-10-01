using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Ayudantes de las e2e de la integración contable de Inventario (feature 012, I2; T472–T475): el escenario de inventario de I1
/// con la contabilidad iniciada, las cuentas auxiliares habilitadas para Inventario y la matriz mínima de las operaciones que
/// usan las pruebas (compra, factura del proveedor, devolución, ajustes y traslados), la bandeja por documento y las consultas
/// por debajo de la API que la integridad necesita (huérfanos, cuadre del libro). Todo lo demás, por HTTP. (nuevo)
/// </summary>
public static class ContabilidadDeInventarioE2E
{
    public const string Inventario = "13052001";
    public const string Transito = "13051001";
    public const string MercanciaPorFacturar = "24109501";
    public const string CuentaPorPagar = "24109502";
    public const string SobranteDeInventario = "42451501";
    public const string PerdidaDeInventario = "51151201";

    private const string Reglas = "/api/accounting/inventory/rules";

    /// <summary>
    /// El escenario de inventario (<see cref="EscenarioDeInventario"/>) en una cooperativa aislada con la contabilidad iniciada
    /// en el ejercicio de hoy, las seis auxiliares y la matriz: las reglas generales de los dos grupos (<c>ASEO</c>,
    /// <c>ABARROTES</c>) para compra, factura, devolución, ajustes positivos y negativos (causa <c>MERMA</c>), ajuste de costo
    /// y traslados. La causa <c>DANO</c> queda sin contrapartida a propósito: es el «producto sin regla» de la validación previa.
    /// </summary>
    public static async Task<EscenarioDeInventario> PrepararAsync(CentralIdentityApiFixture fx, string nombre, bool activar = true)
    {
        var ejercicio = InventarioE2E.HoyEnColombia.Year;
        var esc = await EscenarioDeInventario.PrepararAsync(fx, nombre, activar, primerEjercicioContable: ejercicio,
            corte: EscenarioDeInventario.CortePorDefecto.Year == ejercicio ? null : new DateOnly(ejercicio, 1, 1).AddDays(-1));
        using var http = fx.CreateClient();
        var t = esc.Admin;
        if ((await InventarioE2E.GetAsync(http, t, $"{Reglas}?pageSize=5")).GetProperty("totalCount").GetInt32() > 0) return esc;

        string[] inv = ["CNT", "INV"];
        await ContabilidadE2E.CrearCuentaAsync(http, t, Inventario, "Mercancías no fabricadas", "130520", inv);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Transito, "Mercancías en tránsito", "130510", inv);
        await ContabilidadE2E.CrearCuentaAsync(http, t, MercanciaPorFacturar, "Mercancía recibida por facturar", "241095", inv);
        await ContabilidadE2E.CrearCuentaAsync(http, t, CuentaPorPagar, "Proveedores de mercancía", "241095", inv);
        await ContabilidadE2E.CrearCuentaAsync(http, t, SobranteDeInventario, "Sobrantes de inventario", "424515", inv);
        await ContabilidadE2E.CrearCuentaAsync(http, t, PerdidaDeInventario, "Pérdidas de inventario", "511512", inv);

        var desde = new DateOnly(ejercicio, 1, 1);
        foreach (var grupo in new[] { "ASEO", "ABARROTES" })
        {
            foreach (var operacion in new[] { "Compra", "DevolucionAProveedor", "AjustePositivo", "AjusteNegativo", "AjusteDeCosto", "Reclasificacion" })
                await ReglaAsync(http, t, operacion, "Inventario", Inventario, desde, new { accountingGroupCode = grupo });
            foreach (var operacion in new[] { "Compra", "DevolucionAProveedor", "AjustePositivo", "AjusteNegativo", "AjusteDeCosto", "DespachoTraslado", "RecepcionTraslado", "Reclasificacion" })
                await ReglaAsync(http, t, operacion, "Transito", Transito, desde, new { accountingGroupCode = grupo });
            foreach (var operacion in new[] { "DespachoTraslado", "RecepcionTraslado" })
                await ReglaAsync(http, t, operacion, "Inventario", Inventario, desde, new { accountingGroupCode = grupo });
        }
        foreach (var operacion in new[] { "Compra", "FacturaProveedor", "DevolucionAProveedor" })
            await ReglaAsync(http, t, operacion, "MercanciaPorFacturar", MercanciaPorFacturar, desde, new { });
        await ReglaAsync(http, t, "FacturaProveedor", "CuentaPorPagar", CuentaPorPagar, desde, new { });
        await ReglaAsync(http, t, "AjustePositivo", "Contrapartida", SobranteDeInventario, desde, new { });
        await ReglaAsync(http, t, "AjusteNegativo", "Contrapartida", PerdidaDeInventario, desde, new { reasonCode = "MERMA" });
        return esc;
    }

    public static async Task ReglaAsync(HttpClient http, string token, string operacion, string rol, string cuenta, DateOnly desde, object dimensiones)
    {
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, Reglas, new
        {
            operation = operacion, role = rol, dimensions = dimensiones, accountPublicId = await ContabilidadE2E.CuentaAsync(http, token, cuenta),
            validFrom = desde.ToString("yyyy-MM-dd"), reason = "Matriz del ensayo",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"regla {operacion}/{rol}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Una vigencia de un parámetro de Contabilidad de Inventario (general, por tipo o por cadena).</summary>
    public static Task<JsonElement> ParametroAsync(HttpClient http, string token, string clave, string valor, DateOnly desde,
        Guid? tipo = null, string? cadena = null) =>
        InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/inventory/parameters/INV/{clave}/versions", new
        {
            scopeKind = tipo is null && cadena is null ? "None" : "DocumentType", scopePublicId = tipo, chain = cadena, value = valor,
            validFrom = desde.ToString("yyyy-MM-dd"), reason = "Ensayo de la integración contable", confirmFiscalWithoutPosting = true,
        });

    /// <summary>Las entregas de la bandeja de un documento de Inventario.</summary>
    public static async Task<List<JsonElement>> MensajesAsync(HttpClient http, string token, Guid documento) =>
        (await InventarioE2E.GetAsync(http, token, $"/api/inventory/messages?document={documento}&pageSize=100"))
        .GetProperty("messages").GetProperty("items").EnumerateArray().ToList();

    /// <summary>La única entrega de negocio de ese tipo de un documento.</summary>
    public static async Task<JsonElement> MensajeAsync(HttpClient http, string token, Guid documento, string tipo)
    {
        var lista = (await MensajesAsync(http, token, documento)).Where(m => m.GetProperty("type").GetString() == tipo).ToList();
        lista.Should().ContainSingle($"el documento {documento} emite un «{tipo}» (tiene: {string.Join(", ", lista.Select(m => m.GetProperty("type").GetString()))})");
        return lista[0];
    }

    /// <summary><c>DeliveryStatus</c>: Pending 0, InBatch 1, Processed 2, Rejected 3, NotApplicable 4, ValidationFailed 5.</summary>
    public static int Estado(JsonElement mensaje) => mensaje.GetProperty("deliveryStatus").GetInt32();

    public static string? TipoDeComprobante(JsonElement mensaje) =>
        mensaje.GetProperty("result") is { ValueKind: JsonValueKind.Object } r && r.GetProperty("voucherTypeCode").ValueKind == JsonValueKind.String
            ? r.GetProperty("voucherTypeCode").GetString()
            : null;

    public static Guid? Comprobante(JsonElement mensaje) =>
        mensaje.GetProperty("result") is { ValueKind: JsonValueKind.Object } r && r.GetProperty("accountingDocumentPublicId").ValueKind == JsonValueKind.String
            ? r.GetProperty("accountingDocumentPublicId").GetGuid()
            : null;

    public static async Task<int> EnteroAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc, string sqlPostgres, string sqlSqlServer) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop, sqlPostgres, sqlSqlServer));

    /// <summary>Comprobantes de origen <c>INV</c> vivos sin recibo de mensaje (huérfanos del lado contable).</summary>
    public static Task<int> ComprobantesSinReciboAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc) => EnteroAsync(fx, esc,
        """
        SELECT COUNT(*) FROM dbo."ACC_Documents" d
        WHERE d."OriginModule" = 'INV' AND d."IsDeleted" = false
          AND NOT EXISTS (SELECT 1 FROM dbo."ACC_InventoryPostings" p WHERE p."AccountingDocumentId" = d."Id")
        """,
        """
        SELECT COUNT(*) FROM [dbo].[ACC_Documents] d
        WHERE d.[OriginModule] = 'INV' AND d.[IsDeleted] = 0
          AND NOT EXISTS (SELECT 1 FROM [dbo].[ACC_InventoryPostings] p WHERE p.[AccountingDocumentId] = d.[Id])
        """);

    /// <summary>Recibos de mensajes de negocio sin comprobante que no se explican por valor cero (huérfanos del lado de Inventario).</summary>
    public static Task<int> RecibosSinComprobanteAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc) => EnteroAsync(fx, esc,
        """
        SELECT COUNT(*) FROM dbo."ACC_InventoryPostings" p
        WHERE p."AccountingDocumentId" IS NULL AND p."MessageKind" = 1 AND COALESCE(p."NoVoucherReason", '') <> 'ZeroValue'
        """,
        """
        SELECT COUNT(*) FROM [dbo].[ACC_InventoryPostings] p
        WHERE p.[AccountingDocumentId] IS NULL AND p.[MessageKind] = 1 AND COALESCE(p.[NoVoucherReason], '') <> 'ZeroValue'
        """);

    /// <summary>Σ débito − Σ crédito de todo lo contabilizado (el balance de prueba cuadra si da cero).</summary>
    public static async Task<decimal> DescuadreDelLibroAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc) =>
        Convert.ToDecimal(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT COALESCE(SUM("Debit") - SUM("Credit"), 0) FROM dbo."ACC_JournalEntries" WHERE "IsPosted" = true AND "IsDeleted" = false""",
            "SELECT COALESCE(SUM([Debit]) - SUM([Credit]), 0) FROM [dbo].[ACC_JournalEntries] WHERE [IsPosted] = 1 AND [IsDeleted] = 0"));

    /// <summary>Cuántos comprobantes vivos tienen ese documento de Inventario como origen directo.</summary>
    public static Task<int> ComprobantesDelDocumentoAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc, Guid documento) => EnteroAsync(fx, esc,
        $"""SELECT COUNT(DISTINCT p."AccountingDocumentId") FROM dbo."ACC_InventoryPostings" p WHERE p."SourcePublicId" = '{documento}' AND p."AccountingDocumentId" IS NOT NULL""",
        $"SELECT COUNT(DISTINCT p.[AccountingDocumentId]) FROM [dbo].[ACC_InventoryPostings] p WHERE p.[SourcePublicId] = '{documento}' AND p.[AccountingDocumentId] IS NOT NULL");
}
