using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;

/// <summary>
/// <c>LND_NegotiatedRateRules</c> (feature 014, K9, US6, T169; data-model §2.4; FR-127, D34, R13): una versión de una regla de tasa negociada de una línea de
/// captación, con vigencia propia (versionada por <see cref="Code"/>, independiente de la versión de la línea). Fija los criterios combinables (nulo = no
/// restringe) y el margen máximo sobre la tabla en puntos EA. Terminar una regla es una versión nueva con <see cref="IsActive"/> en falso. Hecho de sólo
/// inserción: la escribe <c>Lending.DepositLines.Manage</c>. (nuevo)
/// </summary>
public class NegotiatedRateRule : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public const int LargoDelMotivo = 300;
    public const int LargoDelCanal = 20;

    public int CreditLineId { get; init; }

    /// <summary>La identidad de la regla dentro de la línea (<c>CodigoDeCatalogo</c>).</summary>
    public string Code { get; init; } = string.Empty;

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Falso: la versión termina la regla desde <see cref="ValidFrom"/>.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Desempate entre reglas igual de específicas (menor gana).</summary>
    public int Priority { get; init; }

    public int? MinSeniorityMonths { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public int? MinTermDays { get; init; }

    public int? MaxTermDays { get; init; }

    public decimal? MinBaseRate { get; init; }

    public decimal? MaxBaseRate { get; init; }

    public decimal? MinHolderTotalBalance { get; init; }

    public decimal? MaxHolderTotalBalance { get; init; }

    public int? BranchId { get; init; }

    public string? Channel { get; init; }

    /// <summary>El margen máximo sobre la tabla (puntos EA como fracción; &gt; 0).</summary>
    public decimal MaxSpread { get; init; }

    /// <summary>Si la prórroga conserva el margen de esta regla; nulo = lo que diga la versión de la línea (D34: por defecto no).</summary>
    public bool? KeepsMarginOnRollover { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>La regla para el motor puro <see cref="TasaNegociada"/>.</summary>
    public ReglaDeTasaNegociada ParaElMotor() => new(Id, Code, Priority, MinSeniorityMonths, MinAmount, MaxAmount, MinTermDays, MaxTermDays, MinBaseRate,
        MaxBaseRate, MinHolderTotalBalance, MaxHolderTotalBalance, BranchId, Channel, MaxSpread);
}
