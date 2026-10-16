using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>
/// Un crédito de la persona que el certificado tributario mira: su número, su línea, si su segmento SARC es <c>VIVIENDA</c> según el catálogo de
/// modalidades vigente al 31 de diciembre y su estado reconstruido a ese día con R = la emisión (nulo si el crédito todavía no existía). (nuevo)
/// </summary>
public sealed record CreditoParaCertificar(string Numero, string Linea, bool EsVivienda, EstadoDelCredito? AlCierre);

/// <summary>
/// Lo que el certificado dice de un crédito: los intereses efectivamente pagados en el año —corrientes y de mora, incluidos los que estaban en
/// suspenso— y el saldo al 31 de diciembre de capital, de intereses y de los demás conceptos cobrables (seguros, gastos de cobranza, otros cargos),
/// menos el anticipo. (nuevo)
/// </summary>
public sealed record InteresesPagadosPorCredito(
    string Numero,
    string Linea,
    bool EsVivienda,
    decimal InteresCorrientePagado,
    decimal InteresDeMoraPagado,
    decimal SaldoDeCapital,
    decimal SaldoDeIntereses,
    decimal SaldoDeOtros)
{
    public decimal InteresesPagados => InteresCorrientePagado + InteresDeMoraPagado;

    public decimal SaldoTotal => SaldoDeCapital + SaldoDeIntereses + SaldoDeOtros;
}

/// <summary>El certificado armado: el año, los créditos (sin los de vivienda), los de vivienda aparte y su explicación. (nuevo)</summary>
public sealed record CertificadoTributarioArmado(
    short Año,
    IReadOnlyList<InteresesPagadosPorCredito> Creditos,
    IReadOnlyList<InteresesPagadosPorCredito> Vivienda,
    ExplicacionDeCartera Explicacion)
{
    public IEnumerable<InteresesPagadosPorCredito> Todos => Creditos.Concat(Vivienda);

    public decimal TotalInteresesPagados => Creditos.Sum(c => c.InteresesPagados);

    public decimal TotalInteresesDeVivienda => Vivienda.Sum(c => c.InteresesPagados);

    public decimal TotalSaldo => Todos.Sum(c => c.SaldoTotal);

    public bool Vacio => Creditos.Count == 0 && Vivienda.Count == 0;
}

/// <summary>
/// El certificado tributario del año (feature 013, US13, K5, T0970; adaptadores-y-archivos.md §8.6; FR-131; US13-3). Por cada crédito de la persona:
/// <list type="bullet">
/// <item><b>intereses efectivamente pagados</b> en el año: lo aplicado por el contrato con fecha valor en el año —filas <c>PaymentAllocation</c> y
/// <c>PaymentReallocation</c> del libro teórico sobre <c>CurrentInterest</c>, <c>DefaultInterest</c>, <c>CurrentInterestInSuspense</c> y
/// <c>DefaultInterestInSuspense</c>—, <b>neto de sus reversos conocidos a R</b>: la reconstrucción con R = la emisión ya no contiene lo reversado
/// (D4). Ésta es la definición (nueva) que valida la contadora con los casos dorados (quickstart §12);</item>
/// <item>el <b>saldo al 31 de diciembre</b> con R = la emisión: capital, intereses y lo demás cobrable, menos el anticipo.</item>
/// </list>
/// Los de segmento SARC <c>VIVIENDA</c> van en otra sección (vacía hasta K6b). Un crédito cancelado en el año aparece con saldo cero; uno que no
/// existía al 31 de diciembre (desembolsado después) no aparece, ni uno sin saldo ni movimientos en el año (cancelado antes). Motor puro. (nuevo)
/// </summary>
public static class CertificadoTributario
{
    /// <summary>Las clases de fila que son pago aplicado por el contrato.</summary>
    public static IReadOnlySet<LoanTransactionKind> ClasesDePago { get; } =
        new HashSet<LoanTransactionKind> { LoanTransactionKind.PaymentAllocation, LoanTransactionKind.PaymentReallocation };

    /// <summary>Los conceptos de interés corriente que cuentan como pagados.</summary>
    public static IReadOnlySet<LoanConcept> Corrientes { get; } =
        new HashSet<LoanConcept> { LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense };

    /// <summary>Los conceptos de interés de mora que cuentan como pagados.</summary>
    public static IReadOnlySet<LoanConcept> DeMora { get; } =
        new HashSet<LoanConcept> { LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense };

    private static readonly LoanConcept[] Otros = [LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge];

    public static CertificadoTributarioArmado Armar(short año, IEnumerable<CreditoParaCertificar> creditos)
    {
        ArgumentNullException.ThrowIfNull(creditos);
        var cierre = new DateOnly(año, 12, 31);
        var explicacion = new ExplicacionDeCartera()
            .Linea($"Intereses pagados en {año}: lo aplicado por el contrato con fecha valor entre el {año}-01-01 y el {año}-12-31, neto de los reversos conocidos a la emisión.")
            .Linea($"Saldo de la deuda al {ExplicacionDeCartera.Fecha(cierre)} con la información a la emisión.");
        var lista = new List<InteresesPagadosPorCredito>();
        foreach (var c in creditos)
        {
            var estado = c.AlCierre;
            if (estado is null || estado.Origen is not { } origen || origen > cierre) continue;
            if (estado.Fecha != cierre) throw new ArgumentException($"El crédito {c.Numero} debe venir reconstruido al {ExplicacionDeCartera.Fecha(cierre)}.", nameof(creditos));

            var delAño = estado.Libro.Movimientos.Where(m => m.Dia.Year == año).ToList();
            var pagos = delAño.Where(m => ClasesDePago.Contains(m.Clase)).ToList();
            var corriente = -pagos.Where(m => Corrientes.Contains(m.Concepto)).Sum(m => m.Monto);
            var mora = -pagos.Where(m => DeMora.Contains(m.Concepto)).Sum(m => m.Monto);
            var capital = estado.Saldo(LoanConcept.Principal);
            var intereses = Corrientes.Concat(DeMora).Sum(x => estado.Saldo(x));
            var otros = Otros.Sum(x => estado.Saldo(x)) - estado.Anticipo;
            var conMovimiento = delAño.Count > 0 || origen.Year == año;
            if (!conMovimiento && capital == 0m && intereses == 0m && otros == 0m) continue;

            lista.Add(new InteresesPagadosPorCredito(c.Numero, c.Linea, c.EsVivienda, corriente, mora, capital, intereses, otros));
            explicacion.Linea($"{c.Numero}: intereses corrientes {ExplicacionDeCartera.Numero(corriente)}, de mora {ExplicacionDeCartera.Numero(mora)}; " +
                              $"saldo {ExplicacionDeCartera.Numero(capital + intereses + otros)}{(c.EsVivienda ? " (vivienda)" : string.Empty)}.");
        }
        var ordenados = lista.OrderBy(c => c.Numero, StringComparer.Ordinal).ToList();
        return new CertificadoTributarioArmado(año, ordenados.Where(c => !c.EsVivienda).ToList(), ordenados.Where(c => c.EsVivienda).ToList(), explicacion);
    }
}
