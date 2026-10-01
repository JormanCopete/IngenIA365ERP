namespace IngenIA365ERP.Domain.Inventory.Counts;

/// <summary>
/// Las tolerancias de reconteo vigentes para la bodega del conteo (<c>Conteo.ToleranciaReconteoPorcentaje</c> y
/// <c>Conteo.ToleranciaReconteoUnidades</c>, feature 012, US11). Una diferencia se <b>tolera</b> si cabe en cualquiera de
/// las dos —hasta <see cref="Unidades"/> en valor absoluto, o hasta <see cref="Porcentaje"/> % del teórico—; con las dos en
/// cero (el defecto) toda diferencia exige reconteo. (nuevo)
/// </summary>
public sealed record ToleranciaDeReconteo(decimal Porcentaje, decimal Unidades)
{
    /// <summary>Sin tolerancia: toda diferencia distinta de cero exige reconteo.</summary>
    public static ToleranciaDeReconteo Ninguna { get; } = new(0m, 0m);
}

/// <summary>Una línea de la foto: su clave (la de la aplicación) y la existencia teórica congelada al abrir. (nuevo)</summary>
public sealed record LineaDeFoto<TClave>(TClave Linea, decimal Teorico) where TClave : notnull;

/// <summary>Una tanda de lecturas de un contador sobre una línea en una ronda (cantidad en unidad base; negativa corrige). (nuevo)</summary>
public sealed record CapturaDeConteo<TClave>(TClave Linea, byte Ronda, int Contador, decimal Cantidad) where TClave : notnull;

/// <summary>Un movimiento de la línea posterior a la foto (entrada positiva, salida negativa), en unidad base. (nuevo)</summary>
public sealed record MovimientoPosteriorALaFoto<TClave>(TClave Linea, decimal Cantidad) where TClave : notnull;

/// <summary>Lo que se compara. <paramref name="SumaMovimientos"/> = el conteo admitía movimientos (<c>Conteo.BloquearMovimientos = false</c>). (nuevo)</summary>
public sealed record PedidoDeComparacion<TClave>(
    IReadOnlyList<LineaDeFoto<TClave>> Foto,
    IReadOnlyList<CapturaDeConteo<TClave>> Capturas,
    IReadOnlyList<MovimientoPosteriorALaFoto<TClave>> Movimientos,
    ToleranciaDeReconteo Tolerancia,
    bool SumaMovimientos) where TClave : notnull;

/// <summary>
/// El resultado de una línea: el teórico con que se compara (foto + movimientos posteriores si se admitían), lo contado en
/// cada ronda, la ronda que manda (la última con capturas; 0 si nadie la contó), lo contado y la diferencia en esa ronda, y si
/// falta el reconteo. (nuevo)
/// </summary>
public sealed record ComparacionDeLinea<TClave>(
    TClave Linea,
    decimal Foto,
    decimal? MovimientosPosteriores,
    decimal Teorico,
    IReadOnlyDictionary<byte, decimal> ContadoPorRonda,
    byte RondaQueManda,
    decimal Contado,
    decimal Diferencia,
    bool RequiereReconteo) where TClave : notnull
{
    public bool TieneDiferencia => Diferencia != 0m;
}

/// <summary>
/// El motor puro de la comparación de un conteo físico (feature 012, US11, T390; FR-040; data-model §8.2): sin IO, sin EF y sin
/// valores fijos —las tolerancias llegan por parámetro—. Lo usan la captura (qué líneas quedan para la ronda siguiente), el
/// cierre (contado y diferencia de cada línea) y las consultas. (nuevo)
/// <list type="bullet">
/// <item>lo contado de una línea en una ronda es la suma de las tandas de <b>todos</b> los contadores en esa ronda (se reparten
/// ubicaciones; un segundo conteo independiente es una ronda nueva); una tanda negativa corrige;</item>
/// <item>manda la <b>última</b> ronda con capturas; una línea de la foto que nadie contó cuenta 0 (faltó todo);</item>
/// <item>con movimientos admitidos, el teórico es la foto más lo movido después de ella;</item>
/// <item>una diferencia fuera de tolerancia en la primera ronda exige reconteo; después del reconteo (ronda
/// <see cref="RondaDeReconteo"/> o posterior) ya no: vale lo que se recontó.</item>
/// </list>
/// Un producto encontrado fuera de la foto llega como una línea más con teórico 0.
/// </summary>
public static class ComparacionDeConteo
{
    /// <summary>La primera ronda (el conteo).</summary>
    public const byte PrimeraRonda = 1;

    /// <summary>La ronda del reconteo: después de ella no se pide otro.</summary>
    public const byte RondaDeReconteo = 2;

    /// <summary>Compara cada línea de la foto con sus capturas.</summary>
    public static IReadOnlyList<ComparacionDeLinea<TClave>> Comparar<TClave>(PedidoDeComparacion<TClave> pedido) where TClave : notnull
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var capturas = pedido.Capturas.ToLookup(c => c.Linea);
        var movimientos = pedido.Movimientos.ToLookup(m => m.Linea);

        return pedido.Foto.Select(linea =>
        {
            decimal? posteriores = pedido.SumaMovimientos ? movimientos[linea.Linea].Sum(m => m.Cantidad) : null;
            var teorico = linea.Teorico + (posteriores ?? 0m);
            var porRonda = capturas[linea.Linea]
                .GroupBy(c => c.Ronda)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Cantidad));
            var ronda = porRonda.Count == 0 ? (byte)0 : porRonda.Keys.Max();
            var contado = ronda == 0 ? 0m : porRonda[ronda];
            var diferencia = contado - teorico;
            var reconteo = ronda < RondaDeReconteo && FueraDeTolerancia(teorico, diferencia, pedido.Tolerancia);
            return new ComparacionDeLinea<TClave>(linea.Linea, linea.Teorico, posteriores, teorico, porRonda, ronda, contado, diferencia, reconteo);
        }).ToList();
    }

    /// <summary>
    /// ¿La diferencia exige reconteo? Sólo si no cabe en ninguna de las dos tolerancias: ni en las unidades ni en el porcentaje
    /// del teórico. Una diferencia cero nunca.
    /// </summary>
    public static bool FueraDeTolerancia(decimal teorico, decimal diferencia, ToleranciaDeReconteo tolerancia)
    {
        ArgumentNullException.ThrowIfNull(tolerancia);
        var absoluta = Math.Abs(diferencia);
        if (absoluta == 0m) return false;
        var porUnidades = absoluta <= Math.Max(0m, tolerancia.Unidades);
        var porPorcentaje = absoluta <= Math.Abs(teorico) * Math.Max(0m, tolerancia.Porcentaje) / 100m;
        return !(porUnidades || porPorcentaje);
    }
}
