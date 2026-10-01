using System.Globalization;
using System.Text;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Domain.Inventory.Purchasing;

/// <summary>
/// Cómo se combinan la tolerancia en porcentaje y la tolerancia en valor (<c>Compras.ReglaDeTolerancia</c>; FR-050, pregunta E5).
/// No se guarda: se lee del parámetro. (nuevo)
/// </summary>
public enum ReglaDeTolerancia
{
    /// <summary>La diferencia tiene que caber en las dos: manda la más estricta (el defecto).</summary>
    AmbasCondiciones = 1,

    /// <summary>Basta con que quepa en una: manda la más amplia.</summary>
    CualquieraDeLas = 2,
}

/// <summary>
/// Las tolerancias del cruce vigentes a la fecha de la operación (<c>Compras.ToleranciaCantidadPorcentaje</c>,
/// <c>…CantidadValor</c>, <c>…PrecioPorcentaje</c>, <c>…PrecioValor</c> y <c>Compras.ReglaDeTolerancia</c>; data-model §4.2). Los
/// porcentajes son fracciones (0,01 = 1 %); el valor de cantidad va en unidad base y el de precio en pesos por unidad. Una
/// tolerancia en cero no participa; con todas en cero (el defecto) no se tolera ninguna diferencia. (nuevo)
/// </summary>
public sealed record ToleranciasDelCruce(
    decimal CantidadPorcentaje,
    decimal CantidadValor,
    decimal PrecioPorcentaje,
    decimal PrecioValor,
    ReglaDeTolerancia Regla = ReglaDeTolerancia.AmbasCondiciones)
{
    /// <summary>El defecto seguro de los parámetros: ninguna diferencia se tolera.</summary>
    public static ToleranciasDelCruce Ninguna { get; } = new(0m, 0m, 0m, 0m);

    /// <summary>
    /// Los valores que se usaron, para <c>INV_PurchaseMatchLines.ToleranceJson</c> (FR-050, SC-007): un objeto con las cinco
    /// claves del parámetro en su orden, los números en cultura invariante y sin ceros de relleno.
    /// </summary>
    public string ComoJson()
    {
        var json = new StringBuilder("{");
        Numero(json, ParametrosDeInventario.ComprasToleranciaCantidadPorcentaje, CantidadPorcentaje).Append(',');
        Numero(json, ParametrosDeInventario.ComprasToleranciaCantidadValor, CantidadValor).Append(',');
        Numero(json, ParametrosDeInventario.ComprasToleranciaPrecioPorcentaje, PrecioPorcentaje).Append(',');
        Numero(json, ParametrosDeInventario.ComprasToleranciaPrecioValor, PrecioValor).Append(',');
        json.Append('"').Append(ParametrosDeInventario.ComprasReglaDeTolerancia).Append("\":\"").Append(Regla).Append('"');
        return json.Append('}').ToString();
    }

    private static StringBuilder Numero(StringBuilder json, string clave, decimal valor) =>
        json.Append('"').Append(clave).Append("\":").Append(valor.ToString("0.############################", CultureInfo.InvariantCulture));
}

/// <summary>
/// Una línea de la factura del proveedor frente a lo que ordenó y recibió (todo en unidad base): lo ordenado en la línea de orden
/// (nulo = sin orden, dos vías), lo recibido y todavía no facturado en las recepciones enlazadas, lo facturado, y los precios
/// unitarios de la orden, de la recepción (su costo neto) y de la factura. (nuevo)
/// </summary>
public sealed record LineaAlCruce(
    decimal? Ordenado,
    decimal RecibidoNoFacturado,
    decimal Facturado,
    decimal? PrecioOrdenado,
    decimal? CostoRecibido,
    decimal PrecioFacturado);

/// <summary>
/// El resultado del cruce de una línea (data-model §9.6): diferencias, si excede la tolerancia y por qué, si esa excepción se puede
/// aprobar, la tolerancia de precio usada (pesos por unidad), los valores de tolerancia en JSON y la explicación. (nuevo)
/// </summary>
public sealed record ResultadoDelCruce(
    bool ConOrden,
    decimal QuantityDifference,
    decimal PriceDifferenceAmount,
    decimal PriceDifferenceRate,
    bool ExceedsTolerance,
    IReadOnlyList<string> Razones,
    bool FacturaSobreLoRecibido,
    bool Aprobable,
    decimal ToleranciaDePrecio,
    string ToleranceJson,
    ExplicacionDeCosto Explicacion)
{
    /// <summary>La columna <c>Reasons</c>: las razones separadas por coma, o nulo si no excede.</summary>
    public string? Reasons => Razones.Count == 0 ? null : string.Join(',', Razones);

    /// <summary>El estado con que nace la fila: <c>Held</c> si excede; nulo si no.</summary>
    public PurchaseMatchStatus? Status => ExceedsTolerance ? PurchaseMatchStatus.Held : null;
}

/// <summary>
/// Una recepción contra una línea de orden (FR-049; data-model §5.5): lo ordenado, lo ya recibido por otras recepciones vivas
/// (<c>PendingApproval</c> o <c>Confirmed</c>, no anuladas) y lo que trae ésta, en unidad base, con las tolerancias vigentes a la
/// fecha de operación. (nuevo)
/// </summary>
public sealed record PedidoDeRecepcionContraOrden(decimal Ordenado, decimal YaRecibido, decimal EstaRecepcion, ToleranciasDelCruce Tolerancias);

/// <summary>
/// Si la recepción cabe (<see cref="Codigo"/> nulo) o se rechaza con <c>Inventory.Purchase.OverReceiptBeyondTolerance</c>: Σ lo
/// recibido con ésta, lo recibido de más, la tolerancia de cantidad según la regla y lo pendiente antes de ésta (los datos del
/// error, api.md §14.9: <c>{ lineNumber, ordered, received, tolerance }</c>). (nuevo)
/// </summary>
public sealed record ResultadoDeRecepcionContraOrden(
    bool Admitida,
    decimal Recibido,
    decimal Excedente,
    decimal Tolerancia,
    decimal Pendiente,
    string? Codigo,
    ExplicacionDeCosto Explicacion);

/// <summary>
/// El cruce a tres vías de las compras (feature 012, US13, T779 y T782; FR-049, FR-050; data-model §9.6; research §compras):
/// puro, sin IO ni valores legales —las tolerancias llegan como parámetros vigentes—, molde de <c>ComparacionDeConteo</c>.
///
/// <para>
/// <b>Factura</b> (<see cref="Cruzar"/>), por línea:
/// <list type="bullet">
/// <item><b>Cantidad</b>: <c>QuantityDifference = facturado − recibido no facturado</c>. Si es positiva, se factura más de lo
/// recibido: con orden la línea queda retenida por <see cref="RazonCantidad"/> y esa razón <b>nunca</b> entra por tolerancia ni
/// se aprueba por excepción (data-model §9.6; decisión T796, <see cref="CodigoCantidadNoAprobable"/>): sólo sale rechazando la
/// factura o registrando otra recepción. Facturar menos que lo recibido es una factura parcial, no una diferencia. La tolerancia de
/// cantidad no participa aquí: es de la recepción contra la orden (<see cref="Recepcion"/>).</item>
/// <item><b>Precio</b>: contra el de la orden (o, sin él, el costo de la recepción). <c>PriceDifferenceRate = (facturado −
/// referencia) / referencia</c> (seis decimales, <c>Tarifa</c>) y <c>PriceDifferenceAmount = round((facturado − referencia) ×
/// cantidad facturada)</c>. Excede si la diferencia por unidad, en valor absoluto —más cara o más barata—, pasa la tolerancia de
/// precio permitida (<see cref="ToleranciaPermitida"/> sobre la referencia).</item>
/// <item><b>Sin orden</b> (dos vías, I1, E6): se calculan las diferencias pero nada se retiene; facturar más de lo recibido lo
/// rechaza antes la aplicación con <c>Inventory.Purchase.InvoiceExceedsReceived</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Recepción contra orden</b> (<see cref="Recepcion"/>, T782): Σ recibido por la línea de orden ≤ ordenado + tolerancia de
/// cantidad vigente.
/// </para>
///
/// <para>
/// <b>La regla</b> (pregunta E5): una tolerancia en cero no participa; con <see cref="ReglaDeTolerancia.AmbasCondiciones"/> la
/// permitida es la menor de las que participan, con <see cref="ReglaDeTolerancia.CualquieraDeLas"/> la mayor, y sin ninguna es cero.
/// </para>
/// </summary>
public static class CruceDeCompra
{
    /// <summary>Razón de la retención: se factura más de lo recibido.</summary>
    public const string RazonCantidad = "Quantity";

    /// <summary>Razón de la retención: el precio facturado sale de la tolerancia.</summary>
    public const string RazonPrecio = "Price";

    /// <summary>El código con que la aplicación publica lo recibido de más fuera de la tolerancia (api.md §14.9).</summary>
    public const string CodigoRecibidoDeMas = "Inventory.Purchase.OverReceiptBeyondTolerance";

    /// <summary>
    /// El código con que se rechaza aprobar una línea retenida por cantidad (decisión T796, nuevo): facturar más de lo recibido
    /// no se aprueba por excepción.
    /// </summary>
    public const string CodigoCantidadNoAprobable = "Inventory.PurchaseMatch.QuantityNotApprovable";

    /// <summary>Largo de <c>INV_PurchaseMatchLines.ToleranceJson</c>.</summary>
    public const int LargoMaximoDeToleranceJson = 1000;

    /// <summary>Decimales de una tarifa (<c>PrecisionDeInventario.Tarifa</c>, 9,6).</summary>
    public const int DecimalesDeTarifa = 6;

    /// <summary>El valor del parámetro <c>Compras.ReglaDeTolerancia</c>; cualquier otro es un error visible.</summary>
    public static ReglaDeTolerancia ReglaDesde(string valor) =>
        Enum.TryParse<ReglaDeTolerancia>(valor, ignoreCase: true, out var r) && Enum.IsDefined(r)
            ? r
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Compras.ReglaDeTolerancia admite «AmbasCondiciones» o «CualquieraDeLas».");

    /// <summary>
    /// La tolerancia permitida sobre <paramref name="referencia"/>: <c>referencia × porcentaje</c> y <paramref name="valor"/>,
    /// combinadas según la regla; una en cero no participa y sin ninguna es cero.
    /// </summary>
    public static decimal ToleranciaPermitida(decimal referencia, decimal porcentaje, decimal valor, ReglaDeTolerancia regla)
    {
        if (porcentaje < 0m) throw new ArgumentOutOfRangeException(nameof(porcentaje), porcentaje, "Una tolerancia no es negativa.");
        if (valor < 0m) throw new ArgumentOutOfRangeException(nameof(valor), valor, "Una tolerancia no es negativa.");

        var participan = new List<decimal>();
        if (porcentaje > 0m) participan.Add(Math.Abs(referencia) * porcentaje);
        if (valor > 0m) participan.Add(valor);
        if (participan.Count == 0) return 0m;
        return regla == ReglaDeTolerancia.CualquieraDeLas ? participan.Max() : participan.Min();
    }

    /// <summary>Cruza una línea de la factura del proveedor contra lo ordenado y lo recibido.</summary>
    public static ResultadoDelCruce Cruzar(LineaAlCruce linea, ToleranciasDelCruce tolerancias, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(tolerancias);
        if (linea.Facturado <= 0m) throw new ArgumentException("La cantidad facturada es positiva.", nameof(linea));
        if (linea.RecibidoNoFacturado < 0m) throw new ArgumentException("Lo recibido no facturado no es negativo.", nameof(linea));

        var conOrden = linea.Ordenado is not null;
        var explicacion = new ExplicacionDeCosto
        {
            Resumen = conOrden
                ? "Cruce a tres vías: lo ordenado, lo recibido y lo facturado, en cantidad y precio (FR-050)."
                : "Sin orden: cruce de dos vías contra la recepción; la diferencia sólo ajusta el costo (E6).",
        };
        if (linea.Ordenado is { } ordenado) explicacion.Paso("Ordenado", ordenado);
        explicacion.Paso("Recibido no facturado", linea.RecibidoNoFacturado).Paso("Facturado", linea.Facturado);

        var razones = new List<string>();

        // ---- cantidad ----
        var diferenciaDeCantidad = linea.Facturado - linea.RecibidoNoFacturado;
        var sobreLoRecibido = diferenciaDeCantidad > 0m;
        var cantidad = sobreLoRecibido ? diferenciaDeCantidad : 0m;
        if (sobreLoRecibido)
        {
            explicacion.Paso("Facturado de más sobre lo recibido", diferenciaDeCantidad)
                .Nota("Regla de cantidad", "Facturar más de lo recibido nunca entra por tolerancia ni se aprueba por excepción: se rechaza la factura o se registra otra recepción.");
            if (conOrden) razones.Add(RazonCantidad);
        }

        // ---- precio ----
        var (etiqueta, referencia) = linea.PrecioOrdenado is { } po && conOrden
            ? ("Precio de referencia (orden)", po)
            : ("Precio de referencia (recepción)", linea.CostoRecibido ?? linea.PrecioFacturado);
        var diferenciaUnitaria = linea.PrecioFacturado - referencia;
        var tasa = referencia == 0m ? 0m : Math.Round(diferenciaUnitaria / referencia, DecimalesDeTarifa, MidpointRounding.AwayFromZero);
        var monto = Redondeo.Monto(diferenciaUnitaria * linea.Facturado, montos);
        var toleranciaDePrecio = ToleranciaPermitida(referencia, tolerancias.PrecioPorcentaje, tolerancias.PrecioValor, tolerancias.Regla);
        explicacion.Paso(etiqueta, referencia).Paso("Precio facturado", linea.PrecioFacturado)
            .Paso("Diferencia por unidad", diferenciaUnitaria).Paso("Diferencia en tarifa", tasa).Paso("Diferencia total", monto)
            .Paso("Tolerancia de precio por unidad", toleranciaDePrecio)
            .Nota("Regla de tolerancia", tolerancias.Regla == ReglaDeTolerancia.CualquieraDeLas
                ? "CualquieraDeLas: manda la mayor de las tolerancias configuradas."
                : "AmbasCondiciones: manda la menor de las tolerancias configuradas.");
        if (conOrden && Math.Abs(diferenciaUnitaria) > toleranciaDePrecio) razones.Add(RazonPrecio);

        var excede = razones.Count > 0;
        var aprobable = !razones.Contains(RazonCantidad);
        explicacion.Nota("Resultado", !conOrden
            ? "Sin orden (dos vías): no se retiene."
            : excede ? $"Retenida por {string.Join(" y ", razones)}{(aprobable ? "; se aprueba por excepción." : "; no se aprueba por excepción.")}"
            : "Dentro de la tolerancia.");

        return new ResultadoDelCruce(conOrden, cantidad, monto, tasa, excede, razones, sobreLoRecibido, aprobable,
            toleranciaDePrecio, tolerancias.ComoJson(), explicacion);
    }

    /// <summary>
    /// La regla de lo recibido de más contra una línea de orden (T782; FR-049): Σ recibido ≤ ordenado + tolerancia de cantidad
    /// vigente (<see cref="ToleranciaPermitida"/> sobre lo ordenado).
    /// </summary>
    public static ResultadoDeRecepcionContraOrden Recepcion(PedidoDeRecepcionContraOrden pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Ordenado <= 0m) throw new ArgumentException("Lo ordenado es positivo.", nameof(pedido));
        if (pedido.EstaRecepcion <= 0m) throw new ArgumentException("Lo recibido es positivo.", nameof(pedido));
        if (pedido.YaRecibido < 0m) throw new ArgumentException("Lo ya recibido no es negativo.", nameof(pedido));

        var t = pedido.Tolerancias;
        var recibido = pedido.YaRecibido + pedido.EstaRecepcion;
        var excedente = Math.Max(0m, recibido - pedido.Ordenado);
        var pendiente = Math.Max(0m, pedido.Ordenado - pedido.YaRecibido);
        var tolerancia = ToleranciaPermitida(pedido.Ordenado, t.CantidadPorcentaje, t.CantidadValor, t.Regla);
        var admitida = excedente <= tolerancia;

        var explicacion = new ExplicacionDeCosto { Resumen = "Recepción contra la orden: lo recibido de más sólo entra dentro de la tolerancia (FR-049)." }
            .Paso("Ordenado", pedido.Ordenado).Paso("Ya recibido", pedido.YaRecibido).Paso("Esta recepción", pedido.EstaRecepcion)
            .Paso("Pendiente antes de esta recepción", pendiente).Paso("Recibido de más", excedente).Paso("Tolerancia de cantidad", tolerancia)
            .Nota("Resultado", admitida ? "Cabe." : "Recibido de más fuera de la tolerancia.");

        return new ResultadoDeRecepcionContraOrden(admitida, recibido, excedente, tolerancia, pendiente,
            admitida ? null : CodigoRecibidoDeMas, explicacion);
    }
}
