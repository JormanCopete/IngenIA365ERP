using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// La cooperativa de ensayo de quickstart §2 reducida a lo que usan las e2e de I1 (T443), en una cooperativa aislada por
/// nombre: grupos contables <c>ASEO</c> y <c>ABARROTES</c>, la unidad <c>CAJA12</c>, productos P1 a P7 (P1 con su caja de
/// 12 y códigos de barras de unidad y de caja; P7 excluido de IVA), la sucursal S2, las bodegas PRIN y PV1 en la Principal
/// y PV2 en S2 con sus tránsitos, y —si se pide— las tres activadas fuera de producción con la diferencia aceptada
/// (I1 no tiene comparación contable). El corte por defecto es el último día del mes antepasado, así que el mes anterior
/// y el actual quedan abiertos para operar. Todo por HTTP, con el administrador de la cooperativa.
/// </summary>
public sealed class EscenarioDeInventario
{
    public required InventarioE2E.CooperativaAislada Coop { get; init; }
    public string Admin => Coop.TokenAdmin;
    public required Guid S1 { get; init; }
    public required Guid S2 { get; init; }
    public required DateOnly Corte { get; init; }

    /// <summary>PRIN, PV1, PV2 y los tránsitos TR1 (Principal) y TR2 (S2).</summary>
    public required IReadOnlyDictionary<string, Guid> Bodegas { get; init; }

    /// <summary>P1..P7 con su unidad base y, en P1, la caja.</summary>
    public required IReadOnlyDictionary<string, Producto> Productos { get; init; }

    /// <summary>Los tipos de documento por código (los sembrados: AJP, AJN, REC, FCP, DVP, TRD, TRR, MUB, CON, BAJ, CIN, SIN…).</summary>
    public required IReadOnlyDictionary<string, Guid> Tipos { get; init; }

    /// <summary>Las causas de ajuste por código (MERMA, DANO, HURTO, DIFCONTEO, RECLTRANSP…).</summary>
    public required IReadOnlyDictionary<string, Guid> Causas { get; init; }

    public sealed record Producto(Guid Id, string Codigo, Guid Unidad, Guid? Caja, string CodigoDeBarras, string? CodigoDeCaja);

    public Guid Bodega(string codigo) => Bodegas[codigo];
    public Producto P(string codigo) => Productos[codigo];

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<EscenarioDeInventario>> Preparados = [];
    private static readonly object Cerrojo = new();

    /// <summary>El escenario de ese nombre (uno por fixture: las pruebas que piden el mismo nombre lo comparten).</summary>
    public static Task<EscenarioDeInventario> PrepararAsync(CentralIdentityApiFixture fx, string nombre, bool activar = true, DateOnly? corte = null)
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx, nombre, activar, corte);
                Preparados[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>El último día del mes antepasado: deja abiertos el mes anterior y el actual.</summary>
    public static DateOnly CortePorDefecto
    {
        get
        {
            var hoy = InventarioE2E.HoyEnColombia;
            return new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1).AddDays(-1);
        }
    }

    private static async Task<EscenarioDeInventario> PrepararDeVerdadAsync(CentralIdentityApiFixture fx, string nombre, bool activar, DateOnly? corte)
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, nombre);
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;

        // Sucursal S2 (la Principal ya existe, sin código).
        var s2 = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/core/branches", new { code = "S2", name = "Sucursal Dos", shortName = "SDOS" });
        s2.IsSuccessStatusCode.Should().BeTrue(await s2.Content.ReadAsStringAsync());
        var sucursales = (await InventarioE2E.GetAsync(http, t, "/api/core/branches?PageNumber=1&PageSize=50")).GetProperty("items").EnumerateArray().ToList();
        var idS2 = sucursales.Single(b => b.GetProperty("name").GetString() == "Sucursal Dos").GetProperty("publicId").GetGuid();

        // Catálogo por plantillas, en el orden de §2.5.
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/accounting-groups",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre"], [["ASEO", "Aseo"], ["ABARROTES", "Abarrotes"]])));
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/units",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre", "decimales", "codigoDian"], [["CAJA12", "Caja x 12", "0", "DZN"]])));
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/product-categories",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre", "padre"], [["GENERAL", "General", null]])));

        string[] encabezados = ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"];
        var filas = new List<string?[]>();
        var codigos = new List<string?[]>();
        for (var i = 1; i <= 7; i++)
        {
            var excluido = i == 7;
            filas.Add([$"P{i}", i == 1 ? "JABON EN BARRA" : $"PRODUCTO {i}", "Inventoriable", "GENERAL", null, "UND", i <= 3 ? "ASEO" : "ABARROTES",
                excluido ? "Excluded" : "Taxed", excluido ? null : "IVA19", "COMPRAS"]);
            codigos.Add([$"P{i}", CodigoDeBarras(nombre, i), null]);
        }
        codigos.Add(["P1", CodigoDeCaja(nombre), "CAJA12"]);
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(
            ("Productos", encabezados, filas),
            ("CodigosDeBarras", ["producto", "codigoDeBarras", "unidad"], codigos),
            ("Unidades", ["producto", "unidad", "factor", "uso"], [["P1", "CAJA12", "12", "Both"]])));

        var productos = new Dictionary<string, Producto>();
        var lista = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
        for (var i = 1; i <= 7; i++)
        {
            var codigo = $"P{i}";
            var id = lista.Single(p => p.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
            var detalle = await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{id}");
            var und = detalle.GetProperty("baseUnit").GetProperty("publicId").GetGuid();
            Guid? caja = null;
            if (i == 1)
            {
                var unidades = await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{id}/units");
                caja = LaCaja(unidades);
            }
            productos[codigo] = new Producto(id, codigo, und, caja, CodigoDeBarras(nombre, i), i == 1 ? CodigoDeCaja(nombre) : null);
        }

        // Bodegas con sus tránsitos.
        var tiposDeBodega = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouse-types")).EnumerateArray().ToList();
        Guid TipoDeBodega(string codigo) => tiposDeBodega.Single(x => x.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
        var principal = coop.SucursalPrincipal;
        var bodegas = new Dictionary<string, Guid>
        {
            ["PRIN"] = await BodegaAsync(http, t, "PRIN", "Bodega principal", principal, TipoDeBodega("PRINCIPAL"), ("TR1", "Tránsito Principal")),
            ["PV1"] = await BodegaAsync(http, t, "PV1", "Punto de venta 1", principal, TipoDeBodega("PUNTOVENTA"), null),
            ["PV2"] = await BodegaAsync(http, t, "PV2", "Punto de venta 2", idS2, TipoDeBodega("PUNTOVENTA"), ("TR2", "Tránsito Dos")),
        };
        var todas = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouses?includeTransit=true")).EnumerateArray().ToList();
        bodegas["TR1"] = todas.Single(w => w.GetProperty("code").GetString() == "TR1").GetProperty("publicId").GetGuid();
        bodegas["TR2"] = todas.Single(w => w.GetProperty("code").GetString() == "TR2").GetProperty("publicId").GetGuid();

        var fechaDeCorte = corte ?? CortePorDefecto;
        if (activar)
            foreach (var codigo in new[] { "PRIN", "PV1", "PV2" })
                await ActivarAsync(http, t, bodegas[codigo], fechaDeCorte);

        var tipos = (await InventarioE2E.GetAsync(http, t, "/api/inventory/document-types")).EnumerateArray()
            .ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("publicId").GetGuid());
        var causas = (await InventarioE2E.GetAsync(http, t, "/api/inventory/adjustment-causes")).EnumerateArray()
            .ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("publicId").GetGuid());

        return new EscenarioDeInventario
        {
            Coop = coop, S1 = principal, S2 = idS2, Corte = fechaDeCorte, Bodegas = bodegas, Productos = productos, Tipos = tipos, Causas = causas,
        };
    }

    /// <summary>Activa una bodega fuera de producción: sin comparación contable (I1), con la diferencia aceptada y motivo.</summary>
    public static async Task ActivarAsync(HttpClient http, string token, Guid bodega, DateOnly corte)
    {
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/inventory/warehouses/{bodega}/activation", new
        {
            cutoffDate = corte.ToString("yyyy-MM-dd"), acceptDifference = true, reason = "Ensayo de I1 sin comparación contable",
        });
    }

    private static async Task<Guid> BodegaAsync(HttpClient http, string token, string codigo, string nombre, Guid sucursal, Guid tipo, (string Codigo, string Nombre)? transito)
    {
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/inventory/warehouses", new
        {
            code = codigo, name = nombre, branchPublicId = sucursal, warehouseTypePublicId = tipo,
            transitWarehouse = transito is { } tr ? new { code = tr.Codigo, name = tr.Nombre } : null,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"bodega {codigo}: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("warehouse").GetProperty("publicId").GetGuid();
    }

    private static Guid? LaCaja(JsonElement unidades)
    {
        var filas = unidades.ValueKind == JsonValueKind.Array ? unidades.EnumerateArray() : unidades.GetProperty("items").EnumerateArray();
        foreach (var u in filas)
        {
            var unidad = u.TryGetProperty("unit", out var anidada) ? anidada : u;
            var codigo = unidad.TryGetProperty("code", out var c) ? c.GetString() : unidad.GetProperty("unitCode").GetString();
            if (codigo == "CAJA12") return u.TryGetProperty("unit", out var un) ? un.GetProperty("publicId").GetGuid() : u.GetProperty("unitPublicId").GetGuid();
        }
        return null;
    }

    /// <summary>Un código de barras propio del escenario (los códigos son únicos en la cooperativa, no entre cooperativas).</summary>
    private static string CodigoDeBarras(string nombre, int i) => $"770{Math.Abs(nombre.GetHashCode()) % 100000:00000}{i:0000}";

    private static string CodigoDeCaja(string nombre) => $"177{Math.Abs(nombre.GetHashCode()) % 100000:00000}0001";

    // ------------------------------------------------------------------------------------------ documentos --

    /// <summary>Una línea: producto (P1..P7), cantidad en la unidad dada (base si no se dice) y costo unitario opcional.</summary>
    public sealed record Linea(string Producto, decimal Cantidad, decimal? Costo = null, bool EnCajas = false, Guid? Ubicacion = null, Guid? UbicacionDestino = null);

    public object Lineas(params Linea[] lineas) => lineas.Select(l => new
    {
        productPublicId = P(l.Producto).Id,
        unitPublicId = l.EnCajas ? P(l.Producto).Caja!.Value : P(l.Producto).Unidad,
        quantity = l.Cantidad,
        unitCost = l.Costo,
        locationPublicId = l.Ubicacion,
        toLocationPublicId = l.UbicacionDestino,
    }).ToList();

    /// <summary>Un borrador de ajuste (del tipo dado: AJP, AJN, BAJ, CIN, MUB) en una bodega; devuelve su <c>PublicId</c>.</summary>
    public async Task<Guid> AjusteAsync(HttpClient http, string token, string tipo, string bodega, Linea[] lineas,
        string? causa = null, DateOnly? fecha = null, string reason = "Ajuste del ensayo")
    {
        var doc = await InventarioE2E.BorradorAsync(http, token, "/api/inventory/adjustments", new
        {
            documentTypePublicId = Tipos[tipo], warehousePublicId = Bodega(bodega), reason,
            adjustmentCausePublicId = causa is null ? (Guid?)null : Causas[causa],
            operationDate = fecha?.ToString("yyyy-MM-dd"),
            lines = Lineas(lineas),
        });
        return doc.GetProperty("publicId").GetGuid();
    }

    /// <summary>Un ajuste confirmado (entrada con AJP, salida con AJN y causa MERMA); devuelve el resultado de confirmar.</summary>
    public async Task<JsonElement> AjusteConfirmadoAsync(HttpClient http, string token, string tipo, string bodega, Linea[] lineas,
        string? causa = null, DateOnly? fecha = null)
    {
        var id = await AjusteAsync(http, token, tipo, bodega, lineas, causa ?? (tipo is "AJN" or "BAJ" ? "MERMA" : null), fecha);
        return await InventarioE2E.ConfirmarAsync(http, token, "/api/inventory/adjustments", id);
    }

    /// <summary>La existencia de un producto (<c>GET /api/inventory/stock/{id}</c>).</summary>
    public Task<JsonElement> ExistenciaAsync(HttpClient http, string token, string producto) =>
        InventarioE2E.GetAsync(http, token, $"/api/inventory/stock/{P(producto).Id}");

    /// <summary>La física de un producto en una bodega, según la existencia por bodega.</summary>
    public async Task<decimal> FisicaAsync(HttpClient http, string token, string producto, string bodega)
    {
        var e = await ExistenciaAsync(http, token, producto);
        var fila = e.GetProperty("byWarehouse").EnumerateArray()
            .FirstOrDefault(w => w.GetProperty("warehouse").GetProperty("publicId").GetGuid() == Bodega(bodega));
        return fila.ValueKind == JsonValueKind.Undefined ? 0m : fila.GetProperty("physical").GetDecimal();
    }
}
