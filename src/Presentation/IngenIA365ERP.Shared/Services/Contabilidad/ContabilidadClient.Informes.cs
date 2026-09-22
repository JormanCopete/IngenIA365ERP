using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Consultas e informes contables (feature 009 E2, US5): una sola ruta,
/// <c>/api/reports/accounting/{vista}</c>, con la vista en el camino y los filtros en la query
/// string (<see cref="FiltrosDeInformeModelo.ToQuery"/>). En JSON devuelve la tabla que pinta
/// <c>TablaDeReporte</c>; con <c>format=xlsx|pdf|docx</c> devuelve el archivo. Mismo molde que los
/// reportes de nómina (<c>NominaClient.ReporteAsync</c>).
/// </summary>
public sealed partial class ContabilidadClient
{
    private const string BaseInformes = "/api/reports/accounting";

    /// <summary>Vistas que ofrece la API (contracts/api.md §8): la pantalla las nombra por esta clave.</summary>
    public static class Vistas
    {
        public const string LibroAuxiliar = "ledger";
        public const string BalanceDePrueba = "trial-balance";
        public const string LibroDiario = "journal";
        public const string LibroMayor = "general-ledger";
        public const string RelacionDeComprobantes = "voucher-list";
        public const string EstadoDeCuentaTercero = "third-party-statement";
        public const string DocumentosPendientes = "pending-documents";
        public const string SaldoDiarioPromedio = "daily-average";
        public const string SituacionFinanciera = "financial-position";
        public const string Resultados = "income-statement";
        public const string CambiosEnElPatrimonio = "equity-changes";
        public const string FlujoDeEfectivo = "cash-flow";
        public const string EjecucionPresupuestal = "budget-execution";
    }

    /// <summary>La tabla de una vista con los filtros dados (<paramref name="query"/> sin «?»; puede venir vacía).</summary>
    public Task<InvitationApiResult<TablaReporteDto>> InformeAsync(string vista, string query, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, RutaDeInforme(vista, query, null), null, ct);

    /// <summary>La misma vista y los mismos filtros, como archivo (<paramref name="formato"/> xlsx, pdf o docx).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarInformeAsync(string vista, string query, string formato, CancellationToken ct = default) =>
        DescargarAsync(RutaDeInforme(vista, query, formato), ct);

    private static string RutaDeInforme(string vista, string query, string? formato)
    {
        var q = query.TrimStart('?');
        if (!string.IsNullOrEmpty(formato)) q = FiltrosDeInformeModelo.Unir(q, $"format={Uri.EscapeDataString(formato)}");
        return string.IsNullOrEmpty(q) ? $"{BaseInformes}/{vista}" : $"{BaseInformes}/{vista}?{q}";
    }
}
