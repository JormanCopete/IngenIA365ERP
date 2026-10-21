using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_ArrearsHistoryLoads</c> (feature 013, K6a, T1031; data-model §23.9; FR-140, US8-10; T49): una carga complementaria del historial de
/// mora (plantilla <c>lending.arrears-history</c>) de los créditos de la carga de migración aplicada, como información: sin transacciones ni
/// hechos. Todo o nada; cada aplicación es una carga nueva con la huella del archivo, y la más reciente manda por mes sobre lo migrado.
/// <para>Se admite también <b>antes de activar</b>, cuando los créditos todavía no existen (nacen en la activación): por eso la carga guarda sus
/// filas normalizadas en <see cref="RowsJson"/> (número del crédito, mes, moras) y la activación las lleva a <c>LND_LoanArrearsHistory</c> con
/// origen <c>ComplementaryLoad</c> y este <c>Id</c>; después de activar las escribe la carga misma. (Enmienda de data-model §23.9 del bloque 2
/// de K6a: <see cref="RowsJson"/> y <see cref="Source"/>, la columna <c>fuente</c> de la plantilla.)</para>
/// Hecho sin diferencias de auditoría (C46): el evento resumen es el de la importación. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class ArrearsHistoryLoad : AuditableEntity, IHechoInmutable
{
    /// <summary>La carga aplicada (o activada) cuyos créditos complementa.</summary>
    public int MigrationLoadId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FileSha256 { get; init; } = string.Empty;

    public int RowCount { get; init; }

    /// <summary>La columna <c>fuente</c> del archivo (la misma en todas las filas): de dónde salió el historial.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Las filas normalizadas de la carga (<c>[{ loanNumber, year, month, maxDaysPastDue, daysPastDueAtMonthEnd }]</c>).</summary>
    public string RowsJson { get; init; } = "[]";

    /// <summary><c>Lending.ArrearsHistory.Import</c>.</summary>
    public int AppliedByUserId { get; init; }

    public DateTime AppliedAt { get; init; }

    /// <summary>Los créditos que quedan con historial completo y los que siguen incompletos, por segmento cubierto.</summary>
    public string CoverageJson { get; init; } = "{}";
}
