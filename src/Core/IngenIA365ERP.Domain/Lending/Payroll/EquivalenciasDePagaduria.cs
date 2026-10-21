using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payroll;

/// <summary>
/// Un ítem de una equivalencia de pagaduría (feature 014, T090; data-model §1.4.2): la naturaleza de la línea de Cartera (<c>Placement</c>,
/// <c>Deposit</c>, <c>Contribution</c>), el código de la línea o <see cref="CualquierLinea"/>, el concepto (<c>LoanConcept</c> o
/// <c>MemberAccountConcept</c> por nombre, o <see cref="CuotaCompleta"/>) y su prioridad dentro del código. (nuevo)
/// </summary>
public sealed record ItemDeEquivalencia(
    [property: JsonPropertyName("nature")] LineNature Nature,
    [property: JsonPropertyName("creditLineCode")] string CreditLineCode,
    [property: JsonPropertyName("concept")] string Concept,
    [property: JsonPropertyName("priority")] int Priority)
{
    /// <summary>Cualquier línea de esa naturaleza.</summary>
    public const string CualquierLinea = "*";

    /// <summary>La cuota completa (todos sus conceptos).</summary>
    public const string CuotaCompleta = "Installment";

    /// <summary>¿Este ítem cubre la línea y el concepto? <see cref="CualquierLinea"/> cubre toda línea; <see cref="CuotaCompleta"/>, todo concepto.</summary>
    public bool Cubre(LineNature naturaleza, string linea, string concepto) =>
        Nature == naturaleza
        && (CreditLineCode == CualquierLinea || string.Equals(CreditLineCode, linea, StringComparison.OrdinalIgnoreCase))
        && (Concept == CuotaCompleta || string.Equals(Concept, concepto, StringComparison.OrdinalIgnoreCase));

    /// <summary>¿Dos ítems se pisan? (la misma naturaleza, líneas que se cruzan —una es «*» o son la misma— y conceptos que se cruzan)</summary>
    public bool SePisaCon(ItemDeEquivalencia otro) =>
        Nature == otro.Nature
        && (CreditLineCode == CualquierLinea || otro.CreditLineCode == CualquierLinea
            || string.Equals(CreditLineCode, otro.CreditLineCode, StringComparison.OrdinalIgnoreCase))
        && (Concept == CuotaCompleta || otro.Concept == CuotaCompleta || string.Equals(Concept, otro.Concept, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Una equivalencia completa para validar y resolver: el código de la pagaduría, su regla de reparto y sus ítems. (nuevo)</summary>
public sealed record EquivalenciaDePagaduria(string Codigo, ConceptAllocationRule Regla, int Prioridad, IReadOnlyList<ItemDeEquivalencia> Items);

/// <summary>Un problema de las equivalencias de un convenio: el código del error y lo que la persona lee. (nuevo)</summary>
public sealed record ProblemaDeEquivalencias(string Codigo, string Mensaje, string CodigoDePagaduria);

/// <summary>
/// Las equivalencias de pagaduría (feature 014, K7, T090/T091; data-model §1.4.2; FR-125, E-05): el JSON canónico de los ítems
/// (<see cref="Serializar"/>: ordenados por prioridad, naturaleza, línea y concepto, sin espacios), su validación —al menos un ítem, prioridad
/// única dentro de un código con <c>Priority</c>, un ítem no aparece en dos códigos de la misma versión (<see cref="CodigoMapeadoDosVeces"/>)— y
/// qué código cubre una línea y un concepto de Cartera (<see cref="Resolver"/>). Puro: no sabe de base ni de archivos. (nuevo)
/// </summary>
public static class EquivalenciasDePagaduria
{
    public const string CodigoMapeadoDosVeces = "Lending.PayrollAgreement.ConceptMappedTwice";
    public const string CodigoSinReparto = "Lending.PayrollAgreement.EquivalenceWithoutSplit";
    public const string CodigoSinItems = "Lending.PayrollAgreement.EquivalenceWithoutItems";
    public const string CodigoPrioridadRepetida = "Lending.PayrollAgreement.EquivalencePriorityRepeated";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>El JSON canónico de los ítems: el mismo conjunto da el mismo texto, en cualquier orden.</summary>
    public static string Serializar(IEnumerable<ItemDeEquivalencia> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var orden = items
            .Select(i => i with { CreditLineCode = i.CreditLineCode.Trim().ToUpperInvariant(), Concept = i.Concept.Trim() })
            .OrderBy(i => i.Priority).ThenBy(i => i.Nature).ThenBy(i => i.CreditLineCode, StringComparer.Ordinal).ThenBy(i => i.Concept, StringComparer.Ordinal)
            .ToList();
        return JsonSerializer.Serialize(orden, Opciones);
    }

    /// <summary>Lee los ítems de su JSON; un texto vacío o inválido es una lista vacía (la validación lo rechaza).</summary>
    public static IReadOnlyList<ItemDeEquivalencia> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<ItemDeEquivalencia>>(json, Opciones) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Valida las equivalencias de una versión; nulo si están bien.</summary>
    public static ProblemaDeEquivalencias? Validar(IReadOnlyList<EquivalenciaDePagaduria> equivalencias)
    {
        ArgumentNullException.ThrowIfNull(equivalencias);
        foreach (var e in equivalencias)
        {
            if (!Enum.IsDefined(e.Regla))
                return new(CodigoSinReparto, $"El código {e.Codigo} de la pagaduría no dice cómo se reparte lo descontado: elija Prioridad o Proporcional.", e.Codigo);
            if (e.Items.Count == 0)
                return new(CodigoSinItems, $"El código {e.Codigo} de la pagaduría no agrupa ninguna línea ni concepto de la Cartera.", e.Codigo);
            if (e.Regla == ConceptAllocationRule.Priority && e.Items.GroupBy(i => i.Priority).FirstOrDefault(g => g.Count() > 1) is { } repetida)
                return new(CodigoPrioridadRepetida,
                    $"El código {e.Codigo} reparte por prioridad y dos de sus ítems tienen la prioridad {repetida.Key}: cada ítem lleva la suya.", e.Codigo);
        }

        var todos = equivalencias.SelectMany(e => e.Items.Select(i => (e.Codigo, Item: i))).ToList();
        for (var a = 0; a < todos.Count; a++)
            for (var b = a + 1; b < todos.Count; b++)
                if (todos[a].Codigo != todos[b].Codigo && todos[a].Item.SePisaCon(todos[b].Item))
                    return new(CodigoMapeadoDosVeces,
                        $"La línea {todos[a].Item.CreditLineCode} ({todos[a].Item.Nature}) con el concepto {todos[a].Item.Concept} aparece en los códigos "
                        + $"{todos[a].Codigo} y {todos[b].Codigo}: un ítem va en un solo código de la pagaduría.", todos[b].Codigo);
        return null;
    }

    /// <summary>El código de la pagaduría que cubre esa línea y ese concepto, o nulo («no mapeado»).</summary>
    public static EquivalenciaDePagaduria? Resolver(IEnumerable<EquivalenciaDePagaduria> equivalencias, LineNature naturaleza, string linea, string concepto) =>
        equivalencias.FirstOrDefault(e => e.Items.Any(i => i.Cubre(naturaleza, linea, concepto)));
}

/// <summary>Lo que hay que cubrir con lo descontado por un código: una clave (cuota, concepto), lo pendiente y su prioridad. (nuevo)</summary>
public sealed record PorRepartir(string Clave, decimal Pendiente, int Prioridad);

/// <summary>El reparto de lo descontado por un código: lo asignado a cada clave y lo que sobró (va a «no aplicados»). (nuevo)</summary>
public sealed record RepartoDeDescuento(IReadOnlyList<(string Clave, decimal Asignado)> Asignaciones, decimal Sobrante);

/// <summary>
/// El reparto de lo que la pagaduría informa descontado por un código (feature 014, K7, T091; data-model §1.4.2; FR-125): con <c>Priority</c>
/// llena en orden de prioridad (y de clave, para que sea determinista) hasta agotar; con <c>Proportional</c> reparte a prorrata de lo pendiente, al
/// centavo, y el residuo del redondeo va a la de mayor pendiente (la primera clave si empatan). Ninguna clave recibe más que su pendiente; lo que
/// excede el total pendiente sobra. Puro. (nuevo)
/// </summary>
public static class RepartoDePagaduria
{
    public static RepartoDeDescuento Repartir(decimal descontado, ConceptAllocationRule regla, IReadOnlyList<PorRepartir> pendientes)
    {
        ArgumentNullException.ThrowIfNull(pendientes);
        if (descontado < 0m) throw new ArgumentOutOfRangeException(nameof(descontado), "Lo descontado no es negativo.");
        var vivos = pendientes.Where(p => p.Pendiente > 0m).OrderBy(p => p.Prioridad).ThenBy(p => p.Clave, StringComparer.Ordinal).ToList();
        var total = vivos.Sum(p => p.Pendiente);
        var aRepartir = Math.Min(descontado, total);
        var sobrante = descontado - aRepartir;
        if (aRepartir == 0m || vivos.Count == 0) return new(vivos.Select(p => (p.Clave, 0m)).ToList(), descontado);

        if (regla == ConceptAllocationRule.Priority)
        {
            var queda = aRepartir;
            var asignaciones = new List<(string, decimal)>();
            foreach (var p in vivos)
            {
                var asigna = Math.Min(queda, p.Pendiente);
                queda -= asigna;
                asignaciones.Add((p.Clave, asigna));
            }
            return new(asignaciones, sobrante);
        }

        var proporcional = vivos.Select(p => (p.Clave, p.Pendiente, Asignado: Math.Min(p.Pendiente, Math.Round(aRepartir * p.Pendiente / total, 2, MidpointRounding.ToZero))))
            .ToList();
        var residuo = aRepartir - proporcional.Sum(p => p.Asignado);
        // El residuo del redondeo va primero a la de mayor pendiente que todavía admite, y si no alcanza, a las siguientes.
        foreach (var i in Enumerable.Range(0, proporcional.Count).OrderByDescending(i => proporcional[i].Pendiente).ThenBy(i => i))
        {
            if (residuo <= 0m) break;
            var cabe = Math.Min(residuo, proporcional[i].Pendiente - proporcional[i].Asignado);
            proporcional[i] = proporcional[i] with { Asignado = proporcional[i].Asignado + cabe };
            residuo -= cabe;
        }
        return new(proporcional.Select(p => (p.Clave, p.Asignado)).ToList(), sobrante + residuo);
    }
}
