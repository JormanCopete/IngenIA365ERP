using static IngenIA365ERP.Shared.Services.FacturacionElectronica.TextosDeFacturacionElectronica;

namespace IngenIA365ERP.Shared.Services.FacturacionElectronica;

/// <summary>
/// Qué acciones ofrece la pantalla para un documento electrónico según su estado (feature 012, I4, T755, T758; contracts/dian.md §5.1,
/// §8; api.md §24.4, §24.5). El servidor decide siempre —una acción que no procede responde su 422 (<c>AwaitingResponse</c>,
/// <c>Final</c>, <c>NotRejected</c>, <c>RejectionNotConfirmed</c>)—; esto sólo evita ofrecerla. Y traduce las rutas que devuelve el servidor
/// (la de API del borrador de reemplazo, la del documento comercial) a la pantalla que las abre. (nuevo)
/// </summary>
public static class AccionesDelDocumentoElectronico
{
    private const int OperacionConsulta = 3;
    private const int ResultadoRechazado = 3, ResultadoNoEncontrado = 5, ResultadoDatosInvalidos = 8;

    /// <summary>«Reintentar ahora»: sólo pendiente o en contingencia (en <c>Sent</c> se consulta; un final no se reintenta).</summary>
    public static bool PuedeReintentar(int estado) =>
        estado is EstadoPendiente or EstadoContingenciaDelFacturador or EstadoContingenciaDeLaDian;

    /// <summary>«Consultar a la DIAN»: mientras no haya respuesta definitiva y, en un rechazado, para confirmarlo.</summary>
    public static bool PuedeConsultar(int estado) => estado is EstadoPendiente or EstadoEnviado or EstadoRechazado;

    /// <summary>Se entrega al comprador validado o en contingencia (<c>EntregaAlComprador.Entregable</c>).</summary>
    public static bool Entregable(int estado) =>
        estado is EstadoValidado or EstadoValidadoConNotificaciones or EstadoContingenciaDelFacturador or EstadoContingenciaDeLaDian;

    /// <summary>El caso a (corregir sin cambio económico) sólo sobre un rechazado.</summary>
    public static bool PuedeCorregirCasoA(int estado) => estado == EstadoRechazado;

    /// <summary>Los casos b y c exigen además el rechazo confirmado.</summary>
    public static bool PuedeReemplazarOCancelar(int estado, bool rechazoConfirmado) => estado == EstadoRechazado && rechazoConfirmado;

    /// <summary>Cambiar al canal vigente: sólo lo que sigue por transmitir (el servidor exige además el canal retirado y la resolución asociada).</summary>
    public static bool PuedeTransmitirPorElCanalVigente(int estado) =>
        estado is EstadoPendiente or EstadoRechazado or EstadoContingenciaDelFacturador or EstadoContingenciaDeLaDian;

    /// <summary>
    /// ¿El rechazo está confirmado? Sí si, después del último envío (emisión o transmisión desde contingencia), una consulta de estado
    /// respondió rechazado, datos inválidos o no encontrado (<c>CasosDeRechazo.RechazoConfirmadoAsync</c>).
    /// </summary>
    /// <param name="intentos">Las transmisiones en orden de intento: (operación, resultado).</param>
    public static bool RechazoConfirmado(int estado, IEnumerable<(int Operacion, int Resultado)> intentos)
    {
        if (estado != EstadoRechazado) return false;
        var lista = intentos.ToList();
        var ultimoEnvio = lista.FindLastIndex(t => t.Operacion != OperacionConsulta);
        return lista.Skip(ultimoEnvio + 1).Any(t => t.Operacion == OperacionConsulta
            && t.Resultado is ResultadoRechazado or ResultadoDatosInvalidos or ResultadoNoEncontrado);
    }

    /// <summary>
    /// La pantalla que edita el borrador de reemplazo del caso b, a partir de la ruta de API que devuelve el servidor
    /// (<c>/api/inventory/sales/invoices/{id}</c>, <c>/sales/credit-notes/{id}</c>, <c>/purchases/support-documents/{id}</c>); nula si no se
    /// conoce.
    /// </summary>
    public static string? PantallaDelBorrador(string? rutaDeApi)
    {
        if (string.IsNullOrWhiteSpace(rutaDeApi)) return null;
        var partes = rutaDeApi.TrimEnd('/').Split('/');
        if (partes.Length == 0 || !Guid.TryParse(partes[^1], out var id)) return null;
        var sinId = string.Join('/', partes[..^1]);
        return sinId switch
        {
            "/api/inventory/sales/invoices" => $"/ventas/facturas/nueva?documento={id}",
            "/api/inventory/sales/credit-notes" => $"/ventas/notas-credito/nueva?documento={id}",
            "/api/inventory/purchases/support-documents" => $"/compras/documentos-soporte/{id}",
            _ => null,
        };
    }

    /// <summary>La pantalla del documento comercial: la del documento soporte llega como <c>?documento=</c> y se abre en su detalle.</summary>
    public static string? PantallaDelOrigen(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return null;
        const string soporte = "/compras/documentos-soporte?documento=";
        return ruta.StartsWith(soporte, StringComparison.Ordinal) && Guid.TryParse(ruta[soporte.Length..], out var id)
            ? $"/compras/documentos-soporte/{id}"
            : ruta;
    }
}
