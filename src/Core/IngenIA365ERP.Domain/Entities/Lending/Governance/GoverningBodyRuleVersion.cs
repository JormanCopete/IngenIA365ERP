using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyRuleVersions</c> (feature 013, T0213; data-model §3.6; C44; FR-039, FR-040, F7, T42): quórum y mayorías del
/// órgano con vigencia, en fracciones exactas (numerador/denominador) para que <c>EvaluadorDeVotacion</c> compare en enteros. Hecho
/// de sólo inserción (C42), leído con <see cref="VigenciaBitemporal"/>.
///
/// <para>
/// T0281: nace por <see cref="Registrar"/> (fracciones 0 &lt; n ≤ d, la de vinculados completa o ausente y sólo en el consejo,
/// <c>Lending.GoverningBody.RulesInvalid</c>); corregirla es otra versión. <see cref="ReglaSellada"/> es lo que la sesión sella al
/// abrir para <see cref="EvaluadorDeVotacion"/>, y <see cref="EsMenosEstrictaQue"/> compara la fracción de vinculados con el límite
/// nacional <c>VINCULADOS_FRACCION_CALIFICADA</c> (el comando lo lee y responde <c>Lending.Parameter.BelowLegalFloor</c>).
/// </para>
/// </summary>
public class GoverningBodyRuleVersion : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int GoverningBodyId { get; init; }

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Quórum sobre la composición vigente.</summary>
    public int QuorumNumerator { get; init; }

    public int QuorumDenominator { get; init; }

    public FractionComparison QuorumComparison { get; init; }

    /// <summary>Mayoría ordinaria.</summary>
    public int MajorityNumerator { get; init; }

    public int MajorityDenominator { get; init; }

    public FractionComparison MajorityComparison { get; init; }

    public MajorityBase MajorityBase { get; init; }

    /// <summary>Sólo consejo: mayoría calificada con vinculados, sobre la composición; con actividad financiera, ≥ el límite nacional.</summary>
    public int? RelatedPartyNumerator { get; init; }

    public int? RelatedPartyDenominator { get; init; }

    /// <summary>Estatutos o reglamento.</summary>
    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public const string CodigoInvalida = "Lending.GoverningBody.RulesInvalid";

    /// <summary>Lo que está mal en la regla, o nulo.</summary>
    public static ProblemaDeCartera? Validar(BorradorDeReglasDeOrgano r, GoverningBodyKind claseDelOrgano)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (!FraccionValida(r.QuorumNumerator, r.QuorumDenominator))
            return Invalida("QuorumFraction", $"El quórum {r.QuorumNumerator}/{r.QuorumDenominator} no es una fracción entre 0 y 1.");
        if (!FraccionValida(r.MajorityNumerator, r.MajorityDenominator))
            return Invalida("MajorityFraction", $"La mayoría {r.MajorityNumerator}/{r.MajorityDenominator} no es una fracción entre 0 y 1.");
        if (!Enum.IsDefined(r.QuorumComparison) || !Enum.IsDefined(r.MajorityComparison) || !Enum.IsDefined(r.MajorityBase))
            return Invalida("ComparisonUnknown", "La comparación o la base de la mayoría no es una de las admitidas.");
        if (r.RelatedPartyNumerator is null != r.RelatedPartyDenominator is null)
            return Invalida("RelatedPartyFractionIncomplete", "La fracción de vinculados lleva numerador y denominador, o ninguno.");
        if (r.RelatedPartyNumerator is { } n && r.RelatedPartyDenominator is { } d)
        {
            if (claseDelOrgano != GoverningBodyKind.BoardOfDirectors)
                return Invalida("RelatedPartyFractionNotBoard", "Sólo el consejo de administración lleva la mayoría calificada de vinculados.");
            if (!FraccionValida(n, d))
                return Invalida("RelatedPartyFraction", $"La fracción de vinculados {n}/{d} no es una fracción entre 0 y 1.");
        }
        if (string.IsNullOrWhiteSpace(r.Source) || string.IsNullOrWhiteSpace(r.Reason))
            return Invalida("SourceRequired", "La regla lleva su fuente (estatutos o reglamento) y el motivo.");
        return null;
    }

    /// <summary>Registra la versión <paramref name="versionNumber"/> de la regla del órgano (lanza si no se validó antes).</summary>
    public static GoverningBodyRuleVersion Registrar(
        int governingBodyId, GoverningBodyKind claseDelOrgano, int versionNumber, BorradorDeReglasDeOrgano r, DateTime registeredAt)
    {
        ReglaDeCarteraException.SiHay(Validar(r, claseDelOrgano));
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        return new GoverningBodyRuleVersion
        {
            GoverningBodyId = governingBodyId,
            VersionNumber = versionNumber,
            ValidFrom = r.ValidFrom,
            RegisteredAt = registeredAt,
            QuorumNumerator = r.QuorumNumerator,
            QuorumDenominator = r.QuorumDenominator,
            QuorumComparison = r.QuorumComparison,
            MajorityNumerator = r.MajorityNumerator,
            MajorityDenominator = r.MajorityDenominator,
            MajorityComparison = r.MajorityComparison,
            MajorityBase = r.MajorityBase,
            RelatedPartyNumerator = r.RelatedPartyNumerator,
            RelatedPartyDenominator = r.RelatedPartyDenominator,
            Source = r.Source.Trim(),
            Reason = r.Reason.Trim(),
        };
    }

    /// <summary>La regla que sella la sesión al abrir, en los tipos de <see cref="EvaluadorDeVotacion"/>.</summary>
    public ReglaDeVotacionSellada ReglaSellada() => new(
        new Fraccion(QuorumNumerator, QuorumDenominator), QuorumComparison,
        new Fraccion(MajorityNumerator, MajorityDenominator), MajorityComparison, MajorityBase,
        RelatedPartyNumerator is { } n && RelatedPartyDenominator is { } d ? new Fraccion(n, d) : null);

    /// <summary>
    /// Si la fracción de vinculados es menos estricta que el límite nacional (una fracción decimal, p. ej. 0,8 = 4/5). Sin fracción
    /// propia no hay nada que comparar: con vinculados rige el límite. Compara <c>n ≥ límite × d</c> sin redondear.
    /// </summary>
    public bool EsMenosEstrictaQue(decimal limiteDeVinculados) =>
        RelatedPartyNumerator is { } n && RelatedPartyDenominator is { } d && n < limiteDeVinculados * d;

    private static bool FraccionValida(int numerador, int denominador) => denominador > 0 && numerador > 0 && numerador <= denominador;

    private static ProblemaDeCartera Invalida(string motivo, string mensaje) => ProblemaDeCartera.Con(CodigoInvalida, mensaje, "reason", motivo);
}
