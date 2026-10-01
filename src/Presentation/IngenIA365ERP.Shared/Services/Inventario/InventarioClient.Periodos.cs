using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Períodos de inventario, valorizado y cambio de grupo contable (feature 012, T294; contracts/api.md §3.6.4, §13.4, §27): la
/// lista de meses, la vista previa del cierre (consultas, sin clave), cerrar reconociendo los avisos y reabrir el último con
/// motivo (con la <see cref="ClaveDeOperacion"/> de la pantalla), el valorizado a una fecha por la vista <c>valuation</c> y el
/// historial y el cambio del grupo contable de un producto.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDePeriodos = Base + "/periods";

    public Task<ResultadoDeInventario<PeriodosDeInventarioDto>> PeriodosAsync(int? anio = null, CancellationToken ct = default) =>
        EnviarAsync<PeriodosDeInventarioDto>(HttpMethod.Get, ConQuery(RutaDePeriodos, Query(("year", anio?.ToString()))), null, null, ct);

    /// <summary>La vista previa del cierre: bloqueos, avisos y remisiones sin facturar.</summary>
    public Task<ResultadoDeInventario<RevisionDelCierreDto>> RevisarCierreAsync(int anio, int mes, CancellationToken ct = default) =>
        EnviarAsync<RevisionDelCierreDto>(HttpMethod.Get, $"{RutaDePeriodos}/{anio}/{mes}/close", null, null, ct);

    /// <summary>Cierra el mes; sin reconocer los avisos, un 422 <c>Inventory.Period.WarningsNotAcknowledged</c> los trae en <c>data</c>.</summary>
    public Task<ResultadoDeInventario<ResultadoDelCierreDto>> CerrarPeriodoAsync(int anio, int mes, CerrarPeriodoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDelCierreDto>(HttpMethod.Post, $"{RutaDePeriodos}/{anio}/{mes}/close", request, clave, ct);

    /// <summary>Reabre el último mes cerrado, con motivo (permiso especial).</summary>
    public Task<ResultadoDeInventario<ResultadoDeLaReaperturaDto>> ReabrirPeriodoAsync(int anio, int mes, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeLaReaperturaDto>(HttpMethod.Post, $"{RutaDePeriodos}/{anio}/{mes}/reopen", new ReabrirPeriodoRequest(motivo), clave, ct);

    /// <summary>El valorizado a <paramref name="fecha"/> por grupo, bodega y producto (vista <c>valuation</c>; exige <c>Inventory.Costs.Read</c>).</summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> ValorizadoAsync(DateOnly fecha, Guid? bodega = null, Guid? grupo = null, bool conTransito = false,
        CancellationToken ct = default) =>
        InformeAsync("valuation", QueryDelValorizado(fecha, bodega, grupo, conTransito), ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarValorizadoAsync(DateOnly fecha, Guid? bodega, Guid? grupo, bool conTransito, string formato,
        CancellationToken ct = default) =>
        DescargarInformeAsync("valuation", QueryDelValorizado(fecha, bodega, grupo, conTransito), formato, ct);

    /// <summary>El historial del grupo contable de un producto, el más reciente primero.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<CambioDeGrupoDto>>> HistorialDeGrupoAsync(Guid producto, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CambioDeGrupoDto>>(HttpMethod.Get, $"{Base}/products/{producto}/accounting-group", null, null, ct);

    /// <summary>Cambia el grupo contable desde la fecha efectiva (vacía = hoy), con motivo.</summary>
    public Task<ResultadoDeInventario<ResultadoDelCambioDeGrupoDto>> CambiarGrupoContableAsync(Guid producto, CambiarGrupoContableRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDelCambioDeGrupoDto>(HttpMethod.Post, $"{Base}/products/{producto}/accounting-group", request, clave, ct);

    private static string QueryDelValorizado(DateOnly fecha, Guid? bodega, Guid? grupo, bool conTransito) => Query(
        ("asOf", fecha.ToString("yyyy-MM-dd")),
        ("warehouse", bodega?.ToString()),
        ("accountingGroup", grupo?.ToString()),
        ("includeTransit", conTransito ? "true" : null));
}
