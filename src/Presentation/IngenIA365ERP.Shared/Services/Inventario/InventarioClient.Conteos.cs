using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Conteos físicos (feature 012, US11, T403; contracts/api.md §12) sobre <c>/api/inventory/counts</c>: la lista con su estado derivado,
/// el detalle, la definición (borrador), abrir con la foto, capturar por tandas, las capturas, cerrar con su número, la vista previa y
/// la generación del ajuste, descartar y anular. Toda escritura lleva la <see cref="ClaveDeOperacion"/> de la operación de pantalla.
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>La clase del conteo (<c>DocumentClass.PhysicalCount</c>).</summary>
    public const int ClaseConteoFisico = 19;

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeConteoDto>>> ListarConteosAsync(FiltroDeConteos filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeConteoDto>>(HttpMethod.Get, ConQuery(RutasDeGrupo.Conteos, Query(
            ("state", filtro.State),
            ("warehousePublicId", filtro.WarehousePublicId?.ToString()),
            ("from", filtro.From?.ToString("yyyy-MM-dd")),
            ("to", filtro.To?.ToString("yyyy-MM-dd")),
            ("page", filtro.Page.ToString()),
            ("pageSize", filtro.PageSize.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<ConteoDto>> ObtenerConteoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<ConteoDto>(HttpMethod.Get, $"{RutasDeGrupo.Conteos}/{id}", null, null, ct);

    /// <summary>Define (sin <paramref name="id"/>) o cambia la definición de un conteo que todavía no se abrió.</summary>
    public Task<ResultadoDeInventario<ConteoDto>> GuardarConteoAsync(Guid? id, DefinicionDeConteoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } existente
            ? EnviarAsync<ConteoDto>(HttpMethod.Put, $"{RutasDeGrupo.Conteos}/{existente}", request, clave, ct)
            : EnviarAsync<ConteoDto>(HttpMethod.Post, RutasDeGrupo.Conteos, request, clave, ct);

    /// <summary>Abre: congela la foto; el conteo sigue sin número.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAperturaDeConteoDto>> AbrirConteoAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeAperturaDeConteoDto>(HttpMethod.Post, $"{RutasDeGrupo.Conteos}/{id}/open", new ConfirmacionRequest(rowVersion), clave, ct);

    /// <summary>Envía una tanda de lecturas: cada una suma y se acepta o se rechaza sola.</summary>
    public Task<ResultadoDeInventario<ResultadoDeCapturaDto>> CapturarConteoAsync(Guid id, CapturaDeConteoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeCapturaDto>(HttpMethod.Post, $"{RutasDeGrupo.Conteos}/{id}/captures", request, clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CapturaRegistradaDto>>> ListarCapturasAsync(Guid id, Guid? contador = null, Guid? producto = null,
        int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<CapturaRegistradaDto>>(HttpMethod.Get, ConQuery($"{RutasDeGrupo.Conteos}/{id}/captures", Query(
            ("counterUserPublicId", contador?.ToString()),
            ("productPublicId", producto?.ToString()),
            ("page", pagina.ToString()),
            ("pageSize", tamano.ToString()))), null, null, ct);

    /// <summary>Cierra: compara, exige el reconteo de lo que está fuera de tolerancia y numera el conteo.</summary>
    public Task<ResultadoDeInventario<ResultadoDeCierreDeConteoDto>> CerrarConteoAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeCierreDeConteoDto>(HttpMethod.Post, $"{RutasDeGrupo.Conteos}/{id}/close", new ConfirmacionRequest(rowVersion), clave, ct);

    public Task<ResultadoDeInventario<AjustePrevistoDto>> VerAjusteDeConteoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<AjustePrevistoDto>(HttpMethod.Get, $"{RutasDeGrupo.Conteos}/{id}/adjustment", null, null, ct);

    /// <summary>Genera el ajuste: uno o dos documentos en aprobación de alguien que no abrió ni contó.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAjusteDeConteoDto>> GenerarAjusteDeConteoAsync(Guid id, string? notas, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeAjusteDeConteoDto>(HttpMethod.Post, $"{RutasDeGrupo.Conteos}/{id}/adjustment", new AjusteDeConteoRequest(notas), clave, ct);

    /// <summary>Descarta un conteo en borrador o abierto: no consume número y libera sus productos.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> DescartarConteoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        DescartarAsync(RutasDeGrupo.Conteos, id, motivo, clave, ct);

    /// <summary>
    /// Las personas de la cooperativa, para declarar contadores (<c>GET /api/admin/users</c>): sólo las ve quien tiene
    /// <c>Security.Users.View</c>; sin él la definición no declara contadores y captura cualquiera con <c>Inventory.Counts.Capture</c>.
    /// </summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PersonaDeLaCooperativaDto>>> ListarPersonasParaContarAsync(CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<PersonaDeLaCooperativaDto>>(HttpMethod.Get, "/api/admin/users?page=1&pageSize=200", null, null, ct);

    /// <summary>Anula un conteo cerrado sin ajustes vigentes: documento contrario sin kardex ni mensajes.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularConteoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        AnularAsync(RutasDeGrupo.Conteos, id, motivo, null, clave, ct);
}
