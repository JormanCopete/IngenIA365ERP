using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Governance;

// Feature 013 (T0280–T0283): lo que reciben y devuelven las fábricas de las entidades de gobierno de Cartera. Viven fuera de
// Domain.Entities porque no son tablas (PrincipioVII_SoftDeleteAndAuditable, LosSaldosYTasasDeCarteraTienenSuPrecision). (nuevo)

/// <summary>Lo que la matriz necesita saber de un órgano para nombrarlo en un nivel colegiado. (nuevo)</summary>
public sealed record OrganoDeMatriz(GoverningBodyKind Kind, bool DecidesCredit, bool IsActive);

/// <summary>La matriz que se quiere publicar: fecha, motivo, fuente, niveles y reglas (api.md §3.5.1). (nuevo)</summary>
public sealed record BorradorDeMatriz(
    DateOnly ValidFrom, string Reason, string? LegalSource, IReadOnlyList<BorradorDeNivel> Niveles, IReadOnlyList<BorradorDeRegla> Reglas)
{
    /// <summary>Punto de acta que la aprobó, si la aprobó un órgano.</summary>
    public int? SessionItemId { get; init; }
}

/// <summary>Un nivel de la matriz antes de publicarla (columnas de data-model §3.2). (nuevo)</summary>
public sealed record BorradorDeNivel(
    string Code,
    string Name,
    int Rank,
    AttributionLevelKind Kind,
    string? PermissionCode,
    int? GoverningBodyId,
    decimal? MaxConsolidatedAmount,
    bool IsExceptionLevel,
    bool IsBoardLevel)
{
    public static BorradorDeNivel De(AttributionLevel n) => new(n.Code, n.Name, n.Rank, n.Kind, n.PermissionCode, n.GoverningBodyId,
        n.MaxConsolidatedAmount, n.IsExceptionLevel, n.IsBoardLevel);
}

/// <summary>Una regla de la matriz antes de publicarla, con su nivel mínimo por código (columnas de data-model §3.3). (nuevo)</summary>
public sealed record BorradorDeRegla(int Order, string Subject, string MinimumLevelCode)
{
    public int? CreditLineId { get; init; }
    public string? ModalityCode { get; init; }
    public decimal? MinConsolidatedAmount { get; init; }
    public decimal? MaxConsolidatedAmount { get; init; }
    public int? MinTermMonths { get; init; }
    public int? MaxTermMonths { get; init; }
    public CollateralKind? CollateralKind { get; init; }
    public bool? ScoreBelowMinimum { get; init; }
    public bool? HasWarnings { get; init; }
    public bool? PolicyException { get; init; }
    public int? MinDaysPastDue { get; init; }
    public int? MaxDaysPastDue { get; init; }
    public LoanConcept? Concept { get; init; }

    public static BorradorDeRegla De(AttributionRule r, string codigoDelNivelMinimo) => new(r.Order, r.Subject, codigoDelNivelMinimo)
    {
        CreditLineId = r.CreditLineId,
        ModalityCode = r.ModalityCode,
        MinConsolidatedAmount = r.MinConsolidatedAmount,
        MaxConsolidatedAmount = r.MaxConsolidatedAmount,
        MinTermMonths = r.MinTermMonths,
        MaxTermMonths = r.MaxTermMonths,
        CollateralKind = r.CollateralKind,
        ScoreBelowMinimum = r.ScoreBelowMinimum,
        HasWarnings = r.HasWarnings,
        PolicyException = r.PolicyException,
        MinDaysPastDue = r.MinDaysPastDue,
        MaxDaysPastDue = r.MaxDaysPastDue,
        Concept = r.Concept,
    };
}

/// <summary>
/// La matriz publicada y lo necesario para armar sus hijas cuando la base le asigne el Id: los niveles con el Id de la matriz y las
/// reglas con el Id de cada nivel por su código (las tablas no tienen navegaciones). (nuevo)
/// </summary>
public sealed class MatrizPublicada(AttributionMatrix matriz, BorradorDeMatriz borrador)
{
    public AttributionMatrix Matriz { get; } = matriz;

    /// <summary>Los niveles de la matriz ya guardada (<paramref name="matrixId"/> &gt; 0).</summary>
    public IReadOnlyList<AttributionLevel> Niveles(int matrixId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(matrixId, 1);
        return borrador.Niveles.Select(n => AttributionLevel.Nuevo(matrixId, n)).ToList();
    }

    /// <summary>Las reglas, con el Id de cada nivel ya guardado por su código (sin distinguir mayúsculas).</summary>
    public IReadOnlyList<AttributionRule> Reglas(int matrixId, IReadOnlyDictionary<string, int> idDeNivelPorCodigo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(matrixId, 1);
        var ids = new Dictionary<string, int>(idDeNivelPorCodigo, StringComparer.OrdinalIgnoreCase);
        return borrador.Reglas.Select(r => AttributionRule.Nueva(matrixId, ids.TryGetValue(r.MinimumLevelCode.Trim(), out var id)
            ? id
            : throw new ReglaDeCarteraException(ProblemaDeCartera.Con(AttributionMatrix.CodigoNivelDesconocido,
                $"No se guardó el nivel «{r.MinimumLevelCode}» de la regla {r.Order.ToString(CultureInfo.InvariantCulture)}.", "levelCode", r.MinimumLevelCode)), r))
            .ToList();
    }
}

/// <summary>La regla de quórum y mayorías que se quiere registrar (api.md §3.6, columnas de data-model §3.6). (nuevo)</summary>
public sealed record BorradorDeReglasDeOrgano(
    DateOnly ValidFrom,
    int QuorumNumerator,
    int QuorumDenominator,
    FractionComparison QuorumComparison,
    int MajorityNumerator,
    int MajorityDenominator,
    FractionComparison MajorityComparison,
    MajorityBase MajorityBase,
    int? RelatedPartyNumerator,
    int? RelatedPartyDenominator,
    string Source,
    string Reason);

/// <summary>El nombramiento que se quiere registrar (columnas de data-model §3.7). (nuevo)</summary>
public sealed record BorradorDeMiembro(
    int GoverningBodyId,
    int PersonId,
    BodyMemberRole Role,
    bool HasVote,
    bool IsPrincipal,
    int? AlternateForMemberId,
    DateOnly ValidFrom,
    DateOnly? ValidTo = null)
{
    public string? AppointmentNotes { get; init; }
    public Guid? AppointmentAttachmentPublicId { get; init; }
}

/// <summary>Lo que se decide sobre un sujeto, sin el decisor (columnas de data-model §3.4). (nuevo)</summary>
public sealed record LoDecidido(
    string SubjectType,
    Guid SubjectPublicId,
    Guid CaseRootPublicId,
    CreditDecisionOutcome Outcome,
    decimal? ConsolidatedAmount = null,
    string? RoutingSha256 = null)
{
    public int? AttributionMatrixId { get; init; }
    public int? LevelId { get; init; }
    public string? Conditions { get; init; }
    public string? Reason { get; init; }
    public bool IsPolicyException { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public int? SupersedesDecisionId { get; init; }
}

/// <summary>El vínculo que se quiere registrar (columnas de data-model §3.12). (nuevo)</summary>
public sealed record BorradorDeVinculado(int PersonId, RelatedPartyBasis Basis, DateOnly ValidFrom, RelatedPartySource Source = RelatedPartySource.Manual)
{
    public int? RelatedThroughPersonId { get; init; }
    public KinshipKind? KinshipKind { get; init; }
    public byte? KinshipDegree { get; init; }
    public decimal? ContributionPercentage { get; init; }
    public DateOnly? ValidTo { get; init; }
    public Guid? SupportAttachmentPublicId { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Una persona excluida de un punto (por su <c>PublicId</c>) con su motivo: <c>Filer</c>, <c>Applicant</c>, <c>Interested</c>. (nuevo)</summary>
public sealed record ExclusionDePunto(Guid PersonPublicId, string Motivo)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>JSON canónico (orden recibido, sin repetidos por persona; la primera razón manda); nulo si no hay excluidos.</summary>
    public static string? ComoJson(IReadOnlyList<ExclusionDePunto> excluidos)
    {
        var unicos = excluidos.GroupBy(e => e.PersonPublicId).Select(g => g.First()).ToList();
        if (unicos.Count == 0) return null;
        if (unicos.Any(e => string.IsNullOrWhiteSpace(e.Motivo)))
            throw new ArgumentException("Cada excluido lleva su motivo.", nameof(excluidos));
        return JsonSerializer.Serialize(unicos.Select(e => new Fila(e.PersonPublicId, e.Motivo.Trim())), Json);
    }

    public static IReadOnlyList<ExclusionDePunto> DeJson(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : (JsonSerializer.Deserialize<List<Fila>>(json, Json) ?? []).Select(f => new ExclusionDePunto(f.PersonPublicId, f.Motivo)).ToList();

    private sealed record Fila(Guid PersonPublicId, string Motivo);
}

/// <summary>Lo que hizo <see cref="UserCreditLineScope.Reemplazar"/>: un problema, o las filas nuevas y las dadas de baja. (nuevo)</summary>
public sealed record ResultadoDeAlcanceDeLineas(
    ProblemaDeCartera? Problema, IReadOnlyList<UserCreditLineScope> Agregadas, IReadOnlyList<UserCreditLineScope> Retiradas);
