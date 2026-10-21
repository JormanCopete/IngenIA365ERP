namespace IngenIA365ERP.Domain.Enums.Lending;

// Gobierno: atribuciones, órganos, sesiones, vinculados y grupo conectado (feature 013, T0201, T0213; data-model parte 1 §3 y §10;
// DT §7.10 C112).

/// <summary>Resultado de una decisión de crédito (S4).</summary>
public enum CreditDecisionOutcome
{
    Approve = 1,
    ApproveWithConditions = 2,
    Deny = 3,
    Return = 4,
}

/// <summary>Fundamento del vínculo de una persona vinculada (Ley 454, art. 61).</summary>
public enum RelatedPartyBasis
{
    BoardMember = 1,
    OversightBoardMember = 2,
    LegalRepresentative = 3,
    SignificantContributor = 4,
    Spouse = 5,
    Relative = 6,
}

/// <summary>Nivel individual (un usuario con permiso) o colegiado (un órgano).</summary>
public enum AttributionLevelKind
{
    Individual = 1,
    Collegiate = 2,
}

/// <summary>Clase de órgano de decisión.</summary>
public enum GoverningBodyKind
{
    BoardOfDirectors = 1,
    OversightBoard = 2,
    CreditCommittee = 3,
    RiskCommittee = 4,
    Management = 5,
    Other = 99,
}

/// <summary>Cómo se compara una fracción de quórum o mayoría.</summary>
public enum FractionComparison
{
    AtLeast = 1,
    MoreThan = 2,
}

/// <summary>Sobre qué base se cuenta la mayoría.</summary>
public enum MajorityBase
{
    PresentEligible = 1,
    Composition = 2,
}

/// <summary>Papel de un miembro del órgano.</summary>
public enum BodyMemberRole
{
    Chair = 1,
    Secretary = 2,
    Member = 3,
    Alternate = 4,
}

/// <summary>Sesión ordinaria o extraordinaria.</summary>
public enum SessionKind
{
    Ordinary = 1,
    Extraordinary = 2,
}

/// <summary>Modalidad de la sesión.</summary>
public enum SessionMode
{
    InPerson = 1,
    Virtual = 2,
    Mixed = 3,
}

/// <summary>Estado de la sesión.</summary>
public enum SessionStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3,
}

/// <summary>Asistencia a la sesión.</summary>
public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Excused = 3,
}

/// <summary>Punto que decide o que sólo recomienda.</summary>
public enum SessionItemKind
{
    Decision = 1,
    Recommendation = 2,
}

/// <summary>Regla de votación de un punto (FR-040: calificada con vinculados).</summary>
public enum VotingRule
{
    Majority = 1,
    RelatedPartyQualified = 2,
}

/// <summary>Sentido del voto.</summary>
public enum VoteValue
{
    For = 1,
    Against = 2,
    Abstain = 3,
}

/// <summary>Cómo quedó registrado un voto (F8).</summary>
public enum VoteMethod
{
    RecordedBySecretary = 1,
    OwnSession = 2,
    InPersonPasskey = 3,
    InPersonTotp = 4,
}

/// <summary>Clase de parentesco de un vinculado.</summary>
public enum KinshipKind
{
    Consanguinity = 1,
    Affinity = 2,
    Civil = 3,
}

/// <summary>De dónde salió un vinculado.</summary>
public enum RelatedPartySource
{
    Manual = 1,
    Import = 2,
    Migration = 3,
}

/// <summary>Fundamento de un grupo conectado registrado (FR-041, SC-022; C112). (nuevo)</summary>
public enum ConnectedGroupBasis
{
    EconomicLink = 1,
    LegalLink = 2,
    CommonCoDebtor = 3,
    RelatedParty = 4,
}
