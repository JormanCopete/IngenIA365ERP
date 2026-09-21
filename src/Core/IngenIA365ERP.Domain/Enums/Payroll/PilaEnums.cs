namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Estado de una generación de la planilla PILA (feature 010, US5; data-model.md §2.8).</summary>
public enum PilaGenerationStatus
{
    /// <summary>Se validó y hubo inconsistencias bloqueantes: no hay archivo.</summary>
    Validated = 0,

    /// <summary>Archivo generado y guardado; falta cargarlo en el operador.</summary>
    Generated = 1,

    /// <summary>Radicada en el operador (número y fecha digitados).</summary>
    Uploaded = 2,

    /// <summary>Reemplazada por una versión posterior del mismo período; conserva su archivo.</summary>
    Superseded = 3,
}

/// <summary>Severidad de una inconsistencia PILA, con la taxonomía de Aportes en Línea (Error / Alerta).</summary>
public enum PilaIssueSeverity
{
    /// <summary>Error del operador: no se genera el archivo hasta corregirla.</summary>
    Blocking = 1,

    /// <summary>Alerta: se genera, pero hay que reconocerla.</summary>
    Warning = 2,
}
