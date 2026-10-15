using System.Globalization;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>De dónde sale un requisito del castigo: lo que exige la norma o lo que fija la política de la cooperativa (FR-095).</summary>
public enum OrigenDeRequisitoDeCastigo
{
    Norma = 1,
    Politica = 2,
}

/// <summary>
/// La política de castigo vigente (parámetros <c>Castigo.*</c>, K3, T0910): si exige el deterioro total, la altura mínima de mora (nula =
/// el dueño no la ha fijado: no se exige) y las gestiones mínimas (desde K4; en K3 las acredita el informe motivado).
/// </summary>
public sealed record PoliticaDeCastigo(bool ExigeDeterioroTotal, int? DiasMinimosDeMora, int GestionesMinimas)
{
    /// <summary>¿Ya existe el registro de gestiones (K4)? Sin él, el informe motivado acredita las gestiones.</summary>
    public bool GestionesRegistradas { get; init; }
}

/// <summary>
/// Lo que se sabe de un crédito a la fecha de corte del lote: la condición de asociado del deudor, el cruce de sus aportes (nulo = la consulta
/// no está disponible mientras rija D19: lo acredita un soporte adjunto), los adjuntos del informe motivado y del concepto de
/// irrecuperabilidad, el soporte del cruce, el deterioro individual del último cierre frente al saldo, la altura de mora y las gestiones.
/// </summary>
public sealed record CreditoParaCastigar(int LoanId, bool DeudorEsAsociado, decimal Saldo, decimal DeterioroDelUltimoCierre, int DiasDeMora)
{
    /// <summary>Aportes cruzados: verdadero o falso si la consulta respondió; nulo si no está disponible (D19).</summary>
    public bool? AportesCruzados { get; init; }

    public Guid? SoporteDeCruceDeAportes { get; init; }

    public Guid? InformeMotivado { get; init; }

    public Guid? ConceptoDeIrrecuperabilidad { get; init; }

    public int Gestiones { get; init; }
}

/// <summary>Un requisito verificado: su clave estable, de dónde sale, si se cumple y la evidencia en palabras.</summary>
public sealed record RequisitoDeCastigo(string Clave, OrigenDeRequisitoDeCastigo Origen, bool Cumple, string Evidencia);

/// <summary>
/// El resultado: cada requisito con su evidencia, la lista de los que faltan (<c>data.missing[]</c> de
/// <see cref="CodigoRequisitoFaltante"/>), el JSON canónico que va a <c>RequirementsJson</c> y la explicación.
/// </summary>
public sealed record VerificacionDeCastigo(int LoanId, IReadOnlyList<RequisitoDeCastigo> Requisitos, string RequisitosJson, ExplicacionDeCartera Explicacion)
{
    public IReadOnlyList<string> Faltantes => Requisitos.Where(r => !r.Cumple).Select(r => r.Clave).ToList();

    public bool Cumple => Requisitos.All(r => r.Cumple);

    /// <summary>Nulo si cumple; si no, <see cref="RequisitosDeCastigo.CodigoRequisitoFaltante"/> con <c>missing</c>.</summary>
    public ProblemaDeCartera? Problema => Cumple
        ? null
        : new ProblemaDeCartera(RequisitosDeCastigo.CodigoRequisitoFaltante,
            $"El crédito no cumple los requisitos del castigo: falta {string.Join(", ", Requisitos.Where(r => !r.Cumple).Select(r => r.Evidencia))}.",
            new Dictionary<string, object?> { ["missing"] = Faltantes });
}

/// <summary>
/// Los requisitos de un crédito para el castigo (feature 013, K3, T0904; FR-095, US14-1, C98):
/// <list type="bullet">
/// <item><b>Norma</b>: el deudor ya no es asociado (<see cref="NoEsAsociado"/>); sus aportes se cruzaron (<see cref="AportesCruzados"/>; si la
/// consulta de aportes no está disponible, D19, lo acredita un soporte adjunto); hay informe motivado (<see cref="InformeMotivado"/>) y
/// concepto de irrecuperabilidad (<see cref="ConceptoDeIrrecuperabilidad"/>).</item>
/// <item><b>Política</b>: deterioro individual del último cierre igual al saldo si la política lo exige (<see cref="DeterioroTotal"/>);
/// altura de mora mínima si el dueño la fijó (<see cref="AlturaMinima"/>); gestiones mínimas (<see cref="GestionesAgotadas"/>; mientras no
/// existan las gestiones de K4, el informe motivado las acredita).</item>
/// </list>
/// Puro y sin valores legales: los mínimos llegan en <see cref="PoliticaDeCastigo"/>. (nuevo)
/// </summary>
public static class RequisitosDeCastigo
{
    /// <summary>El crédito no cumple lo que la norma o la política exigen para castigarlo (C98).</summary>
    public const string CodigoRequisitoFaltante = "Lending.WriteOff.RequirementMissing";

    public const string NoEsAsociado = "NotAnAssociate";
    public const string AportesCruzados = "ContributionsOffset";
    public const string InformeMotivado = "ReasonedReport";
    public const string ConceptoDeIrrecuperabilidad = "IrrecoverabilityOpinion";
    public const string DeterioroTotal = "FullImpairment";
    public const string AlturaMinima = "MinimumDaysPastDue";
    public const string GestionesAgotadas = "CollectionEffortsExhausted";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static VerificacionDeCastigo Verificar(CreditoParaCastigar c, PoliticaDeCastigo politica)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(politica);
        var r = new List<RequisitoDeCastigo>
        {
            new(NoEsAsociado, OrigenDeRequisitoDeCastigo.Norma, !c.DeudorEsAsociado,
                c.DeudorEsAsociado ? "el deudor sigue siendo asociado" : "el deudor ya no es asociado"),
            Aportes(c),
            new(InformeMotivado, OrigenDeRequisitoDeCastigo.Norma, c.InformeMotivado is not null,
                c.InformeMotivado is { } i ? $"informe motivado {i:D}" : "el informe motivado"),
            new(ConceptoDeIrrecuperabilidad, OrigenDeRequisitoDeCastigo.Norma, c.ConceptoDeIrrecuperabilidad is not null,
                c.ConceptoDeIrrecuperabilidad is { } k ? $"concepto de irrecuperabilidad {k:D}" : "el concepto de irrecuperabilidad"),
        };

        if (politica.ExigeDeterioroTotal)
            r.Add(new(DeterioroTotal, OrigenDeRequisitoDeCastigo.Politica, c.DeterioroDelUltimoCierre >= c.Saldo,
                c.DeterioroDelUltimoCierre >= c.Saldo
                    ? $"deterioro del último cierre {ExplicacionDeCartera.Numero(c.DeterioroDelUltimoCierre)} cubre el saldo {ExplicacionDeCartera.Numero(c.Saldo)}"
                    : $"el deterioro total: el del último cierre es {ExplicacionDeCartera.Numero(c.DeterioroDelUltimoCierre)} y el saldo {ExplicacionDeCartera.Numero(c.Saldo)}"));

        if (politica.DiasMinimosDeMora is { } minimo)
            r.Add(new(AlturaMinima, OrigenDeRequisitoDeCastigo.Politica, c.DiasDeMora >= minimo,
                c.DiasDeMora >= minimo
                    ? $"{c.DiasDeMora.ToString(Inv)} días de mora (mínimo {minimo.ToString(Inv)})"
                    : $"la altura de mora mínima: tiene {c.DiasDeMora.ToString(Inv)} días y se piden {minimo.ToString(Inv)}"));

        if (politica.GestionesRegistradas)
            r.Add(new(GestionesAgotadas, OrigenDeRequisitoDeCastigo.Politica, c.Gestiones >= politica.GestionesMinimas,
                c.Gestiones >= politica.GestionesMinimas
                    ? $"{c.Gestiones.ToString(Inv)} gestiones registradas (mínimo {politica.GestionesMinimas.ToString(Inv)})"
                    : $"las gestiones: hay {c.Gestiones.ToString(Inv)} y se piden {politica.GestionesMinimas.ToString(Inv)}"));
        else
            r.Add(new(GestionesAgotadas, OrigenDeRequisitoDeCastigo.Politica, c.InformeMotivado is not null,
                c.InformeMotivado is not null
                    ? "las gestiones las acredita el informe motivado (sin registro de gestiones todavía)"
                    : "las gestiones: sin registro de gestiones, las acredita el informe motivado y no está"));

        var explicacion = new ExplicacionDeCartera();
        foreach (var x in r) explicacion.Linea($"{(x.Cumple ? "Cumple" : "Falta")} ({x.Origen}): {x.Evidencia}.");
        var json = new HuellaDeCartera()
            .Entero("loanId", c.LoanId)
            .Lista("requirements", r, (h, x) => h.Texto("key", x.Clave).Enumeracion<OrigenDeRequisitoDeCastigo>("source", x.Origen).Logico("met", x.Cumple).Texto("evidence", x.Evidencia))
            .Textos("missing", r.Where(x => !x.Cumple).Select(x => x.Clave))
            .Canonico();
        return new(c.LoanId, r, json, explicacion);
    }

    private static RequisitoDeCastigo Aportes(CreditoParaCastigar c) => c.AportesCruzados switch
    {
        true => new(AportesCruzados, OrigenDeRequisitoDeCastigo.Norma, true, "los aportes ya se cruzaron"),
        false => new(AportesCruzados, OrigenDeRequisitoDeCastigo.Norma, false, "el cruce de los aportes"),
        null => c.SoporteDeCruceDeAportes is { } s
            ? new(AportesCruzados, OrigenDeRequisitoDeCastigo.Norma, true, $"cruce de aportes acreditado con el soporte {s:D} (consulta de aportes no disponible)")
            : new(AportesCruzados, OrigenDeRequisitoDeCastigo.Norma, false, "el soporte del cruce de aportes (la consulta de aportes no está disponible)"),
    };
}
