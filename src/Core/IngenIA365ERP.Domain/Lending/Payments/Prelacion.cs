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
/// 2026-11-16 (pedido del dueño): con un <see cref="OrdenDeRubros"/> de la empresa (<c>Pagos.PrelacionDeRubros</c>, <see cref="ConRubros"/>) el orden de
/// los conceptos dentro de una cuota sale de los rubros y no de la lista de la línea; el orden de las cuotas, el suspenso con su concepto, los gastos sin
/// constancia al final y la cuota específica fuera de orden siguen siendo los de la línea. Con rubros, el capital de una cuota con prima
/// (<see cref="RubroDeCartera.CuotasExtras"/>) tiene su propio puesto (<see cref="Posicion(LoanConcept, bool)"/>).
/// Pura. (nuevo)
/// </summary>
public sealed class Prelacion
{
    /// <summary>La lista sellada trae un concepto que no se cobra o repetido.</summary>
    public const string CodigoPrelacionInvalida = "Lending.Payment.AllocationOrderInvalid";

    /// <summary>
    /// Los conceptos que un pago puede cubrir (los pasivos con el deudor, anticipo y saldo a favor, no). 2026-11-16 (feature 024, fase 2): con la
    /// administración, al final para no mover el orden de los que la lista sellada no nombra.
    /// </summary>
    public static IReadOnlyList<LoanConcept> Cobrables { get; } =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge, LoanConcept.Administration,
    ];

    /// <summary>La lista por defecto de data-model §1.8.</summary>
    public static IReadOnlyList<LoanConcept> ConceptosPorDefecto { get; } =
        [LoanConcept.CollectionFee, LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.CurrentInterest, LoanConcept.Principal];

    /// <summary>La prelación por defecto: la más antigua primero, el suspenso con su concepto, sin cuota específica fuera de orden.</summary>
    public static Prelacion PorDefecto { get; } = new(AllocationOrder.OldestInstallmentFirst, ConceptosPorDefecto, suspensoConSuConcepto: true,
        cuotaEspecificaFueraDeOrden: false);

    /// <summary>Un puesto en el orden: un concepto o, con rubros, el capital de una cuota con prima.</summary>
    private readonly record struct Puesto(LoanConcept Concepto, bool DeCuotaExtra);

    private readonly Dictionary<Puesto, int> _posicion;

    public Prelacion(AllocationOrder orden, IReadOnlyList<LoanConcept> conceptos, bool suspensoConSuConcepto, bool cuotaEspecificaFueraDeOrden,
        bool gastosInformados = false, OrdenDeRubros? rubros = null)
    {
        ArgumentNullException.ThrowIfNull(conceptos);
        if (!Enum.IsDefined(orden)) throw new ArgumentOutOfRangeException(nameof(orden), orden, "Orden de prelación desconocido.");
        var lista = conceptos.Count == 0 ? ConceptosPorDefecto : conceptos;
        if (lista.Distinct().Count() != lista.Count || lista.Any(c => !Cobrables.Contains(c)))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoPrelacionInvalida,
                "La prelación lista cada concepto cobrable a lo sumo una vez (capital, corriente, mora, sus suspensos, seguros, administración, gastos y otros).",
                "conceptPriority", string.Join(",", lista)));
        Orden = orden;
        Conceptos = lista;
        SuspensoConSuConcepto = suspensoConSuConcepto;
        CuotaEspecificaFueraDeOrden = cuotaEspecificaFueraDeOrden;
        GastosInformados = gastosInformados;
        Rubros = rubros;
        var puestos = rubros is null
            ? Expandir(lista, suspensoConSuConcepto, gastosInformados).Select(c => new Puesto(c, false)).ToList()
            : ExpandirRubros(rubros, suspensoConSuConcepto, gastosInformados);
        ConceptosEnOrden = puestos.Where(p => !p.DeCuotaExtra).Select(p => p.Concepto).ToList();
        _posicion = puestos.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
    }

    public AllocationOrder Orden { get; }

    /// <summary>La lista sellada tal cual (vacía = la de defecto).</summary>
    public IReadOnlyList<LoanConcept> Conceptos { get; }

    public bool SuspensoConSuConcepto { get; }

    /// <summary><c>AllowsSpecificInstallmentOutOfOrder</c>: se puede pagar una cuota con anteriores impagas.</summary>
    public bool CuotaEspecificaFueraDeOrden { get; }

    /// <summary>Hay constancia de que los gastos de cobranza se informaron al deudor (FR-121).</summary>
    public bool GastosInformados { get; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): el orden de rubros de la empresa con que se ordenan los conceptos (<see cref="ConRubros"/>); nulo, la lista de la
    /// línea (<see cref="Conceptos"/>), lo de siempre. (nuevo)
    /// </summary>
    public OrdenDeRubros? Rubros { get; }

    /// <summary>
    /// 2026-11-16: la línea tiene una lista de conceptos propia, distinta de la de defecto (<see cref="ConceptosPorDefecto"/>). Sólo se puede poner por la
    /// API (<c>conceptOrder</c>): la pantalla y la plantilla de líneas siempre guardan la de defecto. Con lista propia manda la línea, que es más
    /// específica que el orden de rubros de la empresa. (nuevo)
    /// </summary>
    public bool TieneListaPropia => !Conceptos.SequenceEqual(ConceptosPorDefecto);

    /// <summary>Todos los conceptos cobrables en el orden en que se cubren dentro de una cuota (con rubros, el capital va en el puesto del capital).</summary>
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
        CuotaEspecificaFueraDeOrden ? this : new Prelacion(Orden, Conceptos, SuspensoConSuConcepto, cuotaEspecificaFueraDeOrden: true, GastosInformados, Rubros);

    /// <summary>
    /// 2026-11-16 (pedido del dueño): la misma prelación con los conceptos ordenados por el <see cref="OrdenDeRubros"/> de la empresa. No mira si la línea
    /// tiene lista propia (<see cref="TieneListaPropia"/>): eso lo decide quien sella el orden en el pago; la reconstrucción aplica lo sellado. (nuevo)
    /// </summary>
    public Prelacion ConRubros(OrdenDeRubros rubros)
    {
        ArgumentNullException.ThrowIfNull(rubros);
        return rubros.Equals(Rubros) ? this : new Prelacion(Orden, Conceptos, SuspensoConSuConcepto, CuotaEspecificaFueraDeOrden, GastosInformados, rubros);
    }

    /// <summary>La posición de un concepto cobrable en <see cref="ConceptosEnOrden"/>.</summary>
    public int Posicion(LoanConcept concepto) =>
        _posicion.TryGetValue(new Puesto(concepto, false), out var p)
            ? p
            : throw new ArgumentOutOfRangeException(nameof(concepto), concepto, "El concepto no se cubre con un pago.");

    /// <summary>
    /// 2026-11-16: la posición de un concepto en una cuota con prima (<paramref name="deCuotaExtra"/>): con rubros, el capital va en el puesto de
    /// <see cref="RubroDeCartera.CuotasExtras"/>; lo demás —y todo sin rubros— en el de siempre. (nuevo)
    /// </summary>
    public int Posicion(LoanConcept concepto, bool deCuotaExtra) =>
        deCuotaExtra && concepto == LoanConcept.Principal && _posicion.TryGetValue(new Puesto(concepto, true), out var p) ? p : Posicion(concepto);

    /// <summary>
    /// Ordena lo que hay por cubrir: con <see cref="AllocationOrder.OldestInstallmentFirst"/> por cuota (sin cuota = la más antigua) y,
    /// dentro, por concepto; con <see cref="AllocationOrder.ConceptFirst"/> por concepto y, dentro, por cuota. 2026-11-16: con
    /// <paramref name="deCuotaExtra"/>, el capital de una cuota con prima toma el puesto de las cuotas extras (<see cref="Posicion(LoanConcept, bool)"/>).
    /// </summary>
    public IEnumerable<T> Ordenar<T>(IEnumerable<T> elementos, Func<T, int?> cuota, Func<T, LoanConcept> concepto, Func<T, bool>? deCuotaExtra = null)
    {
        ArgumentNullException.ThrowIfNull(elementos);
        int Puesto(T e) => Posicion(concepto(e), deCuotaExtra?.Invoke(e) ?? false);
        return Orden == AllocationOrder.ConceptFirst
            ? elementos.OrderBy(Puesto).ThenBy(e => cuota(e) ?? 0)
            : elementos.OrderBy(e => cuota(e) ?? 0).ThenBy(Puesto);
    }

    private static IReadOnlyList<LoanConcept> Expandir(IReadOnlyList<LoanConcept> lista, bool suspensoConSuConcepto, bool gastosInformados)
    {
        var salida = new List<LoanConcept>();
        foreach (var c in lista)
        {
            if (c == LoanConcept.CollectionFee && !gastosInformados) continue;
            if (c is LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense && suspensoConSuConcepto) continue;
            salida.Add(c);
            // 2026-11-16 (feature 024, fase 2): la administración que la lista no nombra va con el seguro, donde se cobraba cuando era un seguro más
            // (las listas selladas antes no la conocen).
            if (c == LoanConcept.Insurance && !lista.Contains(LoanConcept.Administration)) salida.Add(LoanConcept.Administration);
            if (!suspensoConSuConcepto) continue;
            if (c == LoanConcept.DefaultInterest) salida.Add(LoanConcept.DefaultInterestInSuspense);
            if (c == LoanConcept.CurrentInterest) salida.Add(LoanConcept.CurrentInterestInSuspense);
        }
        // Lo que la lista no nombra: primero los cobrables que faltan (sin los suspensos ni los gastos), luego los suspensos que no van
        // con su concepto y al final los gastos no informados.
        foreach (var c in Cobrables)
            if (!salida.Contains(c) && c is not (LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense or LoanConcept.CollectionFee))
            {
                salida.Add(c);
                if (c == LoanConcept.Insurance && !salida.Contains(LoanConcept.Administration)) salida.Add(LoanConcept.Administration);
            }
        foreach (var c in new[] { LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterestInSuspense })
            if (!salida.Contains(c)) salida.Add(c);
        if (!salida.Contains(LoanConcept.CollectionFee)) salida.Add(LoanConcept.CollectionFee);
        return salida;
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): los puestos de un orden de rubros (<see cref="OrdenDeRubros.Completo"/>): el seguro; la administración (feature
    /// 024, fase 2: su propio concepto); el interés, la mora y la corriente (cada una con su suspenso si va con su concepto); los gastos de cobranza, sólo informados; los otros cargos; las
    /// cuotas extras, el capital de las cuotas con prima, y el capital. Después, como siempre, los suspensos que no van con su concepto y al final los
    /// gastos sin constancia (FR-121).
    /// </summary>
    private static List<Puesto> ExpandirRubros(OrdenDeRubros rubros, bool suspensoConSuConcepto, bool gastosInformados)
    {
        var salida = new List<Puesto>();
        void Poner(LoanConcept c, bool deCuotaExtra = false)
        {
            var puesto = new Puesto(c, deCuotaExtra);
            if (!salida.Contains(puesto)) salida.Add(puesto);
        }
        foreach (var r in rubros.Completo)
        {
            switch (r)
            {
                case RubroDeCartera.Seguro:
                    Poner(LoanConcept.Insurance);
                    break;
                // 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración es su propio concepto.
                case RubroDeCartera.Administracion:
                    Poner(LoanConcept.Administration);
                    break;
                case RubroDeCartera.Interes:
                    Poner(LoanConcept.DefaultInterest);
                    if (suspensoConSuConcepto) Poner(LoanConcept.DefaultInterestInSuspense);
                    Poner(LoanConcept.CurrentInterest);
                    if (suspensoConSuConcepto) Poner(LoanConcept.CurrentInterestInSuspense);
                    break;
                case RubroDeCartera.GastosDeCobranza:
                    if (gastosInformados) Poner(LoanConcept.CollectionFee);
                    break;
                case RubroDeCartera.OtrosCargos:
                    Poner(LoanConcept.OtherCharge);
                    break;
                case RubroDeCartera.CuotasExtras:
                    Poner(LoanConcept.Principal, deCuotaExtra: true);
                    break;
                case RubroDeCartera.Capital:
                    Poner(LoanConcept.Principal);
                    break;
            }
        }
        Poner(LoanConcept.DefaultInterestInSuspense);
        Poner(LoanConcept.CurrentInterestInSuspense);
        Poner(LoanConcept.CollectionFee);
        return salida;
    }
}
