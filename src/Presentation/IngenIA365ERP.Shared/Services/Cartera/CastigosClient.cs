using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de US14 (feature 013, K3, T0916; contracts/api.md §11.3 y §12.6): los lotes de castigo (lista, detalle, armar, agregar y quitar
/// créditos, enviar al consejo, aplicar con el punto del acta, registrar el reporte al supervisor) y la evaluación de cartera (lista, detalle,
/// preparar, aplicar con los puntos del comité y del consejo). Las decisiones del consejo y del comité se toman en Actas. Sin <c>Authorization</c>
/// a mano: la pone el handler; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class CastigosClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeLotes = GobiernoDeCarteraClient.Base + "/write-off-batches";
    public const string RutaDeEvaluaciones = GobiernoDeCarteraClient.Base + "/risk/evaluations";

    /// <summary>El dueño de adjunto de los soportes del castigo (informe motivado, concepto de irrecuperabilidad, cruce de aportes): el crédito.</summary>
    public const string DuenoDeSoportes = "LendingWriteOffSupport";

    /// <summary>El dueño de adjunto del soporte del reporte al supervisor: el lote.</summary>
    public const string DuenoDelReporte = "LendingWriteOffSupervisorReport";

    // ------------------------------------------------------------------------------------------------ lotes de castigo --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<LoteDeCastigoDto>>> LotesAsync(string? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<LoteDeCastigoDto>>(HttpMethod.Get, ConQuery(RutaDeLotes, Query(("status", estado), ("page", Texto(pagina)))),
            null, null, ct);

    public Task<ResultadoDeInventario<LoteDeCastigoDto>> LoteAsync(Guid lote, CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Get, $"{RutaDeLotes}/{lote}", null, null, ct);

    /// <summary>Arma el lote: los que cumplen quedan incluidos y los que no, rechazados con lo que les falta.</summary>
    public Task<ResultadoDeInventario<LoteDeCastigoDto>> ArmarAsync(LoteDeCastigoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Post, RutaDeLotes, request, clave, ct);

    /// <summary>Agrega un crédito al borrador; si no cumple, <c>Lending.WriteOff.RequirementMissing</c> con <c>data.missing</c>.</summary>
    public Task<ResultadoDeInventario<LoteDeCastigoDto>> AgregarAsync(Guid lote, ItemDeCastigoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Post, $"{RutaDeLotes}/{lote}/items", request, clave, ct);

    public Task<ResultadoDeInventario<LoteDeCastigoDto>> QuitarAsync(Guid lote, Guid item, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Delete, $"{RutaDeLotes}/{lote}/items/{item}", null, clave, ct);

    /// <summary>Congela el lote para llevarlo al consejo.</summary>
    public Task<ResultadoDeInventario<LoteDeCastigoDto>> EnviarAlConsejoAsync(Guid lote, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Post, $"{RutaDeLotes}/{lote}/submit", null, clave, ct);

    public Task<ResultadoDeInventario<CastigoAplicadoDto>> AplicarAsync(Guid lote, AplicarCastigoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CastigoAplicadoDto>(HttpMethod.Post, $"{RutaDeLotes}/{lote}/apply", request, clave, ct);

    public Task<ResultadoDeInventario<LoteDeCastigoDto>> ReportarAlSupervisorAsync(Guid lote, ReporteAlSupervisorRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LoteDeCastigoDto>(HttpMethod.Post, $"{RutaDeLotes}/{lote}/supervisor-report", request, clave, ct);

    // -------------------------------------------------------------------------------------------- evaluación de cartera --

    public Task<ResultadoDeInventario<List<EvaluacionDeCarteraDto>>> EvaluacionesAsync(string? estado = null, CancellationToken ct = default) =>
        Enviar<List<EvaluacionDeCarteraDto>>(HttpMethod.Get, ConQuery(RutaDeEvaluaciones, Query(("status", estado))), null, null, ct);

    public Task<ResultadoDeInventario<EvaluacionDeCarteraDto>> EvaluacionAsync(Guid evaluacion, int pagina = 1, CancellationToken ct = default) =>
        Enviar<EvaluacionDeCarteraDto>(HttpMethod.Get, ConQuery($"{RutaDeEvaluaciones}/{evaluacion}", Query(("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<EvaluacionDeCarteraDto>> PrepararAsync(EvaluacionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EvaluacionDeCarteraDto>(HttpMethod.Post, RutaDeEvaluaciones, request, clave, ct);

    public Task<ResultadoDeInventario<EvaluacionDeCarteraDto>> AplicarEvaluacionAsync(Guid evaluacion, AplicarEvaluacionRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EvaluacionDeCarteraDto>(HttpMethod.Post, $"{RutaDeEvaluaciones}/{evaluacion}/apply", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
