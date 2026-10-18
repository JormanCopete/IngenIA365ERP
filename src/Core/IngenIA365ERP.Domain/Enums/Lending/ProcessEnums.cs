namespace IngenIA365ERP.Domain.Enums.Lending;

// Integración, procesos y migración (feature 013, T0201; data-model §9.1 y parte 2 «Nombres fijados», C65).

/// <summary>Estado del recibo de un mensaje que llega a Cartera (PM-09).</summary>
public enum InboundMessageStatus
{
    Processed = 1,
    ValidationFailed = 2,
    Rejected = 3,
    NotIncorporated = 4,
    ConfirmedByException = 5,
}

/// <summary>Resultado de una evaluación de un mensaje recibido.</summary>
public enum InboundEvaluationOutcome
{
    Accepted = 1,
    ValidationFailed = 2,
    NotConvertible = 3,
    NotIncorporated = 4,
}

/// <summary>
/// Clase de una corrida de <c>LND_ProcessRuns</c> (PM-13, T28). Las cuatro primeras son las clases que conduce
/// <c>ProcesadorDeCartera</c> (C59); <see cref="ScheduledTask"/> (marca de la última ejecución de cada tarea diaria, SC-020) y
/// <see cref="MigratedCollectionStages"/> (registro de la etapa de cobranza migrada al desplegar K4) son el vocabulario de C123 y el
/// procesador no las toma. La activación no es corrida (C60).
/// </summary>
public enum LendingProcessKind
{
    DailyCycle = 1,
    MonthlyClose = 2,
    CloseReopen = 3,
    NationalValueCorrection = 4,
    ScheduledTask = 5,
    MigratedCollectionStages = 6,
}

/// <summary>Estado de una corrida. <c>Running = 1</c> es el filtro del único «una corriendo por cooperativa».</summary>
public enum LendingProcessRunStatus
{
    Requested = 0,
    Running = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Failed = 4,
}

/// <summary>De dónde sale una parte de hechos (<c>LND_AccountingFactParts.SourceKind</c>, C02, C04).</summary>
public enum AccountingFactPartSource
{
    DailyCycleBatch = 1,
    LoanCatchUp = 2,
    NationalValueCorrectionBatch = 3,
    MonthlyClose = 4,
    CloseReopening = 5,

    /// <summary>Feature 014 (T014; data-model §1.8, research E05): la puesta al día de una cuenta de captación.</summary>
    AccountCatchUp = 6,
}

/// <summary>Estado de una carga de migración (los estados de DT §2.3; el tipo no tenía nombre).</summary>
public enum MigrationLoadStatus
{
    Draft = 1,
    Applied = 2,
    Replaced = 3,
    Activated = 4,
}

/// <summary>Estado de una tanda importada de una parte (C64).</summary>
public enum MigrationPartStatus
{
    Imported = 1,
    Superseded = 2,
}

/// <summary>Clase de diferencia de una carga (FR-141, FR-143, D29).</summary>
public enum MigrationDifferenceKind
{
    BalanceCheck = 1,
    SaleNotMigrated = 2,
    SaleOutsideLending = 3,
}

/// <summary>Desenlace de la conciliación de una venta acumulada hasta el corte (FR-143).</summary>
public enum SaleReconciliationOutcome
{
    Migrated = 1,
    CancelledBeforeCutoff = 2,
    NotMigratedWithBalance = 3,
    OutsideLendingD29 = 4,
}
