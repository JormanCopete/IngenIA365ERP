namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ObligacionRechazada</c> v1 (§11.2; <c>CAR → Inventory</c>): una por evaluación negativa de conversión, con el motivo vigente.
/// Origen el recibo, clave <c>Confirmation:{evaluación:N}</c>, <c>CadenaDeOrden</c> = la venta. Admite proceso.
/// </summary>
public sealed record ObligacionRechazadaV1
{
    public const string Type = "ObligacionRechazada";

    /// <summary>El mensaje rechazado.</summary>
    public SaleMessageRefV1 SaleMessage { get; init; } = new();

    public ExternalDocumentRefV1 Invoice { get; init; } = new();

    public Guid CreditPaymentPublicId { get; init; }

    /// <summary><c>LND_InboundMessageEvaluations.PublicId</c>.</summary>
    public Guid EvaluationPublicId { get; init; }

    /// <summary><c>NoConvertible</c>, <c>AceptadaSinMigrar</c>, <c>AceptadaFueraDeCartera</c> o <c>SinObligacion</c>.</summary>
    public string Motive { get; init; } = string.Empty;

    /// <summary>Código del error.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Motivo en español, que dice qué hacer (FR-013).</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Si queda para reproceso.</summary>
    public bool Retryable { get; init; }
}
