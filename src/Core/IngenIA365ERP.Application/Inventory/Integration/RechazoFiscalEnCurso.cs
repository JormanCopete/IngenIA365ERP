using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Lo que la fuente de Inventario está haciendo en esta petición por un rechazo de la DIAN (feature 012, I4, T724, T725; contracts/dian.md
/// §8.3 y §8.4; mensajes.md §6.9 y §8.2). <see cref="FuenteDeEmisionDeInventario"/> lo anota antes de confirmar y los efectos de la venta lo
/// leen al emitir sus mensajes:
/// <list type="bullet">
/// <item>la anulación <b>sin efecto fiscal</b> (casos b y c): <see cref="Efectos.AnulacionDeVenta"/> la admite sobre una clase fiscal
/// electrónica, pone <c>fiscalCase</c> en <c>DocumentoAnulado</c> y ajusta el crédito con <c>VoidingByDianRejection</c>;</item>
/// <item>el reemplazo (caso b): la venta de reemplazo no registra un crédito nuevo sino que ajusta el del rechazado con
/// <c>Replacement</c> por el valor nuevo.</item>
/// </list>
/// Scoped: vive lo que dura la petición y sólo lo escribe la fuente. (nuevo)
/// </summary>
public sealed class RechazoFiscalEnCurso
{
    private readonly Dictionary<Guid, CasoFiscalDeAnulacion> _anulaciones = [];
    private readonly Dictionary<Guid, Guid> _reemplazos = [];

    /// <summary>La anulación <paramref name="anulacionPublicId"/> es sin efecto fiscal, por el caso <paramref name="caso"/>.</summary>
    public void AnularSinEfectoFiscal(Guid anulacionPublicId, CasoFiscalDeAnulacion caso) => _anulaciones[anulacionPublicId] = caso;

    /// <summary>El caso fiscal de la anulación, o nulo si es una anulación corriente.</summary>
    public CasoFiscalDeAnulacion? CasoDe(Guid anulacionPublicId) => _anulaciones.TryGetValue(anulacionPublicId, out var c) ? c : null;

    /// <summary>El documento <paramref name="reemplazoPublicId"/> reemplaza al rechazado <paramref name="rechazadoPublicId"/> (caso b).</summary>
    public void Reemplazar(Guid reemplazoPublicId, Guid rechazadoPublicId) => _reemplazos[reemplazoPublicId] = rechazadoPublicId;

    /// <summary>El rechazado que reemplaza <paramref name="reemplazoPublicId"/>, o nulo.</summary>
    public Guid? ReemplazaA(Guid reemplazoPublicId) => _reemplazos.TryGetValue(reemplazoPublicId, out var r) ? r : null;

    /// <summary>El texto de <c>fiscalCase</c> (mensajes.md §6.9).</summary>
    public static string Texto(CasoFiscalDeAnulacion caso) => caso.ToString();
}
