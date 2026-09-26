using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Ajustes, consumos internos y bajas (feature 012, T264; contracts/api.md §9.3, §10): el ciclo común sobre
/// <c>/api/inventory/adjustments</c>. Toda escritura lleva la <see cref="ClaveDeOperacion"/> que la pantalla crea al iniciar la
/// operación; la de confirmar se crea <b>una vez por intento</b> y se conserva en el reintento con el mismo contenido, así el
/// doble clic no confirma dos veces (el servidor responde la misma confirmación con <c>Idempotent-Replayed</c>).
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>Las clases del grupo de ajustes (<c>DocumentClass</c>).</summary>
    public static class ClasesDeAjuste
    {
        public const int Positivo = 10;
        public const int Negativo = 11;
        public const int ConsumoInterno = 12;
        public const int Baja = 13;
        public const int Ensamble = 14;
        public const int MovimientoEntreUbicaciones = 18;
    }

    /// <summary>El tipo de dueño de los soportes de un ajuste (actas, denuncias): <c>InventoryAdjustmentSupport</c>.</summary>
    public const string DuenoDeSoportesDeAjuste = "InventoryAdjustmentSupport";

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarAjustesAsync(FiltroDeDocumentosDeInventario filtro, CancellationToken ct = default) =>
        ListarDocumentosAsync(filtro, RutasDeGrupo.Ajustes, ct);

    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> ObtenerAjusteAsync(Guid id, CancellationToken ct = default) =>
        ObtenerDocumentoAsync(id, RutasDeGrupo.Ajustes, ct);

    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> GuardarAjusteAsync(Guid? id, BorradorDeInventarioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        GuardarBorradorAsync(RutasDeGrupo.Ajustes, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarAjusteAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        DescartarAsync(RutasDeGrupo.Ajustes, id, motivo, clave, ct);

    /// <summary>Confirma: con niveles de aprobación queda en aprobación sin número; un 422 trae en <c>data</c> lo que falta.</summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarAjusteAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        ConfirmarAsync(RutasDeGrupo.Ajustes, id, rowVersion, clave, ct);

    /// <summary>Anula con un documento contrario, con motivo.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularAjusteAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        AnularAsync(RutasDeGrupo.Ajustes, id, motivo, null, clave, ct);
}
