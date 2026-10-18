using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Attributions;

/// <summary>
/// Un nivel de la matriz (<c>LND_AttributionLevels</c>): código, nombre, rango (mayor = más alto), clase, techo de monto consolidado
/// (nulo sólo en el más alto), si es el nivel de excepción o el del consejo, y el permiso (individual) o el órgano (colegiado). (nuevo)
/// </summary>
public sealed record NivelDeAtribucion(
    string Codigo,
    string Nombre,
    int Rango,
    AttributionLevelKind Clase,
    decimal? TechoConsolidado,
    bool EsDeExcepcion,
    bool EsConsejo,
    string? Permiso,
    string? Organo);

/// <summary>
/// Una regla de la matriz (<c>LND_AttributionRules</c>): sujeto, condiciones (nula = no importa) y nivel mínimo por código. Casa si casan
/// todas sus condiciones no nulas; los rangos incluyen sus extremos. (nuevo)
/// </summary>
public sealed record ReglaDeAtribucion(int Orden, string Sujeto, string NivelMinimo)
{
    public string? Linea { get; init; }
    public string? Modalidad { get; init; }
    public decimal? MontoMinimo { get; init; }
    public decimal? MontoMaximo { get; init; }
    public int? PlazoMinimo { get; init; }
    public int? PlazoMaximo { get; init; }
    public CollateralKind? ClaseDeGarantia { get; init; }
    public bool? PuntajeBajoElMinimo { get; init; }
    public bool? ConAlertas { get; init; }
    public bool? ExcepcionALaPolitica { get; init; }
    public int? MoraMinima { get; init; }
    public int? MoraMaxima { get; init; }
    public LoanConcept? Concepto { get; init; }

    /// <summary>Mecanismo de la novedad (K3, T0879); nulo = no importa.</summary>
    public NoveltyMechanism? Mecanismo { get; init; }

    /// <summary>Causa de la novedad (K3, T0879); nulo = no importa.</summary>
    public NoveltyCause? Causa { get; init; }
}

/// <summary>La matriz vigente como datos: niveles y reglas. (nuevo)</summary>
public sealed record MatrizDeAtribuciones(IReadOnlyList<NivelDeAtribucion> Niveles, IReadOnlyList<ReglaDeAtribucion> Reglas);

/// <summary>
/// Lo que se enruta (T0277, nuevo): el sujeto (<see cref="LendingDecisionSubjects"/>) y su monto <b>consolidado</b> (lo pedido más la
/// exposición del deudor y lo que respalda como codeudor; lo arma US2), con las condiciones que miran las reglas, si hay un vinculado y
/// los hallazgos no exceptuables. Líneas y modalidades van por su código de la cooperativa.
/// </summary>
public sealed record SujetoAEnrutar(string Sujeto, decimal MontoConsolidado)
{
    public string? Linea { get; init; }
    public string? Modalidad { get; init; }
    public int? PlazoMeses { get; init; }
    public IReadOnlyCollection<CollateralKind> ClasesDeGarantia { get; init; } = [];
    public bool PuntajeBajoElMinimo { get; init; }
    public bool ConAlertas { get; init; }
    public bool ExcepcionALaPolitica { get; init; }
    public int? DiasDeMora { get; init; }
    public LoanConcept? Concepto { get; init; }

    /// <summary>Mecanismo y causa de la novedad (sujeto <c>Novelty</c>, K3); nulos en los demás sujetos.</summary>
    public NoveltyMechanism? Mecanismo { get; init; }
    public NoveltyCause? Causa { get; init; }
    public bool HayVinculado { get; init; }
    public IReadOnlyCollection<MotivoNoExceptuable> Hallazgos { get; init; } = [];
}

/// <summary>Por qué un sujeto llega a un nivel (los nombres de <c>POST /attribution-matrices/preview</c>). (nuevo)</summary>
public enum RazonDeEnrutamiento
{
    AmountCeiling = 1,
    Rule = 2,
    RelatedParty = 3,
    Exception = 4,
}

/// <summary>Una razón con el nivel al que lleva y su explicación en español. (nuevo)</summary>
public sealed record RazonDeAtribucion(RazonDeEnrutamiento Clase, NivelDeAtribucion Nivel, string Detalle);

/// <summary>
/// El enrutamiento (T0277, nuevo): el nivel efectivo —el mayor por rango entre todas las razones—, las razones en orden (techo, reglas
/// por su orden, vinculado, excepción) y los motivos no exceptuables. Con alguno de éstos el sujeto no se puede aprobar en ningún nivel.
/// </summary>
public sealed record EnrutamientoDeAtribucion(
    NivelDeAtribucion NivelEfectivo,
    IReadOnlyList<RazonDeAtribucion> Razones,
    IReadOnlyList<MotivoNoExceptuable> NoExceptuables)
{
    public bool EsExceptuable => NoExceptuables.Count == 0;
}

/// <summary>
/// Motor puro de las atribuciones de Cartera (feature 013, T0277; T63, R132, FR-037, FR-038, FR-040, FR-094). El nivel efectivo es el
/// <b>mayor</b> por rango entre: el primer nivel (de menor rango) cuyo techo cubre el monto consolidado; el nivel mínimo de cada regla
/// del sujeto que casa; el consejo si hay un vinculado (forzado, FR-040); y el nivel de excepción si el puntaje está bajo el mínimo o hay
/// alertas (FR-037). Los niveles no se acumulan: el sujeto va solo a ése. Sin IO; sólo <c>decimal</c>. (nuevo)
/// </summary>
public static class EnrutadorDeAtribuciones
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static EnrutamientoDeAtribucion Enrutar(SujetoAEnrutar sujeto, MatrizDeAtribuciones matriz)
    {
        ArgumentNullException.ThrowIfNull(sujeto);
        ArgumentNullException.ThrowIfNull(matriz);
        if (!LendingDecisionSubjects.Existe(sujeto.Sujeto))
            throw new ArgumentException($"«{sujeto.Sujeto}» no es un sujeto de decisión de Cartera.", nameof(sujeto));

        var niveles = matriz.Niveles.OrderBy(n => n.Rango).ToList();
        var porCodigo = niveles.ToDictionary(n => n.Codigo, StringComparer.Ordinal);
        var razones = new List<RazonDeAtribucion>();
        var monto = sujeto.MontoConsolidado;

        // 1. Techo de monto consolidado: el primer nivel cuyo techo («hasta», incluido) lo cubre; sin techo cubre todo.
        var porTecho = niveles.FirstOrDefault(n => n.TechoConsolidado is null || monto <= n.TechoConsolidado.Value)
            ?? throw new InvalidOperationException(
                $"Lending.Attribution.MatrixWithoutTopLevel: ningún nivel cubre el monto consolidado {Monto(monto)}; la matriz debe " +
                "terminar en un nivel colegiado sin techo.");
        razones.Add(new(RazonDeEnrutamiento.AmountCeiling, porTecho, porTecho.TechoConsolidado is { } techo
            ? $"Monto consolidado {Monto(monto)} dentro del techo de {porTecho.Nombre} ({Monto(techo)})."
            : $"Monto consolidado {Monto(monto)} por encima de todos los techos: {porTecho.Nombre}."));

        // 2. Reglas del sujeto que casan, en su orden.
        foreach (var regla in matriz.Reglas.Where(r => string.Equals(r.Sujeto, sujeto.Sujeto, StringComparison.Ordinal)).OrderBy(r => r.Orden))
        {
            if (!porCodigo.TryGetValue(regla.NivelMinimo, out var nivel))
                throw new ArgumentException($"La regla {regla.Orden.ToString(Inv)} nombra el nivel «{regla.NivelMinimo}», que no está en la matriz.",
                    nameof(matriz));
            if (Casa(regla, sujeto, out var condiciones))
                razones.Add(new(RazonDeEnrutamiento.Rule, nivel, $"Regla {regla.Orden.ToString(Inv)} ({condiciones}): mínimo {nivel.Nombre}."));
        }

        // 3. Vinculado: al consejo, forzado.
        if (sujeto.HayVinculado)
        {
            var consejo = niveles.SingleOrDefault(n => n.EsConsejo)
                ?? throw new InvalidOperationException("La matriz no tiene nivel de consejo y el sujeto tiene un vinculado (FR-040).");
            razones.Add(new(RazonDeEnrutamiento.RelatedParty, consejo, $"Hay una persona vinculada: decide {consejo.Nombre} (FR-040)."));
        }

        // 4. Excepción: puntaje bajo el mínimo o alertas.
        if (sujeto.PuntajeBajoElMinimo || sujeto.ConAlertas)
        {
            var excepcion = niveles.SingleOrDefault(n => n.EsDeExcepcion)
                ?? throw new InvalidOperationException("La matriz no tiene nivel de excepción y el sujeto lo necesita (FR-037).");
            var porQue = (sujeto.PuntajeBajoElMinimo, sujeto.ConAlertas) switch
            {
                (true, true) => "puntaje bajo el mínimo y alertas",
                (true, false) => "puntaje bajo el mínimo",
                _ => "alertas",
            };
            razones.Add(new(RazonDeEnrutamiento.Exception, excepcion, $"Excepción por {porQue}: mínimo {excepcion.Nombre} (FR-037)."));
        }

        var efectivo = razones.Select(r => r.Nivel).MaxBy(n => n.Rango)!;
        return new EnrutamientoDeAtribucion(efectivo, razones, NoExceptuables.Evaluar(sujeto.Hallazgos));
    }

    private static bool Casa(ReglaDeAtribucion r, SujetoAEnrutar s, out string condiciones)
    {
        var partes = new List<string>();
        condiciones = string.Empty;

        if (r.Linea is { } linea)
        {
            if (!string.Equals(linea, s.Linea, StringComparison.Ordinal)) return false;
            partes.Add($"línea {linea}");
        }
        if (r.Modalidad is { } modalidad)
        {
            if (!string.Equals(modalidad, s.Modalidad, StringComparison.Ordinal)) return false;
            partes.Add($"modalidad {modalidad}");
        }
        if (r.MontoMinimo is { } montoMin)
        {
            if (s.MontoConsolidado < montoMin) return false;
            partes.Add($"monto desde {Monto(montoMin)}");
        }
        if (r.MontoMaximo is { } montoMax)
        {
            if (s.MontoConsolidado > montoMax) return false;
            partes.Add($"monto hasta {Monto(montoMax)}");
        }
        if (r.PlazoMinimo is { } plazoMin)
        {
            if (s.PlazoMeses is not { } plazo || plazo < plazoMin) return false;
            partes.Add($"plazo desde {plazoMin.ToString(Inv)} meses");
        }
        if (r.PlazoMaximo is { } plazoMax)
        {
            if (s.PlazoMeses is not { } plazo || plazo > plazoMax) return false;
            partes.Add($"plazo hasta {plazoMax.ToString(Inv)} meses");
        }
        if (r.ClaseDeGarantia is { } garantia)
        {
            if (!s.ClasesDeGarantia.Contains(garantia)) return false;
            partes.Add($"garantía {garantia}");
        }
        if (r.PuntajeBajoElMinimo is { } puntaje)
        {
            if (s.PuntajeBajoElMinimo != puntaje) return false;
            partes.Add(puntaje ? "puntaje bajo el mínimo" : "puntaje suficiente");
        }
        if (r.ConAlertas is { } alertas)
        {
            if (s.ConAlertas != alertas) return false;
            partes.Add(alertas ? "con alertas" : "sin alertas");
        }
        if (r.ExcepcionALaPolitica is { } politica)
        {
            if (s.ExcepcionALaPolitica != politica) return false;
            partes.Add(politica ? "excepción a la política" : "dentro de la política");
        }
        if (r.MoraMinima is { } moraMin)
        {
            if (s.DiasDeMora is not { } mora || mora < moraMin) return false;
            partes.Add($"mora desde {moraMin.ToString(Inv)} días");
        }
        if (r.MoraMaxima is { } moraMax)
        {
            if (s.DiasDeMora is not { } mora || mora > moraMax) return false;
            partes.Add($"mora hasta {moraMax.ToString(Inv)} días");
        }
        if (r.Concepto is { } concepto)
        {
            if (s.Concepto != concepto) return false;
            partes.Add($"concepto {concepto}");
        }
        if (r.Mecanismo is { } mecanismo)
        {
            if (s.Mecanismo != mecanismo) return false;
            partes.Add($"mecanismo {mecanismo}");
        }
        if (r.Causa is { } causa)
        {
            if (s.Causa != causa) return false;
            partes.Add($"causa {causa}");
        }

        condiciones = partes.Count == 0 ? $"todo {s.Sujeto}" : string.Join(", ", partes);
        return true;
    }

    private static string Monto(decimal valor) => valor.ToString("#,0.##", Inv);
}
