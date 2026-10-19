namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US5 (T139; contracts/api.md §4 de la 014): lo que la ficha de una cuenta manda y recibe por /api/lending/accounts/{id}/settlements y
// /close-preview y /close —las liquidaciones de intereses, la simulación a una fecha y la cancelación—. Los enums llegan como número. (nuevo)

/// <summary>Una liquidación de intereses de la cuenta.</summary>
public sealed record LiquidacionDeInteresesDto(
    Guid PublicId,
    Guid OperationPublicId,
    string OperationNumber,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal Accrued,
    decimal Settled,
    decimal RoundingAdjustment,
    decimal WithholdingBase,
    decimal? InflationaryComponent,
    decimal WithholdingRate,
    decimal Withholding,
    string? WithholdingExemptionReason,
    bool ThresholdReached,
    decimal Net,
    DateTime SettledAt,
    string Origin,
    bool Reversed)
{
    public bool AlreadyExisted { get; init; }
}

/// <summary>Un día de la explicación de la simulación.</summary>
public sealed record DiaDeInteresDto(DateOnly Date, decimal Balance, decimal Base, string RateTier, decimal AnnualRate, decimal DailyRate, decimal Interest,
    bool BelowMinimum, bool Suspended, string Explanation);

/// <summary>La retención de una simulación.</summary>
public sealed record RetencionDeInteresesDto(decimal Base, decimal? InflationaryComponentFraction, decimal InflationaryComponent, decimal TaxableBase,
    decimal Rate, decimal? Threshold, bool ThresholdApplies, IReadOnlyList<DateOnly> ThresholdReachedOn, decimal Amount, string? ExemptionReason,
    string HolderTaxProfile, string Explanation);

/// <summary>La simulación de la liquidación a una fecha (sin efectos).</summary>
public sealed record SimulacionDeInteresesDto(DateOnly Date, DateTime RecordedAsOf, DateOnly PeriodFrom, DateOnly PeriodTo, decimal Accrued, decimal Interest,
    decimal RoundingAdjustment, RetencionDeInteresesDto Withholding, decimal Net, IReadOnlyList<DiaDeInteresDto> Explanation);

public sealed record SimularInteresesRequest(DateOnly Date, DateTime? RecordedAsOf = null);

public sealed record LiquidarInteresesRequest(DateOnly PeriodTo, string Reason);

/// <summary>Lo que impide cancelar.</summary>
public sealed record BloqueoDeCancelacionDto(string Kind, string Detail);

/// <summary>La vista previa de la cancelación.</summary>
public sealed record VistaPreviaDeCancelacionDto(DateOnly Date, SimulacionDeInteresesDto? Settlement, decimal BalanceAfterSettlement, decimal Gmf,
    decimal PendingCharges, decimal ToDeliver, IReadOnlyList<BloqueoDeCancelacionDto> Blockers);

/// <summary>Cómo se entrega el saldo: 1 traslado a otra cuenta, 2 orden de giro.</summary>
public sealed record EntregaDeCancelacionRequest(int Kind, Guid? TargetAccountPublicId, BeneficiarioDeGiroRequest? Payee);

/// <summary>El beneficiario de una orden de giro (la persona; sin banco, la cuenta de depósito registrada).</summary>
public sealed record BeneficiarioDeGiroRequest(Guid PersonPublicId, Guid? BankPublicId = null, byte? AccountType = null, string? AccountNumber = null);

public sealed record CancelarCuentaRequest(DateOnly? Date, EntregaDeCancelacionRequest Delivery, IReadOnlyList<Guid> Signatures, string Reason);

/// <summary>Lo que dejó la cancelación.</summary>
public sealed record CancelacionDeCuentaDto(Guid OperationPublicId, string OperationNumber, LiquidacionDeInteresesDto? Settlement, ResultadoDeMovimientoDto? Delivery,
    decimal Delivered, int Status);

/// <summary>Los textos de la retención y de la cancelación.</summary>
public static class TextosDeIntereses
{
    public const int EntregaPorTraslado = 1;
    public const int EntregaPorGiro = 2;

    public static string MotivoSinRetencion(string? motivo) => motivo switch
    {
        null => "Se retuvo",
        "BelowThreshold" => "Ningún día alcanzó el umbral diario",
        "NonTaxpayer" => "No contribuyente",
        "SelfWithholder" => "Autorretenedor",
        "ZeroRate" => "Tarifa cero",
        "NoInterest" => "Sin interés",
        _ => motivo,
    };

    public static string Bloqueo(string clase) => clase switch
    {
        "Garnishment" => "Embargo",
        "Hold" => "Bloqueo",
        "PendingClearance" => "Valores en canje",
        "TechnicalOverdraft" => "Sobregiro técnico",
        "Deceased" => "Titular fallecido",
        "PendingCharges" => "Cargos por cobrar",
        _ => clase,
    };
}
