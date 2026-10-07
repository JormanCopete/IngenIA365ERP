namespace IngenIA365ERP.Domain.Enums.Lending;

// Riesgo y cierres (feature 013, T0201; data-model §9.1 y parte 2 «Nombres fijados», C65). Los de K3 y K6a existen desde K1
// (data-model §0.14); ningún comando de K1 los escribe.

/// <summary>Categoría de riesgo A a E.</summary>
public enum RatingCategory
{
    A = 1,
    B = 2,
    C = 3,
    D = 4,
    E = 5,
}

/// <summary>Subcategoría de E (Anexo 1).</summary>
public enum RatingSubcategory
{
    None = 0,
    E1 = 1,
    E2 = 2,
}

/// <summary>Causa de la calificación de un crédito en un cierre.</summary>
public enum RatingCause
{
    Arrears = 1,
    Model = 2,
    Drag = 3,
    Restructured = 4,
    Evaluation = 5,
    Analysis = 6,
    SingleInstallment = 7,
    NewLoan = 8,
    Insolvency = 9,
    Homologation = 10,
}

/// <summary>Modelo de deterioro aplicado.</summary>
public enum ImpairmentModel
{
    ArrearsAnnex1 = 1,
    ExpectedLossAnnex2 = 2,
}

/// <summary>Por qué rige el modelo.</summary>
public enum ModelBasis
{
    NotObligated = 1,
    Obligated = 2,
    Voluntary = 3,
}

/// <summary>Cierre base (activación) o mensual.</summary>
public enum MonthlyCloseKind
{
    Base = 1,
    Monthly = 2,
}

/// <summary>Estado de un cierre: <b>calculado</b>, no es columna (<c>Reopened</c> si tiene reapertura, T47).</summary>
public enum MonthlyCloseStatus
{
    Closed = 1,
    Reopened = 2,
}

/// <summary>Origen de una fila del historial de mora.</summary>
public enum ArrearsHistoryOrigin
{
    Migration = 1,
    ComplementaryLoad = 2,
    MonthlyClose = 3,
}

/// <summary>Adopción o desistimiento voluntario de la pérdida esperada (K6a).</summary>
public enum ImpairmentModelElectionKind
{
    VoluntaryAdoption = 1,
    VoluntaryWithdrawal = 2,
}

/// <summary>Estado de una evaluación de cartera (K3).</summary>
public enum PortfolioEvaluationStatus
{
    Draft = 1,
    Recommended = 2,
    Approved = 3,
    Applied = 4,
    Rejected = 5,
    Discarded = 6,
}

/// <summary>Estado de una versión de la política SARC: <b>calculado</b> (§23.10).</summary>
public enum RiskPolicyVersionStatus
{
    PendingValidation = 1,
    Validated = 2,
}
