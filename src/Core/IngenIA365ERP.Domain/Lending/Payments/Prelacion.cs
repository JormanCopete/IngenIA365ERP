using System.Text.Json;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payments;

/// <summary>
/// La <b>prelación</b> sellada del crédito (feature 013, US5, T0533; FR-030, FR-072, T31; data-model §1.8): el orden en que un pago
/// cubre cuotas y conceptos. Son las columnas <c>AllocationOrder</c>, <c>ConceptPriority</c>, <c>SuspenseWithItsConcept</c> y
/// <c>AllowsSpecificInstallmentOutOfOrder</c> de la versión de la línea, copiadas en <c>LoanTermsVersion</c>: la misma para simular y para
/// aplicar.
/// <list type="bullet">
/// <item>Por defecto <see cref="AllocationOrder.OldestInstallmentFirst"/> con gastos informados → seguros → mora con su suspenso →
/// corriente con su suspenso → capital: la cuota más antigua se cubre entera antes de pasar a la siguiente.</item>
/// <item><see cref="AllocationOrder.ConceptFirst"/>: un concepto en todas las cuotas (de la más antigua a la más nueva) antes del
/// siguiente concepto.</item>
/// <item>Los gastos de cobranza (<see cref="LoanConcept.CollectionFee"/>) van primero sólo si se informaron al deudor (FR-121); sin
/// constancia el motor los pasa al final, después de todo lo demás, para que nunca se cobren antes que la deuda.</item>
/// <item>Con <see cref="SuspensoConSuConcepto"/> el interés en suspenso va inmediatamente después de su concepto (lo causado antes de
/// suspender es más antiguo); sin él, los suspensos van después del capital.</item>
/// <item>Los conceptos cobrables que la lista sellada no nombra (<see cref="LoanConcept.OtherCharge"/>, por ejemplo) van después de los
/// nombrados, en el orden del enum.</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public sealed class Prelacion
{
    /// <summary>La lista sellada trae un concepto que no se cobra o repetido.</summary>
    public const string CodigoPrelacionInvalida = "Lending.Payment.AllocationOrderInvalid";

    /// <summary>Los conceptos que un pago puede cubrir (los pasivos con el deudor, anticipo y saldo a favor, no).</summary>
    public static IReadOnlyList<LoanConcept> Cobrables { get; } =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    /// <summary>La lista por defecto de data-model §1.8.</summary>
    public static IReadOnlyList<LoanConcept> ConceptosPorDefecto { get; } =
        [LoanConcept.CollectionFee, LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.CurrentInterest, LoanConcept.Principal];

    /// <summary>La prelación por defecto: la más antigua primero, el suspenso con su concepto, sin cuota específica fuera de orden.</summary>
    public static Prelacion PorDefecto { get; } = new(AllocationOrder.OldestInstallmentFirst, ConceptosPorDefecto, suspensoConSuConcepto: true,
        cuotaEspecificaFueraDeOrden: false);

    private readonly Dictionary<LoanConcept, int> _posicion;

    public Prelacion(AllocationOrder orden, IReadOnlyList<LoanConcept> conceptos, bool suspensoConSuConcepto, bool cuotaEspecificaFueraDeOrden,
        bool gastosInformados = false)
    {
        ArgumentNullException.ThrowIfNull(conceptos);
        if (!Enum.IsDefined(orden)) throw new ArgumentOutOfRangeException(nameof(orden), orden, "Orden de prelación desconocido.");
        var lista = conceptos.Count == 0 ? ConceptosPorDefecto : conceptos;
        if (lista.Distinct().Count() != lista.Count || lista.Any(c => !Cobrables.Contains(c)))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoPrelacionInvalida,
                "La prelación lista cada concepto cobrable a lo sumo una vez (capital, corriente, mora, sus suspensos, seguros, gastos y otros).",
                "conceptPriority", string.Join(",", lista)));
        Orden = orden;
        Conceptos = lista;
        SuspensoConSuConcepto = suspensoConSuConcepto;
        CuotaEspecificaFueraDeOrden = cuotaEspecificaFueraDeOrden;
        GastosInformados = gastosInformados;
        ConceptosEnOrden = Expandir(lista, suspensoConSuConcepto, gastosInformados);
        _posicion = ConceptosEnOrden.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
    }

    public AllocationOrder Orden { get; }

    /// <summary>La lista sellada tal cual (vacía = la de defecto).</summary>
    public IReadOnlyList<LoanConcept> Conceptos { get; }

    public bool SuspensoConSuConcepto { get; }

    /// <summary><c>AllowsSpecificInstallmentOutOfOrder</c>: se puede pagar una cuota con anteriores impagas.</summary>
    public bool CuotaEspecificaFueraDeOrden { get; }

    /// <summary>Hay constancia de que los gastos de cobranza se informaron al deudor (FR-121).</summary>
    public bool GastosInformados { get; }

    /// <summary>Todos los conceptos cobrables en el orden en que se cubren dentro de una cuota.</summary>
    public IReadOnlyList<LoanConcept> ConceptosEnOrden { get; }

    /// <summary>
    /// La prelación sellada en <c>LoanTermsVersion</c>: <paramref name="conceptPriority"/> es el JSON de nombres (<c>["CollectionFee",…]</c>);
    /// vacío o <c>[]</c> es la lista por defecto.
    /// </summary>
    public static Prelacion DesdeSellada(AllocationOrder orden, string? conceptPriority, bool suspensoConSuConcepto, bool cuotaEspecificaFueraDeOrden,
        bool gastosInformados = false)
    {
        var conceptos = new List<LoanConcept>();
        if (!string.IsNullOrWhiteSpace(conceptPriority))
        {
            var nombres = JsonSerializer.Deserialize<List<string>>(conceptPriority) ?? [];
            foreach (var n in nombres)
            {
                if (!Enum.TryParse<LoanConcept>(n, ignoreCase: false, out var c) || !Enum.IsDefined(c))
                    throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoPrelacionInvalida,
                        $"La prelación sellada nombra un concepto desconocido: «{n}».", "conceptPriority", conceptPriority));
                conceptos.Add(c);
            }
        }
        return new Prelacion(orden, conceptos, suspensoConSuConcepto, cuotaEspecificaFueraDeOrden, gastosInformados);
    }

    /// <summary>La misma prelación que admite pagar una cuota específica con anteriores impagas (la reconstrucción de un pago ya aceptado).</summary>
    public Prelacion PermitiendoCuotaFueraDeOrden() =>
        CuotaEspecificaFueraDeOrden ? this : new Prelacion(Orden, Conceptos, SuspensoConSuConcepto, cuotaEspecificaFueraDeOrden: true, GastosInformados);

    /// <summary>La posición de un concepto cobrable en <see cref="ConceptosEnOrden"/>.</summary>
    public int Posicion(LoanConcept concepto) =>
        _posicion.TryGetValue(concepto, out var p) ? p : throw new ArgumentOutOfRangeException(nameof(concepto), concepto, "El concepto no se cubre con un pago.");

    /// <summary>
    /// Ordena lo que hay por cubrir: con <see cref="AllocationOrder.OldestInstallmentFirst"/> por cuota (sin cuota = la más antigua) y,
    /// dentro, por concepto; con <see cref="AllocationOrder.ConceptFirst"/> por concepto y, dentro, por cuota.
    /// </summary>
    public IEnumerable<T> Ordenar<T>(IEnumerable<T> elementos, Func<T, int?> cuota, Func<T, LoanConcept> concepto)
    {
        ArgumentNullException.ThrowIfNull(elementos);
        return Orden == AllocationOrder.ConceptFirst
            ? elementos.OrderBy(e => Posicion(concepto(e))).ThenBy(e => cuota(e) ?? 0)
            : elementos.OrderBy(e => cuota(e) ?? 0).ThenBy(e => Posicion(concepto(e)));
    }

    private static IReadOnlyList<LoanConcept> Expandir(IReadOnlyList<LoanConcept> lista, bool suspensoConSuConcepto, bool gastosInformados)
    {
        var salida = new List<LoanConcept>();
        foreach (var c in lista)
        {
            if (c == LoanConcept.CollectionFee && !gastosInformados) continue;
            if (c is LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense && suspensoConSuConcepto) continue;
            salida.Add(c);
            if (!suspensoConSuConcepto) continue;
            if (c == LoanConcept.DefaultInterest) salida.Add(LoanConcept.DefaultInterestInSuspense);
            if (c == LoanConcept.CurrentInterest) salida.Add(LoanConcept.CurrentInterestInSuspense);
        }
        // Lo que la lista no nombra: primero los cobrables que faltan (sin los suspensos ni los gastos), luego los suspensos que no van
        // con su concepto y al final los gastos no informados.
        foreach (var c in Cobrables)
            if (!salida.Contains(c) && c is not (LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense or LoanConcept.CollectionFee))
                salida.Add(c);
        foreach (var c in new[] { LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterestInSuspense })
            if (!salida.Contains(c)) salida.Add(c);
        if (!salida.Contains(LoanConcept.CollectionFee)) salida.Add(LoanConcept.CollectionFee);
        return salida;
    }
}
