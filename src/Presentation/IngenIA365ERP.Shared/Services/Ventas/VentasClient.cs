using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Cliente tipado de Ventas (feature 012, I3, T633; contracts/api.md §18–§21), en el molde de <see cref="InventarioClient"/> y
/// <c>ComprasClient</c>. Es <c>partial</c>: la base (este archivo) pone las rutas y el envío común; <c>.Pos</c> suma los puntos, las cajas
/// y la venta en el POS; <c>.Caja</c> las sesiones, los movimientos y el cierre del día; <c>.Precios</c> las listas, la resolución y los
/// topes; <c>.Documentos</c> la consulta, la entrega y las facturas y notas de oficina. (nuevo)
/// <list type="bullet">
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>).</item>
/// <item>Toda escritura lleva la <c>Idempotency-Key</c> de la operación de pantalla (<see cref="ClaveDeOperacion"/>): un cobro reintentado
/// con el mismo contenido conserva la clave y el servidor devuelve el mismo resultado, nunca una segunda venta (SC-002).</item>
/// <item>Los enums viajan por nombre (<see cref="TextosDeVentas"/>).</item>
/// </list>
/// </summary>
public sealed partial class VentasClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/inventory";

    /// <summary>Las rutas de I3.</summary>
    public static class Rutas
    {
        public const string Puntos = Base + "/points-of-sale";
        public const string Pos = Base + "/pos";
        public const string Sesiones = Base + "/cash-sessions";
        public const string Movimientos = Base + "/cash-movements";
        public const string CierresDelDia = Base + "/day-closes";
        public const string Listas = Base + "/price-lists";
        public const string Precios = Base + "/prices";
        public const string Topes = Base + "/discount-caps";
        public const string Documentos = Base + "/sales/documents";
        public const string Facturas = Base + "/sales/invoices";
        public const string Notas = Base + "/sales/credit-notes";
        public const string EvaluacionesDeCredito = Base + "/sales/credit-evaluations";
        public const string Reimpresion = Base + "/documents";

        // I6 (T893; §18.4, §19.4): el ciclo comercial y las promociones.
        public const string Cotizaciones = Base + "/sales/quotes";
        public const string Pedidos = Base + "/sales/orders";
        public const string Remisiones = Base + "/sales/shipments";
        public const string NotasDebito = Base + "/sales/debit-notes";
        public const string Promociones = Base + "/promotions";
    }

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);

    private Task<InvitationApiResult<ArchivoDescargado>> Descargar(string url, string nombrePorDefecto, CancellationToken ct) =>
        EnvioDeVentas.DescargarAsync(http, auth, url, nombrePorDefecto, ct);

    private static string Q(string ruta, params (string Nombre, string? Valor)[] pares) => EnvioDeVentas.ConQuery(ruta, EnvioDeVentas.Query(pares));
}
