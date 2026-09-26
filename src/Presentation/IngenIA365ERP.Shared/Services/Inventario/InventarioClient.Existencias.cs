using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Existencias, kardex e integridad (feature 012, T264; contracts/api.md §5, §6): la lista de existencias por producto y
/// bodega, el detalle de un producto, el kardex por la vista <c>kardex</c> del centro de informes (§6.1: no hay ruta de
/// kardex bajo <c>/api/inventory</c>) y verificar (consulta, sin clave) o reconstruir (con clave y motivo) la integridad.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeExistencias = Base + "/stock";
    public const string RutaDeIntegridad = Base + "/integrity";

    /// <summary>Existencia física, reservada, disponible y en tránsito por producto y bodega del alcance.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<FilaDeExistenciaDto>>> ExistenciasAsync(FiltroDeExistencias filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<FilaDeExistenciaDto>>(HttpMethod.Get, ConQuery(RutaDeExistencias, Query(
            ("warehousePublicId", filtro.WarehousePublicId?.ToString()),
            ("productPublicId", filtro.ProductPublicId?.ToString()),
            ("categoryPublicId", filtro.CategoryPublicId?.ToString()),
            ("locationPublicId", filtro.LocationPublicId?.ToString()),
            ("search", filtro.Search),
            ("onlyWithStock", filtro.OnlyWithStock ? "true" : null),
            ("belowReorderPoint", filtro.BelowReorderPoint ? "true" : null),
            ("page", filtro.Page.ToString()),
            ("pageSize", filtro.PageSize.ToString()))), null, null, ct);

    /// <summary>El producto en las bodegas del alcance, por ubicación, en tránsito y con su estado de costo.</summary>
    public Task<ResultadoDeInventario<ExistenciaDelProductoDto>> ExistenciaDelProductoAsync(Guid producto, CancellationToken ct = default) =>
        EnviarAsync<ExistenciaDelProductoDto>(HttpMethod.Get, $"{RutaDeExistencias}/{producto}", null, null, ct);

    /// <summary>
    /// El kardex de un producto (vista <c>kardex</c>): <paramref name="desde"/>–<paramref name="hasta"/>, bodega, ubicación y si
    /// muestra las líneas de ajuste de costo. La primera fila es el saldo al día anterior a <paramref name="desde"/>.
    /// </summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> KardexAsync(Guid producto, Guid? bodega, Guid? ubicacion, DateOnly desde, DateOnly hasta,
        bool conAjustesDeCosto = true, CancellationToken ct = default) =>
        InformeAsync("kardex", QueryDelKardex(producto, bodega, ubicacion, desde, hasta, conAjustesDeCosto), ct);

    /// <summary>El mismo kardex como archivo (<paramref name="formato"/> xlsx, pdf o docx): exige <c>Inventory.Reports.Export</c>.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarKardexAsync(Guid producto, Guid? bodega, Guid? ubicacion, DateOnly desde, DateOnly hasta,
        bool conAjustesDeCosto, string formato, CancellationToken ct = default) =>
        DescargarInformeAsync("kardex", QueryDelKardex(producto, bodega, ubicacion, desde, hasta, conAjustesDeCosto), formato, ct);

    /// <summary>Compara el kardex con las proyecciones (vacío = todo lo del alcance). Es consulta: sin clave de idempotencia.</summary>
    public Task<ResultadoDeInventario<InformeDeIntegridadDto>> VerificarIntegridadAsync(VerificarIntegridadRequest request, CancellationToken ct = default) =>
        EnviarAsync<InformeDeIntegridadDto>(HttpMethod.Post, $"{RutaDeIntegridad}/verify", request, null, ct);

    /// <summary>Recalcula las proyecciones desde el kardex, con motivo y clave; nunca toca el kardex.</summary>
    public Task<ResultadoDeInventario<ResultadoDeReconstruccionDto>> ReconstruirIntegridadAsync(ReconstruirIntegridadRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeReconstruccionDto>(HttpMethod.Post, $"{RutaDeIntegridad}/rebuild", request, clave, ct);

    private static string QueryDelKardex(Guid producto, Guid? bodega, Guid? ubicacion, DateOnly desde, DateOnly hasta, bool conAjustesDeCosto) => Query(
        ("product", producto.ToString()),
        ("warehouse", bodega?.ToString()),
        ("location", ubicacion?.ToString()),
        ("from", desde.ToString("yyyy-MM-dd")),
        ("to", hasta.ToString("yyyy-MM-dd")),
        ("includeCostAdjustments", conAjustesDeCosto ? null : "false"));
}
