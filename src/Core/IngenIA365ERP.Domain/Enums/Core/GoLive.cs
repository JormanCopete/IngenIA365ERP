namespace IngenIA365ERP.Domain.Enums.Core;

/// <summary>El estado de una lista de go/no-go (feature 015, T0033; R-28; FR-227; data-model §16.2). (nuevo)</summary>
public enum GoLiveChecklistStatus
{
    /// <summary>Abierta: se verifican sus condiciones.</summary>
    Open = 1,

    /// <summary>Se sale en vivo.</summary>
    Go = 2,

    /// <summary>No se sale.</summary>
    NoGo = 3,

    /// <summary>Se sale con una parte, con el escenario escrito y el plan de reversa adjuntos (P15).</summary>
    PartialGo = 4,
}

/// <summary>Cómo se verifica una condición de la lista (data-model §16.2). (nuevo)</summary>
public enum GoLiveItemKind
{
    /// <summary>La aporta un módulo con su <c>IVerificacionDeSalida</c>.</summary>
    Automatic = 1,

    /// <summary>La marca una persona con su evidencia.</summary>
    Manual = 2,
}

/// <summary>El resultado de una condición (data-model §16.2). Un <see cref="Red"/> nunca se oculta. (nuevo)</summary>
public enum GoLiveItemStatus
{
    Green = 1,
    Red = 2,
    NotApplicable = 3,
}
