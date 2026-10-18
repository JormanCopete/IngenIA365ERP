using System.Text.Json;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;

/// <summary>
/// <c>LND_DepositLineCharges</c> (feature 014, US4, T114; data-model §2.3; FR-128, D35, R14): el catálogo de cargos de una versión de una línea de
/// captación. <b>Apagado por defecto</b>: la semilla no crea filas y cada cargo nace con <see cref="IsEnabled"/> como lo pida la versión. Hija de la
/// versión (<c>DepositLineVersionId</c>), hecho de sólo inserción como ella: cambiar un cargo es registrar otra versión de la línea. El
/// <see cref="Code"/> es estable entre versiones (es el <c>ReasonCode</c> de <c>IngresoPorCargos</c>). Lo calcula <see cref="CargosDeCuenta"/>. (nuevo)
/// </summary>
public class DepositLineCharge : AuditableEntity, IHechoInmutable
{
    /// <summary>Largo del código (<c>CodigoDeCatalogo</c>, <c>nvarchar(10)</c>).</summary>
    public const int LargoDelCodigo = 10;

    public const int LargoDelNombre = 120;

    /// <summary>Largo del código del servicio en Comercial (<c>nvarchar(30)</c>).</summary>
    public const int LargoDelItemComercial = 30;

    public int DepositLineVersionId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ChargeKind Kind { get; init; }

    public bool IsEnabled { get; init; }

    public ChargeTrigger Trigger { get; init; }

    /// <summary>Obligatoria con <c>Periodic</c> e <c>Inactivity</c>.</summary>
    public RepaymentPeriodicity? Periodicity { get; init; }

    /// <summary>Gratuitos por cuenta y período (D35).</summary>
    public int? FreeOperationsPerPeriod { get; init; }

    public RepaymentPeriodicity? FreePeriod { get; init; }

    public ChargeCalculation CalculationMode { get; init; }

    public decimal? FixedAmount { get; init; }

    public decimal? Percentage { get; init; }

    public ChargeBase? PercentageBase { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public decimal? ExemptIfAverageBalanceAtLeast { get; init; }

    public decimal? ExemptIfMinimumBalanceAtLeast { get; init; }

    /// <summary>Antigüedad como asociado, en meses.</summary>
    public int? ExemptIfSeniorityMonthsAtLeast { get; init; }

    public bool ExemptEmployees { get; init; }

    public bool ExemptGmfExemptAccount { get; init; }

    public bool ExemptMinors { get; init; }

    /// <summary>JSON con los <c>PublicId</c> de las oficinas exoneradas; nulo = ninguna.</summary>
    public string? ExemptBranchPublicIdsJson { get; init; }

    public ChargeShortfallPolicy ShortfallPolicy { get; init; } = ChargeShortfallPolicy.KeepPending;

    /// <summary>Gravado con IVA: la Cartera emite <c>CargoDeCaptacionCausado</c> y Comercial factura (R14).</summary>
    public bool IsVatTaxed { get; init; }

    /// <summary>El código del servicio en Comercial; obligatorio con <see cref="IsVatTaxed"/>.</summary>
    public string? CommercialItemCode { get; init; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    public IReadOnlyList<Guid> LeerOficinasExoneradas() => string.IsNullOrWhiteSpace(ExemptBranchPublicIdsJson)
        ? []
        : JsonSerializer.Deserialize<List<Guid>>(ExemptBranchPublicIdsJson) ?? [];

    /// <summary>Lo que el motor necesita del cargo.</summary>
    public DefinicionDeCargo Definicion() => new(Code, Kind, Trigger, CalculationMode, FixedAmount, Percentage, PercentageBase, MinAmount, MaxAmount,
        FreeOperationsPerPeriod, ExemptIfAverageBalanceAtLeast, ExemptIfMinimumBalanceAtLeast, ExemptIfSeniorityMonthsAtLeast, ExemptEmployees,
        ExemptGmfExemptAccount, ExemptMinors, LeerOficinasExoneradas(), ShortfallPolicy, IsVatTaxed);

    /// <summary>El cargo de una versión ya guardada; lo valida la versión entera (<see cref="ReglasDeLineaDeCaptacion"/>).</summary>
    public static DepositLineCharge Nuevo(int depositLineVersionId, BorradorDeCargoDeCaptacion c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var oficinas = c.ExemptBranchPublicIds.Distinct().Order().ToList();
        return new DepositLineCharge
        {
            DepositLineVersionId = depositLineVersionId,
            Code = c.Code.Trim().ToUpperInvariant(),
            Name = c.Name.Trim(),
            Kind = c.Kind,
            IsEnabled = c.IsEnabled,
            Trigger = c.Trigger,
            Periodicity = c.Periodicity,
            FreeOperationsPerPeriod = c.FreeOperationsPerPeriod,
            FreePeriod = c.FreePeriod,
            CalculationMode = c.CalculationMode,
            FixedAmount = c.FixedAmount,
            Percentage = c.Percentage,
            PercentageBase = c.PercentageBase,
            MinAmount = c.MinAmount,
            MaxAmount = c.MaxAmount,
            ExemptIfAverageBalanceAtLeast = c.ExemptIfAverageBalanceAtLeast,
            ExemptIfMinimumBalanceAtLeast = c.ExemptIfMinimumBalanceAtLeast,
            ExemptIfSeniorityMonthsAtLeast = c.ExemptIfSeniorityMonthsAtLeast,
            ExemptEmployees = c.ExemptEmployees,
            ExemptGmfExemptAccount = c.ExemptGmfExemptAccount,
            ExemptMinors = c.ExemptMinors,
            ExemptBranchPublicIdsJson = oficinas.Count == 0 ? null : JsonSerializer.Serialize(oficinas),
            ShortfallPolicy = c.ShortfallPolicy,
            IsVatTaxed = c.IsVatTaxed,
            CommercialItemCode = string.IsNullOrWhiteSpace(c.CommercialItemCode) ? null : c.CommercialItemCode.Trim(),
        };
    }
}
