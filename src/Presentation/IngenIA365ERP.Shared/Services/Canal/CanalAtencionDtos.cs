namespace IngenIA365ERP.Shared.Services.Canal;

// Feature 021, US12 y US7 (T122): lo que la pantalla de Atención y la de Indicadores leen de la API (contracts/erp-administracion.md). (nuevo)

/// <summary>Un dispositivo de confianza de la cuenta del canal.</summary>
public sealed record DispositivoDelCanalDto(Guid DeviceId, string Kind, string? Label, DateTime? LastSeenAt, bool Revoked);

/// <summary>La cuenta del canal de la persona buscada (contacto enmascarado).</summary>
public sealed record CuentaDelCanalDto(Guid AccountId, string Status, DateTime? ActivatedAt, DateTime? LastLoginAt, DateTime? LockedUntil, string? EmailMasked,
    string? MobileMasked, IReadOnlyList<DispositivoDelCanalDto> Devices, long OpenSessions = 0, string? EnablementStatus = null);

/// <summary>Un intento del canal con lo que el ERP hizo con él.</summary>
public sealed record IntentoDeAtencionDto(string Reference, Guid PersonPublicId, string Status, decimal Total, DateTime CreatedAt, DateTime? FinishedAt,
    string? Authorization, string? PaymentMethodName, string ErpOutcome, string? NoticeOutcome, string? NoticeReason, Guid? ReceiptPublicId,
    string? ReceiptNumber, string? ReceiptStatus, DateTime? ReversedAt, string? ReversalKind, bool CanReverse);

public sealed record AtencionDelCanalDto(CuentaDelCanalDto? Account, IReadOnlyList<IntentoDeAtencionDto> Attempts, bool ChannelAvailable, string? ChannelError);

public sealed record OrdenDeDevolucionDto(Guid PaymentOrderPublicId, string Number, decimal Amount);

/// <summary>El reverso de un pago del canal.</summary>
public sealed record ReversoDelCanalDto(string Reference, string Mode, DateTime ReversedAt, string? Reason, Guid? ReceiptPublicId, string? ReceiptNumber,
    Guid? VoidingPublicId, string? VoidingNumber, IReadOnlyList<OrdenDeDevolucionDto> RefundOrders, bool ChannelUpdated);

public sealed record MetricasDelCanalDto(int CuentasActivas, int Ingresos, int IntentosAprobados, int IntentosRechazados, int IntentosPendientes,
    decimal ValorAprobado, IReadOnlyDictionary<string, int> RechazosPorMotivo, int Habilitadas = 0, int IntentosReversados = 0);

/// <summary>Los indicadores del canal junto a las cifras del ERP.</summary>
public sealed record IndicadoresDelCanalDto(DateOnly From, DateOnly To, MetricasDelCanalDto? Channel, string? ChannelError, int EnabledPeople,
    int NoticesApplied, int NoticesInReview, decimal ValueApplied, int Reversals);
