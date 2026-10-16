using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>Un gestor de una regla de reparto y su peso (mayor que cero). (nuevo)</summary>
public sealed record GestorDeRegla(Guid UsuarioPublicId, decimal Peso);

/// <summary>Un gestor como se guarda en <c>LND_CollectorAssignmentRules.CollectorsJson</c>: <c>{ "userPublicId": "…", "weight": 1 }</c>. (nuevo)</summary>
public sealed record GestorDeReglaJson(Guid UserPublicId, decimal Weight);

/// <summary>
/// Una regla de reparto vigente (<c>LND_CollectorAssignmentRules</c>, data-model §26.6): prioridad, criterios (nulo = cualquiera), forma del
/// reparto, si asigna al deudor completo y sus gestores con su peso. El saldo mínimo y el máximo se incluyen. (nuevo)
/// </summary>
public sealed record ReglaDeReparto(Guid PublicId, int Prioridad, AssignmentDistribution Distribucion, IReadOnlyList<GestorDeRegla> Gestores)
{
    public Guid? Oficina { get; init; }
    public string? Etapa { get; init; }
    public int? Linea { get; init; }
    public int? Pagaduria { get; init; }
    public decimal? SaldoMinimo { get; init; }
    public decimal? SaldoMaximo { get; init; }

    /// <summary>Todos los créditos del deudor que casan con la regla van a un solo gestor (la asignación lleva el crédito nulo).</summary>
    public bool DeudorCompleto { get; init; }

    public bool Casa(CreditoParaReparto c) =>
        (Oficina is not { } o || c.Oficina == o)
        && (Etapa is null || string.Equals(c.Etapa, Etapa, StringComparison.Ordinal))
        && (Linea is not { } l || c.Linea == l)
        && (Pagaduria is not { } p || c.Pagaduria == p)
        && (SaldoMinimo is not { } min || c.Saldo >= min)
        && (SaldoMaximo is not { } max || c.Saldo <= max);
}

/// <summary>Un crédito por repartir con lo que miran las reglas y su gestor vigente, si tiene. (nuevo)</summary>
public sealed record CreditoParaReparto(Guid CreditoPublicId, Guid DeudorPublicId, decimal Saldo)
{
    public Guid? Oficina { get; init; }
    public string? Etapa { get; init; }
    public int? Linea { get; init; }
    public int? Pagaduria { get; init; }
    public Guid? GestorActual { get; init; }
}

/// <summary>
/// Lo que el reparto propone para una unidad: un crédito, o el deudor completo (<see cref="CreditoPublicId"/> nulo) con los créditos que
/// lleva; el gestor, la regla y si se mantiene el gestor que ya tenía. (nuevo)
/// </summary>
public sealed record AsignacionPropuesta(
    Guid? CreditoPublicId, Guid DeudorPublicId, IReadOnlyList<Guid> Creditos, Guid GestorPublicId, Guid ReglaPublicId, bool SeMantiene);

/// <summary>El reparto: las asignaciones en el orden en que se decidieron y los créditos que ninguna regla toma. (nuevo)</summary>
public sealed record ResultadoDeReparto(IReadOnlyList<AsignacionPropuesta> Asignaciones, IReadOnlyList<Guid> SinRegla);

/// <summary>
/// Motor puro del reparto de cartera entre gestores (feature 013, K4, T0937; FR-118, T77; nombre fijado en data-model §26.6). Reproducible:
/// el mismo insumo da siempre el mismo reparto, sin azar ni dependencia del orden de llegada.
/// <list type="number">
/// <item>Cada crédito va a la <b>primera regla que casa</b>: prioridad ascendente y, si empatan, el <c>PublicId</c> de la regla (texto,
/// ordinal).</item>
/// <item>Dentro de una regla, la unidad es el crédito o, si la regla pide el deudor completo, los créditos del deudor que casaron con ella
/// (su tamaño es la cantidad de créditos y su saldo la suma).</item>
/// <item>Las unidades cuyo gestor vigente es uno de los de la regla <b>lo conservan</b> (si es el deudor completo, sólo si todos sus créditos
/// lo tienen) y cuentan en su carga antes de repartir el resto.</item>
/// <item>El resto se recorre por <c>PublicId</c> (del crédito o del deudor; texto, ordinal) y cada unidad va al gestor que, al recibirla,
/// queda con la menor carga relativa: <c>(carga + tamaño) / peso</c>, donde carga y tamaño son cantidad de créditos (<c>ByCount</c>) o saldo
/// (<c>ByBalance</c>); si empatan, el de menor <c>PublicId</c>.</item>
/// </list>
/// Cada regla lleva su propia carga: un gestor que está en dos reglas no suma entre ellas. (nuevo)
/// </summary>
public static class RepartoDeGestores
{
    /// <summary>La regla no trae gestores, repite uno o tiene un peso que no es mayor que cero, o su rango de saldo está invertido.</summary>
    public const string CodigoReglaInvalida = "Lending.CollectorAssignmentRule.Invalid";

    public static ProblemaDeCartera? Validar(ReglaDeReparto regla)
    {
        ArgumentNullException.ThrowIfNull(regla);
        if (regla.Gestores.Count == 0)
            return ProblemaDeCartera.Con(CodigoReglaInvalida, "La regla de reparto necesita al menos un gestor.", "collectors", 0);
        if (regla.Gestores.GroupBy(g => g.UsuarioPublicId).Any(g => g.Count() > 1))
            return ProblemaDeCartera.Con(CodigoReglaInvalida, "Un gestor está dos veces en la regla.", "collectors", regla.Gestores.Count);
        if (regla.Gestores.FirstOrDefault(g => g.Peso <= 0m) is { } sinPeso)
            return ProblemaDeCartera.Con(CodigoReglaInvalida, "El peso de cada gestor es mayor que cero.", "collector", sinPeso.UsuarioPublicId);
        if (regla.SaldoMinimo is { } min && regla.SaldoMaximo is { } max && max < min)
            return ProblemaDeCartera.Con(CodigoReglaInvalida, "El saldo máximo de la regla es menor que el mínimo.", "maxBalance", max);
        if (!Enum.IsDefined(regla.Distribucion))
            return ProblemaDeCartera.Con(CodigoReglaInvalida, "Forma de reparto desconocida.", "distribution", regla.Distribucion.ToString());
        return null;
    }

    public static ResultadoDeReparto Repartir(IReadOnlyCollection<ReglaDeReparto> reglas, IReadOnlyCollection<CreditoParaReparto> creditos)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(creditos);
        foreach (var r in reglas) ReglaDeCarteraException.SiHay(Validar(r));

        var ordenadas = reglas.OrderBy(r => r.Prioridad).ThenBy(r => Texto(r.PublicId), StringComparer.Ordinal).ToList();
        var porRegla = ordenadas.ToDictionary(r => r.PublicId, _ => new List<CreditoParaReparto>());
        var sinRegla = new List<Guid>();
        foreach (var c in creditos.OrderBy(c => Texto(c.CreditoPublicId), StringComparer.Ordinal))
        {
            var regla = ordenadas.FirstOrDefault(r => r.Casa(c));
            if (regla is null) sinRegla.Add(c.CreditoPublicId);
            else porRegla[regla.PublicId].Add(c);
        }

        var asignaciones = new List<AsignacionPropuesta>();
        foreach (var regla in ordenadas)
            asignaciones.AddRange(RepartirRegla(regla, porRegla[regla.PublicId]));
        return new ResultadoDeReparto(asignaciones, sinRegla);
    }

    private sealed record Unidad(Guid Clave, Guid? Credito, Guid Deudor, IReadOnlyList<CreditoParaReparto> Creditos)
    {
        public decimal Tamano(AssignmentDistribution d) => d == AssignmentDistribution.ByBalance ? Creditos.Sum(c => c.Saldo) : Creditos.Count;

        public Guid? GestorActual =>
            Creditos.Select(c => c.GestorActual).Distinct().ToList() is [{ } unico] ? unico : null;
    }

    private static IEnumerable<AsignacionPropuesta> RepartirRegla(ReglaDeReparto regla, List<CreditoParaReparto> creditos)
    {
        var unidades = regla.DeudorCompleto
            ? creditos.GroupBy(c => c.DeudorPublicId)
                .Select(g => new Unidad(g.Key, null, g.Key, g.OrderBy(c => Texto(c.CreditoPublicId), StringComparer.Ordinal).ToList()))
            : creditos.Select(c => new Unidad(c.CreditoPublicId, c.CreditoPublicId, c.DeudorPublicId, [c]));
        var lista = unidades.OrderBy(u => Texto(u.Clave), StringComparer.Ordinal).ToList();

        var gestores = regla.Gestores.OrderBy(g => Texto(g.UsuarioPublicId), StringComparer.Ordinal).ToList();
        var carga = gestores.ToDictionary(g => g.UsuarioPublicId, _ => 0m);
        var resultado = new List<AsignacionPropuesta>();

        foreach (var u in lista.Where(u => u.GestorActual is { } g && carga.ContainsKey(g)))
        {
            var g = u.GestorActual!.Value;
            carga[g] += u.Tamano(regla.Distribucion);
            resultado.Add(Propuesta(u, g, regla, seMantiene: true));
        }

        foreach (var u in lista.Where(u => !(u.GestorActual is { } g && carga.ContainsKey(g))))
        {
            var tamano = u.Tamano(regla.Distribucion);
            GestorDeRegla? elegido = null;
            var mejor = 0m;
            foreach (var g in gestores)
            {
                var relativa = (carga[g.UsuarioPublicId] + tamano) / g.Peso;
                if (elegido is null || relativa < mejor)
                {
                    elegido = g;
                    mejor = relativa;
                }
            }
            carga[elegido!.UsuarioPublicId] += tamano;
            resultado.Add(Propuesta(u, elegido.UsuarioPublicId, regla, seMantiene: false));
        }
        return resultado;
    }

    private static AsignacionPropuesta Propuesta(Unidad u, Guid gestor, ReglaDeReparto regla, bool seMantiene) =>
        new(u.Credito, u.Deudor, u.Creditos.Select(c => c.CreditoPublicId).ToList(), gestor, regla.PublicId, seMantiene);

    /// <summary>El orden de un <c>PublicId</c>: su texto en minúsculas con guiones, comparado ordinal (el mismo en cualquier lenguaje).</summary>
    public static string Texto(Guid id) => id.ToString("D");
}
