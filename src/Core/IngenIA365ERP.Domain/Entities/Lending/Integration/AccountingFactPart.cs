using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Integration;

/// <summary>
/// <c>LND_AccountingFactParts</c> (feature 013, C.3; data-model §22.3; FR-125, FR-128, FR-130; T12, T14, T47, T56): parte de una
/// tanda de hechos contables, el <b>origen</b> de su mensaje en la bandeja (nunca el crédito). Una parte = fuente × fecha ×
/// sucursal contable × tipo de hecho × tanda. Hecho de sólo inserción (<see cref="IHechoInmutable"/>) sin diff de auditoría
/// (C46; el evento resumen es el de la corrida). <see cref="MonthlyCloseId"/> y <see cref="MonthlyCloseReopeningId"/> llevan
/// FK desde <c>CarteraRiesgo</c> (C66).
/// </summary>
[SinDiffDeAuditoria]
public class AccountingFactPart : AuditableEntity, IHechoInmutable
{
    public AccountingFactPartSource SourceKind { get; init; }

    /// <summary>Nulo sólo en <c>LoanCatchUp</c>.</summary>
    public int? ProcessRunId { get; init; }

    /// <summary>Nulo sólo en <c>LoanCatchUp</c>.</summary>
    public int? BatchNumber { get; init; }

    /// <summary><c>LoanCatchUp</c>: la operación que pidió poner al día el crédito.</summary>
    public long? TriggerOperationId { get; init; }

    /// <summary>Partes del cierre (T47). FK desde <c>CarteraRiesgo</c> (C66).</summary>
    public int? MonthlyCloseId { get; init; }

    /// <summary>Partes del reverso de cierre. FK desde <c>CarteraRiesgo</c> (C66).</summary>
    public int? MonthlyCloseReopeningId { get; init; }

    /// <summary>La parte del cierre que revierte: el <c>Related</c> del mensaje (T12).</summary>
    public int? ReversedPartId { get; init; }

    /// <summary>La <c>{versión}</c> de <c>Close:</c> / <c>Reopen:</c>.</summary>
    public int? CloseVersion { get; init; }

    /// <summary>El <c>Type</c> del mensaje.</summary>
    public string FactType { get; init; } = string.Empty;

    /// <summary>Falso = un mensaje (masivo); verdadero = un mensaje por crédito (C02).</summary>
    public bool PerLoanMessages { get; init; }

    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha contable de T36: la causación de d se fecha d; el cierre de M, el último día de M.</summary>
    public DateOnly AccountingDate { get; init; }

    /// <summary>Sucursal contable del sobre (T13).</summary>
    public int BranchId { get; init; }

    public string OriginEventKey { get; init; } = string.Empty;

    /// <summary>El mensaje masivo (sin FK); nulo con <see cref="PerLoanMessages"/>.</summary>
    public Guid? MessagePublicId { get; init; }

    public int MessageCount { get; init; }

    public int LoanCount { get; init; }

    /// <summary>Suma con signo de la parte.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>JSON con los totales por combinación de dimensiones de FR-126 y concepto.</summary>
    public string TotalsByDimensionJson { get; init; } = "[]";

    /// <summary>Huella de la lista por crédito que lleva el contenido del mensaje (no se duplica aquí).</summary>
    public string LoanListSha256 { get; init; } = string.Empty;

    /// <summary>Texto sellado de la validación previa contable (T56).</summary>
    public string? PrevalidationOutcome { get; init; }
}
