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
/// suspenso, y los que cubrió el anticipo ingresado en el año— y el saldo al 31 de diciembre de capital, de intereses y de los demás conceptos
/// cobrables (seguros, gastos de cobranza, otros cargos), menos el anticipo, según <see cref="ConceptosDelSaldoCertificado"/>: un concepto que la
/// cooperativa no incluye va en cero, y el anticipo restado va en <see cref="SaldoDeOtros"/>. <see cref="InteresesConAnticipo"/> es la parte de los
/// intereses pagados que cubrió el anticipo (informativa). (nuevo)
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

    /// <summary>De <see cref="InteresesPagados"/>, lo que cubrió el anticipo ingresado en el año al aplicarse a una cuota (2026-10-16).</summary>
    public decimal InteresesConAnticipo { get; init; }

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
/// <item><b>el anticipo aplicado a intereses</b> (decisión del dueño del 2026-10-16): el interés que un anticipo cubre al aplicarse a una cuota
/// en el año cuenta <b>sólo si el anticipo ingresó en el año</b> (la fecha valor del pago que lo dejó); el que ingresó antes no cuenta aunque se
/// aplique dentro, y del pago que usa un anticipo anterior (la cancelación) se resta lo que ese anticipo cubrió. Ver
/// <see cref="AnticiposDelCertificado"/>;</item>
/// <item>el <b>saldo al 31 de diciembre</b> con R = la emisión: los conceptos que diga <see cref="ConceptosDelSaldoCertificado"/> (parámetro
/// <c>Documentos.CertificadoTributario.SaldoIncluye</c>); el defecto, todos: capital, intereses y lo demás cobrable, menos el anticipo.</item>
/// </list>
/// Sólo los créditos donde la persona es <b>deudora</b> (confirmado por el dueño el 2026-10-16): las codeudas no se certifican.
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

    public static CertificadoTributarioArmado Armar(short año, IEnumerable<CreditoParaCertificar> creditos,
        ConceptosDelSaldoCertificado saldoIncluye = ConceptosDelSaldoCertificado.Todos)
    {
        ArgumentNullException.ThrowIfNull(creditos);
        var cierre = new DateOnly(año, 12, 31);
        var explicacion = new ExplicacionDeCartera()
            .Linea($"Intereses pagados en {año}: lo aplicado por el contrato con fecha valor entre el {año}-01-01 y el {año}-12-31, neto de los reversos conocidos a la emisión.")
            .Linea($"Intereses cubiertos por un anticipo: cuentan sólo si el anticipo ingresó en {año}.")
            .Linea($"Saldo de la deuda al {ExplicacionDeCartera.Fecha(cierre)} con la información a la emisión: {saldoIncluye}.");
        var lista = new List<InteresesPagadosPorCredito>();
        foreach (var c in creditos)
        {
            var estado = c.AlCierre;
            if (estado is null || estado.Origen is not { } origen || origen > cierre) continue;
            if (estado.Fecha != cierre) throw new ArgumentException($"El crédito {c.Numero} debe venir reconstruido al {ExplicacionDeCartera.Fecha(cierre)}.", nameof(creditos));

            var delAño = estado.Libro.Movimientos.Where(m => m.Dia.Year == año).ToList();
            var pagos = delAño.Where(m => ClasesDePago.Contains(m.Clase)).ToList();
            var anticipo = AnticiposDelCertificado.Calcular(estado, año, Corrientes, DeMora);
            var corriente = -pagos.Where(m => Corrientes.Contains(m.Concepto)).Sum(m => m.Monto) + anticipo.CorrienteAplicado - anticipo.CorrienteExcluido;
            var mora = -pagos.Where(m => DeMora.Contains(m.Concepto)).Sum(m => m.Monto) + anticipo.MoraAplicado - anticipo.MoraExcluido;
            var capital = estado.Saldo(LoanConcept.Principal);
            var corrientes = Corrientes.Sum(x => estado.Saldo(x));
            var deMora = DeMora.Sum(x => estado.Saldo(x));
            var otros = Otros.Sum(x => estado.Saldo(x));
            var conMovimiento = delAño.Count > 0 || origen.Year == año;
            if (!conMovimiento && capital == 0m && corrientes + deMora == 0m && otros - estado.Anticipo == 0m) continue;

            var saldoCapital = Incluye(saldoIncluye, ConceptosDelSaldoCertificado.Capital) ? capital : 0m;
            var saldoIntereses = (Incluye(saldoIncluye, ConceptosDelSaldoCertificado.InteresCorriente) ? corrientes : 0m)
                                 + (Incluye(saldoIncluye, ConceptosDelSaldoCertificado.InteresDeMora) ? deMora : 0m);
            var saldoOtros = (Incluye(saldoIncluye, ConceptosDelSaldoCertificado.OtrosCargos) ? otros : 0m)
                             - (Incluye(saldoIncluye, ConceptosDelSaldoCertificado.MenosAnticipo) ? estado.Anticipo : 0m);
            lista.Add(new InteresesPagadosPorCredito(c.Numero, c.Linea, c.EsVivienda, corriente, mora, saldoCapital, saldoIntereses, saldoOtros)
            {
                InteresesConAnticipo = anticipo.Aplicado,
            });
            explicacion.Linea($"{c.Numero}: intereses corrientes {ExplicacionDeCartera.Numero(corriente)}, de mora {ExplicacionDeCartera.Numero(mora)}; " +
                              $"saldo {ExplicacionDeCartera.Numero(saldoCapital + saldoIntereses + saldoOtros)}{(c.EsVivienda ? " (vivienda)" : string.Empty)}.");
            if (anticipo.Aplicado != 0m)
                explicacion.Linea($"{c.Numero}: {ExplicacionDeCartera.Numero(anticipo.Aplicado)} de intereses los cubrió un anticipo ingresado en {año}.");
            if (anticipo.Excluido != 0m)
                explicacion.Linea($"{c.Numero}: {ExplicacionDeCartera.Numero(anticipo.Excluido)} de intereses pagados con un anticipo ingresado antes de {año} no cuentan.");
        }
        var ordenados = lista.OrderBy(c => c.Numero, StringComparer.Ordinal).ToList();
        return new CertificadoTributarioArmado(año, ordenados.Where(c => !c.EsVivienda).ToList(), ordenados.Where(c => c.EsVivienda).ToList(), explicacion);
    }

    private static bool Incluye(ConceptosDelSaldoCertificado conjunto, ConceptosDelSaldoCertificado concepto) => (conjunto & concepto) == concepto;
}
