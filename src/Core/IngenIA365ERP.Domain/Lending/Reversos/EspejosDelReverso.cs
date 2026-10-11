using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Reversos;

/// <summary>
/// Una fila del libro tal como la ve la propuesta de espejos (feature 013, US3, T0416; data-model §5.1, §7.11): su Id, clase, concepto,
/// cuota, importe con signo, fecha valor, de qué operación o corrida es y, si es un espejo, de qué fila. Es la misma vista que tendrá el
/// <c>LibroTeorico</c> de <c>ReconstruccionDeCredito</c> (US4) cuando exista; hoy la arma <c>ReversoDeDesembolso</c> desde
/// <c>LND_LoanTransactions</c>. (nuevo)
/// </summary>
public sealed record FilaDelLibro(
    long Id,
    LoanTransactionKind Clase,
    LoanConcept Concepto,
    int? Cuota,
    decimal Importe,
    DateOnly FechaValor,
    long? OperacionId,
    int? CorridaId,
    long? EspejoDe)
{
    /// <summary>La fila es el espejo de otra (<c>ReversesTransactionId</c>).</summary>
    public bool EsEspejo => EspejoDe is not null;
}

/// <summary>
/// Lo que el reverso de un desembolso espeja (T33, C49, C101): las filas con su original y, con varios tramos, desde qué fecha valor la
/// diferencia de causación se recalcula como <c>AccrualAdjustment</c> (nula con un solo tramo: la causación se espeja). (nuevo)
/// </summary>
public sealed record PropuestaDeEspejos(IReadOnlyList<FilaDelLibro> AEspejar, DateOnly? ConciliarCausacionDesde)
{
    /// <summary>Σ de lo que se espeja por concepto (positivo = lo que el reverso quita).</summary>
    public IReadOnlyDictionary<LoanConcept, decimal> PorConcepto =>
        AEspejar.GroupBy(f => f.Concepto).ToDictionary(g => g.Key, g => g.Sum(f => f.Importe));
}

/// <summary>
/// La propuesta de espejos del reverso de un desembolso desde su origen (feature 013, US3, T0416; FR-002, FR-048; data-model §7.11; T33, C49,
/// C101). Pura: no lee ni escribe; <c>RegistroDeCartera.Espejar</c> arma cada espejo y <c>ReversoDeDesembolso</c> la registra.
/// <list type="bullet">
/// <item><b>Un solo tramo vivo</b>: un espejo por cada fila propia del tramo (su capital) y por cada fila de la causación del crédito
/// (<c>Accrual</c>, <c>AccrualAdjustment</c>, <c>SuspenseReclassification</c>): el crédito queda como si el desembolso no hubiera existido
/// (C101).</item>
/// <item><b>Varios tramos</b>: sólo el capital del tramo; la diferencia de causación la produce el recálculo desde la fecha valor del tramo
/// (<see cref="PropuestaDeEspejos.ConciliarCausacionDesde"/>), como <c>AccrualAdjustment</c> por (día valor, concepto, cuota) (C49).</item>
/// </list>
/// Ningún espejo de un espejo (<c>Lending.Reversal.OfReversal</c>) y ninguna fila se espeja dos veces: lo ya espejado queda fuera. US5 le suma
/// los reversos de pago. (nuevo)
/// </summary>
public static class EspejosDelReverso
{
    public const string CodigoReversoDeReverso = "Lending.Reversal.OfReversal";

    /// <summary>Las clases de fila que son causación del crédito.</summary>
    public static IReadOnlySet<LoanTransactionKind> DeCausacion { get; } = new HashSet<LoanTransactionKind>
    {
        LoanTransactionKind.Accrual,
        LoanTransactionKind.AccrualAdjustment,
        LoanTransactionKind.SuspenseReclassification,
    };

    /// <summary>
    /// La propuesta del reverso del tramo <paramref name="operacionDelTramo"/> con fecha valor <paramref name="fechaValorDelTramo"/>, sobre el
    /// libro del crédito y con <paramref name="tramosVivos"/> tramos sin reversar (contándolo a él).
    /// </summary>
    /// <exception cref="ReglaDeCarteraException">El tramo es un espejo (un reverso no se reversa) o no tiene filas.</exception>
    public static PropuestaDeEspejos DelDesembolso(IReadOnlyCollection<FilaDelLibro> libro, long operacionDelTramo, DateOnly fechaValorDelTramo, int tramosVivos)
    {
        ArgumentNullException.ThrowIfNull(libro);
        ArgumentOutOfRangeException.ThrowIfLessThan(tramosVivos, 1);

        var delTramo = libro.Where(f => f.OperacionId == operacionDelTramo).ToList();
        if (delTramo.Count == 0)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoReversoDeReverso,
                "El tramo no tiene filas en el libro: no hay nada que reversar.", "operationId", operacionDelTramo));
        if (delTramo.Any(f => f.EsEspejo))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoReversoDeReverso,
                "Un reverso no se reversa. Para rehacer, registre una operación nueva.", "operationId", operacionDelTramo));

        var yaEspejadas = libro.Where(f => f.EspejoDe is not null).Select(f => f.EspejoDe!.Value).ToHashSet();
        bool Vigente(FilaDelLibro f) => !f.EsEspejo && !yaEspejadas.Contains(f.Id);

        var aEspejar = delTramo.Where(Vigente).ToList();
        if (tramosVivos > 1) return new PropuestaDeEspejos(aEspejar.OrderBy(f => f.Id).ToList(), fechaValorDelTramo);

        aEspejar.AddRange(libro.Where(f => f.OperacionId != operacionDelTramo && DeCausacion.Contains(f.Clase) && Vigente(f)));
        return new PropuestaDeEspejos(aEspejar.OrderBy(f => f.Id).ToList(), null);
    }
}
