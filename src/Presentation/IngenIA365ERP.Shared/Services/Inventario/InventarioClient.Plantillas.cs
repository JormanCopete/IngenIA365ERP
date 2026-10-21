using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Las plantillas de importación, genéricas sobre la ruta base de cada catálogo (feature 012, T183, T184;
/// contracts/plantillas.md §0): <c>GET {base}/template.xlsx</c> vacía o con datos, y <c>POST {base}/import?mode=review|apply</c>.
/// Las usa el componente único <c>ImportarPlantilla</c>; sirven igual para Inventario que para Core
/// (<c>/api/core/taxes</c>) porque la mecánica es una. Revisar y aplicar son dos operaciones: cada una con su clave.
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>Las dieciséis plantillas en su orden de carga, con <c>canDownload</c>/<c>canImport</c> de quien pregunta.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PlantillaDeParametrizacionDto>>> ListarPlantillasAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<PlantillaDeParametrizacionDto>>(HttpMethod.Get, $"{Base}/templates", null, null, ct);

    /// <summary>La plantilla vacía, o llena con lo que hoy tiene la cooperativa (<paramref name="conDatos"/>; se audita como exportación).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaAsync(string rutaBase, bool conDatos, CancellationToken ct = default) =>
        DescargarAsync($"{rutaBase.TrimEnd('/')}/template.xlsx{(conDatos ? "?withData=true" : string.Empty)}", "plantilla.xlsx", ct);

    /// <summary>Corre las mismas reglas que aplicar y no guarda nada; responde 200 aunque haya errores (<c>valid = false</c>).</summary>
    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> RevisarPlantillaAsync(string rutaBase, string archivo, byte[] contenido,
        string? motivo, ClaveDeOperacion clave, CancellationToken ct = default, string? queryAdicional = null) =>
        SubirAsync<ResultadoDeImportacionDto>($"{rutaBase.TrimEnd('/')}/import?mode=review{Mas(queryAdicional)}", archivo, contenido, Campos(motivo), clave, ct);

    /// <summary>La revisión como el mismo libro con las columnas <c>resultado</c> y <c>errores</c> (<c>format=xlsx</c>).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> RevisionEnExcelAsync(string rutaBase, string archivo, byte[] contenido,
        string? motivo, ClaveDeOperacion clave, CancellationToken ct = default, string? queryAdicional = null) =>
        SubirYDescargarAsync($"{rutaBase.TrimEnd('/')}/import?mode=review&format=xlsx{Mas(queryAdicional)}", archivo, contenido, Campos(motivo), clave, ct);

    /// <summary>
    /// Todo o nada. Con un solo error no guarda y responde 422 <c>Import.Invalid</c> con la revisión en <c>data</c>
    /// (<see cref="ResultadoDeInventario{T}.ComoImportacion"/>).
    /// </summary>
    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> AplicarPlantillaAsync(string rutaBase, string archivo, byte[] contenido,
        string? motivo, ClaveDeOperacion clave, CancellationToken ct = default, string? queryAdicional = null) =>
        SubirAsync<ResultadoDeImportacionDto>($"{rutaBase.TrimEnd('/')}/import?mode=apply{Mas(queryAdicional)}", archivo, contenido, Campos(motivo), clave, ct);

    /// <summary>
    /// Feature 013 (US8, T0758): lo que la ruta de importación de una plantilla pide además del modo (<c>replace=true</c> en las partes de la carga de
    /// migración de Cartera), sin «?» ni «&amp;» inicial.
    /// </summary>
    private static string Mas(string? queryAdicional) => string.IsNullOrWhiteSpace(queryAdicional) ? string.Empty : "&" + queryAdicional.TrimStart('&', '?');

    private static Dictionary<string, string>? Campos(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? null : new Dictionary<string, string> { ["reason"] = motivo.Trim() };
}
