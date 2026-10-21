namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Una regla de tasa negociada vigente (feature 014, US6, T168; data-model §2.4; FR-127, R13): su identidad, la prioridad de desempate, los criterios
/// combinables —antigüedad del asociado, rango de monto, de plazo, de tasa de la tabla y de saldo total del titular en captaciones y aportes, oficina y
/// canal; nulo = no restringe— y el margen máximo sobre la tabla en puntos efectivos anuales (fracción). (nuevo)
/// </summary>
public sealed record ReglaDeTasaNegociada(
    int Id,
    string Codigo,
    int Prioridad,
    int? AntiguedadMinimaMeses,
    decimal? MontoMinimo,
    decimal? MontoMaximo,
    int? PlazoMinimoDias,
    int? PlazoMaximoDias,
    decimal? TasaBaseMinima,
    decimal? TasaBaseMaxima,
    decimal? SaldoDelTitularMinimo,
    decimal? SaldoDelTitularMaximo,
    int? OficinaId,
    string? Canal,
    decimal MargenMaximo);

/// <summary>Lo que se pide negociar: el perfil del titular y de la operación y el margen pedido (fracción, puntos EA). (nuevo)</summary>
public sealed record PedidoDeTasaNegociada(
    int AntiguedadMeses,
    decimal Monto,
    int? PlazoDias,
    decimal TasaDeTabla,
    decimal SaldoDelTitular,
    int OficinaId,
    string? Canal,
    decimal MargenPedido);

/// <summary>
/// La evaluación de una tasa negociada (US6-6): la regla aplicada (la más específica), el margen máximo que admite, la tasa aplicada (tabla + margen), si el
/// margen cabe en la regla y si cabe en la atribución de quien lo pide —si no, espera a un aprobador con atribución suficiente—, el código del rechazo y la
/// explicación. (nuevo)
/// </summary>
public sealed record EvaluacionDeTasaNegociada(
    ReglaDeTasaNegociada? Regla,
    decimal? MargenMaximo,
    decimal TasaAplicada,
    bool CabeEnLaRegla,
    bool CabeEnLaAtribucion,
    string? CodigoDeRechazo,
    string Explicacion)
{
    /// <summary>¿Se aplica sola, sin aprobador? (cabe en la regla y en la atribución de quien la pide).</summary>
    public bool SeAplicaSola => CabeEnLaRegla && CabeEnLaAtribucion;

    /// <summary>¿Espera a un aprobador? (cabe en la regla pero no en la atribución).</summary>
    public bool EsperaAprobador => CabeEnLaRegla && !CabeEnLaAtribucion;
}

/// <summary>
/// El motor de la tasa negociada (feature 014, US6, T168; research R13; FR-127, D34). Puro: recibe las reglas vigentes de la línea y el pedido. La regla
/// aplicable es la <b>más específica</b>: todas sus condiciones no nulas casan con el pedido y, entre las que casan, gana la que más condiciones fija; a igual
/// número, la de menor <c>Prioridad</c> y después la de menor código. El margen nunca supera el máximo de la regla
/// (<c>Lending.NegotiatedRate.MarginAboveRule</c>); sin regla que case no se negocia (<c>Lending.NegotiatedRate.NoApplicableRule</c>). Dentro de la
/// atribución de quien lo pide (el mayor margen que puede conceder, por la matriz) se aplica sola; por encima, espera. (nuevo)
/// </summary>
public static class TasaNegociada
{
    public const string CodigoMargenSobreLaRegla = "Lending.NegotiatedRate.MarginAboveRule";
    public const string CodigoSinRegla = "Lending.NegotiatedRate.NoApplicableRule";
    public const string CodigoMargenInvalido = "Lending.NegotiatedRate.MarginInvalid";

    /// <summary>¿Casan todas las condiciones no nulas de la regla con el pedido?</summary>
    public static bool Casa(ReglaDeTasaNegociada r, PedidoDeTasaNegociada p)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(p);
        if (r.AntiguedadMinimaMeses is { } a && p.AntiguedadMeses < a) return false;
        if (r.MontoMinimo is { } mmin && p.Monto < mmin) return false;
        if (r.MontoMaximo is { } mmax && p.Monto > mmax) return false;
        if (r.PlazoMinimoDias is { } pmin && (p.PlazoDias is not { } d1 || d1 < pmin)) return false;
        if (r.PlazoMaximoDias is { } pmax && (p.PlazoDias is not { } d2 || d2 > pmax)) return false;
        if (r.TasaBaseMinima is { } tmin && p.TasaDeTabla < tmin) return false;
        if (r.TasaBaseMaxima is { } tmax && p.TasaDeTabla > tmax) return false;
        if (r.SaldoDelTitularMinimo is { } smin && p.SaldoDelTitular < smin) return false;
        if (r.SaldoDelTitularMaximo is { } smax && p.SaldoDelTitular > smax) return false;
        if (r.OficinaId is { } o && p.OficinaId != o) return false;
        if (!string.IsNullOrWhiteSpace(r.Canal) && !string.Equals(r.Canal.Trim(), p.Canal?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Cuántas condiciones fija la regla (un rango cuenta cada extremo): su especificidad.</summary>
    public static int Especificidad(ReglaDeTasaNegociada r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var n = 0;
        if (r.AntiguedadMinimaMeses is not null) n++;
        if (r.MontoMinimo is not null) n++;
        if (r.MontoMaximo is not null) n++;
        if (r.PlazoMinimoDias is not null) n++;
        if (r.PlazoMaximoDias is not null) n++;
        if (r.TasaBaseMinima is not null) n++;
        if (r.TasaBaseMaxima is not null) n++;
        if (r.SaldoDelTitularMinimo is not null) n++;
        if (r.SaldoDelTitularMaximo is not null) n++;
        if (r.OficinaId is not null) n++;
        if (!string.IsNullOrWhiteSpace(r.Canal)) n++;
        return n;
    }

    /// <summary>La regla aplicable (ver la clase); nula si ninguna casa.</summary>
    public static ReglaDeTasaNegociada? ReglaAplicable(IEnumerable<ReglaDeTasaNegociada> reglas, PedidoDeTasaNegociada pedido)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        return reglas.Where(r => Casa(r, pedido))
            .OrderByDescending(Especificidad)
            .ThenBy(r => r.Prioridad)
            .ThenBy(r => r.Codigo, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Evalúa el pedido (US6-6): con un margen de cero o menos no hay negociación (se rechaza con <c>MarginInvalid</c>); sin regla, <c>NoApplicableRule</c>;
    /// sobre el máximo de la regla, <c>MarginAboveRule</c> con el máximo; si no, cabe en la regla y cabe en la atribución si
    /// <paramref name="atribucionDeQuienPide"/> (el mayor margen que puede conceder; nulo = ninguno) lo alcanza.
    /// </summary>
    public static EvaluacionDeTasaNegociada Evaluar(IEnumerable<ReglaDeTasaNegociada> reglas, PedidoDeTasaNegociada pedido, decimal? atribucionDeQuienPide)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.MargenPedido <= 0m)
            return new EvaluacionDeTasaNegociada(null, null, pedido.TasaDeTabla, false, false, CodigoMargenInvalido,
                "El margen negociado es mayor que cero: sin margen rige la tasa de la tabla.");
        var regla = ReglaAplicable(reglas, pedido);
        if (regla is null)
            return new EvaluacionDeTasaNegociada(null, null, pedido.TasaDeTabla, false, false, CodigoSinRegla,
                "Ninguna regla de tasa negociada de la línea casa con el titular y la operación: rige la tasa de la tabla.");
        var aplicada = pedido.TasaDeTabla + pedido.MargenPedido;
        if (pedido.MargenPedido > regla.MargenMaximo)
            return new EvaluacionDeTasaNegociada(regla, regla.MargenMaximo, aplicada, false, false, CodigoMargenSobreLaRegla,
                $"La regla {regla.Codigo} admite hasta {regla.MargenMaximo:P4} sobre la tabla; se pidió {pedido.MargenPedido:P4}.");
        var cabe = atribucionDeQuienPide is { } a && pedido.MargenPedido <= a;
        return new EvaluacionDeTasaNegociada(regla, regla.MargenMaximo, aplicada, true, cabe, null,
            cabe
                ? $"Regla {regla.Codigo}: {pedido.MargenPedido:P4} sobre la tabla, dentro de la atribución de quien lo pide."
                : $"Regla {regla.Codigo}: {pedido.MargenPedido:P4} sobre la tabla supera la atribución de quien lo pide "
                  + $"({(atribucionDeQuienPide is { } x ? x.ToString("P4", System.Globalization.CultureInfo.InvariantCulture) : "ninguna")}): espera a un aprobador.");
    }
}
