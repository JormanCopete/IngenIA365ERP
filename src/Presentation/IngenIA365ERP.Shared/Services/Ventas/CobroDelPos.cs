namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// El panel de cobro del POS y de la factura de oficina (feature 012, I3, T639; FR-056, FR-097, contracts/api.md §22.4): la tecla rápida de
/// un medio agrega un pago por lo que falta; muestra lo que falta, lo que sobra y las vueltas. El efectivo recibido de más son vueltas
/// (<see cref="PagoEnCobro.Entregado"/>), nunca un pago de más. De una tarjeta viajan el datáfono (el de la caja por defecto), la
/// autorización y los últimos cuatro dígitos: nunca el número (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>). La validación definitiva
/// (bono único, crédito, disponibilidad) es del servidor; aquí sólo se evita mandar lo que seguro rechaza. (nuevo)
/// </summary>
public sealed class CobroDelPos(decimal aPagar)
{
    private readonly List<PagoEnCobro> _pagos = [];

    public decimal APagar { get; } = aPagar;

    public IReadOnlyList<PagoEnCobro> Pagos => _pagos;

    public decimal Pagado => _pagos.Sum(p => p.Monto);

    public decimal Falta => Math.Max(0m, APagar - Pagado);

    /// <summary>Lo pagado de más en medios que no dan vueltas: no deja cobrar.</summary>
    public decimal Sobra => Math.Max(0m, Pagado - APagar);

    /// <summary>Lo que se devuelve: lo entregado de más en los medios que dan vueltas.</summary>
    public decimal Vueltas => _pagos.Where(p => p.Medio.AllowsChange && p.Entregado is { } e && e > p.Monto).Sum(p => p.Entregado!.Value - p.Monto);

    /// <summary>Agrega un pago de <paramref name="medio"/> por lo que falta; si no falta nada, no agrega.</summary>
    public PagoEnCobro? AgregarPorLoQueFalta(MedioDelPosDto medio)
    {
        if (Falta <= 0m) return null;
        var pago = new PagoEnCobro(medio)
        {
            Monto = Falta,
            Datafono = medio.DefaultCardTerminalPublicId ?? (medio.CardTerminals.Count == 1 ? medio.CardTerminals[0].PublicId : null),
            Cuotas = medio.CreditDefaults?.Installments,
            PlazoDias = medio.CreditDefaults?.TermDays,
            PeriodicidadDias = medio.CreditDefaults?.PeriodicityDays,
            LineaSugerida = medio.CreditDefaults?.SuggestedLineCode,
        };
        _pagos.Add(pago);
        return pago;
    }

    public void Quitar(PagoEnCobro pago) => _pagos.Remove(pago);

    public void Limpiar() => _pagos.Clear();

    /// <summary>Lo que impide cobrar, o nulo si se puede.</summary>
    public string? Problema()
    {
        if (_pagos.Count == 0 || Falta > 0m) return $"Faltan {Falta:N0} por pagar.";
        if (Sobra > 0m) return $"Sobran {Sobra:N0}: ajuste los pagos (las vueltas van en «Entregado» del efectivo).";
        foreach (var p in _pagos)
        {
            if (p.Monto <= 0m) return $"El pago con {p.Medio.Name} no tiene valor.";
            if (p.Medio.RequiresReference && string.IsNullOrWhiteSpace(p.Referencia))
                return $"{p.Medio.Name} ({p.Medio.Code}) exige {TextosDeVentas.TipoDeReferencia(p.Medio.ReferenceKind).ToLowerInvariant()}.";
            if (!string.IsNullOrEmpty(p.Last4) && !Last4Valido(p.Last4))
                return "De la tarjeta sólo se digitan los últimos cuatro dígitos.";
            if (p.Entregado is { } e && e < p.Monto) return $"Lo entregado en {p.Medio.Name} es menor que el pago.";
            if (TextosDeVentas.EsCredito(p.Medio.Class) && (p.Cuotas is null or <= 0 || p.PlazoDias is null or <= 0 || p.PeriodicidadDias is null or <= 0))
                return $"El crédito con {p.Medio.Name} necesita cuotas, plazo y periodicidad.";
        }
        return null;
    }

    /// <summary>Los pagos como los recibe la API; los que se arquean llevan la <paramref name="sesion"/> de caja.</summary>
    public IReadOnlyList<PagoRequest> Pedido(Guid? sesion) => _pagos.Select(p => new PagoRequest(
        p.Medio.PaymentMeansPublicId,
        p.Monto,
        Tendered: p.Medio.AllowsChange ? p.Entregado : null,
        Reference: Texto(p.Referencia),
        AuthorizationCode: Texto(p.Autorizacion),
        CardTerminalPublicId: TextosDeVentas.EsTarjeta(p.Medio.Class) ? p.Datafono : null,
        BatchNumber: Texto(p.Lote),
        Last4: p.Last4 is { } l && Last4Valido(l) ? l : null,
        CashSessionPublicId: sesion,
        Credit: TextosDeVentas.EsCredito(p.Medio.Class) && p.Cuotas is { } c && p.PlazoDias is { } plazo && p.PeriodicidadDias is { } per
            ? new CreditoDelPagoRequest(c, plazo, per, p.PrimerVencimiento, Texto(p.LineaSugerida))
            : null)).ToList();

    public static bool Last4Valido(string? s) => s is { Length: 4 } && s.All(char.IsAsciiDigit);

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Un pago que se está armando en el panel de cobro.</summary>
public sealed class PagoEnCobro(MedioDelPosDto medio)
{
    public MedioDelPosDto Medio { get; } = medio;
    public decimal Monto { get; set; }
    public decimal? Entregado { get; set; }
    public string? Referencia { get; set; }
    public string? Autorizacion { get; set; }
    public Guid? Datafono { get; set; }
    public string? Lote { get; set; }

    /// <summary>Los últimos cuatro dígitos de la tarjeta, nada más.</summary>
    public string? Last4 { get; set; }

    public short? Cuotas { get; set; }
    public short? PlazoDias { get; set; }
    public short? PeriodicidadDias { get; set; }
    public DateOnly? PrimerVencimiento { get; set; }
    public string? LineaSugerida { get; set; }
}
