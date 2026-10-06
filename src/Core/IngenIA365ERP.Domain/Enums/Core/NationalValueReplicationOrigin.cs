namespace IngenIA365ERP.Domain.Enums.Core;

// Valores nacionales en la base de cada cooperativa (feature 013, T39, T41, R4, R7; data-model parte 3 §4.3, §4.4, §9.2).
// Se guarda como int y un valor nunca se renumera.

/// <summary>Quién disparó una réplica de valores nacionales (<c>COR_NationalValueReplications.Origin</c>).</summary>
public enum NationalValueReplicationOrigin
{
    /// <summary>La tarea programada <c>valores-nacionales.replicar</c>.</summary>
    ScheduledTask = 1,

    /// <summary>La semilla <c>NationalValuesReplicaSeeder</c> al dar de alta la cooperativa.</summary>
    TenantSeeder = 2,

    /// <summary>El primer paso del ciclo diario de Cartera.</summary>
    DailyCycle = 3,

    /// <summary>El paso previo del cierre mensual de Cartera.</summary>
    MonthlyClose = 4,
}
